using System.Reflection;
using Modules.Tax.DTOs;
using Modules.Tax.Entities;
using Modules.Tax.Repositories;
using Modules.Tax.Services;
using Shared.Contracts.Catalog;
using Shared.Contracts.Directory;
using Shared.Contracts.FinanceBase;
using Shared.Contracts.Tax;
using Shared.Primitives.Identifiers;
using Xunit;

namespace Tax.Tests;

public sealed class TaxBatchFastPathTests
{
    [Fact]
    public async Task SnapshotCaptureIsIdempotentWhenMasterCapabilityChangesAfterCapture()
    {
        var tenantId = Guid.Parse("10000000-0000-7000-8000-000000000001");
        var entityId = Guid.Parse("20000000-0000-7000-8000-000000000001");
        var repository = new CountingRepository();
        var service = new TaxService(repository,
            DispatchProxy.Create<ICatalogCapabilities, ForbiddenCallProxy>(),
            DispatchProxy.Create<IQuantityConversion, ForbiddenCallProxy>(),
            DispatchProxy.Create<IDirectoryCapabilities, ForbiddenCallProxy>(),
            DispatchProxy.Create<IFinanceBaseCapabilities, ForbiddenCallProxy>(),
            new TaxCountryPackRegistry([]));
        var context = new TaxContextContract(new TenantId(tenantId), new LegalEntityId(entityId), "Sales", "Invoice", "Domestic",
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), null, null, null, null, null, null, null, null, 1m, null,
            "TaxExclusive", "100", "0", "0", "EGP", "EGP", null);
        var capture = new TaxSnapshotCaptureContract("Invoice", Guid.Parse("40000000-0000-7000-8000-000000000001"), Guid.Parse("50000000-0000-7000-8000-000000000001"), context);

        var original = await service.CaptureSnapshotAsync(capture);
        repository.Capability = "Enabled";
        var repeated = await service.CaptureSnapshotAsync(capture);

        Assert.Equal("NotEvaluatedBecauseCapabilityDisabled", original.EvaluationState);
        Assert.Equal(original, repeated);
        Assert.Equal(1, repository.CapabilityReads);
    }

    [Fact]
    public async Task SnapshotEvidenceRemainsStableAfterActiveRuleChanges()
    {
        var tenantId = Guid.Parse("10000000-0000-7000-8000-000000000001");
        var entityId = Guid.Parse("20000000-0000-7000-8000-000000000001");
        var repository = new CountingRepository { Capability = "Enabled" };
        var finance = DispatchProxy.Create<IFinanceBaseCapabilities, CountingFinanceProxy>();
        ((CountingFinanceProxy)(object)finance).Tenant = new TenantId(tenantId);
        ((CountingFinanceProxy)(object)finance).Entity = new LegalEntityId(entityId);
        ((CountingFinanceProxy)(object)finance).ExchangeRate = new ExchangeRateSnapshotContract(tenantId, entityId, Guid.Parse("94000000-0000-7000-8000-000000000001"), "USD", "EGP", "30.00", FinancialCalculationPurpose.Tax, new DateOnly(2026, 1, 1), "ENGINE-TEST-FIXTURE", 7, false, DateTimeOffset.UnixEpoch);
        var service = new TaxService(repository,
            DispatchProxy.Create<ICatalogCapabilities, ForbiddenCallProxy>(),
            DispatchProxy.Create<IQuantityConversion, ForbiddenCallProxy>(),
            DispatchProxy.Create<IDirectoryCapabilities, ForbiddenCallProxy>(), finance, new TaxCountryPackRegistry([]));
        var context = new TaxContextContract(new TenantId(tenantId), new LegalEntityId(entityId), "Sales", "Invoice", "Domestic", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), null, null, null, "EG", "EG", "EG-C", null, null, 1m, null, "TaxExclusive", "100", "0", "0", "USD", "EGP", null, "VAT");
        var capture = new TaxSnapshotCaptureContract("Invoice", Guid.Parse("40000000-0000-7000-8000-000000000002"), Guid.Parse("50000000-0000-7000-8000-000000000002"), context);

        var original = await service.CaptureSnapshotAsync(capture);
        repository.Rate = 12m;
        var changed = await service.CalculateAsync(context);
        var reloaded = await service.CaptureSnapshotAsync(capture);

        Assert.Equal("10", original.Result.TaxTotal);
        Assert.Equal("12", changed.TaxTotal);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(original), System.Text.Json.JsonSerializer.Serialize(reloaded));
        Assert.Equal("Evaluated", original.Evidence!.CapabilityState);
        Assert.Equal("EG-C", original.Evidence.JurisdictionSnapshot);
        Assert.Contains("ENGINE-TEST-FIXTURE", original.Result.TaxFxSnapshot, StringComparison.Ordinal);
        Assert.Equal(original.Result.TaxFxSnapshot, original.Evidence.TaxFxSnapshot);
    }

    [Fact]
    public async Task DisabledFiveHundredLineBatchUsesOneCapabilityReadAndSkipsTaxResolution()
    {
        var tenantId = Guid.Parse("10000000-0000-7000-8000-000000000001");
        var entityId = Guid.Parse("20000000-0000-7000-8000-000000000001");
        var repository = new CountingRepository();
        var service = new TaxService(
            repository,
            DispatchProxy.Create<ICatalogCapabilities, ForbiddenCallProxy>(),
            DispatchProxy.Create<IQuantityConversion, ForbiddenCallProxy>(),
            DispatchProxy.Create<IDirectoryCapabilities, ForbiddenCallProxy>(),
            DispatchProxy.Create<IFinanceBaseCapabilities, ForbiddenCallProxy>(),
            new TaxCountryPackRegistry([]));
        var contexts = Enumerable.Range(1, 500).Select(index => new TaxContextContract(
            new TenantId(tenantId), new LegalEntityId(entityId), "Sales", "Invoice", "Domestic",
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), null, null,
            null, null, null, null, null, null, 1m, null, "TaxExclusive",
            (index * 2).ToString(System.Globalization.CultureInfo.InvariantCulture), "0", "0", "EGP", "EGP", null)).ToArray();

        var results = await service.CalculateBatchAsync(contexts);

        Assert.Equal(500, results.Count);
        Assert.Equal(1, repository.CapabilityReads);
        Assert.Equal(0, repository.PolicyReads);
        Assert.Equal(0, repository.ProfileReads);
        Assert.Equal(0, repository.RuleReads);
        Assert.Equal(0, repository.ProfileBatchReads);
        Assert.Equal(0, repository.RuleBatchReads);
        Assert.All(results, result => Assert.Equal("NotEvaluatedBecauseCapabilityDisabled", result.EvaluationState));
        Assert.All(results, result => Assert.Empty(result.TaxLines));
        Assert.Equal("2", results[0].NetAmount);
        Assert.Equal("1000", results[^1].NetAmount);
    }

    [Fact]
    public async Task EnabledFiveHundredLineBatchUsesSharedContextAndMatchesSingleCalculation()
    {
        var tenantId = Guid.Parse("10000000-0000-7000-8000-000000000001");
        var entityId = Guid.Parse("20000000-0000-7000-8000-000000000001");
        var partyId = Guid.Parse("30000000-0000-7000-8000-000000000001");
        var categoryId = Guid.Parse("60000000-0000-7000-8000-000000000001");
        var productIds = Enumerable.Range(1, 500).Select(index => Guid.Parse($"70000000-0000-7000-8000-{index:000000000000}" )).ToArray();
        var repository = new CountingRepository { Capability = "Enabled", CategoryId = categoryId };
        repository.ProductTreatments = productIds.Select((id,index) => (id, (index % 4) switch { 0 => TaxTreatment.StandardRated, 1 => TaxTreatment.ZeroRated, 2 => TaxTreatment.Exempt, _ => TaxTreatment.OutOfScope })).ToDictionary(item => item.id, item => item.Item2);
        var catalog = DispatchProxy.Create<ICatalogCapabilities, CountingCatalogProxy>();
        ((CountingCatalogProxy)(object)catalog).Products = productIds.ToDictionary(id => id, id => new CatalogVariantPricingContext(id, Guid.Parse("80000000-0000-7000-8000-000000000001"), categoryId, [categoryId], null, "Active"));
        var directory = DispatchProxy.Create<IDirectoryCapabilities, CountingDirectoryProxy>();
        var finance = DispatchProxy.Create<IFinanceBaseCapabilities, CountingFinanceProxy>();
        ((CountingFinanceProxy)(object)finance).Tenant = new TenantId(tenantId);
        ((CountingFinanceProxy)(object)finance).Entity = new LegalEntityId(entityId);
        var service = new TaxService(repository, catalog, DispatchProxy.Create<IQuantityConversion, ForbiddenCallProxy>(), directory, finance, new TaxCountryPackRegistry([]));
        var inputs = productIds.Select((id,index) => ContextContract(tenantId, entityId, partyId, id, index)).ToArray();

        var batch = await service.CalculateBatchAsync(inputs);

        Assert.Equal(500, batch.Count);
        Assert.Equal(1, repository.CapabilityReads);
        Assert.Equal(1, repository.PolicyReads);
        Assert.Equal(0, repository.ProfileReads);
        Assert.Equal(1, repository.ProfileBatchReads);
        Assert.Equal(0, repository.RuleReads);
        Assert.Equal(1, repository.RuleBatchReads);
        Assert.Equal(1, ((CountingCatalogProxy)(object)catalog).BatchCalls);
        Assert.Equal(1, ((CountingDirectoryProxy)(object)directory).BatchCalls);
        Assert.Equal(1, ((CountingFinanceProxy)(object)finance).Calls);
        Assert.Contains(batch, result => result.Treatment == "StandardRated");
        Assert.Contains(batch, result => result.Treatment == "ZeroRated");
        Assert.Contains(batch, result => result.Treatment == "Exempt");
        Assert.Contains(batch, result => result.Treatment == "OutOfScope");

        for (var index = 0; index < inputs.Length; index++)
        {
            var single = await service.CalculateAsync(inputs[index]);
            Assert.Equal(System.Text.Json.JsonSerializer.Serialize(single), System.Text.Json.JsonSerializer.Serialize(batch[index]));
        }
    }

    [Fact]
    public async Task PerDocumentRoundingReconcilesLineRoundingToTheDocumentTotal()
    {
        var tenantId = Guid.Parse("10000000-0000-7000-8000-000000000001");
        var entityId = Guid.Parse("20000000-0000-7000-8000-000000000001");
        var partyId = Guid.Parse("30000000-0000-7000-8000-000000000001");
        var productIds = new[] { Guid.Parse("70000000-0000-7000-8000-000000000001"), Guid.Parse("70000000-0000-7000-8000-000000000002") };
        var categoryId = Guid.Parse("60000000-0000-7000-8000-000000000001");
        var repository = new CountingRepository { Capability = "Enabled", CategoryId = categoryId, RoundingStrategy = TaxRoundingStrategy.PerDocument };
        var catalog = DispatchProxy.Create<ICatalogCapabilities, CountingCatalogProxy>();
        ((CountingCatalogProxy)(object)catalog).Products = productIds.ToDictionary(id => id, id => new CatalogVariantPricingContext(id, Guid.Parse("80000000-0000-7000-8000-000000000001"), categoryId, [categoryId], null, "Active"));
        var finance = DispatchProxy.Create<IFinanceBaseCapabilities, CountingFinanceProxy>();
        ((CountingFinanceProxy)(object)finance).Tenant = new TenantId(tenantId);
        ((CountingFinanceProxy)(object)finance).Entity = new LegalEntityId(entityId);
        var service = new TaxService(repository, catalog, DispatchProxy.Create<IQuantityConversion, ForbiddenCallProxy>(), DispatchProxy.Create<IDirectoryCapabilities, CountingDirectoryProxy>(), finance, new TaxCountryPackRegistry([]));
        var contexts = productIds.Select((id, index) => ContextContract(tenantId, entityId, partyId, id, index) with { CommercialAmount = "0.05" }).ToArray();

        var results = await service.CalculateBatchAsync(contexts);

        Assert.Equal("0.01", results.Sum(result => decimal.Parse(result.TaxTotal, System.Globalization.CultureInfo.InvariantCulture)).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("-0.01", results[1].RoundingEvidence!.Residual);
        Assert.Contains(":VAT-10", results[1].RoundingEvidence!.AllocationTarget);
        Assert.Equal("0", results[1].RoundingEvidence!.FinalTotal);
    }

    private static TaxContextContract ContextContract(Guid tenant, Guid entity, Guid party, Guid product, int index) => new(
        new TenantId(tenant), new LegalEntityId(entity), "Sales", "Invoice", "Domestic", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), null, null,
        party, "EG", "EG", "EG-C", Guid.Parse("80000000-0000-7000-8000-000000000001"), product, 1m, null, "TaxExclusive", (100 + index).ToString(System.Globalization.CultureInfo.InvariantCulture), "0", "0", "EGP", "EGP", null, "VAT");

    private sealed class CountingRepository : ITaxRepository
    {
        private readonly Dictionary<(string Kind, Guid Document, Guid? Line), TaxSnapshotContract> snapshots = [];
        public string Capability { get; set; } = "Disabled";
        public Guid CategoryId { get; set; } = Guid.Parse("60000000-0000-7000-8000-000000000001");
        public IReadOnlyDictionary<Guid, TaxTreatment> ProductTreatments { get; set; } = new Dictionary<Guid, TaxTreatment>();
        public decimal Rate { get; set; } = 10m;
        public TaxRoundingStrategy RoundingStrategy { get; set; } = TaxRoundingStrategy.PerLine;
        public int CapabilityReads { get; private set; }
        public int PolicyReads { get; private set; }
        public int ProfileReads { get; private set; }
        public int RuleReads { get; private set; }
        public int ProfileBatchReads { get; private set; }
        public int RuleBatchReads { get; private set; }

        public Task<TaxRepository.TaxPolicyResolution> GetPolicyAsync(Guid tenantId, Guid legalEntityId, CancellationToken cancellationToken)
        {
            PolicyReads++;
            if (Capability == "Disabled") throw new InvalidOperationException("Disabled calculations must not load Tax Policy when Tax Date is explicit.");
            return Task.FromResult(new TaxRepository.TaxPolicyResolution(new(TaxDateBasis.DocumentDate, "UTC", 1), RoundingStrategy, 2));
        }

        public Task<TaxCapabilityDto?> GetCapabilityAsync(Guid tenantId, Guid legalEntityId, DateOnly at, CancellationToken cancellationToken)
        {
            CapabilityReads++;
            return Task.FromResult<TaxCapabilityDto?>(new(legalEntityId, Capability, at, null, 1));
        }

        public Task<TaxRepository.TaxProfileResolution> ResolveProfilesAsync(Guid tenantId, TaxCalculationContext context, DateOnly taxDate, CancellationToken cancellationToken)
        {
            ProfileReads++;
            if (Capability == "Disabled") throw new InvalidOperationException("Disabled calculations must bypass registration and profile resolution.");
            return Task.FromResult(new TaxRepository.TaxProfileResolution(null, CategoryId, null, null, "NotRegistered"));
        }

        public Task<IReadOnlyDictionary<int, TaxRepository.TaxProfileResolution>> ResolveProfilesBatchAsync(Guid tenantId, IReadOnlyList<TaxBatchContext> contexts, CancellationToken cancellationToken)
        {
            ProfileBatchReads++;
            if (Capability == "Disabled") throw new InvalidOperationException("Disabled calculations must bypass profile batch resolution.");
            return Task.FromResult<IReadOnlyDictionary<int, TaxRepository.TaxProfileResolution>>(contexts.ToDictionary(item => item.Index, _ => new TaxRepository.TaxProfileResolution(null, CategoryId, null, null, "NotRegistered")));
        }

        public Task<IReadOnlyDictionary<int, (TaxRuleCandidate Rule, string PackCode, string PackVersion)>> FindRulesBatchAsync(Guid tenantId, IReadOnlyList<TaxBatchRuleContext> contexts, CancellationToken cancellationToken)
        {
            RuleBatchReads++;
            if (Capability == "Disabled") throw new InvalidOperationException("Disabled calculations must bypass rule batch resolution.");
            return Task.FromResult<IReadOnlyDictionary<int, (TaxRuleCandidate, string, string)>>(contexts.ToDictionary(item => item.Index, item => (TestRule(ProductTreatments.GetValueOrDefault(item.Context.ProductVariantId ?? Guid.Empty, TaxTreatment.StandardRated)), "ENGINE-TEST", "fixture-v1")));
        }

        public Task<TaxCapabilityDto> SetCapabilityAsync(Guid tenantId, Guid legalEntityId, Guid actorId, string capability, DateOnly effectiveFrom, Guid correlationId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<(TaxRuleCandidate Rule, string PackCode, string PackVersion)?> FindRuleAsync(Guid tenantId, TaxCalculationContext context, DateOnly taxDate, CancellationToken cancellationToken)
        {
            RuleReads++;
            if (Capability == "Disabled") throw new InvalidOperationException("Disabled calculations must bypass rule resolution.");
            return Task.FromResult<(TaxRuleCandidate, string, string)?>( (TestRule(ProductTreatments.GetValueOrDefault(context.ProductVariantId ?? Guid.Empty, TaxTreatment.StandardRated)), "ENGINE-TEST", "fixture-v1"));
        }

        public Task<TaxSnapshotContract?> FindSnapshotAsync(Guid tenantId, string sourceDocumentKind, Guid sourceDocumentId, Guid? sourceLineId, CancellationToken cancellationToken) =>
            Task.FromResult(snapshots.GetValueOrDefault((sourceDocumentKind, sourceDocumentId, sourceLineId)));

        public Task<TaxSnapshotContract> SaveSnapshotAsync(Guid tenantId, Guid legalEntityId, TaxSnapshotContract snapshot, CancellationToken cancellationToken)
        {
            var key = (snapshot.SourceDocumentKind, snapshot.SourceDocumentId, snapshot.SourceLineId);
            if (!snapshots.TryGetValue(key, out var existing)) snapshots[key] = snapshot;
            return Task.FromResult(snapshots[key]);
        }

        private TaxRuleCandidate TestRule(TaxTreatment treatment) => new(Guid.Parse("90000000-0000-7000-8000-000000000001"), treatment, 1, new DateOnly(2026, 1, 1), null, null, null, null, null, null, null, null, null, TaxRuleMutability.TenantConfiguration, "1", null, "Engine reference fixture", treatment is TaxTreatment.ZeroRated or TaxTreatment.Exempt or TaxTreatment.OutOfScope ? [] : [new(Guid.Parse("91000000-0000-7000-8000-000000000001"), "VAT", "VAT-10", TaxTreatment.StandardRated, TaxCalculationMethod.PercentageOfNet, Rate, null, "EGP", 1, null)]);
    }

    public class CountingCatalogProxy : DispatchProxy
    {
        public IReadOnlyDictionary<Guid, CatalogVariantPricingContext> Products { get; set; } = new Dictionary<Guid, CatalogVariantPricingContext>();
        public int BatchCalls { get; private set; }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(ICatalogCapabilities.GetPricingContextsBatchAsync)) { BatchCalls++; return Task.FromResult(Products); }
            if (targetMethod?.Name == nameof(ICatalogCapabilities.GetPricingContextAsync)) return Task.FromResult(Products.GetValueOrDefault((Guid)args![1]!));
            throw new NotSupportedException(targetMethod?.Name);
        }
    }

    public class CountingDirectoryProxy : DispatchProxy
    {
        public int BatchCalls { get; private set; }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IDirectoryCapabilities.FindActivePartiesAsync)) { BatchCalls++; return Task.FromResult<IReadOnlySet<Guid>>(new HashSet<Guid>((Guid[])args![1]!)); }
            if (targetMethod?.Name == nameof(IDirectoryCapabilities.IsActivePartyAsync)) return Task.FromResult(true);
            throw new NotSupportedException(targetMethod?.Name);
        }
    }

    public class CountingFinanceProxy : DispatchProxy
    {
        public TenantId Tenant { get; set; }
        public LegalEntityId Entity { get; set; }
        public ExchangeRateSnapshotContract? ExchangeRate { get; set; }
        public int Calls { get; private set; }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name != nameof(IFinanceBaseCapabilities.ResolveCalculationContextAsync)) throw new NotSupportedException(targetMethod?.Name);
            Calls++;
            var rounding = new FinancialRoundingPolicySnapshotContract(Guid.Parse("92000000-0000-7000-8000-000000000001"), 4, 2, "0.01", FinancialLineDocumentStrategy.RoundEachLineThenSum, [new(FinancialRoundingContext.LineNetAmount, FinancialRoundingMode.HalfUp), new(FinancialRoundingContext.DocumentTotal, FinancialRoundingMode.HalfUp)]);
            var policy = new FinancialPolicySnapshotContract(Entity.Value, Guid.Parse("93000000-0000-7000-8000-000000000001"), 30, false, 1);
            return Task.FromResult<FinancialCalculationContextSnapshotContract?>(new(Tenant, Entity, "EGP", (string)args![2]!, FinancialCalculationPurpose.Tax, (DateOnly)args[4]!, policy, rounding, ExchangeRate, DateTimeOffset.UnixEpoch));
        }
    }

    public class ForbiddenCallProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            throw new InvalidOperationException($"Disabled Tax fast path called {targetMethod?.Name}.");
    }
}

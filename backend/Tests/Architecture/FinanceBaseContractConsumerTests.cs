using System.Text.Json;
using System.Globalization;
using FinanceBase.ContractConsumers;
using Shared.Contracts.FinanceBase;
using Shared.Primitives.Identifiers;
using Xunit;

namespace ArchitectureTests;

public sealed class FinanceBaseContractConsumerTests
{
    private static readonly TenantId TenantId = new(Guid.Parse("10000000-0000-7000-8000-000000000001"));
    private static readonly LegalEntityId LegalEntityId = new(Guid.Parse("20000000-0000-7000-8000-000000000001"));
    private static readonly Guid PaymentTermId = Guid.Parse("30000000-0000-7000-8000-000000000001");
    private static readonly DateOnly BusinessDate = new(2026, 10, 20);

    [Fact]
    public async Task PricingConsumerUsesFinancialPrimitivesAndExplicitConversionContext()
    {
        var result = await new PricingConsumer(new ContractProvider()).ResolveAsync(TenantId, LegalEntityId, "USD", BusinessDate);

        Assert.NotNull(result);
        Assert.Equal("USD", result.Context.TransactionCurrencyCode);
        Assert.Equal("EGP", result.Context.BaseCurrencyCode);
        Assert.NotNull(result.Context.ExchangeRate);
        Assert.Equal(0.05m, result.Discount.Ratio);
        Assert.Equal(4.115m, result.UnitPrice.Amount);
    }

    [Fact]
    public async Task TaxConsumerUsesExactMoneyExplicitRoundingAndRateSnapshotContracts()
    {
        var result = await new TaxConsumer(new ContractProvider()).ResolveAsync(TenantId, LegalEntityId, "USD", BusinessDate);

        Assert.NotNull(result);
        Assert.Equal(100.125m, result.TaxableAmount.Amount);
        Assert.Equal(FinancialRoundingContext.DocumentTotal, result.RoundingContext);
        Assert.Equal(15m, result.Rate.Ratio * 100m);
        Assert.Equal(FinancialCalculationPurpose.Tax, result.Context.ExchangeRate!.Purpose);
    }

    [Fact]
    public async Task LedgerConsumerGetsEntityCurrencyPolicyAndAccountingRateThroughContract()
    {
        var result = await new LedgerConsumer(new ContractProvider()).ResolveAsync(TenantId, LegalEntityId, "USD", BusinessDate);

        Assert.NotNull(result);
        Assert.Equal(LegalEntityId, result.Context.LegalEntityId);
        Assert.Equal("EGP", result.Context.BaseCurrencyCode);
        Assert.Equal(FinancialCalculationPurpose.Accounting, result.Context.ExchangeRate!.Purpose);
        Assert.Equal(Guid.Parse("40000000-0000-7000-8000-000000000001"), result.Context.FinancialPolicy.RoundingPolicyId);
        Assert.Equal(250.75m, result.TransactionAmount.Amount);
        Assert.Equal(12537.50m, result.BaseAmount.Amount);
    }

    [Fact]
    public async Task InvoicingConsumerUsesVersionedPaymentTermsScheduleAndDecimalStringSerialization()
    {
        var result = await new InvoicingConsumer(new ContractProvider()).ResolveAsync(TenantId, LegalEntityId, PaymentTermId, "USD", BusinessDate);

        Assert.NotNull(result);
        Assert.Equal(Guid.Parse("50000000-0000-7000-8000-000000000001"), result.PaymentTerm.VersionId);
        Assert.Equal("0.5", result.PaymentTerm.Rules[0].PercentageRatio);
        Assert.Equal("100.25", result.FirstScheduleLine.Amount.Amount);
        Assert.Contains("\"Amount\":\"100.25\"", JsonSerializer.Serialize(result.FirstScheduleLine), StringComparison.Ordinal);
        Assert.NotNull(result.Context.ExchangeRate);
    }

    [Fact]
    public void ConsumerFixtureReferencesOnlyThePublicContractsProject()
    {
        var projectPath = Path.Combine(RepositoryRoot, "Tests", "Architecture", "fixtures", "backend", "finbase-contract-consumers", "FinanceBase.ContractConsumers.csproj");
        var project = System.Xml.Linq.XDocument.Load(projectPath);
        var references = project.Descendants("ProjectReference").Select(element => element.Attribute("Include")?.Value).ToArray();

        Assert.Single(references);
        Assert.Equal("../../../../../Shared/Contracts/Contracts.csproj", references[0]);
        Assert.DoesNotContain("FinanceBase.csproj", project.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Npgsql", project.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Backend.sln"))) directory = directory.Parent;
            return directory?.FullName ?? throw new InvalidOperationException("Unable to locate repository root.");
        }
    }

    private sealed class ContractProvider : IFinanceBaseCapabilities
    {
        public Task<FinancialCalculationContextSnapshotContract?> ResolveCalculationContextAsync(TenantId tenantId, LegalEntityId legalEntityId, string transactionCurrencyCode, FinancialCalculationPurpose purpose, DateOnly businessDate, CancellationToken cancellationToken = default)
        {
            var rate = transactionCurrencyCode == "EGP" ? null : new ExchangeRateSnapshotContract(tenantId.Value, legalEntityId.Value, Guid.Parse("60000000-0000-7000-8000-000000000001"), transactionCurrencyCode, "EGP", "50", purpose, businessDate, "approved-test-rate", 2, false, DateTimeOffset.Parse("2026-10-20T09:00:00Z", CultureInfo.InvariantCulture));
            return Task.FromResult<FinancialCalculationContextSnapshotContract?>(new(
                tenantId,
                legalEntityId,
                "EGP",
                transactionCurrencyCode,
                purpose,
                businessDate,
                new(legalEntityId.Value, Guid.Parse("40000000-0000-7000-8000-000000000001"), 7, false, 3),
                new(Guid.Parse("70000000-0000-7000-8000-000000000001"), 3, 2, "0.01", FinancialLineDocumentStrategy.RoundEachLineThenSum, Enum.GetValues<FinancialRoundingContext>().Select(context => new FinancialRoundingRuleSnapshotContract(context, FinancialRoundingMode.HalfEven)).ToArray()),
                rate,
                DateTimeOffset.Parse("2026-10-20T09:00:00Z", CultureInfo.InvariantCulture)));
        }

        public Task<PaymentTermSnapshotContract?> ResolvePaymentTermAsync(TenantId tenantId, Guid paymentTermId, DateOnly businessDate, CancellationToken cancellationToken = default) => Task.FromResult<PaymentTermSnapshotContract?>(
            new(paymentTermId, Guid.Parse("50000000-0000-7000-8000-000000000001"), "NET-30-50", 1, businessDate, null, [new(1, "Percentage", "0.5", null, "Immediate", 0, null, 0), new(2, "Balance", null, null, "DaysAfterDate", 30, null, 0)]));
    }
}

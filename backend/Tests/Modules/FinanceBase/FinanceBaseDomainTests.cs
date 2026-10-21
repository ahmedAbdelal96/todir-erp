using System.Globalization;
using System.Text.Json;
using Core.Services;
using Modules.FinanceBase.DTOs;
using Modules.FinanceBase.Entities;
using Modules.FinanceBase.Services;
using Shared.Primitives.Identifiers;
using Shared.Primitives.Financial;
using Xunit;

namespace Modules.FinanceBase.Tests;

public sealed class FinanceBaseDomainTests
{
    [Fact]
    public void MoneyRejectsCurrencyMismatchAndPreservesDecimalText()
    {
        var egp = Money.Create("100.12500000", "EGP");
        Assert.Equal("100.12500000", egp.Amount.ToString("0.00000000", CultureInfo.InvariantCulture));
        Assert.Throws<FinancialValueException>(() => egp.Add(Money.Create("1", "USD")));
        Assert.Equal("finbase.invalid_decimal", Assert.Throws<FinancialValueException>(() => Money.Create("1,234.56", "EGP")).Code);
        Assert.Equal("finbase.invalid_decimal", Assert.Throws<FinancialValueException>(() => Money.Create("1e3", "EGP")).Code);
    }

    [Fact]
    public void MoneyArithmeticRemainsCurrencyBoundedAndReportsOverflowStably()
    {
        var amount = Money.Create("12.50", "EGP");
        Assert.Equal(7.5m, amount.Subtract(Money.Create("5", "EGP")).Amount);
        Assert.Equal(25m, amount.Multiply(2m).Amount);
        Assert.Equal(-12.5m, amount.Negate().Amount);
        Assert.Equal("finbase.currency_mismatch", Assert.Throws<FinancialValueException>(() => amount.Subtract(Money.Create("1", "USD"))).Code);
        Assert.Equal("finbase.amount_overflow", Assert.Throws<FinancialValueException>(() => Money.Create(decimal.MaxValue.ToString(CultureInfo.InvariantCulture), "EGP").Multiply(2m)).Code);
    }

    [Fact]
    public void ExchangeRateSnapshotsValidateTheirImmutableContext()
    {
        var candidate = new ExchangeRateCandidate("usd", "egp", 50.25m, ExchangeRatePurpose.Accounting, new DateOnly(2026, 1, 1), ExchangeRateStatus.Approved, Guid.CreateVersion7(), 3, "Manual", true);
        var snapshot = ExchangeRateSnapshot.FromApprovedRate(Guid.CreateVersion7(), Guid.CreateVersion7(), candidate, DateTimeOffset.Parse("2026-01-02T10:00:00Z", CultureInfo.InvariantCulture));
        Assert.Equal("USD", snapshot.FromCurrency);
        Assert.Equal("EGP", snapshot.ToCurrency);
        Assert.Equal(50.25m, snapshot.Rate);
        Assert.True(snapshot.ManualOverride);
        Assert.Equal(candidate.RateId, snapshot.ApprovedRateId);
        candidate = candidate with { Rate = 1m, Version = 4, Status = ExchangeRateStatus.Superseded };
        Assert.Equal(50.25m, snapshot.Rate);
        Assert.Equal(3, snapshot.Version);
        Assert.Equal("finbase.rate_not_approved", Assert.Throws<FinanceBaseDomainException>(() => ExchangeRateSnapshot.FromApprovedRate(Guid.CreateVersion7(), Guid.CreateVersion7(), candidate, DateTimeOffset.UtcNow)).Code);
        Assert.Equal("finbase.rate_snapshot_invalid", Assert.Throws<FinanceBaseDomainException>(() => ExchangeRateSnapshot.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.Empty, "USD", "USD", 0m, ExchangeRatePurpose.Accounting, new DateOnly(2026, 1, 1), "", 0, false, DateTimeOffset.UtcNow)).Code);
    }

    [Fact]
    public void CurrencyConverterUsesOnlyExplicitImmutableSnapshotAndPolicy()
    {
        var tenant = Guid.CreateVersion7(); var entity = Guid.CreateVersion7(); var rateId = Guid.CreateVersion7(); var policyId = Guid.CreateVersion7();
        var rateCandidate = new ExchangeRateCandidate("USD", "EGP", 50.125m, ExchangeRatePurpose.Accounting, new DateOnly(2026, 10, 1), ExchangeRateStatus.Approved, rateId, 2, "Manual", false);
        var rateSnapshot = ExchangeRateSnapshot.FromApprovedRate(tenant, entity, rateCandidate, DateTimeOffset.Parse("2026-10-02T08:00:00Z", CultureInfo.InvariantCulture));
        var policy = RoundingPolicySnapshot.Create(policyId, 4, RoundingMode.HalfUp, 2);
        var converted = CurrencyConverter.Convert(Money.Create("10", "USD"), "EGP", rateSnapshot, policy);
        Assert.Equal(501.25m, converted.Target.Amount);
        Assert.Equal(rateId, converted.Snapshot!.ApprovedRateId);
        Assert.Equal(50.125m, converted.Rate);
        Assert.Throws<FinanceBaseDomainException>(() => CurrencyConverter.Convert(Money.Create("10", "USD"), "EGP", null, policy));
        var sameCurrency = CurrencyConverter.Convert(Money.Create("0.01", "EGP"), "egp", null, policy);
        Assert.Equal(1m, sameCurrency.Rate);
        Assert.Null(sameCurrency.Snapshot);
        Assert.Equal(0.01m, sameCurrency.Target.Amount);
        var reversed = ExchangeRateSnapshot.FromApprovedRate(tenant, entity, rateCandidate with { FromCurrency = "EGP", ToCurrency = "USD" }, DateTimeOffset.UtcNow);
        Assert.Equal("finbase.rate_missing", Assert.Throws<FinanceBaseDomainException>(() => CurrencyConverter.Convert(Money.Create("10", "USD"), "EGP", reversed, policy)).Code);
    }

    [Fact]
    public void RoundingPolicySnapshotFreezesContextModesAndLineDocumentStrategy()
    {
        var rules = Enum.GetValues<RoundingContext>()
            .Select(context => new RoundingPolicyContextRule(context, context == RoundingContext.FXBaseAmount ? RoundingMode.Down : RoundingMode.HalfUp))
            .ToArray();
        var snapshot = RoundingPolicySnapshot.Create(Guid.CreateVersion7(), 3, 2, LineDocumentRoundingStrategy.SumExactLinesThenRound, rules);
        Assert.Equal(RoundingMode.Down, snapshot.ModeFor(RoundingContext.FXBaseAmount));
        Assert.Equal(RoundingMode.HalfUp, snapshot.ModeFor(RoundingContext.DocumentTotal));
        Assert.Equal(LineDocumentRoundingStrategy.SumExactLinesThenRound, snapshot.LineDocumentStrategy);
        Assert.Equal(1.23m, MoneyRounding.Round(1.239m, snapshot.Scale, snapshot.ModeFor(RoundingContext.FXBaseAmount)));
        Assert.Throws<FinanceBaseDomainException>(() => RoundingPolicySnapshot.Create(Guid.CreateVersion7(), 1, 2, LineDocumentRoundingStrategy.RoundEachLineThenSum, rules[..^1]));
    }

    [Fact]
    public async Task BaseCurrencyChangeGuardAllowsPreGoLiveAndLocksPostGoLiveChanges()
    {
        var tenant = new TenantId(Guid.CreateVersion7()); var entity = new LegalEntityId(Guid.CreateVersion7());
        var legalEntities = new TestLegalEntityFinancialReadStore("EGP");
        var beforeGoLive = new BaseCurrencyChangeGuard(legalEntities, new TestFinancialGoLiveGuard(false));
        await beforeGoLive.EnsureChangeAllowedAsync(tenant, entity, "USD");
        await beforeGoLive.EnsureChangeAllowedAsync(tenant, entity, "egp");

        var afterGoLive = new BaseCurrencyChangeGuard(legalEntities, new TestFinancialGoLiveGuard(true));
        var exception = await Assert.ThrowsAsync<FinanceBaseException>(() => afterGoLive.EnsureChangeAllowedAsync(tenant, entity, "USD"));
        Assert.Equal("finbase.base_currency_locked", exception.Code);
        await afterGoLive.EnsureChangeAllowedAsync(tenant, entity, "EGP");
    }

    [Fact]
    public void PercentageUsesRatioAsItsCanonicalRepresentation()
    {
        Assert.Equal(0.15m, Percentage.FromPercent(15m).Ratio);
        Assert.Equal(0.3333m, Percentage.FromRatio(0.3333m).Ratio);
        Assert.Throws<FinancialValueException>(() => Percentage.FromRatio(15m));
    }

    [Fact]
    public void PublicPaymentPercentageContractSerializesAsAnInvariantString()
    {
        var request = new PaymentTermRuleRequest(1, PaymentAllocation.Percentage, "0.15", null, DueDateBasis.Immediate, 0, null, 0);
        var json = JsonSerializer.Serialize(request);
        Assert.Contains("\"Percentage\":\"0.15\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void RoundingUsesExplicitHalfUpPolicy()
    {
        Assert.Equal(12.35m, MoneyRounding.Round(12.345m, 2, RoundingMode.HalfUp));
        Assert.Equal(-12.35m, MoneyRounding.Round(-12.345m, 2, RoundingMode.HalfUp));
        Assert.Equal(12.34m, MoneyRounding.Round(12.345m, 2, RoundingMode.HalfEven));
        Assert.Equal(12.34m, MoneyRounding.Round(12.349m, 2, RoundingMode.Down));
        Assert.Equal(-12.35m, MoneyRounding.Round(-12.341m, 2, RoundingMode.Up));
    }

    [Fact]
    public void VersionedGoldenVectorsMatchApprovedMoneyRoundingFxAndScheduleBehavior()
    {
        using var stream = typeof(FinanceBaseDomainTests).Assembly.GetManifestResourceStream("FinanceBase.Tests.GoldenVectors.finbase-v1.json");
        Assert.NotNull(stream);
        using var vectors = JsonDocument.Parse(stream);
        Assert.Equal("finbase-golden-v1", vectors.RootElement.GetProperty("version").GetString());
        foreach (var vector in vectors.RootElement.GetProperty("money").EnumerateArray())
        {
            var money = Money.Create(vector.GetProperty("amount").GetString()!, vector.GetProperty("currency").GetString()!);
            Assert.Equal(decimal.Parse(vector.GetProperty("expected").GetString()!, CultureInfo.InvariantCulture), money.Amount);
        }
        foreach (var vector in vectors.RootElement.GetProperty("rounding").EnumerateArray())
        {
            var amount = decimal.Parse(vector.GetProperty("amount").GetString()!, CultureInfo.InvariantCulture);
            var mode = Enum.Parse<RoundingMode>(vector.GetProperty("mode").GetString()!);
            Assert.Equal(decimal.Parse(vector.GetProperty("expected").GetString()!, CultureInfo.InvariantCulture), MoneyRounding.Round(amount, vector.GetProperty("scale").GetInt32(), mode));
        }
        foreach (var vector in vectors.RootElement.GetProperty("conversion").EnumerateArray())
        {
            var rate = decimal.Parse(vector.GetProperty("rate").GetString()!, CultureInfo.InvariantCulture);
            var targetCurrency = vector.GetProperty("target").GetString()!;
            var policy = RoundingPolicySnapshot.Create(Guid.CreateVersion7(), 1, Enum.Parse<RoundingMode>(vector.GetProperty("mode").GetString()!), vector.GetProperty("scale").GetInt32());
            var snapshot = ExchangeRateSnapshot.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), vector.GetProperty("currency").GetString()!, targetCurrency, rate, ExchangeRatePurpose.Accounting, new DateOnly(2026, 1, 1), "GoldenVector", 1, false, DateTimeOffset.Parse("2026-01-01T00:00:00Z", CultureInfo.InvariantCulture));
            var converted = CurrencyConverter.Convert(Money.Create(vector.GetProperty("amount").GetString()!, vector.GetProperty("currency").GetString()!), targetCurrency, snapshot, policy);
            Assert.Equal(decimal.Parse(vector.GetProperty("expected").GetString()!, CultureInfo.InvariantCulture), converted.Target.Amount);
        }
        foreach (var vector in vectors.RootElement.GetProperty("schedule").EnumerateArray())
        {
            var total = decimal.Parse(vector.GetProperty("total").GetString()!, CultureInfo.InvariantCulture);
            var rules = new[]
            {
                new PaymentTermRuleInput(1, PaymentAllocation.Percentage, decimal.Parse(vector.GetProperty("firstRatio").GetString()!, CultureInfo.InvariantCulture), null, DueDateBasis.Immediate, 0, null, 0),
                new PaymentTermRuleInput(2, PaymentAllocation.Percentage, decimal.Parse(vector.GetProperty("secondRatio").GetString()!, CultureInfo.InvariantCulture), null, DueDateBasis.DaysAfterDate, 30, null, 0),
                new PaymentTermRuleInput(3, PaymentAllocation.Balance, null, null, DueDateBasis.EndOfMonth, 0, null, 0)
            };
            var schedule = PaymentScheduleCalculator.Calculate(total, new DateOnly(2026, 1, 1), rules, vector.GetProperty("scale").GetInt32());
            Assert.Equal(total, schedule.Sum(line => line.Amount));
            Assert.Equal(decimal.Parse(vector.GetProperty("expectedFirst").GetString()!, CultureInfo.InvariantCulture), schedule[0].Amount);
            Assert.Equal(decimal.Parse(vector.GetProperty("expectedSecond").GetString()!, CultureInfo.InvariantCulture), schedule[1].Amount);
            Assert.Equal(decimal.Parse(vector.GetProperty("expectedBalance").GetString()!, CultureInfo.InvariantCulture), schedule[2].Amount);
        }
    }

    [Fact]
    public void ScheduleAllocatesExactTotalAndResidualToFinalBalance()
    {
        var rules = new[]
        {
            new PaymentTermRuleInput(1, PaymentAllocation.Percentage, 0.3333m, null, DueDateBasis.Immediate, 0, null, 0),
            new PaymentTermRuleInput(2, PaymentAllocation.Balance, null, null, DueDateBasis.DaysAfterDate, 30, null, 0)
        };
        var schedule = PaymentScheduleCalculator.Calculate(100m, new DateOnly(2026, 1, 1), rules, 2);
        Assert.Equal(100m, schedule.Sum(x => x.Amount));
        Assert.Equal(66.67m, schedule[1].Amount);
        Assert.Equal(new DateOnly(2026, 1, 31), schedule[1].DueDate);
    }

    [Theory]
    [InlineData(0.01)]
    [InlineData(-100.00)]
    [InlineData(999999999999.99)]
    public void BalanceScheduleAlwaysReconcilesTinyNegativeAndLargeTotals(decimal total)
    {
        var rules = new[]
        {
            new PaymentTermRuleInput(1, PaymentAllocation.Percentage, 0.3333m, null, DueDateBasis.Immediate, 0, null, 0),
            new PaymentTermRuleInput(2, PaymentAllocation.Percentage, 0.3333m, null, DueDateBasis.DaysAfterDate, 30, null, 0),
            new PaymentTermRuleInput(3, PaymentAllocation.Balance, null, null, DueDateBasis.EndOfMonth, 0, null, 0)
        };
        var schedule = PaymentScheduleCalculator.Calculate(total, new DateOnly(2024, 2, 29), rules, 2);
        Assert.Equal(total, schedule.Sum(x => x.Amount));
        Assert.Equal(new DateOnly(2024, 2, 29), schedule[0].DueDate);
        Assert.Equal(new DateOnly(2024, 2, 29), schedule[2].DueDate);
    }

    [Fact]
    public void DayOfFollowingMonthClampsToLeapFebruary()
    {
        var rule = new PaymentTermRuleInput(1, PaymentAllocation.Balance, null, null, DueDateBasis.DayOfFollowingMonth, 0, 31, 0);
        var schedule = PaymentScheduleCalculator.Calculate(1m, new DateOnly(2024, 1, 31), [rule], 2);
        Assert.Equal(new DateOnly(2024, 2, 29), schedule.Single().DueDate);
    }

    [Fact]
    public void GeneratedSchedulesAlwaysReconcileAndReplayDeterministically()
    {
        var basis = new DateOnly(2026, 12, 31);
        for (var index = 1; index <= 1000; index++)
        {
            var total = (index % 2 == 0 ? 1m : -1m) * index / 100m;
            var firstRatio = index % 3 == 0 ? 0.3333m : 0.15m;
            var secondRatio = index % 3 == 0 ? 0.3333m : 0.35m;
            var rules = new[]
            {
                new PaymentTermRuleInput(1, PaymentAllocation.Percentage, firstRatio, null, DueDateBasis.Immediate, 0, null, 0),
                new PaymentTermRuleInput(2, PaymentAllocation.Percentage, secondRatio, null, DueDateBasis.DayOfFollowingMonth, 0, 31, 0),
                new PaymentTermRuleInput(3, PaymentAllocation.Balance, null, null, DueDateBasis.DaysAfterEndOfMonth, 0, null, index % 2)
            };
            var actual = PaymentScheduleCalculator.Calculate(total, basis, rules, 2);
            var replay = PaymentScheduleCalculator.Calculate(total, basis, rules, 2);
            Assert.Equal(total, actual.Sum(line => line.Amount));
            Assert.Equal(actual, replay);
        }
    }

    [Fact]
    public void CanonicalScheduleScenariosPreserveTotalsAndCalendarBoundaries()
    {
        var basis = new DateOnly(2024, 12, 15);
        PaymentTermRuleInput Rule(int sequence, PaymentAllocation allocation, decimal? ratio, DueDateBasis due, int days = 0) => new(sequence, allocation, ratio, null, due, days, null, 0);
        var thirtySixtyNinety = PaymentScheduleCalculator.Calculate(100m, basis,
        [Rule(1, PaymentAllocation.Percentage, 0.30m, DueDateBasis.DaysAfterDate, 30), Rule(2, PaymentAllocation.Percentage, 0.30m, DueDateBasis.DaysAfterDate, 60), Rule(3, PaymentAllocation.Balance, null, DueDateBasis.DaysAfterDate, 90)], 2);
        Assert.Equal(100m, thirtySixtyNinety.Sum(line => line.Amount));
        Assert.Equal([new DateOnly(2025, 1, 14), new DateOnly(2025, 2, 13), new DateOnly(2025, 3, 15)], thirtySixtyNinety.Select(line => line.DueDate));

        var thirtySeventy = PaymentScheduleCalculator.Calculate(100m, basis,
        [Rule(1, PaymentAllocation.Percentage, 0.30m, DueDateBasis.Immediate), Rule(2, PaymentAllocation.Balance, null, DueDateBasis.EndOfMonth)], 2);
        Assert.Equal([30m, 70m], thirtySeventy.Select(line => line.Amount));
        Assert.Equal(new DateOnly(2024, 12, 31), thirtySeventy[1].DueDate);

        var residual = PaymentScheduleCalculator.Calculate(0.01m, new DateOnly(2024, 1, 31),
        [Rule(1, PaymentAllocation.Percentage, 0.3333m, DueDateBasis.Immediate), new(2, PaymentAllocation.Percentage, 0.3333m, null, DueDateBasis.DayOfFollowingMonth, 0, 31, 0), new(3, PaymentAllocation.Balance, null, null, DueDateBasis.DaysAfterEndOfMonth, 0, null, 0)], 2);
        Assert.Equal(0.01m, residual.Sum(line => line.Amount));
        Assert.Equal(new DateOnly(2024, 2, 29), residual[1].DueDate);
        Assert.Equal(new DateOnly(2024, 1, 31), residual[2].DueDate);

        var negative = PaymentScheduleCalculator.Calculate(-101.25m, basis,
        [Rule(1, PaymentAllocation.Percentage, 0.30m, DueDateBasis.Immediate), Rule(2, PaymentAllocation.Balance, null, DueDateBasis.DaysAfterDate, 30)], 2);
        Assert.Equal(-101.25m, negative.Sum(line => line.Amount));
        Assert.Equal(new DateOnly(2025, 1, 14), negative[1].DueDate);
    }

    [Fact]
    public void PaymentScheduleAcceptsResolvedPaymentAllocationRoundingContext()
    {
        var rules = new[]
        {
            new PaymentTermRuleInput(1, PaymentAllocation.Percentage, 0.5m, null, DueDateBasis.Immediate, 0, null, 0),
            new PaymentTermRuleInput(2, PaymentAllocation.Balance, null, null, DueDateBasis.DaysAfterDate, 30, null, 0)
        };
        var contextRules = Enum.GetValues<RoundingContext>()
            .Select(context => new RoundingPolicyContextRule(context, context == RoundingContext.PaymentAllocation ? RoundingMode.Down : RoundingMode.HalfUp));
        var policy = RoundingPolicySnapshot.Create(Guid.CreateVersion7(), 2, 2, LineDocumentRoundingStrategy.RoundEachLineThenSum, contextRules);
        var schedule = PaymentScheduleCalculator.Calculate(0.01m, new DateOnly(2026, 1, 1), rules, policy);
        Assert.Equal([0m, 0.01m], schedule.Select(line => line.Amount));
        Assert.Equal(0.01m, schedule.Sum(line => line.Amount));
    }

    [Fact]
    public void ResolverDoesNotUseFutureOrUnapprovedRates()
    {
        var rates = new[]
        {
            new ExchangeRateCandidate("USD", "EGP", 50m, ExchangeRatePurpose.Accounting, new DateOnly(2026, 1, 1), ExchangeRateStatus.Approved),
            new ExchangeRateCandidate("USD", "EGP", 55m, ExchangeRatePurpose.Accounting, new DateOnly(2026, 2, 1), ExchangeRateStatus.Draft)
        };
        var resolved = ExchangeRateResolver.Resolve(rates, "USD", "EGP", ExchangeRatePurpose.Accounting, new DateOnly(2026, 1, 20), 60);
        Assert.Equal(50m, resolved.Rate);
        var future = Assert.Throws<FinanceBaseDomainException>(() => ExchangeRateResolver.Resolve([
            new("USD", "EGP", 56m, ExchangeRatePurpose.Accounting, new DateOnly(2026, 2, 1), ExchangeRateStatus.Approved)
        ], "USD", "EGP", ExchangeRatePurpose.Accounting, new DateOnly(2026, 1, 20), 60));
        Assert.Equal("finbase.rate_missing", future.Code);
    }

    [Fact]
    public void ResolverFailsStableForMissingStaleAndAmbiguousRates()
    {
        var date = new DateOnly(2026, 3, 1);
        var missing = Assert.Throws<FinanceBaseDomainException>(() => ExchangeRateResolver.Resolve([], "USD", "EGP", ExchangeRatePurpose.Accounting, date, 7));
        Assert.Equal("finbase.rate_missing", missing.Code);
        var stale = Assert.Throws<FinanceBaseDomainException>(() => ExchangeRateResolver.Resolve([new("USD", "EGP", 50m, ExchangeRatePurpose.Accounting, new DateOnly(2026, 1, 1), ExchangeRateStatus.Approved)], "USD", "EGP", ExchangeRatePurpose.Accounting, date, 7));
        Assert.Equal("finbase.rate_stale", stale.Code);
        var duplicate = Assert.Throws<FinanceBaseDomainException>(() => ExchangeRateResolver.Resolve([
            new("USD", "EGP", 50m, ExchangeRatePurpose.Accounting, date, ExchangeRateStatus.Approved),
            new("USD", "EGP", 51m, ExchangeRatePurpose.Accounting, date, ExchangeRateStatus.Approved)
        ], "USD", "EGP", ExchangeRatePurpose.Accounting, date, 7));
        Assert.Equal("finbase.rate_not_approved", duplicate.Code);
    }

    [Fact]
    public void SameCurrencyResolutionAlwaysPreservesAmountAndUsesUnitRate()
    {
        for (var cents = -1000; cents <= 1000; cents++)
        {
            var amount = Money.Create((cents / 100m).ToString(CultureInfo.InvariantCulture), "egp");
            var resolved = ExchangeRateResolver.Resolve([], amount.Currency, "EGP", ExchangeRatePurpose.Accounting, new DateOnly(2026, 1, 1), 0);
            Assert.Equal(1m, resolved.Rate);
            Assert.Equal(amount.Amount, amount.Amount * resolved.Rate);
        }
    }

    private sealed class TestLegalEntityFinancialReadStore(string baseCurrency) : ILegalEntityFinancialReadStore
    {
        public Task<string?> GetBaseCurrencyCodeAsync(TenantId tenantId, LegalEntityId legalEntityId, CancellationToken cancellationToken = default) => Task.FromResult<string?>(baseCurrency);
        public Task<string?> GetTimeZoneAsync(TenantId tenantId, LegalEntityId legalEntityId, CancellationToken cancellationToken = default) => Task.FromResult<string?>("UTC");
    }

    private sealed class TestFinancialGoLiveGuard(bool live) : IFinancialGoLiveGuard
    {
        public Task<bool> HasPostedFinancialFactsAsync(TenantId tenantId, LegalEntityId legalEntityId, CancellationToken cancellationToken = default) => Task.FromResult(live);
    }
}

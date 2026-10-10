using System.Globalization;
using Modules.FinanceBase.Entities;
using Xunit;

namespace Modules.FinanceBase.Tests;

public sealed class FinanceBaseDomainTests
{
    [Fact]
    public void MoneyRejectsCurrencyMismatchAndPreservesDecimalText()
    {
        var egp = Money.Create("100.12500000", "EGP");
        Assert.Equal("100.12500000", egp.Amount.ToString("0.00000000", CultureInfo.InvariantCulture));
        Assert.Throws<FinanceBaseDomainException>(() => egp.Add(Money.Create("1", "USD")));
    }

    [Fact]
    public void RoundingUsesExplicitHalfUpPolicy()
    {
        Assert.Equal(12.35m, MoneyRounding.Round(12.345m, 2, RoundingMode.HalfUp));
        Assert.Equal(-12.35m, MoneyRounding.Round(-12.345m, 2, RoundingMode.HalfUp));
    }

    [Fact]
    public void ScheduleAllocatesExactTotalAndResidualToFinalBalance()
    {
        var rules = new[]
        {
            new PaymentTermRuleInput(1, PaymentAllocation.Percentage, 33.33m, null, DueDateBasis.InvoiceDate, 0, null, 0),
            new PaymentTermRuleInput(2, PaymentAllocation.Balance, null, null, DueDateBasis.InvoiceDate, 30, null, 0)
        };
        var schedule = PaymentScheduleCalculator.Calculate(100m, new DateOnly(2026, 1, 1), rules, 2);
        Assert.Equal(100m, schedule.Sum(x => x.Amount));
        Assert.Equal(66.67m, schedule[1].Amount);
        Assert.Equal(new DateOnly(2026, 1, 31), schedule[1].DueDate);
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
    }
}

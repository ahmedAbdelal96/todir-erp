using Modules.Banking.Rules;
using Xunit;

namespace Modules.Banking.Tests;

public sealed class BankingRulesTests
{
    [Fact]
    public void MatchConservationAcceptsExactDifference()
    {
        BankingRules.ValidateMatch(100m, 97m, 3m);
    }

    [Fact]
    public void MatchConservationRejectsOverclaim()
    {
        Assert.Throws<InvalidOperationException>(() => BankingRules.ValidateMatch(100m, 97m, 4m));
    }

    [Fact]
    public void CashCountUsesDenominationArithmetic()
    {
        Assert.Equal(55000m, BankingRules.CalculateCashCount([(100m, 550)]));
    }

    [Fact]
    public void CashCountRejectsNegativeQuantities()
    {
        Assert.Throws<InvalidOperationException>(() => BankingRules.CalculateCashCount([(100m, -1)]));
    }
}

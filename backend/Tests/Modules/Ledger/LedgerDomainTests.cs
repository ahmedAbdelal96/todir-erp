using Modules.Ledger.Entities;
using Shared.Contracts.Ledger;
using Xunit;

namespace Modules.Ledger.Tests;

public sealed class LedgerDomainTests
{
    [Fact]
    public void NormalBalanceFollowsAccountType()
    {
        Assert.Equal(NormalBalance.Debit, LedgerDomainRules.NormalBalanceFor(AccountType.Asset));
        Assert.Equal(NormalBalance.Credit, LedgerDomainRules.NormalBalanceFor(AccountType.Liability));
        Assert.Equal(NormalBalance.Credit, LedgerDomainRules.NormalBalanceFor(AccountType.Revenue));
    }

    [Fact]
    public void UnbalancedEntryIsRejectedWithStableCode()
    {
        var exception = Assert.Throws<LedgerDomainException>(() => LedgerDomainRules.EnsureBalanced(100m, 99m));
        Assert.Equal("ledger.entry_unbalanced", exception.Code);
    }

    [Fact]
    public void FactFingerprintIgnoresObjectPropertyInsertionOrderButTracksMeaning()
    {
        var first = new UnorderedPayload(new Dictionary<string, string> { ["b"] = "2", ["a"] = "1" });
        var second = new UnorderedPayload(new Dictionary<string, string> { ["a"] = "1", ["b"] = "2" });
        Assert.Equal(AccountingFactFingerprint.Compute(first), AccountingFactFingerprint.Compute(second));
        Assert.NotEqual(AccountingFactFingerprint.Compute(first), AccountingFactFingerprint.Compute(new UnorderedPayload(new Dictionary<string, string> { ["a"] = "changed", ["b"] = "2" })));
    }

    [Fact]
    public void CanonicalBusinessPayloadsDoNotExposeLedgerDebitCreditEscapeHatch()
    {
        foreach (var payload in typeof(AccountingFactTypes).Assembly.GetTypes().Where(type => type.Namespace == "Shared.Contracts.Ledger" && type.Name.EndsWith("Payload", StringComparison.Ordinal) && type != typeof(ExplicitLedgerPostingPayload) && type != typeof(LedgerPostingLinePayload)))
        {
            Assert.DoesNotContain(payload.GetProperties(), property => property.Name is "Debit" or "Credit" or "DebitLines" or "CreditLines");
        }
    }

    private sealed record UnorderedPayload(Dictionary<string, string> Values) : IAccountingFactPayload;
}

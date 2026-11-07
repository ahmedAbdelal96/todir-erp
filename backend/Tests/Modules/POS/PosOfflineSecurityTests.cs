using Xunit;

namespace POSTests;

public sealed class PosOfflineSecurityTests
{
    [Fact]
    public void EnvelopeSignatureBindsDeviceSequenceAndPayload()
    {
        var signature = Modules.POS.PosOfflineSecurity.SignEnvelope(
            "offline-test-secret",
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            "terminal-1",
            7,
            "offline-7",
            3,
            "{\"total\":10}");

        Assert.True(Modules.POS.PosOfflineSecurity.VerifyEnvelope(
            "offline-test-secret",
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            "terminal-1",
            7,
            "offline-7",
            3,
            "{\"total\":10}",
            signature));
        Assert.False(Modules.POS.PosOfflineSecurity.VerifyEnvelope(
            "offline-test-secret",
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            "terminal-1",
            8,
            "offline-7",
            3,
            "{\"total\":10}",
            signature));
    }

    [Fact]
    public void OfflineCashSaleRequiresAPositiveLineAndPaymentAccount()
    {
        var request = new Modules.POS.OfflineCashSalePayload(
            1,
            "CashSale",
            Guid.NewGuid(),
            "EGP",
            Guid.Empty,
            "offline-sale-1",
            []);

        Assert.Throws<ArgumentException>(() => Modules.POS.PosOfflineCashSalePolicy.Validate(request));
    }
}

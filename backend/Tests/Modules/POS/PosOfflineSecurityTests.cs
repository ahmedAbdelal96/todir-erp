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

    [Fact]
    public void OfflineCashSaleAllowsAnonymousWalkInCustomer()
    {
        var request = new Modules.POS.OfflineCashSalePayload(
            1,
            "CashSale",
            Guid.NewGuid(),
            "EGP",
            Guid.NewGuid(),
            "offline-sale-anon-1",
            [new Modules.POS.OfflineCashSaleLine(
                Guid.NewGuid(), "Item 1", "EA", 1m, 10m, 0m, 0m,
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow))],
            null);

        Modules.POS.PosOfflineCashSalePolicy.Validate(request);
    }

    [Fact]
    public void EnvelopeSignatureRejectsTamperedPayload()
    {
        var registerId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var signature = Modules.POS.PosOfflineSecurity.SignEnvelope(
            "pkg-secret-123",
            registerId,
            "term-1",
            10,
            "idem-10",
            2,
            "{\"price\":100.00}");

        // Untampered must pass
        Assert.True(Modules.POS.PosOfflineSecurity.VerifyEnvelope(
            "pkg-secret-123", registerId, "term-1", 10, "idem-10", 2, "{\"price\":100.00}", signature));

        // Tampered price must fail verification
        Assert.False(Modules.POS.PosOfflineSecurity.VerifyEnvelope(
            "pkg-secret-123", registerId, "term-1", 10, "idem-10", 2, "{\"price\":10.00}", signature));
    }

    [Fact]
    public void EnvelopeSignatureRejectsTamperedDevice()
    {
        var registerId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var signature = Modules.POS.PosOfflineSecurity.SignEnvelope(
            "pkg-secret-123",
            registerId,
            "term-authorized",
            10,
            "idem-10",
            2,
            "{\"item\":\"valid\"}");

        // Tampered device identity must fail verification
        Assert.False(Modules.POS.PosOfflineSecurity.VerifyEnvelope(
            "pkg-secret-123", registerId, "term-rogue", 10, "idem-10", 2, "{\"item\":\"valid\"}", signature));
    }
}

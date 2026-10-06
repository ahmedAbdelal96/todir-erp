using Todir.Platform.Identity;
using Todir.Platform.Security;
using Xunit;

namespace Todir.Platform.Tests;

public sealed class SecurityServiceTests
{
    [Fact]
    public void MfaEnrollmentStoresOnlyRecoveryCodeDigests()
    {
        var enrollment = new MfaService().CreateEnrollment(3);
        Assert.Equal(3, enrollment.RecoveryCodeDigests.Count);
        Assert.All(enrollment.RecoveryCodeDigests, digest => Assert.Equal(64, digest.Length));
        Assert.False(new MfaService().VerifyTotp(enrollment.SecretBase32, "000000", DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task SessionRevocationInvalidatesExistingSession()
    {
        var service = new SessionService(TimeProvider.System);
        var identity = new Todir.Platform.Identity.Identity(Todir.SharedKernel.Identifiers.IdentityId.New(), "A@EXAMPLE.COM", true, IdentityStatus.Active, DateTimeOffset.UtcNow, null, Guid.NewGuid());
        var membership = new Membership(Todir.SharedKernel.Identifiers.MembershipId.New(), Todir.SharedKernel.Identifiers.TenantId.New(), identity.Id, "A", MembershipStatus.Active, null, null, "en", "UTC", DateTimeOffset.UtcNow, 1);
        var session = (await service.CreateAsync(identity, membership))!;
        Assert.True(service.IsValid(session, DateTimeOffset.UtcNow));
        await service.RevokeAsync(identity.Id);
        Assert.False(service.IsValid(session, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void FilePolicyRejectsDeclaredSizeAndMagicMismatch()
    {
        var policy = new FileUploadPolicy(10);
        Assert.False(policy.Validate("invoice.pdf", "application/pdf", "not pdf"u8, 8).Accepted);
        Assert.True(policy.Validate("invoice.pdf", "application/pdf", "%PDF-1.7"u8, 8).Accepted);
    }

    [Fact]
    public void SupportGrantExpiresAndCannotCrossTenantBoundary()
    {
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var clock = new FakeTimeProvider(now);
        var service = new SupportAccessService(clock);
        var tenant = Todir.SharedKernel.Identifiers.TenantId.New();
        var actor = Guid.NewGuid();
        var grant = service.Request(tenant, actor, "incident response", now.AddMinutes(10));
        service.Approve(grant.Id, Guid.NewGuid());
        Assert.False(service.CanAccess(grant.Id, Todir.SharedKernel.Identifiers.TenantId.New(), actor));
        Assert.True(service.CanAccess(grant.Id, tenant, actor));
        clock.Advance(TimeSpan.FromMinutes(11));
        Assert.False(service.CanAccess(grant.Id, tenant, actor));
    }

    private sealed class FakeTimeProvider(DateTimeOffset initialTime) : TimeProvider
    {
        private DateTimeOffset current = initialTime;
        public override DateTimeOffset GetUtcNow() => current;
        public void Advance(TimeSpan amount) => current = current.Add(amount);
    }
}

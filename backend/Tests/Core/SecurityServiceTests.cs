using Core.Identity;
using Core.Security;
using Core.Scheduling;
using Core.Services;
using Shared.Primitives.Identifiers;
using Microsoft.AspNetCore.DataProtection;
using Quartz;
using Xunit;

namespace Core.Tests;

public sealed class SecurityServiceTests
{
    [Fact]
    public void MaintenanceJobDeclaresNonConcurrentExecution()
    {
        Assert.NotNull(typeof(PlatformMaintenanceJob).GetCustomAttributes(typeof(DisallowConcurrentExecutionAttribute), inherit: true).SingleOrDefault());
    }

    [Fact]
    public async Task SupportWorkflowRequiresTenantBoundApprovalAndRevocation()
    {
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var tenant = TenantId.New();
        var actorIdentity = IdentityId.New();
        var approverIdentity = IdentityId.New();
        var actor = actorIdentity.Value;
        var mfa = new MfaCredentialManager(new MfaService(), new InMemoryMfaCredentialStore(), DataProtectionProvider.Create("support-workflow-tests"));
        var actorEnrollment = await mfa.EnrollAsync(actorIdentity);
        var approverEnrollment = await mfa.EnrollAsync(approverIdentity);
        var actorProof = (await mfa.VerifyStepUpAsync(actorIdentity, actorEnrollment.RecoveryCodes[0], now))!;
        var approverProof = (await mfa.VerifyStepUpAsync(approverIdentity, approverEnrollment.RecoveryCodes[0], now))!;
        var service = new SupportAccessService(new FakeTimeProvider(now));
        var workflow = new InMemorySupportAccessWorkflow(service);
        var grant = await workflow.RequestAsync(tenant, actor, "incident response", now.AddMinutes(10), actorProof);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => workflow.ApproveAsync(TenantId.New(), grant.Id, approverIdentity.Value, approverProof));
        Assert.NotNull(await workflow.ApproveAsync(tenant, grant.Id, approverIdentity.Value, approverProof));
        Assert.True(await workflow.CanAccessAsync(tenant, grant.Id, actor, actorProof));
        Assert.True(await workflow.RevokeAsync(tenant, grant.Id, actor, actorProof));
        Assert.False(await workflow.CanAccessAsync(tenant, grant.Id, actor, actorProof));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => workflow.RequestAsync(tenant, actor, "incident response", now.AddMinutes(10), null!));
    }

    [Fact]
    public void MfaEnrollmentStoresOnlyRecoveryCodeDigests()
    {
        var enrollment = new MfaService().CreateEnrollment(3);
        Assert.Equal(3, enrollment.RecoveryCodeDigests.Count);
        Assert.All(enrollment.RecoveryCodeDigests, digest => Assert.Equal(64, digest.Length));
        Assert.False(new MfaService().VerifyTotp(enrollment.SecretBase32, "000000", DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task MfaCredentialManagerRequiresAndConsumesRecoveryCode()
    {
        var identityId = IdentityId.New();
        var manager = new MfaCredentialManager(new MfaService(), new InMemoryMfaCredentialStore(), DataProtectionProvider.Create("platform-kernel-tests"));
        var enrollment = await manager.EnrollAsync(identityId);

        Assert.True(await manager.IsEnabledAsync(identityId));
        Assert.True(await manager.VerifyAsync(identityId, enrollment.RecoveryCodes[0], DateTimeOffset.UtcNow));
        Assert.False(await manager.VerifyAsync(identityId, enrollment.RecoveryCodes[0], DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task SessionRevocationInvalidatesExistingSession()
    {
        var service = new SessionService(TimeProvider.System);
        var identity = new Core.Identity.Identity(Shared.Primitives.Identifiers.IdentityId.New(), "A@EXAMPLE.COM", true, IdentityStatus.Active, DateTimeOffset.UtcNow, null, Guid.NewGuid());
        var membership = new Membership(Shared.Primitives.Identifiers.MembershipId.New(), Shared.Primitives.Identifiers.TenantId.New(), identity.Id, "A", MembershipStatus.Active, null, null, "en", "UTC", DateTimeOffset.UtcNow, 1);
        var session = (await service.CreateAsync(identity, membership))!;
        Assert.True(service.IsValid(session, DateTimeOffset.UtcNow));
        await service.RevokeAsync(identity.Id);
        Assert.False(service.IsValid(session, DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task SessionActivityIsBoundedAndCannotCrossReauthenticationDeadline()
    {
        var initial = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var clock = new FakeTimeProvider(initial);
        var policy = new SessionPolicy(TimeSpan.FromMinutes(30), TimeSpan.FromHours(24), TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5));
        var service = new SessionService(clock, policy: policy);
        var identity = new Core.Identity.Identity(IdentityId.New(), "A@EXAMPLE.COM", true, IdentityStatus.Active, initial, null, Guid.NewGuid());
        var membership = new Membership(MembershipId.New(), TenantId.New(), identity.Id, "A", MembershipStatus.Active, null, null, "en", "UTC", initial, 1);
        var session = (await service.CreateAsync(identity, membership))!;

        clock.Advance(TimeSpan.FromSeconds(30));
        Assert.True(await service.AcceptActivityAsync(session.SessionId, identity.Id, membership.TenantId));
        var throttled = (await service.LoadAsync(session.SessionId, identity.Id, membership.TenantId))!;
        Assert.Equal(session.LastActivityAt, throttled.LastActivityAt);

        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.True(await service.AcceptActivityAsync(session.SessionId, identity.Id, membership.TenantId));
        var refreshed = (await service.LoadAsync(session.SessionId, identity.Id, membership.TenantId))!;
        Assert.Equal(initial.AddMinutes(1).AddSeconds(30), refreshed.LastActivityAt);
        Assert.True(refreshed.ExpiresAt <= refreshed.ReauthenticationExpiresAt);
        Assert.False(await service.RevokeAsync(session.SessionId, TenantId.New()));
    }

    [Fact]
    public async Task SessionMfaStepUpIsTenantBoundAndFreshnessIsPersisted()
    {
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var service = new SessionService(new FakeTimeProvider(now));
        var identity = new Core.Identity.Identity(IdentityId.New(), "A@EXAMPLE.COM", true, IdentityStatus.Active, now, null, Guid.NewGuid());
        var membership = new Membership(MembershipId.New(), TenantId.New(), identity.Id, "A", MembershipStatus.Active, null, null, "en", "UTC", now, 1);
        var session = (await service.CreateAsync(identity, membership))!;

        Assert.False(await service.MarkMfaVerifiedAsync(session.SessionId, identity.Id, TenantId.New(), now));
        Assert.True(await service.MarkMfaVerifiedAsync(session.SessionId, identity.Id, membership.TenantId, now));
        Assert.Equal(now, (await service.LoadAsync(session.SessionId, identity.Id, membership.TenantId))!.MfaVerifiedAt);
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
        var tenant = Shared.Primitives.Identifiers.TenantId.New();
        var actor = Guid.NewGuid();
        var grant = service.Request(tenant, actor, "incident response", now.AddMinutes(10));
        service.Approve(grant.Id, Guid.NewGuid());
        Assert.False(service.CanAccess(grant.Id, Shared.Primitives.Identifiers.TenantId.New(), actor));
        Assert.True(service.CanAccess(grant.Id, tenant, actor));
        clock.Advance(TimeSpan.FromMinutes(11));
        Assert.False(service.CanAccess(grant.Id, tenant, actor));
    }

    [Fact]
    public void MaintenanceExpiresApprovedSupportGrants()
    {
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var clock = new FakeTimeProvider(now);
        var service = new SupportAccessService(clock);
        var grant = service.Request(TenantId.New(), Guid.NewGuid(), "incident response", now.AddMinutes(1));
        service.Approve(grant.Id, Guid.NewGuid());
        clock.Advance(TimeSpan.FromMinutes(2));

        Assert.Equal(1, service.ExpireDueGrants());
        Assert.Equal(0, service.ExpireDueGrants());
    }

    [Fact]
    public void SignedFileAccessIsTenantBoundAndExpires()
    {
        var tenant = TenantId.New();
        var otherTenant = TenantId.New();
        var file = new FileRecord(Guid.NewGuid(), tenant, "sales", "invoice", Guid.NewGuid(), "tenant/object", "invoice.pdf", "application/pdf", 12, "hash", FileStatus.Ready, "internal", Guid.NewGuid(), DateTimeOffset.UtcNow);
        var signer = new HmacFileAccessSigner("file-secret");
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(5);
        var url = signer.CreateDownloadUrl(file, tenant, expiresAt);
        var signature = url[(url.IndexOf("signature=", StringComparison.Ordinal) + "signature=".Length)..];

        Assert.True(signer.VerifyDownload(file, tenant, expiresAt, signature, DateTimeOffset.UtcNow));
        Assert.False(signer.VerifyDownload(file, otherTenant, expiresAt, signature, DateTimeOffset.UtcNow));
        Assert.False(signer.VerifyDownload(file, tenant, expiresAt, "not-hex", DateTimeOffset.UtcNow));
        Assert.False(signer.VerifyDownload(file, tenant, expiresAt, signature, expiresAt));
    }

    [Fact]
    public void WebhookSignatureRejectsReplayAndMalformedInput()
    {
        var now = DateTimeOffset.UtcNow;
        var verifier = new HmacWebhookVerifier("webhook-secret");
        var body = "{\"event\":\"paid\"}"u8.ToArray();
        var timestamp = now.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        var payload = System.Text.Encoding.UTF8.GetBytes(timestamp + "." + System.Text.Encoding.UTF8.GetString(body));
        var signature = Convert.ToHexString(System.Security.Cryptography.HMACSHA256.HashData(System.Text.Encoding.UTF8.GetBytes("webhook-secret"), payload));

        Assert.True(verifier.Verify(body, timestamp, signature, now));
        Assert.False(verifier.Verify(body, timestamp, "malformed", now));
        Assert.False(verifier.Verify(body, (now - TimeSpan.FromHours(1)).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture), signature, now));
    }

    [Fact]
    public void ImportCheckpointIsIdempotentAndResumable()
    {
        var store = new InMemoryImportCheckpointStore();
        var batchId = Guid.CreateVersion7();
        var checkpoint = new ImportCheckpoint(batchId, 4, 400, 100, "digest", DateTimeOffset.UtcNow);
        Assert.True(store.TrySave(checkpoint));
        Assert.False(store.TrySave(checkpoint));
        Assert.Equal(checkpoint, store.Find(batchId, 4));
    }

    private sealed class FakeTimeProvider(DateTimeOffset initialTime) : TimeProvider
    {
        private DateTimeOffset current = initialTime;
        public override DateTimeOffset GetUtcNow() => current;
        public void Advance(TimeSpan amount) => current = current.Add(amount);
    }
}

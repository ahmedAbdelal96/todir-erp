using Core.Identity;
using Microsoft.AspNetCore.DataProtection;
using Shared.Primitives.Identifiers;
using Xunit;
using IdentityRecord = Core.Identity.Identity;

namespace Core.Tests;

public sealed class AuthenticationFlowTests
{
    [Fact]
    public async Task TenantBoundAccountAuthenticatesWithoutOrganizationSelection()
    {
        var identity = CreateIdentity("one@example.com", TenantId.New());
        var membership = CreateMembership(identity.Id, identity.TenantId!.Value, "One");
        var flow = CreateFlow(new FakeIdentityStore(identity, membership));

        var result = await flow.BeginAsync("one@example.com", "Password1234");

        Assert.Equal(AuthenticationNextStep.Authenticated, result.State);
        Assert.Equal(identity.TenantId.Value, result.Session!.TenantId);
        Assert.Null(result.ChallengeToken);
    }

    [Fact]
    public async Task MfaIsASeparateShortLivedStepAndCompletesForTheBoundTenant()
    {
        var identity = CreateIdentity("mfa@example.com", TenantId.New(), mfaEnabled: true);
        var membership = CreateMembership(identity.Id, identity.TenantId!.Value, "MFA");
        var credentialStore = new InMemoryMfaCredentialStore();
        var manager = new MfaCredentialManager(new MfaService(), credentialStore, DataProtectionProvider.Create("authentication-flow-tests"));
        var enrollment = await manager.EnrollAsync(identity.Id);
        var flow = CreateFlow(new FakeIdentityStore(identity, membership), manager);

        var begin = await flow.BeginAsync("mfa@example.com", "Password1234");
        Assert.Equal(AuthenticationNextStep.VerifyMfa, begin.State);
        var completed = await flow.VerifyMfaAsync(begin.ChallengeToken!, enrollment.RecoveryCodes[0]);

        Assert.Equal(AuthenticationNextStep.Authenticated, completed.State);
        Assert.True(completed.MfaVerified);
        Assert.Equal(identity.TenantId.Value, completed.Session!.TenantId);
    }

    [Fact]
    public async Task TemporaryPasswordRequiresPasswordChangeBeforeSession()
    {
        var identity = CreateIdentity("first@example.com", TenantId.New(), mustChangePassword: true);
        var membership = CreateMembership(identity.Id, identity.TenantId!.Value, "First");
        var flow = CreateFlow(new FakeIdentityStore(identity, membership));

        var begin = await flow.BeginAsync("first@example.com", "Password1234");
        Assert.Equal(AuthenticationNextStep.ChangePassword, begin.State);
        var completed = await flow.ChangePasswordAsync(begin.ChallengeToken!, "NewPassword1234");
        Assert.Equal(AuthenticationNextStep.Authenticated, completed.State);
        Assert.Equal(identity.TenantId.Value, completed.Session!.TenantId);
    }

    private static AuthenticationFlowService CreateFlow(FakeIdentityStore store, MfaCredentialManager? mfa = null) => new(
        store,
        new InMemoryAuthenticationChallengeStore(TimeProvider.System),
        new SessionService(TimeProvider.System),
        mfa ?? new MfaCredentialManager(new MfaService(), new InMemoryMfaCredentialStore(), DataProtectionProvider.Create("authentication-flow-tests-default")),
        TimeProvider.System);

    private static IdentityRecord CreateIdentity(string email, TenantId tenantId, bool mustChangePassword = false, bool mfaEnabled = false) => new(IdentityId.New(), IdentityConventions.NormalizeEmail(email), true, IdentityStatus.Active, DateTimeOffset.UtcNow, null, Guid.NewGuid(), IdentityConventions.HashPassword("Password1234"), tenantId, "Test User", email.Split('@')[0], mustChangePassword, mfaEnabled);
    private static Membership CreateMembership(IdentityId identityId, TenantId tenantId, string displayName) => new(MembershipId.New(), tenantId, identityId, displayName, MembershipStatus.Active, null, null, "ar", "Africa/Cairo", DateTimeOffset.UtcNow, 1);

    private sealed class FakeIdentityStore(IdentityRecord identity, Membership membership) : IIdentityStore
    {
        public Task<IdentityRecord?> AuthenticateAsync(string email, string password, CancellationToken cancellationToken = default) => Task.FromResult<IdentityRecord?>(IdentityConventions.NormalizeEmail(email) == identity.NormalizedEmail && IdentityConventions.VerifyPassword(password, identity.PasswordHash!) ? identity : null);
        public Task<IdentityRecord?> LoadActiveAsync(IdentityId identityId, CancellationToken cancellationToken = default) => Task.FromResult<IdentityRecord?>(identityId == identity.Id && identity.Status == IdentityStatus.Active ? identity : null);
        public Task<bool> CompleteInitialPasswordChangeAsync(IdentityId identityId, string newPassword, CancellationToken cancellationToken = default) => Task.FromResult(identityId == identity.Id && PasswordPolicy.IsValid(newPassword));
        public Task<bool> ChangePasswordAsync(IdentityId identityId, string currentPassword, string newPassword, CancellationToken cancellationToken = default) => Task.FromResult(identityId == identity.Id);
        public Task<Membership?> FindActiveMembershipAsync(IdentityId identityId, TenantId tenantId, CancellationToken cancellationToken = default) => Task.FromResult<Membership?>(identityId == identity.Id && tenantId == membership.TenantId ? membership : null);
        public Task<bool> ValidateSessionAsync(IdentityId identityId, TenantId tenantId, Guid securityStamp, CancellationToken cancellationToken = default) => Task.FromResult(identityId == identity.Id && tenantId == membership.TenantId && securityStamp == identity.SecurityStamp);
        public Task<bool> RevokeSessionsAsync(IdentityId identityId, CancellationToken cancellationToken = default) => Task.FromResult(identityId == identity.Id);
    }
}

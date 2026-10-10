using Core.Identity;
using Microsoft.AspNetCore.DataProtection;
using Shared.Primitives.Identifiers;
using Xunit;
using IdentityRecord = Core.Identity.Identity;

namespace Core.Tests;

public sealed class AuthenticationFlowTests
{
    [Fact]
    public async Task PasswordOnlyIdentityWithOneAccessContextBecomesAuthenticated()
    {
        var identity = CreateIdentity("ONE@EXAMPLE.COM");
        var membership = CreateMembership(identity.Id, TenantId.New(), "One");
        var store = new FakeIdentityStore(identity, [new(membership, "One")]);
        var flow = CreateFlow(store);

        var result = await flow.BeginAsync("one@example.com", "password");

        Assert.Equal(AuthenticationNextStep.Authenticated, result.State);
        Assert.Equal(membership.TenantId.Value, result.Session!.TenantId.Value);
        Assert.Null(result.ChallengeToken);
    }

    [Fact]
    public async Task MultipleAccessContextsRequireOrganizationSelectionAndCannotCrossMembershipBoundary()
    {
        var identity = CreateIdentity("MULTI@EXAMPLE.COM");
        var first = CreateMembership(identity.Id, TenantId.New(), "First");
        var second = CreateMembership(identity.Id, TenantId.New(), "Second");
        var store = new FakeIdentityStore(identity, [new(first, "First"), new(second, "Second")]);
        var flow = CreateFlow(store);

        var begin = await flow.BeginAsync("multi@example.com", "password");
        Assert.Equal(AuthenticationNextStep.SelectOrganization, begin.State);
        Assert.Equal(2, begin.Organizations.Count);
        Assert.Null((await flow.SelectOrganizationAsync(begin.ChallengeToken!, Guid.NewGuid())).Session);

        var selected = await flow.SelectOrganizationAsync(begin.ChallengeToken!, second.Id.Value);
        Assert.Equal(AuthenticationNextStep.Authenticated, selected.State);
        Assert.Equal(second.TenantId, selected.Session!.TenantId);
        Assert.Equal(AuthenticationNextStep.Rejected, (await flow.SelectOrganizationAsync(begin.ChallengeToken!, first.Id.Value)).State);
    }

    [Fact]
    public async Task MfaIsASeparateShortLivedStepBeforeOrganizationResolution()
    {
        var identity = CreateIdentity("MFA@EXAMPLE.COM");
        var membership = CreateMembership(identity.Id, TenantId.New(), "MFA");
        var secondMembership = CreateMembership(identity.Id, TenantId.New(), "MFA second");
        var credentialStore = new InMemoryMfaCredentialStore();
        var manager = new MfaCredentialManager(new MfaService(), credentialStore, DataProtectionProvider.Create("authentication-flow-tests"));
        var enrollment = await manager.EnrollAsync(identity.Id);
        var flow = CreateFlow(new FakeIdentityStore(identity, [new(membership, "MFA"), new(secondMembership, "MFA second")]), manager);

        var begin = await flow.BeginAsync("mfa@example.com", "password");
        Assert.Equal(AuthenticationNextStep.VerifyMfa, begin.State);
        Assert.Null(begin.Session);
        var completed = await flow.VerifyMfaAsync(begin.ChallengeToken!, enrollment.RecoveryCodes[0]);
        Assert.Equal(AuthenticationNextStep.SelectOrganization, completed.State);
        Assert.True(completed.MfaVerified);
        var selected = await flow.SelectOrganizationAsync(completed.ChallengeToken!, secondMembership.Id.Value);
        Assert.Equal(AuthenticationNextStep.Authenticated, selected.State);
        Assert.True(selected.MfaVerified);
    }

    private static AuthenticationFlowService CreateFlow(FakeIdentityStore store, MfaCredentialManager? mfa = null) => new(
        store,
        new InMemoryAuthenticationChallengeStore(TimeProvider.System),
        new SessionService(TimeProvider.System),
        mfa ?? new MfaCredentialManager(new MfaService(), new InMemoryMfaCredentialStore(), DataProtectionProvider.Create("authentication-flow-tests-default")),
        TimeProvider.System);

    private static IdentityRecord CreateIdentity(string email) => new(IdentityId.New(), email, true, IdentityStatus.Active, DateTimeOffset.UtcNow, null, Guid.NewGuid(), IdentityConventions.HashPassword("password"));
    private static Membership CreateMembership(IdentityId identityId, TenantId tenantId, string displayName) => new(MembershipId.New(), tenantId, identityId, displayName, MembershipStatus.Active, null, null, "ar", "Africa/Cairo", DateTimeOffset.UtcNow, 1);

    private sealed class FakeIdentityStore(IdentityRecord identity, IReadOnlyList<AuthenticationAccessContext> contexts) : IIdentityStore
    {
        public Task<IdentityRecord?> AuthenticateAsync(string email, string password, CancellationToken cancellationToken = default) => Task.FromResult<IdentityRecord?>(IdentityConventions.NormalizeEmail(email) == identity.NormalizedEmail && IdentityConventions.VerifyPassword(password, identity.PasswordHash!) ? identity : null);
        public Task<IdentityRecord?> LoadActiveAsync(IdentityId identityId, CancellationToken cancellationToken = default) => Task.FromResult<IdentityRecord?>(identityId == identity.Id && identity.Status == IdentityStatus.Active ? identity : null);
        public Task<IReadOnlyList<AuthenticationAccessContext>> ListActiveAccessContextsAsync(IdentityId identityId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AuthenticationAccessContext>>(identityId == identity.Id ? contexts : []);
        public Task<IReadOnlyList<Membership>> ListActiveMembershipsAsync(IdentityId identityId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Membership>>(contexts.Where(value => value.Membership.Status == MembershipStatus.Active).Select(value => value.Membership).ToArray());
        public Task<IReadOnlyList<Membership>> ListActiveMembershipsAsync(IdentityId identityId, TenantId tenantId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Membership>>(contexts.Where(value => value.Membership.TenantId == tenantId && value.Membership.Status == MembershipStatus.Active).Select(value => value.Membership).ToArray());
        public Task<Membership?> FindActiveMembershipAsync(IdentityId identityId, TenantId tenantId, CancellationToken cancellationToken = default) => Task.FromResult(contexts.FirstOrDefault(value => value.Membership.TenantId == tenantId && value.Membership.Status == MembershipStatus.Active)?.Membership);
        public Task<bool> ValidateSessionAsync(IdentityId identityId, TenantId tenantId, Guid securityStamp, CancellationToken cancellationToken = default) => Task.FromResult(identityId == identity.Id && contexts.Count > 0 && tenantId == contexts[0].Membership.TenantId && securityStamp == identity.SecurityStamp);
        public Task<bool> RevokeSessionsAsync(IdentityId identityId, CancellationToken cancellationToken = default) => Task.FromResult(identityId == identity.Id);
    }
}

using System.Security.Cryptography;
using Shared.Primitives.Identifiers;

namespace Core.Identity;

public enum AuthenticationNextStep
{
    Authenticated,
    VerifyMfa,
    SelectOrganization,
    Rejected
}

public sealed record AuthenticationAccessContext(Membership Membership, string OrganizationName);

public sealed record AuthenticationOrganizationOption(
    Guid MembershipId,
    Guid TenantId,
    string OrganizationName,
    string MembershipDisplayName);

public sealed record AuthenticationFlowResult(
    AuthenticationNextStep State,
    string? ChallengeToken,
    IReadOnlyList<AuthenticationOrganizationOption> Organizations,
    SessionContext? Session,
    bool MfaVerified,
    string? ErrorCode)
{
    public static AuthenticationFlowResult Rejected(string code) => new(AuthenticationNextStep.Rejected, null, [], null, false, code);
}

public sealed record AuthenticationChallenge(
    IdentityId IdentityId,
    AuthenticationNextStep NextStep,
    DateTimeOffset ExpiresAt,
    int Attempts,
    bool MfaVerified,
    string? Token = null);

public interface IAuthenticationChallengeStore
{
    Task<AuthenticationChallenge> CreateAsync(IdentityId identityId, AuthenticationNextStep nextStep, DateTimeOffset expiresAt, bool mfaVerified = false, CancellationToken cancellationToken = default);
    Task<AuthenticationChallenge?> FindAsync(string token, CancellationToken cancellationToken = default);
    Task<bool> RegisterFailureAsync(string token, CancellationToken cancellationToken = default);
    Task<bool> ConsumeAsync(string token, IdentityId identityId, AuthenticationNextStep nextStep, CancellationToken cancellationToken = default);
}

public sealed class InMemoryAuthenticationChallengeStore(TimeProvider clock) : IAuthenticationChallengeStore
{
    private readonly Dictionary<string, (AuthenticationChallenge Challenge, bool Consumed)> challenges = new(StringComparer.Ordinal);
    private readonly object gate = new();

    public Task<AuthenticationChallenge> CreateAsync(IdentityId identityId, AuthenticationNextStep nextStep, DateTimeOffset expiresAt, bool mfaVerified = false, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var challenge = new AuthenticationChallenge(identityId, nextStep, expiresAt, 0, mfaVerified, token);
        lock (gate) challenges[token] = (challenge, false);
        return Task.FromResult(challenge);
    }

    public Task<AuthenticationChallenge?> FindAsync(string token, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            if (!challenges.TryGetValue(token, out var item) || item.Consumed) return Task.FromResult<AuthenticationChallenge?>(null);
            return Task.FromResult<AuthenticationChallenge?>(item.Challenge);
        }
    }

    public Task<bool> RegisterFailureAsync(string token, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            if (!challenges.TryGetValue(token, out var item) || item.Consumed || item.Challenge.ExpiresAt <= clock.GetUtcNow()) return Task.FromResult(false);
            var attempts = item.Challenge.Attempts + 1;
            var challenge = item.Challenge with { Attempts = attempts };
            challenges[token] = (challenge, attempts >= IdentityConventions.AuthenticationChallengeMaxAttempts);
            return Task.FromResult(attempts < IdentityConventions.AuthenticationChallengeMaxAttempts);
        }
    }

    public Task<bool> ConsumeAsync(string token, IdentityId identityId, AuthenticationNextStep nextStep, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            if (!challenges.TryGetValue(token, out var item) || item.Consumed || item.Challenge.ExpiresAt <= clock.GetUtcNow() || item.Challenge.IdentityId != identityId || item.Challenge.NextStep != nextStep) return Task.FromResult(false);
            challenges[token] = (item.Challenge, true);
            return Task.FromResult(true);
        }
    }
}

public sealed class NpgsqlAuthenticationChallengeStore(Npgsql.NpgsqlDataSource dataSource) : IAuthenticationChallengeStore
{
    public async Task<AuthenticationChallenge> CreateAsync(IdentityId identityId, AuthenticationNextStep nextStep, DateTimeOffset expiresAt, bool mfaVerified = false, CancellationToken cancellationToken = default)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var digest = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
        await using var command = dataSource.CreateCommand("insert into platform_iam.auth_challenge (id, token_digest, identity_id, next_step, attempts, mfa_verified, expires_at, created_at) values ($1, $2, $3, $4, 0, $5, $6, now())");
        command.Parameters.AddWithValue(Guid.CreateVersion7());
        command.Parameters.AddWithValue(digest);
        command.Parameters.AddWithValue(identityId.Value);
        command.Parameters.AddWithValue(nextStep.ToString());
        command.Parameters.AddWithValue(mfaVerified);
        command.Parameters.AddWithValue(expiresAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return new AuthenticationChallenge(identityId, nextStep, expiresAt, 0, mfaVerified, token);
    }

    public async Task<AuthenticationChallenge?> FindAsync(string token, CancellationToken cancellationToken = default)
    {
        var digest = Digest(token);
        await using var command = dataSource.CreateCommand("select identity_id, next_step, expires_at, attempts, mfa_verified from platform_iam.auth_challenge where token_digest = $1 and consumed_at is null and expires_at > now() limit 1");
        command.Parameters.AddWithValue(digest);
        await using var reader = await command.ExecuteReaderAsync(System.Data.CommandBehavior.SingleRow, cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new AuthenticationChallenge(new IdentityId(reader.GetGuid(0)), Enum.Parse<AuthenticationNextStep>(reader.GetString(1)), reader.GetFieldValue<DateTimeOffset>(2), reader.GetInt32(3), reader.GetBoolean(4))
            : null;
    }

    public async Task<bool> RegisterFailureAsync(string token, CancellationToken cancellationToken = default)
    {
        var digest = Digest(token);
        await using var command = dataSource.CreateCommand("with changed as (update platform_iam.auth_challenge set attempts = attempts + 1, consumed_at = case when attempts + 1 >= $2 then now() else consumed_at end where token_digest = $1 and consumed_at is null and expires_at > now() returning attempts) select attempts from changed");
        command.Parameters.AddWithValue(digest);
        command.Parameters.AddWithValue(IdentityConventions.AuthenticationChallengeMaxAttempts);
        var attempts = await command.ExecuteScalarAsync(cancellationToken);
        return attempts is not null && Convert.ToInt32(attempts, System.Globalization.CultureInfo.InvariantCulture) < IdentityConventions.AuthenticationChallengeMaxAttempts;
    }

    public async Task<bool> ConsumeAsync(string token, IdentityId identityId, AuthenticationNextStep nextStep, CancellationToken cancellationToken = default)
    {
        var digest = Digest(token);
        await using var command = dataSource.CreateCommand("update platform_iam.auth_challenge set consumed_at = now() where token_digest = $1 and identity_id = $2 and next_step = $3 and consumed_at is null and expires_at > now() and attempts < $4");
        command.Parameters.AddWithValue(digest);
        command.Parameters.AddWithValue(identityId.Value);
        command.Parameters.AddWithValue(nextStep.ToString());
        command.Parameters.AddWithValue(IdentityConventions.AuthenticationChallengeMaxAttempts);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    private static string Digest(string token) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
}

public sealed class AuthenticationFlowService(
    IIdentityStore identityStore,
    IAuthenticationChallengeStore challenges,
    ISessionService sessions,
    MfaCredentialManager mfa,
    TimeProvider clock)
{
    public async Task<AuthenticationFlowResult> BeginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var identity = await identityStore.AuthenticateAsync(email, password, cancellationToken);
        if (identity is null) return AuthenticationFlowResult.Rejected("auth.invalid_credentials");
        var access = await identityStore.ListActiveAccessContextsAsync(identity.Id, cancellationToken);
        if (access.Count == 0) return AuthenticationFlowResult.Rejected("auth.invalid_credentials");
        var mfaEnabled = await mfa.IsEnabledAsync(identity.Id, cancellationToken);
        if (mfaEnabled)
        {
            var challenge = await challenges.CreateAsync(identity.Id, AuthenticationNextStep.VerifyMfa, clock.GetUtcNow().Add(IdentityConventions.AuthenticationChallengeLifetime), cancellationToken: cancellationToken);
            return new(AuthenticationNextStep.VerifyMfa, challenge.Token, [], null, false, null);
        }
        return await CompleteWithAccessAsync(identity, access, false, cancellationToken);
    }

    public async Task<AuthenticationFlowResult> VerifyMfaAsync(string token, string code, CancellationToken cancellationToken = default)
    {
        var challenge = await challenges.FindAsync(token, cancellationToken);
        if (challenge is null || challenge.NextStep != AuthenticationNextStep.VerifyMfa) return AuthenticationFlowResult.Rejected("auth.challenge_invalid");
        if (!await mfa.VerifyAsync(challenge.IdentityId, code, clock.GetUtcNow(), cancellationToken))
        {
            var available = await challenges.RegisterFailureAsync(token, cancellationToken);
            return available ? new(AuthenticationNextStep.VerifyMfa, token, [], null, false, "auth.mfa_invalid") : AuthenticationFlowResult.Rejected("auth.challenge_invalid");
        }
        if (!await challenges.ConsumeAsync(token, challenge.IdentityId, AuthenticationNextStep.VerifyMfa, cancellationToken)) return AuthenticationFlowResult.Rejected("auth.challenge_invalid");
        var identity = await identityStore.LoadActiveAsync(challenge.IdentityId, cancellationToken);
        if (identity is null) return AuthenticationFlowResult.Rejected("auth.invalid_credentials");
        return await CompleteWithAccessAsync(identity, await identityStore.ListActiveAccessContextsAsync(identity.Id, cancellationToken), true, cancellationToken);
    }

    public async Task<AuthenticationFlowResult> SelectOrganizationAsync(string token, Guid membershipId, CancellationToken cancellationToken = default)
    {
        var challenge = await challenges.FindAsync(token, cancellationToken);
        if (challenge is null || challenge.NextStep != AuthenticationNextStep.SelectOrganization) return AuthenticationFlowResult.Rejected("auth.challenge_invalid");
        var access = await identityStore.ListActiveAccessContextsAsync(challenge.IdentityId, cancellationToken);
        var selected = access.FirstOrDefault(value => value.Membership.Id.Value == membershipId);
        if (selected is null)
        {
            var available = await challenges.RegisterFailureAsync(token, cancellationToken);
            return available ? AuthenticationFlowResult.Rejected("auth.organization_invalid") : AuthenticationFlowResult.Rejected("auth.challenge_invalid");
        }
        if (!await challenges.ConsumeAsync(token, challenge.IdentityId, AuthenticationNextStep.SelectOrganization, cancellationToken)) return AuthenticationFlowResult.Rejected("auth.challenge_invalid");
        var identity = await identityStore.LoadActiveAsync(challenge.IdentityId, cancellationToken);
        if (identity is null) return AuthenticationFlowResult.Rejected("auth.invalid_credentials");
        var session = await sessions.CreateAsync(identity, selected.Membership, cancellationToken);
        return session is null ? AuthenticationFlowResult.Rejected("auth.invalid_credentials") : new(AuthenticationNextStep.Authenticated, null, [], session, challenge.MfaVerified, null);
    }

    private async Task<AuthenticationFlowResult> CompleteWithAccessAsync(Identity identity, IReadOnlyList<AuthenticationAccessContext> access, bool mfaVerified, CancellationToken cancellationToken)
    {
        if (access.Count == 0) return AuthenticationFlowResult.Rejected("auth.invalid_credentials");
        if (access.Count > 1)
        {
            var challenge = await challenges.CreateAsync(identity.Id, AuthenticationNextStep.SelectOrganization, clock.GetUtcNow().Add(IdentityConventions.AuthenticationChallengeLifetime), mfaVerified, cancellationToken);
            return new(AuthenticationNextStep.SelectOrganization, challenge.Token, access.Select(ToOption).ToArray(), null, mfaVerified, null);
        }
        var session = await sessions.CreateAsync(identity, access[0].Membership, cancellationToken);
        return session is null ? AuthenticationFlowResult.Rejected("auth.invalid_credentials") : new(AuthenticationNextStep.Authenticated, null, [], session, mfaVerified, null);
    }

    private static AuthenticationOrganizationOption ToOption(AuthenticationAccessContext access) => new(access.Membership.Id.Value, access.Membership.TenantId.Value, access.OrganizationName, access.Membership.DisplayName);
}

using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Shared.Primitives.Identifiers;

namespace Core.Identity;

public enum IdentityStatus { Active, Suspended, Revoked }
public enum MembershipStatus { Invited, Active, Suspended, Revoked }

public sealed record Identity(
    IdentityId Id,
    string NormalizedEmail,
    bool EmailVerified,
    IdentityStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt,
    Guid SecurityStamp,
    string? PasswordHash = null,
    TenantId? TenantId = null,
    string? Name = null,
    string? LoginPrefix = null,
    bool MustChangePassword = false,
    bool MfaEnabled = false);

public sealed record Membership(
    MembershipId Id,
    TenantId TenantId,
    IdentityId IdentityId,
    string DisplayName,
    MembershipStatus Status,
    LegalEntityId? DefaultLegalEntityId,
    BranchId? DefaultBranchId,
    string? Locale,
    string? TimeZone,
    DateTimeOffset CreatedAt,
    long Version);

public sealed record SessionContext(
    IdentityId IdentityId,
    MembershipId MembershipId,
    TenantId TenantId,
    LegalEntityId? LegalEntityId,
    BranchId? BranchId,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt,
    Guid SecurityStamp,
    Guid SessionId = default,
    DateTimeOffset? LastActivityAt = null,
    DateTimeOffset? ReauthenticationExpiresAt = null,
    DateTimeOffset? MfaVerifiedAt = null);

public sealed record MobileSessionContext(IdentityId IdentityId, MembershipId MembershipId, TenantId TenantId, DateTimeOffset IssuedAt, DateTimeOffset ExpiresAt, Guid SecurityStamp, Guid SessionId, DateTimeOffset? MfaVerifiedAt = null);

public static class LoginDomainConventions
{
    private static readonly System.Text.RegularExpressions.Regex DomainPattern = new(
        "^(?=.{1,253}$)(?:[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\\.)+[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?$",
        System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private static readonly System.Text.RegularExpressions.Regex PrefixPattern = new(
        "^[a-z0-9](?:[a-z0-9._-]{0,62}[a-z0-9])?$",
        System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    public static string NormalizeDomain(string domain)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        var normalized = domain.Trim().ToLowerInvariant();
        if (normalized.Contains('@') || normalized.Contains("://", StringComparison.Ordinal) || normalized.Contains('/') || !DomainPattern.IsMatch(normalized))
            throw new ArgumentException("identity.login_domain_invalid", nameof(domain));
        return normalized;
    }

    public static string NormalizePrefix(string prefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        var normalized = prefix.Trim().ToLowerInvariant();
        if (!PrefixPattern.IsMatch(normalized) || normalized.Contains('@') || normalized.Any(char.IsControl))
            throw new ArgumentException("identity.email_prefix_invalid", nameof(prefix));
        return normalized;
    }

    public static string BuildEmail(string prefix, string domain) => $"{NormalizePrefix(prefix)}@{NormalizeDomain(domain)}";
}

public interface IIdentityAuthenticator
{
    Task<Identity?> AuthenticateAsync(string email, string password, CancellationToken cancellationToken = default);
}

public interface ISessionService
{
    Task<SessionContext?> CreateAsync(Identity identity, Membership membership, CancellationToken cancellationToken = default);
    Task<SessionContext?> CreateAsync(Identity identity, Membership membership, DateTimeOffset? mfaVerifiedAt, CancellationToken cancellationToken = default);
    Task<SessionContext?> LoadAsync(Guid sessionId, IdentityId identityId, TenantId tenantId, CancellationToken cancellationToken = default);
    Task<bool> ValidateAsync(Guid sessionId, IdentityId identityId, TenantId tenantId, Guid securityStamp, CancellationToken cancellationToken = default);
    Task<bool> AcceptActivityAsync(Guid sessionId, IdentityId identityId, TenantId tenantId, CancellationToken cancellationToken = default);
    Task<bool> MarkMfaVerifiedAsync(Guid sessionId, IdentityId identityId, TenantId tenantId, DateTimeOffset verifiedAt, CancellationToken cancellationToken = default);
    Task<bool> RevokeAsync(Guid sessionId, TenantId tenantId, CancellationToken cancellationToken = default);
    Task RevokeAsync(IdentityId identityId, CancellationToken cancellationToken = default);
    bool IsValid(SessionContext session, DateTimeOffset now);
}

public interface IMobileSessionService
{
    Task<MobileSessionContext?> CreateAsync(Identity identity, Membership membership, DateTimeOffset? mfaVerifiedAt = null, CancellationToken cancellationToken = default);
    Task<MobileSessionContext?> LoadAsync(Guid sessionId, IdentityId identityId, TenantId tenantId, CancellationToken cancellationToken = default);
    Task<bool> ValidateAsync(Guid sessionId, IdentityId identityId, TenantId tenantId, Guid securityStamp, CancellationToken cancellationToken = default);
    Task<bool> MarkMfaVerifiedAsync(Guid sessionId, IdentityId identityId, TenantId tenantId, DateTimeOffset verifiedAt, CancellationToken cancellationToken = default);
    Task<bool> RevokeAsync(Guid sessionId, TenantId tenantId, CancellationToken cancellationToken = default);
    Task RevokeAsync(IdentityId identityId, CancellationToken cancellationToken = default);
}

public static class IdentityConventions
{
    private static readonly PasswordHasher<object> PasswordHasher = new();
    public const int FailedLoginThreshold = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan AuthenticationChallengeLifetime = TimeSpan.FromMinutes(5);
    public const int AuthenticationChallengeMaxAttempts = 5;

    public static string NormalizeEmail(string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        return email.Trim().ToUpperInvariant();
    }

    public static string HashPassword(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        return PasswordHasher.HashPassword(new object(), password);
    }

    public static bool VerifyPassword(string password, string encoded)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(encoded)) return false;
        try
        {
            if (PasswordHasher.VerifyHashedPassword(new object(), encoded, password) is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded) return true;
        }
        catch (FormatException) { }
        return VerifyLegacyPassword(password, encoded);
    }

    private static bool VerifyLegacyPassword(string password, string encoded)
    {
        // Existing accounts may still carry the pre-correction hash format; new writes always use PasswordHasher.
        var parts = encoded.Split('$');
        if (parts.Length != 4 || parts[0] != "pbkdf2-sha256" || !int.TryParse(parts[1], out var iterations)) return false;
        try
        {
            var expected = Convert.FromBase64String(parts[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), Convert.FromBase64String(parts[2]), iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException) { return false; }
    }
}

public sealed class SessionService(TimeProvider clock, TimeSpan? lifetime = null, SessionPolicy? policy = null) : ISessionService
{
    private readonly SessionPolicy policy = policy ?? SessionPolicy.Default with { InactivityTimeout = lifetime ?? SessionPolicy.Default.InactivityTimeout };
    private readonly Dictionary<Guid, SessionContext> sessions = [];
    private readonly HashSet<IdentityId> revokedIdentities = [];
    private readonly object gate = new();

    public Task<SessionContext?> CreateAsync(Identity identity, Membership membership, CancellationToken cancellationToken = default) => CreateAsync(identity, membership, null, cancellationToken);

    public Task<SessionContext?> CreateAsync(Identity identity, Membership membership, DateTimeOffset? mfaVerifiedAt, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (identity.Status != IdentityStatus.Active || membership.Status != MembershipStatus.Active) return Task.FromResult<SessionContext?>(null);
        var now = clock.GetUtcNow();
        lock (gate)
        {
            if (revokedIdentities.Contains(identity.Id)) return Task.FromResult<SessionContext?>(null);
            var session = new SessionContext(identity.Id, membership.Id, membership.TenantId, membership.DefaultLegalEntityId, membership.DefaultBranchId, now, now.Add(policy.InactivityTimeout), identity.SecurityStamp, Guid.CreateVersion7(), now, now.Add(policy.ReauthenticationTimeout), mfaVerifiedAt);
            sessions[session.SessionId] = session;
            return Task.FromResult<SessionContext?>(session);
        }
    }

    public Task<SessionContext?> LoadAsync(Guid sessionId, IdentityId identityId, TenantId tenantId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate) return Task.FromResult(sessions.TryGetValue(sessionId, out var session) && session.IdentityId == identityId && session.TenantId == tenantId ? session : null);
    }

    public Task<bool> ValidateAsync(Guid sessionId, IdentityId identityId, TenantId tenantId, Guid securityStamp, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate) return Task.FromResult(sessions.TryGetValue(sessionId, out var session) && session.IdentityId == identityId && session.TenantId == tenantId && session.SecurityStamp == securityStamp && IsValidCore(session, clock.GetUtcNow()));
    }

    public Task<bool> AcceptActivityAsync(Guid sessionId, IdentityId identityId, TenantId tenantId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            if (!sessions.TryGetValue(sessionId, out var session) || session.IdentityId != identityId || session.TenantId != tenantId || !IsValidCore(session, clock.GetUtcNow())) return Task.FromResult(false);
            var now = clock.GetUtcNow();
            if (session.LastActivityAt is { } last && now - last < policy.ActivityWriteInterval) return Task.FromResult(true);
            var expiry = Min(now.Add(policy.InactivityTimeout), session.ReauthenticationExpiresAt ?? now);
            sessions[sessionId] = session with { LastActivityAt = now, ExpiresAt = expiry };
            return Task.FromResult(true);
        }
    }

    public Task<bool> MarkMfaVerifiedAsync(Guid sessionId, IdentityId identityId, TenantId tenantId, DateTimeOffset verifiedAt, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            if (!sessions.TryGetValue(sessionId, out var session) || session.IdentityId != identityId || session.TenantId != tenantId || !IsValidCore(session, verifiedAt)) return Task.FromResult(false);
            sessions[sessionId] = session with { MfaVerifiedAt = verifiedAt };
            return Task.FromResult(true);
        }
    }

    public Task<bool> RevokeAsync(Guid sessionId, TenantId tenantId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            if (!sessions.TryGetValue(sessionId, out var session) || session.TenantId != tenantId) return Task.FromResult(false);
            sessions.Remove(sessionId);
            return Task.FromResult(true);
        }
    }

    public Task RevokeAsync(IdentityId identityId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            revokedIdentities.Add(identityId);
            foreach (var sessionId in sessions.Where(item => item.Value.IdentityId == identityId).Select(item => item.Key).ToArray()) sessions.Remove(sessionId);
        }
        return Task.CompletedTask;
    }

    public bool IsValid(SessionContext session, DateTimeOffset now)
    {
        lock (gate) return sessions.ContainsKey(session.SessionId) && IsValidCore(session, now);
    }

    private bool IsValidCore(SessionContext session, DateTimeOffset now) => now < session.ExpiresAt && (session.ReauthenticationExpiresAt is null || now < session.ReauthenticationExpiresAt) && !revokedIdentities.Contains(session.IdentityId);
    private static DateTimeOffset Min(DateTimeOffset left, DateTimeOffset right) => left <= right ? left : right;
}

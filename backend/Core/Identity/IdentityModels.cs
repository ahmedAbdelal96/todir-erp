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
    Guid SecurityStamp);

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
    Task RevokeAsync(IdentityId identityId, CancellationToken cancellationToken = default);
    bool IsValid(SessionContext session, DateTimeOffset now);
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

public sealed class SessionService(TimeProvider clock, TimeSpan? lifetime = null) : ISessionService
{
    private readonly TimeSpan lifetime = lifetime ?? TimeSpan.FromHours(8);
    private readonly HashSet<IdentityId> revokedIdentities = [];
    private readonly object gate = new();

    public Task<SessionContext?> CreateAsync(Identity identity, Membership membership, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (identity.Status != IdentityStatus.Active || membership.Status != MembershipStatus.Active) return Task.FromResult<SessionContext?>(null);
        var now = clock.GetUtcNow();
        lock (gate) if (revokedIdentities.Contains(identity.Id)) return Task.FromResult<SessionContext?>(null);
        return Task.FromResult<SessionContext?>(new SessionContext(identity.Id, membership.Id, membership.TenantId, membership.DefaultLegalEntityId, membership.DefaultBranchId, now, now.Add(lifetime), identity.SecurityStamp));
    }

    public Task RevokeAsync(IdentityId identityId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate) revokedIdentities.Add(identityId);
        return Task.CompletedTask;
    }

    public bool IsValid(SessionContext session, DateTimeOffset now)
    {
        lock (gate) return now < session.ExpiresAt && !revokedIdentities.Contains(session.IdentityId);
    }
}

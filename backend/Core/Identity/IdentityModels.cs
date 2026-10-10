using System.Security.Cryptography;
using System.Text;
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
    string? PasswordHash = null);

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
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, 120_000, HashAlgorithmName.SHA256, 32);
        return $"pbkdf2-sha256$120000${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool VerifyPassword(string password, string encoded)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(encoded)) return false;
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

using System.Collections.Concurrent;
using System.Security.Cryptography;
using Core.Identity;
using Shared.Primitives.Identifiers;

namespace Core.Security;

public sealed record FileValidationResult(bool Accepted, string? ErrorCode);

public sealed class FileUploadPolicy
{
    private static readonly IReadOnlyDictionary<string, byte[]> MagicHeaders = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
    {
        ["application/pdf"] = [0x25, 0x50, 0x44, 0x46],
        ["image/png"] = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A],
        ["image/jpeg"] = [0xFF, 0xD8, 0xFF]
    };

    private readonly long maximumBytes;
    private readonly IReadOnlySet<string> allowedContentTypes;

    public FileUploadPolicy(long maximumBytes = 25 * 1024 * 1024, IReadOnlySet<string>? allowedContentTypes = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumBytes, 1, nameof(maximumBytes));
        this.maximumBytes = maximumBytes;
        this.allowedContentTypes = allowedContentTypes ?? new HashSet<string>(MagicHeaders.Keys, StringComparer.OrdinalIgnoreCase);
    }

    public FileValidationResult Validate(string originalName, string contentType, ReadOnlySpan<byte> content, long declaredSize)
    {
        if (string.IsNullOrWhiteSpace(originalName) || originalName.IndexOfAny(['/', '\\', '\0']) >= 0) return new(false, "file.invalid_name");
        if (declaredSize < 0 || declaredSize > maximumBytes || declaredSize != content.Length) return new(false, "file.invalid_size");
        if (!allowedContentTypes.Contains(contentType) || !MagicHeaders.TryGetValue(contentType, out var magic) || content.Length < magic.Length || !content[..magic.Length].SequenceEqual(magic)) return new(false, "file.content_mismatch");
        return new(true, null);
    }
}

public sealed record SupportAccessGrant(Guid Id, TenantId TenantId, Guid SupportActorId, string Reason, Guid? ApprovedBy, DateTimeOffset ExpiresAt, SupportAccessStatus Status, DateTimeOffset CreatedAt);
public enum SupportAccessStatus { Requested, Approved, Active, Expired, Revoked }
public sealed class SupportAccessService(TimeProvider timeProvider)
{
    private readonly ConcurrentDictionary<Guid, SupportAccessGrant> grants = new();

    public bool IsMfaProofValid(MfaStepUpProof? proof, Guid actorId) => proof is not null && proof.IdentityId.Value == actorId && proof.IsValid(timeProvider);

    public SupportAccessGrant Request(TenantId tenantId, Guid supportActorId, string reason, DateTimeOffset expiresAt)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("support.reason_required", nameof(reason));
        if (expiresAt <= timeProvider.GetUtcNow()) throw new InvalidOperationException("support.expiry_required");
        var grant = new SupportAccessGrant(Guid.NewGuid(), tenantId, supportActorId, reason.Trim(), null, expiresAt, SupportAccessStatus.Requested, timeProvider.GetUtcNow());
        grants[grant.Id] = grant;
        return grant;
    }

    public SupportAccessGrant Approve(Guid grantId, Guid approvedBy) => Approve(null, grantId, approvedBy);

    public SupportAccessGrant Approve(TenantId? tenantId, Guid grantId, Guid approvedBy)
    {
        if (!grants.TryGetValue(grantId, out var grant)) throw new KeyNotFoundException("support.grant_not_found");
        if (tenantId is { } selectedTenant && grant.TenantId != selectedTenant) throw new KeyNotFoundException("support.grant_not_found");
        if (grant.ExpiresAt <= timeProvider.GetUtcNow()) throw new InvalidOperationException("support.grant_expired");
        var approved = grant with { ApprovedBy = approvedBy, Status = SupportAccessStatus.Approved };
        grants[grantId] = approved;
        return approved;
    }

    public bool CanAccess(Guid grantId, TenantId tenantId, Guid supportActorId)
    {
        if (!grants.TryGetValue(grantId, out var grant) || grant.TenantId != tenantId || grant.SupportActorId != supportActorId || grant.Status is not (SupportAccessStatus.Approved or SupportAccessStatus.Active)) return false;
        if (grant.ExpiresAt <= timeProvider.GetUtcNow())
        {
            grants[grantId] = grant with { Status = SupportAccessStatus.Expired };
            return false;
        }
        if (grant.Status == SupportAccessStatus.Approved) grants[grantId] = grant with { Status = SupportAccessStatus.Active };
        return true;
    }

    public bool Revoke(Guid grantId)
    {
        return Revoke(null, grantId);
    }

    public bool Revoke(TenantId? tenantId, Guid grantId)
    {
        if (!grants.TryGetValue(grantId, out var grant) || tenantId is { } selectedTenant && grant.TenantId != selectedTenant) return false;
        grants[grantId] = grant with { Status = SupportAccessStatus.Revoked };
        return true;
    }

    public int ExpireDueGrants()
    {
        var now = timeProvider.GetUtcNow();
        var expired = 0;
        foreach (var pair in grants)
        {
            if (pair.Value.ExpiresAt > now || pair.Value.Status is SupportAccessStatus.Expired or SupportAccessStatus.Revoked) continue;
            if (grants.TryUpdate(pair.Key, pair.Value with { Status = SupportAccessStatus.Expired }, pair.Value)) expired++;
        }
        return expired;
    }
}

public sealed record ImportCheckpoint(Guid BatchId, int ChunkNumber, int StartRow, int RowCount, string InputDigest, DateTimeOffset SavedAt);
public interface IImportCheckpointStore
{
    bool TrySave(ImportCheckpoint checkpoint);
    ImportCheckpoint? Find(Guid batchId, int chunkNumber);
}
public sealed class InMemoryImportCheckpointStore : IImportCheckpointStore
{
    private readonly ConcurrentDictionary<(Guid BatchId, int ChunkNumber), ImportCheckpoint> checkpoints = new();
    public bool TrySave(ImportCheckpoint checkpoint) => checkpoints.TryAdd((checkpoint.BatchId, checkpoint.ChunkNumber), checkpoint);
    public ImportCheckpoint? Find(Guid batchId, int chunkNumber) => checkpoints.TryGetValue((batchId, chunkNumber), out var checkpoint) ? checkpoint : null;
}

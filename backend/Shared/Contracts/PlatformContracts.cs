using Shared.Primitives.Identifiers;

namespace Shared.Contracts.Platform;

public interface IPlatformOrganizationCapabilities
{
    Task<bool> BranchBelongsToLegalEntityAsync(TenantId tenantId, LegalEntityId legalEntityId, Guid branchId, CancellationToken cancellationToken = default);
}

public interface IPlatformMembershipLifecycleCapability
{
    Task<bool> DeactivateMembershipAsync(TenantId tenantId, Guid membershipId, string reasonCode, CancellationToken cancellationToken = default);
}

/// <summary>A tenant-scoped immutable reference returned by the Platform Files boundary; file bytes remain outside business modules.</summary>
public sealed record VerifiedPlatformFileReference(TenantId TenantId, Guid FileRef, string Sha256, string ContentType, long SizeBytes, string Classification = "internal");
public sealed record SignedPlatformFileDownload(string Url, DateTimeOffset ExpiresAt);
public sealed class PlatformFileStorageUnavailableException() : Exception("platform.files_storage_unavailable");
public interface IPlatformFileReferenceCapability
{
    Task<VerifiedPlatformFileReference?> VerifyReadyReferenceAsync(TenantId tenantId, Guid fileRef, string ownerModule, string ownerEntityType, Guid ownerEntityId, CancellationToken cancellationToken = default);
    Task<SignedPlatformFileDownload?> CreateReadyDownloadAsync(TenantId tenantId, Guid fileRef, string ownerModule, string ownerEntityType, Guid ownerEntityId, TimeSpan lifetime, CancellationToken cancellationToken = default);
}

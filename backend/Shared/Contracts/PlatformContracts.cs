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
public sealed record SignedPlatformFileUpload(Guid FileRef, string Url, IReadOnlyDictionary<string, string> Fields, string ContentType, long SizeBytes, DateTimeOffset ExpiresAt);
public sealed record PlatformFileUploadCompletion(bool Ready, bool Rejected, string? ReasonCode = null);
public sealed class PlatformFileStorageUnavailableException() : Exception("platform.files_storage_unavailable");
public interface IPlatformFileReferenceCapability
{
    Task<VerifiedPlatformFileReference?> VerifyReadyReferenceAsync(TenantId tenantId, Guid fileRef, string ownerModule, string ownerEntityType, Guid ownerEntityId, CancellationToken cancellationToken = default);
    Task<SignedPlatformFileDownload?> CreateReadyDownloadAsync(TenantId tenantId, Guid fileRef, string ownerModule, string ownerEntityType, Guid ownerEntityId, TimeSpan lifetime, CancellationToken cancellationToken = default);
}

public interface IPlatformFileUploadCapability
{
    Task<SignedPlatformFileUpload?> CreateUploadIntentAsync(TenantId tenantId, Guid actorId, Guid membershipId, string ownerModule, string ownerEntityType, Guid ownerEntityId, string originalName, string contentType, long sizeBytes, string classification, CancellationToken cancellationToken = default);
    Task<PlatformFileUploadCompletion?> CompleteUploadAsync(TenantId tenantId, Guid actorId, Guid membershipId, string ownerModule, string ownerEntityType, Guid ownerEntityId, Guid fileRef, CancellationToken cancellationToken = default);
}

public sealed record PlatformApprovalRequestResult(Guid Id, Guid LegalEntityId, string OwnerModule, string OwnerEntityType, Guid OwnerEntityId, long ResourceVersion, Guid? PolicyVersionId, string State, Guid? DecidedBy, string? DecisionReason, DateTimeOffset CreatedAt, DateTimeOffset? DecidedAt);
public interface IPlatformApprovalCapability
{
    Task<PlatformApprovalRequestResult> RequestAsync(TenantId tenantId, Guid actorId, Guid membershipId, Guid legalEntityId, string ownerModule, string ownerEntityType, Guid ownerEntityId, long resourceVersion, Guid? policyVersionId, string? reason, CancellationToken cancellationToken = default);
    Task<PlatformApprovalRequestResult> DecideAsync(TenantId tenantId, Guid actorId, Guid membershipId, Guid requestId, string decision, string? reason, CancellationToken cancellationToken = default);
    Task<PlatformApprovalRequestResult?> GetAsync(TenantId tenantId, Guid requestId, CancellationToken cancellationToken = default);
}

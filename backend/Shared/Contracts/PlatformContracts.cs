using Shared.Primitives.Identifiers;

namespace Shared.Contracts.Platform;

public interface IPlatformOrganizationCapabilities
{
    Task<bool> BranchBelongsToLegalEntityAsync(TenantId tenantId, LegalEntityId legalEntityId, Guid branchId, CancellationToken cancellationToken = default);
}

/// <summary>A tenant-scoped immutable reference returned by the Platform Files boundary; file bytes remain outside business modules.</summary>
public sealed record VerifiedPlatformFileReference(TenantId TenantId, Guid FileRef, string Sha256, string ContentType, long SizeBytes);

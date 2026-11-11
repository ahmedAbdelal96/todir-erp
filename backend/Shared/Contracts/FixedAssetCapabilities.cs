using Shared.Primitives.Identifiers;

namespace Shared.Contracts.FixedAssets;

public sealed record FixedAssetCustodyItem(Guid AssetId, string AssetNumber, string Name, Guid LegalEntityId, Guid AssignmentId, DateOnly EffectiveFrom, DateOnly? EffectiveTo, Guid? LocationId, string? Condition);

public interface IFixedAssetCustodyCapabilities
{
    Task<IReadOnlyList<FixedAssetCustodyItem>> ListEmployeeAssetsAsync(TenantId tenantId, Guid employeeId, Guid legalEntityId, DateOnly asOfDate, CancellationToken cancellationToken = default);
}

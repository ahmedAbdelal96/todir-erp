using Shared.Primitives.Identifiers;

namespace Shared.Contracts.Inventory;

public sealed record FixedAssetInventorySourceSnapshot(
    Guid ProductVariantId,
    Guid? SerialId,
    decimal Quantity,
    decimal CarryingValue,
    string CurrencyCode,
    string CostingMethod,
    DateOnly AsOfDate,
    IReadOnlyList<Guid> MovementIds,
    IReadOnlyList<Guid> SourceDocumentIds);

public interface IInventoryFixedAssetCapabilities
{
    Task<FixedAssetInventorySourceSnapshot?> GetInternalUseCapitalizationSourceAsync(
        TenantId tenantId, Guid legalEntityId, Guid productVariantId, Guid? serialId, DateOnly asOfDate,
        CancellationToken cancellationToken = default);
}

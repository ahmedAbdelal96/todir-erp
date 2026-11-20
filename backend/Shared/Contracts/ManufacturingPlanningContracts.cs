using Shared.Primitives.Identifiers;

namespace Shared.Contracts.ManufacturingPlanning;

public sealed record ManufacturingDemandSnapshot(Guid SourceDocumentId, Guid SourceLineId, Guid LegalEntityId, Guid ProductVariantId, DateOnly NeedDate, decimal RemainingQuantity, string UomCode, string DemandType, string SourceReference);
public sealed record ManufacturingSupplySnapshot(Guid SourceDocumentId, Guid SourceLineId, Guid LegalEntityId, Guid ProductVariantId, DateOnly ExpectedDate, decimal RemainingQuantity, string UomCode, string SupplyType, string SourceReference, string State);

public interface IManufacturingDemandCapabilities
{
    Task<IReadOnlyList<ManufacturingDemandSnapshot>> ListAcceptedDemandAsync(TenantId tenant, Guid legalEntityId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
}

public interface IManufacturingSupplyCapabilities
{
    Task<IReadOnlyList<ManufacturingSupplySnapshot>> ListEligibleSupplyAsync(TenantId tenant, Guid legalEntityId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
}

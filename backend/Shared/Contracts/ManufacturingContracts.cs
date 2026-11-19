namespace Shared.Contracts.Manufacturing;

public sealed record ManufacturingCostSource(string SourceType, string SourceId, string CostCategory, decimal Amount, string CurrencyCode, DateOnly SourceDate);
public sealed record ManufacturingProductionFact(Guid TenantId, Guid LegalEntityId, Guid PlantId, Guid ProductionOrderId, string Number, Guid ProductVariantId, decimal Quantity, decimal ActualCost, string CurrencyCode, DateOnly FactDate);

public interface IManufacturingCostReader
{
    Task<ManufacturingProductionFact?> GetProductionFactAsync(Guid tenantId, Guid productionOrderId, CancellationToken cancellationToken = default);
}

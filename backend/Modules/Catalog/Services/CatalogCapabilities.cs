using Modules.Catalog.Repositories;
using Shared.Contracts.Catalog;
using Shared.Primitives.Identifiers;

namespace Modules.Catalog.Services;

/// <summary>Applies Catalog capability rules over tenant-scoped repository reads.</summary>
public sealed class CatalogCapabilities(CatalogCapabilityRepository repository) : ICatalogCapabilities, IQuantityConversion
{
    public Task<CatalogProductSummary?> GetProductSummaryAsync(TenantId tenantId, Guid productTemplateId, Guid? legalEntityId, CancellationToken cancellationToken = default)
        => repository.GetProductSummaryAsync(tenantId, productTemplateId, legalEntityId, cancellationToken);

    public Task<bool> IsActiveCategoryAsync(TenantId tenantId, Guid categoryId, CancellationToken cancellationToken = default)
        => repository.IsActiveCategoryAsync(tenantId, categoryId, cancellationToken);

    public Task<bool> IsActiveBrandAsync(TenantId tenantId, Guid brandId, CancellationToken cancellationToken = default)
        => repository.IsActiveBrandAsync(tenantId, brandId, cancellationToken);

    public Task<CatalogVariantIdentity?> FindVariantAsync(TenantId tenantId, Guid productVariantId, Guid? legalEntityId, CancellationToken cancellationToken = default)
        => repository.FindVariantAsync(tenantId, productVariantId, legalEntityId, cancellationToken);

    public Task<CatalogVariantPricingContext?> GetPricingContextAsync(TenantId tenantId, Guid productVariantId, Guid? legalEntityId, CancellationToken cancellationToken = default)
        => repository.GetPricingContextAsync(tenantId, productVariantId, legalEntityId, cancellationToken);

    public Task<IReadOnlyDictionary<Guid, CatalogVariantPricingContext>> GetPricingContextsBatchAsync(TenantId tenantId, IReadOnlyCollection<Guid> productVariantIds, Guid? legalEntityId, CancellationToken cancellationToken = default)
        => repository.GetPricingContextsBatchAsync(tenantId, productVariantIds, legalEntityId, cancellationToken);

    public Task<IReadOnlyDictionary<Guid, Guid>> GetBaseUomsBatchAsync(TenantId tenantId, IReadOnlyCollection<Guid> productVariantIds, DateTimeOffset effectiveAt, Guid? legalEntityId, CancellationToken cancellationToken = default)
        => repository.GetBaseUomsBatchAsync(tenantId, productVariantIds, effectiveAt, legalEntityId, cancellationToken);

    public Task<IReadOnlyDictionary<Guid, IReadOnlyList<CatalogAllowedUom>>> GetAllowedUomsBatchAsync(TenantId tenantId, IReadOnlyCollection<Guid> productVariantIds, DateTimeOffset effectiveAt, Guid? legalEntityId, CancellationToken cancellationToken = default)
        => repository.GetAllowedUomsBatchAsync(tenantId, productVariantIds, effectiveAt, legalEntityId, cancellationToken);

    public Task<CatalogVariantSnapshot?> ResolveVariantAsync(TenantId tenantId, string skuOrBarcode, DateTimeOffset effectiveAt, Guid? legalEntityId, CancellationToken cancellationToken = default)
        => repository.ResolveVariantAsync(tenantId, skuOrBarcode, effectiveAt, legalEntityId, cancellationToken);

    public Task<IReadOnlyDictionary<string, CatalogVariantIdentity>> ResolveVariantsBySkuAsync(TenantId tenantId, IReadOnlyCollection<string> skus, Guid? legalEntityId, CancellationToken cancellationToken = default)
        => repository.ResolveVariantsBySkuAsync(tenantId, skus, legalEntityId, cancellationToken);

    public Task<IReadOnlyList<CatalogAllowedUom>> GetAllowedUomsAsync(TenantId tenantId, Guid productVariantId, DateTimeOffset effectiveAt, Guid? legalEntityId, CancellationToken cancellationToken = default)
        => repository.GetAllowedUomsAsync(tenantId, productVariantId, effectiveAt, legalEntityId, cancellationToken);

    public Task<CatalogSearchPage> SearchAsync(TenantId tenantId, string? query, Guid? categoryId, Guid? legalEntityId, int page, int pageSize, CancellationToken cancellationToken = default)
        => repository.SearchAsync(tenantId, query, categoryId, legalEntityId, page, pageSize, cancellationToken);

    public async Task<CatalogQuantityConversion?> ConvertAsync(TenantId tenantId, Guid productVariantId, Guid unitId, decimal quantity, DateTimeOffset effectiveAt, Guid? legalEntityId, CancellationToken cancellationToken = default)
    {
        var validation = await ValidateAsync(tenantId, productVariantId, unitId, quantity, effectiveAt, legalEntityId, cancellationToken);
        if (!validation.IsAllowed || !validation.QuantityValid || validation.FactorToBase is null) return null;
        var baseQuantity = decimal.Round(checked(quantity * validation.FactorToBase.Value), validation.Precision, MidpointRounding.ToEven);
        return new CatalogQuantityConversion(productVariantId, unitId, validation.UnitCode!, quantity, validation.FactorToBase.Value, baseQuantity, effectiveAt);
    }

    public async Task<CatalogUomValidation> ValidateAsync(TenantId tenantId, Guid productVariantId, Guid unitId, decimal quantity, DateTimeOffset effectiveAt, Guid? legalEntityId, CancellationToken cancellationToken = default)
    {
        var unit = (await repository.GetAllowedUomsAsync(tenantId, productVariantId, effectiveAt, legalEntityId, cancellationToken)).FirstOrDefault(item => item.UnitId == unitId);
        if (unit is null) return new CatalogUomValidation(productVariantId, unitId, null, false, false, "catalog.uom_not_allowed", false, 0, null, effectiveAt);
        var quantityValid = unit.AllowFraction || quantity == decimal.Truncate(quantity);
        return new CatalogUomValidation(productVariantId, unitId, unit.UnitCode, true, quantityValid, quantityValid ? null : "catalog.fraction_not_allowed", unit.AllowFraction, unit.RoundingPrecision ?? unit.Precision, unit.FactorToBase, effectiveAt);
    }
}

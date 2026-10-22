using Shared.Primitives.Identifiers;

namespace Shared.Contracts.Catalog;

/// <summary>Stable read capabilities offered by Catalog to other modules.</summary>
public interface ICatalogCapabilities
{
    Task<CatalogProductSummary?> GetProductSummaryAsync(TenantId tenantId, Guid productTemplateId, Guid? legalEntityId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, IReadOnlyList<Guid>>> ResolveProductTemplatesByCodeAsync(TenantId tenantId, IReadOnlyCollection<string> codes, Guid? legalEntityId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, CatalogProductTemplateReferences>> GetProductTemplateReferencesBatchAsync(TenantId tenantId, IReadOnlyCollection<Guid> productTemplateIds, Guid? legalEntityId, CancellationToken cancellationToken = default);
    Task<bool> IsActiveCategoryAsync(TenantId tenantId, Guid categoryId, CancellationToken cancellationToken = default);
    Task<bool> IsActiveBrandAsync(TenantId tenantId, Guid brandId, CancellationToken cancellationToken = default);
    Task<CatalogVariantIdentity?> FindVariantAsync(TenantId tenantId, Guid productVariantId, Guid? legalEntityId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, CatalogVariantIdentity>> FindVariantsBatchAsync(TenantId tenantId, IReadOnlyCollection<Guid> productVariantIds, Guid? legalEntityId, CancellationToken cancellationToken = default);
    Task<CatalogVariantPricingContext?> GetPricingContextAsync(TenantId tenantId, Guid productVariantId, Guid? legalEntityId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, CatalogVariantPricingContext>> GetPricingContextsBatchAsync(TenantId tenantId, IReadOnlyCollection<Guid> productVariantIds, Guid? legalEntityId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, Guid>> GetBaseUomsBatchAsync(TenantId tenantId, IReadOnlyCollection<Guid> productVariantIds, DateTimeOffset effectiveAt, Guid? legalEntityId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<CatalogAllowedUom>>> GetAllowedUomsBatchAsync(TenantId tenantId, IReadOnlyCollection<Guid> productVariantIds, DateTimeOffset effectiveAt, Guid? legalEntityId, CancellationToken cancellationToken = default);
    Task<CatalogVariantSnapshot?> ResolveVariantAsync(TenantId tenantId, string skuOrBarcode, DateTimeOffset effectiveAt, Guid? legalEntityId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, CatalogVariantIdentity>> ResolveVariantsBySkuAsync(TenantId tenantId, IReadOnlyCollection<string> skus, Guid? legalEntityId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, IReadOnlyList<CatalogVariantIdentity>>> ResolveVariantsByExternalReferenceAsync(TenantId tenantId, IReadOnlyCollection<string> references, Guid? legalEntityId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, CatalogVariantReferences>> GetVariantReferencesBatchAsync(TenantId tenantId, IReadOnlyCollection<Guid> productVariantIds, Guid? legalEntityId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CatalogAllowedUom>> GetAllowedUomsAsync(TenantId tenantId, Guid productVariantId, DateTimeOffset effectiveAt, Guid? legalEntityId, CancellationToken cancellationToken = default);
    Task<CatalogQuantityConversion?> ConvertAsync(TenantId tenantId, Guid productVariantId, Guid unitId, decimal quantity, DateTimeOffset effectiveAt, Guid? legalEntityId, CancellationToken cancellationToken = default);
    Task<CatalogSearchPage> SearchAsync(TenantId tenantId, string? query, Guid? categoryId, Guid? legalEntityId, int page, int pageSize, CancellationToken cancellationToken = default);
}

/// <summary>Stable conversion and validation boundary for transaction modules.</summary>
public interface IQuantityConversion
{
    Task<CatalogUomValidation> ValidateAsync(TenantId tenantId, Guid productVariantId, Guid unitId, decimal quantity, DateTimeOffset effectiveAt, Guid? legalEntityId, CancellationToken cancellationToken = default);
    Task<CatalogQuantityConversion?> ConvertAsync(TenantId tenantId, Guid productVariantId, Guid unitId, decimal quantity, DateTimeOffset effectiveAt, Guid? legalEntityId, CancellationToken cancellationToken = default);
}

/// <summary>Product-level identity for other modules; Catalog remains the source of truth.</summary>
public sealed record CatalogProductSummary(Guid ProductTemplateId, string DisplayName, string? NameAr, string? NameEn, string Nature, string Status);
public sealed record CatalogProductTemplateReferences(string ProductTemplateCode, string DisplayName);

/// <summary>Canonical product identity for a transaction line; consumers persist ProductVariantId.</summary>
public sealed record CatalogVariantIdentity(Guid ProductVariantId, Guid ProductTemplateId, string DisplayName, string? Sku, string Nature, string Status);
public sealed record CatalogVariantReferences(string? Sku, IReadOnlyList<string> ExternalReferences);
public sealed record CatalogVariantPricingContext(Guid ProductVariantId, Guid ProductTemplateId, Guid? CategoryId, IReadOnlyList<Guid> CategoryPath, Guid? BrandId, string Status);

/// <summary>Resolved sellable/buyable variant identity and its effective base unit.</summary>
public sealed record CatalogVariantSnapshot(Guid ProductVariantId, Guid ProductTemplateId, string DisplayName, string? Sku, Guid BaseUomId, string BaseUomCode, string Nature, Guid? EnteredUomId, string? EnteredUomCode, decimal? EnteredFactorToBase);

/// <summary>Unit conversion valid for a variant at a specific time.</summary>
public sealed record CatalogAllowedUom(Guid UnitId, string UnitCode, decimal FactorToBase, bool IsBase, bool AllowFraction, int Precision, DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveTo, IReadOnlyList<string>? UsageFlags = null, int? RoundingPrecision = null);
public sealed record CatalogUomValidation(Guid ProductVariantId, Guid UnitId, string? UnitCode, bool IsAllowed, bool QuantityValid, string? FailureCode, bool AllowFraction, int Precision, decimal? FactorToBase, DateTimeOffset EffectiveAt);

/// <summary>Small tenant and entity scoped page for downstream pickers.</summary>
public sealed record CatalogSearchItem(Guid ProductTemplateId, Guid ProductVariantId, string DisplayName, string? NameAr, string? NameEn, string? Sku, string? CategoryName, string? BaseUomCode, string Nature);
public sealed record CatalogSearchPage(IReadOnlyList<CatalogSearchItem> Items, int TotalCount, int Page, int PageSize);

/// <summary>Quantity expressed in the requested unit and its canonical base-unit equivalent.</summary>
public sealed record CatalogQuantityConversion(Guid ProductVariantId, Guid UnitId, string UnitCode, decimal Quantity, decimal FactorToBase, decimal BaseQuantity, DateTimeOffset EffectiveAt);

using Todir.Modules.Catalog.Domain;

namespace Todir.Modules.Catalog.Contracts;

public sealed record CreateProductRequest(string DisplayName, ProductNature Nature, string? Sku, string? NameAr, string? NameEn, Guid? CategoryId, Guid? BrandId, Guid BaseUomId);
public sealed record UpdateProductRequest(string DisplayName, string? Sku, string? NameAr, string? NameEn, Guid? CategoryId, Guid? BrandId, long Version);
public sealed record ProductSummary(Guid Id, Guid DefaultVariantId, string DisplayName, string? NameAr, string? NameEn, ProductNature Nature, string? Sku, string? BaseUomCode, string? CategoryName, string? BrandName, CatalogStatus Status, DateTimeOffset UpdatedAt, long Version);
public sealed record ProductDetails(Guid Id, Guid DefaultVariantId, string DisplayName, string? NameAr, string? NameEn, ProductNature Nature, string? Sku, string? BaseUomCode, Guid? CategoryId, string? CategoryName, Guid? BrandId, string? BrandName, CatalogStatus Status, IReadOnlyList<BarcodeDto> Barcodes, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, long Version);
public sealed record PagedProducts(IReadOnlyList<ProductSummary> Items, int TotalCount, int Page, int PageSize);
public sealed record CreateCategoryRequest(string Name, Guid? ParentId);
public sealed record CategoryDto(Guid Id, string Name, Guid? ParentId, CatalogStatus Status, long Version);
public sealed record CreateBrandRequest(string Name);
public sealed record BrandDto(Guid Id, string Name, CatalogStatus Status, long Version);
public sealed record CreateUomRequest(string Code, string NameAr, string NameEn, UomDimension Dimension, bool AllowFraction, int Precision);
public sealed record UomDto(Guid Id, string Code, string NameAr, string NameEn, UomDimension Dimension, bool AllowFraction, int Precision, CatalogStatus Status);
public sealed record AddUomRequest(Guid UomId, decimal FactorToBase, bool IsBase, DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveTo);
public sealed record BarcodeDto(Guid Id, Guid VariantId, Guid? UomId, string BarcodeType, string Value, bool IsPrimary);
public sealed record AddBarcodeRequest(Guid? UomId, BarcodeType BarcodeType, string Value, bool IsPrimary);
public sealed record CatalogVersionRequest(long Version);
public sealed class CatalogException(string code, string message) : Exception(message) { public string Code { get; } = code; }
public sealed record ProductVariantSnapshot(Guid VariantId, Guid TemplateId, string? Sku, string DisplayName, Guid BaseUomId, decimal ConversionFactor);
public sealed record CreateVariantRequest(string? Sku, string? DisplaySuffix, IReadOnlyDictionary<Guid, Guid>? AttributeValues = null);
public sealed record VariantDto(Guid Id, Guid TemplateId, string? Sku, string? DisplaySuffix, bool IsDefaultVariant, CatalogStatus Status, long Version);
public sealed record AddProductUomRequest(Guid UomId, decimal FactorToBase, DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveTo = null);
public sealed record ProductUomDto(Guid Id, Guid VariantId, Guid UomId, string UomCode, decimal FactorToBase, bool IsBase, DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveTo, CatalogStatus Status);
public sealed record AddAliasRequest(Guid VariantId, string Alias, string AliasType, string? LanguageCode);
public sealed record AddTranslationRequest(string LanguageCode, string Name, string? Description);
public sealed record AddReferenceRequest(Guid VariantId, string ReferenceType, Guid? PartyId, string ReferenceCode);
public sealed record ProductReferenceDto(Guid Id, Guid VariantId, string ReferenceType, Guid? PartyId, string ReferenceCode, CatalogStatus Status);
public sealed record ProductReadinessDto(Guid ProductId, bool Ready, IReadOnlyList<string> Missing, IReadOnlyList<string> Warnings);

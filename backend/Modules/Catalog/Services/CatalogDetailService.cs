using Modules.Catalog.DTOs;
using Shared.Primitives.Identifiers;

namespace Modules.Catalog.Services;

public sealed class CatalogDetailService(Repositories.CatalogDetailRepository repository)
{
    public Task<CatalogAttributeDto> CreateAttributeAsync(TenantId tenantId, CreateAttributeRequest request, CancellationToken ct) => repository.CreateAttributeAsync(tenantId, request, ct);
    public Task<CatalogAttributeValueDto> CreateAttributeValueAsync(TenantId tenantId, Guid attributeId, CreateAttributeValueRequest request, CancellationToken ct) => repository.CreateAttributeValueAsync(tenantId, attributeId, request, ct);
    public Task<TemplateAttributeDto> AssignTemplateAttributeAsync(TenantId tenantId, Guid templateId, Guid attributeId, bool isRequired, int displayOrder, CancellationToken ct, Guid? legalEntityId = null) => repository.AssignTemplateAttributeAsync(tenantId, templateId, attributeId, isRequired, displayOrder, ct, legalEntityId);
    public Task<IReadOnlyList<TemplateAttributeDto>> ListTemplateAttributesAsync(TenantId tenantId, Guid templateId, CancellationToken ct, Guid? legalEntityId = null) => repository.ListTemplateAttributesAsync(tenantId, templateId, ct, legalEntityId);
    public Task<VariantDto> CreateVariantAsync(TenantId tenantId, Guid templateId, CreateVariantRequest request, CancellationToken ct, Guid? legalEntityId = null) => repository.CreateVariantAsync(tenantId, templateId, request, ct, legalEntityId);
    public Task<ProductUomDto> AddUomAsync(TenantId tenantId, Guid templateId, Guid variantId, AddProductUomRequest request, CancellationToken ct, Guid? legalEntityId = null) => repository.AddUomAsync(tenantId, templateId, variantId, request, ct, legalEntityId);
    public Task<IReadOnlyList<ProductUomDto>> ListUomsAsync(TenantId tenantId, Guid variantId, CancellationToken ct, Guid? legalEntityId = null) => repository.ListUomsAsync(tenantId, variantId, ct, legalEntityId);
    public Task<ProductVariantSnapshot> ResolveAsync(TenantId tenantId, string value, CancellationToken ct, Guid? legalEntityId = null) => repository.ResolveAsync(tenantId, value, ct, legalEntityId);
    public Task<ProductReadinessDto> ReadinessAsync(TenantId tenantId, Guid templateId, CancellationToken ct, Guid? legalEntityId = null) => repository.ReadinessAsync(tenantId, templateId, ct, legalEntityId);
    public Task<bool> AddAliasAsync(TenantId tenantId, Guid templateId, AddAliasRequest request, CancellationToken ct, Guid? legalEntityId = null) => repository.AddAliasAsync(tenantId, templateId, request, ct, legalEntityId);
    public Task<bool> AddTranslationAsync(TenantId tenantId, Guid templateId, AddTranslationRequest request, CancellationToken ct, Guid? legalEntityId = null) => repository.AddTranslationAsync(tenantId, templateId, request, ct, legalEntityId);
    public Task<ProductReferenceDto> AddReferenceAsync(TenantId tenantId, Guid templateId, AddReferenceRequest request, CancellationToken ct, Guid? legalEntityId = null) => repository.AddReferenceAsync(tenantId, templateId, request, ct, legalEntityId);
    public Task<PagedProductReferences> ListReferencesAsync(TenantId tenantId, Guid templateId, int page, int pageSize, CancellationToken ct, Guid? legalEntityId = null) => repository.ListReferencesAsync(tenantId, templateId, page, pageSize, ct, legalEntityId);
}

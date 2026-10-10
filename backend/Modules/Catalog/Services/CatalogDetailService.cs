using Modules.Catalog.DTOs;
using Shared.Primitives.Identifiers;

namespace Modules.Catalog.Services;

public sealed class CatalogDetailService(Repositories.CatalogDetailRepository repository)
{
    public Task<VariantDto> CreateVariantAsync(TenantId tenantId, Guid templateId, CreateVariantRequest request, CancellationToken ct) => repository.CreateVariantAsync(tenantId, templateId, request, ct);
    public Task<ProductUomDto> AddUomAsync(TenantId tenantId, Guid templateId, Guid variantId, AddProductUomRequest request, CancellationToken ct) => repository.AddUomAsync(tenantId, templateId, variantId, request, ct);
    public Task<IReadOnlyList<ProductUomDto>> ListUomsAsync(TenantId tenantId, Guid variantId, CancellationToken ct) => repository.ListUomsAsync(tenantId, variantId, ct);
    public Task<ProductVariantSnapshot> ResolveAsync(TenantId tenantId, string value, CancellationToken ct) => repository.ResolveAsync(tenantId, value, ct);
    public Task<ProductReadinessDto> ReadinessAsync(TenantId tenantId, Guid templateId, CancellationToken ct) => repository.ReadinessAsync(tenantId, templateId, ct);
    public Task<bool> AddAliasAsync(TenantId tenantId, Guid templateId, AddAliasRequest request, CancellationToken ct) => repository.AddAliasAsync(tenantId, templateId, request, ct);
    public Task<bool> AddTranslationAsync(TenantId tenantId, Guid templateId, AddTranslationRequest request, CancellationToken ct) => repository.AddTranslationAsync(tenantId, templateId, request, ct);
    public Task<ProductReferenceDto> AddReferenceAsync(TenantId tenantId, Guid templateId, AddReferenceRequest request, CancellationToken ct) => repository.AddReferenceAsync(tenantId, templateId, request, ct);
}

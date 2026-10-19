using Modules.Catalog.DTOs;
using Modules.Catalog.Entities;
using Shared.Primitives.Identifiers;

namespace Modules.Catalog.Services;

public sealed class ProductService(Repositories.ProductRepository repository)
{
    public Task<ProductDetails> CreateAsync(TenantId tenantId, Guid actorId, Guid membershipId, CreateProductRequest request, Guid correlationId, string? traceParent, CancellationToken ct) => repository.CreateAsync(tenantId, actorId, membershipId, request, correlationId, traceParent, ct);
    public Task<ProductDetails?> GetAsync(TenantId tenantId, Guid templateId, Guid? legalEntityId, CancellationToken ct) => repository.GetAsync(tenantId, templateId, legalEntityId, ct);
    public Task<PagedProducts> ListAsync(TenantId tenantId, string? search, ProductNature? nature, CatalogStatus? status, Guid? categoryId, Guid? brandId, Guid? legalEntityId, int page, int pageSize, CancellationToken ct) => repository.ListAsync(tenantId, search, nature, status, categoryId, brandId, legalEntityId, page, pageSize, ct);
    public Task<IReadOnlyList<ProductVisibilityDto>> SetVisibilityAsync(TenantId tenantId, Guid actorId, Guid membershipId, Guid productId, ProductVisibilityRequest request, Guid correlationId, string? traceParent, CancellationToken ct) => repository.SetVisibilityAsync(tenantId, actorId, membershipId, productId, request, correlationId, traceParent, ct);
    public Task<ProductDetails> UpdateAsync(TenantId tenantId, Guid actorId, Guid membershipId, Guid id, Guid? legalEntityId, UpdateProductRequest request, Guid correlationId, string? traceParent, CancellationToken ct) => repository.UpdateAsync(tenantId, actorId, membershipId, id, legalEntityId, request, correlationId, traceParent, ct);
    public Task<ProductDetails> ChangeStatusAsync(TenantId tenantId, Guid actorId, Guid membershipId, Guid id, Guid? legalEntityId, bool restore, long version, Guid correlationId, string? traceParent, CancellationToken ct) => repository.ChangeStatusAsync(tenantId, actorId, membershipId, id, legalEntityId, restore, version, correlationId, traceParent, ct);
    public Task<BarcodeDto> AddBarcodeAsync(TenantId tenantId, Guid actorId, Guid membershipId, Guid templateId, Guid? requestedVariantId, Guid? legalEntityId, AddBarcodeRequest request, Guid correlationId, CancellationToken ct) => repository.AddBarcodeAsync(tenantId, actorId, membershipId, templateId, requestedVariantId, legalEntityId, request, correlationId, ct);
}

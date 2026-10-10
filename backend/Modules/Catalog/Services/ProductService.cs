using Modules.Catalog.DTOs;
using Modules.Catalog.Entities;
using Shared.Primitives.Identifiers;

namespace Modules.Catalog.Services;

public sealed class ProductService(Repositories.ProductRepository repository)
{
    public Task<ProductDetails> CreateAsync(TenantId tenantId, Guid actorId, Guid membershipId, CreateProductRequest request, Guid correlationId, string? traceParent, CancellationToken ct) => repository.CreateAsync(tenantId, actorId, membershipId, request, correlationId, traceParent, ct);
    public Task<ProductDetails?> GetAsync(TenantId tenantId, Guid templateId, CancellationToken ct) => repository.GetAsync(tenantId, templateId, ct);
    public Task<PagedProducts> ListAsync(TenantId tenantId, string? search, ProductNature? nature, CatalogStatus? status, Guid? categoryId, Guid? brandId, int page, int pageSize, CancellationToken ct) => repository.ListAsync(tenantId, search, nature, status, categoryId, brandId, page, pageSize, ct);
    public Task<ProductDetails> UpdateAsync(TenantId tenantId, Guid actorId, Guid membershipId, Guid id, UpdateProductRequest request, Guid correlationId, string? traceParent, CancellationToken ct) => repository.UpdateAsync(tenantId, actorId, membershipId, id, request, correlationId, traceParent, ct);
    public Task<ProductDetails> ChangeStatusAsync(TenantId tenantId, Guid actorId, Guid membershipId, Guid id, bool restore, long version, Guid correlationId, string? traceParent, CancellationToken ct) => repository.ChangeStatusAsync(tenantId, actorId, membershipId, id, restore, version, correlationId, traceParent, ct);
    public Task<BarcodeDto> AddBarcodeAsync(TenantId tenantId, Guid actorId, Guid membershipId, Guid templateId, Guid? requestedVariantId, AddBarcodeRequest request, Guid correlationId, CancellationToken ct) => repository.AddBarcodeAsync(tenantId, actorId, membershipId, templateId, requestedVariantId, request, correlationId, ct);
}

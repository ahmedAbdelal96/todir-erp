using Modules.Catalog.DTOs;
using Shared.Primitives.Identifiers;

namespace Modules.Catalog.Services;

public sealed class CatalogMasterDataService(Repositories.CatalogMasterDataRepository repository)
{
    public Task<CategoryDto> CreateCategoryAsync(TenantId tenantId, CreateCategoryRequest request, CancellationToken ct) => repository.CreateCategoryAsync(tenantId, request, ct);
    public Task<IReadOnlyList<CategoryDto>> ListCategoriesAsync(TenantId tenantId, CancellationToken ct) => repository.ListCategoriesAsync(tenantId, ct);
    public Task<BrandDto> CreateBrandAsync(TenantId tenantId, CreateBrandRequest request, CancellationToken ct) => repository.CreateBrandAsync(tenantId, request, ct);
    public Task<IReadOnlyList<BrandDto>> ListBrandsAsync(TenantId tenantId, CancellationToken ct) => repository.ListBrandsAsync(tenantId, ct);
    public Task<UomDto> CreateUomAsync(TenantId tenantId, CreateUomRequest request, CancellationToken ct) => repository.CreateUomAsync(tenantId, request, ct);
    public Task<IReadOnlyList<UomDto>> ListUomsAsync(TenantId tenantId, CancellationToken ct) => repository.ListUomsAsync(tenantId, ct);
}

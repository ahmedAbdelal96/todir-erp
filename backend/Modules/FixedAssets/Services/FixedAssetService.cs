using Shared.Primitives.Identifiers;

namespace Modules.FixedAssets.Services;

public sealed class FixedAssetService(FixedAssetRepository repository)
{
    public Task<AssetCategoryView> CreateCategoryAsync(TenantId tenant, Guid actor, CreateAssetCategoryRequest request, CancellationToken ct) => repository.CreateCategoryAsync(tenant, actor, request, ct);
    public Task<IReadOnlyList<AssetCategoryView>> ListCategoriesAsync(TenantId tenant, int limit, CancellationToken ct) => repository.ListCategoriesAsync(tenant, limit, ct);
    public Task<AssetView> CreateAssetAsync(TenantId tenant, Guid actor, CreateAssetRequest request, CancellationToken ct) => repository.CreateAssetAsync(tenant, actor, request, ct);
    public Task<IReadOnlyList<AssetView>> ListAssetsAsync(TenantId tenant, Guid? legalEntityId, string? state, string? search, int limit, int offset, CancellationToken ct) => repository.ListAssetsAsync(tenant, legalEntityId, state, search, limit, offset, ct);
    public Task<AssetAssignmentView> AssignAsync(TenantId tenant, Guid actor, Guid assetId, AssignAssetRequest request, CancellationToken ct) => repository.AssignAsync(tenant, actor, assetId, request, ct);
    public Task<IReadOnlyList<AssetBookView>> ListBooksAsync(TenantId tenant, Guid? legalEntityId, CancellationToken ct) => repository.ListBooksAsync(tenant, legalEntityId, ct);
    public Task<AssetBookView> CreateBookAsync(TenantId tenant, Guid actor, CreateBookRequest request, CancellationToken ct) => repository.CreateBookAsync(tenant, actor, request, ct);
    public Task<Guid> AddAssetBookAsync(TenantId tenant, Guid actor, Guid assetId, CreateAssetBookRequest request, CancellationToken ct) => repository.AddAssetBookAsync(tenant, actor, assetId, request, ct);
    public Task<DepreciationRunView> PrepareDepreciationRunAsync(TenantId tenant, Guid actor, CreateDepreciationRunRequest request, CancellationToken ct) => repository.PrepareDepreciationRunAsync(tenant, actor, request, ct);
    public Task<DepreciationRunView> PostDepreciationRunAsync(TenantId tenant, Guid actor, Guid runId, CancellationToken ct) => repository.PostDepreciationRunAsync(tenant, actor, runId, ct);
    public Task<DisposalView> DisposeAsync(TenantId tenant, Guid actor, Guid assetId, DisposeAssetRequest request, CancellationToken ct) => repository.DisposeAsync(tenant, actor, assetId, request, ct);
}

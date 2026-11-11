using Shared.Primitives.Identifiers;

namespace Modules.FixedAssets.Services;

public sealed class FixedAssetService(FixedAssetRepository repository)
{
    public Task<Guid?> ResolveLegalEntityAsync(TenantId tenant, string resourceType, Guid resourceId, CancellationToken ct) => repository.ResolveLegalEntityAsync(tenant, resourceType, resourceId, ct);
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
    public Task<DepreciationRunView> ReverseDepreciationRunAsync(TenantId tenant, Guid actor, Guid runId, ReverseDepreciationRunRequest request, CancellationToken ct) => repository.ReverseDepreciationRunAsync(tenant, actor, runId, request, ct);
    public Task<IReadOnlyList<CapitalizationCandidateView>> ListCapitalizationCandidatesAsync(TenantId tenant, Guid? legalEntityId, string? status, int limit, CancellationToken ct) => repository.ListCapitalizationCandidatesAsync(tenant, legalEntityId, status, limit, ct);
    public Task<CapitalizationCandidateView> CreateCapitalizationCandidateAsync(TenantId tenant, Guid actor, CreateCapitalizationCandidateRequest request, CancellationToken ct) => repository.CreateCapitalizationCandidateAsync(tenant, actor, request, ct);
    public Task<CapitalizationCandidateView> ReviewCapitalizationCandidateAsync(TenantId tenant, Guid actor, Guid candidateId, ReviewCapitalizationCandidateRequest request, CancellationToken ct) => repository.ReviewCapitalizationCandidateAsync(tenant, actor, candidateId, request, ct);
    public Task<SourceAllocationView> AllocateCapitalizationCandidateAsync(TenantId tenant, Guid actor, Guid candidateId, AllocateCapitalizationCandidateRequest request, CancellationToken ct) => repository.AllocateCapitalizationCandidateAsync(tenant, actor, candidateId, request, ct);
    public Task<SourceAllocationCorrectionView> CorrectSourceAllocationAsync(TenantId tenant, Guid actor, Guid candidateId, CorrectSourceAllocationRequest request, CancellationToken ct) => repository.CorrectSourceAllocationAsync(tenant, actor, candidateId, request, ct);
    public Task<CipAssetView> CreateCipAssetAsync(TenantId tenant, Guid actor, CreateCipAssetRequest request, CancellationToken ct) => repository.CreateCipAssetAsync(tenant, actor, request, ct);
    public Task<CipAssetView> AddCipCostSourceAsync(TenantId tenant, Guid actor, Guid assetId, AddCipCostSourceRequest request, CancellationToken ct) => repository.AddCipCostSourceAsync(tenant, actor, assetId, request, ct);
    public Task<CipAssetView> CapitalizeCipAsync(TenantId tenant, Guid actor, Guid assetId, CapitalizeCipRequest request, CancellationToken ct) => repository.CapitalizeCipAsync(tenant, actor, assetId, request, ct);
    public Task<AssetBookCostChangeView> ChangeAssetBookCostAsync(TenantId tenant, Guid actor, Guid assetId, ChangeAssetBookCostRequest request, CancellationToken ct) => repository.ChangeAssetBookCostAsync(tenant, actor, assetId, request, ct);
    public Task<OpeningImportView> CreateOpeningImportAsync(TenantId tenant, Guid actor, CreateOpeningImportRequest request, CancellationToken ct) => repository.CreateOpeningImportAsync(tenant, actor, request, ct);
    public Task<OpeningImportView> PostOpeningImportAsync(TenantId tenant, Guid actor, Guid importId, CancellationToken ct) => repository.PostOpeningImportAsync(tenant, actor, importId, ct);
    public Task<AssetBookBalanceView> GetAssetBookBalanceAsync(TenantId tenant, Guid assetId, Guid bookId, Guid legalEntityId, CancellationToken ct) => repository.GetAssetBookBalanceAsync(tenant, assetId, bookId, legalEntityId, ct);
    public Task<AssetGlReconciliationView> ReconcileAssetBookAsync(TenantId tenant, Guid bookId, Guid legalEntityId, DateOnly asOfDate, CancellationToken ct) => repository.ReconcileAssetBookAsync(tenant, bookId, legalEntityId, asOfDate, ct);
    public Task<DisposalView> DisposeAsync(TenantId tenant, Guid actor, Guid assetId, DisposeAssetRequest request, CancellationToken ct) => repository.DisposeAsync(tenant, actor, assetId, request, ct);
}

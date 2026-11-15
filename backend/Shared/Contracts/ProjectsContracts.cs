using Shared.Primitives.Identifiers;

namespace Shared.Contracts.Projects;

public sealed record ProjectCapitalizationRequest(TenantId TenantId, Guid LegalEntityId, Guid ProjectId, Guid CostFactId, decimal Amount, string CurrencyCode, string IdempotencyKey, Guid ActorId);
public sealed record ProjectCapitalizationResult(string State, string? ExternalReference, string? FailureCode);

public interface IProjectCapitalizationCapability
{
    Task<ProjectCapitalizationResult> RequestAsync(ProjectCapitalizationRequest request, CancellationToken cancellationToken = default);
}

public sealed record ProjectCostFactContract(Guid ProjectId, string SourceModule, string SourceType, string SourceId, string CostCategory, decimal BaseAmount, string BaseCurrency, DateOnly FactDate, Guid FactId);
public sealed record ProjectCostFactIngress(TenantId TenantId, Guid ActorId, Guid ProjectId, Guid? TaskId, string SourceModule, string SourceType, string SourceId, string? SourceLineRef, string CostCategory, string OriginalCurrency, decimal OriginalAmount, string BaseCurrency, decimal BaseAmount, decimal? ExchangeRate, DateOnly FactDate, string IdempotencyKey, Guid? CorrectionOfId = null, Guid? LegalEntityId = null);
public interface IProjectCostFactReader
{
    Task<IReadOnlyList<ProjectCostFactContract>> ListAsync(TenantId tenantId, Guid projectId, DateOnly? from = null, DateOnly? to = null, CancellationToken cancellationToken = default);
}
public interface IProjectCostFactWriter
{
    Task<ProjectCostFactContract> RecordAsync(ProjectCostFactIngress request, Guid correlationId, CancellationToken cancellationToken = default);
}

public sealed record ProjectCommitmentIngress(TenantId TenantId, Guid ProjectId, string SourceModule, string SourceId,
    DateOnly CommitmentDate, string CurrencyCode, decimal CommittedAmount, decimal ActualizedAmount, string IdempotencyKey, Guid? LegalEntityId = null);

/// <summary>Typed source integration seam. Source modules remain owners of their facts; Projects owns only its projections.</summary>
public interface IProjectSourceIntegrationCapabilities
{
    Task<ProjectCostFactContract> RecordCostAsync(ProjectCostFactIngress request, Guid correlationId, CancellationToken cancellationToken = default);
    Task<ProjectCostFactContract?> RecordReversalAsync(TenantId tenant, Guid actor, Guid projectId, string sourceModule, string sourceId, decimal originalCurrencyAmount, string currencyCode, DateOnly factDate, string idempotencyKey, Guid correlationId, CancellationToken cancellationToken = default);
    Task RecordCommitmentAsync(ProjectCommitmentIngress request, CancellationToken cancellationToken = default);
    Task CancelCommitmentAsync(TenantId tenant, Guid projectId, string sourceModule, string sourceId, CancellationToken cancellationToken = default);
    Task ApplyCapitalHandoffResultAsync(TenantId tenant, Guid handoffId, string state, string? externalReference, CancellationToken cancellationToken = default);
}

using Modules.Tax.DTOs;
using Modules.Tax.Entities;
using Shared.Contracts.Tax;

namespace Modules.Tax.Repositories;

public interface ITaxRepository
{
    Task<TaxRepository.TaxPolicyResolution> GetPolicyAsync(Guid tenantId, Guid legalEntityId, CancellationToken cancellationToken);
    Task<TaxCapabilityDto?> GetCapabilityAsync(Guid tenantId, Guid legalEntityId, DateOnly at, CancellationToken cancellationToken);
    Task<TaxRepository.TaxProfileResolution> ResolveProfilesAsync(Guid tenantId, TaxCalculationContext context, DateOnly taxDate, CancellationToken cancellationToken);
    Task<IReadOnlyDictionary<int, TaxRepository.TaxProfileResolution>> ResolveProfilesBatchAsync(Guid tenantId, IReadOnlyList<TaxBatchContext> contexts, CancellationToken cancellationToken);
    Task<IReadOnlyDictionary<int, (TaxRuleCandidate Rule, string PackCode, string PackVersion)>> FindRulesBatchAsync(Guid tenantId, IReadOnlyList<TaxBatchRuleContext> contexts, CancellationToken cancellationToken);
    Task<TaxCapabilityDto> SetCapabilityAsync(Guid tenantId, Guid legalEntityId, Guid actorId, string capability, DateOnly effectiveFrom, Guid correlationId, CancellationToken cancellationToken);
    Task<(TaxRuleCandidate Rule, string PackCode, string PackVersion)?> FindRuleAsync(Guid tenantId, TaxCalculationContext context, DateOnly taxDate, CancellationToken cancellationToken);
    Task<TaxSnapshotContract?> FindSnapshotAsync(Guid tenantId, string sourceDocumentKind, Guid sourceDocumentId, Guid? sourceLineId, CancellationToken cancellationToken);
    Task<TaxSnapshotContract> SaveSnapshotAsync(Guid tenantId, Guid legalEntityId, TaxSnapshotContract snapshot, CancellationToken cancellationToken);
}

public sealed record TaxBatchContext(int Index, TaxCalculationContext Context, DateOnly TaxDate);
public sealed record TaxBatchRuleContext(int Index, TaxCalculationContext Context, DateOnly TaxDate);

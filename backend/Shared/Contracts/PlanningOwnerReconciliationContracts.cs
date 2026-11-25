using Shared.Primitives.Identifiers;

namespace Shared.Contracts.Planning;

public sealed record PlanningOwnerFinancialFact(
    string SourceModule,
    string SourceDocumentId,
    string SourceLineId,
    string SourceVersion,
    Guid LegalEntityId,
    DateOnly PeriodStart,
    string MetricCode,
    string DimensionKeyJson,
    string SourceKind,
    decimal Amount,
    string CurrencyCode);

public interface IPlanningOwnerReconciliationSource
{
    string SourceModule { get; }
    Task<IReadOnlyList<PlanningOwnerFinancialFact>> ListFinancialFactsAsync(TenantId tenantId, Guid? legalEntityId, CancellationToken cancellationToken = default);
}

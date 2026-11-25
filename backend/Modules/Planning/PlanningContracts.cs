using System.Text.Json;

namespace Modules.Planning;

public sealed record CreatePlanningCycleRequest(Guid LegalEntityId, string Name, string CycleType, string Horizon, DateOnly StartPeriod, DateOnly EndPeriod, DateOnly? ActualCutoff, Guid? OwnerUserId, DateTimeOffset? DueAt);
public sealed record CreatePlanningModelRequest(string Name, string ModelType, Guid? LegalEntityId, string CurrencyCode);
public sealed record CreateModelVersionRequest(Guid ModelId, JsonElement Definition);
public sealed record CreatePlanVersionRequest(Guid CycleId, Guid ModelVersionId, string Name, string VersionKind, Guid? ScenarioId, DateOnly? ActualCutoff);
public sealed record CreateScenarioRequest(string Name, string ScenarioType, Guid? BaseVersionId, string Visibility);
public sealed record GridCellRequest(Guid LegalEntityId, DateOnly PeriodStart, string MetricCode, JsonElement DimensionKey, decimal? Amount, string CurrencyCode, bool ExplicitZero, string? SourceLabel);
public sealed record GridWriteRequest(Guid PlanVersionId, IReadOnlyList<GridCellRequest> Cells, long? ExpectedVersion);
public sealed record GridQueryRequest(Guid PlanVersionId, Guid? LegalEntityId, DateOnly? From, DateOnly? To, string? MetricCode, int? Page, int? PageSize);
public sealed record CreateAssumptionRequest(Guid PlanVersionId, string Code, string Name, string Unit, string? Description);
public sealed record AssumptionValueRequest(DateOnly PeriodStart, decimal Value, string? Source);
public sealed record RecalculateRequest(Guid PlanVersionId);
public sealed record CreateBaselineRequest(Guid PlanVersionId, string Name, decimal OriginalTotal, string CurrencyCode);
public sealed record RevisionRequest(decimal DeltaAmount, string Reason);
public sealed record TransferRequest(Guid FromCoordinateId, Guid ToCoordinateId, decimal Amount, string Reason, Guid? FromBucketId = null, Guid? ToBucketId = null);
public sealed record BudgetBucketRequest(Guid PlanVersionId, Guid PolicyId, Guid LegalEntityId, DateOnly PeriodStart, string MetricCode, JsonElement DimensionKey, decimal OriginalBudget, string CurrencyCode);
public sealed record BudgetCheckRequest(Guid BucketId, decimal ProposedAmount, string SourceModule, string SourceDocumentId, string SourceLineId, string SourceVersion, bool Reserve);
public sealed record BudgetReservationRequest(decimal Amount, string SourceModule, string SourceDocumentId, string SourceLineId, string SourceVersion);
public sealed record WorkforcePlanRequest(Guid PlanVersionId, Guid LegalEntityId, Guid? DepartmentRef, string? RoleCode, DateOnly PeriodStart, decimal Fte, decimal AverageCost, string CurrencyCode, string? Source);
public sealed record CapexPlanRequest(Guid PlanVersionId, Guid LegalEntityId, string Name, string Category, Guid? DepartmentRef, decimal PlannedAmount, string CurrencyCode, DateOnly PurchasePeriod, DateOnly? InServicePeriod, int Priority, string? Justification);
public sealed record RevenueDriverRequest(Guid PlanVersionId, Guid LegalEntityId, DateOnly PeriodStart, string DriverKey, decimal Volume, decimal Price, string CurrencyCode, string? Source);
public sealed record WorkingCapitalRequest(Guid PlanVersionId, Guid LegalEntityId, DateOnly PeriodStart, decimal Dso, decimal Dpo, decimal InventoryDays);
public sealed record PlanningFxRequest(Guid PlanVersionId, DateOnly PeriodStart, string FromCurrency, string ToCurrency, decimal Rate, string? RateType, string Source);
public sealed record ActualRefreshRequest(string MappingVersion, string SourceWatermark, IReadOnlyList<ActualFactRequest> Facts);
public sealed record ActualFactRequest(Guid PlanVersionId, Guid LegalEntityId, DateOnly PeriodStart, string MetricCode, JsonElement DimensionKey, decimal Amount, string CurrencyCode, string SourceModule, string SourceReference);
public sealed record VarianceQueryRequest(Guid PlanVersionId, Guid LegalEntityId, DateOnly? From, DateOnly? To, string? MetricCode);
public sealed record CommentRequest(Guid PlanVersionId, Guid? CoordinateId, string Body);

public sealed record PlanningCycleView(Guid Id, Guid LegalEntityId, string Name, string CycleType, string Horizon, DateOnly StartPeriod, DateOnly EndPeriod, DateOnly? ActualCutoff, string State, long RowVersion);
public sealed record PlanningModelView(Guid Id, string Name, string ModelType, Guid? LegalEntityId, string CurrencyCode, string Status);
public sealed record ModelVersionView(Guid Id, Guid ModelId, int VersionNo, string State);
public sealed record PlanVersionView(Guid Id, Guid CycleId, Guid ModelVersionId, string Name, string VersionKind, string State, DateOnly? ActualCutoff, long RowVersion);
public sealed record ScenarioView(Guid Id, string Name, string ScenarioType, string Visibility, string State);
public sealed record GridCellView(Guid CoordinateId, Guid LegalEntityId, DateOnly PeriodStart, string MetricCode, JsonElement DimensionKey, decimal? Amount, decimal? ActualAmount, decimal? CalculatedAmount, string? SourceLabel, long RowVersion);
public sealed record AssumptionView(Guid Id, Guid PlanVersionId, string Code, string Name, string Unit, string? Description);
public sealed record BudgetCheckView(string Decision, Guid BucketId, decimal CurrentBudget, decimal Actual, decimal Commitment, decimal Proposed, decimal AvailableBefore, decimal AvailableAfter, string ThresholdStatus, IReadOnlyList<string> Reasons, Guid? ReservationId);
public sealed record WorkforcePlanView(Guid Id, Guid PlanVersionId, Guid LegalEntityId, Guid? DepartmentRef, string? RoleCode, DateOnly PeriodStart, decimal Fte, decimal AverageCost, string CurrencyCode, string Source);
public sealed record CapexPlanView(Guid Id, Guid PlanVersionId, Guid LegalEntityId, string Name, string Category, decimal PlannedAmount, string CurrencyCode, DateOnly PurchasePeriod, DateOnly? InServicePeriod, int Priority, string State);
public sealed record VarianceView(Guid LegalEntityId, DateOnly PeriodStart, string MetricCode, decimal BudgetAmount, decimal ForecastAmount, decimal ActualAmount, decimal VarianceAmount, decimal Materiality);
public sealed record BudgetAvailabilityView(Guid BucketId, decimal CurrentBudget, decimal Actual, decimal Commitment, decimal Precommitment, decimal Reserved, decimal Available, string Mode, string ThresholdStatus);
public sealed record CalculationRunView(Guid Id, Guid PlanVersionId, string State, int CalculatedCells, DateTimeOffset? CompletedAt, string? ErrorDetail);
public sealed record ActualRefreshRunView(Guid Id, string State, string MappingVersion, string? SourceWatermark, DateTimeOffset StartedAt, DateTimeOffset? CompletedAt, string? ErrorDetail);

public sealed class PlanningException(string code) : Exception(code) { public string Code => Message; }

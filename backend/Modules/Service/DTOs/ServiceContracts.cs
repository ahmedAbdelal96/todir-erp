namespace Modules.Service.DTOs;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, bool HasNext, int TotalCount);
public sealed record CreateServiceRequest(Guid LegalEntityId, string Context, string Title, string? Description, Guid? CustomerPartyId, Guid? InstalledBaseId, Guid? FixedAssetId, string Priority = "Normal", DateTimeOffset? RequestedAt = null);
public sealed record ServiceRequestView(Guid Id, Guid LegalEntityId, string RequestNumber, string Context, string Title, string? Description, Guid? CustomerPartyId, Guid? InstalledBaseId, Guid? FixedAssetId, string State, string Priority, DateTimeOffset RequestedAt, long Version);
public sealed record CreateWorkOrderRequest(Guid LegalEntityId, string Context, string Title, string? Description, Guid? CustomerPartyId, Guid? InstalledBaseId, Guid? FixedAssetId, Guid? ServiceRequestId, Guid? ProjectId, string BillingOwner = "None", string WorkType = "Corrective", string Priority = "Normal", DateTimeOffset? PlannedStart = null, DateTimeOffset? PlannedEnd = null);
public sealed record WorkOrderView(Guid Id, Guid LegalEntityId, string WorkOrderNumber, string Context, string Title, string? Description, Guid? CustomerPartyId, Guid? InstalledBaseId, Guid? FixedAssetId, Guid? ProjectId, string BillingOwner, string WorkType, string Priority, string State, DateTimeOffset? PlannedStart, DateTimeOffset? PlannedEnd, DateTimeOffset? TechnicalCompletedAt, DateTimeOffset? AcceptedAt, DateTimeOffset? ClosedAt, long Version);
public sealed record TransitionRequest(long Version, string? Reason = null);
public sealed record CreateVisitRequest(DateTimeOffset PlannedStart, DateTimeOffset PlannedEnd, string LocationType = "OnSite", string? SiteInstructions = null);
public sealed record VisitView(Guid Id, Guid WorkOrderId, DateTimeOffset PlannedStart, DateTimeOffset PlannedEnd, string LocationType, string State, string? SiteInstructions, DateTimeOffset? ActualStart, DateTimeOffset? ActualEnd, long Version);
public sealed record CreateAssignmentRequest(Guid? EmployeeId, Guid? UserId, string ResourceType = "Technician", string? DisplayName = null);
public sealed record AssignmentView(Guid Id, Guid VisitId, Guid? EmployeeId, Guid? UserId, string ResourceType, string? DisplayName, string State);
public sealed record CreateResourceRequirementRequest(string? SkillCode, decimal Quantity = 1m, DateTimeOffset? RequiredFrom = null, DateTimeOffset? RequiredTo = null);
public sealed record ResourceRequirementView(Guid Id, Guid WorkOrderId, string? SkillCode, decimal Quantity, DateTimeOffset? RequiredFrom, DateTimeOffset? RequiredTo, string State);
public sealed record CreateCrewRequest(Guid LegalEntityId, string Code, string Name);
public sealed record CrewView(Guid Id, Guid LegalEntityId, string Code, string Name, bool Active);
public sealed record CreateSkillRequest(string Code, string Name);
public sealed record SkillView(Guid Id, string Code, string Name, bool Active);
public sealed record CreateTerritoryRequest(string Code, string Name);
public sealed record TerritoryView(Guid Id, string Code, string Name, bool Active);
public sealed record CreateExternalReferenceRequest(Guid EntityId, string EntityType, string SourceModule, string SourceType, string SourceId, string? SnapshotJson = null);
public sealed record ExternalReferenceView(Guid Id, Guid EntityId, string EntityType, string SourceModule, string SourceType, string SourceId, string? SnapshotJson);
public sealed record CreateInstalledBaseRequest(Guid LegalEntityId, string Name, string? SerialNumber, string? ModelNumber, Guid? ProductId, Guid? CustomerPartyId, Guid? OwnerPartyId, Guid? ParentId, string? SiteName, DateOnly? InstalledDate = null);
public sealed record InstalledBaseView(Guid Id, Guid LegalEntityId, string Name, string? SerialNumber, string? ModelNumber, Guid? ProductId, Guid? CustomerPartyId, Guid? OwnerPartyId, Guid? ParentId, string State, string? SiteName, DateOnly? InstalledDate, long Version);
public sealed record CreateTemplateRequest(Guid LegalEntityId, string Code, string Name, int Version, decimal? ExpectedDurationHours, string DefinitionJson = "{}");
public sealed record TemplateView(Guid Id, Guid LegalEntityId, string Code, string Name, int Version, decimal? ExpectedDurationHours, string DefinitionJson, string State);
public sealed record ChecklistAnswerRequest(Guid WorkOrderId, string QuestionKey, string QuestionType, string? AnswerJson, bool Required = false);
public sealed record ChecklistAnswerView(Guid Id, Guid WorkOrderId, string QuestionKey, string QuestionType, string? AnswerJson, bool Required, DateTimeOffset? AnsweredAt);
public sealed record DiagnosisRequest(Guid WorkOrderId, string? ReportedSymptom, string? Diagnosis, string? FailureModeCode, string? RootCauseCode, string? ResolutionCode, string? Notes);
public sealed record DiagnosisView(Guid Id, Guid WorkOrderId, string? ReportedSymptom, string? Diagnosis, string? FailureModeCode, string? RootCauseCode, string? ResolutionCode, string? Notes, Guid RecordedBy, DateTimeOffset RecordedAt);
public sealed record ComponentEventRequest(Guid ItemId, Guid ComponentItemId, string EventType, DateTimeOffset OccurredAt, Guid? ReplacedComponentId, Guid? WorkOrderId, string? Notes);
public sealed record ComponentEventView(Guid Id, Guid ItemId, Guid ComponentItemId, string EventType, DateTimeOffset OccurredAt, Guid? ReplacedComponentId, Guid? WorkOrderId, string? Notes);
public sealed record CreateMaintenancePlanRequest(Guid LegalEntityId, string Name, string SubjectType, Guid SubjectId, string TriggerType, DateOnly? StartDate, int? IntervalDays, decimal? MeterThreshold, string? MeterCode);
public sealed record MaintenancePlanView(Guid Id, Guid LegalEntityId, string Name, string SubjectType, Guid SubjectId, string TriggerType, DateOnly? StartDate, int? IntervalDays, decimal? MeterThreshold, string? MeterCode, string State);
public sealed record MaintenanceDueView(Guid Id, Guid PlanId, Guid SubjectId, DateOnly DueDate, string State, Guid? WorkOrderId, string? Reason, long Version = 1);
public sealed record GenerateDueRequest(DateOnly FromDate, DateOnly ToDate, int Limit = 500);
public sealed record MaintenanceDueUpdateRequest(string State, string? Reason, long ExpectedVersion = 1);
public sealed record DueGenerationView(int InspectedPlans, int CreatedDueItems);
public sealed record ConvertDueRequest(Guid LegalEntityId, string Context, string Title, string? Description, Guid? CustomerPartyId, Guid? ProjectId, string BillingOwner = "None", string WorkType = "Preventive", string Priority = "Normal");
public sealed record MaintenanceConversionView(MaintenanceDueView Due, WorkOrderView WorkOrder);
public sealed record CreateSlaProfileRequest(Guid LegalEntityId, string Code, int ResponseMinutes, int? ArrivalMinutes, int ResolutionMinutes, short WorkingWeekdays = 127, string WorkingStart = "08:00", string WorkingEnd = "17:00", string TimeZoneId = "Africa/Cairo");
public sealed record SlaProfileView(Guid Id, Guid LegalEntityId, string Code, int ResponseMinutes, int? ArrivalMinutes, int ResolutionMinutes, string State, short WorkingWeekdays = 127, string WorkingStart = "08:00", string WorkingEnd = "17:00", string TimeZoneId = "Africa/Cairo");
public sealed record StartSlaClockRequest(Guid WorkOrderId, Guid ProfileId, string ClockType, DateTimeOffset StartedAt);
public sealed record SlaClockView(Guid Id, Guid WorkOrderId, string ClockType, DateTimeOffset StartedAt, DateTimeOffset DueAt, string State, int PausedMinutes);
public sealed record SlaClockActionRequest(string? Reason = null, DateTimeOffset? OccurredAt = null);
public sealed record SlaMonitorView(int BreachedClocks);
public sealed record RecordMeterReadingRequest(Guid LegalEntityId, Guid SubjectId, string MeterCode, decimal Value, string Uom, DateTimeOffset ReadingAt, string Source = "Manual", bool IsReset = false);
public sealed record MeterReadingView(Guid Id, Guid SubjectId, string MeterCode, decimal Value, string Uom, DateTimeOffset ReadingAt, string Source, bool IsReset);
public sealed record CreateContractRequest(Guid LegalEntityId, Guid ContractHolderPartyId, Guid BillToPartyId, DateOnly StartDate, DateOnly EndDate, string ContractType = "Annual", string CurrencyCode = "EGP", decimal? Value = null, string? ScopeJson = null);
public sealed record ServiceContractView(Guid Id, Guid LegalEntityId, Guid ContractHolderPartyId, Guid BillToPartyId, DateOnly StartDate, DateOnly EndDate, string ContractType, string CurrencyCode, decimal? Value, string State, string? ScopeJson);
public sealed record CreateWarrantyRequest(Guid LegalEntityId, string WarrantyType, Guid? ProviderPartyId, Guid? CustomerPartyId, Guid SubjectId, DateOnly StartDate, DateOnly EndDate, string? ScopeJson = null);
public sealed record WarrantyView(Guid Id, Guid LegalEntityId, string WarrantyType, Guid? ProviderPartyId, Guid? CustomerPartyId, Guid? SubjectId, DateOnly StartDate, DateOnly EndDate, string State, string ScopeJson);
public sealed record EntitlementLineRequest(string ChargeCategory, decimal Amount, string CurrencyCode = "EGP", decimal Quantity = 1m, string? Description = null);
public sealed record EntitlementRequest(Guid LegalEntityId, Guid? CustomerPartyId, Guid? InstalledBaseId, Guid? WorkOrderId, DateOnly AsOfDate, IReadOnlyList<EntitlementLineRequest>? Lines = null);
public sealed record CoverageResult(string ChargeCategory, string CoverageSource, bool Covered, decimal CoveredAmount, decimal CustomerAmount, string Explanation);
public sealed record EntitlementView(IReadOnlyList<CoverageResult> Lines, string SlaState, Guid? ContractId, Guid? WarrantyId);
public sealed record CreateChargeRequest(string Category, string Description, decimal CostAmount, decimal CustomerAmount, string CurrencyCode = "EGP", string? SourceType = null, Guid? SourceId = null);
public sealed record ServiceChargeView(Guid Id, Guid WorkOrderId, string Category, string Description, decimal CostAmount, decimal CoveredAmount, decimal CustomerAmount, string CurrencyCode, string State, Guid? InvoiceId);
public sealed record InvoiceAllocationRequest(Guid InvoiceId, decimal AllocatedAmount);
public sealed record PartActionRequest(decimal Quantity, string? InventoryReference = null, string? IdempotencyKey = null,
    Guid? LegalEntityId = null, Guid? WarehouseId = null, Guid? LocationId = null, Guid? UnitOfMeasureId = null,
    Guid? InventoryReservationId = null, Guid? LotId = null, Guid? SerialId = null);
public sealed record CreatePartRequirementRequest(Guid ProductId, decimal Quantity, string UomCode, string? Description = null,
    Guid? LegalEntityId = null, Guid? WarehouseId = null, Guid? LocationId = null, Guid? UnitOfMeasureId = null,
    Guid? LotId = null, Guid? SerialId = null);
public sealed record PartRequirementView(Guid Id, Guid WorkOrderId, Guid ProductId, decimal Quantity, string UomCode, string State, decimal ReservedQuantity, decimal IssuedQuantity, decimal ConsumedQuantity, decimal ReturnedQuantity);
public sealed record RecordLaborRequest(Guid VisitId, Guid? EmployeeId, DateTimeOffset StartedAt, DateTimeOffset EndedAt, string TimeCategory, decimal? CostRate, string CurrencyCode = "EGP", string? IdempotencyKey = null);
public sealed record LaborView(Guid Id, Guid VisitId, Guid? EmployeeId, DateTimeOffset StartedAt, DateTimeOffset EndedAt, string TimeCategory, decimal DurationHours, decimal? CostAmount, string CurrencyCode, string State);
public sealed record OfflineCommandRequest(Guid ClientCommandId, string CommandType, Guid? WorkOrderId, Guid? VisitId, long? ExpectedVersion, string PayloadJson = "{}");
public sealed record OfflineCommandView(Guid Id, Guid ClientCommandId, string CommandType, string State, string? ConflictCode, string? ResultJson);
public sealed record ContractUsageRequest(Guid WorkOrderId, string UsageType, decimal Quantity, string IdempotencyKey);
public sealed record MobileDayView(IReadOnlyList<VisitView> Visits, IReadOnlyList<WorkOrderView> WorkOrders, DateOnly Date);
public sealed record ServiceReportView(Guid Id, Guid WorkOrderId, int Version, string State, string ContentJson, DateTimeOffset CreatedAt);
public sealed record CreateReportRequest(string ContentJson);
public sealed record ServiceKpiView(int OpenRequests, int OpenWorkOrders, int VisitsToday, decimal OpenCharges, decimal DowntimeHours);
public sealed record DowntimeRequest(Guid SubjectId, Guid? WorkOrderId, string DowntimeType, DateTimeOffset StartedAt, string? Reason);
public sealed record DowntimeView(Guid Id, Guid SubjectId, Guid? WorkOrderId, string DowntimeType, DateTimeOffset StartedAt, DateTimeOffset? EndedAt, string? Reason);
public sealed record CreateMaintenanceRoundRequest(Guid LegalEntityId, string Code, string Name, DateOnly ScheduledDate);
public sealed record MaintenanceRoundView(Guid Id, Guid LegalEntityId, string Code, string Name, DateOnly ScheduledDate, string State, long Version, int SubjectCount);
public sealed record AddRoundSubjectRequest(string SubjectType, Guid SubjectId, int SequenceNo);
public sealed record RoundSubjectView(Guid Id, Guid RoundId, string SubjectType, Guid SubjectId, int SequenceNo, string State, string? Result, string MeasurementsJson, string? Exceptions, string? FollowUp);
public sealed record RecordRoundSubjectRequest(string State, string? Result, string MeasurementsJson = "{}", string? Exceptions = null, string? FollowUp = null, long ExpectedRoundVersion = 1);
public sealed record CreateSupplierRecoveryRequest(Guid WorkOrderId, Guid SupplierPartyId, string SourceReference, decimal Amount, string CurrencyCode = "EGP", string? Reason = null, string? IdempotencyKey = null);
public sealed record SupplierRecoveryView(Guid Id, Guid WorkOrderId, Guid SupplierPartyId, string SourceReference, decimal Amount, string CurrencyCode, string State, string? Reason);
public sealed record FixedAssetServiceHistoryView(Guid Id, Guid FixedAssetId, Guid WorkOrderId, string EventType, decimal CostAmount, string CurrencyCode, string SnapshotJson, DateTimeOffset OccurredAt);
public sealed class ServiceException(string code) : Exception(code)
{
    public string Code { get; } = code;
}

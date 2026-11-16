export type ServiceRequestContext = "InternalMaintenance" | "CustomerFieldService";
export type ServiceRequestState = "New" | "Triaged" | "InProgress" | "Resolved" | "Closed" | "Cancelled";
export type ServicePriority = "Low" | "Normal" | "High" | "Critical" | "low" | "medium" | "urgent";

export type ServiceContext = "customer" | "internal" | "InternalMaintenance" | "CustomerFieldService";
export type WorkOrderStatus = "draft" | "ready" | "scheduled" | "in_progress" | "completed" | "closed" | "canceled" | WorkOrderState;
export type ServiceRequestStatus = "new" | "triaged" | "in_progress" | "resolved" | "closed" | "canceled" | ServiceRequestState;

export type WorkOrderContext = "InternalMaintenance" | "CustomerFieldService" | "customer" | "internal";
export type WorkOrderBillingOwner = "Service" | "Projects" | "None";
export type WorkOrderType = "Corrective" | "Preventive" | "Installation" | "Inspection" | "Emergency" | "corrective" | "preventive" | "installation" | "inspection" | "emergency";
export type WorkOrderState =
  | "Draft"
  | "Ready"
  | "Scheduled"
  | "InProgress"
  | "Waiting"
  | "TechnicalCompleted"
  | "Accepted"
  | "Closed"
  | "Cancelled";

export type VisitLocationType = "OnSite" | "Remote" | "Workshop" | "Depot";
export type VisitState =
  | "Planned"
  | "Dispatched"
  | "Traveling"
  | "Arrived"
  | "InProgress"
  | "Paused"
  | "Completed"
  | "NoAccess"
  | "Cancelled";

export type AssignmentState = "Assigned" | "Accepted" | "Declined" | "Removed";
export type InstalledBaseState = "Draft" | "Active" | "Suspended" | "Retired";
export type ComponentEventType = "Installed" | "Removed" | "Replaced";

export type TemplateState = "Draft" | "Active" | "Archived";
export type QuestionType = "Text" | "Boolean" | "Number" | "Choice" | "Measurement" | "Photo";

export type MaintenancePlanSubjectType = "InstalledBase" | "FixedAsset";
export type MaintenancePlanTriggerType = "CalendarFixed" | "FromLastCompletion" | "Meter";
export type MaintenancePlanState = "Draft" | "Active" | "Suspended" | "Retired";

export type MaintenanceDueState = "Forecast" | "Due" | "Postponed" | "Skipped" | "Converted" | "Completed";
export type MaintenanceRoundState = "Draft" | "Planned" | "InProgress" | "Completed" | "Cancelled";
export type RoundSubjectState = "Pending" | "Inspected" | "Skipped" | "Failed";

export type SlaClockType = "Response" | "Arrival" | "Resolution";
export type SlaClockState = "Running" | "Paused" | "Completed" | "Breached";

export type PartRequirementState =
  | "Required"
  | "Reserved"
  | "PartiallyReserved"
  | "Issued"
  | "Consumed"
  | "Returned"
  | "Unavailable"
  | "Ordered";

export type LaborTimeCategory =
  | "Travel"
  | "OnSiteWork"
  | "RemoteWork"
  | "Waiting"
  | "Break"
  | "Administrative";

export type ServiceChargeCategory = "Labor" | "Parts" | "Travel" | "ExternalService" | "Fee";
export type ServiceChargeState = "Draft" | "Reviewed" | "Requested" | "Invoiced" | "WrittenOff" | "Cancelled";

export type ServiceContractType = "Annual" | "Comprehensive" | "PreventiveOnly" | "OnDemand";
export type ServiceContractState = "Draft" | "Active" | "Suspended" | "Expired" | "Cancelled";

export type WarrantyType = "Customer" | "Supplier";
export type WarrantyState = "Draft" | "Active" | "Expired" | "Cancelled";

export type SupplierRecoveryState = "Draft" | "Requested" | "Recovered" | "Rejected" | "Cancelled";

export type PagedResult<T> = Readonly<{
  items: readonly T[];
  page: number;
  pageSize: number;
  hasNext: boolean;
  totalCount: number;
}>;

export type ServiceRequestView = Readonly<{
  id: string;
  legalEntityId: string;
  requestNumber: string;
  context: ServiceRequestContext | string;
  title: string;
  subject?: string | undefined;
  description?: string | null | undefined;
  customerPartyId?: string | null | undefined;
  customerName?: string | null | undefined;
  siteLocation?: string | null | undefined;
  assetReference?: string | null | undefined;
  installedBaseId?: string | null | undefined;
  fixedAssetId?: string | null | undefined;
  state: ServiceRequestState | string;
  status?: string | undefined;
  priority: ServicePriority | string;
  requestedAt: string;
  createdAtUtc?: string | undefined;
  version: number;
}>;

export type ServiceRequestDto = ServiceRequestView;

export type CreateServiceRequest = Readonly<{
  legalEntityId: string;
  context: ServiceRequestContext | string;
  title?: string | undefined;
  subject?: string | undefined;
  requestNumber?: string | undefined;
  description?: string | undefined;
  customerPartyId?: string | undefined;
  customerName?: string | undefined;
  siteLocation?: string | undefined;
  assetReference?: string | undefined;
  installedBaseId?: string | undefined;
  fixedAssetId?: string | undefined;
  priority?: ServicePriority | string | undefined;
  requestedAt?: string | undefined;
}>;

export type WorkOrderView = Readonly<{
  id: string;
  legalEntityId: string;
  workOrderNumber: string;
  context: WorkOrderContext | string;
  title: string;
  subject?: string | undefined;
  description?: string | null | undefined;
  customerPartyId?: string | null | undefined;
  customerName?: string | null | undefined;
  siteLocation?: string | null | undefined;
  installedBaseId?: string | null | undefined;
  fixedAssetId?: string | null | undefined;
  projectId?: string | null | undefined;
  billingOwner: WorkOrderBillingOwner;
  workType: WorkOrderType;
  workOrderType?: string | undefined;
  priority: ServicePriority | string;
  state: WorkOrderState | string;
  status?: string | undefined;
  plannedStart?: string | null | undefined;
  plannedEnd?: string | null | undefined;
  createdAtUtc?: string | undefined;
  technicalCompletedAt?: string | null | undefined;
  acceptedAt?: string | null | undefined;
  closedAt?: string | null | undefined;
  version: number;
  templateId?: string | null | undefined;
  templateVersion?: number | null | undefined;
}>;

export type WorkOrderDto = WorkOrderView;

export type CreateWorkOrderRequest = Readonly<{
  legalEntityId: string;
  context: WorkOrderContext;
  title: string;
  description?: string | undefined;
  customerPartyId?: string | undefined;
  installedBaseId?: string | undefined;
  fixedAssetId?: string | undefined;
  serviceRequestId?: string | undefined;
  projectId?: string | undefined;
  billingOwner?: WorkOrderBillingOwner | undefined;
  workType?: WorkOrderType | undefined;
  priority?: ServicePriority | undefined;
  plannedStart?: string | undefined;
  plannedEnd?: string | undefined;
  templateId?: string | undefined;
  templateVersion?: number | undefined;
}>;

export type TransitionRequest = Readonly<{
  version: number;
  reason?: string | undefined;
}>;

export type VisitView = Readonly<{
  id: string;
  workOrderId: string;
  plannedStart: string;
  plannedEnd: string;
  locationType: VisitLocationType;
  state: VisitState;
  siteInstructions?: string | null | undefined;
  actualStart?: string | null | undefined;
  actualEnd?: string | null | undefined;
  version: number;
  visitNumber?: string | undefined;
  visitType?: string | undefined;
  scheduledStartUtc?: string | undefined;
  scheduledEndUtc?: string | undefined;
  status?: string | undefined;
  notes?: string | undefined;
}>;

export type VisitDto = VisitView;
export type VisitType = "onsite" | "depot" | "remote" | VisitLocationType;
export type ChargeType = "labor" | "parts" | "travel" | "fee" | "expense" | ServiceChargeCategory;
export type ActivityType = "travel" | "onsite" | "remote" | "diagnostics" | "repair" | LaborTimeCategory;

export type CreateVisitRequest = Readonly<{
  plannedStart: string;
  plannedEnd: string;
  locationType?: VisitLocationType | undefined;
  siteInstructions?: string | undefined;
}>;

export type AssignmentView = Readonly<{
  id: string;
  visitId: string;
  employeeId?: string | null | undefined;
  userId?: string | null | undefined;
  resourceType: string;
  displayName?: string | null | undefined;
  state: AssignmentState;
}>;

export type CreateAssignmentRequest = Readonly<{
  employeeId?: string | undefined;
  userId?: string | undefined;
  resourceType?: string | undefined;
  displayName?: string | undefined;
}>;

export type ResourceRequirementView = Readonly<{
  id: string;
  workOrderId: string;
  skillCode?: string | null | undefined;
  quantity: number;
  requiredFrom?: string | null | undefined;
  requiredTo?: string | null | undefined;
  state: string;
}>;

export type CreateResourceRequirementRequest = Readonly<{
  skillCode?: string | undefined;
  quantity?: number | undefined;
  requiredFrom?: string | undefined;
  requiredTo?: string | undefined;
}>;

export type CrewView = Readonly<{
  id: string;
  legalEntityId: string;
  code: string;
  name: string;
  active: boolean;
}>;

export type CreateCrewRequest = Readonly<{
  legalEntityId: string;
  code: string;
  name: string;
  active?: boolean | undefined;
}>;

export type SkillView = Readonly<{
  id: string;
  code: string;
  name: string;
  active: boolean;
}>;

export type CreateSkillRequest = Readonly<{
  code: string;
  name: string;
  active?: boolean | undefined;
}>;

export type TerritoryView = Readonly<{
  id: string;
  code: string;
  name: string;
  active: boolean;
}>;

export type CreateTerritoryRequest = Readonly<{
  code: string;
  name: string;
  active?: boolean | undefined;
}>;

export type ExternalReferenceView = Readonly<{
  id: string;
  entityId: string;
  entityType: string;
  sourceModule: string;
  sourceType: string;
  sourceId: string;
  snapshotJson?: string | null | undefined;
}>;

export type InstalledBaseView = Readonly<{
  id: string;
  legalEntityId: string;
  name: string;
  serialNumber?: string | null | undefined;
  modelNumber?: string | null | undefined;
  productId?: string | null | undefined;
  customerPartyId?: string | null | undefined;
  ownerPartyId?: string | null | undefined;
  parentId?: string | null | undefined;
  state: InstalledBaseState;
  siteName?: string | null | undefined;
  installedDate?: string | null | undefined;
  version: number;
  itemCode?: string | undefined;
  itemName?: string | undefined;
  customerName?: string | undefined;
  siteLocation?: string | undefined;
  status?: string | undefined;
  warrantyEndDate?: string | undefined;
}>;

export type InstalledBaseItemDto = InstalledBaseView;

export type CreateInstalledBaseRequest = Readonly<{
  legalEntityId: string;
  name: string;
  serialNumber?: string | undefined;
  modelNumber?: string | undefined;
  productId?: string | undefined;
  customerPartyId?: string | undefined;
  ownerPartyId?: string | undefined;
  parentId?: string | undefined;
  siteName?: string | undefined;
  installedDate?: string | undefined;
}>;

export type ComponentEventView = Readonly<{
  id: string;
  itemId: string;
  componentItemId: string;
  eventType: ComponentEventType;
  occurredAt: string;
  replacedComponentId?: string | null | undefined;
  workOrderId?: string | null | undefined;
  notes?: string | null | undefined;
}>;

export type TemplateView = Readonly<{
  id: string;
  legalEntityId: string;
  code: string;
  name: string;
  version: number;
  expectedDurationHours?: number | null | undefined;
  definitionJson: string;
  state: TemplateState;
}>;

export type CreateTemplateRequest = Readonly<{
  legalEntityId: string;
  code: string;
  name: string;
  version: number;
  expectedDurationHours?: number | undefined;
  definitionJson?: string | undefined;
}>;

export type ChecklistAnswerView = Readonly<{
  id: string;
  workOrderId: string;
  questionKey: string;
  questionType: QuestionType;
  answerJson?: string | null | undefined;
  required: boolean;
  answeredAt?: string | null | undefined;
  itemCode?: string | undefined;
  prompt?: string | undefined;
  passed?: boolean | undefined;
  value?: string | number | undefined;
  uom?: string | undefined;
}>;

export type ChecklistAnswerRequest = Readonly<{
  workOrderId: string;
  questionKey: string;
  questionType: QuestionType;
  answerJson?: string | undefined;
  required?: boolean | undefined;
}>;

export type DiagnosisView = Readonly<{
  id: string;
  workOrderId: string;
  reportedSymptom?: string | null | undefined;
  diagnosis?: string | null | undefined;
  failureModeCode?: string | null | undefined;
  rootCauseCode?: string | null | undefined;
  resolutionCode?: string | null | undefined;
  notes?: string | null | undefined;
  recordedBy: string;
  recordedAt: string;
  symptom?: string | undefined;
  failureCode?: string | undefined;
  rootCause?: string | undefined;
}>;

export type DiagnosisRequest = Readonly<{
  workOrderId: string;
  reportedSymptom?: string | undefined;
  diagnosis?: string | undefined;
  failureModeCode?: string | undefined;
  rootCauseCode?: string | undefined;
  resolutionCode?: string | undefined;
  notes?: string | undefined;
}>;

export type MaintenancePlanView = Readonly<{
  id: string;
  legalEntityId: string;
  name: string;
  subjectType: MaintenancePlanSubjectType;
  subjectId: string;
  triggerType: MaintenancePlanTriggerType;
  startDate?: string | null | undefined;
  intervalDays?: number | null | undefined;
  meterThreshold?: number | null | undefined;
  meterCode?: string | null | undefined;
  state: MaintenancePlanState;
  planCode?: string | undefined;
  scheduleType?: string | undefined;
  calendarIntervalDays?: number | undefined;
  meterInterval?: number | undefined;
  isActive?: boolean | undefined;
}>;

export type CreateMaintenancePlanRequest = Readonly<{
  legalEntityId: string;
  name: string;
  subjectType: MaintenancePlanSubjectType;
  subjectId: string;
  triggerType: MaintenancePlanTriggerType;
  startDate?: string | undefined;
  intervalDays?: number | undefined;
  meterThreshold?: number | undefined;
  meterCode?: string | undefined;
  planCode?: string | undefined;
}>;

export type MaintenanceDueView = Readonly<{
  id: string;
  planId: string;
  subjectId: string;
  dueDate: string;
  state: MaintenanceDueState;
  workOrderId?: string | null | undefined;
  reason?: string | null | undefined;
  version: number;
  dueDateUtc?: string | undefined;
  triggerReason?: string | undefined;
  status?: string | undefined;
}>;

export type MaintenanceRoundView = Readonly<{
  id: string;
  legalEntityId: string;
  code: string;
  name: string;
  scheduledDate: string;
  state: MaintenanceRoundState;
  version: number;
  subjectCount: number;
}>;

export type CreateMaintenanceRoundRequest = Readonly<{
  legalEntityId: string;
  code: string;
  name: string;
  scheduledDate: string;
}>;

export type RoundSubjectView = Readonly<{
  id: string;
  roundId: string;
  subjectType: string;
  subjectId: string;
  sequenceNo: number;
  state: RoundSubjectState;
  result?: string | null | undefined;
  measurementsJson: string;
  exceptions?: string | null | undefined;
  followUp?: string | null | undefined;
}>;

export type MeterReadingView = Readonly<{
  id: string;
  subjectId: string;
  meterCode: string;
  value: number;
  uom: string;
  readingAt: string;
  source: string;
  isReset: boolean;
}>;

export type RecordMeterReadingRequest = Readonly<{
  legalEntityId: string;
  subjectId: string;
  meterCode: string;
  value: number;
  uom: string;
  readingAt: string;
  source?: string | undefined;
  isReset?: boolean | undefined;
}>;

export type ServiceContractView = Readonly<{
  id: string;
  legalEntityId: string;
  contractHolderPartyId: string;
  billToPartyId: string;
  startDate: string;
  endDate: string;
  contractType: ServiceContractType;
  currencyCode: string;
  value?: number | null | undefined;
  state: ServiceContractState;
  scopeJson?: string | null | undefined;
  contractNumber?: string | undefined;
  customerName?: string | undefined;
  startDateUtc?: string | undefined;
  endDateUtc?: string | undefined;
  coverageTerms?: string | undefined;
  price?: number | undefined;
  currency?: string | undefined;
  status?: string | undefined;
}>;

export type CreateContractRequest = Readonly<{
  legalEntityId: string;
  contractHolderPartyId: string;
  billToPartyId: string;
  startDate: string;
  endDate: string;
  contractType?: ServiceContractType | undefined;
  currencyCode?: string | undefined;
  value?: number | undefined;
  scopeJson?: string | undefined;
  contractNumber?: string | undefined;
}>;

export type WarrantyView = Readonly<{
  id: string;
  legalEntityId: string;
  warrantyType: WarrantyType;
  providerPartyId?: string | null | undefined;
  customerPartyId?: string | null | undefined;
  subjectId?: string | null | undefined;
  startDate: string;
  endDate: string;
  state: WarrantyState;
  scopeJson?: string | null | undefined;
  serialNumber?: string | undefined;
  customerName?: string | undefined;
  startDateUtc?: string | undefined;
  endDateUtc?: string | undefined;
  status?: string | undefined;
}>;

export type CreateWarrantyRequest = Readonly<{
  legalEntityId: string;
  warrantyType: WarrantyType;
  providerPartyId?: string | undefined;
  customerPartyId?: string | undefined;
  subjectId: string;
  startDate: string;
  endDate: string;
  scopeJson?: string | undefined;
}>;

export type SlaProfileView = Readonly<{
  id: string;
  legalEntityId: string;
  code: string;
  responseMinutes: number;
  arrivalMinutes?: number | null | undefined;
  resolutionMinutes: number;
  state: string;
  workingWeekdays: number;
  workingStart: string;
  workingEnd: string;
  timeZoneId: string;
}>;

export type CreateSlaProfileRequest = Readonly<{
  legalEntityId: string;
  code: string;
  responseMinutes: number;
  arrivalMinutes?: number | undefined;
  resolutionMinutes: number;
  workingWeekdays?: number | undefined;
  workingStart?: string | undefined;
  workingEnd?: string | undefined;
  timeZoneId?: string | undefined;
}>;

export type SlaClockView = Readonly<{
  id: string;
  workOrderId: string;
  clockType: SlaClockType;
  startedAt: string;
  dueAt: string;
  deadlineUtc?: string | undefined;
  targetHours?: number | undefined;
  state: SlaClockState;
  isBreached?: boolean | undefined;
  isStopped?: boolean | undefined;
  pausedMinutes: number;
}>;

export type PartRequirementView = Readonly<{
  id: string;
  workOrderId: string;
  productId: string;
  quantity: number;
  uomCode: string;
  state: PartRequirementState;
  reservedQuantity: number;
  issuedQuantity: number;
  consumedQuantity: number;
  returnedQuantity: number;
  productName?: string | undefined;
  partNumber?: string | undefined;
  status?: string | undefined;
  itemId?: string | undefined;
  description?: string | undefined;
  requiredQuantity?: number | undefined;
  uom?: string | undefined;
}>;

export type WorkOrderPartView = PartRequirementView;
export type WorkOrderPartDto = PartRequirementView;

export type CreatePartRequirementRequest = Readonly<{
  productId: string;
  quantity: number;
  uomCode: string;
  description?: string | undefined;
  legalEntityId?: string | undefined;
  warehouseId?: string | undefined;
  locationId?: string | undefined;
  unitOfMeasureId?: string | undefined;
  lotId?: string | undefined;
  serialId?: string | undefined;
}>;

export type PartActionRequest = Readonly<{
  quantity: number;
  inventoryReference?: string | undefined;
  idempotencyKey?: string | undefined;
  legalEntityId?: string | undefined;
  warehouseId?: string | undefined;
  locationId?: string | undefined;
  unitOfMeasureId?: string | undefined;
  inventoryReservationId?: string | undefined;
  lotId?: string | undefined;
  serialId?: string | undefined;
  inventoryOperationId?: string | undefined;
}>;

export type LaborView = Readonly<{
  id: string;
  visitId: string;
  employeeId?: string | null | undefined;
  startedAt: string;
  endedAt: string;
  timeCategory: LaborTimeCategory;
  durationHours: number;
  costAmount?: number | null | undefined;
  currencyCode: string;
  state: string;
  technicianId?: string | undefined;
  activityType?: string | undefined;
  hours?: number | undefined;
  hourlyRate?: number | undefined;
  currency?: string | undefined;
  isBillable?: boolean | undefined;
}>;

export type RecordLaborRequest = Readonly<{
  visitId: string;
  employeeId?: string | undefined;
  startedAt: string;
  endedAt: string;
  timeCategory: LaborTimeCategory;
  costRate?: number | undefined;
  currencyCode?: string | undefined;
  idempotencyKey?: string | undefined;
}>;

export type ServiceReportView = Readonly<{
  id: string;
  workOrderId: string;
  version: number;
  state: "Draft" | "Accepted";
  contentJson: string;
  createdAt: string;
  signedFileId?: string | null | undefined;
  createdAtUtc?: string | undefined;
  summary?: string | undefined;
  customerRepresentative?: string | undefined;
  signatureCaptured?: boolean | undefined;
}>;

export type CreateReportRequest = Readonly<{
  contentJson: string;
  signedFileId?: string | undefined;
}>;

export type ServiceChargeView = Readonly<{
  id: string;
  workOrderId: string;
  category: ServiceChargeCategory;
  description: string;
  costAmount: number;
  coveredAmount: number;
  customerAmount: number;
  currencyCode: string;
  state: ServiceChargeState;
  invoiceId?: string | null | undefined;
  correctionOfId?: string | null | undefined;
}>;

export type CreateChargeRequest = Readonly<{
  category: ServiceChargeCategory;
  description: string;
  costAmount: number;
  customerAmount: number;
  currencyCode?: string | undefined;
  sourceType?: string | undefined;
  sourceId?: string | undefined;
  correctionOfId?: string | undefined;
}>;

export type DowntimeView = Readonly<{
  id: string;
  subjectId: string;
  workOrderId?: string | null | undefined;
  downtimeType: "Planned" | "Unplanned";
  startedAt: string;
  endedAt?: string | null | undefined;
  reason?: string | null | undefined;
}>;

export type DowntimeRequest = Readonly<{
  subjectId: string;
  workOrderId?: string | undefined;
  downtimeType: "Planned" | "Unplanned";
  startedAt: string;
  reason?: string | undefined;
}>;

export type SupplierRecoveryView = Readonly<{
  id: string;
  workOrderId: string;
  supplierPartyId: string;
  sourceReference: string;
  amount: number;
  currencyCode: string;
  state: SupplierRecoveryState;
  reason?: string | null | undefined;
}>;

export type CreateSupplierRecoveryRequest = Readonly<{
  workOrderId: string;
  supplierPartyId: string;
  sourceReference: string;
  amount: number;
  currencyCode?: string | undefined;
  reason?: string | undefined;
  idempotencyKey?: string | undefined;
}>;

export type ServiceKpiView = Readonly<{
  openRequests: number;
  openWorkOrders: number;
  visitsToday: number;
  openCharges: number;
  downtimeHours: number;
  failureCount: number;
  mtbfHours: number;
  mttrHours: number;
  availabilityPercent: number;
}>;

export type MobileDayView = Readonly<{
  visits: readonly VisitView[];
  workOrders: readonly WorkOrderView[];
  date: string;
}>;

export type OfflineCommandRequest = Readonly<{
  clientCommandId: string;
  commandType: string;
  workOrderId?: string | undefined;
  visitId?: string | undefined;
  expectedVersion?: number | undefined;
  payloadJson?: string | undefined;
}>;

export type OfflineCommandView = Readonly<{
  id: string;
  clientCommandId: string;
  commandType: string;
  state: "Accepted" | "Processed" | "Rejected" | "Retryable" | "Conflict";
  conflictCode?: string | null | undefined;
  resultJson?: string | null | undefined;
}>;

export type CoverageResult = Readonly<{
  chargeCategory: string;
  coverageSource: string;
  covered: boolean;
  coveredAmount: number;
  customerAmount: number;
  explanation: string;
}>;

export type EntitlementView = Readonly<{
  lines: readonly CoverageResult[];
  slaState: string;
  contractId?: string | null | undefined;
  warrantyId?: string | null | undefined;
}>;

export type EntitlementLineRequest = Readonly<{
  chargeCategory: string;
  amount: number;
  currencyCode?: string | undefined;
  quantity?: number | undefined;
  description?: string | undefined;
}>;

export type EntitlementRequest = Readonly<{
  legalEntityId: string;
  customerPartyId?: string | undefined;
  installedBaseId?: string | undefined;
  workOrderId?: string | undefined;
  asOfDate: string;
  lines?: readonly EntitlementLineRequest[] | undefined;
}>;

export type FixedAssetServiceHistoryView = Readonly<{
  id: string;
  fixedAssetId: string;
  workOrderId: string;
  eventType: string;
  costAmount: number;
  currencyCode: string;
  snapshotJson: string;
  occurredAt: string;
}>;

import { httpClient } from "../../../core/api/http-client.ts";
import type {
  ServiceRequestView,
  CreateServiceRequest,
  WorkOrderView,
  CreateWorkOrderRequest,
  TransitionRequest,
  VisitView,
  CreateVisitRequest,
  AssignmentView,
  CreateAssignmentRequest,
  ResourceRequirementView,
  CreateResourceRequirementRequest,
  CrewView,
  CreateCrewRequest,
  SkillView,
  CreateSkillRequest,
  TerritoryView,
  CreateTerritoryRequest,
  ExternalReferenceView,
  InstalledBaseView,
  CreateInstalledBaseRequest,
  ComponentEventView,
  TemplateView,
  CreateTemplateRequest,
  ChecklistAnswerView,
  ChecklistAnswerRequest,
  DiagnosisView,
  DiagnosisRequest,
  MaintenancePlanView,
  CreateMaintenancePlanRequest,
  MaintenanceDueView,
  MaintenanceRoundView,
  CreateMaintenanceRoundRequest,
  RoundSubjectView,
  MeterReadingView,
  RecordMeterReadingRequest,
  ServiceContractView,
  CreateContractRequest,
  WarrantyView,
  CreateWarrantyRequest,
  SlaProfileView,
  CreateSlaProfileRequest,
  SlaClockView,
  PartRequirementView,
  CreatePartRequirementRequest,
  PartActionRequest,
  LaborView,
  RecordLaborRequest,
  ServiceReportView,
  CreateReportRequest,
  ServiceChargeView,
  CreateChargeRequest,
  DowntimeView,
  DowntimeRequest,
  SupplierRecoveryView,
  CreateSupplierRecoveryRequest,
  ServiceKpiView,
  MobileDayView,
  OfflineCommandRequest,
  OfflineCommandView,
  EntitlementView,
  EntitlementRequest,
  FixedAssetServiceHistoryView,
  PagedResult
} from "../types.ts";

const BASE = "/api/v1/service";

function toQueryString(params: Record<string, string | number | boolean | null | undefined>): string {
  const entries = Object.entries(params)
    .filter(([_, v]) => v !== undefined && v !== null && v !== "")
    .map(([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(String(v))}`);
  return entries.length > 0 ? `?${entries.join("&")}` : "";
}

export const serviceApi = {
  // KPI
  getKpi: (): Promise<ServiceKpiView> => httpClient<ServiceKpiView>(`${BASE}/reports/kpi`),

  // Service Requests
  listRequests: (params: {
    legalEntityId?: string | undefined;
    state?: string | undefined;
    search?: string | undefined;
    page?: number | undefined;
    pageSize?: number | undefined;
  }): Promise<PagedResult<ServiceRequestView>> =>
    httpClient<PagedResult<ServiceRequestView>>(`${BASE}/requests${toQueryString(params)}`),

  getRequest: (requestId: string): Promise<ServiceRequestView> =>
    httpClient<ServiceRequestView>(`${BASE}/requests/${requestId}`),

  createRequest: (request: CreateServiceRequest): Promise<ServiceRequestView> =>
    httpClient<ServiceRequestView>(`${BASE}/requests`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  transitionRequest: (
    requestId: string,
    action: "triage" | "resolve" | "close" | "cancel"
  ): Promise<ServiceRequestView> =>
    httpClient<ServiceRequestView>(`${BASE}/requests/${requestId}/${action}`, {
      method: "POST"
    }),

  // Work Orders
  listWorkOrders: (params: {
    legalEntityId?: string | undefined;
    state?: string | undefined;
    contextName?: string | undefined;
    page?: number | undefined;
    pageSize?: number | undefined;
  }): Promise<PagedResult<WorkOrderView>> =>
    httpClient<PagedResult<WorkOrderView>>(`${BASE}/work-orders${toQueryString(params)}`),

  listUnscheduledWorkOrders: (params: {
    legalEntityId?: string | undefined;
    page?: number | undefined;
    pageSize?: number | undefined;
  }): Promise<PagedResult<WorkOrderView>> =>
    httpClient<PagedResult<WorkOrderView>>(`${BASE}/dispatch/unscheduled${toQueryString(params)}`),

  getWorkOrder: (workOrderId: string): Promise<WorkOrderView> =>
    httpClient<WorkOrderView>(`${BASE}/work-orders/${workOrderId}`),

  createWorkOrder: (request: CreateWorkOrderRequest): Promise<WorkOrderView> =>
    httpClient<WorkOrderView>(`${BASE}/work-orders`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  transitionWorkOrder: (
    workOrderId: string,
    action:
      | "ready"
      | "schedule"
      | "start"
      | "waiting"
      | "technical-complete"
      | "accept"
      | "close"
      | "cancel"
      | "reopen",
    request: TransitionRequest
  ): Promise<WorkOrderView> =>
    httpClient<WorkOrderView>(`${BASE}/work-orders/${workOrderId}/${action}`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // Visits
  listWorkOrderVisits: (
    workOrderId: string,
    params?: { state?: string | undefined; page?: number | undefined; pageSize?: number | undefined }
  ): Promise<PagedResult<VisitView>> =>
    httpClient<PagedResult<VisitView>>(
      `${BASE}/work-orders/${workOrderId}/visits${toQueryString(params ?? {})}`
    ),

  createWorkOrderVisit: (workOrderId: string, request: CreateVisitRequest): Promise<VisitView> =>
    httpClient<VisitView>(`${BASE}/work-orders/${workOrderId}/visits`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  listVisits: (params: {
    workOrderId?: string | undefined;
    state?: string | undefined;
    page?: number | undefined;
    pageSize?: number | undefined;
  }): Promise<PagedResult<VisitView>> =>
    httpClient<PagedResult<VisitView>>(`${BASE}/visits${toQueryString(params)}`),

  getVisit: (visitId: string): Promise<VisitView> =>
    httpClient<VisitView>(`${BASE}/visits/${visitId}`),

  transitionVisit: (
    visitId: string,
    action:
      | "dispatch"
      | "travel"
      | "arrive"
      | "start"
      | "complete"
      | "no-access"
      | "cancel",
    request: TransitionRequest
  ): Promise<VisitView> =>
    httpClient<VisitView>(`${BASE}/visits/${visitId}/${action}`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  listVisitAssignments: (visitId: string): Promise<readonly AssignmentView[]> =>
    httpClient<readonly AssignmentView[]>(`${BASE}/visits/${visitId}/assignments`),

  assignVisit: (visitId: string, request: CreateAssignmentRequest): Promise<AssignmentView> =>
    httpClient<AssignmentView>(`${BASE}/visits/${visitId}/assignments`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // Resource Requirements
  listResourceRequirements: (workOrderId: string): Promise<readonly ResourceRequirementView[]> =>
    httpClient<readonly ResourceRequirementView[]>(`${BASE}/work-orders/${workOrderId}/resource-requirements`),

  addResourceRequirement: (
    workOrderId: string,
    request: CreateResourceRequirementRequest
  ): Promise<ResourceRequirementView> =>
    httpClient<ResourceRequirementView>(`${BASE}/work-orders/${workOrderId}/resource-requirements`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // Checklist & Diagnosis
  listChecklistAnswers: (workOrderId: string): Promise<readonly ChecklistAnswerView[]> =>
    httpClient<readonly ChecklistAnswerView[]>(`${BASE}/work-orders/${workOrderId}/checklist-answers`),

  saveChecklistAnswer: (
    workOrderId: string,
    request: ChecklistAnswerRequest
  ): Promise<ChecklistAnswerView> =>
    httpClient<ChecklistAnswerView>(`${BASE}/work-orders/${workOrderId}/checklist-answers`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  listDiagnoses: (workOrderId: string): Promise<readonly DiagnosisView[]> =>
    httpClient<readonly DiagnosisView[]>(`${BASE}/work-orders/${workOrderId}/diagnoses`),

  recordDiagnosis: (workOrderId: string, request: DiagnosisRequest): Promise<DiagnosisView> =>
    httpClient<DiagnosisView>(`${BASE}/work-orders/${workOrderId}/diagnoses`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // Parts Requirements
  listParts: (workOrderId: string): Promise<readonly PartRequirementView[]> =>
    httpClient<readonly PartRequirementView[]>(`${BASE}/work-orders/${workOrderId}/parts`),

  addPartRequirement: (
    workOrderId: string,
    request: CreatePartRequirementRequest
  ): Promise<PartRequirementView> =>
    httpClient<PartRequirementView>(`${BASE}/work-orders/${workOrderId}/parts`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  applyPartAction: (
    partId: string,
    action: "reserve" | "issue" | "consume" | "return" | "reverse",
    request: PartActionRequest
  ): Promise<PartRequirementView> =>
    httpClient<PartRequirementView>(`${BASE}/parts/${partId}/${action}`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // Labor
  listWorkOrderLabor: (workOrderId: string): Promise<readonly LaborView[]> =>
    httpClient<readonly LaborView[]>(`${BASE}/work-orders/${workOrderId}/labor`),

  listVisitLabor: (visitId: string): Promise<readonly LaborView[]> =>
    httpClient<readonly LaborView[]>(`${BASE}/visits/${visitId}/labor`),

  recordLabor: (request: RecordLaborRequest): Promise<LaborView> =>
    httpClient<LaborView>(`${BASE}/labor`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // Reports
  listWorkOrderReports: (workOrderId: string): Promise<readonly ServiceReportView[]> =>
    httpClient<readonly ServiceReportView[]>(`${BASE}/work-orders/${workOrderId}/reports`),

  saveReport: (workOrderId: string, request: CreateReportRequest): Promise<ServiceReportView> =>
    httpClient<ServiceReportView>(`${BASE}/work-orders/${workOrderId}/report`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  acceptReport: (reportId: string, version: number): Promise<ServiceReportView> =>
    httpClient<ServiceReportView>(`${BASE}/reports/${reportId}/accept?version=${version}`, {
      method: "POST"
    }),

  // SLA
  listSlaProfiles: (params: {
    legalEntityId?: string | undefined;
    page?: number | undefined;
    pageSize?: number | undefined;
  }): Promise<PagedResult<SlaProfileView>> =>
    httpClient<PagedResult<SlaProfileView>>(`${BASE}/sla/profiles${toQueryString(params)}`),

  createSlaProfile: (request: CreateSlaProfileRequest): Promise<SlaProfileView> =>
    httpClient<SlaProfileView>(`${BASE}/sla/profiles`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  listSlaClocks: (workOrderId: string): Promise<readonly SlaClockView[]> =>
    httpClient<readonly SlaClockView[]>(`${BASE}/work-orders/${workOrderId}/sla-clocks`),

  startSlaClock: (request: {
    workOrderId: string;
    profileId: string;
    clockType: string;
    startedAt: string;
  }): Promise<SlaClockView> =>
    httpClient<SlaClockView>(`${BASE}/sla/clocks`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  pauseSlaClock: (
    workOrderId: string,
    clockType: string,
    request: { reason?: string | undefined }
  ): Promise<SlaClockView> =>
    httpClient<SlaClockView>(`${BASE}/sla/clocks/${workOrderId}/${clockType}/pause`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  resumeSlaClock: (
    workOrderId: string,
    clockType: string,
    request: { reason?: string | undefined }
  ): Promise<SlaClockView> =>
    httpClient<SlaClockView>(`${BASE}/sla/clocks/${workOrderId}/${clockType}/resume`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // Charges & Billing
  listCharges: (
    workOrderId: string,
    params?: { page?: number | undefined; pageSize?: number | undefined }
  ): Promise<PagedResult<ServiceChargeView>> =>
    httpClient<PagedResult<ServiceChargeView>>(
      `${BASE}/work-orders/${workOrderId}/charges${toQueryString(params ?? {})}`
    ),

  createCharge: (workOrderId: string, request: CreateChargeRequest): Promise<ServiceChargeView> =>
    httpClient<ServiceChargeView>(`${BASE}/work-orders/${workOrderId}/charges`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  requestChargeInvoice: (chargeId: string): Promise<ServiceChargeView> =>
    httpClient<ServiceChargeView>(`${BASE}/charges/${chargeId}/request-invoice`, {
      method: "POST"
    }),

  allocateChargeInvoice: (
    chargeId: string,
    request: { invoiceId: string; allocatedAmount: number; invoiceLineId?: string | undefined }
  ): Promise<ServiceChargeView> =>
    httpClient<ServiceChargeView>(`${BASE}/charges/${chargeId}/allocate-invoice`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // Supplier Recoveries
  listSupplierRecoveries: (workOrderId?: string): Promise<readonly SupplierRecoveryView[]> =>
    workOrderId
      ? httpClient<readonly SupplierRecoveryView[]>(`${BASE}/work-orders/${workOrderId}/supplier-recoveries`)
      : httpClient<readonly SupplierRecoveryView[]>(`${BASE}/supplier-recoveries`),

  createSupplierRecovery: (request: CreateSupplierRecoveryRequest): Promise<SupplierRecoveryView> =>
    httpClient<SupplierRecoveryView>(`${BASE}/supplier-recoveries`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  transitionSupplierRecovery: (
    recoveryId: string,
    state: "Requested" | "Recovered" | "Rejected" | "Cancelled"
  ): Promise<SupplierRecoveryView> =>
    httpClient<SupplierRecoveryView>(`${BASE}/supplier-recoveries/${recoveryId}/${state}`, {
      method: "POST"
    }),

  // Downtime
  listWorkOrderDowntime: (workOrderId: string): Promise<readonly DowntimeView[]> =>
    httpClient<readonly DowntimeView[]>(`${BASE}/work-orders/${workOrderId}/downtime`),

  listSubjectDowntime: (subjectId: string): Promise<readonly DowntimeView[]> =>
    httpClient<readonly DowntimeView[]>(`${BASE}/subjects/${subjectId}/downtime`),

  startDowntime: (subjectId: string, request: DowntimeRequest): Promise<DowntimeView> =>
    httpClient<DowntimeView>(`${BASE}/subjects/${subjectId}/downtime`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  endDowntime: (downtimeId: string, endedAt: string): Promise<DowntimeView> =>
    httpClient<DowntimeView>(
      `${BASE}/downtime/${downtimeId}/end?endedAt=${encodeURIComponent(endedAt)}`,
      { method: "POST" }
    ),

  // Installed Base
  listInstalledBase: (params: {
    legalEntityId?: string | undefined;
    customerPartyId?: string | undefined;
    search?: string | undefined;
    page?: number | undefined;
    pageSize?: number | undefined;
  }): Promise<PagedResult<InstalledBaseView>> =>
    httpClient<PagedResult<InstalledBaseView>>(`${BASE}/installed-base${toQueryString(params)}`),

  getInstalledBase: (itemId: string): Promise<InstalledBaseView> =>
    httpClient<InstalledBaseView>(`${BASE}/installed-base/${itemId}`),

  createInstalledBase: (request: CreateInstalledBaseRequest): Promise<InstalledBaseView> =>
    httpClient<InstalledBaseView>(`${BASE}/installed-base`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  listComponentEvents: (itemId: string): Promise<readonly ComponentEventView[]> =>
    httpClient<readonly ComponentEventView[]>(`${BASE}/installed-base/${itemId}/component-events`),

  recordComponentEvent: (
    itemId: string,
    request: {
      itemId: string;
      componentItemId: string;
      eventType: string;
      occurredAt: string;
      replacedComponentId?: string | undefined;
      workOrderId?: string | undefined;
      notes?: string | undefined;
    }
  ): Promise<ComponentEventView> =>
    httpClient<ComponentEventView>(`${BASE}/installed-base/${itemId}/component-events`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // Work Templates
  listTemplates: (params: {
    legalEntityId?: string | undefined;
    page?: number | undefined;
    pageSize?: number | undefined;
  }): Promise<PagedResult<TemplateView>> =>
    httpClient<PagedResult<TemplateView>>(`${BASE}/templates${toQueryString(params)}`),

  getTemplate: (templateId: string): Promise<TemplateView> =>
    httpClient<TemplateView>(`${BASE}/templates/${templateId}`),

  createTemplate: (request: CreateTemplateRequest): Promise<TemplateView> =>
    httpClient<TemplateView>(`${BASE}/templates`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // Preventive Maintenance & Plans
  listMaintenancePlans: (params: {
    legalEntityId?: string | undefined;
    page?: number | undefined;
    pageSize?: number | undefined;
  }): Promise<PagedResult<MaintenancePlanView>> =>
    httpClient<PagedResult<MaintenancePlanView>>(`${BASE}/maintenance-plans${toQueryString(params)}`),

  getMaintenancePlan: (planId: string): Promise<MaintenancePlanView> =>
    httpClient<MaintenancePlanView>(`${BASE}/maintenance-plans/${planId}`),

  createMaintenancePlan: (request: CreateMaintenancePlanRequest): Promise<MaintenancePlanView> =>
    httpClient<MaintenancePlanView>(`${BASE}/maintenance-plans`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  listMaintenanceDue: (params: {
    legalEntityId?: string | undefined;
    state?: string | undefined;
    from?: string | undefined;
    to?: string | undefined;
    page?: number | undefined;
    pageSize?: number | undefined;
  }): Promise<PagedResult<MaintenanceDueView>> =>
    httpClient<PagedResult<MaintenanceDueView>>(`${BASE}/maintenance-due${toQueryString(params)}`),

  getMaintenanceDue: (dueId: string): Promise<MaintenanceDueView> =>
    httpClient<MaintenanceDueView>(`${BASE}/maintenance-due/${dueId}`),

  generateMaintenanceDue: (request: {
    fromDate: string;
    toDate: string;
    limit?: number | undefined;
  }): Promise<{ inspectedPlans: number; createdDueItems: number }> =>
    httpClient<{ inspectedPlans: number; createdDueItems: number }>(`${BASE}/maintenance-due/generate`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  convertDueToWorkOrder: (
    dueId: string,
    request: {
      legalEntityId: string;
      context: string;
      title: string;
      description?: string | undefined;
      customerPartyId?: string | undefined;
      projectId?: string | undefined;
      billingOwner?: string | undefined;
      workType?: string | undefined;
      priority?: string | undefined;
    }
  ): Promise<{ due: MaintenanceDueView; workOrder: WorkOrderView }> =>
    httpClient<{ due: MaintenanceDueView; workOrder: WorkOrderView }>(
      `${BASE}/maintenance-due/${dueId}/convert`,
      {
        method: "POST",
        body: JSON.stringify(request)
      }
    ),

  updateDueState: (
    dueId: string,
    request: { state: string; reason?: string | undefined; expectedVersion?: number | undefined }
  ): Promise<MaintenanceDueView> =>
    httpClient<MaintenanceDueView>(`${BASE}/maintenance-due/${dueId}/state`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // Maintenance Rounds
  listMaintenanceRounds: (params: {
    legalEntityId?: string | undefined;
    state?: string | undefined;
    page?: number | undefined;
    pageSize?: number | undefined;
  }): Promise<PagedResult<MaintenanceRoundView>> =>
    httpClient<PagedResult<MaintenanceRoundView>>(`${BASE}/maintenance-rounds${toQueryString(params)}`),

  getMaintenanceRound: (roundId: string): Promise<MaintenanceRoundView> =>
    httpClient<MaintenanceRoundView>(`${BASE}/maintenance-rounds/${roundId}`),

  createMaintenanceRound: (request: CreateMaintenanceRoundRequest): Promise<MaintenanceRoundView> =>
    httpClient<MaintenanceRoundView>(`${BASE}/maintenance-rounds`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  listRoundSubjects: (roundId: string): Promise<readonly RoundSubjectView[]> =>
    httpClient<readonly RoundSubjectView[]>(`${BASE}/maintenance-rounds/${roundId}/subjects`),

  addRoundSubject: (
    roundId: string,
    request: { subjectType: string; subjectId: string; sequenceNo: number }
  ): Promise<RoundSubjectView> =>
    httpClient<RoundSubjectView>(`${BASE}/maintenance-rounds/${roundId}/subjects`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  transitionRound: (
    roundId: string,
    action: "plan" | "start" | "complete" | "cancel",
    request: TransitionRequest
  ): Promise<MaintenanceRoundView> =>
    httpClient<MaintenanceRoundView>(`${BASE}/maintenance-rounds/${roundId}/${action}`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  recordRoundSubject: (
    subjectId: string,
    request: {
      state: string;
      result?: string | undefined;
      measurementsJson?: string | undefined;
      exceptions?: string | undefined;
      followUp?: string | undefined;
      expectedRoundVersion?: number | undefined;
    }
  ): Promise<RoundSubjectView> =>
    httpClient<RoundSubjectView>(`${BASE}/maintenance-round-subjects/${subjectId}/record`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // Meters
  listMeterReadings: (subjectId: string): Promise<readonly MeterReadingView[]> =>
    httpClient<readonly MeterReadingView[]>(`${BASE}/subjects/${subjectId}/meter-readings`),

  recordMeterReading: (
    subjectId: string,
    request: RecordMeterReadingRequest
  ): Promise<MeterReadingView> =>
    httpClient<MeterReadingView>(`${BASE}/subjects/${subjectId}/meter-readings`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // Contracts & Warranties
  listContracts: (params: {
    legalEntityId?: string | undefined;
    state?: string | undefined;
    page?: number | undefined;
    pageSize?: number | undefined;
  }): Promise<PagedResult<ServiceContractView>> =>
    httpClient<PagedResult<ServiceContractView>>(`${BASE}/contracts${toQueryString(params)}`),

  getContract: (contractId: string): Promise<ServiceContractView> =>
    httpClient<ServiceContractView>(`${BASE}/contracts/${contractId}`),

  createContract: (request: CreateContractRequest): Promise<ServiceContractView> =>
    httpClient<ServiceContractView>(`${BASE}/contracts`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  activateContract: (contractId: string): Promise<ServiceContractView> =>
    httpClient<ServiceContractView>(`${BASE}/contracts/${contractId}/activate`, {
      method: "POST"
    }),

  recordContractUsage: (
    contractId: string,
    request: { workOrderId: string; usageType: string; quantity: number; idempotencyKey: string }
  ): Promise<ServiceContractView> =>
    httpClient<ServiceContractView>(`${BASE}/contracts/${contractId}/usage`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  listWarranties: (params: {
    legalEntityId?: string | undefined;
    subjectId?: string | undefined;
    state?: string | undefined;
    page?: number | undefined;
    pageSize?: number | undefined;
  }): Promise<PagedResult<WarrantyView>> =>
    httpClient<PagedResult<WarrantyView>>(`${BASE}/warranties${toQueryString(params)}`),

  getWarranty: (warrantyId: string): Promise<WarrantyView> =>
    httpClient<WarrantyView>(`${BASE}/warranties/${warrantyId}`),

  createWarranty: (request: CreateWarrantyRequest): Promise<WarrantyView> =>
    httpClient<WarrantyView>(`${BASE}/warranties`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  resolveEntitlement: (request: EntitlementRequest): Promise<EntitlementView> =>
    httpClient<EntitlementView>(`${BASE}/entitlements/resolve`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // Crews, Skills, Territories
  listCrews: (legalEntityId: string): Promise<readonly CrewView[]> =>
    httpClient<readonly CrewView[]>(`${BASE}/crews?legalEntityId=${encodeURIComponent(legalEntityId)}`),

  createCrew: (request: CreateCrewRequest): Promise<CrewView> =>
    httpClient<CrewView>(`${BASE}/crews`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  listSkills: (): Promise<readonly SkillView[]> =>
    httpClient<readonly SkillView[]>(`${BASE}/skills`),

  createSkill: (request: CreateSkillRequest): Promise<SkillView> =>
    httpClient<SkillView>(`${BASE}/skills`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  listTerritories: (): Promise<readonly TerritoryView[]> =>
    httpClient<readonly TerritoryView[]>(`${BASE}/territories`),

  createTerritory: (request: CreateTerritoryRequest): Promise<TerritoryView> =>
    httpClient<TerritoryView>(`${BASE}/territories`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // Field Technician My Day & Mobile
  getMyDay: (params?: { date?: string | undefined; employeeId?: string | undefined }): Promise<MobileDayView> =>
    httpClient<MobileDayView>(`${BASE}/mobile/my-day${toQueryString(params ?? {})}`),

  submitOfflineCommand: (request: OfflineCommandRequest): Promise<OfflineCommandView> =>
    httpClient<OfflineCommandView>(`${BASE}/mobile/commands`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // Fixed Asset Service History
  listFixedAssetServiceHistory: (
    fixedAssetId: string,
    params?: { page?: number | undefined; pageSize?: number | undefined }
  ): Promise<PagedResult<FixedAssetServiceHistoryView>> =>
    httpClient<PagedResult<FixedAssetServiceHistoryView>>(
      `${BASE}/fixed-assets/${fixedAssetId}/service-history${toQueryString(params ?? {})}`
    )
};

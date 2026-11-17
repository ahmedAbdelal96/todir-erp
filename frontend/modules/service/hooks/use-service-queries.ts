import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { serviceApi } from "../services/service-api.ts";
import type {
  CreateServiceRequest,
  CreateWorkOrderRequest,
  TransitionRequest,
  CreateVisitRequest,
  CreateAssignmentRequest,
  CreateResourceRequirementRequest,
  ChecklistAnswerRequest,
  DiagnosisRequest,
  CreatePartRequirementRequest,
  PartActionRequest,
  RecordLaborRequest,
  CreateReportRequest,
  CreateChargeRequest,
  DowntimeRequest,
  CreateSupplierRecoveryRequest,
  CreateInstalledBaseRequest,
  CreateTemplateRequest,
  CreateMaintenancePlanRequest,
  CreateMaintenanceRoundRequest,
  RecordMeterReadingRequest,
  CreateContractRequest,
  CreateWarrantyRequest,
  CreateSlaProfileRequest,
  OfflineCommandRequest,
  EntitlementRequest
} from "../types.ts";

export const serviceKeys = {
  all: ["service"] as const,
  kpi: () => [...serviceKeys.all, "kpi"] as const,
  requests: (filters?: Record<string, unknown>) => [...serviceKeys.all, "requests", filters] as const,
  request: (id: string) => [...serviceKeys.all, "request", id] as const,
  workOrders: (filters?: Record<string, unknown>) => [...serviceKeys.all, "workOrders", filters] as const,
  unscheduledWorkOrders: (filters?: Record<string, unknown>) =>
    [...serviceKeys.all, "unscheduledWorkOrders", filters] as const,
  workOrder: (id: string) => [...serviceKeys.all, "workOrder", id] as const,
  visits: (filters?: Record<string, unknown>) => [...serviceKeys.all, "visits", filters] as const,
  visit: (id: string) => [...serviceKeys.all, "visit", id] as const,
  workOrderVisits: (workOrderId: string, filters?: Record<string, unknown>) =>
    [...serviceKeys.all, "workOrderVisits", workOrderId, filters] as const,
  assignments: (visitId: string) => [...serviceKeys.all, "assignments", visitId] as const,
  resourceRequirements: (workOrderId: string) =>
    [...serviceKeys.all, "resourceRequirements", workOrderId] as const,
  checklistAnswers: (workOrderId: string) => [...serviceKeys.all, "checklistAnswers", workOrderId] as const,
  diagnoses: (workOrderId: string) => [...serviceKeys.all, "diagnoses", workOrderId] as const,
  parts: (workOrderId: string) => [...serviceKeys.all, "parts", workOrderId] as const,
  labor: (scope: { workOrderId?: string | undefined; visitId?: string | undefined }) =>
    [...serviceKeys.all, "labor", scope] as const,
  reports: (workOrderId: string) => [...serviceKeys.all, "reports", workOrderId] as const,
  slaClocks: (workOrderId: string) => [...serviceKeys.all, "slaClocks", workOrderId] as const,
  allCharges: (filters?: Record<string, unknown>) =>
    [...serviceKeys.all, "allCharges", filters] as const,
  charges: (workOrderId: string, filters?: Record<string, unknown>) =>
    [...serviceKeys.all, "charges", workOrderId, filters] as const,
  supplierRecoveries: (workOrderId?: string) =>
    [...serviceKeys.all, "supplierRecoveries", workOrderId] as const,
  downtime: (scope: { workOrderId?: string | undefined; subjectId?: string | undefined }) =>
    [...serviceKeys.all, "downtime", scope] as const,
  installedBase: (filters?: Record<string, unknown>) =>
    [...serviceKeys.all, "installedBase", filters] as const,
  installedBaseItem: (id: string) => [...serviceKeys.all, "installedBaseItem", id] as const,
  componentEvents: (itemId: string) => [...serviceKeys.all, "componentEvents", itemId] as const,
  templates: (filters?: Record<string, unknown>) => [...serviceKeys.all, "templates", filters] as const,
  template: (id: string) => [...serviceKeys.all, "template", id] as const,
  maintenancePlans: (filters?: Record<string, unknown>) =>
    [...serviceKeys.all, "maintenancePlans", filters] as const,
  maintenancePlan: (id: string) => [...serviceKeys.all, "maintenancePlan", id] as const,
  maintenanceDue: (filters?: Record<string, unknown>) =>
    [...serviceKeys.all, "maintenanceDue", filters] as const,
  maintenanceRounds: (filters?: Record<string, unknown>) =>
    [...serviceKeys.all, "maintenanceRounds", filters] as const,
  maintenanceRound: (id: string) => [...serviceKeys.all, "maintenanceRound", id] as const,
  roundSubjects: (roundId: string) => [...serviceKeys.all, "roundSubjects", roundId] as const,
  meterReadings: (subjectId: string) => [...serviceKeys.all, "meterReadings", subjectId] as const,
  contracts: (filters?: Record<string, unknown>) => [...serviceKeys.all, "contracts", filters] as const,
  contract: (id: string) => [...serviceKeys.all, "contract", id] as const,
  warranties: (filters?: Record<string, unknown>) => [...serviceKeys.all, "warranties", filters] as const,
  warranty: (id: string) => [...serviceKeys.all, "warranty", id] as const,
  slaProfiles: (filters?: Record<string, unknown>) => [...serviceKeys.all, "slaProfiles", filters] as const,
  crews: (legalEntityId: string) => [...serviceKeys.all, "crews", legalEntityId] as const,
  skills: () => [...serviceKeys.all, "skills"] as const,
  territories: () => [...serviceKeys.all, "territories"] as const,
  myDay: (date?: string, employeeId?: string) => [...serviceKeys.all, "myDay", date, employeeId] as const,
  fixedAssetHistory: (assetId: string) => [...serviceKeys.all, "fixedAssetHistory", assetId] as const
};

// KPI
export function useServiceKpiQuery() {
  return useQuery({
    queryKey: serviceKeys.kpi(),
    queryFn: () => serviceApi.getKpi()
  });
}

// Service Requests
export function useServiceRequestsQuery(
  arg1?: string | { legalEntityId?: string; state?: string; search?: string; context?: any; status?: any; page?: number; pageSize?: number },
  arg2?: { legalEntityId?: string; state?: string; search?: string; context?: any; status?: any; page?: number; pageSize?: number }
) {
  const filters = typeof arg1 === "string" ? { legalEntityId: arg1, ...arg2 } : (arg1 ?? {});
  return useQuery({
    queryKey: serviceKeys.requests(filters),
    queryFn: () => serviceApi.listRequests(filters)
  });
}

export function useServiceRequestQuery(arg1?: string | undefined, arg2?: string | undefined) {
  const id = arg2 ?? arg1;
  return useQuery({
    queryKey: serviceKeys.request(id ?? ""),
    queryFn: () => serviceApi.getRequest(id!),
    enabled: Boolean(id)
  });
}

export function useCreateServiceRequestMutation(legalEntityId?: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateServiceRequest) => serviceApi.createRequest(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: serviceKeys.all });
    }
  });
}

export function useTransitionServiceRequestMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({
      requestId,
      action
    }: {
      requestId: string;
      action: "triage" | "resolve" | "close" | "cancel";
    }) => serviceApi.transitionRequest(requestId, action),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: serviceKeys.all });
    }
  });
}

// Work Orders
export function useWorkOrdersQuery(
  arg1?: string | { legalEntityId?: string; state?: string; status?: any; context?: any; contextName?: string; page?: number; pageSize?: number },
  arg2?: { legalEntityId?: string; state?: string; status?: any; context?: any; contextName?: string; page?: number; pageSize?: number }
) {
  const filters = typeof arg1 === "string" ? { legalEntityId: arg1, ...arg2 } : (arg1 ?? {});
  return useQuery({
    queryKey: serviceKeys.workOrders(filters),
    queryFn: () => serviceApi.listWorkOrders(filters)
  });
}

export function useUnscheduledWorkOrdersQuery(filters: {
  legalEntityId?: string | undefined;
  page?: number | undefined;
  pageSize?: number | undefined;
}) {
  return useQuery({
    queryKey: serviceKeys.unscheduledWorkOrders(filters),
    queryFn: () => serviceApi.listUnscheduledWorkOrders(filters)
  });
}

export function useWorkOrderQuery(arg1?: string | undefined, arg2?: string | undefined) {
  const id = arg2 ?? arg1;
  return useQuery({
    queryKey: serviceKeys.workOrder(id ?? ""),
    queryFn: () => serviceApi.getWorkOrder(id!),
    enabled: Boolean(id)
  });
}

export function useCreateWorkOrderMutation(_legalEntityId?: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateWorkOrderRequest) => serviceApi.createWorkOrder(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: serviceKeys.all });
    }
  });
}

export function useTransitionWorkOrderMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({
      workOrderId,
      action,
      request
    }: {
      workOrderId: string;
      action:
        | "ready"
        | "schedule"
        | "start"
        | "waiting"
        | "technical-complete"
        | "accept"
        | "close"
        | "cancel"
        | "reopen";
      request: TransitionRequest;
    }) => serviceApi.transitionWorkOrder(workOrderId, action, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: serviceKeys.all });
    }
  });
}

// Visits & Scheduling
export function useWorkOrderVisitsQuery(
  workOrderId?: string | undefined,
  params?: { state?: string | undefined; page?: number | undefined; pageSize?: number | undefined }
) {
  return useQuery({
    queryKey: serviceKeys.workOrderVisits(workOrderId ?? "", params),
    queryFn: () => serviceApi.listWorkOrderVisits(workOrderId!, params),
    enabled: Boolean(workOrderId)
  });
}

export function useVisitsQuery(
  arg1?: string | { workOrderId?: string; state?: string; status?: string; page?: number; pageSize?: number },
  arg2?: { workOrderId?: string; state?: string; status?: string; page?: number; pageSize?: number }
) {
  const params = typeof arg1 === "string" ? { ...arg2 } : (arg1 ?? {});
  return useQuery({
    queryKey: serviceKeys.visits(params),
    queryFn: () => serviceApi.listVisits(params)
  });
}

export function useVisitQuery(visitId?: string | undefined) {
  return useQuery({
    queryKey: serviceKeys.visit(visitId ?? ""),
    queryFn: () => serviceApi.getVisit(visitId!),
    enabled: Boolean(visitId)
  });
}

export function useCreateVisitMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ workOrderId, request }: { workOrderId: string; request: CreateVisitRequest }) =>
      serviceApi.createWorkOrderVisit(workOrderId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: serviceKeys.all });
    }
  });
}

export function useTransitionVisitMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({
      visitId,
      action,
      request
    }: {
      visitId: string;
      action:
        | "dispatch"
        | "travel"
        | "arrive"
        | "start"
        | "complete"
        | "no-access"
        | "cancel";
      request: TransitionRequest;
    }) => serviceApi.transitionVisit(visitId, action, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: serviceKeys.all });
    }
  });
}

export function useVisitAssignmentsQuery(visitId?: string | undefined) {
  return useQuery({
    queryKey: serviceKeys.assignments(visitId ?? ""),
    queryFn: () => serviceApi.listVisitAssignments(visitId!),
    enabled: Boolean(visitId)
  });
}

export function useAssignVisitMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ visitId, request }: { visitId: string; request: CreateAssignmentRequest }) =>
      serviceApi.assignVisit(visitId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: serviceKeys.all });
    }
  });
}

// Sub-Resources
export function useResourceRequirementsQuery(workOrderId?: string | undefined) {
  return useQuery({
    queryKey: serviceKeys.resourceRequirements(workOrderId ?? ""),
    queryFn: () => serviceApi.listResourceRequirements(workOrderId!),
    enabled: Boolean(workOrderId)
  });
}

export function useAddResourceRequirementMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({
      workOrderId,
      request
    }: {
      workOrderId: string;
      request: CreateResourceRequirementRequest;
    }) => serviceApi.addResourceRequirement(workOrderId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: serviceKeys.all });
    }
  });
}

export function useChecklistAnswersQuery(workOrderId?: string | undefined) {
  return useQuery({
    queryKey: serviceKeys.checklistAnswers(workOrderId ?? ""),
    queryFn: () => serviceApi.listChecklistAnswers(workOrderId!),
    enabled: Boolean(workOrderId)
  });
}

export function useSaveChecklistAnswerMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({
      workOrderId,
      request
    }: {
      workOrderId: string;
      request: ChecklistAnswerRequest;
    }) => serviceApi.saveChecklistAnswer(workOrderId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: serviceKeys.all });
    }
  });
}

export function useDiagnosesQuery(workOrderId?: string | undefined) {
  return useQuery({
    queryKey: serviceKeys.diagnoses(workOrderId ?? ""),
    queryFn: () => serviceApi.listDiagnoses(workOrderId!),
    enabled: Boolean(workOrderId)
  });
}

export function useRecordDiagnosisMutation(_legalEntityId?: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (args: any) => {
      const workOrderId = args.workOrderId;
      const request = args.request ?? {
        workOrderId,
        reportedSymptom: args.reportedSymptom || args.symptom,
        diagnosis: args.diagnosis,
        failureModeCode: args.failureModeCode || args.failureCode,
        rootCauseCode: args.rootCauseCode || args.rootCause,
        resolutionCode: args.resolutionCode,
        notes: args.notes
      };
      return serviceApi.recordDiagnosis(workOrderId, request);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: serviceKeys.all });
    }
  });
}

export function usePartsQuery(workOrderId?: string | undefined) {
  return useQuery({
    queryKey: serviceKeys.parts(workOrderId ?? ""),
    queryFn: () => serviceApi.listParts(workOrderId!),
    enabled: Boolean(workOrderId)
  });
}

export function useAddPartRequirementMutation(_legalEntityId?: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (args: any) => {
      const workOrderId = args.workOrderId;
      const request = args.request ?? {
        productId: args.productId || args.itemId || "PART-001",
        quantity: args.quantity ?? args.requiredQuantity ?? 1,
        uomCode: args.uomCode || args.uom || "PCS",
        description: args.description,
        legalEntityId: args.legalEntityId
      };
      return serviceApi.addPartRequirement(workOrderId, request);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: serviceKeys.all });
    }
  });
}

export function useApplyPartActionMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (args: any) => {
      const partId = args.partId || args.requirementId;
      const action = args.action;
      const request = args.request;
      return serviceApi.applyPartAction(partId, action, request);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: serviceKeys.all });
    }
  });
}

export function useWorkOrderLaborQuery(workOrderId?: string | undefined) {
  return useQuery({
    queryKey: serviceKeys.labor({ workOrderId }),
    queryFn: () => serviceApi.listWorkOrderLabor(workOrderId!),
    enabled: Boolean(workOrderId)
  });
}

export function useVisitLaborQuery(visitId?: string | undefined) {
  return useQuery({
    queryKey: serviceKeys.labor({ visitId }),
    queryFn: () => serviceApi.listVisitLabor(visitId!),
    enabled: Boolean(visitId)
  });
}

export function useRecordLaborMutation(_legalEntityId?: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (args: any) => {
      const visitId = args.visitId || "00000000-0000-0000-0000-000000000000";
      const request = args.request ?? {
        visitId,
        employeeId: args.employeeId || args.technicianId || "TECH-001",
        startedAt: args.startedAt || new Date().toISOString(),
        endedAt: args.endedAt || new Date().toISOString(),
        timeCategory: args.timeCategory || (args.activityType === "travel" ? "Travel" : "OnSiteWork"),
        costRate: args.costRate || args.hourlyRate,
        currencyCode: args.currencyCode || args.currency || "SAR"
      };
      return serviceApi.recordLabor(request);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: serviceKeys.all });
    }
  });
}

export function useWorkOrderReportsQuery(workOrderId?: string | undefined) {
  return useQuery({
    queryKey: serviceKeys.reports(workOrderId ?? ""),
    queryFn: () => serviceApi.listWorkOrderReports(workOrderId!),
    enabled: Boolean(workOrderId)
  });
}

export function useSaveReportMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ workOrderId, request }: { workOrderId: string; request: CreateReportRequest }) =>
      serviceApi.saveReport(workOrderId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: serviceKeys.all });
    }
  });
}

export function useAcceptReportMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ reportId, version }: { reportId: string; version: number }) =>
      serviceApi.acceptReport(reportId, version),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: serviceKeys.all });
    }
  });
}

export function useSlaClocksQuery(workOrderId?: string | undefined) {
  return useQuery({
    queryKey: serviceKeys.slaClocks(workOrderId ?? ""),
    queryFn: () => serviceApi.listSlaClocks(workOrderId!),
    enabled: Boolean(workOrderId)
  });
}

export function useStartSlaClockMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: {
      workOrderId: string;
      profileId: string;
      clockType: string;
      startedAt: string;
    }) => serviceApi.startSlaClock(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: serviceKeys.all });
    }
  });
}

export function usePauseSlaClockMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({
      workOrderId,
      clockType,
      reason
    }: {
      workOrderId: string;
      clockType: string;
      reason?: string | undefined;
    }) => serviceApi.pauseSlaClock(workOrderId, clockType, { reason }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: serviceKeys.all });
    }
  });
}

export function useResumeSlaClockMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({
      workOrderId,
      clockType,
      reason
    }: {
      workOrderId: string;
      clockType: string;
      reason?: string | undefined;
    }) => serviceApi.resumeSlaClock(workOrderId, clockType, { reason }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: serviceKeys.all });
    }
  });
}

export function useWorkOrderChargesQuery(
  workOrderId?: string | undefined,
  params?: { page?: number | undefined; pageSize?: number | undefined }
) {
  return useQuery({
    queryKey: serviceKeys.charges(workOrderId ?? "", params),
    queryFn: () => serviceApi.listCharges(workOrderId!, params),
    enabled: Boolean(workOrderId)
  });
}

export function useAllChargesQuery(
  arg1?: string | { legalEntityId?: string; state?: string; page?: number; pageSize?: number },
  arg2?: { state?: string; page?: number; pageSize?: number }
) {
  const filters = typeof arg1 === "string" ? { legalEntityId: arg1, ...arg2 } : (arg1 ?? {});
  return useQuery({
    queryKey: serviceKeys.allCharges(filters),
    queryFn: () => serviceApi.listAllCharges(filters)
  });
}

export function useCreateChargeMutation(_legalEntityId?: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (args: any) => {
      const workOrderId = args.workOrderId;
      const request = args.request ?? {
        category: args.category || args.chargeType || "Labor",
        description: args.description || "",
        costAmount: Number(args.costAmount) || 0,
        customerAmount: Number(args.customerAmount ?? args.totalAmount) || 0,
        currencyCode: args.currencyCode || args.currency || "SAR"
      };
      return serviceApi.createCharge(workOrderId, request);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: serviceKeys.all });
    }
  });
}

export function useRequestChargeInvoiceMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (chargeId: string) => serviceApi.requestChargeInvoice(chargeId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: serviceKeys.all });
    }
  });
}

export function useAllocateChargeInvoiceMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({
      chargeId,
      request
    }: {
      chargeId: string;
      request: { invoiceId: string; allocatedAmount: number; invoiceLineId?: string | undefined };
    }) => serviceApi.allocateChargeInvoice(chargeId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: serviceKeys.all });
    }
  });
}

export function useSupplierRecoveriesQuery(workOrderId?: string | undefined) {
  return useQuery({
    queryKey: serviceKeys.supplierRecoveries(workOrderId),
    queryFn: () => serviceApi.listSupplierRecoveries(workOrderId)
  });
}

export function useCreateSupplierRecoveryMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateSupplierRecoveryRequest) =>
      serviceApi.createSupplierRecovery(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: serviceKeys.all });
    }
  });
}

export function useTransitionSupplierRecoveryMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({
      recoveryId,
      state
    }: {
      recoveryId: string;
      state: "Requested" | "Recovered" | "Rejected" | "Cancelled";
    }) => serviceApi.transitionSupplierRecovery(recoveryId, state),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: serviceKeys.all });
    }
  });
}

export function useWorkOrderDowntimeQuery(workOrderId?: string | undefined) {
  return useQuery({
    queryKey: serviceKeys.downtime({ workOrderId }),
    queryFn: () => serviceApi.listWorkOrderDowntime(workOrderId!),
    enabled: Boolean(workOrderId)
  });
}

export function useSubjectDowntimeQuery(subjectId?: string | undefined) {
  return useQuery({
    queryKey: serviceKeys.downtime({ subjectId }),
    queryFn: () => serviceApi.listSubjectDowntime(subjectId!),
    enabled: Boolean(subjectId)
  });
}

export function useStartDowntimeMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ subjectId, request }: { subjectId: string; request: DowntimeRequest }) =>
      serviceApi.startDowntime(subjectId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: serviceKeys.all });
    }
  });
}

export function useEndDowntimeMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ downtimeId, endedAt }: { downtimeId: string; endedAt: string }) =>
      serviceApi.endDowntime(downtimeId, endedAt),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: serviceKeys.all });
    }
  });
}

// Installed Base
export function useInstalledBaseQuery(arg1?: any, maybeFilters?: any) {
  const filters = typeof arg1 === "string" ? { legalEntityId: arg1, ...maybeFilters } : (arg1 ?? {});
  return useQuery({
    queryKey: serviceKeys.installedBase(filters),
    queryFn: () => serviceApi.listInstalledBase(filters)
  });
}

export function useInstalledBaseItemQuery(itemId?: string | undefined) {
  return useQuery({
    queryKey: serviceKeys.installedBaseItem(itemId ?? ""),
    queryFn: () => serviceApi.getInstalledBase(itemId!),
    enabled: Boolean(itemId)
  });
}

export function useCreateInstalledBaseMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateInstalledBaseRequest) => serviceApi.createInstalledBase(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: serviceKeys.all });
    }
  });
}

export function useComponentEventsQuery(itemId?: string | undefined) {
  return useQuery({
    queryKey: serviceKeys.componentEvents(itemId ?? ""),
    queryFn: () => serviceApi.listComponentEvents(itemId!),
    enabled: Boolean(itemId)
  });
}

export function useRecordComponentEventMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({
      itemId,
      request
    }: {
      itemId: string;
      request: {
        itemId: string;
        componentItemId: string;
        eventType: string;
        occurredAt: string;
        replacedComponentId?: string | undefined;
        workOrderId?: string | undefined;
        notes?: string | undefined;
      };
    }) => serviceApi.recordComponentEvent(itemId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: serviceKeys.all });
    }
  });
}

// Templates
export function useTemplatesQuery(params: {
  legalEntityId?: string | undefined;
  page?: number | undefined;
  pageSize?: number | undefined;
}) {
  return useQuery({
    queryKey: serviceKeys.templates(params),
    queryFn: () => serviceApi.listTemplates(params)
  });
}

export function useTemplateQuery(templateId?: string | undefined) {
  return useQuery({
    queryKey: serviceKeys.template(templateId ?? ""),
    queryFn: () => serviceApi.getTemplate(templateId!),
    enabled: Boolean(templateId)
  });
}

export function useCreateTemplateMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateTemplateRequest) => serviceApi.createTemplate(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: serviceKeys.all });
    }
  });
}

// Preventive Plans & Due
export function useMaintenancePlansQuery(arg1?: any, maybeParams?: any) {
  const params = typeof arg1 === "string" ? { legalEntityId: arg1, ...maybeParams } : (arg1 ?? {});
  return useQuery({
    queryKey: serviceKeys.maintenancePlans(params),
    queryFn: () => serviceApi.listMaintenancePlans(params)
  });
}

export function useMaintenancePlanQuery(planId?: string | undefined) {
  return useQuery({
    queryKey: serviceKeys.maintenancePlan(planId ?? ""),
    queryFn: () => serviceApi.getMaintenancePlan(planId!),
    enabled: Boolean(planId)
  });
}

export function useCreateMaintenancePlanMutation(_legalEntityId?: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateMaintenancePlanRequest) =>
      serviceApi.createMaintenancePlan(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: serviceKeys.all });
    }
  });
}

export function useMaintenanceDueQuery(arg1?: any, maybeParams?: any) {
  const params = typeof arg1 === "string" ? { legalEntityId: arg1, ...maybeParams } : (arg1 ?? {});
  return useQuery({
    queryKey: serviceKeys.maintenanceDue(params),
    queryFn: () => serviceApi.listMaintenanceDue(params)
  });
}

export const useMaintenanceDuesQuery = useMaintenanceDueQuery;

export function useGenerateDueMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: { fromDate: string; toDate: string; limit?: number | undefined }) =>
      serviceApi.generateMaintenanceDue(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: serviceKeys.all });
    }
  });
}

export function useConvertDueMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({
      dueId,
      request
    }: {
      dueId: string;
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
      };
    }) => serviceApi.convertDueToWorkOrder(dueId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: serviceKeys.all });
    }
  });
}

export function useUpdateDueStateMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({
      dueId,
      request
    }: {
      dueId: string;
      request: { state: string; reason?: string | undefined; expectedVersion?: number | undefined };
    }) => serviceApi.updateDueState(dueId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: serviceKeys.all });
    }
  });
}

// Maintenance Rounds
export function useMaintenanceRoundsQuery(params: {
  legalEntityId?: string | undefined;
  state?: string | undefined;
  page?: number | undefined;
  pageSize?: number | undefined;
}) {
  return useQuery({
    queryKey: serviceKeys.maintenanceRounds(params),
    queryFn: () => serviceApi.listMaintenanceRounds(params)
  });
}

export function useMaintenanceRoundQuery(roundId?: string | undefined) {
  return useQuery({
    queryKey: serviceKeys.maintenanceRound(roundId ?? ""),
    queryFn: () => serviceApi.getMaintenanceRound(roundId!),
    enabled: Boolean(roundId)
  });
}

export function useCreateMaintenanceRoundMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateMaintenanceRoundRequest) =>
      serviceApi.createMaintenanceRound(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: serviceKeys.all });
    }
  });
}

export function useRoundSubjectsQuery(roundId?: string | undefined) {
  return useQuery({
    queryKey: serviceKeys.roundSubjects(roundId ?? ""),
    queryFn: () => serviceApi.listRoundSubjects(roundId!),
    enabled: Boolean(roundId)
  });
}

export function useAddRoundSubjectMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({
      roundId,
      request
    }: {
      roundId: string;
      request: { subjectType: string; subjectId: string; sequenceNo: number };
    }) => serviceApi.addRoundSubject(roundId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: serviceKeys.all });
    }
  });
}

export function useTransitionRoundMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({
      roundId,
      action,
      request
    }: {
      roundId: string;
      action: "plan" | "start" | "complete" | "cancel";
      request: TransitionRequest;
    }) => serviceApi.transitionRound(roundId, action, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: serviceKeys.all });
    }
  });
}

export function useRecordRoundSubjectMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({
      subjectId,
      request
    }: {
      subjectId: string;
      request: {
        state: string;
        result?: string | undefined;
        measurementsJson?: string | undefined;
        exceptions?: string | undefined;
        followUp?: string | undefined;
        expectedRoundVersion?: number | undefined;
      };
    }) => serviceApi.recordRoundSubject(subjectId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: serviceKeys.all });
    }
  });
}

// Meters
export function useSubjectMeterReadingsQuery(subjectId?: string | undefined) {
  return useQuery({
    queryKey: serviceKeys.meterReadings(subjectId ?? ""),
    queryFn: () => serviceApi.listMeterReadings(subjectId!),
    enabled: Boolean(subjectId)
  });
}

export function useRecordMeterReadingMutation(_legalEntityId?: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (args: any) => {
      const subjectId = args.subjectId || args.installedBaseItemId || "00000000-0000-0000-0000-000000000000";
      const request = args.request ?? {
        legalEntityId: args.legalEntityId || "00000000-0000-0000-0000-000000000001",
        subjectId,
        meterCode: args.meterCode || "HOURS",
        value: Number(args.value) || 0,
        uom: args.uom || "HRS",
        readingAt: args.readingAt || new Date().toISOString()
      };
      return serviceApi.recordMeterReading(subjectId, request);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: serviceKeys.all });
    }
  });
}

// Contracts & Warranties
export function useServiceContractsQuery(params: {
  legalEntityId?: string | undefined;
  state?: string | undefined;
  page?: number | undefined;
  pageSize?: number | undefined;
}) {
  return useQuery({
    queryKey: serviceKeys.contracts(params),
    queryFn: () => serviceApi.listContracts(params)
  });
}

export function useServiceContractQuery(contractId?: string | undefined) {
  return useQuery({
    queryKey: serviceKeys.contract(contractId ?? ""),
    queryFn: () => serviceApi.getContract(contractId!),
    enabled: Boolean(contractId)
  });
}

export function useCreateContractMutation(_legalEntityId?: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateContractRequest) => serviceApi.createContract(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: serviceKeys.all });
    }
  });
}

export function useActivateContractMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (contractId: string) => serviceApi.activateContract(contractId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: serviceKeys.all });
    }
  });
}

export function useRecordContractUsageMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({
      contractId,
      request
    }: {
      contractId: string;
      request: { workOrderId: string; usageType: string; quantity: number; idempotencyKey: string };
    }) => serviceApi.recordContractUsage(contractId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: serviceKeys.all });
    }
  });
}

export function useWarrantiesQuery(arg1?: any, maybeParams?: any) {
  const p = typeof arg1 === "string" ? { legalEntityId: arg1, ...maybeParams } : (arg1 ?? {});
  return useQuery({
    queryKey: serviceKeys.warranties(p),
    queryFn: () => serviceApi.listWarranties(p)
  });
}

export function useWarrantyQuery(warrantyId?: string | undefined) {
  return useQuery({
    queryKey: serviceKeys.warranty(warrantyId ?? ""),
    queryFn: () => serviceApi.getWarranty(warrantyId!),
    enabled: Boolean(warrantyId)
  });
}

export function useCreateWarrantyMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateWarrantyRequest) => serviceApi.createWarranty(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: serviceKeys.all });
    }
  });
}

export function useResolveEntitlementMutation() {
  return useMutation({
    mutationFn: (request: EntitlementRequest) => serviceApi.resolveEntitlement(request)
  });
}

// SLA Profiles
export function useSlaProfilesQuery(arg1?: any, maybeParams?: any) {
  const p = typeof arg1 === "string" ? { legalEntityId: arg1, ...maybeParams } : (arg1 ?? {});
  return useQuery({
    queryKey: serviceKeys.slaProfiles(p),
    queryFn: () => serviceApi.listSlaProfiles(p)
  });
}

export function useCreateSlaProfileMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateSlaProfileRequest) => serviceApi.createSlaProfile(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: serviceKeys.all });
    }
  });
}

// Dispatcher Reference Lists
export function useCrewsQuery(legalEntityId: string) {
  return useQuery({
    queryKey: serviceKeys.crews(legalEntityId),
    queryFn: () => serviceApi.listCrews(legalEntityId),
    enabled: Boolean(legalEntityId)
  });
}

export function useSkillsQuery() {
  return useQuery({
    queryKey: serviceKeys.skills(),
    queryFn: () => serviceApi.listSkills()
  });
}

export function useTerritoriesQuery() {
  return useQuery({
    queryKey: serviceKeys.territories(),
    queryFn: () => serviceApi.listTerritories()
  });
}

// Technician My Day
export function useMyDayQuery(date?: string | undefined, employeeId?: string | undefined) {
  return useQuery({
    queryKey: serviceKeys.myDay(date, employeeId),
    queryFn: () => serviceApi.getMyDay({ date, employeeId })
  });
}

export function useSubmitOfflineCommandMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: OfflineCommandRequest) => serviceApi.submitOfflineCommand(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: serviceKeys.all });
    }
  });
}

// Fixed Asset History
export function useFixedAssetServiceHistoryQuery(
  fixedAssetId?: string | undefined,
  params?: { page?: number | undefined; pageSize?: number | undefined }
) {
  return useQuery({
    queryKey: serviceKeys.fixedAssetHistory(fixedAssetId ?? ""),
    queryFn: () => serviceApi.listFixedAssetServiceHistory(fixedAssetId!, params),
    enabled: Boolean(fixedAssetId)
  });
}

// Aliases & Convenience Wrappers
export const useWorkOrderPartsQuery = (arg1: string, maybeWorkOrderId?: string) =>
  usePartsQuery(maybeWorkOrderId ?? arg1);

export const useWorkOrderChecklistsQuery = (arg1: string, maybeWorkOrderId?: string) =>
  useChecklistAnswersQuery(maybeWorkOrderId ?? arg1);

export const useWorkOrderDiagnosesQuery = (arg1: string, maybeWorkOrderId?: string) =>
  useDiagnosesQuery(maybeWorkOrderId ?? arg1);

export const useWorkOrderSlaClocksQuery = (arg1: string, maybeWorkOrderId?: string) =>
  useSlaClocksQuery(maybeWorkOrderId ?? arg1);

export const useWorkOrderSupplierRecoveriesQuery = (arg1?: string, maybeWorkOrderId?: string) =>
  useSupplierRecoveriesQuery(maybeWorkOrderId ?? arg1);

export const useContractsQuery = (arg1?: any, maybeParams?: any) => {
  const p = typeof arg1 === "string" ? { legalEntityId: arg1, ...maybeParams } : (arg1 ?? {});
  return useServiceContractsQuery(p);
};

export const useMeterReadingsQuery = (arg1: string, maybeSubjectId?: string) =>
  useSubjectMeterReadingsQuery(maybeSubjectId ?? arg1);

// Lifecycle convenience mutations
export const useStartWorkOrderMutation = (legalEntityId?: string) => {
  const t = useTransitionWorkOrderMutation();
  return {
    ...t,
    mutateAsync: (args: { legalEntityId?: string; workOrderId: string }) =>
      (t.mutateAsync as any)({ workOrderId: args.workOrderId, action: "start", request: { version: 1 } })
  };
};

export const useCompleteWorkOrderMutation = (legalEntityId?: string) => {
  const t = useTransitionWorkOrderMutation();
  return {
    ...t,
    mutateAsync: (args: { legalEntityId?: string; workOrderId: string }) =>
      (t.mutateAsync as any)({ workOrderId: args.workOrderId, action: "technical-complete", request: { version: 1 } })
  };
};

export const useCloseWorkOrderMutation = (legalEntityId?: string) => {
  const t = useTransitionWorkOrderMutation();
  return {
    ...t,
    mutateAsync: (args: { legalEntityId?: string; workOrderId: string }) =>
      (t.mutateAsync as any)({ workOrderId: args.workOrderId, action: "close", request: { version: 1 } })
  };
};

export const useCancelWorkOrderMutation = (legalEntityId?: string) => {
  const t = useTransitionWorkOrderMutation();
  return {
    ...t,
    mutateAsync: (args: { legalEntityId?: string; workOrderId: string; reason?: string }) =>
      (t.mutateAsync as any)({ workOrderId: args.workOrderId, action: "cancel", request: { version: 1, reason: args.reason } })
  };
};

export const useTriageServiceRequestMutation = (legalEntityId?: string) => {
  const t = useTransitionServiceRequestMutation();
  return {
    ...t,
    mutateAsync: (args: { legalEntityId?: string; requestId: string; priority?: any; triageNotes?: string }) =>
      (t.mutateAsync as any)({ requestId: args.requestId, action: "triage", request: { version: 1, reason: args.triageNotes } })
  };
};

export const useResolveServiceRequestMutation = (legalEntityId?: string) => {
  const t = useTransitionServiceRequestMutation();
  return {
    ...t,
    mutateAsync: (args: { legalEntityId?: string; requestId: string; resolutionSummary?: string }) =>
      (t.mutateAsync as any)({ requestId: args.requestId, action: "resolve", request: { version: 1, reason: args.resolutionSummary } })
  };
};

export const useCloseServiceRequestMutation = (legalEntityId?: string) => {
  const t = useTransitionServiceRequestMutation();
  return {
    ...t,
    mutateAsync: (args: { legalEntityId?: string; requestId: string }) =>
      (t.mutateAsync as any)({ requestId: args.requestId, action: "close", request: { version: 1 } })
  };
};

export const useCancelServiceRequestMutation = (legalEntityId?: string) => {
  const t = useTransitionServiceRequestMutation();
  return {
    ...t,
    mutateAsync: (args: { legalEntityId?: string; requestId: string; reason?: string }) =>
      (t.mutateAsync as any)({ requestId: args.requestId, action: "cancel", request: { version: 1, reason: args.reason } })
  };
};

export const useScheduleVisitMutation = (legalEntityId?: string) => {
  const c = useCreateVisitMutation();
  return {
    ...c,
    mutateAsync: (args: any) =>
      (c.mutateAsync as any)({
        workOrderId: args.workOrderId,
        request: {
          locationType: args.visitType === "onsite" ? "OnSite" : args.visitType === "depot" ? "Depot" : "Remote",
          plannedStart: args.scheduledStartUtc,
          plannedEnd: args.scheduledEndUtc,
          siteInstructions: args.notes
        }
      })
  };
};

export const useAssignTechnicianMutation = (_legalEntityId?: string) => {
  const a = useAssignVisitMutation();
  return {
    ...a,
    mutateAsync: (args: any) => {
      const isGuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
      const empId = args.employeeId ?? args.technicianId;
      const employeeId = empId && isGuid.test(empId) ? empId : undefined;
      const displayName = args.displayName || (!employeeId && empId ? String(empId) : "فني خدمة معتمد");
      return a.mutateAsync({
        visitId: args.visitId,
        request: {
          employeeId,
          displayName,
          resourceType: args.resourceType || "Technician"
        }
      });
    }
  };
};

export const useStartVisitTravelMutation = (legalEntityId?: string) => {
  const t = useTransitionVisitMutation();
  return {
    ...t,
    mutate: (args: { legalEntityId?: string; visitId: string }) =>
      (t.mutate as any)({ visitId: args.visitId, action: "travel", request: { version: 1 } }),
    mutateAsync: (args: { legalEntityId?: string; visitId: string }) =>
      (t.mutateAsync as any)({ visitId: args.visitId, action: "travel", request: { version: 1 } })
  };
};

export const useArriveVisitMutation = (legalEntityId?: string) => {
  const t = useTransitionVisitMutation();
  return {
    ...t,
    mutate: (args: { legalEntityId?: string; visitId: string }) =>
      (t.mutate as any)({ visitId: args.visitId, action: "arrive", request: { version: 1 } }),
    mutateAsync: (args: { legalEntityId?: string; visitId: string }) =>
      (t.mutateAsync as any)({ visitId: args.visitId, action: "arrive", request: { version: 1 } })
  };
};

export const useStartVisitMutation = (legalEntityId?: string) => {
  const t = useTransitionVisitMutation();
  return {
    ...t,
    mutate: (args: { legalEntityId?: string; visitId: string }) =>
      (t.mutate as any)({ visitId: args.visitId, action: "start", request: { version: 1 } }),
    mutateAsync: (args: { legalEntityId?: string; visitId: string }) =>
      (t.mutateAsync as any)({ visitId: args.visitId, action: "start", request: { version: 1 } })
  };
};

export const useCompleteVisitMutation = (legalEntityId?: string) => {
  const t = useTransitionVisitMutation();
  return {
    ...t,
    mutate: (args: { legalEntityId?: string; visitId: string }) =>
      (t.mutate as any)({ visitId: args.visitId, action: "complete", request: { version: 1 } }),
    mutateAsync: (args: { legalEntityId?: string; visitId: string }) =>
      (t.mutateAsync as any)({ visitId: args.visitId, action: "complete", request: { version: 1 } })
  };
};

export const useCancelVisitMutation = (legalEntityId?: string) => {
  const t = useTransitionVisitMutation();
  return {
    ...t,
    mutateAsync: (args: { legalEntityId?: string; visitId: string; reason?: string }) =>
      (t.mutateAsync as any)({ visitId: args.visitId, action: "cancel", request: { version: 1, reason: args.reason } })
  };
};

export const useAnswerChecklistItemMutation = (legalEntityId?: string) => {
  const s = useSaveChecklistAnswerMutation();
  return {
    ...s,
    mutateAsync: (args: any) =>
      (s.mutateAsync as any)({
        workOrderId: args.workOrderId,
        request: {
          workOrderId: args.workOrderId,
          questionKey: args.itemCode,
          questionType: "Measurement",
          answerJson: JSON.stringify({ passed: args.passed, value: args.value, uom: args.uom, notes: args.notes }),
          required: true
        }
      })
  };
};

export const useReservePartMutation = (legalEntityId?: string) => {
  const p = useApplyPartActionMutation();
  return {
    ...p,
    mutateAsync: (args: any) =>
      (p.mutateAsync as any)({
        partId: args.partId,
        action: "reserve",
        request: {
          quantity: Number(args.quantity) || 1,
          inventoryReference: args.warehouseLocation || args.inventoryReference,
          legalEntityId: args.legalEntityId
        }
      })
  };
};

export const useIssuePartMutation = (legalEntityId?: string) => {
  const p = useApplyPartActionMutation();
  return {
    ...p,
    mutateAsync: (args: any) =>
      (p.mutateAsync as any)({
        partId: args.partId,
        action: "issue",
        request: {
          quantity: Number(args.quantity) || 1,
          inventoryReference: args.warehouseLocation || args.inventoryReference,
          legalEntityId: args.legalEntityId
        }
      })
  };
};

export const useConsumePartMutation = (legalEntityId?: string) => {
  const p = useApplyPartActionMutation();
  return {
    ...p,
    mutateAsync: (args: any) =>
      (p.mutateAsync as any)({
        partId: args.partId,
        action: "consume",
        request: {
          quantity: Number(args.quantity) || 1,
          inventoryReference: args.inventoryReference,
          legalEntityId: args.legalEntityId
        }
      })
  };
};

export const useReturnPartMutation = (legalEntityId?: string) => {
  const p = useApplyPartActionMutation();
  return {
    ...p,
    mutateAsync: (args: any) =>
      (p.mutateAsync as any)({
        partId: args.partId,
        action: "return",
        request: {
          quantity: Number(args.quantity) || 1,
          inventoryReference: args.warehouseLocation || args.inventoryReference,
          legalEntityId: args.legalEntityId
        }
      })
  };
};

export const useCreateReportMutation = (legalEntityId?: string) => {
  const r = useSaveReportMutation();
  return {
    ...r,
    mutateAsync: (args: any) =>
      (r.mutateAsync as any)({
        workOrderId: args.workOrderId,
        request: {
          contentJson: JSON.stringify({
            summary: args.summary,
            customerRepresentative: args.customerRepresentative,
            notes: args.customerNotes
          }),
          signedFileId: args.signatureCaptured ? "sig-captured" : undefined
        }
      })
  };
};

export const useRegisterInstalledBaseMutation = (_legalEntityId?: string) => {
  const c = useCreateInstalledBaseMutation();
  return {
    ...c,
    mutateAsync: (args: any) => {
      const isGuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
      const custId = args.customerPartyId ?? (isGuid.test(args.customerName) ? args.customerName : undefined);
      const customerPartyId = custId && isGuid.test(custId) ? custId : undefined;
      return c.mutateAsync({
        legalEntityId: args.legalEntityId,
        name: args.name || args.itemName || args.itemCode || "Equipment",
        serialNumber: args.serialNumber || undefined,
        modelNumber: args.modelNumber || args.itemCode || undefined,
        customerPartyId,
        siteName: args.siteLocation || args.siteName || undefined,
        installedDate: args.installedDate || undefined
      });
    }
  };
};

export const useGenerateMaintenanceDueMutation = (legalEntityId?: string) => {
  const g = useGenerateDueMutation();
  return {
    ...g,
    mutateAsync: (args: any) =>
      (g.mutateAsync as any)({
        fromDate: new Date().toISOString(),
        toDate: new Date(Date.now() + (args.horizonDays ?? 30) * 86400000).toISOString()
      })
  };
};

export const useConvertDueToWorkOrderMutation = (legalEntityId?: string) => {
  const c = useConvertDueMutation();
  return {
    ...c,
    mutateAsync: (args: any) =>
      (c.mutateAsync as any)({
        dueId: args.dueId,
        request: {
          legalEntityId: args.legalEntityId || "00000000-0000-0000-0000-000000000001",
          context: "CustomerFieldService",
          title: args.title || ""
        }
      })
  };
};

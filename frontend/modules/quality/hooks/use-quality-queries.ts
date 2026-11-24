import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { qualityApi } from "../services/quality-api.ts";
import type {
  CreateQualityPlanRequest,
  CreateQualityPlanVersionRequest,
  CreateQualityApplicabilityRequest,
  CreateCharacteristicRequest,
  CreateSpecificationRequest,
  CreatePlanCharacteristicRequest,
  CreateSamplingPlanRequest,
  CreateInspectionRequest,
  AddInspectionResultRequest,
  CompleteInspectionRequest,
  ReinspectionRequest,
  CreateRestrictionRequest,
  ReleaseRestrictionRequest,
  CreateDispositionRequest,
  ApproveDispositionRequest,
  CreateIssueRequest,
  CreateNcrRequest,
  CreateContainmentRequest,
  CreateCapaRequest,
  CreateCapaActionRequest,
  CompleteCapaActionRequest,
  CreateCapaRootCauseRequest,
  EffectivenessCheckRequest,
  CreateSupplierProfileRequest,
  CreateScarRequest,
  SupplierScarResponseRequest,
  SupplierScarReviewRequest,
  IssueCertificateRequest
} from "../types.ts";

export const QUALITY_QUERY_KEYS = {
  all: ["quality"] as const,
  plans: (legalEntityId: string, eventType?: string | null, pageSize?: number, cursor?: string | null) =>
    [...QUALITY_QUERY_KEYS.all, "plans", legalEntityId, eventType, pageSize, cursor] as const,
  characteristics: (legalEntityId: string, search?: string | null, pageSize?: number, cursor?: string | null) =>
    [...QUALITY_QUERY_KEYS.all, "characteristics", legalEntityId, search, pageSize, cursor] as const,
  samplingPlans: (legalEntityId: string, search?: string | null, pageSize?: number, cursor?: string | null) =>
    [...QUALITY_QUERY_KEYS.all, "samplingPlans", legalEntityId, search, pageSize, cursor] as const,
  samplingCalc: (samplingPlanVersionId: string, lotQuantity: number) =>
    [...QUALITY_QUERY_KEYS.all, "samplingCalc", samplingPlanVersionId, lotQuantity] as const,
  inspections: (legalEntityId: string, state?: string | null, eventType?: string | null, search?: string | null, pageSize?: number, cursor?: string | null) =>
    [...QUALITY_QUERY_KEYS.all, "inspections", legalEntityId, state, eventType, search, pageSize, cursor] as const,
  inspection: (inspectionId: string) =>
    [...QUALITY_QUERY_KEYS.all, "inspection", inspectionId] as const,
  restrictions: (legalEntityId: string, status?: string | null, restrictionType?: string | null, search?: string | null, pageSize?: number, cursor?: string | null) =>
    [...QUALITY_QUERY_KEYS.all, "restrictions", legalEntityId, status, restrictionType, search, pageSize, cursor] as const,
  restriction: (restrictionId: string) =>
    [...QUALITY_QUERY_KEYS.all, "restriction", restrictionId] as const,
  dispositions: (legalEntityId: string, state?: string | null, decision?: string | null, search?: string | null, pageSize?: number, cursor?: string | null) =>
    [...QUALITY_QUERY_KEYS.all, "dispositions", legalEntityId, state, decision, search, pageSize, cursor] as const,
  disposition: (dispositionId: string) =>
    [...QUALITY_QUERY_KEYS.all, "disposition", dispositionId] as const,
  issues: (legalEntityId: string, state?: string | null, severity?: string | null, search?: string | null, pageSize?: number, cursor?: string | null) =>
    [...QUALITY_QUERY_KEYS.all, "issues", legalEntityId, state, severity, search, pageSize, cursor] as const,
  issue: (issueId: string) =>
    [...QUALITY_QUERY_KEYS.all, "issue", issueId] as const,
  ncrs: (legalEntityId: string, state?: string | null, severity?: string | null, search?: string | null, pageSize?: number, cursor?: string | null) =>
    [...QUALITY_QUERY_KEYS.all, "ncrs", legalEntityId, state, severity, search, pageSize, cursor] as const,
  ncr: (ncrId: string) =>
    [...QUALITY_QUERY_KEYS.all, "ncr", ncrId] as const,
  capas: (legalEntityId: string, state?: string | null, overdueOnly?: boolean, search?: string | null, pageSize?: number, cursor?: string | null) =>
    [...QUALITY_QUERY_KEYS.all, "capas", legalEntityId, state, overdueOnly, search, pageSize, cursor] as const,
  capa: (capaId: string) =>
    [...QUALITY_QUERY_KEYS.all, "capa", capaId] as const,
  supplierProfiles: (legalEntityId: string, riskLevel?: string | null, status?: string | null, pageSize?: number, cursor?: string | null) =>
    [...QUALITY_QUERY_KEYS.all, "supplierProfiles", legalEntityId, riskLevel, status, pageSize, cursor] as const,
  scars: (legalEntityId: string, state?: string | null, overdueOnly?: boolean, search?: string | null, pageSize?: number, cursor?: string | null) =>
    [...QUALITY_QUERY_KEYS.all, "scars", legalEntityId, state, overdueOnly, search, pageSize, cursor] as const,
  scar: (scarId: string) =>
    [...QUALITY_QUERY_KEYS.all, "scar", scarId] as const,
  certificates: (legalEntityId: string, state?: string | null, certificateType?: string | null, search?: string | null, pageSize?: number, cursor?: string | null) =>
    [...QUALITY_QUERY_KEYS.all, "certificates", legalEntityId, state, certificateType, search, pageSize, cursor] as const,
  certificate: (certificateId: string) =>
    [...QUALITY_QUERY_KEYS.all, "certificate", certificateId] as const
};

// --------------------------------------------------------------------------
// 1. Queries
// --------------------------------------------------------------------------

export function useQualityPlans(legalEntityId: string, eventType?: string | null, pageSize?: number, cursor?: string | null) {
  return useQuery({
    queryKey: QUALITY_QUERY_KEYS.plans(legalEntityId, eventType, pageSize, cursor),
    queryFn: () => qualityApi.listPlans(legalEntityId, eventType, pageSize, cursor),
    enabled: Boolean(legalEntityId)
  });
}

export function useQualityCharacteristics(legalEntityId: string, search?: string | null, pageSize?: number, cursor?: string | null) {
  return useQuery({
    queryKey: QUALITY_QUERY_KEYS.characteristics(legalEntityId, search, pageSize, cursor),
    queryFn: () => qualityApi.listCharacteristics(legalEntityId, search, pageSize, cursor),
    enabled: Boolean(legalEntityId)
  });
}

export function useQualitySamplingPlans(legalEntityId: string, search?: string | null, pageSize?: number, cursor?: string | null) {
  return useQuery({
    queryKey: QUALITY_QUERY_KEYS.samplingPlans(legalEntityId, search, pageSize, cursor),
    queryFn: () => qualityApi.listSamplingPlans(legalEntityId, search, pageSize, cursor),
    enabled: Boolean(legalEntityId)
  });
}

export function useSamplingCalculation(samplingPlanVersionId?: string | null, lotQuantity?: number | null) {
  return useQuery({
    queryKey: QUALITY_QUERY_KEYS.samplingCalc(samplingPlanVersionId ?? "", lotQuantity ?? 0),
    queryFn: () => qualityApi.calculateSampling(samplingPlanVersionId!, lotQuantity!),
    enabled: Boolean(samplingPlanVersionId && lotQuantity && lotQuantity > 0)
  });
}

export function useQualityInspections(legalEntityId: string, state?: string | null, eventType?: string | null, search?: string | null, pageSize?: number, cursor?: string | null) {
  return useQuery({
    queryKey: QUALITY_QUERY_KEYS.inspections(legalEntityId, state, eventType, search, pageSize, cursor),
    queryFn: () => qualityApi.listInspections(legalEntityId, state, eventType, search, pageSize, cursor),
    enabled: Boolean(legalEntityId)
  });
}

export function useQualityInspection(inspectionId?: string | null) {
  return useQuery({
    queryKey: QUALITY_QUERY_KEYS.inspection(inspectionId ?? ""),
    queryFn: () => qualityApi.getInspection(inspectionId!),
    enabled: Boolean(inspectionId)
  });
}

export function useQualityRestrictions(legalEntityId: string, status?: string | null, restrictionType?: string | null, search?: string | null, pageSize?: number, cursor?: string | null) {
  return useQuery({
    queryKey: QUALITY_QUERY_KEYS.restrictions(legalEntityId, status, restrictionType, search, pageSize, cursor),
    queryFn: () => qualityApi.listRestrictions(legalEntityId, status, restrictionType, search, pageSize, cursor),
    enabled: Boolean(legalEntityId)
  });
}

export function useQualityRestriction(restrictionId?: string | null) {
  return useQuery({
    queryKey: QUALITY_QUERY_KEYS.restriction(restrictionId ?? ""),
    queryFn: () => qualityApi.getRestriction(restrictionId!),
    enabled: Boolean(restrictionId)
  });
}

export function useQualityDispositions(legalEntityId: string, state?: string | null, decision?: string | null, search?: string | null, pageSize?: number, cursor?: string | null) {
  return useQuery({
    queryKey: QUALITY_QUERY_KEYS.dispositions(legalEntityId, state, decision, search, pageSize, cursor),
    queryFn: () => qualityApi.listDispositions(legalEntityId, state, decision, search, pageSize, cursor),
    enabled: Boolean(legalEntityId)
  });
}

export function useQualityDisposition(dispositionId?: string | null) {
  return useQuery({
    queryKey: QUALITY_QUERY_KEYS.disposition(dispositionId ?? ""),
    queryFn: () => qualityApi.getDisposition(dispositionId!),
    enabled: Boolean(dispositionId)
  });
}

export function useQualityIssues(legalEntityId: string, state?: string | null, severity?: string | null, search?: string | null, pageSize?: number, cursor?: string | null) {
  return useQuery({
    queryKey: QUALITY_QUERY_KEYS.issues(legalEntityId, state, severity, search, pageSize, cursor),
    queryFn: () => qualityApi.listIssues(legalEntityId, state, severity, search, pageSize, cursor),
    enabled: Boolean(legalEntityId)
  });
}

export function useQualityIssue(issueId?: string | null) {
  return useQuery({
    queryKey: QUALITY_QUERY_KEYS.issue(issueId ?? ""),
    queryFn: () => qualityApi.getIssue(issueId!),
    enabled: Boolean(issueId)
  });
}

export function useQualityNcrs(legalEntityId: string, state?: string | null, severity?: string | null, search?: string | null, pageSize?: number, cursor?: string | null) {
  return useQuery({
    queryKey: QUALITY_QUERY_KEYS.ncrs(legalEntityId, state, severity, search, pageSize, cursor),
    queryFn: () => qualityApi.listNcrs(legalEntityId, state, severity, search, pageSize, cursor),
    enabled: Boolean(legalEntityId)
  });
}

export function useQualityNcr(ncrId?: string | null) {
  return useQuery({
    queryKey: QUALITY_QUERY_KEYS.ncr(ncrId ?? ""),
    queryFn: () => qualityApi.getNcr(ncrId!),
    enabled: Boolean(ncrId)
  });
}

export function useQualityCapas(legalEntityId: string, state?: string | null, overdueOnly?: boolean, search?: string | null, pageSize?: number, cursor?: string | null) {
  return useQuery({
    queryKey: QUALITY_QUERY_KEYS.capas(legalEntityId, state, overdueOnly, search, pageSize, cursor),
    queryFn: () => qualityApi.listCapas(legalEntityId, state, overdueOnly, search, pageSize, cursor),
    enabled: Boolean(legalEntityId)
  });
}

export function useQualityCapa(capaId?: string | null) {
  return useQuery({
    queryKey: QUALITY_QUERY_KEYS.capa(capaId ?? ""),
    queryFn: () => qualityApi.getCapa(capaId!),
    enabled: Boolean(capaId)
  });
}

export function useQualitySupplierProfiles(legalEntityId: string, riskLevel?: string | null, status?: string | null, pageSize?: number, cursor?: string | null) {
  return useQuery({
    queryKey: QUALITY_QUERY_KEYS.supplierProfiles(legalEntityId, riskLevel, status, pageSize, cursor),
    queryFn: () => qualityApi.listSupplierProfiles(legalEntityId, riskLevel, status, pageSize, cursor),
    enabled: Boolean(legalEntityId)
  });
}

export function useQualityScars(legalEntityId: string, state?: string | null, overdueOnly?: boolean, search?: string | null, pageSize?: number, cursor?: string | null) {
  return useQuery({
    queryKey: QUALITY_QUERY_KEYS.scars(legalEntityId, state, overdueOnly, search, pageSize, cursor),
    queryFn: () => qualityApi.listScars(legalEntityId, state, overdueOnly, search, pageSize, cursor),
    enabled: Boolean(legalEntityId)
  });
}

export function useQualityScar(scarId?: string | null) {
  return useQuery({
    queryKey: QUALITY_QUERY_KEYS.scar(scarId ?? ""),
    queryFn: () => qualityApi.getScar(scarId!),
    enabled: Boolean(scarId)
  });
}

export function useQualityCertificates(legalEntityId: string, state?: string | null, certificateType?: string | null, search?: string | null, pageSize?: number, cursor?: string | null) {
  return useQuery({
    queryKey: QUALITY_QUERY_KEYS.certificates(legalEntityId, state, certificateType, search, pageSize, cursor),
    queryFn: () => qualityApi.listCertificates(legalEntityId, state, certificateType, search, pageSize, cursor),
    enabled: Boolean(legalEntityId)
  });
}

export function useQualityCertificate(certificateId?: string | null) {
  return useQuery({
    queryKey: QUALITY_QUERY_KEYS.certificate(certificateId ?? ""),
    queryFn: () => qualityApi.getCertificate(certificateId!),
    enabled: Boolean(certificateId)
  });
}

// --------------------------------------------------------------------------
// 2. Mutations
// --------------------------------------------------------------------------

export function useCreateQualityPlan() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateQualityPlanRequest) => qualityApi.createPlan(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [...QUALITY_QUERY_KEYS.all, "plans"] });
    }
  });
}

export function useCreateQualityPlanVersion() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ planId, request }: { planId: string; request: CreateQualityPlanVersionRequest }) =>
      qualityApi.createPlanVersion(planId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [...QUALITY_QUERY_KEYS.all, "plans"] });
    }
  });
}

export function useActivateQualityPlanVersion() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (versionId: string) => qualityApi.activatePlanVersion(versionId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [...QUALITY_QUERY_KEYS.all, "plans"] });
    }
  });
}

export function useCreateQualityApplicability() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ planId, request }: { planId: string; request: CreateQualityApplicabilityRequest }) =>
      qualityApi.createApplicability(planId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [...QUALITY_QUERY_KEYS.all, "plans"] });
    }
  });
}

export function useCreateCharacteristic() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateCharacteristicRequest) => qualityApi.createCharacteristic(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [...QUALITY_QUERY_KEYS.all, "characteristics"] });
    }
  });
}

export function useCreateSpecification() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateSpecificationRequest) => qualityApi.createSpecification(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [...QUALITY_QUERY_KEYS.all, "plans"] });
    }
  });
}

export function useAddPlanCharacteristic() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ versionId, request }: { versionId: string; request: CreatePlanCharacteristicRequest }) =>
      qualityApi.addPlanCharacteristic(versionId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [...QUALITY_QUERY_KEYS.all, "plans"] });
    }
  });
}

export function useCreateSamplingPlan() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateSamplingPlanRequest) => qualityApi.createSamplingPlan(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [...QUALITY_QUERY_KEYS.all, "samplingPlans"] });
    }
  });
}

export function useCreateInspection() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateInspectionRequest) => qualityApi.createInspection(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [...QUALITY_QUERY_KEYS.all, "inspections"] });
    }
  });
}

export function useAddInspectionResult() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ inspectionId, request }: { inspectionId: string; request: AddInspectionResultRequest }) =>
      qualityApi.addResult(inspectionId, request),
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: QUALITY_QUERY_KEYS.inspection(variables.inspectionId) });
      queryClient.invalidateQueries({ queryKey: [...QUALITY_QUERY_KEYS.all, "inspections"] });
    }
  });
}

export function useCompleteInspection() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ inspectionId, request }: { inspectionId: string; request: CompleteInspectionRequest }) =>
      qualityApi.completeInspection(inspectionId, request),
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: QUALITY_QUERY_KEYS.inspection(variables.inspectionId) });
      queryClient.invalidateQueries({ queryKey: [...QUALITY_QUERY_KEYS.all, "inspections"] });
    }
  });
}

export function useReinspect() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ inspectionId, request }: { inspectionId: string; request: ReinspectionRequest }) =>
      qualityApi.reinspect(inspectionId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [...QUALITY_QUERY_KEYS.all, "inspections"] });
    }
  });
}

export function useCreateRestrictionForInspection() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ inspectionId, legalEntityId, request }: { inspectionId: string; legalEntityId: string; request: CreateRestrictionRequest }) =>
      qualityApi.createRestrictionForInspection(inspectionId, legalEntityId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [...QUALITY_QUERY_KEYS.all, "restrictions"] });
      queryClient.invalidateQueries({ queryKey: [...QUALITY_QUERY_KEYS.all, "inspections"] });
    }
  });
}

export function useReleaseRestriction() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ restrictionId, request }: { restrictionId: string; request: ReleaseRestrictionRequest }) =>
      qualityApi.releaseRestriction(restrictionId, request),
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: QUALITY_QUERY_KEYS.restriction(variables.restrictionId) });
      queryClient.invalidateQueries({ queryKey: [...QUALITY_QUERY_KEYS.all, "restrictions"] });
    }
  });
}

export function useCreateDisposition() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ legalEntityId, request }: { legalEntityId: string; request: CreateDispositionRequest }) =>
      qualityApi.createDisposition(legalEntityId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [...QUALITY_QUERY_KEYS.all, "dispositions"] });
    }
  });
}

export function useApproveDisposition() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ dispositionId, request }: { dispositionId: string; request: ApproveDispositionRequest }) =>
      qualityApi.approveDisposition(dispositionId, request),
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: QUALITY_QUERY_KEYS.disposition(variables.dispositionId) });
      queryClient.invalidateQueries({ queryKey: [...QUALITY_QUERY_KEYS.all, "dispositions"] });
    }
  });
}

export function useExecuteDisposition() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (dispositionId: string) => qualityApi.executeDisposition(dispositionId),
    onSuccess: (_data, dispositionId) => {
      queryClient.invalidateQueries({ queryKey: QUALITY_QUERY_KEYS.disposition(dispositionId) });
      queryClient.invalidateQueries({ queryKey: [...QUALITY_QUERY_KEYS.all, "dispositions"] });
      queryClient.invalidateQueries({ queryKey: [...QUALITY_QUERY_KEYS.all, "restrictions"] });
    }
  });
}

export function useReconcileDisposition() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (dispositionId: string) => qualityApi.reconcileDisposition(dispositionId),
    onSuccess: (_data, dispositionId) => {
      queryClient.invalidateQueries({ queryKey: QUALITY_QUERY_KEYS.disposition(dispositionId) });
      queryClient.invalidateQueries({ queryKey: [...QUALITY_QUERY_KEYS.all, "dispositions"] });
      queryClient.invalidateQueries({ queryKey: [...QUALITY_QUERY_KEYS.all, "restrictions"] });
    }
  });
}

export function useCreateIssue() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ legalEntityId, request }: { legalEntityId: string; request: CreateIssueRequest }) =>
      qualityApi.createIssue(legalEntityId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [...QUALITY_QUERY_KEYS.all, "issues"] });
    }
  });
}

export function useCreateNcr() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ legalEntityId, request }: { legalEntityId: string; request: CreateNcrRequest }) =>
      qualityApi.createNcr(legalEntityId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [...QUALITY_QUERY_KEYS.all, "ncrs"] });
    }
  });
}

export function useAddContainment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ ncrId, request }: { ncrId: string; request: CreateContainmentRequest }) =>
      qualityApi.addContainment(ncrId, request),
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: QUALITY_QUERY_KEYS.ncr(variables.ncrId) });
      queryClient.invalidateQueries({ queryKey: [...QUALITY_QUERY_KEYS.all, "ncrs"] });
    }
  });
}

export function useCreateCapa() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ legalEntityId, request }: { legalEntityId: string; request: CreateCapaRequest }) =>
      qualityApi.createCapa(legalEntityId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [...QUALITY_QUERY_KEYS.all, "capas"] });
    }
  });
}

export function useAddCapaActionItem() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ capaId, request }: { capaId: string; request: CreateCapaActionRequest }) =>
      qualityApi.addCapaAction(capaId, request),
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: QUALITY_QUERY_KEYS.capa(variables.capaId) });
      queryClient.invalidateQueries({ queryKey: [...QUALITY_QUERY_KEYS.all, "capas"] });
    }
  });
}

export function useCompleteCapaActionItem() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ actionId, request }: { actionId: string; request: CompleteCapaActionRequest }) =>
      qualityApi.completeCapaAction(actionId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [...QUALITY_QUERY_KEYS.all, "capas"] });
    }
  });
}

export function useAddCapaRootCause() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ capaId, request }: { capaId: string; request: CreateCapaRootCauseRequest }) =>
      qualityApi.addCapaRootCause(capaId, request),
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: QUALITY_QUERY_KEYS.capa(variables.capaId) });
      queryClient.invalidateQueries({ queryKey: [...QUALITY_QUERY_KEYS.all, "capas"] });
    }
  });
}

export function useRecordEffectiveness() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ capaId, request }: { capaId: string; request: EffectivenessCheckRequest }) =>
      qualityApi.recordEffectiveness(capaId, request),
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: QUALITY_QUERY_KEYS.capa(variables.capaId) });
      queryClient.invalidateQueries({ queryKey: [...QUALITY_QUERY_KEYS.all, "capas"] });
    }
  });
}

export function useSaveSupplierProfile() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateSupplierProfileRequest) => qualityApi.saveSupplierProfile(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [...QUALITY_QUERY_KEYS.all, "supplierProfiles"] });
    }
  });
}

export function useCreateScar() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateScarRequest) => qualityApi.createScar(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [...QUALITY_QUERY_KEYS.all, "scars"] });
    }
  });
}

export function useSubmitScarResponse() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ scarId, request }: { scarId: string; request: SupplierScarResponseRequest }) =>
      qualityApi.submitScarResponse(scarId, request),
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: QUALITY_QUERY_KEYS.scar(variables.scarId) });
      queryClient.invalidateQueries({ queryKey: [...QUALITY_QUERY_KEYS.all, "scars"] });
    }
  });
}

export function useReviewScar() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ scarId, request }: { scarId: string; request: SupplierScarReviewRequest }) =>
      qualityApi.reviewScar(scarId, request),
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: QUALITY_QUERY_KEYS.scar(variables.scarId) });
      queryClient.invalidateQueries({ queryKey: [...QUALITY_QUERY_KEYS.all, "scars"] });
    }
  });
}

export function useIssueCertificate() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ legalEntityId, request }: { legalEntityId: string; request: IssueCertificateRequest }) =>
      qualityApi.issueCertificate(legalEntityId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [...QUALITY_QUERY_KEYS.all, "certificates"] });
    }
  });
}

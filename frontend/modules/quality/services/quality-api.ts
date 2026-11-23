import { httpClient } from "../../../core/api/http-client.ts";
import type {
  QualityPage,
  QualityPlanListItem,
  CreateQualityPlanRequest,
  CreateQualityPlanVersionRequest,
  CreateQualityApplicabilityRequest,
  QualityCharacteristicListItem,
  CreateCharacteristicRequest,
  CreateSpecificationRequest,
  CreatePlanCharacteristicRequest,
  QualitySamplingPlanListItem,
  CreateSamplingPlanRequest,
  SamplingCalculationView,
  InspectionListItem,
  InspectionView,
  InspectionResultView,
  CreateInspectionRequest,
  AddInspectionResultRequest,
  CompleteInspectionRequest,
  ReinspectionRequest,
  QualityRestrictionListItem,
  QualityRestrictionView,
  CreateRestrictionRequest,
  ReleaseRestrictionRequest,
  QualityDispositionListItem,
  QualityDispositionView,
  CreateDispositionRequest,
  ApproveDispositionRequest,
  QualityIssueListItem,
  QualityIssueView,
  CreateIssueRequest,
  QualityNcrListItem,
  QualityNcrView,
  CreateNcrRequest,
  CreateContainmentRequest,
  QualityCapaListItem,
  CapaView,
  CapaActionView,
  CreateCapaRequest,
  CreateCapaActionRequest,
  CompleteCapaActionRequest,
  CreateCapaRootCauseRequest,
  EffectivenessCheckRequest,
  QualitySupplierProfileListItem,
  CreateSupplierProfileRequest,
  QualityScarListItem,
  QualityScarView,
  CreateScarRequest,
  SupplierScarResponseRequest,
  SupplierScarReviewRequest,
  QualityCertificateListItem,
  QualityCertificateView,
  IssueCertificateRequest
} from "../types.ts";

const BASE_URL = "/api/v1/quality";

function toQueryString(params: Record<string, string | number | boolean | null | undefined>): string {
  const searchParams = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== null && value !== "") {
      searchParams.set(key, String(value));
    }
  }
  const str = searchParams.toString();
  return str ? `?${str}` : "";
}

export const qualityApi = {
  // --------------------------------------------------------------------------
  // 1. Quality Plans & Specifications
  // --------------------------------------------------------------------------

  listPlans(legalEntityId: string, eventType?: string | null, pageSize?: number, cursor?: string | null) {
    const qs = toQueryString({ legalEntityId, eventType, pageSize, cursor });
    return httpClient<QualityPage<QualityPlanListItem>>(`${BASE_URL}/plans${qs}`, { legalEntityId });
  },

  createPlan(request: CreateQualityPlanRequest) {
    return httpClient<QualityPlanListItem>(`${BASE_URL}/plans`, {
      method: "POST",
      body: JSON.stringify(request),
      legalEntityId: request.legalEntityId
    });
  },

  createPlanVersion(planId: string, request: CreateQualityPlanVersionRequest) {
    return httpClient<{ id: string; versionNo: number }>(`${BASE_URL}/plans/${planId}/versions`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  activatePlanVersion(versionId: string) {
    return httpClient<{ versionId: string; status: string }>(`${BASE_URL}/plan-versions/${versionId}/activate`, {
      method: "POST"
    });
  },

  createApplicability(planId: string, request: CreateQualityApplicabilityRequest) {
    return httpClient<{ id: string }>(`${BASE_URL}/plans/${planId}/applicability`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  createCharacteristic(request: CreateCharacteristicRequest) {
    return httpClient<QualityCharacteristicListItem>(`${BASE_URL}/characteristics`, {
      method: "POST",
      body: JSON.stringify(request),
      legalEntityId: request.legalEntityId
    });
  },

  listCharacteristics(legalEntityId: string, search?: string | null, pageSize?: number, cursor?: string | null) {
    const qs = toQueryString({ legalEntityId, search, pageSize, cursor });
    return httpClient<QualityPage<QualityCharacteristicListItem>>(`${BASE_URL}/characteristics${qs}`, { legalEntityId });
  },

  createSpecification(request: CreateSpecificationRequest) {
    return httpClient<{ id: string }>(`${BASE_URL}/specifications`, {
      method: "POST",
      body: JSON.stringify(request),
      legalEntityId: request.legalEntityId
    });
  },

  addPlanCharacteristic(versionId: string, request: CreatePlanCharacteristicRequest) {
    return httpClient<{ id: string }>(`${BASE_URL}/plan-versions/${versionId}/characteristics`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  createSamplingPlan(request: CreateSamplingPlanRequest) {
    return httpClient<QualitySamplingPlanListItem>(`${BASE_URL}/sampling-plans`, {
      method: "POST",
      body: JSON.stringify(request),
      legalEntityId: request.legalEntityId
    });
  },

  listSamplingPlans(legalEntityId: string, search?: string | null, pageSize?: number, cursor?: string | null) {
    const qs = toQueryString({ legalEntityId, search, pageSize, cursor });
    return httpClient<QualityPage<QualitySamplingPlanListItem>>(`${BASE_URL}/sampling-plans${qs}`, { legalEntityId });
  },

  calculateSampling(samplingPlanVersionId: string, lotQuantity: number) {
    const qs = toQueryString({ lotQuantity });
    return httpClient<SamplingCalculationView>(`${BASE_URL}/sampling/${samplingPlanVersionId}/calculate${qs}`);
  },

  // --------------------------------------------------------------------------
  // 2. Inspections & Execution
  // --------------------------------------------------------------------------

  listInspections(legalEntityId: string, state?: string | null, eventType?: string | null, pageSize?: number, cursor?: string | null) {
    const qs = toQueryString({ legalEntityId, state, eventType, pageSize, cursor });
    return httpClient<QualityPage<InspectionListItem>>(`${BASE_URL}/inspections${qs}`, { legalEntityId });
  },

  getInspection(inspectionId: string) {
    return httpClient<InspectionView>(`${BASE_URL}/inspections/${inspectionId}`);
  },

  createInspection(request: CreateInspectionRequest) {
    return httpClient<InspectionView>(`${BASE_URL}/inspections`, {
      method: "POST",
      body: JSON.stringify(request),
      legalEntityId: request.legalEntityId
    });
  },

  addResult(inspectionId: string, request: AddInspectionResultRequest) {
    return httpClient<InspectionResultView>(`${BASE_URL}/inspections/${inspectionId}/results`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  completeInspection(inspectionId: string, request: CompleteInspectionRequest) {
    return httpClient<InspectionView>(`${BASE_URL}/inspections/${inspectionId}/complete`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  reinspect(inspectionId: string, request: ReinspectionRequest) {
    return httpClient<InspectionView>(`${BASE_URL}/inspections/${inspectionId}/reinspect`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  createRestrictionForInspection(inspectionId: string, legalEntityId: string, request: CreateRestrictionRequest) {
    const qs = toQueryString({ legalEntityId });
    return httpClient<QualityRestrictionView>(`${BASE_URL}/inspections/${inspectionId}/restrictions${qs}`, {
      method: "POST",
      body: JSON.stringify(request),
      legalEntityId
    });
  },

  // --------------------------------------------------------------------------
  // 3. Restrictions & Holds
  // --------------------------------------------------------------------------

  listRestrictions(legalEntityId: string, status?: string | null, restrictionType?: string | null, search?: string | null, pageSize?: number, cursor?: string | null) {
    const qs = toQueryString({ legalEntityId, status, restrictionType, search, pageSize, cursor });
    return httpClient<QualityPage<QualityRestrictionListItem>>(`${BASE_URL}/restrictions${qs}`, { legalEntityId });
  },

  getRestriction(restrictionId: string) {
    return httpClient<QualityRestrictionView>(`${BASE_URL}/restrictions/${restrictionId}`);
  },

  releaseRestriction(restrictionId: string, request: ReleaseRestrictionRequest) {
    return httpClient<QualityRestrictionView>(`${BASE_URL}/restrictions/${restrictionId}/release`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  // --------------------------------------------------------------------------
  // 4. Dispositions
  // --------------------------------------------------------------------------

  listDispositions(legalEntityId: string, state?: string | null, decision?: string | null, search?: string | null, pageSize?: number, cursor?: string | null) {
    const qs = toQueryString({ legalEntityId, state, decision, search, pageSize, cursor });
    return httpClient<QualityPage<QualityDispositionListItem>>(`${BASE_URL}/dispositions${qs}`, { legalEntityId });
  },

  getDisposition(dispositionId: string) {
    return httpClient<QualityDispositionView>(`${BASE_URL}/dispositions/${dispositionId}`);
  },

  createDisposition(legalEntityId: string, request: CreateDispositionRequest) {
    const qs = toQueryString({ legalEntityId });
    return httpClient<QualityDispositionView>(`${BASE_URL}/dispositions${qs}`, {
      method: "POST",
      body: JSON.stringify(request),
      legalEntityId
    });
  },

  approveDisposition(dispositionId: string, request: ApproveDispositionRequest) {
    return httpClient<QualityDispositionView>(`${BASE_URL}/dispositions/${dispositionId}/approve`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  executeDisposition(dispositionId: string) {
    return httpClient<QualityDispositionView>(`${BASE_URL}/dispositions/${dispositionId}/execute`, {
      method: "POST"
    });
  },

  reconcileDisposition(dispositionId: string) {
    return httpClient<QualityDispositionView>(`${BASE_URL}/dispositions/${dispositionId}/reconcile`, {
      method: "POST"
    });
  },

  // --------------------------------------------------------------------------
  // 5. Issues & Nonconformance (NCR)
  // --------------------------------------------------------------------------

  listIssues(legalEntityId: string, state?: string | null, severity?: string | null, search?: string | null, pageSize?: number, cursor?: string | null) {
    const qs = toQueryString({ legalEntityId, state, severity, search, pageSize, cursor });
    return httpClient<QualityPage<QualityIssueListItem>>(`${BASE_URL}/issues${qs}`, { legalEntityId });
  },

  getIssue(issueId: string) {
    return httpClient<QualityIssueView>(`${BASE_URL}/issues/${issueId}`);
  },

  createIssue(legalEntityId: string, request: CreateIssueRequest) {
    const qs = toQueryString({ legalEntityId });
    return httpClient<QualityIssueView>(`${BASE_URL}/issues${qs}`, {
      method: "POST",
      body: JSON.stringify(request),
      legalEntityId
    });
  },

  listNcrs(legalEntityId: string, state?: string | null, severity?: string | null, search?: string | null, pageSize?: number, cursor?: string | null) {
    const qs = toQueryString({ legalEntityId, state, severity, search, pageSize, cursor });
    return httpClient<QualityPage<QualityNcrListItem>>(`${BASE_URL}/ncrs${qs}`, { legalEntityId });
  },

  getNcr(ncrId: string) {
    return httpClient<QualityNcrView>(`${BASE_URL}/ncrs/${ncrId}`);
  },

  createNcr(legalEntityId: string, request: CreateNcrRequest) {
    const qs = toQueryString({ legalEntityId });
    return httpClient<QualityNcrView>(`${BASE_URL}/ncrs${qs}`, {
      method: "POST",
      body: JSON.stringify(request),
      legalEntityId
    });
  },

  addContainment(ncrId: string, request: CreateContainmentRequest) {
    return httpClient<{ id: string }>(`${BASE_URL}/ncrs/${ncrId}/containment`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  // --------------------------------------------------------------------------
  // 6. CAPA
  // --------------------------------------------------------------------------

  listCapas(legalEntityId: string, state?: string | null, overdueOnly?: boolean, search?: string | null, pageSize?: number, cursor?: string | null) {
    const qs = toQueryString({ legalEntityId, state, overdueOnly, search, pageSize, cursor });
    return httpClient<QualityPage<QualityCapaListItem>>(`${BASE_URL}/capas${qs}`, { legalEntityId });
  },

  getCapa(capaId: string) {
    return httpClient<CapaView>(`${BASE_URL}/capas/${capaId}`);
  },

  createCapa(legalEntityId: string, request: CreateCapaRequest) {
    const qs = toQueryString({ legalEntityId });
    return httpClient<CapaView>(`${BASE_URL}/capas${qs}`, {
      method: "POST",
      body: JSON.stringify(request),
      legalEntityId
    });
  },

  addCapaAction(capaId: string, request: CreateCapaActionRequest) {
    return httpClient<CapaActionView>(`${BASE_URL}/capas/${capaId}/actions`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  completeCapaAction(actionId: string, request: CompleteCapaActionRequest) {
    return httpClient<CapaActionView>(`${BASE_URL}/capa-actions/${actionId}/complete`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  addCapaRootCause(capaId: string, request: CreateCapaRootCauseRequest) {
    return httpClient<{ id: string }>(`${BASE_URL}/capas/${capaId}/root-causes`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  recordEffectiveness(capaId: string, request: EffectivenessCheckRequest) {
    return httpClient<{ id: string }>(`${BASE_URL}/capas/${capaId}/effectiveness`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  // --------------------------------------------------------------------------
  // 7. Supplier Quality & SCAR
  // --------------------------------------------------------------------------

  listSupplierProfiles(legalEntityId: string, riskLevel?: string | null, status?: string | null, pageSize?: number, cursor?: string | null) {
    const qs = toQueryString({ legalEntityId, riskLevel, status, pageSize, cursor });
    return httpClient<QualityPage<QualitySupplierProfileListItem>>(`${BASE_URL}/supplier-profiles${qs}`, { legalEntityId });
  },

  saveSupplierProfile(request: CreateSupplierProfileRequest) {
    return httpClient<QualitySupplierProfileListItem>(`${BASE_URL}/supplier-profiles`, {
      method: "PUT",
      body: JSON.stringify(request),
      legalEntityId: request.legalEntityId
    });
  },

  listScars(legalEntityId: string, state?: string | null, overdueOnly?: boolean, search?: string | null, pageSize?: number, cursor?: string | null) {
    const qs = toQueryString({ legalEntityId, state, overdueOnly, search, pageSize, cursor });
    return httpClient<QualityPage<QualityScarListItem>>(`${BASE_URL}/scars${qs}`, { legalEntityId });
  },

  getScar(scarId: string) {
    return httpClient<QualityScarView>(`${BASE_URL}/scars/${scarId}`);
  },

  createScar(request: CreateScarRequest) {
    return httpClient<QualityScarView>(`${BASE_URL}/scars`, {
      method: "POST",
      body: JSON.stringify(request),
      legalEntityId: request.legalEntityId
    });
  },

  submitScarResponse(scarId: string, request: SupplierScarResponseRequest) {
    return httpClient<QualityScarView>(`${BASE_URL}/scars/${scarId}/response`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  reviewScar(scarId: string, request: SupplierScarReviewRequest) {
    return httpClient<QualityScarView>(`${BASE_URL}/scars/${scarId}/review`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  // --------------------------------------------------------------------------
  // 8. Certificates (COA / CoC)
  // --------------------------------------------------------------------------

  listCertificates(legalEntityId: string, state?: string | null, certificateType?: string | null, search?: string | null, pageSize?: number, cursor?: string | null) {
    const qs = toQueryString({ legalEntityId, state, certificateType, search, pageSize, cursor });
    return httpClient<QualityPage<QualityCertificateListItem>>(`${BASE_URL}/certificates${qs}`, { legalEntityId });
  },

  getCertificate(certificateId: string) {
    return httpClient<QualityCertificateView>(`${BASE_URL}/certificates/${certificateId}`);
  },

  issueCertificate(legalEntityId: string, request: IssueCertificateRequest) {
    const qs = toQueryString({ legalEntityId });
    return httpClient<QualityCertificateView>(`${BASE_URL}/certificates${qs}`, {
      method: "POST",
      body: JSON.stringify(request),
      legalEntityId
    });
  }
};

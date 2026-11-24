/**
 * Module 27 Quality Management — Authoritative Frontend TypeScript Types
 * Mirrors backend/Modules/Quality/DTOs/QualityContracts.cs strictly.
 */

export interface QualityPage<T> {
  items: T[];
  nextCursor: string | null;
}

// --------------------------------------------------------------------------
// 1. Quality Plans & Specifications
// --------------------------------------------------------------------------

export interface QualityPlanListItem {
  id: string;
  code: string;
  name: string;
  eventType: string;
  mandatory: boolean;
  status: string;
  rowVersion: number;
}

export interface CreateQualityPlanRequest {
  legalEntityId: string;
  code: string;
  name: string;
  eventType?: string;
  mandatory?: boolean;
}

export interface CreateQualityPlanVersionRequest {
  effectiveFrom: string; // YYYY-MM-DD
  effectiveTo?: string | null;
  notes?: string | null;
}

export interface CreateQualityApplicabilityRequest {
  productVariantId?: string | null;
  supplierPartyId?: string | null;
  plantId?: string | null;
  customerPartyId?: string | null;
  eventType?: string | null;
}

export interface QualityCharacteristicListItem {
  id: string;
  legalEntityId: string;
  code: string;
  name: string;
  dataType: string; // "Numeric" | "Text" | "Boolean"
  baseUomCode: string | null;
  required: boolean;
}

export interface CreateCharacteristicRequest {
  legalEntityId: string;
  code: string;
  name: string;
  dataType?: string;
  baseUomCode?: string | null;
  required?: boolean;
  allowedValuesJson?: string | null;
}

export interface CreateSpecificationRequest {
  legalEntityId: string;
  code: string;
  name: string;
  effectiveFrom: string;
  effectiveTo?: string | null;
}

export interface CreatePlanCharacteristicRequest {
  characteristicId: string;
  specificationVersionId: string;
  minimum?: number | null;
  maximum?: number | null;
  target?: string | null;
  allowedValuesJson?: string | null;
  required?: boolean;
  evidenceRequired?: boolean;
}

export interface QualitySamplingPlanListItem {
  id: string;
  legalEntityId: string;
  code: string;
  name: string;
  method: string; // "Full" | "Fixed" | "Percentage"
  fixedQuantity: number | null;
  percentage: number | null;
}

export interface CreateSamplingPlanRequest {
  legalEntityId: string;
  code: string;
  name: string;
  method?: string;
  fixedQuantity?: number | null;
  percentage?: number | null;
}

export interface SamplingCalculationView {
  method: string;
  lotQuantity: number;
  sampleSize: number;
  isFullInspection: boolean;
}

// --------------------------------------------------------------------------
// 2. Inspections & Execution
// --------------------------------------------------------------------------

export interface InspectionListItem {
  id: string;
  number: string;
  eventType: string;
  state: string; // "Draft" | "Pending" | "InProgress" | "Completed"
  decisionState: string; // "Pending" | "Passed" | "Failed" | "Concession"
  productVariantId: string | null;
  sourceDocumentId: string | null;
  lotQuantity: number;
  sampleSize: number;
  createdAt: string;
}

export interface InspectionResultView {
  id: string;
  characteristicId: string;
  numericValue: number | null;
  textValue: string | null;
  booleanValue: boolean | null;
  passed: boolean | null;
  uomCode: string | null;
  evidenceReference: string | null;
}

export interface InspectionCharacteristicSnapshotView {
  characteristicId: string;
  code: string;
  name: string;
  dataType: string;
  baseUomCode: string | null;
  minimum: number | null;
  maximum: number | null;
  target: string | null;
  allowedValuesJson: string | null;
  required: boolean;
  evidenceRequired: boolean;
}

export interface InspectionView {
  id: string;
  legalEntityId: string;
  number: string;
  eventType: string;
  state: string;
  decisionState: string;
  productVariantId: string | null;
  sourceDocumentId: string | null;
  productionOrderId: string | null;
  productionOperationId: string | null;
  supplierPartyId: string | null;
  lotQuantity: number;
  sampleSize: number;
  lotReference: string | null;
  results: InspectionResultView[];
  rowVersion: number;
  planCharacteristics?: InspectionCharacteristicSnapshotView[];
}

export interface CreateInspectionRequest {
  legalEntityId: string;
  eventType: string;
  productVariantId?: string | null;
  lotQuantity: number;
  sourceDocumentId?: string | null;
  productionOrderId?: string | null;
  productionOperationId?: string | null;
  supplierPartyId?: string | null;
  customerPartyId?: string | null;
  planVersionId?: string | null;
  samplingPlanVersionId?: string | null;
  lotReference?: string | null;
  sourceFingerprint?: string | null;
}

export interface AddInspectionResultRequest {
  characteristicId: string;
  numericValue?: number | null;
  textValue?: string | null;
  booleanValue?: boolean | null;
  uomCode?: string | null;
  evidenceReference?: string | null;
  sampleId?: string | null;
  instrumentReference?: string | null;
  calibrationStatus?: string | null;
  calibrationDueOn?: string | null;
}

export interface CompleteInspectionRequest {
  reviewNotes?: string | null;
}

export interface ReinspectionRequest {
  reason: string;
}

// --------------------------------------------------------------------------
// 3. Restrictions & Holds
// --------------------------------------------------------------------------

export interface QualityRestrictionListItem {
  id: string;
  legalEntityId: string;
  restrictionType: string; // "QualityHold" | "Quarantine" | "Blocked" | "ReturnsInspection"
  status: string; // "Active" | "Released"
  reasonCode: string;
  productVariantId: string | null;
  lotId: string | null;
  quantity: number | null;
  releasedQuantity: number;
  ownerExecutionStatus: string; // "NotRequired" | "Pending" | "Executing" | "Confirmed" | "Failed" | "Unknown"
  createdAt: string;
}

export interface QualityRestrictionView {
  id: string;
  restrictionType: string;
  status: string;
  reasonCode: string;
  productVariantId: string | null;
  lotId: string | null;
  quantity: number | null;
  inspectionId: string | null;
  createdAt: string;
  releasedAt: string | null;
  releasedQuantity: number;
  ownerExecutionStatus: string;
  ownerOperationId: string | null;
  releaseOperationId: string | null;
}

export interface CreateRestrictionRequest {
  restrictionType: string;
  reasonCode: string;
  productVariantId?: string | null;
  warehouseId?: string | null;
  locationId?: string | null;
  lotId?: string | null;
  serialId?: string | null;
  inspectionId?: string | null;
  quantity?: number | null;
  unitOfMeasureId?: string | null;
  operationalDate?: string | null;
  postingDate?: string | null;
}

export interface ReleaseRestrictionRequest {
  reason: string;
  quantity?: number | null;
  unitOfMeasureId?: string | null;
  operationalDate?: string | null;
  postingDate?: string | null;
  idempotencyKey?: string | null;
}

// --------------------------------------------------------------------------
// 4. Dispositions
// --------------------------------------------------------------------------

export interface QualityDispositionListItem {
  id: string;
  legalEntityId: string;
  inspectionId: string | null;
  decision: string; // "Accept" | "Reject" | "UseAsIs" | "Concession" | "Return" | "SupplierReturn" | "Scrap" | "Rework" | "Partial"
  state: string; // "Draft" | "Approved" | "Executing" | "Executed" | "Failed" | "Unknown"
  quantity: number | null;
  reason: string;
  ownerExecutionStatus: string;
  createdAt: string;
}

export interface QualityDispositionView {
  id: string;
  inspectionId: string | null;
  decision: string;
  state: string;
  quantity: number | null;
  reason: string;
  createdAt: string;
  ownerExecutionStatus: string;
  ownerOperationId: string | null;
}

export interface CreateDispositionRequest {
  decision: string;
  reason: string;
  inspectionId?: string | null;
  quantity?: number | null;
  externalReference?: string | null;
  unitOfMeasureId?: string | null;
  operationalDate?: string | null;
  postingDate?: string | null;
}

export interface ApproveDispositionRequest {
  notes?: string | null;
}

// --------------------------------------------------------------------------
// 5. Issues & Nonconformance (NCR)
// --------------------------------------------------------------------------

export interface QualityIssueListItem {
  id: string;
  legalEntityId: string;
  issueType: string;
  title: string;
  severity: string; // "Minor" | "Major" | "Critical"
  state: string; // "Open" | "UnderInvestigation" | "Resolved" | "Closed"
  inspectionId: string | null;
  createdAt: string;
}

export interface QualityIssueView {
  id: string;
  issueType: string;
  title: string;
  severity: string;
  state: string;
  inspectionId: string | null;
  supplierPartyId: string | null;
  createdAt: string;
}

export interface CreateIssueRequest {
  issueType: string;
  title: string;
  description: string;
  severity?: string;
  inspectionId?: string | null;
  supplierPartyId?: string | null;
}

export interface QualityNcrListItem {
  id: string;
  legalEntityId: string;
  number: string;
  title: string;
  severity: string;
  state: string;
  issueId: string | null;
  createdAt: string;
}

export interface QualityNcrView {
  id: string;
  number: string;
  legalEntityId: string;
  title: string;
  severity: string;
  state: string;
  issueId: string | null;
  createdAt: string;
}

export interface CreateNcrRequest {
  issueId?: string | null;
  title: string;
  description: string;
  severity?: string;
}

export interface CreateContainmentRequest {
  actionType: string;
  description: string;
  productVariantId?: string | null;
  lotId?: string | null;
  quantity?: number | null;
}

// --------------------------------------------------------------------------
// 6. CAPA
// --------------------------------------------------------------------------

export interface QualityCapaListItem {
  id: string;
  legalEntityId: string;
  number: string;
  type: string; // "Corrective" | "Preventive"
  title: string;
  state: string; // "Draft" | "Investigating" | "ActionPending" | "UnderReview" | "Closed" | "Ineffective"
  ncrId: string | null;
  openActionCount: number;
  createdAt: string;
}

export interface CapaView {
  id: string;
  number: string;
  type: string;
  title: string;
  state: string;
  ncrId: string | null;
  createdAt: string;
}

export interface CapaActionView {
  id: string;
  capaId: string;
  actionType: string;
  state: string;
}

export interface CreateCapaRequest {
  title: string;
  problemStatement: string;
  type?: string;
  ncrId?: string | null;
}

export interface CreateCapaActionRequest {
  actionType: string;
  description: string;
  ownerId: string;
  dueDate: string; // YYYY-MM-DD
}

export interface CreateCapaRootCauseRequest {
  category: string;
  description: string;
  verified?: boolean;
}

export interface CompleteCapaActionRequest {
  evidenceReference?: string | null;
}

export interface EffectivenessCheckRequest {
  effective: boolean;
  evidenceReference: string;
  notes: string;
}

// --------------------------------------------------------------------------
// 7. Supplier Quality & SCAR
// --------------------------------------------------------------------------

export interface QualitySupplierProfileListItem {
  id: string;
  legalEntityId: string;
  supplierPartyId: string;
  riskLevel: string; // "Low" | "Medium" | "High" | "Critical"
  targetPpm: number;
  receivingInspectionRequired: boolean;
  status: string;
  rowVersion: number;
}

export interface CreateSupplierProfileRequest {
  legalEntityId: string;
  supplierPartyId: string;
  riskLevel?: string;
  targetPpm?: number;
  receivingInspectionRequired?: boolean;
}

export interface QualityScarListItem {
  id: string;
  legalEntityId: string;
  number: string;
  supplierPartyId: string;
  title: string;
  state: string; // "Issued" | "ResponsePending" | "UnderReview" | "Accepted" | "Rejected" | "Closed"
  dueDate: string | null;
  ncrId: string | null;
}

export interface QualityScarView {
  id: string;
  number: string;
  legalEntityId: string;
  supplierPartyId: string;
  title: string;
  state: string;
  ncrId: string | null;
  dueDate: string | null;
}

export interface CreateScarRequest {
  legalEntityId: string;
  supplierPartyId: string;
  title: string;
  description: string;
  ncrId?: string | null;
  dueDate?: string | null;
}

export interface SupplierScarResponseRequest {
  response: string;
  evidenceReference: string;
}

export interface SupplierScarReviewRequest {
  decision: string; // "Accepted" | "Rejected"
  notes: string;
}

// --------------------------------------------------------------------------
// 8. Certificates (COA / CoC)
// --------------------------------------------------------------------------

export interface QualityCertificateListItem {
  id: string;
  legalEntityId: string;
  number: string;
  certificateType: string; // "COA" | "COC"
  state: string; // "Draft" | "Issued" | "Revoked" | "Superseded"
  inspectionId: string;
  customerReference: string | null;
  createdAt: string;
}

export interface QualityCertificateView {
  id: string;
  number: string;
  certificateType: string;
  state: string;
  inspectionId: string;
  customerReference: string | null;
  createdAt: string;
}

export interface IssueCertificateRequest {
  inspectionId: string;
  certificateType?: string;
  customerReference?: string | null;
}

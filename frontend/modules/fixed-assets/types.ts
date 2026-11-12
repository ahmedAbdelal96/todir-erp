export type AssetLifecycleState =
  | "Draft"
  | "UnderConstruction"
  | "Active"
  | "FullyDepreciated"
  | "Disposed"
  | "WrittenOff";

export type AssetNature =
  | "Land"
  | "Building"
  | "LeaseholdImprovement"
  | "Vehicle"
  | "Equipment"
  | "Furniture"
  | "Tool"
  | "CIP"
  | "Intangible";

export type TrackingGranularity = "Individual" | "Grouped" | "Bulk";

export type AssignmentType = "Employee" | "Department" | "Location" | "Unassigned";

export type LocationType = "Site" | "Building" | "Floor" | "Room" | "Vehicle" | "Other";

export type HandoverType = "Issuance" | "Transfer" | "Return" | "Audit";

export type CandidateSourceType = "SupplierInvoice" | "InventoryIssue" | "ExpenseClaim" | "PurchasingOrder" | "Manual";

export type CandidateStatus = "Pending" | "Reviewed" | "Allocated" | "Rejected";

export type CandidateDecision = "Accept" | "Reject" | "Hold";

export type DisposalType = "Scrap" | "Loss" | "Theft" | "Sale" | "Donation";

export type VerificationResult = "Found" | "Missing" | "WrongLocation" | "WrongCustodian" | "Damaged";

export type VerificationCampaignStatus = "Draft" | "Active" | "Completed" | "Cancelled";

export type DepreciationRunStatus = "Draft" | "Calculated" | "Posted" | "Reversed";

export type DepreciationMethod = "StraightLine" | "NoDepreciation";

export type DepreciationConvention = "FullMonth" | "HalfMonth" | "ActualDays";

// ==========================================
// Views / DTOs from Backend
// ==========================================

export interface AssetCategoryView {
  readonly id: string;
  readonly code: string;
  readonly name: string;
  readonly assetNature: AssetNature;
  readonly trackingGranularity: TrackingGranularity;
  readonly requiresLocation: boolean;
  readonly requiresCustody: boolean;
  readonly requiresDepreciation: boolean;
  readonly capitalizationThreshold?: number | null;
  readonly defaultDepreciationMethod: DepreciationMethod;
  readonly defaultUsefulLifeMonths?: number | null;
  readonly defaultResidualValue: number;
}

export interface AssetView {
  readonly id: string;
  readonly legalEntityId: string;
  readonly categoryId: string;
  readonly assetNumber: string;
  readonly name: string;
  readonly description?: string | null;
  readonly assetTag?: string | null;
  readonly lifecycleState: AssetLifecycleState;
  readonly assetNature: AssetNature;
  readonly trackingGranularity: TrackingGranularity;
  readonly productVariantRef?: string | null;
  readonly inventorySerialRef?: string | null;
  readonly acquisitionDate?: string | null;
  readonly capitalizationDate?: string | null;
  readonly placedInServiceDate?: string | null;
  readonly quantity: number;
  readonly createdAt: string;
  readonly rowVersion: number;
}

export interface AssetAssignmentView {
  readonly id: string;
  readonly assetId: string;
  readonly assignmentType: AssignmentType;
  readonly employeeId?: string | null;
  readonly departmentId?: string | null;
  readonly custodianName?: string | null;
  readonly locationId?: string | null;
  readonly effectiveFrom: string;
  readonly effectiveTo?: string | null;
  readonly condition?: string | null;
  readonly accessories?: string | null;
  readonly acknowledgementReference?: string | null;
}

export interface AssetLocationView {
  readonly id: string;
  readonly legalEntityId: string;
  readonly parentLocationId?: string | null;
  readonly code: string;
  readonly name: string;
  readonly locationType: LocationType;
  readonly active: boolean;
}

export interface AssetHandoverView {
  readonly id: string;
  readonly assetId: string;
  readonly assignmentId: string;
  readonly handoverType: HandoverType;
  readonly acknowledgedAt?: string | null;
  readonly condition?: string | null;
  readonly evidence?: Record<string, unknown> | null;
  readonly createdAt: string;
}

export interface AssetDocumentView {
  readonly id: string;
  readonly assetId: string;
  readonly fileRef: string;
  readonly role: string;
  readonly title?: string | null;
  readonly sha256: string;
  readonly contentType: string;
  readonly sizeBytes: number;
  readonly classification: string;
  readonly createdAt: string;
}

export interface AssetScanView {
  readonly assetId: string;
  readonly assetNumber: string;
  readonly name: string;
  readonly assetTag?: string | null;
  readonly lifecycleState: AssetLifecycleState;
  readonly qrPayload: string;
}

export interface AssetTimelineEventView {
  readonly id: string;
  readonly eventType: string;
  readonly effectiveDate?: string | null;
  readonly occurredAt: string;
  readonly payload: Record<string, unknown>;
}

export interface VerificationCampaignView {
  readonly id: string;
  readonly legalEntityId: string;
  readonly name: string;
  readonly snapshotAt: string;
  readonly status: VerificationCampaignStatus;
  readonly lineCount: number;
  readonly pendingCount: number;
  readonly createdAt: string;
}

export interface AssetBookView {
  readonly id: string;
  readonly legalEntityId: string;
  readonly code: string;
  readonly name: string;
  readonly postsToLedger: boolean;
  readonly currencyCode: string;
  readonly defaultDepreciationMethod: DepreciationMethod;
  readonly defaultConvention: DepreciationConvention;
  readonly defaultUsefulLifeMonths?: number | null;
}

export interface DepreciationRunView {
  readonly id: string;
  readonly legalEntityId: string;
  readonly bookId: string;
  readonly periodStart: string;
  readonly periodEnd: string;
  readonly status: DepreciationRunStatus;
  readonly totalAmount: number;
  readonly lineCount: number;
  readonly createdAt: string;
}

export interface DisposalView {
  readonly assetId: string;
  readonly disposalType: DisposalType;
  readonly disposalDate: string;
  readonly grossCost: number;
  readonly accumulatedDepreciation: number;
  readonly netBookValue: number;
  readonly proceeds: number;
  readonly directCosts: number;
  readonly gainLoss: number;
  readonly status: string;
  readonly accumulatedImpairment?: number;
  readonly revaluationEffects?: number;
}

export interface AssetSaleView {
  readonly assetId: string;
  readonly disposalId: string;
  readonly invoiceId: string;
  readonly paymentReference?: string | null;
  readonly disposal: DisposalView;
  readonly status: string;
}

export interface ReinstatementView {
  readonly assetId: string;
  readonly disposalId: string;
  readonly reinstatementDate: string;
  readonly restoredQuantity: number;
  readonly restoredBookCount: number;
  readonly status: string;
  readonly alreadyApplied?: boolean;
}

export interface CapitalizationCandidateView {
  readonly id: string;
  readonly legalEntityId: string;
  readonly sourceType: CandidateSourceType;
  readonly sourceReference: string;
  readonly sourceSnapshot: Record<string, unknown>;
  readonly eligibleAmount: number;
  readonly allocatedAmount: number;
  readonly status: CandidateStatus;
  readonly decision?: CandidateDecision | null;
  readonly createdAt: string;
}

export interface SourceAllocationView {
  readonly id: string;
  readonly candidateId: string;
  readonly assetId: string;
  readonly amount: number;
  readonly allocationType: string;
  readonly createdAt: string;
  readonly alreadyApplied?: boolean;
}

export interface SourceAllocationCorrectionView {
  readonly id: string;
  readonly allocationId: string;
  readonly candidateId: string;
  readonly assetId: string;
  readonly amount: number;
  readonly remainingAllocationAmount: number;
  readonly candidateAllocatedAmount: number;
  readonly reason: string;
  readonly createdAt: string;
  readonly alreadyApplied?: boolean;
}

export interface CipAssetView {
  readonly assetId: string;
  readonly assetNumber: string;
  readonly name: string;
  readonly lifecycleState: AssetLifecycleState;
  readonly totalCost: number;
  readonly costSourceCount: number;
  readonly capitalizationDate?: string | null;
  readonly placedInServiceDate?: string | null;
}

export interface CipSplitTargetView {
  readonly assetId: string;
  readonly assetNumber: string;
  readonly name: string;
  readonly amount: number;
  readonly quantity: number;
  readonly lifecycleState: AssetLifecycleState;
}

export interface CipSplitView {
  readonly cipAssetId: string;
  readonly status: string;
  readonly originalCost: number;
  readonly allocatedAmount: number;
  readonly remainingCost: number;
  readonly targets: readonly CipSplitTargetView[];
  readonly alreadyApplied?: boolean;
}

export interface GroupedAssetSplitView {
  readonly parentAssetId: string;
  readonly childAssetId: string;
  readonly childAssetNumber: string;
  readonly parentQuantityBefore: number;
  readonly parentQuantityAfter: number;
  readonly childQuantity: number;
  readonly allocatedCost: number;
  readonly status: string;
  readonly alreadyApplied?: boolean;
}

export interface CombineCipView {
  readonly targetAssetId: string;
  readonly targetAssetNumber: string;
  readonly totalAmount: number;
  readonly sourceAssetIds: readonly string[];
  readonly status: string;
  readonly alreadyApplied?: boolean;
}

export interface AssetBookCostChangeView {
  readonly assetId: string;
  readonly bookId: string;
  readonly amount: number;
  readonly transactionType: string;
  readonly previousCost: number;
  readonly newCost: number;
  readonly ledgerFactReference?: string | null;
  readonly alreadyApplied?: boolean;
}

export interface AssetBookEstimateChangeView {
  readonly assetId: string;
  readonly bookId: string;
  readonly effectiveFrom: string;
  readonly usefulLifeMonths?: number | null;
  readonly residualValue?: number | null;
  readonly reason: string;
  readonly idempotencyKey: string;
  readonly createdAt: string;
  readonly alreadyApplied?: boolean;
}

export interface AssetDepreciationScheduleLineView {
  readonly assetId: string;
  readonly bookId: string;
  readonly periodStart: string;
  readonly periodEnd: string;
  readonly depreciationAmount: number;
  readonly cumulativeDepreciation: number;
  readonly projectedNetBookValue: number;
  readonly method: string;
}

export interface AssetImpairmentView {
  readonly assetId: string;
  readonly bookId: string;
  readonly amount: number;
  readonly effectiveDate: string;
  readonly reason: string;
  readonly approvalRequestId: string;
  readonly status: string;
  readonly ledgerFactReference?: string | null;
  readonly createdAt: string;
  readonly alreadyApplied?: boolean;
}

export interface AssetRevaluationView {
  readonly assetId: string;
  readonly bookId: string;
  readonly proposedGrossCost: number;
  readonly proposedResidualValue: number;
  readonly effectiveDate: string;
  readonly reason: string;
  readonly approvalRequestId: string;
  readonly providerCode: string;
  readonly providerVersion: string;
  readonly status: string;
  readonly createdAt: string;
}

export interface OpeningImportView {
  readonly id: string;
  readonly legalEntityId: string;
  readonly sourceSystem: string;
  readonly batchReference: string;
  readonly openingDate: string;
  readonly status: string;
  readonly lineCount: number;
  readonly validLineCount: number;
  readonly createdAt: string;
}

export interface AssetBookBalanceView {
  readonly assetId: string;
  readonly bookId: string;
  readonly legalEntityId: string;
  readonly originalCost: number;
  readonly accumulatedDepreciation: number;
  readonly netBookValue: number;
  readonly lifecycleState: AssetLifecycleState;
  readonly transactionCount: number;
  readonly lastTransactionDate?: string | null;
  readonly accumulatedImpairment?: number;
  readonly revaluationEffects?: number;
}

export interface AssetGlReconciliationView {
  readonly legalEntityId: string;
  readonly bookId: string;
  readonly asOfDate: string;
  readonly assetGrossCost: number;
  readonly ledgerGrossCost: number;
  readonly grossCostDifference: number;
  readonly assetAccumulatedDepreciation: number;
  readonly ledgerAccumulatedDepreciation: number;
  readonly accumulatedDepreciationDifference: number;
  readonly assetNetBookValue: number;
  readonly currencyCode: string;
  readonly status: string;
  readonly assetAccumulatedImpairment?: number;
  readonly revaluationEffects?: number;
}

export interface AssetRegisterRowView {
  readonly assetId: string;
  readonly legalEntityId: string;
  readonly assetNumber: string;
  readonly name: string;
  readonly lifecycleState: AssetLifecycleState;
  readonly assetNature: AssetNature;
  readonly trackingGranularity: TrackingGranularity;
  readonly assetTag?: string | null;
  readonly productVariantRef?: string | null;
  readonly inventorySerialRef?: string | null;
  readonly quantity: number;
  readonly bookId?: string | null;
  readonly bookCode?: string | null;
  readonly currencyCode?: string | null;
  readonly grossCost: number;
  readonly accumulatedDepreciation: number;
  readonly accumulatedImpairment: number;
  readonly revaluationEffects: number;
  readonly netBookValue: number;
  readonly assignmentType?: string | null;
  readonly employeeId?: string | null;
  readonly departmentId?: string | null;
  readonly custodianName?: string | null;
  readonly locationId?: string | null;
}

export interface AssetSourceDrilldownView {
  readonly assetId: string;
  readonly bookId: string;
  readonly bookCode: string;
  readonly transactionType: string;
  readonly transactionDate: string;
  readonly amount: number;
  readonly accumulatedDepreciation: number;
  readonly accumulatedImpairment: number;
  readonly revaluationEffect: number;
  readonly sourceReference?: string | null;
  readonly idempotencyKey?: string | null;
  readonly ledgerFactReference?: string | null;
}

export interface MassTransferPreviewItemView {
  readonly assetId: string;
  readonly assetNumber?: string | null;
  readonly status: string;
  readonly reason?: string | null;
}

export interface MassTransferPreviewView {
  readonly legalEntityId: string;
  readonly requestedCount: number;
  readonly eligibleCount: number;
  readonly rejectedCount: number;
  readonly items: readonly MassTransferPreviewItemView[];
}

export interface MassTransferView {
  readonly operationId: string;
  readonly status: string;
  readonly requestedCount: number;
  readonly appliedCount: number;
  readonly items: readonly MassTransferPreviewItemView[];
  readonly alreadyApplied?: boolean;
}

// ==========================================
// Requests
// ==========================================

export interface CreateAssetCategoryRequest {
  readonly code: string;
  readonly name: string;
  readonly assetNature: AssetNature;
  readonly trackingGranularity: TrackingGranularity;
  readonly requiresLocation: boolean;
  readonly requiresCustody: boolean;
  readonly requiresDepreciation: boolean;
  readonly capitalizationThreshold?: number | null;
  readonly defaultDepreciationMethod?: DepreciationMethod;
  readonly defaultUsefulLifeMonths?: number | null;
  readonly defaultResidualValue?: number;
}

export interface CreateAssetRequest {
  readonly legalEntityId: string;
  readonly categoryId: string;
  readonly name: string;
  readonly description?: string | null;
  readonly assetTag?: string | null;
  readonly productVariantRef?: string | null;
  readonly inventorySerialRef?: string | null;
  readonly sourceOriginType?: string | null;
  readonly acquisitionDate?: string | null;
  readonly capitalizationDate?: string | null;
  readonly placedInServiceDate?: string | null;
  readonly parentAssetId?: string | null;
  readonly quantity?: number | null;
}

export interface AssignAssetRequest {
  readonly assignmentType: AssignmentType;
  readonly employeeId?: string | null;
  readonly departmentId?: string | null;
  readonly custodianName?: string | null;
  readonly locationId?: string | null;
  readonly effectiveFrom: string;
  readonly effectiveTo?: string | null;
  readonly condition?: string | null;
  readonly accessories?: string | null;
  readonly acknowledgementReference?: string | null;
}

export interface ReturnAssetRequest {
  readonly effectiveFrom: string;
  readonly locationId?: string | null;
  readonly condition?: string | null;
  readonly accessories?: string | null;
  readonly acknowledgementReference?: string | null;
  readonly evidence?: Record<string, unknown>;
}

export interface CreateAssetLocationRequest {
  readonly legalEntityId: string;
  readonly parentLocationId?: string | null;
  readonly code: string;
  readonly name: string;
  readonly locationType?: LocationType;
}

export interface CreateAssetHandoverRequest {
  readonly assignmentId: string;
  readonly handoverType: HandoverType;
  readonly acknowledgedAt?: string | null;
  readonly condition?: string | null;
  readonly evidence?: Record<string, unknown>;
}

export interface AddAssetDocumentRequest {
  readonly fileRef: string;
  readonly role: string;
  readonly title?: string | null;
}

export interface CreateVerificationCampaignRequest {
  readonly legalEntityId: string;
  readonly name: string;
}

export interface VerifyAssetRequest {
  readonly result: VerificationResult;
  readonly observedLocationId?: string | null;
  readonly notes?: string | null;
  readonly evidence?: Record<string, unknown> | null;
}

export interface MassTransferRequest {
  readonly legalEntityId: string;
  readonly assetIds: readonly string[];
  readonly assignment: AssignAssetRequest;
  readonly idempotencyKey: string;
  readonly reason: string;
}

export interface CreateBookRequest {
  readonly legalEntityId: string;
  readonly code: string;
  readonly name: string;
  readonly postsToLedger: boolean;
  readonly currencyCode: string;
  readonly defaultDepreciationMethod?: DepreciationMethod;
  readonly defaultConvention?: DepreciationConvention;
  readonly defaultUsefulLifeMonths?: number | null;
}

export interface CreateAssetBookRequest {
  readonly legalEntityId: string;
  readonly bookDefinitionId: string;
  readonly originalCost: number;
  readonly residualValue: number;
  readonly usefulLifeMonths: number;
  readonly depreciationMethod: DepreciationMethod;
  readonly placedInServiceDate: string;
}

export interface CreateDepreciationRunRequest {
  readonly legalEntityId: string;
  readonly bookId: string;
  readonly periodStart: string;
  readonly periodEnd: string;
  readonly idempotencyKey?: string | null;
}

export interface ReverseDepreciationRunRequest {
  readonly reversalDate: string;
  readonly reason: string;
  readonly idempotencyKey?: string | null;
}

export interface DisposeAssetRequest {
  readonly legalEntityId: string;
  readonly disposalType: DisposalType;
  readonly disposalDate: string;
  readonly proceeds: number;
  readonly directCosts: number;
  readonly reason: string;
  readonly idempotencyKey?: string | null;
  readonly quantity?: number | null;
  readonly saleReference?: string | null;
}

export interface SellAssetRequest {
  readonly legalEntityId: string;
  readonly invoiceId: string;
  readonly saleDate: string;
  readonly reason: string;
  readonly idempotencyKey: string;
  readonly paymentReference?: string | null;
  readonly quantity?: number | null;
}

export interface ReinstateAssetRequest {
  readonly legalEntityId: string;
  readonly disposalId: string;
  readonly reinstatementDate: string;
  readonly reason: string;
  readonly idempotencyKey: string;
}

export interface AllocateCapitalizationCandidateRequest {
  readonly assetId: string;
  readonly amount: number;
  readonly allocationType?: string;
  readonly effectiveDate?: string | null;
}

export interface CorrectSourceAllocationRequest {
  readonly allocationId?: string;
  readonly amount: number;
  readonly reason: string;
  readonly idempotencyKey: string;
}

export interface CreateCipAssetRequest {
  readonly legalEntityId: string;
  readonly categoryId: string;
  readonly name: string;
  readonly acquisitionDate: string;
  readonly sourceType: string;
  readonly sourceReference: string;
  readonly amount: number;
  readonly description?: string | null;
}

export interface AddCipCostSourceRequest {
  readonly sourceType: string;
  readonly sourceReference: string;
  readonly amount: number;
  readonly reversalOfId?: string | null;
}

export interface CapitalizeCipRequest {
  readonly targetCategoryId: string;
  readonly name?: string | null;
  readonly capitalizationDate: string;
  readonly placedInServiceDate?: string | null;
}

export interface CipSplitTargetRequest {
  readonly targetCategoryId: string;
  readonly name: string;
  readonly amount: number;
  readonly quantity: number;
  readonly capitalizationDate: string;
  readonly placedInServiceDate?: string | null;
}

export interface SplitCipRequest {
  readonly idempotencyKey: string;
  readonly targets: readonly CipSplitTargetRequest[];
}

export interface SplitGroupedAssetRequest {
  readonly idempotencyKey: string;
  readonly quantity: number;
  readonly name: string;
  readonly effectiveDate: string;
}

export interface CombineCipSourceRequest {
  readonly assetId: string;
  readonly amount: number;
}

export interface CombineCipRequest {
  readonly legalEntityId: string;
  readonly targetCategoryId: string;
  readonly name: string;
  readonly capitalizationDate: string;
  readonly placedInServiceDate?: string | null;
  readonly idempotencyKey: string;
  readonly sources: readonly CombineCipSourceRequest[];
}

export interface ChangeAssetBookCostRequest {
  readonly legalEntityId: string;
  readonly bookId?: string;
  readonly amount: number;
  readonly effectiveDate: string;
  readonly reason: string;
  readonly idempotencyKey?: string | null;
}

export interface CreateAssetBookEstimateChangeRequest {
  readonly legalEntityId: string;
  readonly effectiveFrom: string;
  readonly usefulLifeMonths?: number | null;
  readonly residualValue?: number | null;
  readonly reason: string;
  readonly idempotencyKey?: string | null;
}

export interface CreateAssetImpairmentRequest {
  readonly legalEntityId: string;
  readonly bookId?: string;
  readonly amount: number;
  readonly effectiveDate: string;
  readonly reason: string;
  readonly approvalRequestId: string;
  readonly evidenceFileRef?: string | null;
  readonly idempotencyKey?: string | null;
}

export interface RequestAssetImpairmentApprovalRequest {
  readonly legalEntityId: string;
  readonly bookId?: string;
  readonly amount: number;
  readonly effectiveDate: string;
  readonly reason: string;
  readonly policyVersionId?: string | null;
}

export interface CreateAssetRevaluationRequest {
  readonly legalEntityId: string;
  readonly bookId?: string;
  readonly proposedGrossCost: number;
  readonly proposedResidualValue: number;
  readonly effectiveDate: string;
  readonly reason: string;
  readonly approvalRequestId: string;
  readonly providerCode: string;
  readonly providerVersion: string;
}

export interface RequestAssetRevaluationApprovalRequest {
  readonly legalEntityId: string;
  readonly bookId?: string;
  readonly proposedGrossCost: number;
  readonly proposedResidualValue: number;
  readonly effectiveDate: string;
  readonly reason: string;
  readonly providerCode: string;
  readonly providerVersion: string;
  readonly policyVersionId?: string | null;
}

export interface ReviewCapitalizationCandidateRequest {
  readonly decision: CandidateDecision;
  readonly reason?: string | null;
}

// ==========================================
// Convenience Type Aliases for Module UI
// ==========================================
export type FixedAssetDto = AssetView & {
  readonly assetCode?: string | undefined;
  readonly assetName?: string | undefined;
  readonly categoryName?: string | undefined;
  readonly nature?: AssetNature | undefined;
  readonly acquisitionCost?: number | undefined;
  readonly accumulatedDepreciation?: number | undefined;
  readonly netBookValue?: number | undefined;
  readonly currency?: string | undefined;
  readonly locationName?: string | undefined;
  readonly custodianId?: string | undefined;
  readonly department?: string | undefined;
  readonly inServiceDate?: string | null | undefined;
  readonly serialNumber?: string | null | undefined;
  readonly barcode?: string | null | undefined;
  readonly modelNumber?: string | null | undefined;
  readonly manufacturer?: string | null | undefined;
  readonly warrantyExpiryDate?: string | null | undefined;
  readonly customAttributes?: Record<string, unknown> | null | undefined;
  readonly depreciationMethod?: string | undefined;
  readonly usefulLifeMonths?: number | undefined;
  readonly salvageValue?: number | undefined;
};
export type AssetCategoryDto = AssetCategoryView & {
  readonly categoryName?: string | undefined;
  readonly categoryCode?: string | undefined;
  readonly nature?: AssetNature | undefined;
};
export type AssetLocationDto = AssetLocationView & {
  readonly locationName?: string | undefined;
  readonly locationCode?: string | undefined;
  readonly building?: string | null | undefined;
  readonly floorNumber?: string | null | undefined;
  readonly assetCount?: number | undefined;
};
export type AssetAssignmentDto = AssetAssignmentView;
export type AssetBookDto = AssetBookView & {
  readonly bookName?: string | undefined;
  readonly bookCode?: string | undefined;
  readonly bookType?: string | undefined;
};
export type VerificationCampaignDto = VerificationCampaignView & {
  readonly campaignName?: string | undefined;
  readonly scope?: string | undefined;
  readonly targetLocationName?: string | undefined;
};
export type CapitalizationCandidateDto = CapitalizationCandidateView & {
  readonly description?: string | undefined;
  readonly amount?: number | undefined;
  readonly currencyCode?: string | undefined;
  readonly sourceDocumentRef?: string | undefined;
  readonly transactionDate?: string | undefined;
};
export type CipProjectDto = CipAssetView & {
  readonly projectCode?: string | undefined;
  readonly projectName?: string | undefined;
  readonly description?: string | null | undefined;
  readonly accumulatedCost?: number | undefined;
  readonly currency?: string | undefined;
  readonly estimatedCompletionDate?: string | null | undefined;
  readonly status?: string | undefined;
};
export type DepreciationRunDto = DepreciationRunView & {
  readonly periodYear?: number | undefined;
  readonly periodMonth?: number | undefined;
  readonly cutoffDate?: string | undefined;
  readonly description?: string | null | undefined;
  readonly totalAssetsCount?: number | undefined;
  readonly totalDepreciationAmount?: number | undefined;
  readonly currency?: string | undefined;
  readonly glJournalEntryId?: string | null | undefined;
};
export type AuditFindingStatus = VerificationResult;
export type CreateFixedAssetRequest = Partial<CreateAssetRequest> & {
  readonly assetName?: string | undefined;
  readonly assetCode?: string | undefined;
  readonly acquisitionCost?: number | undefined;
  readonly currency?: string | undefined;
  readonly salvageValue?: number | undefined;
  readonly usefulLifeMonths?: number | undefined;
  readonly inServiceDate?: string | undefined;
  readonly serialNumber?: string | undefined;
  readonly barcode?: string | undefined;
  readonly modelNumber?: string | undefined;
  readonly manufacturer?: string | undefined;
  readonly warrantyExpiryDate?: string | undefined;
  readonly locationId?: string | undefined;
  readonly department?: string | undefined;
  readonly custodianId?: string | undefined;
  readonly customAttributes?: Record<string, unknown> | undefined;
  readonly depreciationMethod?: DepreciationMethod | undefined;
};
export type TransferCustodyRequest = {
  readonly targetCustodianId?: string | undefined;
  readonly targetDepartment?: string | undefined;
  readonly targetLocationId?: string | undefined;
  readonly targetRoom?: string | undefined;
  readonly handoverNote?: string | undefined;
};
export type ReturnCustodyRequest = {
  readonly targetLocationId?: string | undefined;
  readonly condition?: string | undefined;
  readonly handoverNote?: string | undefined;
};
export type CapitalizeCipProjectRequest = {
  readonly targetAssetName?: string | undefined;
  readonly targetCategoryId?: string | undefined;
  readonly inServiceDate?: string | undefined;
  readonly usefulLifeMonths?: number | undefined;
  readonly salvageValue?: number | undefined;
  readonly capitalizationAmount?: number | undefined;
};
export type SplitCipProjectRequest = {
  readonly portions: readonly { projectName: string; amount: number }[];
};
export type CombineCipProjectsRequest = {
  readonly projectIds: readonly string[];
  readonly combinedProjectName: string;
};
export type PrepareDepreciationRunRequest = {
  readonly periodYear: number;
  readonly periodMonth: number;
  readonly cutoffDate: string;
  readonly description?: string | undefined;
};
export type RecordAuditFindingRequest = {
  readonly assetId?: string | undefined;
  readonly barcodeOrTag?: string | undefined;
  readonly status: AuditFindingStatus;
  readonly actualLocationId?: string | undefined;
  readonly notes?: string | undefined;
};
export type ChangeEstimateRequest = {
  readonly remainingLifeMonths: number;
  readonly salvageValue?: number | undefined;
  readonly effectiveDate: string;
  readonly justification: string;
};
export type AddCostRequest = {
  readonly additionAmount: number;
  readonly additionDate: string;
  readonly description: string;
  readonly sourceDocumentRef?: string | undefined;
};
export type ProposeImpairmentRequest = {
  readonly recoverableAmount: number;
  readonly impairmentLoss: number;
  readonly impairmentDate: string;
  readonly reason: string;
};
export type MassTransferCustodyRequest = {
  readonly assetIds: readonly string[];
  readonly targetCustodianId?: string | undefined;
  readonly targetDepartment?: string | undefined;
  readonly targetLocationId?: string | undefined;
  readonly handoverNote?: string | undefined;
};
export type BookType = "Corporate" | "Tax" | "Management";


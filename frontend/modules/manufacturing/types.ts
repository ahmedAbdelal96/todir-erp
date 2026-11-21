// Types and contracts for Module 26 Manufacturing / MRP

export type ManufacturingMode = "Discrete" | "ProcessBatch";
export type MakeBuyPolicy = "Make" | "Buy" | "MakeOrBuy" | "Subcontract";
export type ProductionOrderState = "Draft" | "Released" | "InProgress" | "OnHold" | "Completed" | "Closed" | "Cancelled";
export type ProductionState = ProductionOrderState;
export type MaterialState = "Pending" | "PartiallyIssued" | "FullyIssued" | "Consumed";
export type OperationState = "Pending" | "Ready" | "InProgress" | "Paused" | "Completed" | "Skipped";
export type QualityState = "Pending" | "Passed" | "Failed" | "Exempt" | "Blocked";
export type CostState = "Estimated" | "Accumulating" | "Reconciled" | "Closed";
export type BomState = "Draft" | "Approved" | "Active" | "Superseded" | "Archived";
export type ConsumptionMethod = "Manual" | "Backflush" | "Staged";
export type OutputType = "Good" | "Primary" | "CoProduct" | "ByProduct" | "Loss";
export type PlannedSupplyType = "Production" | "Purchase" | "Transfer" | "Manufacture";
export type PlannedSupplyState = "Suggested" | "Firmed" | "Cancelled" | "Stale";
export type CapacityModel = "Hours" | "Units" | "Finite" | "Infinite";

export interface Plant {
  readonly id: string;
  readonly legalEntityId: string;
  readonly code: string;
  readonly name: string;
  readonly timeZone: string;
  readonly state: string;
}

export interface ManufacturingProfile {
  readonly id: string;
  readonly legalEntityId: string;
  readonly productVariantId: string;
  readonly manufacturingEnabled: boolean;
  readonly defaultMode: ManufacturingMode;
  readonly makeBuyPolicy: MakeBuyPolicy;
  readonly defaultPlantId?: string | null;
}

export interface Bom {
  readonly id: string;
  readonly productVariantId: string;
  readonly code: string;
  readonly name: string;
  readonly state: string;
}

export interface BomLine {
  readonly id: string;
  readonly lineNo: number;
  readonly componentProductVariantId: string;
  readonly requiredQty: number;
  readonly uomCode: string;
  readonly scrapFactor: number;
  readonly consumptionMethod: ConsumptionMethod;
  readonly substituteGroupId?: string | null;
  readonly phantom: boolean;
}

export interface BomVersion {
  readonly id: string;
  readonly bomId: string;
  readonly revision: number;
  readonly state: BomState;
  readonly validFrom: string;
  readonly validTo?: string | null;
  readonly baseOutputQty: number;
  readonly outputUomCode: string;
  readonly lines: readonly BomLine[];
}

export interface Formula {
  readonly id: string;
  readonly productVariantId: string;
  readonly code: string;
  readonly name: string;
  readonly state: string;
}

export interface FormulaIngredient {
  readonly id: string;
  readonly lineNo: number;
  readonly productVariantId: string;
  readonly quantity: number;
  readonly uomCode: string;
  readonly fixedQuantity: boolean;
}

export interface FormulaOutput {
  readonly id: string;
  readonly lineNo: number;
  readonly productVariantId: string;
  readonly outputType: OutputType;
  readonly quantity: number;
  readonly uomCode: string;
  readonly allocationBasis: string;
}

export interface FormulaVersion {
  readonly id: string;
  readonly formulaId: string;
  readonly revision: number;
  readonly state: string;
  readonly validFrom: string;
  readonly validTo?: string | null;
  readonly baseBatchQty: number;
  readonly outputUomCode: string;
  readonly expectedYield: number;
  readonly ingredients: readonly FormulaIngredient[];
  readonly outputs: readonly FormulaOutput[];
}

export interface Routing {
  readonly id: string;
  readonly productVariantId?: string | null;
  readonly code: string;
  readonly name: string;
  readonly state: string;
}

export interface RoutingOperation {
  readonly id: string;
  readonly sequenceNo: number;
  readonly code: string;
  readonly name: string;
  readonly workCenterId?: string | null;
  readonly setupMinutes: number;
  readonly runMinutesPerUnit: number;
  readonly queueMinutes: number;
  readonly moveMinutes: number;
  readonly operationType: string;
  readonly dependsOnSequence?: number | null;
  readonly qualityGateRequired: boolean;
}

export interface RoutingVersion {
  readonly id: string;
  readonly routingId: string;
  readonly revision: number;
  readonly state: string;
  readonly validFrom: string;
  readonly validTo?: string | null;
  readonly operations: readonly RoutingOperation[];
}

export interface WorkCenter {
  readonly id: string;
  readonly plantId: string;
  readonly code: string;
  readonly name: string;
  readonly capacityModel: CapacityModel;
  readonly defaultEfficiency: number;
  readonly state: string;
}

export interface WorkCenterCalendar {
  readonly id: string;
  readonly workCenterId: string;
  readonly weekday: number;
  readonly shiftStart: string;
  readonly shiftEnd: string;
  readonly effectiveFrom: string;
  readonly effectiveTo?: string | null;
  readonly breakMinutes: number;
}

export interface ProductionDefinition {
  readonly id: string;
  readonly legalEntityId: string;
  readonly plantId: string;
  readonly productVariantId: string;
  readonly manufacturingMode: ManufacturingMode;
  readonly bomVersionId?: string | null;
  readonly formulaVersionId?: string | null;
  readonly routingVersionId?: string | null;
  readonly validFrom: string;
  readonly validTo?: string | null;
  readonly minQty?: number | null;
  readonly maxQty?: number | null;
  readonly priority: number;
  readonly expectedYield?: number;
  readonly state: string;
}

export interface ProductionOrder {
  readonly id: string;
  readonly number: string;
  readonly orderNumber?: string;
  readonly legalEntityId: string;
  readonly plantId: string;
  readonly productVariantId: string;
  readonly productionDefinitionId?: string | null;
  readonly orderedQty: number;
  readonly uomCode: string;
  readonly needDate: string;
  readonly plannedStart?: string | null;
  readonly plannedEnd?: string | null;
  readonly state: ProductionOrderState;
  readonly materialState: MaterialState;
  readonly materialStatus?: MaterialState;
  readonly operationState: OperationState;
  readonly operationStatus?: OperationState;
  readonly qualityState: QualityState;
  readonly qualityStatus?: QualityState;
  readonly costState: CostState;
  readonly costStatus?: CostState;
  readonly completedQty: number;
  readonly scrapQty: number;
  readonly rowVersion: number;
  readonly version?: number;
}

export interface ProductionMaterial {
  readonly id: string;
  readonly productVariantId: string;
  readonly componentProductVariantId?: string;
  readonly preferredProductVariantId: string;
  readonly requiredQty: number;
  readonly reservedQty: number;
  readonly issuedQty: number;
  readonly consumedQty: number;
  readonly returnedQty: number;
  readonly scrappedQty: number;
  readonly uomCode: string;
  readonly state: string;
}

export interface ProductionOperation {
  readonly id: string;
  readonly productionOrderId: string;
  readonly sequenceNo: number;
  readonly code?: string;
  readonly name: string;
  readonly workCenterId?: string | null;
  readonly standardSetupMinutes?: number;
  readonly standardRunMinutesPerUnit?: number;
  readonly state: OperationState;
  readonly completedQty: number;
  readonly actualGoodQty: number;
  readonly actualScrapQty: number;
  readonly actualMinutes?: number | null;
  readonly actualLaborMinutes?: number | null;
  readonly actualMachineMinutes?: number | null;
  readonly qualityGateRequired: boolean;
  readonly rowVersion: number;
  readonly version?: number;
}

export interface QualityGate {
  readonly id: string;
  readonly productionOrderId: string;
  readonly productionOperationId?: string | null;
  readonly required: boolean;
  readonly capabilityAvailable: boolean;
  readonly decisionState: string;
  readonly sourceRef?: string | null;
}

export interface SubcontractCommitment {
  readonly id: string;
  readonly productionOrderId: string;
  readonly productionOperationId?: string | null;
  readonly sourceModule: string;
  readonly sourceDocumentId: string;
  readonly quantity: number;
  readonly uomCode: string;
  readonly expectedDate: string;
  readonly state: string;
  readonly committedAmount: number;
  readonly currencyCode?: string | null;
}

export interface ProductionOutput {
  readonly id: string;
  readonly productionOrderId: string;
  readonly productVariantId: string;
  readonly quantity: number;
  readonly outputType: OutputType;
  readonly receiptStatus: string;
  readonly idempotencyKey: string;
}

export interface ProductionCost {
  readonly productionOrderId: string;
  readonly currencyCode: string;
  readonly materialCost: number;
  readonly laborCost: number;
  readonly machineCost: number;
  readonly overheadCost: number;
  readonly subcontractCost: number;
  readonly totalCost: number;
  readonly unitCost: number;
  readonly actualMaterialCost?: number;
  readonly actualLaborCost?: number;
  readonly actualOverheadCost?: number;
}

export interface ProductionCloseReadiness {
  readonly productionOrderId: string;
  readonly state: string;
  readonly canClose?: boolean;
  readonly materialIssuedQty: number;
  readonly materialAccountedQty: number;
  readonly goodOutputQty: number;
  readonly scrapQty: number;
  readonly operationsComplete: boolean;
  readonly allOperationsComplete?: boolean;
  readonly qualityState: QualityState;
  readonly costState: CostState;
  readonly wipAmount: number;
  readonly wipBalance?: number;
  readonly blockReason?: string | null;
  readonly blockers?: readonly string[];
  readonly issuedMaterialCost?: number;
  readonly consumedMaterialCost?: number;
}

export interface ManufacturingCostingSummary {
  readonly accumulatingOrdersCount: number;
  readonly totalWipAmount: number;
  readonly currencyCode: string;
  readonly completedBlockedOrdersCount: number;
  readonly eligibleForCloseOrdersCount: number;
  readonly closedReconciledOrdersCount: number;
}

export interface PlanningRun {
  readonly id: string;
  readonly runNumber?: string;
  readonly legalEntityId: string;
  readonly plantId?: string | null;
  readonly horizonStart: string;
  readonly horizonEnd: string;
  readonly state: string;
  readonly demandCount: number;
  readonly plannedSupplyCount: number;
  readonly exceptionCount: number;
}

export interface PlannedSupply {
  readonly id: string;
  readonly planningRunId: string;
  readonly plantId?: string | null;
  readonly productVariantId: string;
  readonly supplyType: PlannedSupplyType;
  readonly needDate: string;
  readonly dueDate?: string;
  readonly suggestedStart?: string | null;
  readonly quantity: number;
  readonly uomCode: string;
  readonly state: PlannedSupplyState;
  readonly firmedOrderId?: string | null;
}

export interface MpsPlan {
  readonly id: string;
  readonly legalEntityId: string;
  readonly plantId: string;
  readonly productVariantId: string;
  readonly bucketStart: string;
  readonly bucketEnd: string;
  readonly quantity: number;
  readonly uomCode: string;
  readonly state: string;
  readonly sourceRef?: string | null;
  readonly version: number;
}

export interface CapacityItem {
  readonly workCenterId: string;
  readonly from: string;
  readonly to: string;
  readonly availableHours: number;
  readonly downtimeHours: number;
  readonly plannedHours: number;
  readonly remainingHours: number;
  readonly state: string;
}

export interface GenealogyItem {
  readonly id: string;
  readonly productionOrderId: string;
  readonly outputId: string;
  readonly inputEventId: string;
  readonly inputLotId?: string | null;
  readonly outputLotId?: string | null;
  readonly inputQuantity: number;
  readonly outputQuantity: number;
  readonly certainty: string;
}

export interface SubstitutionGroup {
  readonly id: string;
  readonly code: string;
  readonly name: string;
  readonly policy: string;
}

export interface SubstituteItem {
  readonly id: string;
  readonly groupId: string;
  readonly productVariantId: string;
  readonly priority: number;
  readonly replacementRatio: number;
  readonly uomCode: string;
  readonly validFrom: string;
  readonly validTo?: string | null;
}

// Request Contracts
export interface CreatePlantRequest {
  readonly legalEntityId: string;
  readonly code: string;
  readonly name: string;
  readonly branchId?: string | null;
  readonly timeZone?: string;
}

export interface CreateManufacturingProfileRequest {
  readonly legalEntityId: string;
  readonly productVariantId: string;
  readonly defaultMode?: ManufacturingMode;
  readonly makeBuyPolicy?: MakeBuyPolicy;
  readonly defaultPlantId?: string | null;
}

export interface CreateBomRequest {
  readonly productVariantId: string;
  readonly code: string;
  readonly name: string;
}

export interface BomLineInput {
  readonly componentProductVariantId: string;
  readonly requiredQty: number;
  readonly uomCode: string;
  readonly scrapFactor?: number;
  readonly consumptionMethod?: ConsumptionMethod;
  readonly operationSequence?: number | null;
  readonly substituteGroupId?: string | null;
  readonly phantom?: boolean;
}

export interface CreateBomVersionRequest {
  readonly validFrom: string;
  readonly validTo?: string | null;
  readonly baseOutputQty: number;
  readonly outputUomCode: string;
  readonly lines: readonly BomLineInput[];
  readonly expectedYield?: number;
  readonly expectedScrap?: number;
}

export interface CreateFormulaRequest {
  readonly productVariantId: string;
  readonly code: string;
  readonly name: string;
}

export interface FormulaIngredientInput {
  readonly productVariantId: string;
  readonly quantity: number;
  readonly uomCode: string;
  readonly fixedQuantity?: boolean;
}

export interface FormulaOutputInput {
  readonly productVariantId: string;
  readonly outputType: OutputType;
  readonly quantity: number;
  readonly uomCode: string;
  readonly allocationBasis?: string;
}

export interface CreateFormulaVersionRequest {
  readonly validFrom: string;
  readonly validTo?: string | null;
  readonly baseBatchQty: number;
  readonly outputUomCode: string;
  readonly ingredients: readonly FormulaIngredientInput[];
  readonly outputs: readonly FormulaOutputInput[];
  readonly expectedYield?: number;
}

export interface CreateRoutingRequest {
  readonly productVariantId?: string | null | undefined;
  readonly code: string;
  readonly name: string;
}

export interface RoutingOperationInput {
  readonly sequenceNo: number;
  readonly code: string;
  readonly name: string;
  readonly workCenterId?: string | null | undefined;
  readonly setupMinutes?: number | undefined;
  readonly runMinutesPerUnit?: number | undefined;
  readonly queueMinutes?: number | undefined;
  readonly moveMinutes?: number | undefined;
  readonly operationType?: string | undefined;
  readonly dependsOnSequence?: number | null | undefined;
  readonly qualityGateRequired?: boolean | undefined;
}

export interface CreateRoutingVersionRequest {
  readonly validFrom: string;
  readonly validTo?: string | null | undefined;
  readonly operations: readonly RoutingOperationInput[];
}

export interface CreateWorkCenterRequest {
  readonly plantId: string;
  readonly code: string;
  readonly name: string;
  readonly capacityModel?: CapacityModel;
  readonly timeZone?: string;
  readonly defaultEfficiency?: number;
}

export interface CreateWorkCenterCalendarRequest {
  readonly weekday: number;
  readonly shiftStart: string;
  readonly shiftEnd: string;
  readonly effectiveFrom: string;
  readonly effectiveTo?: string | null;
  readonly breakMinutes?: number;
}

export interface CreateProductionDefinitionRequest {
  readonly legalEntityId: string;
  readonly plantId: string;
  readonly productVariantId: string;
  readonly manufacturingMode: ManufacturingMode;
  readonly bomVersionId?: string | null | undefined;
  readonly formulaVersionId?: string | null | undefined;
  readonly routingVersionId?: string | null | undefined;
  readonly validFrom: string;
  readonly validTo?: string | null | undefined;
  readonly minQty?: number | null | undefined;
  readonly maxQty?: number | null | undefined;
  readonly priority?: number | undefined;
  readonly expectedYield?: number | undefined;
}

export interface ResolveDefinitionRequest {
  readonly legalEntityId: string;
  readonly plantId: string;
  readonly productVariantId: string;
  readonly quantity: number;
  readonly asOfDate: string;
}

export interface CreateProductionOrderRequest {
  readonly legalEntityId: string;
  readonly plantId: string;
  readonly productVariantId: string;
  readonly orderedQty: number;
  readonly uomCode: string;
  readonly needDate: string;
  readonly plannedStart?: string | null | undefined;
  readonly plannedEnd?: string | null | undefined;
  readonly sourceType?: string | undefined;
  readonly sourceRef?: string | null | undefined;
  readonly productionDefinitionId?: string | null | undefined;
  readonly projectId?: string | null | undefined;
  readonly projectTaskId?: string | null | undefined;
}

export interface ProductionActionRequest {
  readonly expectedVersion: number;
  readonly reason?: string | null | undefined;
}

export interface MaterialActionRequest {
  readonly requirementId: string;
  readonly quantity: number;
  readonly uomCode: string;
  readonly eventType: "Reserved" | "Issued" | "Consumed" | "Returned";
  readonly idempotencyKey: string;
  readonly inventoryOperationId?: string | null | undefined;
  readonly unitCost?: number | null | undefined;
  readonly currencyCode?: string | null | undefined;
  readonly substituteProductVariantId?: string | null | undefined;
  readonly warehouseId?: string | null | undefined;
  readonly locationId?: string | null | undefined;
  readonly enteredUomId?: string | null | undefined;
  readonly conversionFactorSnapshot?: number | undefined;
  readonly inventoryReservationId?: string | null | undefined;
}

export interface OutputReportRequest {
  readonly quantity: number;
  readonly uomCode: string;
  readonly outputType?: OutputType | undefined;
  readonly idempotencyKey: string;
  readonly operationId?: string | null | undefined;
  readonly provisionalValue?: number | null | undefined;
  readonly currencyCode?: string | null | undefined;
  readonly lotId?: string | null | undefined;
  readonly serialId?: string | null | undefined;
  readonly warehouseId?: string | null | undefined;
  readonly locationId?: string | null | undefined;
  readonly enteredUomId?: string | null | undefined;
  readonly conversionFactorSnapshot?: number | undefined;
  readonly inventoryOperationId?: string | null | undefined;
  readonly productVariantId?: string | null | undefined;
}

export interface ScrapReportRequest {
  readonly quantity: number;
  readonly uomCode: string;
  readonly category: string;
  readonly reason?: string | null | undefined;
  readonly idempotencyKey: string;
  readonly operationId?: string | null | undefined;
}

export interface OperationActionRequest {
  readonly expectedVersion: number;
  readonly idempotencyKey: string;
  readonly reason?: string | null | undefined;
  readonly quantity?: number | null | undefined;
  readonly actualMinutes?: number | null | undefined;
}

export interface AssignOperationResourceRequest {
  readonly resourceId: string;
  readonly requiredQuantity?: number;
}

export interface RecordLaborFactRequest {
  readonly productionOperationId?: string | null;
  readonly resourceId?: string | null;
  readonly employeeId?: string | null;
  readonly workDate: string;
  readonly minutes: number;
  readonly rateAmount: number;
  readonly currencyCode: string;
  readonly sourceRef: string;
  readonly idempotencyKey: string;
}

export interface QualityGateDecisionRequest {
  readonly productionOperationId?: string | null | undefined;
  readonly required: boolean;
  readonly capabilityAvailable: boolean;
  readonly decisionState: string;
  readonly sourceRef?: string | null | undefined;
}

export interface QualityGatePolicyRequest {
  readonly productionOperationId?: string | null | undefined;
  readonly required: boolean;
  readonly reason: string;
}

export interface CreateSubcontractCommitmentRequest {
  readonly productionOperationId?: string | null | undefined;
  readonly sourceModule: string;
  readonly sourceDocumentId: string;
  readonly supplierPartyId?: string | null | undefined;
  readonly quantity: number;
  readonly uomCode: string;
  readonly expectedDate: string;
  readonly committedAmount: number;
  readonly currencyCode?: string | null | undefined;
  readonly idempotencyKey: string;
}

export interface CreatePlanningRunRequest {
  readonly legalEntityId: string;
  readonly plantId?: string | null | undefined;
  readonly horizonStart: string;
  readonly horizonEnd: string;
  readonly policyVersion?: string | undefined;
}

export interface CreateMpsPlanRequest {
  readonly legalEntityId: string;
  readonly plantId: string;
  readonly productVariantId: string;
  readonly bucketStart: string;
  readonly bucketEnd: string;
  readonly quantity: number;
  readonly uomCode: string;
  readonly sourceRef?: string | null;
}

export interface RecordCostFactRequest {
  readonly sourceType: string;
  readonly sourceId: string;
  readonly costCategory: "Material" | "Labor" | "Machine" | "Overhead" | "Subcontract";
  readonly amount: number;
  readonly currencyCode: string;
  readonly quantity?: number | null;
  readonly sourceDate: string;
  readonly idempotencyKey: string;
}

export interface AllocateOutputCostRequest {
  readonly outputId: string;
  readonly allocationBasis: string;
  readonly allocationValue: number;
  readonly allocatedAmount: number;
  readonly currencyCode: string;
  readonly idempotencyKey: string;
}

export interface GenealogyLinkRequest {
  readonly outputId: string;
  readonly inputEventId: string;
  readonly inputQuantity: number;
  readonly outputQuantity: number;
  readonly inputLotId?: string | null | undefined;
  readonly outputLotId?: string | null | undefined;
  readonly certainty: "Exact" | "Possible" | "Commingled";
  readonly idempotencyKey: string;
}

export interface CreateSubstitutionGroupRequest {
  readonly code: string;
  readonly name: string;
  readonly policy?: "Suggest" | "Automatic" | "Forbidden";
}

export interface AddSubstituteRequest {
  readonly productVariantId: string;
  readonly priority: number;
  readonly validFrom: string;
  readonly validTo?: string | null;
  readonly replacementRatio: number;
  readonly uomCode: string;
  readonly autoApplyAllowed?: boolean;
  readonly maxCostIncreasePct?: number | null;
  readonly qualityApprovalRequired?: boolean;
}

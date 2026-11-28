export interface PlanningCycleView {
  readonly id: string;
  readonly legalEntityId: string;
  readonly name: string;
  readonly cycleType: string;
  readonly horizon: string;
  readonly startPeriod: string;
  readonly endPeriod: string;
  readonly actualCutoff?: string | null;
  readonly state: string;
  readonly rowVersion: number;
}

export interface CreatePlanningCycleRequest {
  readonly legalEntityId: string;
  readonly name: string;
  readonly cycleType: string;
  readonly horizon: string;
  readonly startPeriod: string;
  readonly endPeriod: string;
  readonly actualCutoff?: string | null;
  readonly ownerUserId?: string | null;
  readonly dueAt?: string | null;
}

export interface PlanningModelView {
  readonly id: string;
  readonly name: string;
  readonly modelType: string;
  readonly legalEntityId?: string | null;
  readonly currencyCode: string;
  readonly status: string;
}

export interface CreatePlanningModelRequest {
  readonly name: string;
  readonly modelType: string;
  readonly legalEntityId?: string | null;
  readonly currencyCode: string;
}

export interface ModelVersionView {
  readonly id: string;
  readonly modelId: string;
  readonly versionNo: number;
  readonly state: string;
}

export interface CreateModelVersionRequest {
  readonly modelId: string;
  readonly definition: Record<string, unknown>;
}

export interface PlanVersionView {
  readonly id: string;
  readonly cycleId: string;
  readonly modelVersionId: string;
  readonly name: string;
  readonly versionKind: string;
  readonly state: string;
  readonly actualCutoff?: string | null;
  readonly rowVersion: number;
}

export interface CreatePlanVersionRequest {
  readonly cycleId: string;
  readonly modelVersionId: string;
  readonly name: string;
  readonly versionKind: string;
  readonly scenarioId?: string | null;
  readonly actualCutoff?: string | null;
}

export interface ScenarioView {
  readonly id: string;
  readonly name: string;
  readonly scenarioType: string;
  readonly visibility: string;
  readonly state: string;
}

export interface CreateScenarioRequest {
  readonly name: string;
  readonly scenarioType: string;
  readonly baseVersionId?: string | null;
  readonly visibility: string;
}

export interface ScenarioAdoptRequest {
  readonly name: string;
  readonly versionKind: string;
  readonly actualCutoff?: string | null;
}

export interface GridCellRequest {
  readonly legalEntityId: string;
  readonly periodStart: string;
  readonly metricCode: string;
  readonly dimensionKey: Record<string, unknown>;
  readonly amount: number | null;
  readonly currencyCode: string;
  readonly explicitZero: boolean;
  readonly sourceLabel?: string | null;
}

export interface GridWriteRequest {
  readonly planVersionId: string;
  readonly cells: readonly GridCellRequest[];
  readonly expectedVersion?: number | null;
}

export interface GridQueryRequest {
  readonly planVersionId: string;
  readonly legalEntityId?: string | null;
  readonly from?: string | null;
  readonly to?: string | null;
  readonly metricCode?: string | null;
  readonly page?: number | null;
  readonly pageSize?: number | null;
}

export interface GridCellView {
  readonly coordinateId: string;
  readonly legalEntityId: string;
  readonly periodStart: string;
  readonly metricCode: string;
  readonly dimensionKey: Record<string, unknown>;
  readonly amount?: number | null;
  readonly actualAmount?: number | null;
  readonly calculatedAmount?: number | null;
  readonly sourceLabel?: string | null;
  readonly rowVersion: number;
}

export interface SpreadRequest {
  readonly planVersionId: string;
  readonly legalEntityId: string;
  readonly metricCode: string;
  readonly dimensionKey: Record<string, unknown>;
  readonly total: number;
  readonly method: string;
  readonly targetPeriods: readonly string[];
  readonly weights?: readonly number[] | null;
  readonly currencyCode: string;
}

export interface SpreadCellView {
  readonly periodStart: string;
  readonly amount: number;
  readonly weight: number;
}

export interface CsvImportRequest {
  readonly planVersionId: string;
  readonly csv: string;
  readonly commit?: boolean;
}

export interface XlsxImportRequest {
  readonly planVersionId: string;
  readonly spreadsheetContentBase64: string;
  readonly commit?: boolean;
}

export interface XlsxFileView {
  readonly fileName: string;
  readonly contentType: string;
  readonly spreadsheetContentBase64: string;
  readonly rowCount: number;
  readonly template: boolean;
}

export interface CsvImportView {
  readonly planVersionId: string;
  readonly committed: boolean;
  readonly acceptedRows: number;
  readonly errors: readonly string[];
}

export interface AssumptionView {
  readonly id: string;
  readonly planVersionId: string;
  readonly code: string;
  readonly name: string;
  readonly unit: string;
  readonly description?: string | null;
}

export interface CreateAssumptionRequest {
  readonly planVersionId: string;
  readonly code: string;
  readonly name: string;
  readonly unit: string;
  readonly description?: string | null;
}

export interface AssumptionValueRequest {
  readonly periodStart: string;
  readonly value: number;
  readonly source?: string | null;
}

export interface CalculationRunView {
  readonly id: string;
  readonly planVersionId: string;
  readonly state: string;
  readonly calculatedCells: number;
  readonly completedAt?: string | null;
  readonly errorDetail?: string | null;
}

export interface RollingForecastRequest {
  readonly sourceVersionId: string;
  readonly actualCutoff: string;
  readonly name?: string | null;
  readonly horizonMonths?: number;
}

export interface CreateBaselineRequest {
  readonly planVersionId: string;
  readonly name: string;
  readonly originalTotal: number;
  readonly currencyCode: string;
}

export interface RevisionRequest {
  readonly deltaAmount: number;
  readonly reason: string;
  readonly effectiveFrom?: string | null;
}

export interface TransferRequest {
  readonly fromCoordinateId: string;
  readonly toCoordinateId: string;
  readonly amount: number;
  readonly reason: string;
  readonly fromBucketId?: string | null;
  readonly toBucketId?: string | null;
  readonly effectiveFrom?: string | null;
}

export interface BudgetBucketRequest {
  readonly planVersionId: string;
  readonly policyId: string;
  readonly legalEntityId: string;
  readonly periodStart: string;
  readonly metricCode: string;
  readonly dimensionKey: Record<string, unknown>;
  readonly originalBudget: number;
  readonly currencyCode: string;
}

export interface BudgetCheckRequest {
  readonly bucketId: string;
  readonly proposedAmount: number;
  readonly sourceModule: string;
  readonly sourceDocumentId: string;
  readonly sourceLineId: string;
  readonly sourceVersion: string;
  readonly reserve: boolean;
}

export interface BudgetCheckView {
  readonly decision: string;
  readonly bucketId: string;
  readonly currentBudget: number;
  readonly actual: number;
  readonly commitment: number;
  readonly proposed: number;
  readonly availableBefore: number;
  readonly availableAfter: number;
  readonly thresholdStatus: string;
  readonly reasons: readonly string[];
  readonly reservationId?: string | null;
}

export interface BudgetAvailabilityView {
  readonly bucketId: string;
  readonly currentBudget: number;
  readonly actual: number;
  readonly commitment: number;
  readonly precommitment: number;
  readonly reserved: number;
  readonly available: number;
  readonly mode: string;
  readonly thresholdStatus: string;
}

export interface BudgetOverrideRequest {
  readonly bucketId: string;
  readonly sourceModule: string;
  readonly sourceDocumentId: string;
  readonly sourceLineId: string;
  readonly sourceVersion: string;
  readonly amount: number;
  readonly currencyCode: string;
  readonly reason: string;
  readonly validUntil?: string | null;
}

export interface BudgetOverrideView {
  readonly id: string;
  readonly bucketId: string;
  readonly sourceModule: string;
  readonly sourceDocumentId: string;
  readonly sourceLineId: string;
  readonly sourceVersion: string;
  readonly amount: number;
  readonly usedAmount: number;
  readonly currencyCode: string;
  readonly state: string;
  readonly reason: string;
  readonly approvalRequestId?: string | null;
  readonly requestedBy: string;
  readonly approvedBy?: string | null;
  readonly validUntil?: string | null;
}

export interface BudgetOverrideDecisionRequest {
  readonly decision: string;
  readonly reason?: string | null;
}

export interface BudgetControlReconciliationRequest {
  readonly bucketId?: string | null;
  readonly rebuild?: boolean;
}

export interface BudgetControlReconciliationView {
  readonly id: string;
  readonly bucketId?: string | null;
  readonly state: string;
  readonly rebuildApplied: boolean;
  readonly bucketCount: number;
  readonly discrepancyAmount: number;
  readonly startedAt: string;
  readonly completedAt?: string | null;
  readonly errorDetail?: string | null;
}

export interface WorkforcePlanRequest {
  readonly planVersionId: string;
  readonly legalEntityId: string;
  readonly departmentRef?: string | null;
  readonly roleCode?: string | null;
  readonly periodStart: string;
  readonly fte: number;
  readonly averageCost: number;
  readonly currencyCode: string;
  readonly source?: string | null;
}

export interface WorkforcePlanView {
  readonly id: string;
  readonly planVersionId: string;
  readonly legalEntityId: string;
  readonly departmentRef?: string | null;
  readonly roleCode?: string | null;
  readonly periodStart: string;
  readonly fte: number;
  readonly averageCost: number;
  readonly currencyCode: string;
  readonly source: string;
}

export interface WorkforceCostImpactView {
  readonly planVersionId: string;
  readonly periodStart: string;
  readonly fte: number;
  readonly cost: number;
}

export interface OpexPlanLineRequest {
  readonly planVersionId: string;
  readonly legalEntityId: string;
  readonly departmentRef?: string | null;
  readonly categoryCode: string;
  readonly accountRef?: string | null;
  readonly costCenterRef?: string | null;
  readonly periodStart: string;
  readonly method: "Fixed" | "Variable" | "Recurring" | "DriverBased";
  readonly baseAmount: number;
  readonly driverValue: number;
  readonly rate: number;
  readonly growthPercent: number;
  readonly inflationPercent: number;
  readonly currencyCode: string;
  readonly source?: string | null;
}

export interface OpexPlanLineView {
  readonly id: string;
  readonly planVersionId: string;
  readonly legalEntityId: string;
  readonly departmentRef?: string | null;
  readonly categoryCode: string;
  readonly accountRef?: string | null;
  readonly costCenterRef?: string | null;
  readonly periodStart: string;
  readonly method: string;
  readonly baseAmount: number;
  readonly driverValue: number;
  readonly rate: number;
  readonly growthPercent: number;
  readonly inflationPercent: number;
  readonly plannedAmount: number;
  readonly currencyCode: string;
  readonly source: string;
}

export interface CapexPlanRequest {
  readonly planVersionId: string;
  readonly legalEntityId: string;
  readonly name: string;
  readonly category: string;
  readonly departmentRef?: string | null;
  readonly plannedAmount: number;
  readonly currencyCode: string;
  readonly purchasePeriod: string;
  readonly inServicePeriod?: string | null;
  readonly priority: number;
  readonly justification?: string | null;
  readonly usefulLifeMonths?: number;
}

export interface CapexPlanView {
  readonly id: string;
  readonly planVersionId: string;
  readonly legalEntityId: string;
  readonly name: string;
  readonly category: string;
  readonly plannedAmount: number;
  readonly currencyCode: string;
  readonly purchasePeriod: string;
  readonly inServicePeriod?: string | null;
  readonly priority: number;
  readonly state: string;
  readonly usefulLifeMonths: number;
}

export interface RevenueDriverRequest {
  readonly planVersionId: string;
  readonly legalEntityId: string;
  readonly periodStart: string;
  readonly driverKey: string;
  readonly volume: number;
  readonly price: number;
  readonly currencyCode: string;
  readonly source?: string | null;
}

export interface WorkingCapitalRequest {
  readonly planVersionId: string;
  readonly legalEntityId: string;
  readonly periodStart: string;
  readonly dso: number;
  readonly dpo: number;
  readonly inventoryDays: number;
}

export interface PlanningFxRequest {
  readonly planVersionId: string;
  readonly periodStart: string;
  readonly fromCurrency: string;
  readonly toCurrency: string;
  readonly rate: number;
  readonly rateType?: string | null;
  readonly source: string;
}

export interface AllocationTarget {
  readonly legalEntityId: string;
  readonly departmentRef?: string | null;
  readonly weight: number;
  readonly dimensionKey: Record<string, unknown>;
}

export interface AllocationRequest {
  readonly planVersionId: string;
  readonly periodStart: string;
  readonly metricCode: string;
  readonly sourceAmount: number;
  readonly targets: readonly AllocationTarget[];
  readonly currencyCode: string;
  readonly ruleCode: string;
}

export interface AllocationResultView {
  readonly id: string;
  readonly planVersionId: string;
  readonly periodStart: string;
  readonly legalEntityId: string;
  readonly departmentRef?: string | null;
  readonly amount: number;
  readonly weight: number;
  readonly ruleCode: string;
}

export interface DimensionView {
  readonly id: string;
  readonly modelVersionId: string;
  readonly code: string;
  readonly name: string;
  readonly memberKind: string;
  readonly required: boolean;
  readonly active: boolean;
}

export interface CreateDimensionRequest {
  readonly modelVersionId: string;
  readonly code: string;
  readonly name: string;
  readonly memberKind: string;
  readonly required: boolean;
}

export interface DimensionMemberView {
  readonly id: string;
  readonly dimensionId: string;
  readonly code: string;
  readonly name: string;
  readonly parentCode?: string | null;
  readonly active: boolean;
}

export interface CreateDimensionMemberRequest {
  readonly dimensionId: string;
  readonly code: string;
  readonly name: string;
  readonly parentCode?: string | null;
  readonly sourceReference?: string | null;
}

export interface CreateMetricRequest {
  readonly code: string;
  readonly name: string;
  readonly metricKind: string;
  readonly dataType: string;
  readonly sourcePolicy: string;
  readonly financialRole?: string | null;
  readonly unitCode?: string;
  readonly precisionScale?: number;
  readonly favorableDirection?: string;
}

export interface MetricView {
  readonly id: string;
  readonly code: string;
  readonly name: string;
  readonly metricKind: string;
  readonly dataType: string;
  readonly sourcePolicy: string;
  readonly financialRole?: string | null;
  readonly unitCode: string;
  readonly precisionScale: number;
  readonly favorableDirection: string;
  readonly active: boolean;
}

export interface CreateMetricMappingRequest {
  readonly mappingVersion: string;
  readonly sourceModule: string;
  readonly sourceType: string;
  readonly sourceKey: string;
  readonly targetMetricCode: string;
  readonly legalEntityId?: string | null;
  readonly effectiveFrom?: string | null;
  readonly effectiveTo?: string | null;
}

export interface MetricMappingView {
  readonly id: string;
  readonly mappingVersion: string;
  readonly sourceModule: string;
  readonly sourceType: string;
  readonly sourceKey: string;
  readonly targetMetricCode: string;
  readonly legalEntityId?: string | null;
  readonly state: string;
}

export interface CreateFormulaRequest {
  readonly modelVersionId: string;
  readonly code: string;
  readonly expression: string;
  readonly outputMetricCode: string;
  readonly dependencies: readonly string[];
}

export interface FormulaView {
  readonly id: string;
  readonly modelVersionId: string;
  readonly code: string;
  readonly expression: string;
  readonly outputMetricCode: string;
  readonly dependencies: readonly string[];
  readonly active: boolean;
}

export interface SubmissionUnitView {
  readonly id: string;
  readonly planVersionId: string;
  readonly legalEntityId: string;
  readonly departmentRef?: string | null;
  readonly name: string;
  readonly state: string;
  readonly rowVersion: number;
  readonly approvalRequestId?: string | null;
}

export interface CreateSubmissionUnitRequest {
  readonly planVersionId: string;
  readonly legalEntityId: string;
  readonly departmentRef?: string | null;
  readonly name: string;
  readonly assignedTo?: string | null;
  readonly coordinateIds?: readonly string[] | null;
}

export interface CreatePlanningTargetRequest {
  readonly planVersionId: string;
  readonly legalEntityId: string;
  readonly periodStart: string;
  readonly metricCode: string;
  readonly targetAmount: number;
  readonly currencyCode: string;
  readonly reason?: string | null;
}

export interface CreatePlanningProposalRequest {
  readonly planVersionId: string;
  readonly legalEntityId: string;
  readonly departmentRef?: string | null;
  readonly periodStart: string;
  readonly metricCode: string;
  readonly proposedAmount: number;
  readonly currencyCode: string;
  readonly reason?: string | null;
}

export interface AdjustPlanningProposalRequest {
  readonly adjustedAmount: number;
  readonly reason: string;
}

export interface PlanningTargetView {
  readonly id: string;
  readonly planVersionId: string;
  readonly legalEntityId: string;
  readonly periodStart: string;
  readonly metricCode: string;
  readonly targetAmount: number;
  readonly proposalAmount?: number | null;
  readonly gapAmount?: number | null;
  readonly adjustedAmount?: number | null;
  readonly currencyCode: string;
  readonly state: string;
  readonly proposalId?: string | null;
}

export interface StatementRequest {
  readonly planVersionId: string;
  readonly legalEntityId: string;
  readonly from: string;
  readonly to: string;
  readonly statementType: string;
  readonly currencyCode: string;
}

export interface StatementLineView {
  readonly statementType: string;
  readonly lineCode: string;
  readonly periodStart: string;
  readonly amount: number;
  readonly sourceAmount: number;
  readonly sourceBasis: string;
  readonly balanced: boolean;
}

export interface AccuracyRequest {
  readonly forecastVersionId: string;
  readonly actualVersionId: string;
  readonly from: string;
  readonly to: string;
  readonly metricCode: string;
}

export interface ForecastAccuracyView {
  readonly forecastVersionId: string;
  readonly actualVersionId: string;
  readonly metricCode: string;
  readonly forecastTotal: number;
  readonly actualTotal: number;
  readonly absoluteError: number;
  readonly percentageError: number;
  readonly bias: number;
}

export interface ActualRefreshRequest {
  readonly mappingVersion: string;
  readonly sourceWatermark: string;
  readonly planVersionId?: string | null;
  readonly actualThrough?: string | null;
}

export interface ActualRefreshRunView {
  readonly id: string;
  readonly state: string;
  readonly mappingVersion: string;
  readonly sourceWatermark?: string | null;
  readonly startedAt: string;
  readonly completedAt?: string | null;
  readonly errorDetail?: string | null;
}

export interface ActualBasisView {
  readonly planVersionId: string;
  readonly snapshotId: string;
  readonly mappingVersion: string;
  readonly sourceWatermark: string;
  readonly actualThrough: string;
  readonly currencyBasis: string;
  readonly state: string;
  readonly publishedAt?: string | null;
  readonly pinned: boolean;
}

export interface VarianceQueryRequest {
  readonly planVersionId: string;
  readonly legalEntityId: string;
  readonly from?: string | null;
  readonly to?: string | null;
  readonly metricCode?: string | null;
}

export interface VarianceView {
  readonly coordinateId: string;
  readonly legalEntityId: string;
  readonly periodStart: string;
  readonly metricCode: string;
  readonly budgetAmount: number;
  readonly forecastAmount: number;
  readonly actualAmount: number;
  readonly varianceAmount: number;
  readonly materiality: number;
  readonly favorability: string;
}

export interface VarianceDriverView {
  readonly coordinateId: string;
  readonly driverCode: string;
  readonly basis: string;
  readonly amount: number;
}

export interface VarianceDecompositionView {
  readonly coordinateId: string;
  readonly legalEntityId: string;
  readonly periodStart: string;
  readonly metricCode: string;
  readonly budgetAmount: number;
  readonly forecastAmount: number;
  readonly actualAmount: number;
  readonly budgetToForecast: number;
  readonly forecastToActual: number;
  readonly budgetToActual: number;
  readonly materiality: number;
  readonly drivers: readonly VarianceDriverView[];
}

export interface CfoOverviewRequest {
  readonly planVersionId: string;
  readonly legalEntityIds: readonly string[];
  readonly from: string;
  readonly to: string;
  readonly reportingCurrencyCode: string;
}

export interface CfoMetricView {
  readonly metricCode: string;
  readonly forecastAmount: number;
  readonly actualAmount: number;
  readonly varianceAmount: number;
  readonly reportingForecastAmount: number;
  readonly reportingActualAmount: number;
  readonly reportingVarianceAmount: number;
  readonly reportingCurrencyCode: string;
  readonly sourceBasis: string;
}

export interface RollupLineView {
  readonly coordinateId: string;
  readonly legalEntityId: string;
  readonly periodStart: string;
  readonly metricCode: string;
  readonly sourceAmount: number;
  readonly rate: number;
  readonly reportingAmount: number;
  readonly sourceCurrency: string;
  readonly reportingCurrency: string;
  readonly sourceBasis: string;
}

export interface CfoOverviewView {
  readonly planVersionId: string;
  readonly reportingCurrencyCode: string;
  readonly metrics: readonly CfoMetricView[];
  readonly rollupLines: readonly RollupLineView[];
}

export interface DepartmentPlannerRequest {
  readonly planVersionId: string;
  readonly legalEntityId: string;
  readonly departmentRef?: string | null;
  readonly from?: string | null;
  readonly to?: string | null;
}

export interface DepartmentPlannerLineView {
  readonly planVersionId: string;
  readonly legalEntityId: string;
  readonly departmentRef?: string | null;
  readonly periodStart: string;
  readonly metricCode: string;
  readonly budgetAmount: number;
  readonly forecastAmount: number;
  readonly actualAmount: number;
  readonly varianceAmount: number;
  readonly materiality: number;
  readonly commitmentAmount: number;
  readonly availableAmount: number;
  readonly submissionState: string;
  readonly coordinateIds: readonly string[];
}

export interface CompareVersionView {
  readonly legalEntityId: string;
  readonly periodStart: string;
  readonly metricCode: string;
  readonly leftAmount: number;
  readonly rightAmount: number;
  readonly difference: number;
}

export interface RollupRequest {
  readonly planVersionId: string;
  readonly legalEntityIds: readonly string[];
  readonly from: string;
  readonly to: string;
  readonly metricCode: string;
  readonly reportingCurrencyCode: string;
}

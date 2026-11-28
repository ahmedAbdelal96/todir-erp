import { httpClient } from "../../../core/api/http-client.ts";
import type {
  AccuracyRequest,
  ActualBasisView,
  ActualRefreshRequest,
  ActualRefreshRunView,
  AdjustPlanningProposalRequest,
  AllocationRequest,
  AllocationResultView,
  AssumptionValueRequest,
  AssumptionView,
  BudgetAvailabilityView,
  BudgetBucketRequest,
  BudgetCheckRequest,
  BudgetCheckView,
  BudgetControlReconciliationRequest,
  BudgetControlReconciliationView,
  BudgetOverrideDecisionRequest,
  BudgetOverrideRequest,
  BudgetOverrideView,
  CalculationRunView,
  CapexPlanRequest,
  CapexPlanView,
  CfoOverviewRequest,
  CfoOverviewView,
  CompareVersionView,
  CreateAssumptionRequest,
  CreateBaselineRequest,
  CreateDimensionMemberRequest,
  CreateDimensionRequest,
  CreateFormulaRequest,
  CreateMetricMappingRequest,
  CreateMetricRequest,
  CreateModelVersionRequest,
  CreatePlanningCycleRequest,
  CreatePlanningModelRequest,
  CreatePlanningProposalRequest,
  CreatePlanningTargetRequest,
  CreatePlanVersionRequest,
  CreateScenarioRequest,
  CreateSubmissionUnitRequest,
  CsvImportRequest,
  CsvImportView,
  DepartmentPlannerLineView,
  DepartmentPlannerRequest,
  DimensionMemberView,
  DimensionView,
  ForecastAccuracyView,
  FormulaView,
  GridCellRequest,
  GridCellView,
  GridQueryRequest,
  GridWriteRequest,
  MetricMappingView,
  MetricView,
  ModelVersionView,
  OpexPlanLineRequest,
  OpexPlanLineView,
  PlanningCycleView,
  PlanningFxRequest,
  PlanningModelView,
  PlanningTargetView,
  PlanVersionView,
  RevenueDriverRequest,
  RevisionRequest,
  RollingForecastRequest,
  RollupLineView,
  RollupRequest,
  ScenarioAdoptRequest,
  ScenarioView,
  SpreadCellView,
  SpreadRequest,
  StatementLineView,
  StatementRequest,
  SubmissionUnitView,
  TransferRequest,
  VarianceDecompositionView,
  VarianceDriverView,
  VarianceQueryRequest,
  VarianceView,
  WorkforceCostImpactView,
  WorkforcePlanRequest,
  WorkforcePlanView,
  WorkingCapitalRequest,
  XlsxFileView,
  XlsxImportRequest
} from "../types.ts";

const BASE = "/api/v1/planning";

function postJson<T>(url: string, body?: unknown): Promise<T> {
  return httpClient<T>(
    url,
    body !== undefined
      ? { method: "POST", body: JSON.stringify(body) }
      : { method: "POST" }
  );
}

export const planningApi = {
  // Cycles
  listCycles(legalEntityId?: string | null): Promise<PlanningCycleView[]> {
    const qs = legalEntityId ? `?legalEntityId=${encodeURIComponent(legalEntityId)}` : "";
    return httpClient<PlanningCycleView[]>(`${BASE}/cycles${qs}`);
  },
  createCycle(request: CreatePlanningCycleRequest): Promise<PlanningCycleView> {
    return postJson<PlanningCycleView>(`${BASE}/cycles`, request);
  },
  advanceCycle(id: string, action: "open" | "submit" | "approve" | "publish" | "close" | "archive"): Promise<PlanningCycleView> {
    return postJson<PlanningCycleView>(`${BASE}/cycles/${id}/${action}`);
  },

  // Models, Versions, Metrics, Dimensions, Formulas
  createModel(request: CreatePlanningModelRequest): Promise<PlanningModelView> {
    return postJson<PlanningModelView>(`${BASE}/models`, request);
  },
  createModelVersion(request: CreateModelVersionRequest): Promise<ModelVersionView> {
    return postJson<ModelVersionView>(`${BASE}/model-versions`, request);
  },
  createMetric(request: CreateMetricRequest): Promise<MetricView> {
    return postJson<MetricView>(`${BASE}/metrics`, request);
  },
  createMetricMapping(request: CreateMetricMappingRequest): Promise<MetricMappingView> {
    return postJson<MetricMappingView>(`${BASE}/metric-mappings`, request);
  },
  advanceMetricMapping(id: string, action: "publish" | "retire"): Promise<MetricMappingView> {
    return postJson<MetricMappingView>(`${BASE}/metric-mappings/${id}/${action}`);
  },
  listDimensions(modelVersionId: string): Promise<DimensionView[]> {
    return httpClient<DimensionView[]>(`${BASE}/dimensions?modelVersionId=${encodeURIComponent(modelVersionId)}`);
  },
  createDimension(request: CreateDimensionRequest): Promise<DimensionView> {
    return postJson<DimensionView>(`${BASE}/dimensions`, request);
  },
  listDimensionMembers(dimensionId: string): Promise<DimensionMemberView[]> {
    return httpClient<DimensionMemberView[]>(`${BASE}/dimensions/${dimensionId}/members`);
  },
  addDimensionMember(dimensionId: string, request: CreateDimensionMemberRequest): Promise<DimensionMemberView> {
    return postJson<DimensionMemberView>(`${BASE}/dimensions/${dimensionId}/members`, request);
  },
  createFormula(request: CreateFormulaRequest): Promise<FormulaView> {
    return postJson<FormulaView>(`${BASE}/formulas`, request);
  },

  // Plan Versions & Calculations & Forecasts
  listPlanVersions(): Promise<PlanVersionView[]> {
    return httpClient<PlanVersionView[]>(`${BASE}/versions`);
  },
  createPlanVersion(request: CreatePlanVersionRequest): Promise<PlanVersionView> {
    return postJson<PlanVersionView>(`${BASE}/versions`, request);
  },
  clonePlanVersion(id: string, name: string): Promise<PlanVersionView> {
    return postJson<PlanVersionView>(`${BASE}/versions/${id}/clone?name=${encodeURIComponent(name)}`);
  },
  advancePlanVersion(id: string, action: "submit" | "approve" | "publish" | "close"): Promise<PlanVersionView> {
    return postJson<PlanVersionView>(`${BASE}/versions/${id}/${action}`);
  },
  compareVersions(id: string, otherVersionId: string, legalEntityId: string, from?: string | null, to?: string | null, metricCode?: string | null): Promise<CompareVersionView[]> {
    const params = new URLSearchParams({ otherVersionId, legalEntityId });
    if (from) params.set("from", from);
    if (to) params.set("to", to);
    if (metricCode) params.set("metricCode", metricCode);
    return httpClient<CompareVersionView[]>(`${BASE}/versions/${id}/compare?${params.toString()}`);
  },
  recalculate(id: string): Promise<CalculationRunView> {
    return postJson<CalculationRunView>(`${BASE}/versions/${id}/recalculate`);
  },
  retryCalculation(runId: string): Promise<CalculationRunView> {
    return postJson<CalculationRunView>(`${BASE}/calculation-runs/${runId}/retry`);
  },
  rollForecast(request: RollingForecastRequest): Promise<PlanVersionView> {
    return postJson<PlanVersionView>(`${BASE}/forecasts/roll`, request);
  },
  calculateForecastAccuracy(request: AccuracyRequest): Promise<ForecastAccuracyView> {
    return postJson<ForecastAccuracyView>(`${BASE}/forecast-accuracy`, request);
  },

  // Scenarios
  createScenario(request: CreateScenarioRequest): Promise<ScenarioView> {
    return postJson<ScenarioView>(`${BASE}/scenarios`, request);
  },
  cloneScenario(id: string, name: string): Promise<ScenarioView> {
    return postJson<ScenarioView>(`${BASE}/scenarios/${id}/clone?name=${encodeURIComponent(name)}`);
  },
  shareScenario(id: string, userId: string, accessLevel: "Read" | "Edit" | "Adopt"): Promise<ScenarioView> {
    const params = new URLSearchParams({ userId, accessLevel });
    return postJson<ScenarioView>(`${BASE}/scenarios/${id}/share?${params.toString()}`);
  },
  adoptScenario(id: string, request: ScenarioAdoptRequest): Promise<PlanVersionView> {
    return postJson<PlanVersionView>(`${BASE}/scenarios/${id}/adopt-preview`, request);
  },

  // Grid & Spread & Import/Export
  queryGrid(request: GridQueryRequest): Promise<GridCellView[]> {
    return postJson<GridCellView[]>(`${BASE}/grids/query`, request);
  },
  writeGridBatch(request: GridWriteRequest): Promise<GridCellView[]> {
    return postJson<GridCellView[]>(`${BASE}/grids/write-batch`, request);
  },
  spreadExact(request: SpreadRequest): Promise<SpreadCellView[]> {
    return postJson<SpreadCellView[]>(`${BASE}/grids/spread-exact`, request);
  },
  pastePreview(request: GridWriteRequest): Promise<GridCellRequest[]> {
    return postJson<GridCellRequest[]>(`${BASE}/grids/paste-preview`, request);
  },
  importCsvPreview(request: CsvImportRequest): Promise<CsvImportView> {
    return postJson<CsvImportView>(`${BASE}/grid/import/preview`, { ...request, commit: false });
  },
  importCsvCommit(request: CsvImportRequest): Promise<CsvImportView> {
    return postJson<CsvImportView>(`${BASE}/grid/import`, { ...request, commit: true });
  },
  importXlsxPreview(request: XlsxImportRequest): Promise<CsvImportView> {
    return postJson<CsvImportView>(`${BASE}/grid/import/xlsx/preview`, { ...request, commit: false });
  },
  importXlsxCommit(request: XlsxImportRequest): Promise<CsvImportView> {
    return postJson<CsvImportView>(`${BASE}/grid/import/xlsx`, { ...request, commit: true });
  },
  exportXlsx(planVersionId: string, legalEntityId?: string | null, from?: string | null, to?: string | null, template = false): Promise<XlsxFileView> {
    const params = new URLSearchParams({ planVersionId });
    if (legalEntityId) params.set("legalEntityId", legalEntityId);
    if (from) params.set("from", from);
    if (to) params.set("to", to);
    if (template) params.set("template", "true");
    return httpClient<XlsxFileView>(`${BASE}/grid/export/xlsx?${params.toString()}`);
  },
  downloadTemplateXlsx(planVersionId: string): Promise<XlsxFileView> {
    return httpClient<XlsxFileView>(`${BASE}/grid/template/xlsx?planVersionId=${encodeURIComponent(planVersionId)}`);
  },

  // Assumptions & Allocations
  listAssumptions(planVersionId: string): Promise<AssumptionView[]> {
    return httpClient<AssumptionView[]>(`${BASE}/assumptions?planVersionId=${encodeURIComponent(planVersionId)}`);
  },
  createAssumption(request: CreateAssumptionRequest): Promise<AssumptionView> {
    return postJson<AssumptionView>(`${BASE}/assumptions`, request);
  },
  setAssumptionValue(id: string, request: AssumptionValueRequest): Promise<{ saved: boolean }> {
    return postJson<{ saved: boolean }>(`${BASE}/assumptions/${id}/values`, request);
  },
  allocate(request: AllocationRequest): Promise<AllocationResultView[]> {
    return postJson<AllocationResultView[]>(`${BASE}/allocations`, request);
  },

  // Budgets, Baselines, Revisions, Transfers, Control
  createBaseline(request: CreateBaselineRequest): Promise<{ id: string; name: string; state: string; originalTotal: number; currencyCode: string }> {
    return postJson(`${BASE}/budget/baselines`, request);
  },
  advanceBaseline(id: string, action: "approve" | "publish" | "close"): Promise<{ id: string; state: string }> {
    return postJson(`${BASE}/budget/baselines/${id}/${action}`);
  },
  approveRevision(baselineId: string, request: RevisionRequest): Promise<{ approved: boolean }> {
    return postJson<{ approved: boolean }>(`${BASE}/budget/baselines/${baselineId}/revisions`, request);
  },
  getCurrentBudget(baselineId: string, asOfDate?: string | null): Promise<{ baselineId: string; originalTotal: number; revisionTotal: number; transferNet: number; currentBudget: number; currencyCode: string }> {
    const qs = asOfDate ? `?asOfDate=${encodeURIComponent(asOfDate)}` : "";
    return httpClient(`${BASE}/budget/baselines/${baselineId}/current${qs}`);
  },
  createBudgetPolicy(legalEntityId: string, name: string, mode: string, basis: string, thresholdPercent: number): Promise<{ id: string }> {
    const params = new URLSearchParams({ legalEntityId, name, mode, basis, thresholdPercent: String(thresholdPercent) });
    return postJson(`${BASE}/budget/policies?${params.toString()}`);
  },
  createBudgetBucket(request: BudgetBucketRequest): Promise<{ id: string }> {
    return postJson(`${BASE}/budget/buckets`, request);
  },
  createBudgetTransfer(budgetId: string, request: TransferRequest): Promise<{ id: string; state: string }> {
    return postJson(`${BASE}/budgets/${budgetId}/transfers`, request);
  },
  approveBudgetTransfer(transferId: string): Promise<{ id: string; state: string }> {
    return postJson(`${BASE}/budget-transfers/${transferId}/approve`);
  },
  checkBudget(request: BudgetCheckRequest): Promise<BudgetCheckView> {
    return postJson<BudgetCheckView>(`${BASE}/budget/check`, { ...request, reserve: false });
  },
  checkAndReserveBudget(request: BudgetCheckRequest): Promise<BudgetCheckView> {
    return postJson<BudgetCheckView>(`${BASE}/budget/check-and-reserve`, { ...request, reserve: true });
  },
  getBucketAvailability(bucketId: string): Promise<BudgetAvailabilityView> {
    return httpClient<BudgetAvailabilityView>(`${BASE}/budget/buckets/${bucketId}/availability`);
  },
  releaseReservation(id: string): Promise<{ released: boolean }> {
    return postJson<{ released: boolean }>(`${BASE}/budget/reservations/${id}/release`);
  },
  adjustReservation(id: string, amount: number): Promise<{ id: string; reservedAmount: number }> {
    return postJson(`${BASE}/budget/reservations/${id}/adjust`, { amount });
  },
  requestBudgetOverride(request: BudgetOverrideRequest): Promise<BudgetOverrideView> {
    return postJson<BudgetOverrideView>(`${BASE}/budget/overrides`, request);
  },
  getBudgetOverride(id: string): Promise<BudgetOverrideView> {
    return httpClient<BudgetOverrideView>(`${BASE}/budget/overrides/${id}`);
  },
  decideBudgetOverride(id: string, request: BudgetOverrideDecisionRequest): Promise<BudgetOverrideView> {
    return postJson<BudgetOverrideView>(`${BASE}/budget/overrides/${id}/decision`, request);
  },
  reconcileBudgetControl(request: BudgetControlReconciliationRequest): Promise<BudgetControlReconciliationView> {
    return postJson<BudgetControlReconciliationView>(`${BASE}/budget-control/reconcile`, request);
  },

  // Sub-ledger Planning Models (Workforce, OPEX, CapEx, Revenue, Working Capital, FX)
  listWorkforce(planVersionId: string): Promise<WorkforcePlanView[]> {
    return httpClient<WorkforcePlanView[]>(`${BASE}/workforce?planVersionId=${encodeURIComponent(planVersionId)}`);
  },
  addWorkforce(request: WorkforcePlanRequest): Promise<WorkforcePlanView> {
    return postJson<WorkforcePlanView>(`${BASE}/workforce`, request);
  },
  importWorkforceBaseline(lines: readonly WorkforcePlanRequest[]): Promise<WorkforcePlanView[]> {
    return postJson<WorkforcePlanView[]>(`${BASE}/workforce/import-baseline`, lines);
  },
  getWorkforceCostImpact(planVersionId: string, from?: string | null, to?: string | null): Promise<WorkforceCostImpactView[]> {
    const params = new URLSearchParams({ planVersionId });
    if (from) params.set("from", from);
    if (to) params.set("to", to);
    return httpClient<WorkforceCostImpactView[]>(`${BASE}/workforce/cost-impact?${params.toString()}`);
  },
  listOpexLines(planVersionId: string, legalEntityId?: string | null, from?: string | null, to?: string | null): Promise<OpexPlanLineView[]> {
    const params = new URLSearchParams({ planVersionId });
    if (legalEntityId) params.set("legalEntityId", legalEntityId);
    if (from) params.set("from", from);
    if (to) params.set("to", to);
    return httpClient<OpexPlanLineView[]>(`${BASE}/opex?${params.toString()}`);
  },
  addOpexLine(request: OpexPlanLineRequest): Promise<OpexPlanLineView> {
    return postJson<OpexPlanLineView>(`${BASE}/opex`, request);
  },
  listCapex(planVersionId: string): Promise<CapexPlanView[]> {
    return httpClient<CapexPlanView[]>(`${BASE}/capex?planVersionId=${encodeURIComponent(planVersionId)}`);
  },
  addCapex(request: CapexPlanRequest): Promise<CapexPlanView> {
    return postJson<CapexPlanView>(`${BASE}/capex`, request);
  },
  prioritizeCapex(id: string, priority: number): Promise<CapexPlanView> {
    return postJson<CapexPlanView>(`${BASE}/capex/${id}/prioritize?priority=${priority}`);
  },
  approveCapexBudget(id: string): Promise<CapexPlanView> {
    return postJson<CapexPlanView>(`${BASE}/capex/${id}/approve-budget`);
  },
  addRevenueDriver(request: RevenueDriverRequest): Promise<{ saved: boolean }> {
    return postJson<{ saved: boolean }>(`${BASE}/revenue`, request);
  },
  addWorkingCapital(request: WorkingCapitalRequest): Promise<{ saved: boolean }> {
    return postJson<{ saved: boolean }>(`${BASE}/working-capital`, request);
  },
  addPlanningFx(request: PlanningFxRequest): Promise<{ saved: boolean }> {
    return postJson<{ saved: boolean }>(`${BASE}/fx`, request);
  },

  // Submissions, Targets, Proposals
  listSubmissionUnits(planVersionId?: string | null): Promise<SubmissionUnitView[]> {
    const qs = planVersionId ? `?planVersionId=${encodeURIComponent(planVersionId)}` : "";
    return httpClient<SubmissionUnitView[]>(`${BASE}/submissions${qs}`);
  },
  createSubmissionUnit(request: CreateSubmissionUnitRequest): Promise<SubmissionUnitView> {
    return postJson<SubmissionUnitView>(`${BASE}/submissions`, request);
  },
  attachSubmissionCoordinates(id: string, coordinateIds: readonly string[]): Promise<{ attached: number }> {
    return postJson<{ attached: number }>(`${BASE}/submissions/${id}/coordinates`, { coordinateIds });
  },
  transitionSubmission(id: string, action: "submit" | "approve" | "return" | "lock", reason?: string | null): Promise<SubmissionUnitView> {
    return postJson<SubmissionUnitView>(`${BASE}/submissions/${id}/transition`, { action, reason: reason ?? null });
  },
  createTarget(request: CreatePlanningTargetRequest): Promise<PlanningTargetView> {
    return postJson<PlanningTargetView>(`${BASE}/targets`, request);
  },
  advanceTarget(id: string, action: "approve" | "publish" | "close"): Promise<PlanningTargetView> {
    return postJson<PlanningTargetView>(`${BASE}/targets/${id}/${action}`);
  },
  createProposal(request: CreatePlanningProposalRequest): Promise<PlanningTargetView> {
    return postJson<PlanningTargetView>(`${BASE}/targets/proposals`, request);
  },
  adjustProposal(id: string, request: AdjustPlanningProposalRequest): Promise<PlanningTargetView> {
    return postJson<PlanningTargetView>(`${BASE}/targets/proposals/${id}/adjust`, request);
  },
  advanceProposal(id: string, action: "submit" | "approve" | "reject"): Promise<PlanningTargetView> {
    return postJson<PlanningTargetView>(`${BASE}/targets/proposals/${id}/${action}`);
  },

  // Reporting, Statements, Variance, Actuals Refresh
  queryStatements(request: StatementRequest): Promise<StatementLineView[]> {
    return postJson<StatementLineView[]>(`${BASE}/statements/query`, request);
  },
  queryRollup(request: RollupRequest): Promise<RollupLineView[]> {
    return postJson<RollupLineView[]>(`${BASE}/reports/rollup`, request);
  },
  queryDepartmentPlanner(request: DepartmentPlannerRequest): Promise<DepartmentPlannerLineView[]> {
    return postJson<DepartmentPlannerLineView[]>(`${BASE}/reports/department-planner`, request);
  },
  queryCfoOverview(request: CfoOverviewRequest): Promise<CfoOverviewView> {
    return postJson<CfoOverviewView>(`${BASE}/reports/cfo`, request);
  },
  refreshActuals(request: ActualRefreshRequest): Promise<ActualRefreshRunView> {
    return postJson<ActualRefreshRunView>(`${BASE}/actuals/refresh`, request);
  },
  getActualRefreshRun(id: string): Promise<ActualRefreshRunView> {
    return httpClient<ActualRefreshRunView>(`${BASE}/actuals/refresh-runs/${id}`);
  },
  getActualBasis(planVersionId: string): Promise<ActualBasisView> {
    return httpClient<ActualBasisView>(`${BASE}/versions/${planVersionId}/actual-basis`);
  },
  queryVariance(request: VarianceQueryRequest): Promise<VarianceView[]> {
    return postJson<VarianceView[]>(`${BASE}/variance/query`, request);
  },
  getVarianceDrivers(coordinateId: string): Promise<VarianceDriverView[]> {
    return httpClient<VarianceDriverView[]>(`${BASE}/variance/${coordinateId}/drivers`);
  },
  getVarianceDecomposition(coordinateId: string): Promise<VarianceDecompositionView> {
    return httpClient<VarianceDecompositionView>(`${BASE}/variance/${coordinateId}/decomposition`);
  },
  addVarianceCommentary(planVersionId: string, coordinateId: string | null, body: string): Promise<{ saved: boolean }> {
    return postJson<{ saved: boolean }>(`${BASE}/variance/commentary`, { planVersionId, coordinateId, body });
  }
};

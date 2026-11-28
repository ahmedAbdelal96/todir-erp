import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { planningApi } from "../services/planning-api.ts";
import type {
  AccuracyRequest,
  ActualRefreshRequest,
  AdjustPlanningProposalRequest,
  AllocationRequest,
  AssumptionValueRequest,
  BudgetBucketRequest,
  BudgetCheckRequest,
  BudgetControlReconciliationRequest,
  BudgetOverrideDecisionRequest,
  BudgetOverrideRequest,
  CapexPlanRequest,
  CfoOverviewRequest,
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
  DepartmentPlannerRequest,
  GridQueryRequest,
  GridWriteRequest,
  OpexPlanLineRequest,
  PlanningFxRequest,
  RevenueDriverRequest,
  RevisionRequest,
  RollingForecastRequest,
  RollupRequest,
  ScenarioAdoptRequest,
  SpreadRequest,
  StatementRequest,
  TransferRequest,
  VarianceQueryRequest,
  WorkforcePlanRequest,
  WorkingCapitalRequest,
  XlsxImportRequest
} from "../types.ts";

export const PLANNING_QUERY_KEYS = {
  all: ["planning"] as const,
  cycles: (legalEntityId?: string | null) => [...PLANNING_QUERY_KEYS.all, "cycles", legalEntityId ?? "all"] as const,
  versions: () => [...PLANNING_QUERY_KEYS.all, "versions"] as const,
  grid: (req: GridQueryRequest) => [...PLANNING_QUERY_KEYS.all, "grid", req.planVersionId, req.legalEntityId ?? "all", req.from ?? "", req.to ?? "", req.metricCode ?? "", req.page ?? 1] as const,
  assumptions: (planVersionId: string) => [...PLANNING_QUERY_KEYS.all, "assumptions", planVersionId] as const,
  submissions: (planVersionId?: string | null) => [...PLANNING_QUERY_KEYS.all, "submissions", planVersionId ?? "all"] as const,
  dimensions: (modelVersionId: string) => [...PLANNING_QUERY_KEYS.all, "dimensions", modelVersionId] as const,
  dimensionMembers: (dimensionId: string) => [...PLANNING_QUERY_KEYS.all, "dimensionMembers", dimensionId] as const,
  workforce: (planVersionId: string) => [...PLANNING_QUERY_KEYS.all, "workforce", planVersionId] as const,
  workforceCostImpact: (planVersionId: string) => [...PLANNING_QUERY_KEYS.all, "workforceCostImpact", planVersionId] as const,
  opex: (planVersionId: string, legalEntityId?: string | null) => [...PLANNING_QUERY_KEYS.all, "opex", planVersionId, legalEntityId ?? "all"] as const,
  capex: (planVersionId: string) => [...PLANNING_QUERY_KEYS.all, "capex", planVersionId] as const,
  actualBasis: (planVersionId: string) => [...PLANNING_QUERY_KEYS.all, "actualBasis", planVersionId] as const,
  actualRefreshRun: (runId: string) => [...PLANNING_QUERY_KEYS.all, "actualRefreshRun", runId] as const,
  variance: (req: VarianceQueryRequest) => [...PLANNING_QUERY_KEYS.all, "variance", req.planVersionId, req.legalEntityId, req.from ?? "", req.to ?? "", req.metricCode ?? ""] as const,
  varianceDrivers: (coordinateId: string) => [...PLANNING_QUERY_KEYS.all, "varianceDrivers", coordinateId] as const,
  varianceDecomposition: (coordinateId: string) => [...PLANNING_QUERY_KEYS.all, "varianceDecomposition", coordinateId] as const,
  statements: (req: StatementRequest) => [...PLANNING_QUERY_KEYS.all, "statements", req.planVersionId, req.legalEntityId, req.statementType, req.from, req.to, req.currencyCode] as const,
  cfoOverview: (req: CfoOverviewRequest) => [...PLANNING_QUERY_KEYS.all, "cfo", req.planVersionId, req.legalEntityIds.join(","), req.from, req.to, req.reportingCurrencyCode] as const,
  departmentPlanner: (req: DepartmentPlannerRequest) => [...PLANNING_QUERY_KEYS.all, "departmentPlanner", req.planVersionId, req.legalEntityId, req.departmentRef ?? "all"] as const,
  rollup: (req: RollupRequest) => [...PLANNING_QUERY_KEYS.all, "rollup", req.planVersionId, req.legalEntityIds.join(","), req.metricCode, req.reportingCurrencyCode] as const,
  compareVersions: (id: string, otherId: string, legalEntityId: string) => [...PLANNING_QUERY_KEYS.all, "compare", id, otherId, legalEntityId] as const,
  currentBudget: (baselineId: string) => [...PLANNING_QUERY_KEYS.all, "currentBudget", baselineId] as const,
  bucketAvailability: (bucketId: string) => [...PLANNING_QUERY_KEYS.all, "bucketAvailability", bucketId] as const,
  budgetOverride: (overrideId: string) => [...PLANNING_QUERY_KEYS.all, "budgetOverride", overrideId] as const
};

export function usePlanningCycles(legalEntityId?: string | null, enabled = true) {
  return useQuery({
    queryKey: PLANNING_QUERY_KEYS.cycles(legalEntityId),
    queryFn: () => planningApi.listCycles(legalEntityId),
    enabled
  });
}

export function useCreatePlanningCycle() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (req: CreatePlanningCycleRequest) => planningApi.createCycle(req),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: PLANNING_QUERY_KEYS.all });
    }
  });
}

export function useAdvancePlanningCycle() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, action }: { id: string; action: "open" | "submit" | "approve" | "publish" | "close" | "archive" }) =>
      planningApi.advanceCycle(id, action),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: PLANNING_QUERY_KEYS.all });
    }
  });
}

export function usePlanVersions(enabled = true) {
  return useQuery({
    queryKey: PLANNING_QUERY_KEYS.versions(),
    queryFn: () => planningApi.listPlanVersions(),
    enabled
  });
}

export function useCreatePlanVersion() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (req: CreatePlanVersionRequest) => planningApi.createPlanVersion(req),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: PLANNING_QUERY_KEYS.versions() });
    }
  });
}

export function useClonePlanVersion() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, name }: { id: string; name: string }) => planningApi.clonePlanVersion(id, name),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: PLANNING_QUERY_KEYS.versions() });
    }
  });
}

export function useAdvancePlanVersion() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, action }: { id: string; action: "submit" | "approve" | "publish" | "close" }) =>
      planningApi.advancePlanVersion(id, action),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: PLANNING_QUERY_KEYS.all });
    }
  });
}

export function useCompareVersions(id: string, otherVersionId: string, legalEntityId: string, from?: string | null, to?: string | null, metricCode?: string | null) {
  return useQuery({
    queryKey: PLANNING_QUERY_KEYS.compareVersions(id, otherVersionId, legalEntityId),
    queryFn: () => planningApi.compareVersions(id, otherVersionId, legalEntityId, from, to, metricCode),
    enabled: Boolean(id && otherVersionId && legalEntityId)
  });
}

export function usePlanningGrid(req: GridQueryRequest, enabled = true) {
  return useQuery({
    queryKey: PLANNING_QUERY_KEYS.grid(req),
    queryFn: () => planningApi.queryGrid(req),
    enabled: enabled && Boolean(req.planVersionId)
  });
}

export function useWriteGridBatch() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (req: GridWriteRequest) => planningApi.writeGridBatch(req),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: PLANNING_QUERY_KEYS.all });
    }
  });
}

export function useSpreadExact() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (req: SpreadRequest) => planningApi.spreadExact(req),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: PLANNING_QUERY_KEYS.all });
    }
  });
}

export function useRecalculatePlanVersion() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (planVersionId: string) => planningApi.recalculate(planVersionId),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: PLANNING_QUERY_KEYS.all });
    }
  });
}

export function useRetryCalculationRun() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (runId: string) => planningApi.retryCalculation(runId),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: PLANNING_QUERY_KEYS.all });
    }
  });
}

export function useRollForecast() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (req: RollingForecastRequest) => planningApi.rollForecast(req),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: PLANNING_QUERY_KEYS.versions() });
    }
  });
}

export function useCalculateForecastAccuracy() {
  return useMutation({
    mutationFn: (req: AccuracyRequest) => planningApi.calculateForecastAccuracy(req)
  });
}

export function useCreateScenario() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (req: CreateScenarioRequest) => planningApi.createScenario(req),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: PLANNING_QUERY_KEYS.all });
    }
  });
}

export function useCloneScenario() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, name }: { id: string; name: string }) => planningApi.cloneScenario(id, name),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: PLANNING_QUERY_KEYS.all });
    }
  });
}

export function useShareScenario() {
  return useMutation({
    mutationFn: ({ id, userId, accessLevel }: { id: string; userId: string; accessLevel: "Read" | "Edit" | "Adopt" }) =>
      planningApi.shareScenario(id, userId, accessLevel)
  });
}

export function useAdoptScenario() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: ScenarioAdoptRequest }) =>
      planningApi.adoptScenario(id, request),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: PLANNING_QUERY_KEYS.versions() });
    }
  });
}

export function useImportCsvPreview() {
  return useMutation({
    mutationFn: (req: CsvImportRequest) => planningApi.importCsvPreview(req)
  });
}

export function useImportCsvCommit() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (req: CsvImportRequest) => planningApi.importCsvCommit(req),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: PLANNING_QUERY_KEYS.all });
    }
  });
}

export function useImportXlsxPreview() {
  return useMutation({
    mutationFn: (req: XlsxImportRequest) => planningApi.importXlsxPreview(req)
  });
}

export function useImportXlsxCommit() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (req: XlsxImportRequest) => planningApi.importXlsxCommit(req),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: PLANNING_QUERY_KEYS.all });
    }
  });
}

export function useAssumptions(planVersionId: string) {
  return useQuery({
    queryKey: PLANNING_QUERY_KEYS.assumptions(planVersionId),
    queryFn: () => planningApi.listAssumptions(planVersionId),
    enabled: Boolean(planVersionId)
  });
}

export function useCreateAssumption() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (req: CreateAssumptionRequest) => planningApi.createAssumption(req),
    onSuccess: (_, vars) => {
      void qc.invalidateQueries({ queryKey: PLANNING_QUERY_KEYS.assumptions(vars.planVersionId) });
    }
  });
}

export function useSetAssumptionValue() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: AssumptionValueRequest }) =>
      planningApi.setAssumptionValue(id, request),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: PLANNING_QUERY_KEYS.all });
    }
  });
}

export function useAllocate() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (req: AllocationRequest) => planningApi.allocate(req),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: PLANNING_QUERY_KEYS.all });
    }
  });
}

export function useWorkforcePlan(planVersionId: string, enabled = true) {
  return useQuery({
    queryKey: PLANNING_QUERY_KEYS.workforce(planVersionId),
    queryFn: () => planningApi.listWorkforce(planVersionId),
    enabled: enabled && Boolean(planVersionId)
  });
}

export function useWorkforceCostImpact(planVersionId: string, enabled = true) {
  return useQuery({
    queryKey: PLANNING_QUERY_KEYS.workforceCostImpact(planVersionId),
    queryFn: () => planningApi.getWorkforceCostImpact(planVersionId),
    enabled: enabled && Boolean(planVersionId)
  });
}

export function useAddWorkforceLine() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (req: WorkforcePlanRequest) => planningApi.addWorkforce(req),
    onSuccess: (_, vars) => {
      void qc.invalidateQueries({ queryKey: PLANNING_QUERY_KEYS.workforce(vars.planVersionId) });
      void qc.invalidateQueries({ queryKey: PLANNING_QUERY_KEYS.workforceCostImpact(vars.planVersionId) });
    }
  });
}

export function useOpexLines(planVersionId: string, legalEntityId?: string | null) {
  return useQuery({
    queryKey: PLANNING_QUERY_KEYS.opex(planVersionId, legalEntityId),
    queryFn: () => planningApi.listOpexLines(planVersionId, legalEntityId),
    enabled: Boolean(planVersionId)
  });
}

export function useAddOpexLine() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (req: OpexPlanLineRequest) => planningApi.addOpexLine(req),
    onSuccess: (_, vars) => {
      void qc.invalidateQueries({ queryKey: PLANNING_QUERY_KEYS.opex(vars.planVersionId, vars.legalEntityId) });
      void qc.invalidateQueries({ queryKey: PLANNING_QUERY_KEYS.all });
    }
  });
}

export function useCapexPlan(planVersionId: string, enabled = true) {
  return useQuery({
    queryKey: PLANNING_QUERY_KEYS.capex(planVersionId),
    queryFn: () => planningApi.listCapex(planVersionId),
    enabled: enabled && Boolean(planVersionId)
  });
}

export function useAddCapexItem() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (req: CapexPlanRequest) => planningApi.addCapex(req),
    onSuccess: (_, vars) => {
      void qc.invalidateQueries({ queryKey: PLANNING_QUERY_KEYS.capex(vars.planVersionId) });
    }
  });
}

export function usePrioritizeCapex() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, priority }: { id: string; priority: number }) => planningApi.prioritizeCapex(id, priority),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: PLANNING_QUERY_KEYS.all });
    }
  });
}

export function useApproveCapexBudget() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => planningApi.approveCapexBudget(id),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: PLANNING_QUERY_KEYS.all });
    }
  });
}

export function useAddRevenueDriver() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (req: RevenueDriverRequest) => planningApi.addRevenueDriver(req),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: PLANNING_QUERY_KEYS.all });
    }
  });
}

export function useAddWorkingCapital() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (req: WorkingCapitalRequest) => planningApi.addWorkingCapital(req),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: PLANNING_QUERY_KEYS.all });
    }
  });
}

export function useAddPlanningFx() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (req: PlanningFxRequest) => planningApi.addPlanningFx(req),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: PLANNING_QUERY_KEYS.all });
    }
  });
}

export function useSubmissionUnits(planVersionId?: string | null) {
  return useQuery({
    queryKey: PLANNING_QUERY_KEYS.submissions(planVersionId),
    queryFn: () => planningApi.listSubmissionUnits(planVersionId)
  });
}

export function useCreateSubmissionUnit() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (req: CreateSubmissionUnitRequest) => planningApi.createSubmissionUnit(req),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: PLANNING_QUERY_KEYS.all });
    }
  });
}

export function useTransitionSubmission() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, action, reason }: { id: string; action: "submit" | "approve" | "return" | "lock"; reason?: string | null }) =>
      planningApi.transitionSubmission(id, action, reason),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: PLANNING_QUERY_KEYS.all });
    }
  });
}

export function useDepartmentPlanner(req: DepartmentPlannerRequest, enabled = true) {
  return useQuery({
    queryKey: PLANNING_QUERY_KEYS.departmentPlanner(req),
    queryFn: () => planningApi.queryDepartmentPlanner(req),
    enabled: enabled && Boolean(req.planVersionId && req.legalEntityId)
  });
}

export function useCreatePlanningTarget() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (req: CreatePlanningTargetRequest) => planningApi.createTarget(req),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: PLANNING_QUERY_KEYS.all });
    }
  });
}

export function useAdvancePlanningTarget() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, action }: { id: string; action: "approve" | "publish" | "close" }) => planningApi.advanceTarget(id, action),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: PLANNING_QUERY_KEYS.all });
    }
  });
}

export function useCreatePlanningProposal() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (req: CreatePlanningProposalRequest) => planningApi.createProposal(req),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: PLANNING_QUERY_KEYS.all });
    }
  });
}

export function useAdjustPlanningProposal() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: AdjustPlanningProposalRequest }) => planningApi.adjustProposal(id, request),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: PLANNING_QUERY_KEYS.all });
    }
  });
}

export function useAdvancePlanningProposal() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, action }: { id: string; action: "submit" | "approve" | "reject" }) => planningApi.advanceProposal(id, action),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: PLANNING_QUERY_KEYS.all });
    }
  });
}

export function useCfoOverview(req: CfoOverviewRequest, enabled = true) {
  return useQuery({
    queryKey: PLANNING_QUERY_KEYS.cfoOverview(req),
    queryFn: () => planningApi.queryCfoOverview(req),
    enabled: enabled && Boolean(req.planVersionId && req.legalEntityIds.length > 0)
  });
}

export function useActualBasis(planVersionId: string, enabled = true) {
  return useQuery({
    queryKey: PLANNING_QUERY_KEYS.actualBasis(planVersionId),
    queryFn: () => planningApi.getActualBasis(planVersionId),
    enabled: enabled && Boolean(planVersionId),
    retry: false
  });
}

export function useRefreshActuals() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (req: ActualRefreshRequest) => planningApi.refreshActuals(req),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: PLANNING_QUERY_KEYS.all });
    }
  });
}

export function useVarianceQuery(req: VarianceQueryRequest, enabled = true) {
  return useQuery({
    queryKey: PLANNING_QUERY_KEYS.variance(req),
    queryFn: () => planningApi.queryVariance(req),
    enabled: enabled && Boolean(req.planVersionId && req.legalEntityId)
  });
}

export function useVarianceDecomposition(coordinateId: string | null) {
  return useQuery({
    queryKey: PLANNING_QUERY_KEYS.varianceDecomposition(coordinateId ?? ""),
    queryFn: () => planningApi.getVarianceDecomposition(coordinateId!),
    enabled: Boolean(coordinateId)
  });
}

export function useStatementsQuery(req: StatementRequest, enabled = true) {
  return useQuery({
    queryKey: PLANNING_QUERY_KEYS.statements(req),
    queryFn: () => planningApi.queryStatements(req),
    enabled: enabled && Boolean(req.planVersionId && req.legalEntityId)
  });
}

export function useAddVarianceCommentary() {
  return useMutation({
    mutationFn: ({ planVersionId, coordinateId, body }: { planVersionId: string; coordinateId: string | null; body: string }) =>
      planningApi.addVarianceCommentary(planVersionId, coordinateId, body)
  });
}

export function useCreateBaseline() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (req: CreateBaselineRequest) => planningApi.createBaseline(req),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: PLANNING_QUERY_KEYS.all });
    }
  });
}

export function useAdvanceBaseline() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, action }: { id: string; action: "approve" | "publish" | "close" }) => planningApi.advanceBaseline(id, action),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: PLANNING_QUERY_KEYS.all });
    }
  });
}

export function useApproveRevision() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ baselineId, request }: { baselineId: string; request: RevisionRequest }) => planningApi.approveRevision(baselineId, request),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: PLANNING_QUERY_KEYS.all });
    }
  });
}

export function useCurrentBudget(baselineId: string, asOfDate?: string | null) {
  return useQuery({
    queryKey: PLANNING_QUERY_KEYS.currentBudget(baselineId),
    queryFn: () => planningApi.getCurrentBudget(baselineId, asOfDate),
    enabled: Boolean(baselineId)
  });
}

export function useCreateBudgetTransfer() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ budgetId, request }: { budgetId: string; request: TransferRequest }) => planningApi.createBudgetTransfer(budgetId, request),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: PLANNING_QUERY_KEYS.all });
    }
  });
}

export function useApproveBudgetTransfer() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (transferId: string) => planningApi.approveBudgetTransfer(transferId),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: PLANNING_QUERY_KEYS.all });
    }
  });
}

export function useBucketAvailability(bucketId: string) {
  return useQuery({
    queryKey: PLANNING_QUERY_KEYS.bucketAvailability(bucketId),
    queryFn: () => planningApi.getBucketAvailability(bucketId),
    enabled: Boolean(bucketId),
    retry: false
  });
}

export function useCheckBudget() {
  return useMutation({
    mutationFn: (req: BudgetCheckRequest) => planningApi.checkBudget(req)
  });
}

export function useCheckAndReserveBudget() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (req: BudgetCheckRequest) => planningApi.checkAndReserveBudget(req),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: PLANNING_QUERY_KEYS.all });
    }
  });
}

export function useRequestBudgetOverride() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (req: BudgetOverrideRequest) => planningApi.requestBudgetOverride(req),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: PLANNING_QUERY_KEYS.all });
    }
  });
}

export function useDecideBudgetOverride() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: BudgetOverrideDecisionRequest }) => planningApi.decideBudgetOverride(id, request),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: PLANNING_QUERY_KEYS.all });
    }
  });
}

export function useReconcileBudgetControl() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (req: BudgetControlReconciliationRequest) => planningApi.reconcileBudgetControl(req),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: PLANNING_QUERY_KEYS.all });
    }
  });
}

export function useDimensions(modelVersionId: string) {
  return useQuery({
    queryKey: PLANNING_QUERY_KEYS.dimensions(modelVersionId),
    queryFn: () => planningApi.listDimensions(modelVersionId),
    enabled: Boolean(modelVersionId)
  });
}

export function useDimensionMembers(dimensionId: string) {
  return useQuery({
    queryKey: PLANNING_QUERY_KEYS.dimensionMembers(dimensionId),
    queryFn: () => planningApi.listDimensionMembers(dimensionId),
    enabled: Boolean(dimensionId)
  });
}

export function useCreatePlanningModel() {
  return useMutation({ mutationFn: (req: CreatePlanningModelRequest) => planningApi.createModel(req) });
}
export function useCreateModelVersion() {
  return useMutation({ mutationFn: (req: CreateModelVersionRequest) => planningApi.createModelVersion(req) });
}
export function useCreateDimension() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (req: CreateDimensionRequest) => planningApi.createDimension(req),
    onSuccess: (_, vars) => {
      void qc.invalidateQueries({ queryKey: PLANNING_QUERY_KEYS.dimensions(vars.modelVersionId) });
    }
  });
}
export function useAddDimensionMember() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ dimensionId, request }: { dimensionId: string; request: CreateDimensionMemberRequest }) => planningApi.addDimensionMember(dimensionId, request),
    onSuccess: (_, vars) => {
      void qc.invalidateQueries({ queryKey: PLANNING_QUERY_KEYS.dimensionMembers(vars.dimensionId) });
    }
  });
}
export function useCreateMetric() {
  return useMutation({ mutationFn: (req: CreateMetricRequest) => planningApi.createMetric(req) });
}
export function useCreateMetricMapping() {
  return useMutation({ mutationFn: (req: CreateMetricMappingRequest) => planningApi.createMetricMapping(req) });
}
export function useAdvanceMetricMapping() {
  return useMutation({ mutationFn: ({ id, action }: { id: string; action: "publish" | "retire" }) => planningApi.advanceMetricMapping(id, action) });
}
export function useCreateFormula() {
  return useMutation({ mutationFn: (req: CreateFormulaRequest) => planningApi.createFormula(req) });
}
export function useCreateBudgetPolicy() {
  return useMutation({
    mutationFn: ({ legalEntityId, name, mode, basis, thresholdPercent }: { legalEntityId: string; name: string; mode: string; basis: string; thresholdPercent: number }) =>
      planningApi.createBudgetPolicy(legalEntityId, name, mode, basis, thresholdPercent)
  });
}
export function useCreateBudgetBucket() {
  return useMutation({ mutationFn: (req: BudgetBucketRequest) => planningApi.createBudgetBucket(req) });
}

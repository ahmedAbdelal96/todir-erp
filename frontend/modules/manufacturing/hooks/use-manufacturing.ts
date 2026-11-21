import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { manufacturingApi } from "../services/manufacturing-api.ts";
import type {
  CreatePlantRequest,
  CreateManufacturingProfileRequest,
  CreateBomRequest,
  CreateBomVersionRequest,
  CreateFormulaRequest,
  CreateFormulaVersionRequest,
  CreateRoutingRequest,
  CreateRoutingVersionRequest,
  CreateWorkCenterRequest,
  CreateWorkCenterCalendarRequest,
  CreateProductionDefinitionRequest,
  ResolveDefinitionRequest,
  CreateSubstitutionGroupRequest,
  AddSubstituteRequest,
  CreateProductionOrderRequest,
  ProductionActionRequest,
  MaterialActionRequest,
  OperationActionRequest,
  AssignOperationResourceRequest,
  OutputReportRequest,
  ScrapReportRequest,
  QualityGateDecisionRequest,
  QualityGatePolicyRequest,
  CreateSubcontractCommitmentRequest,
  RecordLaborFactRequest,
  RecordCostFactRequest,
  AllocateOutputCostRequest,
  CreatePlanningRunRequest,
  CreateMpsPlanRequest,
  GenealogyLinkRequest
} from "../types.ts";

export const MANUFACTURING_QUERY_KEYS = {
  all: ["manufacturing"] as const,
  plants: (legalEntityId: string) => [...MANUFACTURING_QUERY_KEYS.all, "plants", legalEntityId] as const,
  profiles: (legalEntityId: string, variantId?: string) => [...MANUFACTURING_QUERY_KEYS.all, "profiles", legalEntityId, variantId] as const,
  boms: (variantId?: string) => [...MANUFACTURING_QUERY_KEYS.all, "boms", variantId] as const,
  bomVersions: (bomId: string) => [...MANUFACTURING_QUERY_KEYS.all, "bomVersions", bomId] as const,
  bomVersion: (versionId: string) => [...MANUFACTURING_QUERY_KEYS.all, "bomVersion", versionId] as const,
  formulas: (variantId?: string) => [...MANUFACTURING_QUERY_KEYS.all, "formulas", variantId] as const,
  formulaVersions: (formulaId: string) => [...MANUFACTURING_QUERY_KEYS.all, "formulaVersions", formulaId] as const,
  routings: (variantId?: string) => [...MANUFACTURING_QUERY_KEYS.all, "routings", variantId] as const,
  routingVersions: (routingId: string) => [...MANUFACTURING_QUERY_KEYS.all, "routingVersions", routingId] as const,
  workCenters: (plantId?: string) => [...MANUFACTURING_QUERY_KEYS.all, "workCenters", plantId] as const,
  capacity: (plantId: string, from: string, to: string, workCenterId?: string) => [...MANUFACTURING_QUERY_KEYS.all, "capacity", plantId, from, to, workCenterId] as const,
  definitions: (legalEntityId: string, plantId?: string, variantId?: string) => [...MANUFACTURING_QUERY_KEYS.all, "definitions", legalEntityId, plantId, variantId] as const,
  substitutionGroups: () => [...MANUFACTURING_QUERY_KEYS.all, "substitutionGroups"] as const,
  orders: (legalEntityId: string, state?: string, variantId?: string, plantId?: string, search?: string, limit?: number, offset?: number) => [...MANUFACTURING_QUERY_KEYS.all, "orders", legalEntityId, state, variantId, plantId, search, limit, offset] as const,
  ordersCount: (legalEntityId: string, state?: string, variantId?: string, plantId?: string, search?: string) => [...MANUFACTURING_QUERY_KEYS.all, "ordersCount", legalEntityId, state, variantId, plantId, search] as const,
  order: (orderId: string) => [...MANUFACTURING_QUERY_KEYS.all, "order", orderId] as const,
  materials: (orderId: string) => [...MANUFACTURING_QUERY_KEYS.all, "materials", orderId] as const,
  operations: (orderId: string) => [...MANUFACTURING_QUERY_KEYS.all, "operations", orderId] as const,
  shopFloorQueue: (plantId: string, from: string, to: string, state?: string) => [...MANUFACTURING_QUERY_KEYS.all, "shopFloorQueue", plantId, from, to, state] as const,
  qualityGates: (orderId: string) => [...MANUFACTURING_QUERY_KEYS.all, "qualityGates", orderId] as const,
  cost: (orderId: string) => [...MANUFACTURING_QUERY_KEYS.all, "cost", orderId] as const,
  closeReadiness: (orderId: string) => [...MANUFACTURING_QUERY_KEYS.all, "closeReadiness", orderId] as const,
  planningRuns: (legalEntityId: string) => [...MANUFACTURING_QUERY_KEYS.all, "planningRuns", legalEntityId] as const,
  plannedSupplies: (runId: string) => [...MANUFACTURING_QUERY_KEYS.all, "plannedSupplies", runId] as const,
  mpsPlans: (legalEntityId: string, plantId?: string, variantId?: string) => [...MANUFACTURING_QUERY_KEYS.all, "mpsPlans", legalEntityId, plantId, variantId] as const,
  forwardGenealogy: (outputId: string) => [...MANUFACTURING_QUERY_KEYS.all, "forwardGenealogy", outputId] as const,
  reverseGenealogy: (eventId: string) => [...MANUFACTURING_QUERY_KEYS.all, "reverseGenealogy", eventId] as const
};

// Query Hooks
export function usePlantsQuery(legalEntityId: string | undefined) {
  return useQuery({
    queryKey: MANUFACTURING_QUERY_KEYS.plants(legalEntityId ?? ""),
    queryFn: () => manufacturingApi.listPlants(legalEntityId!),
    enabled: Boolean(legalEntityId)
  });
}

export function useProfilesQuery(legalEntityId: string | undefined, productVariantId?: string) {
  return useQuery({
    queryKey: MANUFACTURING_QUERY_KEYS.profiles(legalEntityId ?? "", productVariantId),
    queryFn: () => manufacturingApi.listProfiles(legalEntityId!, productVariantId),
    enabled: Boolean(legalEntityId)
  });
}

export function useBomsQuery(productVariantId?: string) {
  return useQuery({
    queryKey: MANUFACTURING_QUERY_KEYS.boms(productVariantId),
    queryFn: () => manufacturingApi.listBoms(productVariantId)
  });
}

export function useBomVersionsQuery(bomId: string) {
  return useQuery({
    queryKey: MANUFACTURING_QUERY_KEYS.bomVersions(bomId),
    queryFn: () => manufacturingApi.listBomVersions(bomId),
    enabled: Boolean(bomId)
  });
}

export function useBomVersionQuery(versionId: string) {
  return useQuery({
    queryKey: MANUFACTURING_QUERY_KEYS.bomVersion(versionId),
    queryFn: () => manufacturingApi.getBomVersion(versionId),
    enabled: Boolean(versionId)
  });
}

export function useFormulasQuery(productVariantId?: string) {
  return useQuery({
    queryKey: MANUFACTURING_QUERY_KEYS.formulas(productVariantId),
    queryFn: () => manufacturingApi.listFormulas(productVariantId)
  });
}

export function useFormulaVersionsQuery(formulaId: string) {
  return useQuery({
    queryKey: MANUFACTURING_QUERY_KEYS.formulaVersions(formulaId),
    queryFn: () => manufacturingApi.listFormulaVersions(formulaId),
    enabled: Boolean(formulaId)
  });
}

export function useRoutingsQuery(productVariantId?: string) {
  return useQuery({
    queryKey: MANUFACTURING_QUERY_KEYS.routings(productVariantId),
    queryFn: () => manufacturingApi.listRoutings(productVariantId)
  });
}

export function useRoutingVersionsQuery(routingId: string) {
  return useQuery({
    queryKey: MANUFACTURING_QUERY_KEYS.routingVersions(routingId),
    queryFn: () => manufacturingApi.listRoutingVersions(routingId),
    enabled: Boolean(routingId)
  });
}

export function useWorkCentersQuery(plantId?: string) {
  return useQuery({
    queryKey: MANUFACTURING_QUERY_KEYS.workCenters(plantId),
    queryFn: () => manufacturingApi.listWorkCenters(plantId)
  });
}

export function useCapacityQuery(plantId: string, from: string, to: string, workCenterId?: string) {
  return useQuery({
    queryKey: MANUFACTURING_QUERY_KEYS.capacity(plantId, from, to, workCenterId),
    queryFn: () => manufacturingApi.getCapacity(plantId, from, to, workCenterId),
    enabled: Boolean(plantId && from && to)
  });
}

export function useDefinitionsQuery(legalEntityId: string | undefined, plantId?: string, productVariantId?: string) {
  return useQuery({
    queryKey: MANUFACTURING_QUERY_KEYS.definitions(legalEntityId ?? "", plantId, productVariantId),
    queryFn: () => manufacturingApi.listDefinitions(legalEntityId!, plantId, productVariantId),
    enabled: Boolean(legalEntityId)
  });
}

export function useSubstitutionGroupsQuery() {
  return useQuery({
    queryKey: MANUFACTURING_QUERY_KEYS.substitutionGroups(),
    queryFn: () => manufacturingApi.listSubstitutionGroups()
  });
}

export function useProductionOrdersQuery(legalEntityId: string | undefined, filters?: { state?: string | undefined; productVariantId?: string | undefined; plantId?: string | undefined; search?: string | undefined; limit?: number | undefined; offset?: number | undefined }) {
  return useQuery({
    queryKey: MANUFACTURING_QUERY_KEYS.orders(legalEntityId ?? "", filters?.state, filters?.productVariantId, filters?.plantId, filters?.search, filters?.limit, filters?.offset),
    queryFn: () => manufacturingApi.listProductionOrders(legalEntityId!, filters),
    enabled: Boolean(legalEntityId)
  });
}

export function useProductionOrdersCountQuery(legalEntityId: string | undefined, filters?: { state?: string | undefined; productVariantId?: string | undefined; plantId?: string | undefined; search?: string | undefined }) {
  return useQuery({
    queryKey: MANUFACTURING_QUERY_KEYS.ordersCount(legalEntityId ?? "", filters?.state, filters?.productVariantId, filters?.plantId, filters?.search),
    queryFn: () => manufacturingApi.countProductionOrders(legalEntityId!, filters),
    enabled: Boolean(legalEntityId)
  });
}

export const useOrdersQuery = useProductionOrdersQuery;
export const useOrdersCountQuery = useProductionOrdersCountQuery;
export const useOrderQuery = useProductionOrderQuery;
export const useMaterialsQuery = useOrderMaterialsQuery;
export const useOperationsQuery = useOrderOperationsQuery;
export const useCostQuery = useOrderCostQuery;
export const useCloseReadinessQuery = useOrderCloseReadinessQuery;
export function useCreateMpsPlanMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: any) => Promise.resolve(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: MANUFACTURING_QUERY_KEYS.all });
    }
  });
}

export const useCreateOrderMutation = useCreateProductionOrderMutation;
export const useRecordQualityDecisionMutation = useQualityGateDecisionMutation;
export const useUpdateQualityPolicyMutation = useQualityGatePolicyMutation;
export const useTransitionOrderMutation = useTransitionProductionOrderMutation;

export function useProductionOrderQuery(orderId: string) {
  return useQuery({
    queryKey: MANUFACTURING_QUERY_KEYS.order(orderId),
    queryFn: () => manufacturingApi.getProductionOrder(orderId),
    enabled: Boolean(orderId)
  });
}

export function useOrderMaterialsQuery(orderId: string) {
  return useQuery({
    queryKey: MANUFACTURING_QUERY_KEYS.materials(orderId),
    queryFn: () => manufacturingApi.listOrderMaterials(orderId),
    enabled: Boolean(orderId)
  });
}

export function useOrderOperationsQuery(orderId: string) {
  return useQuery({
    queryKey: MANUFACTURING_QUERY_KEYS.operations(orderId),
    queryFn: () => manufacturingApi.listOrderOperations(orderId),
    enabled: Boolean(orderId)
  });
}

export function useShopFloorQueueQuery(plantId: string, from: string, to: string, state?: string, limit?: number) {
  return useQuery({
    queryKey: MANUFACTURING_QUERY_KEYS.shopFloorQueue(plantId, from, to, state),
    queryFn: () => manufacturingApi.listShopFloorQueue(plantId, from, to, state, limit),
    enabled: Boolean(plantId && from && to)
  });
}

export function useQualityGatesQuery(orderId: string) {
  return useQuery({
    queryKey: MANUFACTURING_QUERY_KEYS.qualityGates(orderId),
    queryFn: () => manufacturingApi.getOrderQualityGates(orderId),
    enabled: Boolean(orderId)
  });
}

export function useOrderCostQuery(orderId: string) {
  return useQuery({
    queryKey: MANUFACTURING_QUERY_KEYS.cost(orderId),
    queryFn: () => manufacturingApi.getOrderCost(orderId),
    enabled: Boolean(orderId)
  });
}

export function useOrderCloseReadinessQuery(orderId: string) {
  return useQuery({
    queryKey: MANUFACTURING_QUERY_KEYS.closeReadiness(orderId),
    queryFn: () => manufacturingApi.getOrderCloseReadiness(orderId),
    enabled: Boolean(orderId)
  });
}

export function usePlanningRunsQuery(legalEntityId: string | undefined) {
  return useQuery({
    queryKey: MANUFACTURING_QUERY_KEYS.planningRuns(legalEntityId ?? ""),
    queryFn: () => manufacturingApi.listPlanningRuns(legalEntityId!),
    enabled: Boolean(legalEntityId)
  });
}

export function usePlannedSuppliesQuery(runId: string) {
  return useQuery({
    queryKey: MANUFACTURING_QUERY_KEYS.plannedSupplies(runId),
    queryFn: () => manufacturingApi.listPlannedSupplies(runId),
    enabled: Boolean(runId)
  });
}

export function useMpsPlansQuery(legalEntityId: string | undefined, filters?: { plantId?: string; productVariantId?: string }) {
  return useQuery({
    queryKey: MANUFACTURING_QUERY_KEYS.mpsPlans(legalEntityId ?? "", filters?.plantId, filters?.productVariantId),
    queryFn: () => manufacturingApi.listMpsPlans(legalEntityId!, filters),
    enabled: Boolean(legalEntityId)
  });
}

export function useForwardGenealogyQuery(outputId: string) {
  return useQuery({
    queryKey: MANUFACTURING_QUERY_KEYS.forwardGenealogy(outputId),
    queryFn: () => manufacturingApi.getForwardGenealogy(outputId),
    enabled: Boolean(outputId)
  });
}

export function useReverseGenealogyQuery(eventId: string) {
  return useQuery({
    queryKey: MANUFACTURING_QUERY_KEYS.reverseGenealogy(eventId),
    queryFn: () => manufacturingApi.getReverseGenealogy(eventId),
    enabled: Boolean(eventId)
  });
}

// Mutation Hooks
export function useCreateProductionOrderMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateProductionOrderRequest) => manufacturingApi.createProductionOrder(request),
    onSuccess: (data) => {
      queryClient.invalidateQueries({ queryKey: MANUFACTURING_QUERY_KEYS.orders(data.legalEntityId) });
    }
  });
}

export function useTransitionProductionOrderMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ orderId, action, request }: { orderId: string; action: string; request: ProductionActionRequest }) =>
      manufacturingApi.transitionProductionOrder(orderId, action, request),
    onSuccess: (data) => {
      queryClient.invalidateQueries({ queryKey: MANUFACTURING_QUERY_KEYS.order(data.id) });
      queryClient.invalidateQueries({ queryKey: MANUFACTURING_QUERY_KEYS.orders(data.legalEntityId) });
      queryClient.invalidateQueries({ queryKey: MANUFACTURING_QUERY_KEYS.closeReadiness(data.id) });
    }
  });
}

export function useMaterialActionMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ orderId, route, request }: { orderId: string; route: "reserve" | "issue" | "consume" | "return"; request: MaterialActionRequest }) =>
      manufacturingApi.recordMaterialAction(orderId, route, request),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: MANUFACTURING_QUERY_KEYS.materials(variables.orderId) });
      queryClient.invalidateQueries({ queryKey: MANUFACTURING_QUERY_KEYS.order(variables.orderId) });
      queryClient.invalidateQueries({ queryKey: MANUFACTURING_QUERY_KEYS.cost(variables.orderId) });
      queryClient.invalidateQueries({ queryKey: MANUFACTURING_QUERY_KEYS.closeReadiness(variables.orderId) });
    }
  });
}

export function useTransitionOperationMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ operationId, action, request }: { operationId: string; action: string; request: OperationActionRequest }) =>
      manufacturingApi.transitionOperation(operationId, action, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: MANUFACTURING_QUERY_KEYS.all });
    }
  });
}

export function useReportOutputMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ orderId, request }: { orderId: string; request: OutputReportRequest }) =>
      manufacturingApi.reportOutput(orderId, request),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: MANUFACTURING_QUERY_KEYS.order(variables.orderId) });
      queryClient.invalidateQueries({ queryKey: MANUFACTURING_QUERY_KEYS.operations(variables.orderId) });
      queryClient.invalidateQueries({ queryKey: MANUFACTURING_QUERY_KEYS.closeReadiness(variables.orderId) });
    }
  });
}

export function useReportScrapMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ orderId, request }: { orderId: string; request: ScrapReportRequest }) =>
      manufacturingApi.reportScrap(orderId, request),
    onSuccess: (data) => {
      queryClient.invalidateQueries({ queryKey: MANUFACTURING_QUERY_KEYS.order(data.id) });
      queryClient.invalidateQueries({ queryKey: MANUFACTURING_QUERY_KEYS.operations(data.id) });
      queryClient.invalidateQueries({ queryKey: MANUFACTURING_QUERY_KEYS.closeReadiness(data.id) });
    }
  });
}

export function useQualityGateDecisionMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ orderId, request }: { orderId: string; request: QualityGateDecisionRequest }) =>
      manufacturingApi.setQualityGate(orderId, request),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: MANUFACTURING_QUERY_KEYS.qualityGates(variables.orderId) });
      queryClient.invalidateQueries({ queryKey: MANUFACTURING_QUERY_KEYS.order(variables.orderId) });
      queryClient.invalidateQueries({ queryKey: MANUFACTURING_QUERY_KEYS.closeReadiness(variables.orderId) });
    }
  });
}

export function useQualityGatePolicyMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ orderId, request }: { orderId: string; request: QualityGatePolicyRequest }) =>
      manufacturingApi.updateQualityGatePolicy(orderId, request),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: MANUFACTURING_QUERY_KEYS.qualityGates(variables.orderId) });
      queryClient.invalidateQueries({ queryKey: MANUFACTURING_QUERY_KEYS.order(variables.orderId) });
    }
  });
}

export function useRecordLaborMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ orderId, request }: { orderId: string; request: RecordLaborFactRequest }) =>
      manufacturingApi.recordLabor(orderId, request),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: MANUFACTURING_QUERY_KEYS.cost(variables.orderId) });
      queryClient.invalidateQueries({ queryKey: MANUFACTURING_QUERY_KEYS.closeReadiness(variables.orderId) });
    }
  });
}

export function useRecordCostFactMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ orderId, request }: { orderId: string; request: RecordCostFactRequest }) =>
      manufacturingApi.recordCostFact(orderId, request),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: MANUFACTURING_QUERY_KEYS.cost(variables.orderId) });
      queryClient.invalidateQueries({ queryKey: MANUFACTURING_QUERY_KEYS.closeReadiness(variables.orderId) });
    }
  });
}

export function useAllocateOutputCostMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ orderId, request }: { orderId: string; request: AllocateOutputCostRequest }) =>
      manufacturingApi.allocateOutputCost(orderId, request),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: MANUFACTURING_QUERY_KEYS.cost(variables.orderId) });
    }
  });
}

export function useCreatePlanningRunMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreatePlanningRunRequest) => manufacturingApi.createPlanningRun(request),
    onSuccess: (data) => {
      queryClient.invalidateQueries({ queryKey: MANUFACTURING_QUERY_KEYS.planningRuns(data.legalEntityId) });
    }
  });
}

export function useRunPlanningMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (runId: string) => manufacturingApi.runPlanning(runId),
    onSuccess: (data) => {
      queryClient.invalidateQueries({ queryKey: MANUFACTURING_QUERY_KEYS.planningRuns(data.legalEntityId) });
      queryClient.invalidateQueries({ queryKey: MANUFACTURING_QUERY_KEYS.plannedSupplies(data.id) });
    }
  });
}

export function useFirmProductionMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ plannedSupplyId, legalEntityId, plantId }: { plannedSupplyId: string; legalEntityId: string; plantId: string }) =>
      manufacturingApi.firmProduction(plannedSupplyId, legalEntityId, plantId),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: MANUFACTURING_QUERY_KEYS.orders(variables.legalEntityId) });
      queryClient.invalidateQueries({ queryKey: MANUFACTURING_QUERY_KEYS.all });
    }
  });
}

export function useCreatePlantMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreatePlantRequest) => manufacturingApi.createPlant(request),
    onSuccess: (data) => {
      queryClient.invalidateQueries({ queryKey: MANUFACTURING_QUERY_KEYS.plants(data.legalEntityId) });
    }
  });
}

export function useCreateBomMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateBomRequest) => manufacturingApi.createBom(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["manufacturing", "boms"] });
    }
  });
}

export function useCreateBomVersionMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ bomId, request }: { bomId: string; request: CreateBomVersionRequest }) =>
      manufacturingApi.createBomVersion(bomId, request),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: MANUFACTURING_QUERY_KEYS.bomVersions(variables.bomId) });
    }
  });
}

export function useChangeBomVersionStateMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ versionId, action }: { versionId: string; action: "approve" | "activate" | "supersede" }) =>
      manufacturingApi.changeBomVersionState(versionId, action),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["manufacturing"] });
    }
  });
}

export function useCreateFormulaMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateFormulaRequest) => manufacturingApi.createFormula(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["manufacturing", "formulas"] });
    }
  });
}

export function useCreateFormulaVersionMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ formulaId, request }: { formulaId: string; request: CreateFormulaVersionRequest }) =>
      manufacturingApi.createFormulaVersion(formulaId, request),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: MANUFACTURING_QUERY_KEYS.formulaVersions(variables.formulaId) });
    }
  });
}

export function useCreateRoutingMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateRoutingRequest) => manufacturingApi.createRouting(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["manufacturing", "routings"] });
    }
  });
}

export function useCreateRoutingVersionMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ routingId, request }: { routingId: string; request: CreateRoutingVersionRequest }) =>
      manufacturingApi.createRoutingVersion(routingId, request),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: MANUFACTURING_QUERY_KEYS.routingVersions(variables.routingId) });
    }
  });
}

export function useCreateWorkCenterMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateWorkCenterRequest) => manufacturingApi.createWorkCenter(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["manufacturing", "workCenters"] });
    }
  });
}

export function useCreateWorkCenterCalendarMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ workCenterId, request }: { workCenterId: string; request: CreateWorkCenterCalendarRequest }) =>
      manufacturingApi.createWorkCenterCalendar(workCenterId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["manufacturing", "capacity"] });
    }
  });
}

export function useCreateDefinitionMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateProductionDefinitionRequest) => manufacturingApi.createDefinition(request),
    onSuccess: (data) => {
      queryClient.invalidateQueries({ queryKey: MANUFACTURING_QUERY_KEYS.definitions(data.legalEntityId) });
    }
  });
}

export function useCreateSubstitutionGroupMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateSubstitutionGroupRequest) => manufacturingApi.createSubstitutionGroup(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: MANUFACTURING_QUERY_KEYS.substitutionGroups() });
    }
  });
}

export function useAddSubstituteMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ groupId, request }: { groupId: string; request: AddSubstituteRequest }) =>
      manufacturingApi.addSubstitute(groupId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: MANUFACTURING_QUERY_KEYS.substitutionGroups() });
    }
  });
}

export function useLinkGenealogyMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ orderId, request }: { orderId: string; request: GenealogyLinkRequest }) =>
      manufacturingApi.linkGenealogy(orderId, request),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: MANUFACTURING_QUERY_KEYS.forwardGenealogy(variables.request.outputId) });
      queryClient.invalidateQueries({ queryKey: MANUFACTURING_QUERY_KEYS.reverseGenealogy(variables.request.inputEventId) });
    }
  });
}

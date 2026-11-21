import { httpClient } from "../../../core/api/http-client.ts";
import type {
  Plant,
  CreatePlantRequest,
  ManufacturingProfile,
  CreateManufacturingProfileRequest,
  Bom,
  CreateBomRequest,
  BomVersion,
  CreateBomVersionRequest,
  Formula,
  CreateFormulaRequest,
  FormulaVersion,
  CreateFormulaVersionRequest,
  Routing,
  CreateRoutingRequest,
  RoutingVersion,
  CreateRoutingVersionRequest,
  WorkCenter,
  CreateWorkCenterRequest,
  CreateWorkCenterCalendarRequest,
  CapacityItem,
  ProductionDefinition,
  CreateProductionDefinitionRequest,
  ResolveDefinitionRequest,
  SubstitutionGroup,
  CreateSubstitutionGroupRequest,
  SubstituteItem,
  AddSubstituteRequest,
  ProductionOrder,
  CreateProductionOrderRequest,
  ProductionActionRequest,
  ProductionMaterial,
  MaterialActionRequest,
  ProductionOperation,
  OperationActionRequest,
  AssignOperationResourceRequest,
  OutputReportRequest,
  ProductionOutput,
  ScrapReportRequest,
  QualityGate,
  QualityGateDecisionRequest,
  QualityGatePolicyRequest,
  SubcontractCommitment,
  CreateSubcontractCommitmentRequest,
  RecordLaborFactRequest,
  ProductionCost,
  RecordCostFactRequest,
  AllocateOutputCostRequest,
  ProductionCloseReadiness,
  ManufacturingCostingSummary,
  PlanningRun,
  CreatePlanningRunRequest,
  PlannedSupply,
  MpsPlan,
  CreateMpsPlanRequest,
  GenealogyItem,
  GenealogyLinkRequest
} from "../types.ts";

const BASE_URL = "/api/v1/manufacturing";

function buildQuery(params: Record<string, string | number | boolean | null | undefined>): string {
  const q = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== null && value !== "") {
      q.set(key, String(value));
    }
  }
  const str = q.toString();
  return str ? `?${str}` : "";
}

export const manufacturingApi = {
  // Plants
  listPlants(legalEntityId: string): Promise<Plant[]> {
    const qs = buildQuery({ legalEntityId });
    return httpClient<Plant[]>(`${BASE_URL}/plants${qs}`, { legalEntityId });
  },

  createPlant(request: CreatePlantRequest): Promise<Plant> {
    return httpClient<Plant>(`${BASE_URL}/plants`, {
      method: "POST",
      body: JSON.stringify(request),
      legalEntityId: request.legalEntityId
    });
  },

  // Product Profiles
  listProfiles(legalEntityId: string, productVariantId?: string): Promise<ManufacturingProfile[]> {
    const qs = buildQuery({ legalEntityId, productVariantId });
    return httpClient<ManufacturingProfile[]>(`${BASE_URL}/product-profiles${qs}`, { legalEntityId });
  },

  saveProfile(request: CreateManufacturingProfileRequest): Promise<ManufacturingProfile> {
    return httpClient<ManufacturingProfile>(`${BASE_URL}/product-profiles`, {
      method: "PUT",
      body: JSON.stringify(request),
      legalEntityId: request.legalEntityId
    });
  },

  // BOMs
  listBoms(productVariantId?: string): Promise<Bom[]> {
    const qs = buildQuery({ productVariantId });
    return httpClient<Bom[]>(`${BASE_URL}/boms${qs}`);
  },

  createBom(request: CreateBomRequest): Promise<Bom> {
    return httpClient<Bom>(`${BASE_URL}/boms`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  listBomVersions(bomId: string): Promise<BomVersion[]> {
    return httpClient<BomVersion[]>(`${BASE_URL}/boms/${bomId}/versions`);
  },

  getBomVersion(versionId: string): Promise<BomVersion> {
    return httpClient<BomVersion>(`${BASE_URL}/bom-versions/${versionId}`);
  },

  createBomVersion(bomId: string, request: CreateBomVersionRequest): Promise<BomVersion> {
    return httpClient<BomVersion>(`${BASE_URL}/boms/${bomId}/versions`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  changeBomVersionState(versionId: string, action: "approve" | "activate" | "supersede"): Promise<BomVersion> {
    return httpClient<BomVersion>(`${BASE_URL}/bom-versions/${versionId}/${action}`, {
      method: "POST"
    });
  },

  // Formulas
  listFormulas(productVariantId?: string): Promise<Formula[]> {
    const qs = buildQuery({ productVariantId });
    return httpClient<Formula[]>(`${BASE_URL}/formulas${qs}`);
  },

  createFormula(request: CreateFormulaRequest): Promise<Formula> {
    return httpClient<Formula>(`${BASE_URL}/formulas`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  listFormulaVersions(formulaId: string): Promise<FormulaVersion[]> {
    return httpClient<FormulaVersion[]>(`${BASE_URL}/formulas/${formulaId}/versions`);
  },

  createFormulaVersion(formulaId: string, request: CreateFormulaVersionRequest): Promise<FormulaVersion> {
    return httpClient<FormulaVersion>(`${BASE_URL}/formulas/${formulaId}/versions`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  // Routings
  listRoutings(productVariantId?: string): Promise<Routing[]> {
    const qs = buildQuery({ productVariantId });
    return httpClient<Routing[]>(`${BASE_URL}/routings${qs}`);
  },

  createRouting(request: CreateRoutingRequest): Promise<Routing> {
    return httpClient<Routing>(`${BASE_URL}/routings`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  listRoutingVersions(routingId: string): Promise<RoutingVersion[]> {
    return httpClient<RoutingVersion[]>(`${BASE_URL}/routings/${routingId}/versions`);
  },

  createRoutingVersion(routingId: string, request: CreateRoutingVersionRequest): Promise<RoutingVersion> {
    return httpClient<RoutingVersion>(`${BASE_URL}/routings/${routingId}/versions`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  // Work Centers
  listWorkCenters(plantId?: string): Promise<WorkCenter[]> {
    const qs = buildQuery({ plantId });
    return httpClient<WorkCenter[]>(`${BASE_URL}/work-centers${qs}`);
  },

  createWorkCenter(request: CreateWorkCenterRequest): Promise<WorkCenter> {
    return httpClient<WorkCenter>(`${BASE_URL}/work-centers`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  createWorkCenterCalendar(workCenterId: string, request: CreateWorkCenterCalendarRequest): Promise<{ workCenterId: string; added: boolean }> {
    return httpClient<{ workCenterId: string; added: boolean }>(`${BASE_URL}/work-centers/${workCenterId}/calendar`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  getCapacity(plantId: string, from: string, to: string, workCenterId?: string): Promise<CapacityItem[]> {
    const qs = buildQuery({ from, to, workCenterId });
    return httpClient<CapacityItem[]>(`${BASE_URL}/plants/${plantId}/capacity${qs}`);
  },

  // Definitions
  listDefinitions(legalEntityId: string, plantId?: string, productVariantId?: string): Promise<ProductionDefinition[]> {
    const qs = buildQuery({ legalEntityId, plantId, productVariantId });
    return httpClient<ProductionDefinition[]>(`${BASE_URL}/definitions${qs}`, { legalEntityId });
  },

  createDefinition(request: CreateProductionDefinitionRequest): Promise<ProductionDefinition> {
    return httpClient<ProductionDefinition>(`${BASE_URL}/definitions`, {
      method: "POST",
      body: JSON.stringify(request),
      legalEntityId: request.legalEntityId
    });
  },

  resolveDefinition(request: ResolveDefinitionRequest): Promise<ProductionDefinition> {
    return httpClient<ProductionDefinition>(`${BASE_URL}/definitions/resolve`, {
      method: "POST",
      body: JSON.stringify(request),
      legalEntityId: request.legalEntityId
    });
  },

  // Substitutes
  listSubstitutionGroups(): Promise<SubstitutionGroup[]> {
    return httpClient<SubstitutionGroup[]>(`${BASE_URL}/substitution-groups`);
  },

  createSubstitutionGroup(request: CreateSubstitutionGroupRequest): Promise<SubstitutionGroup> {
    return httpClient<SubstitutionGroup>(`${BASE_URL}/substitution-groups`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  addSubstitute(groupId: string, request: AddSubstituteRequest): Promise<SubstituteItem> {
    return httpClient<SubstituteItem>(`${BASE_URL}/substitution-groups/${groupId}/members`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  // Production Orders
  listProductionOrders(
    legalEntityId: string,
    filters?: { state?: string | undefined; productVariantId?: string | undefined; plantId?: string | undefined; search?: string | undefined; limit?: number | undefined; offset?: number | undefined }
  ): Promise<ProductionOrder[]> {
    const qs = buildQuery({ ...filters });
    return httpClient<ProductionOrder[]>(`${BASE_URL}/legal-entities/${legalEntityId}/production-orders${qs}`, { legalEntityId });
  },

  countProductionOrders(
    legalEntityId: string,
    filters?: { state?: string | undefined; productVariantId?: string | undefined; plantId?: string | undefined; search?: string | undefined }
  ): Promise<number> {
    const qs = buildQuery({ legalEntityId, ...filters });
    return httpClient<number>(`${BASE_URL}/production-orders/count${qs}`, { legalEntityId });
  },

  getProductionOrder(orderId: string): Promise<ProductionOrder> {
    return httpClient<ProductionOrder>(`${BASE_URL}/production-orders/${orderId}`);
  },

  createProductionOrder(request: CreateProductionOrderRequest): Promise<ProductionOrder> {
    return httpClient<ProductionOrder>(`${BASE_URL}/production-orders`, {
      method: "POST",
      body: JSON.stringify(request),
      legalEntityId: request.legalEntityId
    });
  },

  transitionProductionOrder(orderId: string, action: string, request: ProductionActionRequest): Promise<ProductionOrder> {
    return httpClient<ProductionOrder>(`${BASE_URL}/production-orders/${orderId}/${action}`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  // Materials
  listOrderMaterials(orderId: string): Promise<ProductionMaterial[]> {
    return httpClient<ProductionMaterial[]>(`${BASE_URL}/production-orders/${orderId}/materials`);
  },

  recordMaterialAction(orderId: string, route: "reserve" | "issue" | "consume" | "return", request: MaterialActionRequest): Promise<ProductionMaterial> {
    return httpClient<ProductionMaterial>(`${BASE_URL}/production-orders/${orderId}/materials/${route}`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  // Operations & Shop Floor
  listOrderOperations(orderId: string): Promise<ProductionOperation[]> {
    return httpClient<ProductionOperation[]>(`${BASE_URL}/production-orders/${orderId}/operations`);
  },

  listShopFloorQueue(plantId: string, from: string, to: string, state?: string, limit?: number): Promise<ProductionOperation[]> {
    const qs = buildQuery({ from, to, state, limit });
    return httpClient<ProductionOperation[]>(`${BASE_URL}/plants/${plantId}/shop-floor-queue${qs}`);
  },

  transitionOperation(operationId: string, action: string, request: OperationActionRequest): Promise<ProductionOperation> {
    return httpClient<ProductionOperation>(`${BASE_URL}/production-operations/${operationId}/${action}`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  assignOperationResource(orderId: string, operationId: string, request: AssignOperationResourceRequest): Promise<{ operationId: string; resourceId: string; assigned: boolean }> {
    return httpClient<{ operationId: string; resourceId: string; assigned: boolean }>(`${BASE_URL}/production-orders/${orderId}/operations/${operationId}/resources`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  reportOutput(orderId: string, request: OutputReportRequest): Promise<ProductionOutput> {
    return httpClient<ProductionOutput>(`${BASE_URL}/production-orders/${orderId}/outputs`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  reportScrap(orderId: string, request: ScrapReportRequest): Promise<ProductionOrder> {
    return httpClient<ProductionOrder>(`${BASE_URL}/production-orders/${orderId}/scrap`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  // Quality Gates
  getOrderQualityGates(orderId: string, limit?: number): Promise<QualityGate[]> {
    const qs = buildQuery({ limit });
    return httpClient<QualityGate[]>(`${BASE_URL}/production-orders/${orderId}/quality-gates${qs}`);
  },

  setQualityGate(orderId: string, request: QualityGateDecisionRequest): Promise<QualityGate> {
    return httpClient<QualityGate>(`${BASE_URL}/production-orders/${orderId}/quality-gates`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  updateQualityGatePolicy(orderId: string, request: QualityGatePolicyRequest): Promise<QualityGate> {
    return httpClient<QualityGate>(`${BASE_URL}/production-orders/${orderId}/quality-gates/policy`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  // Subcontracting & Labor
  createSubcontractCommitment(orderId: string, request: CreateSubcontractCommitmentRequest): Promise<SubcontractCommitment> {
    return httpClient<SubcontractCommitment>(`${BASE_URL}/production-orders/${orderId}/subcontract-commitments`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  recordLabor(orderId: string, request: RecordLaborFactRequest): Promise<ProductionCost> {
    return httpClient<ProductionCost>(`${BASE_URL}/production-orders/${orderId}/labor`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  // Costing & WIP
  getOrderCost(orderId: string): Promise<ProductionCost> {
    return httpClient<ProductionCost>(`${BASE_URL}/production-orders/${orderId}/cost`);
  },

  recordCostFact(orderId: string, request: RecordCostFactRequest): Promise<ProductionCost> {
    return httpClient<ProductionCost>(`${BASE_URL}/production-orders/${orderId}/cost-facts`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  allocateOutputCost(orderId: string, request: AllocateOutputCostRequest): Promise<{ id: string; allocationBasis: string }> {
    return httpClient<{ id: string; allocationBasis: string }>(`${BASE_URL}/production-orders/${orderId}/output-cost-allocations`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  getOrderCloseReadiness(orderId: string): Promise<ProductionCloseReadiness> {
    return httpClient<ProductionCloseReadiness>(`${BASE_URL}/production-orders/${orderId}/close-readiness`);
  },

  getCostingSummary(legalEntityId: string): Promise<ManufacturingCostingSummary> {
    return httpClient<ManufacturingCostingSummary>(`${BASE_URL}/legal-entities/${legalEntityId}/costing-summary`, { legalEntityId });
  },

  // MRP & Planning
  listPlanningRuns(legalEntityId: string, limit?: number): Promise<PlanningRun[]> {
    const qs = buildQuery({ legalEntityId, limit });
    return httpClient<PlanningRun[]>(`${BASE_URL}/planning-runs${qs}`, { legalEntityId });
  },

  createPlanningRun(request: CreatePlanningRunRequest): Promise<PlanningRun> {
    return httpClient<PlanningRun>(`${BASE_URL}/planning-runs`, {
      method: "POST",
      body: JSON.stringify(request),
      legalEntityId: request.legalEntityId
    });
  },

  runPlanning(runId: string): Promise<PlanningRun> {
    return httpClient<PlanningRun>(`${BASE_URL}/planning-runs/${runId}/run`, {
      method: "POST"
    });
  },

  listPlannedSupplies(runId: string): Promise<PlannedSupply[]> {
    return httpClient<PlannedSupply[]>(`${BASE_URL}/planning-runs/${runId}/supplies`);
  },

  firmProduction(plannedSupplyId: string, legalEntityId: string, plantId: string): Promise<ProductionOrder> {
    const qs = buildQuery({ legalEntityId, plantId });
    return httpClient<ProductionOrder>(`${BASE_URL}/planned-supplies/${plannedSupplyId}/firm-production${qs}`, {
      method: "POST",
      legalEntityId
    });
  },

  // MPS
  listMpsPlans(legalEntityId: string, filters?: { plantId?: string; productVariantId?: string; from?: string; to?: string; limit?: number }): Promise<MpsPlan[]> {
    const qs = buildQuery({ legalEntityId, ...filters });
    return httpClient<MpsPlan[]>(`${BASE_URL}/mps-plans${qs}`, { legalEntityId });
  },

  createMpsPlan(request: CreateMpsPlanRequest): Promise<MpsPlan> {
    return httpClient<MpsPlan>(`${BASE_URL}/mps-plans`, {
      method: "POST",
      body: JSON.stringify(request),
      legalEntityId: request.legalEntityId
    });
  },

  publishMpsPlan(planId: string): Promise<MpsPlan> {
    return httpClient<MpsPlan>(`${BASE_URL}/mps-plans/${planId}/publish`, {
      method: "POST"
    });
  },

  // Genealogy
  linkGenealogy(orderId: string, request: GenealogyLinkRequest): Promise<GenealogyItem> {
    return httpClient<GenealogyItem>(`${BASE_URL}/production-orders/${orderId}/genealogy`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  getForwardGenealogy(outputId: string): Promise<GenealogyItem[]> {
    return httpClient<GenealogyItem[]>(`${BASE_URL}/outputs/${outputId}/genealogy`);
  },

  getReverseGenealogy(inputEventId: string): Promise<GenealogyItem[]> {
    return httpClient<GenealogyItem[]>(`${BASE_URL}/material-events/${inputEventId}/genealogy`);
  }
};

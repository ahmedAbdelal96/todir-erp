import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { crmApi } from "../services/crm-api.ts";
import type {
  CreateLeadRequest,
  UpdateLeadRequest,
  AssignLeadRequest,
  QualifyLeadRequest,
  DisqualifyLeadRequest,
  ConvertLeadRequest,
  MergeLeadsRequest,
  ImportLeadsRequest,
  CreateOpportunityRequest,
  UpdateOpportunityRequest,
  AssignOpportunityRequest,
  MoveOpportunityStageRequest,
  CloseOpportunityRequest,
  ForecastCategoryRequest,
  UpsertOpportunityInterestRequest,
  UpsertOpportunityContactRoleRequest,
  CreateActivityRequest,
  UpdateActivityRequest,
  CompleteActivityRequest,
  CreatePipelineRequest,
  UpdatePipelineRequest,
  CreateStageRequest,
  ReorderStagesRequest,
  CreateTeamRequest,
  AddTeamMemberRequest,
  CreateCustomFieldDefinitionRequest,
  SetCustomFieldValueRequest,
  QuoteHandoffRequest,
  CreateForecastSnapshotRequest
} from "../types.ts";

export const crmKeys = {
  all: ["crm"] as const,
  leads: (entityId: string, params?: Record<string, unknown>) =>
    [...crmKeys.all, "leads", entityId, params] as const,
  lead: (entityId: string, id: string) =>
    [...crmKeys.all, "lead", entityId, id] as const,
  opportunities: (entityId: string, params?: Record<string, unknown>) =>
    [...crmKeys.all, "opportunities", entityId, params] as const,
  opportunity: (entityId: string, id: string) =>
    [...crmKeys.all, "opportunity", entityId, id] as const,
  opportunityInterests: (entityId: string, id: string) =>
    [...crmKeys.all, "opportunityInterests", entityId, id] as const,
  opportunityContactRoles: (entityId: string, id: string) =>
    [...crmKeys.all, "opportunityContactRoles", entityId, id] as const,
  activities: (entityId: string, params?: Record<string, unknown>) =>
    [...crmKeys.all, "activities", entityId, params] as const,
  sellerHome: (entityId: string, params?: Record<string, unknown>) =>
    [...crmKeys.all, "sellerHome", entityId, params] as const,
  sellerWorklist: (entityId: string, params?: Record<string, unknown>) =>
    [...crmKeys.all, "sellerWorklist", entityId, params] as const,
  sellerAttention: (entityId: string, params?: Record<string, unknown>) =>
    [...crmKeys.all, "sellerAttention", entityId, params] as const,
  pipelines: (entityId: string) =>
    [...crmKeys.all, "pipelines", entityId] as const,
  forecast: (entityId: string, params?: Record<string, unknown>) =>
    [...crmKeys.all, "forecast", entityId, params] as const,
  pipelineInspection: (entityId: string, params?: Record<string, unknown>) =>
    [...crmKeys.all, "pipelineInspection", entityId, params] as const,
  reportsSummary: (entityId: string, params?: Record<string, unknown>) =>
    [...crmKeys.all, "reportsSummary", entityId, params] as const,
  teams: (entityId: string) =>
    [...crmKeys.all, "teams", entityId] as const,
  teamMembers: (entityId: string, teamId: string) =>
    [...crmKeys.all, "teamMembers", entityId, teamId] as const,
  customFields: (entityId: string, entityType: string) =>
    [...crmKeys.all, "customFields", entityId, entityType] as const,
  customFieldValues: (entityId: string, entityType: string, recordId: string) =>
    [...crmKeys.all, "customFieldValues", entityId, entityType, recordId] as const,
  customer360: (entityId: string, partyId: string) =>
    [...crmKeys.all, "customer360", entityId, partyId] as const,
  audit: (entityId: string, params?: Record<string, unknown>) =>
    [...crmKeys.all, "audit", entityId, params] as const,
  externalEvents: (entityId: string, params?: Record<string, unknown>) =>
    [...crmKeys.all, "externalEvents", entityId, params] as const
};

// --- Lead Queries & Mutations ---

export function useLeads(
  legalEntityId: string,
  params?: {
    status?: string | undefined;
    search?: string | undefined;
    scope?: string | undefined;
    page?: number | undefined;
    pageSize?: number | undefined;
  }
) {
  return useQuery({
    queryKey: crmKeys.leads(legalEntityId, params),
    queryFn: () => crmApi.listLeads(legalEntityId, params),
    enabled: Boolean(legalEntityId)
  });
}

export function useLead(legalEntityId: string, id: string) {
  return useQuery({
    queryKey: crmKeys.lead(legalEntityId, id),
    queryFn: () => crmApi.getLead(legalEntityId, id),
    enabled: Boolean(legalEntityId && id)
  });
}

export function useCreateLeadMutation(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateLeadRequest) => crmApi.createLead(legalEntityId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [...crmKeys.all, "leads", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: [...crmKeys.all, "sellerHome", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: [...crmKeys.all, "reportsSummary", legalEntityId] });
    }
  });
}

export function useUpdateLeadMutation(legalEntityId: string, id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: UpdateLeadRequest) => crmApi.updateLead(legalEntityId, id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: crmKeys.lead(legalEntityId, id) });
      queryClient.invalidateQueries({ queryKey: [...crmKeys.all, "leads", legalEntityId] });
    }
  });
}

export function useAssignLeadMutation(legalEntityId: string, id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: AssignLeadRequest) => crmApi.assignLead(legalEntityId, id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: crmKeys.lead(legalEntityId, id) });
      queryClient.invalidateQueries({ queryKey: [...crmKeys.all, "leads", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: [...crmKeys.all, "sellerHome", legalEntityId] });
    }
  });
}

export function useQualifyLeadMutation(legalEntityId: string, id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: QualifyLeadRequest) => crmApi.qualifyLead(legalEntityId, id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: crmKeys.lead(legalEntityId, id) });
      queryClient.invalidateQueries({ queryKey: [...crmKeys.all, "leads", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: [...crmKeys.all, "reportsSummary", legalEntityId] });
    }
  });
}

export function useDisqualifyLeadMutation(legalEntityId: string, id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: DisqualifyLeadRequest) => crmApi.disqualifyLead(legalEntityId, id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: crmKeys.lead(legalEntityId, id) });
      queryClient.invalidateQueries({ queryKey: [...crmKeys.all, "leads", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: [...crmKeys.all, "reportsSummary", legalEntityId] });
    }
  });
}

export function useConvertLeadMutation(legalEntityId: string, id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: ConvertLeadRequest) => crmApi.convertLead(legalEntityId, id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: crmKeys.lead(legalEntityId, id) });
      queryClient.invalidateQueries({ queryKey: [...crmKeys.all, "leads", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: [...crmKeys.all, "opportunities", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: [...crmKeys.all, "reportsSummary", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: [...crmKeys.all, "forecast", legalEntityId] });
    }
  });
}

export function useMergeLeadsMutation(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: MergeLeadsRequest) => crmApi.mergeLeads(legalEntityId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [...crmKeys.all, "leads", legalEntityId] });
    }
  });
}

export function useImportLeadsMutation(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: ImportLeadsRequest) => crmApi.importLeads(legalEntityId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [...crmKeys.all, "leads", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: [...crmKeys.all, "reportsSummary", legalEntityId] });
    }
  });
}

// --- Opportunity Queries & Mutations ---

export function useOpportunities(
  legalEntityId: string,
  params?: {
    status?: string | undefined;
    pipelineId?: string | undefined;
    search?: string | undefined;
    scope?: string | undefined;
    page?: number | undefined;
    pageSize?: number | undefined;
  }
) {
  return useQuery({
    queryKey: crmKeys.opportunities(legalEntityId, params),
    queryFn: () => crmApi.listOpportunities(legalEntityId, params),
    enabled: Boolean(legalEntityId)
  });
}

export function useOpportunity(legalEntityId: string, id: string) {
  return useQuery({
    queryKey: crmKeys.opportunity(legalEntityId, id),
    queryFn: () => crmApi.getOpportunity(legalEntityId, id),
    enabled: Boolean(legalEntityId && id)
  });
}

export function useCreateOpportunityMutation(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateOpportunityRequest) => crmApi.createOpportunity(legalEntityId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [...crmKeys.all, "opportunities", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: [...crmKeys.all, "forecast", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: [...crmKeys.all, "reportsSummary", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: [...crmKeys.all, "pipelineInspection", legalEntityId] });
    }
  });
}

export function useUpdateOpportunityMutation(legalEntityId: string, id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: UpdateOpportunityRequest) => crmApi.updateOpportunity(legalEntityId, id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: crmKeys.opportunity(legalEntityId, id) });
      queryClient.invalidateQueries({ queryKey: [...crmKeys.all, "opportunities", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: [...crmKeys.all, "forecast", legalEntityId] });
    }
  });
}

export function useAssignOpportunityMutation(legalEntityId: string, id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: AssignOpportunityRequest) => crmApi.assignOpportunity(legalEntityId, id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: crmKeys.opportunity(legalEntityId, id) });
      queryClient.invalidateQueries({ queryKey: [...crmKeys.all, "opportunities", legalEntityId] });
    }
  });
}

export function useMoveOpportunityStageMutation(legalEntityId: string, id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: MoveOpportunityStageRequest) => crmApi.moveOpportunityStage(legalEntityId, id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: crmKeys.opportunity(legalEntityId, id) });
      queryClient.invalidateQueries({ queryKey: [...crmKeys.all, "opportunities", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: [...crmKeys.all, "forecast", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: [...crmKeys.all, "pipelineInspection", legalEntityId] });
    }
  });
}

export function useWinOpportunityMutation(legalEntityId: string, id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CloseOpportunityRequest) => crmApi.winOpportunity(legalEntityId, id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: crmKeys.opportunity(legalEntityId, id) });
      queryClient.invalidateQueries({ queryKey: [...crmKeys.all, "opportunities", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: [...crmKeys.all, "forecast", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: [...crmKeys.all, "reportsSummary", legalEntityId] });
    }
  });
}

export function useLoseOpportunityMutation(legalEntityId: string, id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CloseOpportunityRequest) => crmApi.loseOpportunity(legalEntityId, id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: crmKeys.opportunity(legalEntityId, id) });
      queryClient.invalidateQueries({ queryKey: [...crmKeys.all, "opportunities", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: [...crmKeys.all, "forecast", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: [...crmKeys.all, "reportsSummary", legalEntityId] });
    }
  });
}

export function useSetForecastCategoryMutation(legalEntityId: string, id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: ForecastCategoryRequest) => crmApi.setForecastCategory(legalEntityId, id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: crmKeys.opportunity(legalEntityId, id) });
      queryClient.invalidateQueries({ queryKey: [...crmKeys.all, "opportunities", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: [...crmKeys.all, "forecast", legalEntityId] });
    }
  });
}

export function useCreateQuotationMutation(legalEntityId: string, id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: QuoteHandoffRequest) => crmApi.createQuotation(legalEntityId, id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: crmKeys.opportunity(legalEntityId, id) });
      queryClient.invalidateQueries({ queryKey: [...crmKeys.all, "opportunities", legalEntityId] });
    }
  });
}

export function useOpportunityInterests(legalEntityId: string, id: string) {
  return useQuery({
    queryKey: crmKeys.opportunityInterests(legalEntityId, id),
    queryFn: () => crmApi.listOpportunityInterests(legalEntityId, id),
    enabled: Boolean(legalEntityId && id)
  });
}

export function useAddOpportunityInterestMutation(legalEntityId: string, id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: UpsertOpportunityInterestRequest) => crmApi.addOpportunityInterest(legalEntityId, id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: crmKeys.opportunityInterests(legalEntityId, id) });
    }
  });
}

export function useOpportunityContactRoles(legalEntityId: string, id: string) {
  return useQuery({
    queryKey: crmKeys.opportunityContactRoles(legalEntityId, id),
    queryFn: () => crmApi.listOpportunityContactRoles(legalEntityId, id),
    enabled: Boolean(legalEntityId && id)
  });
}

export function useAddOpportunityContactRoleMutation(legalEntityId: string, id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: UpsertOpportunityContactRoleRequest) => crmApi.addOpportunityContactRole(legalEntityId, id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: crmKeys.opportunityContactRoles(legalEntityId, id) });
    }
  });
}

// --- Activity Queries & Mutations ---

export function useActivities(
  legalEntityId: string,
  params?: {
    state?: string | undefined;
    scope?: string | undefined;
    search?: string | undefined;
    page?: number | undefined;
    pageSize?: number | undefined;
  }
) {
  return useQuery({
    queryKey: crmKeys.activities(legalEntityId, params),
    queryFn: () => crmApi.listActivities(legalEntityId, params),
    enabled: Boolean(legalEntityId)
  });
}

export function useSellerHome(legalEntityId: string, params?: { page?: number; pageSize?: number }) {
  return useQuery({
    queryKey: crmKeys.sellerHome(legalEntityId, params),
    queryFn: () => crmApi.getSellerHome(legalEntityId, params),
    enabled: Boolean(legalEntityId)
  });
}

export function useSellerWorklist(legalEntityId: string, params?: { page?: number; pageSize?: number }) {
  return useQuery({
    queryKey: crmKeys.sellerWorklist(legalEntityId, params),
    queryFn: () => crmApi.getSellerWorklist(legalEntityId, params),
    enabled: Boolean(legalEntityId)
  });
}

export function useSellerAttention(legalEntityId: string, params?: { page?: number; pageSize?: number }) {
  return useQuery({
    queryKey: crmKeys.sellerAttention(legalEntityId, params),
    queryFn: () => crmApi.getSellerAttention(legalEntityId, params),
    enabled: Boolean(legalEntityId)
  });
}

export function useCreateActivityMutation(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateActivityRequest) => crmApi.createActivity(legalEntityId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [...crmKeys.all, "activities", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: [...crmKeys.all, "sellerHome", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: [...crmKeys.all, "sellerWorklist", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: [...crmKeys.all, "sellerAttention", legalEntityId] });
    }
  });
}

export function useUpdateActivityMutation(legalEntityId: string, id?: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (args: UpdateActivityRequest | { activityId: string; data: UpdateActivityRequest }) => {
      const actId = id ?? ("activityId" in args ? args.activityId : "");
      const req = "data" in args ? args.data : args;
      return crmApi.updateActivity(legalEntityId, actId, req);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [...crmKeys.all, "activities", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: [...crmKeys.all, "sellerHome", legalEntityId] });
    }
  });
}

export function useCompleteActivityMutation(legalEntityId: string, id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CompleteActivityRequest) => crmApi.completeActivity(legalEntityId, id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [...crmKeys.all, "activities", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: [...crmKeys.all, "sellerHome", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: [...crmKeys.all, "sellerWorklist", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: [...crmKeys.all, "sellerAttention", legalEntityId] });
    }
  });
}

export function useCancelActivityMutation(legalEntityId: string, id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (version: number) => crmApi.cancelActivity(legalEntityId, id, version),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [...crmKeys.all, "activities", legalEntityId] });
      queryClient.invalidateQueries({ queryKey: [...crmKeys.all, "sellerHome", legalEntityId] });
    }
  });
}

// --- Pipelines & Stages ---

export function usePipelines(legalEntityId: string) {
  return useQuery({
    queryKey: crmKeys.pipelines(legalEntityId),
    queryFn: () => crmApi.listPipelines(legalEntityId),
    enabled: Boolean(legalEntityId)
  });
}

export function useCreatePipelineMutation(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreatePipelineRequest) => crmApi.createPipeline(legalEntityId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: crmKeys.pipelines(legalEntityId) });
    }
  });
}

export function useUpdatePipelineMutation(legalEntityId: string, pipelineId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: UpdatePipelineRequest) => crmApi.updatePipeline(legalEntityId, pipelineId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: crmKeys.pipelines(legalEntityId) });
    }
  });
}

export function useCreateStageMutation(legalEntityId: string, pipelineId?: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (args: CreateStageRequest | { pipelineId: string; data: CreateStageRequest }) => {
      const pipeId = pipelineId ?? ("pipelineId" in args ? args.pipelineId : "");
      const req = "data" in args ? args.data : args;
      return crmApi.createStage(legalEntityId, pipeId, req);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: crmKeys.pipelines(legalEntityId) });
    }
  });
}

export function useReorderStagesMutation(legalEntityId: string, pipelineId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: ReorderStagesRequest) => crmApi.reorderStages(legalEntityId, pipelineId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: crmKeys.pipelines(legalEntityId) });
    }
  });
}

// --- Forecast & Management ---

export function useForecast(legalEntityId: string, params?: { from?: string; to?: string; currency?: string }) {
  return useQuery({
    queryKey: crmKeys.forecast(legalEntityId, params),
    queryFn: () => crmApi.getForecast(legalEntityId, params),
    enabled: Boolean(legalEntityId)
  });
}

export function usePipelineInspection(
  legalEntityId: string,
  params?: { status?: string; pipelineId?: string; search?: string; scope?: string; page?: number; pageSize?: number }
) {
  return useQuery({
    queryKey: crmKeys.pipelineInspection(legalEntityId, params),
    queryFn: () => crmApi.getPipelineInspection(legalEntityId, params),
    enabled: Boolean(legalEntityId)
  });
}

export function useReportSummary(legalEntityId: string, params?: { currency?: string; scope?: string }) {
  return useQuery({
    queryKey: crmKeys.reportsSummary(legalEntityId, params),
    queryFn: () => crmApi.getReportSummary(legalEntityId, params),
    enabled: Boolean(legalEntityId)
  });
}

export function useCreateForecastSnapshotMutation(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateForecastSnapshotRequest) => crmApi.createForecastSnapshot(legalEntityId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [...crmKeys.all, "forecast", legalEntityId] });
    }
  });
}

// --- Teams ---

export function useTeams(legalEntityId: string) {
  return useQuery({
    queryKey: crmKeys.teams(legalEntityId),
    queryFn: () => crmApi.listTeams(legalEntityId),
    enabled: Boolean(legalEntityId)
  });
}

export function useCreateTeamMutation(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateTeamRequest) => crmApi.createTeam(legalEntityId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: crmKeys.teams(legalEntityId) });
    }
  });
}

export function useTeamMembers(legalEntityId: string, teamId?: string) {
  return useQuery({
    queryKey: teamId ? crmKeys.teamMembers(legalEntityId, teamId) : [...crmKeys.all, "teamMembers", legalEntityId, "none"],
    queryFn: () => teamId ? crmApi.listTeamMembers(legalEntityId, teamId) : Promise.resolve([]),
    enabled: Boolean(legalEntityId && teamId)
  });
}

export function useAddTeamMemberMutation(legalEntityId: string, teamId?: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (args: AddTeamMemberRequest | { teamId: string; data: AddTeamMemberRequest }) => {
      const tmId = teamId ?? ("teamId" in args ? args.teamId : "");
      const req = "data" in args ? args.data : args;
      return crmApi.addTeamMember(legalEntityId, tmId, req);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: crmKeys.teams(legalEntityId) });
      if (teamId) {
        queryClient.invalidateQueries({ queryKey: crmKeys.teamMembers(legalEntityId, teamId) });
      } else {
        queryClient.invalidateQueries({ queryKey: [...crmKeys.all, "teamMembers"] });
      }
    }
  });
}

export function useEndTeamMemberMutation(legalEntityId: string, teamId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (userId: string) => crmApi.endTeamMember(legalEntityId, teamId, userId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: crmKeys.teamMembers(legalEntityId, teamId) });
    }
  });
}

// --- Custom Fields ---

export function useCustomFields(legalEntityId: string, entityType?: string) {
  return useQuery({
    queryKey: entityType ? crmKeys.customFields(legalEntityId, entityType) : [...crmKeys.all, "customFields", legalEntityId],
    queryFn: () => entityType ? crmApi.listCustomFields(legalEntityId, entityType) : crmApi.listCustomFields(legalEntityId, "Lead"),
    enabled: Boolean(legalEntityId)
  });
}

export function useCustomFieldValues(legalEntityId: string, entityType: string, recordId: string) {
  return useQuery({
    queryKey: crmKeys.customFieldValues(legalEntityId, entityType, recordId),
    queryFn: () => crmApi.listCustomFieldValues(legalEntityId, entityType, recordId),
    enabled: Boolean(legalEntityId && entityType && recordId)
  });
}

export function useCreateCustomFieldMutation(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateCustomFieldDefinitionRequest) => crmApi.createCustomField(legalEntityId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [...crmKeys.all, "customFields", legalEntityId] });
    }
  });
}

export function useSetCustomFieldValuesMutation(legalEntityId: string, entityType: string, recordId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: SetCustomFieldValueRequest) => crmApi.setCustomFieldValues(legalEntityId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: crmKeys.customFieldValues(legalEntityId, entityType, recordId) });
    }
  });
}

// --- Customer 360 ---

export function useCustomer360(legalEntityId: string, partyId: string) {
  return useQuery({
    queryKey: crmKeys.customer360(legalEntityId, partyId),
    queryFn: () => crmApi.getCustomer360(legalEntityId, partyId),
    enabled: Boolean(legalEntityId && partyId)
  });
}

// --- Audit & External Events ---

export function useCrmAudit(legalEntityId: string, params?: { offset?: number; limit?: number }) {
  return useQuery({
    queryKey: crmKeys.audit(legalEntityId, params),
    queryFn: () => crmApi.listAudit(legalEntityId, params),
    enabled: Boolean(legalEntityId)
  });
}

export function useCrmExternalEvents(legalEntityId: string, params?: { messageType?: string; page?: number; pageSize?: number }) {
  return useQuery({
    queryKey: crmKeys.externalEvents(legalEntityId, params),
    queryFn: () => crmApi.listExternalEvents(legalEntityId, params),
    enabled: Boolean(legalEntityId)
  });
}

// Semantic and query aliases
export {
  useLeads as useCrmLeadsQuery,
  useOpportunities as useCrmOpportunitiesQuery,
  useActivities as useCrmActivitiesQuery,
  useSellerHome as useCrmSellerHomeQuery,
  useSellerWorklist as useCrmSellerWorklistQuery,
  useSellerAttention as useCrmSellerAttentionQuery,
  usePipelines as useCrmPipelinesQuery,
  useForecast as useCrmForecastQuery,
  usePipelineInspection as useCrmPipelineInspectionQuery,
  useReportSummary as useCrmReportsSummaryQuery,
  useTeams as useCrmTeamsQuery,
  useTeamMembers as useCrmTeamMembersQuery,
  useCustomFields as useCrmCustomFieldsQuery,
  useCustomer360 as useCrmCustomer360Query,
  useCrmAudit as useCrmAuditQuery,
};

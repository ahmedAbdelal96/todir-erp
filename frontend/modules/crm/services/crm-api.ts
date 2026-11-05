import { httpClient, generateKey } from "../../../core/api/http-client.ts";
import type {
  CrmPage,
  Lead,
  CreateLeadRequest,
  UpdateLeadRequest,
  AssignLeadRequest,
  QualifyLeadRequest,
  DisqualifyLeadRequest,
  ConvertLeadRequest,
  MergeLeadsRequest,
  ImportLeadsRequest,
  ImportLeadsResult,
  Opportunity,
  CreateOpportunityRequest,
  UpdateOpportunityRequest,
  AssignOpportunityRequest,
  MoveOpportunityStageRequest,
  CloseOpportunityRequest,
  ForecastCategoryRequest,
  OpportunityInterest,
  UpsertOpportunityInterestRequest,
  OpportunityContactRole,
  UpsertOpportunityContactRoleRequest,
  Activity,
  CreateActivityRequest,
  UpdateActivityRequest,
  CompleteActivityRequest,
  Pipeline,
  CreatePipelineRequest,
  UpdatePipelineRequest,
  CreateStageRequest,
  ReorderStagesRequest,
  Team,
  TeamMember,
  CreateTeamRequest,
  AddTeamMemberRequest,
  ForecastDto,
  CreateForecastSnapshotRequest,
  CrmReportDto,
  Customer360Dto,
  CustomFieldDefinition,
  CreateCustomFieldDefinitionRequest,
  CustomFieldValue,
  SetCustomFieldValueRequest,
  QuoteHandoffRequest,
  CrmExternalEvent
} from "../types.ts";

const BASE = "/api/v1/crm";

export const crmApi = {
  // Leads
  listLeads: (
    legalEntityId: string,
    params?: {
      status?: string | undefined;
      search?: string | undefined;
      scope?: string | undefined;
      page?: number | undefined;
      pageSize?: number | undefined;
    }
  ): Promise<CrmPage<Lead>> => {
    const q = new URLSearchParams();
    if (params?.status) q.set("status", params.status);
    if (params?.search) q.set("search", params.search);
    if (params?.scope) q.set("scope", params.scope);
    if (params?.page) q.set("page", String(params.page));
    if (params?.pageSize) q.set("pageSize", String(params.pageSize));
    return httpClient<CrmPage<Lead>>(`${BASE}/legal-entities/${legalEntityId}/leads?${q.toString()}`);
  },

  getLead: (legalEntityId: string, id: string): Promise<Lead> =>
    httpClient<Lead>(`${BASE}/legal-entities/${legalEntityId}/leads/${id}`),

  createLead: (legalEntityId: string, request: CreateLeadRequest): Promise<Lead> => {
    const idempotencyKey = generateKey();
    return httpClient<Lead>(`${BASE}/legal-entities/${legalEntityId}/leads`, {
      method: "POST",
      headers: { "Idempotency-Key": idempotencyKey },
      body: JSON.stringify(request)
    });
  },

  updateLead: (legalEntityId: string, id: string, request: UpdateLeadRequest): Promise<Lead> =>
    httpClient<Lead>(`${BASE}/legal-entities/${legalEntityId}/leads/${id}`, {
      method: "PUT",
      body: JSON.stringify(request)
    }),

  assignLead: (legalEntityId: string, id: string, request: AssignLeadRequest): Promise<Lead> =>
    httpClient<Lead>(`${BASE}/legal-entities/${legalEntityId}/leads/${id}/assign`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  qualifyLead: (legalEntityId: string, id: string, request: QualifyLeadRequest): Promise<Lead> =>
    httpClient<Lead>(`${BASE}/legal-entities/${legalEntityId}/leads/${id}/qualify`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  disqualifyLead: (legalEntityId: string, id: string, request: DisqualifyLeadRequest): Promise<Lead> =>
    httpClient<Lead>(`${BASE}/legal-entities/${legalEntityId}/leads/${id}/disqualify`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  convertLead: (legalEntityId: string, id: string, request: ConvertLeadRequest): Promise<Opportunity> =>
    httpClient<Opportunity>(`${BASE}/legal-entities/${legalEntityId}/leads/${id}/convert`, {
      method: "POST",
      headers: { "Idempotency-Key": request.idempotencyKey || generateKey() },
      body: JSON.stringify(request)
    }),

  mergeLeads: (legalEntityId: string, request: MergeLeadsRequest): Promise<Lead> =>
    httpClient<Lead>(`${BASE}/legal-entities/${legalEntityId}/leads/merge`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  importLeads: (legalEntityId: string, request: ImportLeadsRequest): Promise<ImportLeadsResult> =>
    httpClient<ImportLeadsResult>(`${BASE}/legal-entities/${legalEntityId}/leads/import`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  exportLeads: async (
    legalEntityId: string,
    params?: { status?: string | undefined; search?: string | undefined }
  ): Promise<string> => {
    const q = new URLSearchParams();
    if (params?.status) q.set("status", params.status);
    if (params?.search) q.set("search", params.search);
    const res = await fetch(`${BASE}/legal-entities/${legalEntityId}/leads/export?${q.toString()}`, {
      headers: { Accept: "text/csv" }
    });
    if (!res.ok) throw new Error(`Export failed with status ${res.status}`);
    return res.text();
  },

  // Opportunities
  listOpportunities: (
    legalEntityId: string,
    params?: {
      status?: string | undefined;
      pipelineId?: string | undefined;
      search?: string | undefined;
      scope?: string | undefined;
      page?: number | undefined;
      pageSize?: number | undefined;
    }
  ): Promise<CrmPage<Opportunity>> => {
    const q = new URLSearchParams();
    if (params?.status) q.set("status", params.status);
    if (params?.pipelineId) q.set("pipelineId", params.pipelineId);
    if (params?.search) q.set("search", params.search);
    if (params?.scope) q.set("scope", params.scope);
    if (params?.page) q.set("page", String(params.page));
    if (params?.pageSize) q.set("pageSize", String(params.pageSize));
    return httpClient<CrmPage<Opportunity>>(`${BASE}/legal-entities/${legalEntityId}/opportunities?${q.toString()}`);
  },

  getOpportunity: (legalEntityId: string, id: string): Promise<Opportunity> =>
    httpClient<Opportunity>(`${BASE}/legal-entities/${legalEntityId}/opportunities/${id}`),

  createOpportunity: (legalEntityId: string, request: CreateOpportunityRequest): Promise<Opportunity> =>
    httpClient<Opportunity>(`${BASE}/legal-entities/${legalEntityId}/opportunities`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  updateOpportunity: (legalEntityId: string, id: string, request: UpdateOpportunityRequest): Promise<Opportunity> =>
    httpClient<Opportunity>(`${BASE}/legal-entities/${legalEntityId}/opportunities/${id}`, {
      method: "PUT",
      body: JSON.stringify(request)
    }),

  assignOpportunity: (legalEntityId: string, id: string, request: AssignOpportunityRequest): Promise<Opportunity> =>
    httpClient<Opportunity>(`${BASE}/legal-entities/${legalEntityId}/opportunities/${id}/assign`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  setForecastCategory: (legalEntityId: string, id: string, request: ForecastCategoryRequest): Promise<Opportunity> =>
    httpClient<Opportunity>(`${BASE}/legal-entities/${legalEntityId}/opportunities/${id}/forecast-category`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  moveOpportunityStage: (legalEntityId: string, id: string, request: MoveOpportunityStageRequest): Promise<Opportunity> =>
    httpClient<Opportunity>(`${BASE}/legal-entities/${legalEntityId}/opportunities/${id}/stage`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  winOpportunity: (legalEntityId: string, id: string, request: CloseOpportunityRequest): Promise<Opportunity> =>
    httpClient<Opportunity>(`${BASE}/legal-entities/${legalEntityId}/opportunities/${id}/won`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  loseOpportunity: (legalEntityId: string, id: string, request: CloseOpportunityRequest): Promise<Opportunity> =>
    httpClient<Opportunity>(`${BASE}/legal-entities/${legalEntityId}/opportunities/${id}/lost`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  createQuotation: (legalEntityId: string, id: string, request: QuoteHandoffRequest): Promise<{ id: string }> =>
    httpClient<{ id: string }>(`${BASE}/legal-entities/${legalEntityId}/opportunities/${id}/quotation`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  listOpportunityInterests: (legalEntityId: string, id: string): Promise<readonly OpportunityInterest[]> =>
    httpClient<readonly OpportunityInterest[]>(`${BASE}/legal-entities/${legalEntityId}/opportunities/${id}/interests`),

  addOpportunityInterest: (legalEntityId: string, id: string, request: UpsertOpportunityInterestRequest): Promise<OpportunityInterest> =>
    httpClient<OpportunityInterest>(`${BASE}/legal-entities/${legalEntityId}/opportunities/${id}/interests`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  listOpportunityContactRoles: (legalEntityId: string, id: string): Promise<readonly OpportunityContactRole[]> =>
    httpClient<readonly OpportunityContactRole[]>(`${BASE}/legal-entities/${legalEntityId}/opportunities/${id}/contact-roles`),

  addOpportunityContactRole: (legalEntityId: string, id: string, request: UpsertOpportunityContactRoleRequest): Promise<OpportunityContactRole> =>
    httpClient<OpportunityContactRole>(`${BASE}/legal-entities/${legalEntityId}/opportunities/${id}/contact-roles`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // Activities
  listActivities: (
    legalEntityId: string,
    params?: {
      state?: string | undefined;
      scope?: string | undefined;
      search?: string | undefined;
      page?: number | undefined;
      pageSize?: number | undefined;
    }
  ): Promise<CrmPage<Activity>> => {
    const q = new URLSearchParams();
    if (params?.state) {
      const stateParam = params.state === "Pending" ? "Scheduled" : params.state;
      q.set("state", stateParam);
    }
    if (params?.scope) q.set("scope", params.scope);
    if (params?.search) q.set("search", params.search);
    if (params?.page) q.set("page", String(params.page));
    if (params?.pageSize) q.set("pageSize", String(params.pageSize));
    return httpClient<CrmPage<Activity>>(`${BASE}/legal-entities/${legalEntityId}/activities?${q.toString()}`);
  },

  createActivity: (legalEntityId: string, request: CreateActivityRequest): Promise<Activity> => {
    const idempotencyKey = request.idempotencyKey || generateKey();
    return httpClient<Activity>(`${BASE}/legal-entities/${legalEntityId}/activities`, {
      method: "POST",
      headers: { "Idempotency-Key": idempotencyKey },
      body: JSON.stringify(request)
    });
  },

  updateActivity: (legalEntityId: string, id: string, request: UpdateActivityRequest): Promise<Activity> =>
    httpClient<Activity>(`${BASE}/legal-entities/${legalEntityId}/activities/${id}`, {
      method: "PUT",
      body: JSON.stringify(request)
    }),

  completeActivity: (legalEntityId: string, id: string, request: CompleteActivityRequest): Promise<Activity> =>
    httpClient<Activity>(`${BASE}/legal-entities/${legalEntityId}/activities/${id}/complete`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  cancelActivity: (legalEntityId: string, id: string, version: number): Promise<Activity> =>
    httpClient<Activity>(`${BASE}/legal-entities/${legalEntityId}/activities/${id}/cancel?version=${version}`, {
      method: "POST"
    }),

  // Seller Home & Worklist
  getSellerHome: (
    legalEntityId: string,
    params?: { page?: number; pageSize?: number }
  ): Promise<CrmPage<Activity>> => {
    const q = new URLSearchParams();
    if (params?.page) q.set("page", String(params.page));
    if (params?.pageSize) q.set("pageSize", String(params.pageSize));
    return httpClient<CrmPage<Activity>>(`${BASE}/legal-entities/${legalEntityId}/home?${q.toString()}`);
  },

  getSellerWorklist: (
    legalEntityId: string,
    params?: { page?: number; pageSize?: number }
  ): Promise<CrmPage<Activity>> => {
    const q = new URLSearchParams();
    if (params?.page) q.set("page", String(params.page));
    if (params?.pageSize) q.set("pageSize", String(params.pageSize));
    return httpClient<CrmPage<Activity>>(`${BASE}/legal-entities/${legalEntityId}/worklist?${q.toString()}`);
  },

  getSellerAttention: (
    legalEntityId: string,
    params?: { page?: number; pageSize?: number }
  ): Promise<CrmPage<Activity>> => {
    const q = new URLSearchParams();
    if (params?.page) q.set("page", String(params.page));
    if (params?.pageSize) q.set("pageSize", String(params.pageSize));
    return httpClient<CrmPage<Activity>>(`${BASE}/legal-entities/${legalEntityId}/attention?${q.toString()}`);
  },

  // Pipelines & Stages
  listPipelines: (legalEntityId: string): Promise<readonly Pipeline[]> =>
    httpClient<readonly Pipeline[]>(`${BASE}/legal-entities/${legalEntityId}/pipelines`),

  createPipeline: (legalEntityId: string, request: CreatePipelineRequest): Promise<Pipeline> =>
    httpClient<Pipeline>(`${BASE}/legal-entities/${legalEntityId}/pipelines`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  updatePipeline: (legalEntityId: string, pipelineId: string, request: UpdatePipelineRequest): Promise<Pipeline> =>
    httpClient<Pipeline>(`${BASE}/legal-entities/${legalEntityId}/pipelines/${pipelineId}`, {
      method: "PUT",
      body: JSON.stringify(request)
    }),

  createStage: (legalEntityId: string, pipelineId: string, request: CreateStageRequest): Promise<Pipeline> =>
    httpClient<Pipeline>(`${BASE}/legal-entities/${legalEntityId}/pipelines/${pipelineId}/stages`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  reorderStages: (legalEntityId: string, pipelineId: string, request: ReorderStagesRequest): Promise<Pipeline> =>
    httpClient<Pipeline>(`${BASE}/legal-entities/${legalEntityId}/pipelines/${pipelineId}/reorder-stages`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // Forecast & Inspection
  getForecast: (
    legalEntityId: string,
    params?: { from?: string; to?: string; currency?: string }
  ): Promise<ForecastDto> => {
    const q = new URLSearchParams();
    if (params?.from) q.set("from", params.from);
    if (params?.to) q.set("to", params.to);
    if (params?.currency) q.set("currency", params.currency);
    return httpClient<ForecastDto>(`${BASE}/legal-entities/${legalEntityId}/forecast?${q.toString()}`);
  },

  getPipelineInspection: (
    legalEntityId: string,
    params?: {
      status?: string | undefined;
      pipelineId?: string | undefined;
      search?: string | undefined;
      scope?: string | undefined;
      page?: number | undefined;
      pageSize?: number | undefined;
    }
  ): Promise<CrmPage<Opportunity>> => {
    const q = new URLSearchParams();
    if (params?.status) q.set("status", params.status);
    if (params?.pipelineId) q.set("pipelineId", params.pipelineId);
    if (params?.search) q.set("search", params.search);
    if (params?.scope) q.set("scope", params.scope);
    if (params?.page) q.set("page", String(params.page));
    if (params?.pageSize) q.set("pageSize", String(params.pageSize));
    return httpClient<CrmPage<Opportunity>>(`${BASE}/legal-entities/${legalEntityId}/pipeline-inspection?${q.toString()}`);
  },

  getReportSummary: (
    legalEntityId: string,
    params?: { currency?: string; scope?: string }
  ): Promise<CrmReportDto> => {
    const q = new URLSearchParams();
    if (params?.currency) q.set("currency", params.currency);
    if (params?.scope) q.set("scope", params.scope);
    return httpClient<CrmReportDto>(`${BASE}/legal-entities/${legalEntityId}/reports/summary?${q.toString()}`);
  },

  createForecastSnapshot: (legalEntityId: string, request: CreateForecastSnapshotRequest): Promise<{ id: string }> =>
    httpClient<{ id: string }>(`${BASE}/legal-entities/${legalEntityId}/forecast/snapshots`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // Teams
  listTeams: (legalEntityId: string): Promise<readonly Team[]> =>
    httpClient<readonly Team[]>(`${BASE}/legal-entities/${legalEntityId}/teams`),

  createTeam: (legalEntityId: string, request: CreateTeamRequest): Promise<Team> =>
    httpClient<Team>(`${BASE}/legal-entities/${legalEntityId}/teams`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  listTeamMembers: (legalEntityId: string, teamId: string): Promise<readonly TeamMember[]> =>
    httpClient<readonly TeamMember[]>(`${BASE}/legal-entities/${legalEntityId}/teams/${teamId}/members`),

  addTeamMember: (legalEntityId: string, teamId: string, request: AddTeamMemberRequest): Promise<TeamMember> =>
    httpClient<TeamMember>(`${BASE}/legal-entities/${legalEntityId}/teams/${teamId}/members`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  endTeamMember: (legalEntityId: string, teamId: string, userId: string): Promise<{ ended: boolean }> =>
    httpClient<{ ended: boolean }>(`${BASE}/legal-entities/${legalEntityId}/teams/${teamId}/members/${userId}/end`, {
      method: "POST"
    }),

  // Custom Fields
  listCustomFields: (legalEntityId: string, entityType: string): Promise<readonly CustomFieldDefinition[]> =>
    httpClient<readonly CustomFieldDefinition[]>(`${BASE}/legal-entities/${legalEntityId}/custom-fields?entityType=${entityType}`),

  listCustomFieldValues: (legalEntityId: string, entityType: string, recordId: string): Promise<readonly CustomFieldValue[]> =>
    httpClient<readonly CustomFieldValue[]>(`${BASE}/legal-entities/${legalEntityId}/custom-fields/${entityType}/${recordId}`),

  createCustomField: (legalEntityId: string, request: CreateCustomFieldDefinitionRequest): Promise<CustomFieldDefinition> =>
    httpClient<CustomFieldDefinition>(`${BASE}/legal-entities/${legalEntityId}/custom-fields`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  setCustomFieldValues: (legalEntityId: string, request: SetCustomFieldValueRequest): Promise<{ saved: boolean }> =>
    httpClient<{ saved: boolean }>(`${BASE}/legal-entities/${legalEntityId}/custom-fields/values`, {
      method: "PUT",
      body: JSON.stringify(request)
    }),

  // Customer 360
  getCustomer360: (legalEntityId: string, partyId: string): Promise<Customer360Dto> =>
    httpClient<Customer360Dto>(`${BASE}/legal-entities/${legalEntityId}/accounts/${partyId}/workspace`),

  // Audit & External Events
  listAudit: (legalEntityId: string, params?: { offset?: number; limit?: number }): Promise<readonly unknown[]> => {
    const q = new URLSearchParams();
    if (params?.offset !== undefined) q.set("offset", String(params.offset));
    if (params?.limit !== undefined) q.set("limit", String(params.limit));
    return httpClient<readonly unknown[]>(`${BASE}/legal-entities/${legalEntityId}/audit?${q.toString()}`);
  },

  listExternalEvents: (
    legalEntityId: string,
    params?: { messageType?: string; page?: number; pageSize?: number }
  ): Promise<readonly CrmExternalEvent[]> => {
    const q = new URLSearchParams();
    if (params?.messageType) q.set("messageType", params.messageType);
    if (params?.page) q.set("page", String(params.page));
    if (params?.pageSize) q.set("pageSize", String(params.pageSize));
    return httpClient<readonly CrmExternalEvent[]>(`${BASE}/legal-entities/${legalEntityId}/external-events?${q.toString()}`);
  }
};

import { httpClient } from "../../../core/api/http-client.ts";
import type {
  ProjectListItem,
  ProjectDetail,
  FinancialSummary,
  ProjectTaskView,
  MilestoneView,
  TaskDependencyView,
  ProjectDeliverableView,
  ProjectTeamMemberView,
  ProjectResourceRequirementView,
  ProjectResourceAllocationView,
  TimeEntryView,
  BudgetView,
  BudgetVersionView,
  ForecastView,
  BillingRuleView,
  BillingPlanLineView,
  BillableChargeView,
  InvoiceRequestView,
  ChangeRequestView,
  CapitalHandoffView,
  ProjectTemplateView,
  ProjectBaselineView,
  ProjectDocumentView,
  ProjectTimelineEventView,
  CloseView,
  ProjectCloseExceptionView,
  ProjectPortfolioItem,
  ProjectResourceLoadView,
  PagedResult,
  CreateProjectRequest,
  CreateProjectFromSalesSourceRequest,
  UpdateProjectRequest,
  TimeActionRequest,
  CreateProjectTaskRequest,
  CreateTaskDependencyRequest,
  CreateMilestoneRequest,
  MilestoneActionRequest,
  AddProjectTeamMemberRequest,
  CreateTimeEntryRequest,
  CreateBudgetRequest,
  CreateBudgetRevisionRequest,
  CreateForecastRequest,
  RecordCostFactRequest,
  RecordCommitmentRequest,
  CreateBillingRuleRequest,
  CreateBillingPlanLineRequest,
  CreateBillableChargeRequest,
  RequestInvoiceRequest,
  CreateChangeRequest,
  ChangeDecisionRequest,
  CapitalHandoffRequest,
  CreateProjectTemplateRequest,
  CreateResourceRequirementRequest,
  AllocateProjectResourceRequest,
  CreateProjectDeliverableRequest,
  DeliverableActionRequest,
  CreateProjectDocumentRequest,
  CreateProjectBaselineRequest,
  CreateCloseExceptionRequest,
  RateCardView,
  CreateRateCardRequest
} from "../types.ts";

const BASE = "/api/v1/projects";

function buildQuery(params: Record<string, string | number | boolean | null | undefined>): string {
  const query = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== null && value !== "") {
      query.set(key, String(value));
    }
  }
  const s = query.toString();
  return s ? `?${s}` : "";
}

export const projectsApi = {
  // 1. Projects Register / Listing
  listProjects: (
    legalEntityId: string,
    params?: { search?: string; state?: string; projectType?: string; page?: number; pageSize?: number }
  ): Promise<PagedResult<ProjectListItem>> => {
    const q = buildQuery({
      search: params?.search,
      state: params?.state,
      projectType: params?.projectType,
      page: params?.page ?? 1,
      pageSize: params?.pageSize ?? 50
    });
    return httpClient<PagedResult<ProjectListItem>>(`${BASE}/legal-entities/${encodeURIComponent(legalEntityId)}${q}`);
  },

  // 2. Project Detail (360 view)
  getProject: (projectId: string): Promise<ProjectDetail> =>
    httpClient<ProjectDetail>(`${BASE}/${encodeURIComponent(projectId)}`),

  // 3. Create Project
  createProject: (request: CreateProjectRequest): Promise<ProjectDetail> =>
    httpClient<ProjectDetail>(`${BASE}/legal-entities/${encodeURIComponent(request.legalEntityId)}`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // 4. Create from Sales Source (Quotation)
  createFromSalesSource: (request: CreateProjectFromSalesSourceRequest): Promise<ProjectDetail> =>
    httpClient<ProjectDetail>(`${BASE}/from-sales-source`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // 5. Update Project Basic Info
  updateProject: (projectId: string, request: UpdateProjectRequest): Promise<ProjectDetail> =>
    httpClient<ProjectDetail>(`${BASE}/${encodeURIComponent(projectId)}`, {
      method: "PATCH",
      body: JSON.stringify(request)
    }),

  // 6. Transition Lifecycle State
  transitionState: (projectId: string, state: string, version: number): Promise<ProjectDetail> =>
    httpClient<ProjectDetail>(`${BASE}/${encodeURIComponent(projectId)}/state/${encodeURIComponent(state)}`, {
      method: "POST",
      body: JSON.stringify({ version } satisfies TimeActionRequest)
    }),

  // 7. Portfolio & Resource Load
  listPortfolio: (
    legalEntityId: string,
    params?: { page?: number; pageSize?: number }
  ): Promise<PagedResult<ProjectPortfolioItem>> => {
    const q = buildQuery({ page: params?.page ?? 1, pageSize: params?.pageSize ?? 50 });
    return httpClient<PagedResult<ProjectPortfolioItem>>(`${BASE}/legal-entities/${encodeURIComponent(legalEntityId)}/portfolio${q}`);
  },

  listResourceLoad: (legalEntityId: string): Promise<readonly ProjectResourceLoadView[]> =>
    httpClient<readonly ProjectResourceLoadView[]>(`${BASE}/legal-entities/${encodeURIComponent(legalEntityId)}/resource-load`),

  // 8. Financial Summary
  getFinancialSummary: (projectId: string): Promise<FinancialSummary> =>
    httpClient<FinancialSummary>(`${BASE}/${encodeURIComponent(projectId)}/financial-summary`),

  // 9. Tasks
  listTasks: (
    projectId: string,
    params?: { page?: number; pageSize?: number }
  ): Promise<PagedResult<ProjectTaskView>> => {
    const q = buildQuery({ page: params?.page ?? 1, pageSize: params?.pageSize ?? 50 });
    return httpClient<PagedResult<ProjectTaskView>>(`${BASE}/${encodeURIComponent(projectId)}/tasks${q}`);
  },

  addTask: (projectId: string, request: CreateProjectTaskRequest): Promise<ProjectTaskView> =>
    httpClient<ProjectTaskView>(`${BASE}/${encodeURIComponent(projectId)}/tasks`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  addDependency: (projectId: string, request: CreateTaskDependencyRequest): Promise<TaskDependencyView> =>
    httpClient<TaskDependencyView>(`${BASE}/${encodeURIComponent(projectId)}/task-dependencies`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // 10. Milestones
  listMilestones: (
    projectId: string,
    params?: { page?: number; pageSize?: number }
  ): Promise<PagedResult<MilestoneView>> => {
    const q = buildQuery({ page: params?.page ?? 1, pageSize: params?.pageSize ?? 50 });
    return httpClient<PagedResult<MilestoneView>>(`${BASE}/${encodeURIComponent(projectId)}/milestones${q}`);
  },

  addMilestone: (projectId: string, request: CreateMilestoneRequest): Promise<MilestoneView> =>
    httpClient<MilestoneView>(`${BASE}/${encodeURIComponent(projectId)}/milestones`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  milestoneAction: (
    milestoneId: string,
    action: "reach" | "accept" | "reject",
    request: MilestoneActionRequest
  ): Promise<MilestoneView> =>
    httpClient<MilestoneView>(`${BASE}/milestones/${encodeURIComponent(milestoneId)}/${encodeURIComponent(action)}`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // 11. Deliverables
  listDeliverables: (
    projectId: string,
    params?: { page?: number; pageSize?: number }
  ): Promise<PagedResult<ProjectDeliverableView>> => {
    const q = buildQuery({ page: params?.page ?? 1, pageSize: params?.pageSize ?? 50 });
    return httpClient<PagedResult<ProjectDeliverableView>>(`${BASE}/${encodeURIComponent(projectId)}/deliverables${q}`);
  },

  addDeliverable: (projectId: string, request: CreateProjectDeliverableRequest): Promise<ProjectDeliverableView> =>
    httpClient<ProjectDeliverableView>(`${BASE}/${encodeURIComponent(projectId)}/deliverables`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  deliverableAction: (
    deliverableId: string,
    action: "submit" | "accept" | "reject",
    request: DeliverableActionRequest
  ): Promise<ProjectDeliverableView> =>
    httpClient<ProjectDeliverableView>(`${BASE}/deliverables/${encodeURIComponent(deliverableId)}/${encodeURIComponent(action)}`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // 12. Team & Resources
  listTeam: (
    projectId: string,
    params?: { page?: number; pageSize?: number }
  ): Promise<PagedResult<ProjectTeamMemberView>> => {
    const q = buildQuery({ page: params?.page ?? 1, pageSize: params?.pageSize ?? 50 });
    return httpClient<PagedResult<ProjectTeamMemberView>>(`${BASE}/${encodeURIComponent(projectId)}/team${q}`);
  },

  addTeamMember: (projectId: string, request: AddProjectTeamMemberRequest): Promise<ProjectTeamMemberView> =>
    httpClient<ProjectTeamMemberView>(`${BASE}/${encodeURIComponent(projectId)}/team`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  listResourceRequirements: (
    projectId: string,
    params?: { page?: number; pageSize?: number }
  ): Promise<PagedResult<ProjectResourceRequirementView>> => {
    const q = buildQuery({ page: params?.page ?? 1, pageSize: params?.pageSize ?? 50 });
    return httpClient<PagedResult<ProjectResourceRequirementView>>(`${BASE}/${encodeURIComponent(projectId)}/resources/requirements${q}`);
  },

  addResourceRequirement: (projectId: string, request: CreateResourceRequirementRequest): Promise<ProjectResourceRequirementView> =>
    httpClient<ProjectResourceRequirementView>(`${BASE}/${encodeURIComponent(projectId)}/resources/requirements`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  listResourceAllocations: (
    projectId: string,
    params?: { page?: number; pageSize?: number }
  ): Promise<PagedResult<ProjectResourceAllocationView>> => {
    const q = buildQuery({ page: params?.page ?? 1, pageSize: params?.pageSize ?? 50 });
    return httpClient<PagedResult<ProjectResourceAllocationView>>(`${BASE}/${encodeURIComponent(projectId)}/resources/allocations${q}`);
  },

  allocateResource: (projectId: string, request: AllocateProjectResourceRequest): Promise<ProjectResourceAllocationView> =>
    httpClient<ProjectResourceAllocationView>(`${BASE}/${encodeURIComponent(projectId)}/resources/allocations`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // 13. Time Entries (Timesheets)
  listTime: (
    projectId: string,
    params?: { page?: number; pageSize?: number }
  ): Promise<PagedResult<TimeEntryView>> => {
    const q = buildQuery({ page: params?.page ?? 1, pageSize: params?.pageSize ?? 50 });
    return httpClient<PagedResult<TimeEntryView>>(`${BASE}/${encodeURIComponent(projectId)}/time${q}`);
  },

  createTime: (projectId: string, request: CreateTimeEntryRequest): Promise<TimeEntryView> =>
    httpClient<TimeEntryView>(`${BASE}/${encodeURIComponent(projectId)}/time`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  timeAction: (
    timeId: string,
    action: "submit" | "approve" | "reject" | "finalize" | "correct",
    request: TimeActionRequest
  ): Promise<TimeEntryView> =>
    httpClient<TimeEntryView>(`${BASE}/time/${encodeURIComponent(timeId)}/${encodeURIComponent(action)}`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // 14. Budgets & Forecasts
  createBudget: (projectId: string, request: CreateBudgetRequest): Promise<BudgetView> =>
    httpClient<BudgetView>(`${BASE}/${encodeURIComponent(projectId)}/budgets`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  createBudgetRevision: (projectId: string, budgetId: string, request: CreateBudgetRevisionRequest): Promise<BudgetVersionView> =>
    httpClient<BudgetVersionView>(`${BASE}/${encodeURIComponent(projectId)}/budgets/${encodeURIComponent(budgetId)}/revisions`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  approveBudgetVersion: (budgetVersionId: string): Promise<BudgetVersionView> =>
    httpClient<BudgetVersionView>(`${BASE}/budget-versions/${encodeURIComponent(budgetVersionId)}/approve`, {
      method: "POST",
      body: JSON.stringify({})
    }),

  createForecast: (projectId: string, request: CreateForecastRequest): Promise<ForecastView> =>
    httpClient<ForecastView>(`${BASE}/${encodeURIComponent(projectId)}/forecasts`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  recordCostFact: (projectId: string, request: RecordCostFactRequest): Promise<unknown> =>
    httpClient<unknown>(`${BASE}/${encodeURIComponent(projectId)}/cost-facts`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  recordCommitment: (projectId: string, request: RecordCommitmentRequest): Promise<unknown> =>
    httpClient<unknown>(`${BASE}/${encodeURIComponent(projectId)}/commitments`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // 15. Billing Rules, Charges & Invoice Requests
  createBillingRule: (projectId: string, request: CreateBillingRuleRequest): Promise<BillingRuleView> =>
    httpClient<BillingRuleView>(`${BASE}/${encodeURIComponent(projectId)}/billing-rules`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  addBillingPlanLine: (projectId: string, request: CreateBillingPlanLineRequest): Promise<BillingPlanLineView> =>
    httpClient<BillingPlanLineView>(`${BASE}/${encodeURIComponent(projectId)}/billing-plan-lines`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  generateBillingCharges: (projectId: string, billingRuleId: string): Promise<readonly BillableChargeView[]> =>
    httpClient<readonly BillableChargeView[]>(`${BASE}/${encodeURIComponent(projectId)}/billing-rules/${encodeURIComponent(billingRuleId)}/generate-charges`, {
      method: "POST",
      body: JSON.stringify({})
    }),

  createCharge: (projectId: string, request: CreateBillableChargeRequest): Promise<BillableChargeView> =>
    httpClient<BillableChargeView>(`${BASE}/${encodeURIComponent(projectId)}/charges`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  requestInvoice: (projectId: string, request: RequestInvoiceRequest): Promise<InvoiceRequestView> =>
    httpClient<InvoiceRequestView>(`${BASE}/${encodeURIComponent(projectId)}/invoice-requests`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // 16. Change Requests & Decisions
  listChanges: (projectId: string, params?: { page?: number; pageSize?: number }): Promise<PagedResult<ChangeRequestView>> => {
    const q = new URLSearchParams();
    if (params?.page) q.set("page", String(params.page));
    if (params?.pageSize) q.set("pageSize", String(params.pageSize));
    const qs = q.toString();
    return httpClient<PagedResult<ChangeRequestView>>(`${BASE}/${encodeURIComponent(projectId)}/changes${qs ? `?${qs}` : ""}`);
  },

  createChange: (projectId: string, request: CreateChangeRequest): Promise<ChangeRequestView> =>
    httpClient<ChangeRequestView>(`${BASE}/${encodeURIComponent(projectId)}/changes`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  decideChange: (projectId: string, changeId: string, request: ChangeDecisionRequest): Promise<ChangeRequestView> =>
    httpClient<ChangeRequestView>(`${BASE}/${encodeURIComponent(projectId)}/changes/${encodeURIComponent(changeId)}/decision`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // 17. Capital Projects & Fixed Assets Handoff
  createCapitalHandoff: (projectId: string, request: CapitalHandoffRequest): Promise<CapitalHandoffView> =>
    httpClient<CapitalHandoffView>(`${BASE}/${encodeURIComponent(projectId)}/capital-handoffs`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // 18. Baselines
  listBaselines: (
    projectId: string,
    params?: { page?: number; pageSize?: number }
  ): Promise<PagedResult<ProjectBaselineView>> => {
    const q = buildQuery({ page: params?.page ?? 1, pageSize: params?.pageSize ?? 50 });
    return httpClient<PagedResult<ProjectBaselineView>>(`${BASE}/${encodeURIComponent(projectId)}/baselines${q}`);
  },

  createBaseline: (projectId: string, request: CreateProjectBaselineRequest): Promise<ProjectBaselineView> =>
    httpClient<ProjectBaselineView>(`${BASE}/${encodeURIComponent(projectId)}/baselines`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // 19. Documents
  listDocuments: (
    projectId: string,
    params?: { page?: number; pageSize?: number }
  ): Promise<PagedResult<ProjectDocumentView>> => {
    const q = buildQuery({ page: params?.page ?? 1, pageSize: params?.pageSize ?? 50 });
    return httpClient<PagedResult<ProjectDocumentView>>(`${BASE}/${encodeURIComponent(projectId)}/documents${q}`);
  },

  addDocument: (projectId: string, request: CreateProjectDocumentRequest): Promise<ProjectDocumentView> =>
    httpClient<ProjectDocumentView>(`${BASE}/${encodeURIComponent(projectId)}/documents`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // 20. Timeline / Audit
  listTimeline: (
    projectId: string,
    params?: { page?: number; pageSize?: number }
  ): Promise<PagedResult<ProjectTimelineEventView>> => {
    const q = buildQuery({ page: params?.page ?? 1, pageSize: params?.pageSize ?? 100 });
    return httpClient<PagedResult<ProjectTimelineEventView>>(`${BASE}/${encodeURIComponent(projectId)}/timeline${q}`);
  },

  // 21. Close Readiness & Execution
  getCloseReadiness: (projectId: string): Promise<CloseView> =>
    httpClient<CloseView>(`${BASE}/${encodeURIComponent(projectId)}/close`),

  listCloseExceptions: (
    projectId: string,
    params?: { page?: number; pageSize?: number }
  ): Promise<PagedResult<ProjectCloseExceptionView>> => {
    const q = buildQuery({ page: params?.page ?? 1, pageSize: params?.pageSize ?? 50 });
    return httpClient<PagedResult<ProjectCloseExceptionView>>(`${BASE}/${encodeURIComponent(projectId)}/close-exceptions${q}`);
  },

  addCloseException: (projectId: string, request: CreateCloseExceptionRequest): Promise<ProjectCloseExceptionView> =>
    httpClient<ProjectCloseExceptionView>(`${BASE}/${encodeURIComponent(projectId)}/close-exceptions`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  resolveCloseException: (exceptionId: string): Promise<ProjectCloseExceptionView> =>
    httpClient<ProjectCloseExceptionView>(`${BASE}/close-exceptions/${encodeURIComponent(exceptionId)}/resolve`, {
      method: "POST",
      body: JSON.stringify({})
    }),

  closeProject: (projectId: string): Promise<CloseView> =>
    httpClient<CloseView>(`${BASE}/${encodeURIComponent(projectId)}/close`, {
      method: "POST",
      body: JSON.stringify({})
    }),

  reopenProject: (projectId: string): Promise<CloseView> =>
    httpClient<CloseView>(`${BASE}/${encodeURIComponent(projectId)}/reopen`, {
      method: "POST",
      body: JSON.stringify({})
    }),

  // 22. Templates
  listTemplates: (
    legalEntityId?: string,
    params?: { page?: number; pageSize?: number }
  ): Promise<PagedResult<ProjectTemplateView>> => {
    const q = buildQuery({
      legalEntityId,
      page: params?.page ?? 1,
      pageSize: params?.pageSize ?? 50
    });
    return httpClient<PagedResult<ProjectTemplateView>>(`${BASE}/templates${q}`);
  },

  createTemplate: (request: CreateProjectTemplateRequest): Promise<ProjectTemplateView> =>
    httpClient<ProjectTemplateView>(`${BASE}/templates`, {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // 23. Rate Cards
  createRateCard: (legalEntityId: string, request: CreateRateCardRequest): Promise<RateCardView> =>
    httpClient<RateCardView>(`${BASE}/legal-entities/${encodeURIComponent(legalEntityId)}/rate-cards`, {
      method: "POST",
      body: JSON.stringify(request)
    })
};

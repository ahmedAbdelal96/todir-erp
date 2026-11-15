import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { projectsApi } from "../services/projects-api.ts";
import type {
  CreateProjectRequest,
  CreateProjectFromSalesSourceRequest,
  UpdateProjectRequest,
  CreateProjectTaskRequest,
  CreateTaskDependencyRequest,
  CreateMilestoneRequest,
  MilestoneActionRequest,
  CreateProjectDeliverableRequest,
  DeliverableActionRequest,
  AddProjectTeamMemberRequest,
  CreateResourceRequirementRequest,
  AllocateProjectResourceRequest,
  CreateTimeEntryRequest,
  TimeActionRequest,
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
  CreateProjectBaselineRequest,
  CreateProjectDocumentRequest,
  CreateCloseExceptionRequest,
  CreateProjectTemplateRequest,
  CreateRateCardRequest,
  MilestoneActionKind,
  DeliverableActionKind,
  TimeActionKind
} from "../types.ts";

export const projectKeys = {
  all: ["projects"] as const,
  list: (legalEntityId: string, params?: { search?: string; state?: string; projectType?: string; page?: number; pageSize?: number }) =>
    ["projects", "list", legalEntityId, params] as const,
  detail: (projectId: string) => ["projects", "detail", projectId] as const,
  financialSummary: (projectId: string) => ["projects", "financial-summary", projectId] as const,
  tasks: (projectId: string, params?: { page?: number; pageSize?: number }) =>
    ["projects", "tasks", projectId, params] as const,
  milestones: (projectId: string, params?: { page?: number; pageSize?: number }) =>
    ["projects", "milestones", projectId, params] as const,
  deliverables: (projectId: string, params?: { page?: number; pageSize?: number }) =>
    ["projects", "deliverables", projectId, params] as const,
  team: (projectId: string, params?: { page?: number; pageSize?: number }) =>
    ["projects", "team", projectId, params] as const,
  resourceRequirements: (projectId: string, params?: { page?: number; pageSize?: number }) =>
    ["projects", "resource-requirements", projectId, params] as const,
  resourceAllocations: (projectId: string, params?: { page?: number; pageSize?: number }) =>
    ["projects", "resource-allocations", projectId, params] as const,
  time: (projectId: string, params?: { page?: number; pageSize?: number }) =>
    ["projects", "time", projectId, params] as const,
  baselines: (projectId: string, params?: { page?: number; pageSize?: number }) =>
    ["projects", "baselines", projectId, params] as const,
  documents: (projectId: string, params?: { page?: number; pageSize?: number }) =>
    ["projects", "documents", projectId, params] as const,
  timeline: (projectId: string, params?: { page?: number; pageSize?: number }) =>
    ["projects", "timeline", projectId, params] as const,
  close: (projectId: string) => ["projects", "close", projectId] as const,
  closeExceptions: (projectId: string, params?: { page?: number; pageSize?: number }) =>
    ["projects", "close-exceptions", projectId, params] as const,
  portfolio: (legalEntityId: string, params?: { page?: number; pageSize?: number }) =>
    ["projects", "portfolio", legalEntityId, params] as const,
  resourceLoad: (legalEntityId: string) => ["projects", "resource-load", legalEntityId] as const,
  templates: (legalEntityId?: string, params?: { page?: number; pageSize?: number }) =>
    ["projects", "templates", legalEntityId, params] as const
};

export function useProjectsList(
  legalEntityId: string | undefined,
  params?: { search?: string; state?: string; projectType?: string; page?: number; pageSize?: number }
) {
  return useQuery({
    queryKey: projectKeys.list(legalEntityId || "", params),
    queryFn: () => projectsApi.listProjects(legalEntityId!, params),
    enabled: Boolean(legalEntityId)
  });
}

export function useProjectDetail(projectId: string | undefined) {
  return useQuery({
    queryKey: projectKeys.detail(projectId || ""),
    queryFn: () => projectsApi.getProject(projectId!),
    enabled: Boolean(projectId)
  });
}

export function useFinancialSummary(projectId: string | undefined) {
  return useQuery({
    queryKey: projectKeys.financialSummary(projectId || ""),
    queryFn: () => projectsApi.getFinancialSummary(projectId!),
    enabled: Boolean(projectId)
  });
}

export function useProjectTasks(
  projectId: string | undefined,
  params?: { page?: number; pageSize?: number }
) {
  return useQuery({
    queryKey: projectKeys.tasks(projectId || "", params),
    queryFn: () => projectsApi.listTasks(projectId!, params),
    enabled: Boolean(projectId)
  });
}

export function useProjectMilestones(
  projectId: string | undefined,
  params?: { page?: number; pageSize?: number }
) {
  return useQuery({
    queryKey: projectKeys.milestones(projectId || "", params),
    queryFn: () => projectsApi.listMilestones(projectId!, params),
    enabled: Boolean(projectId)
  });
}

export function useProjectDeliverables(
  projectId: string | undefined,
  params?: { page?: number; pageSize?: number }
) {
  return useQuery({
    queryKey: projectKeys.deliverables(projectId || "", params),
    queryFn: () => projectsApi.listDeliverables(projectId!, params),
    enabled: Boolean(projectId)
  });
}

export function useProjectTeam(
  projectId: string | undefined,
  params?: { page?: number; pageSize?: number }
) {
  return useQuery({
    queryKey: projectKeys.team(projectId || "", params),
    queryFn: () => projectsApi.listTeam(projectId!, params),
    enabled: Boolean(projectId)
  });
}

export function useResourceRequirements(
  projectId: string | undefined,
  params?: { page?: number; pageSize?: number }
) {
  return useQuery({
    queryKey: projectKeys.resourceRequirements(projectId || "", params),
    queryFn: () => projectsApi.listResourceRequirements(projectId!, params),
    enabled: Boolean(projectId)
  });
}

export function useResourceAllocations(
  projectId: string | undefined,
  params?: { page?: number; pageSize?: number }
) {
  return useQuery({
    queryKey: projectKeys.resourceAllocations(projectId || "", params),
    queryFn: () => projectsApi.listResourceAllocations(projectId!, params),
    enabled: Boolean(projectId)
  });
}

export function useProjectTime(
  projectId: string | undefined,
  params?: { page?: number; pageSize?: number }
) {
  return useQuery({
    queryKey: projectKeys.time(projectId || "", params),
    queryFn: () => projectsApi.listTime(projectId!, params),
    enabled: Boolean(projectId)
  });
}

export function useProjectBaselines(
  projectId: string | undefined,
  params?: { page?: number; pageSize?: number }
) {
  return useQuery({
    queryKey: projectKeys.baselines(projectId || "", params),
    queryFn: () => projectsApi.listBaselines(projectId!, params),
    enabled: Boolean(projectId)
  });
}

export function useProjectDocuments(
  projectId: string | undefined,
  params?: { page?: number; pageSize?: number }
) {
  return useQuery({
    queryKey: projectKeys.documents(projectId || "", params),
    queryFn: () => projectsApi.listDocuments(projectId!, params),
    enabled: Boolean(projectId)
  });
}

export function useProjectTimeline(
  projectId: string | undefined,
  params?: { page?: number; pageSize?: number }
) {
  return useQuery({
    queryKey: projectKeys.timeline(projectId || "", params),
    queryFn: () => projectsApi.listTimeline(projectId!, params),
    enabled: Boolean(projectId)
  });
}

export function useCloseReadiness(projectId: string | undefined) {
  return useQuery({
    queryKey: projectKeys.close(projectId || ""),
    queryFn: () => projectsApi.getCloseReadiness(projectId!),
    enabled: Boolean(projectId)
  });
}

export function useCloseExceptions(
  projectId: string | undefined,
  params?: { page?: number; pageSize?: number }
) {
  return useQuery({
    queryKey: projectKeys.closeExceptions(projectId || "", params),
    queryFn: () => projectsApi.listCloseExceptions(projectId!, params),
    enabled: Boolean(projectId)
  });
}

export function useProjectPortfolio(
  legalEntityId: string | undefined,
  params?: { page?: number; pageSize?: number }
) {
  return useQuery({
    queryKey: projectKeys.portfolio(legalEntityId || "", params),
    queryFn: () => projectsApi.listPortfolio(legalEntityId!, params),
    enabled: Boolean(legalEntityId)
  });
}

export function useResourceLoad(legalEntityId: string | undefined) {
  return useQuery({
    queryKey: projectKeys.resourceLoad(legalEntityId || ""),
    queryFn: () => projectsApi.listResourceLoad(legalEntityId!),
    enabled: Boolean(legalEntityId)
  });
}

export function useProjectTemplates(
  legalEntityId?: string,
  params?: { page?: number; pageSize?: number }
) {
  return useQuery({
    queryKey: projectKeys.templates(legalEntityId, params),
    queryFn: () => projectsApi.listTemplates(legalEntityId, params)
  });
}

// ---------------- MUTATIONS ----------------

export function useCreateProject() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (req: CreateProjectRequest) => projectsApi.createProject(req),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: projectKeys.all });
    }
  });
}

export function useCreateProjectFromSalesSource() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (req: CreateProjectFromSalesSourceRequest) => projectsApi.createFromSalesSource(req),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: projectKeys.all });
    }
  });
}

export function useUpdateProject() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ projectId, request }: { projectId: string; request: UpdateProjectRequest }) =>
      projectsApi.updateProject(projectId, request),
    onSuccess: (_, vars) => {
      qc.invalidateQueries({ queryKey: projectKeys.detail(vars.projectId) });
      qc.invalidateQueries({ queryKey: projectKeys.all });
    }
  });
}

export function useTransitionProjectState() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ projectId, state, version }: { projectId: string; state: string; version: number }) =>
      projectsApi.transitionState(projectId, state, version),
    onSuccess: (_, vars) => {
      qc.invalidateQueries({ queryKey: projectKeys.detail(vars.projectId) });
      qc.invalidateQueries({ queryKey: projectKeys.all });
    }
  });
}

export function useAddTask() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ projectId, request }: { projectId: string; request: CreateProjectTaskRequest }) =>
      projectsApi.addTask(projectId, request),
    onSuccess: (_, vars) => {
      qc.invalidateQueries({ queryKey: projectKeys.tasks(vars.projectId) });
      qc.invalidateQueries({ queryKey: projectKeys.detail(vars.projectId) });
    }
  });
}

export function useAddDependency() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ projectId, request }: { projectId: string; request: CreateTaskDependencyRequest }) =>
      projectsApi.addDependency(projectId, request),
    onSuccess: (_, vars) => {
      qc.invalidateQueries({ queryKey: projectKeys.tasks(vars.projectId) });
    }
  });
}

export function useAddMilestone() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ projectId, request }: { projectId: string; request: CreateMilestoneRequest }) =>
      projectsApi.addMilestone(projectId, request),
    onSuccess: (_, vars) => {
      qc.invalidateQueries({ queryKey: projectKeys.milestones(vars.projectId) });
    }
  });
}

export function useExecuteMilestoneAction() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({
      milestoneId,
      action,
      request
    }: {
      projectId: string;
      milestoneId: string;
      action: MilestoneActionKind;
      request: MilestoneActionRequest;
    }) => projectsApi.milestoneAction(milestoneId, action, request),
    onSuccess: (_, vars) => {
      qc.invalidateQueries({ queryKey: projectKeys.milestones(vars.projectId) });
      qc.invalidateQueries({ queryKey: projectKeys.detail(vars.projectId) });
      qc.invalidateQueries({ queryKey: projectKeys.financialSummary(vars.projectId) });
    }
  });
}

export function useAddDeliverable() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ projectId, request }: { projectId: string; request: CreateProjectDeliverableRequest }) =>
      projectsApi.addDeliverable(projectId, request),
    onSuccess: (_, vars) => {
      qc.invalidateQueries({ queryKey: projectKeys.deliverables(vars.projectId) });
    }
  });
}

export function useExecuteDeliverableAction() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({
      deliverableId,
      action,
      request
    }: {
      projectId: string;
      deliverableId: string;
      action: DeliverableActionKind;
      request: DeliverableActionRequest;
    }) => projectsApi.deliverableAction(deliverableId, action, request),
    onSuccess: (_, vars) => {
      qc.invalidateQueries({ queryKey: projectKeys.deliverables(vars.projectId) });
    }
  });
}

export function useAddTeamMember() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ projectId, request }: { projectId: string; request: AddProjectTeamMemberRequest }) =>
      projectsApi.addTeamMember(projectId, request),
    onSuccess: (_, vars) => {
      qc.invalidateQueries({ queryKey: projectKeys.team(vars.projectId) });
      qc.invalidateQueries({ queryKey: projectKeys.detail(vars.projectId) });
    }
  });
}

export function useAddResourceRequirement() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ projectId, request }: { projectId: string; request: CreateResourceRequirementRequest }) =>
      projectsApi.addResourceRequirement(projectId, request),
    onSuccess: (_, vars) => {
      qc.invalidateQueries({ queryKey: projectKeys.resourceRequirements(vars.projectId) });
    }
  });
}

export function useAllocateResource() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ projectId, request }: { projectId: string; request: AllocateProjectResourceRequest }) =>
      projectsApi.allocateResource(projectId, request),
    onSuccess: (_, vars) => {
      qc.invalidateQueries({ queryKey: projectKeys.resourceAllocations(vars.projectId) });
      qc.invalidateQueries({ queryKey: projectKeys.resourceRequirements(vars.projectId) });
    }
  });
}

export function useCreateTimeEntry() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ projectId, request }: { projectId: string; request: CreateTimeEntryRequest }) =>
      projectsApi.createTime(projectId, request),
    onSuccess: (_, vars) => {
      qc.invalidateQueries({ queryKey: projectKeys.time(vars.projectId) });
    }
  });
}

export function useExecuteTimeAction() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({
      timeId,
      action,
      request
    }: {
      projectId: string;
      timeId: string;
      action: TimeActionKind;
      request: TimeActionRequest;
    }) => projectsApi.timeAction(timeId, action, request),
    onSuccess: (_, vars) => {
      qc.invalidateQueries({ queryKey: projectKeys.time(vars.projectId) });
      qc.invalidateQueries({ queryKey: projectKeys.detail(vars.projectId) });
      qc.invalidateQueries({ queryKey: projectKeys.financialSummary(vars.projectId) });
    }
  });
}

export function useCreateBudget() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ projectId, request }: { projectId: string; request: CreateBudgetRequest }) =>
      projectsApi.createBudget(projectId, request),
    onSuccess: (_, vars) => {
      qc.invalidateQueries({ queryKey: projectKeys.detail(vars.projectId) });
      qc.invalidateQueries({ queryKey: projectKeys.financialSummary(vars.projectId) });
    }
  });
}

export function useCreateBudgetRevision() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({
      projectId,
      budgetId,
      request
    }: {
      projectId: string;
      budgetId: string;
      request: CreateBudgetRevisionRequest;
    }) => projectsApi.createBudgetRevision(projectId, budgetId, request),
    onSuccess: (_, vars) => {
      qc.invalidateQueries({ queryKey: projectKeys.detail(vars.projectId) });
      qc.invalidateQueries({ queryKey: projectKeys.financialSummary(vars.projectId) });
    }
  });
}

export function useApproveBudgetVersion() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ budgetVersionId }: { projectId: string; budgetVersionId: string }) =>
      projectsApi.approveBudgetVersion(budgetVersionId),
    onSuccess: (_, vars) => {
      qc.invalidateQueries({ queryKey: projectKeys.detail(vars.projectId) });
      qc.invalidateQueries({ queryKey: projectKeys.financialSummary(vars.projectId) });
    }
  });
}

export function useCreateForecast() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ projectId, request }: { projectId: string; request: CreateForecastRequest }) =>
      projectsApi.createForecast(projectId, request),
    onSuccess: (_, vars) => {
      qc.invalidateQueries({ queryKey: projectKeys.detail(vars.projectId) });
      qc.invalidateQueries({ queryKey: projectKeys.financialSummary(vars.projectId) });
    }
  });
}

export function useRecordCostFact() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ projectId, request }: { projectId: string; request: RecordCostFactRequest }) =>
      projectsApi.recordCostFact(projectId, request),
    onSuccess: (_, vars) => {
      qc.invalidateQueries({ queryKey: projectKeys.detail(vars.projectId) });
      qc.invalidateQueries({ queryKey: projectKeys.financialSummary(vars.projectId) });
    }
  });
}

export function useRecordCommitment() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ projectId, request }: { projectId: string; request: RecordCommitmentRequest }) =>
      projectsApi.recordCommitment(projectId, request),
    onSuccess: (_, vars) => {
      qc.invalidateQueries({ queryKey: projectKeys.detail(vars.projectId) });
      qc.invalidateQueries({ queryKey: projectKeys.financialSummary(vars.projectId) });
    }
  });
}

export function useCreateBillingRule() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ projectId, request }: { projectId: string; request: CreateBillingRuleRequest }) =>
      projectsApi.createBillingRule(projectId, request),
    onSuccess: (_, vars) => {
      qc.invalidateQueries({ queryKey: projectKeys.detail(vars.projectId) });
      qc.invalidateQueries({ queryKey: projectKeys.financialSummary(vars.projectId) });
    }
  });
}

export function useAddBillingPlanLine() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ projectId, request }: { projectId: string; request: CreateBillingPlanLineRequest }) =>
      projectsApi.addBillingPlanLine(projectId, request),
    onSuccess: (_, vars) => {
      qc.invalidateQueries({ queryKey: projectKeys.detail(vars.projectId) });
    }
  });
}

export function useGenerateBillingCharges() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ projectId, billingRuleId }: { projectId: string; billingRuleId: string }) =>
      projectsApi.generateBillingCharges(projectId, billingRuleId),
    onSuccess: (_, vars) => {
      qc.invalidateQueries({ queryKey: projectKeys.detail(vars.projectId) });
      qc.invalidateQueries({ queryKey: projectKeys.financialSummary(vars.projectId) });
    }
  });
}

export function useCreateBillableCharge() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ projectId, request }: { projectId: string; request: CreateBillableChargeRequest }) =>
      projectsApi.createCharge(projectId, request),
    onSuccess: (_, vars) => {
      qc.invalidateQueries({ queryKey: projectKeys.detail(vars.projectId) });
      qc.invalidateQueries({ queryKey: projectKeys.financialSummary(vars.projectId) });
    }
  });
}

export function useRequestInvoice() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ projectId, request }: { projectId: string; request: RequestInvoiceRequest }) =>
      projectsApi.requestInvoice(projectId, request),
    onSuccess: (_, vars) => {
      qc.invalidateQueries({ queryKey: projectKeys.detail(vars.projectId) });
      qc.invalidateQueries({ queryKey: projectKeys.financialSummary(vars.projectId) });
    }
  });
}

export function useProjectChanges(projectId: string, params?: { page?: number; pageSize?: number }) {
  return useQuery({
    queryKey: [...projectKeys.detail(projectId), "changes", params],
    queryFn: () => projectsApi.listChanges(projectId, params),
    enabled: Boolean(projectId)
  });
}

export function useCreateChange() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ projectId, request }: { projectId: string; request: CreateChangeRequest }) =>
      projectsApi.createChange(projectId, request),
    onSuccess: (_, vars) => {
      qc.invalidateQueries({ queryKey: projectKeys.detail(vars.projectId) });
      qc.invalidateQueries({ queryKey: [...projectKeys.detail(vars.projectId), "changes"] });
    }
  });
}

export function useDecideChange() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({
      projectId,
      changeId,
      request
    }: {
      projectId: string;
      changeId: string;
      request: ChangeDecisionRequest;
    }) => projectsApi.decideChange(projectId, changeId, request),
    onSuccess: (_, vars) => {
      qc.invalidateQueries({ queryKey: projectKeys.detail(vars.projectId) });
      qc.invalidateQueries({ queryKey: projectKeys.financialSummary(vars.projectId) });
      qc.invalidateQueries({ queryKey: [...projectKeys.detail(vars.projectId), "changes"] });
    }
  });
}

export function useCreateCapitalHandoff() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ projectId, request }: { projectId: string; request: CapitalHandoffRequest }) =>
      projectsApi.createCapitalHandoff(projectId, request),
    onSuccess: (_, vars) => {
      qc.invalidateQueries({ queryKey: projectKeys.detail(vars.projectId) });
    }
  });
}

export function useCreateBaseline() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ projectId, request }: { projectId: string; request: CreateProjectBaselineRequest }) =>
      projectsApi.createBaseline(projectId, request),
    onSuccess: (_, vars) => {
      qc.invalidateQueries({ queryKey: projectKeys.baselines(vars.projectId) });
    }
  });
}

export function useAddDocument() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ projectId, request }: { projectId: string; request: CreateProjectDocumentRequest }) =>
      projectsApi.addDocument(projectId, request),
    onSuccess: (_, vars) => {
      qc.invalidateQueries({ queryKey: projectKeys.documents(vars.projectId) });
    }
  });
}

export function useAddCloseException() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ projectId, request }: { projectId: string; request: CreateCloseExceptionRequest }) =>
      projectsApi.addCloseException(projectId, request),
    onSuccess: (_, vars) => {
      qc.invalidateQueries({ queryKey: projectKeys.closeExceptions(vars.projectId) });
      qc.invalidateQueries({ queryKey: projectKeys.close(vars.projectId) });
    }
  });
}

export function useResolveCloseException() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ exceptionId }: { projectId: string; exceptionId: string }) =>
      projectsApi.resolveCloseException(exceptionId),
    onSuccess: (_, vars) => {
      qc.invalidateQueries({ queryKey: projectKeys.closeExceptions(vars.projectId) });
      qc.invalidateQueries({ queryKey: projectKeys.close(vars.projectId) });
    }
  });
}

export function useCloseProject() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ projectId }: { projectId: string }) => projectsApi.closeProject(projectId),
    onSuccess: (_, vars) => {
      qc.invalidateQueries({ queryKey: projectKeys.detail(vars.projectId) });
      qc.invalidateQueries({ queryKey: projectKeys.close(vars.projectId) });
      qc.invalidateQueries({ queryKey: projectKeys.all });
    }
  });
}

export function useReopenProject() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ projectId }: { projectId: string }) => projectsApi.reopenProject(projectId),
    onSuccess: (_, vars) => {
      qc.invalidateQueries({ queryKey: projectKeys.detail(vars.projectId) });
      qc.invalidateQueries({ queryKey: projectKeys.close(vars.projectId) });
      qc.invalidateQueries({ queryKey: projectKeys.all });
    }
  });
}

export function useCreateProjectTemplate() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (req: CreateProjectTemplateRequest) => projectsApi.createTemplate(req),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["projects", "templates"] });
    }
  });
}

export function useCreateRateCard() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ legalEntityId, request }: { legalEntityId: string; request: CreateRateCardRequest }) =>
      projectsApi.createRateCard(legalEntityId, request),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: projectKeys.all });
    }
  });
}

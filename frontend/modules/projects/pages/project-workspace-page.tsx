import React, { useState, useEffect } from "react";
import { useTranslation } from "react-i18next";
import { useParams, useNavigate, useSearchParams, useOutletContext } from "react-router-dom";
import type { WorkspaceOutletContext } from "../../../app/router/route-contract.ts";
import { useOptionalLegalEntityContext, useOptionalPermissions } from "../../../core/runtime/runtime-provider.tsx";
import { useIsMobile } from "../hooks/use-is-mobile.ts";
import { Surface } from "../../../components/pages/surface.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { ProjectsHeader } from "../components/projects-header.tsx";
import { ProjectMoney } from "../components/project-money.tsx";
import { ProjectTypeBadge } from "../components/project-type-badge.tsx";
import { ProjectStatusBadge } from "../components/project-status-badge.tsx";
import { ProjectHealthBadge } from "../components/project-health-badge.tsx";
import {
  useProjectDetail,
  useFinancialSummary,
  useProjectTasks,
  useProjectMilestones,
  useProjectDeliverables,
  useProjectTeam,
  useResourceRequirements,
  useResourceAllocations,
  useProjectTime,
  useProjectBaselines,
  useProjectDocuments,
  useProjectTimeline,
  useCloseReadiness,
  useCloseExceptions,
  useProjectChanges,
  useTransitionProjectState,
  useExecuteMilestoneAction,
  useExecuteDeliverableAction,
  useExecuteTimeAction,
  useGenerateBillingCharges,
  useResolveCloseException,
  useCloseProject,
  useReopenProject
} from "../hooks/use-projects-queries.ts";
import { TasksMilestonesTab } from "../features/tasks/tasks-milestones-tab.tsx";
import { TeamResourcesTab } from "../features/team/team-resources-tab.tsx";
import { TimesheetsTab } from "../features/time/timesheets-tab.tsx";
import { useEmployeeDetail } from "../../hr/public.ts";
import { FinancialsSummaryTab } from "../features/financials/financials-summary-tab.tsx";
import { BillingInvoicingTab } from "../features/billing/billing-invoicing-tab.tsx";
import { ChangeOrdersTab } from "../features/changes/change-orders-tab.tsx";
import { CapitalHandoffTab } from "../features/capital/capital-handoff-tab.tsx";
import { DocumentsTimelineTab } from "../features/documents/documents-timeline-tab.tsx";
import { ProjectClosingTab } from "../features/closing/project-closing-tab.tsx";
import { EditProjectModal } from "../features/modals/project-modals.tsx";
import {
  NewTaskModal,
  NewMilestoneModal,
  NewDeliverableModal,
  NewTeamMemberModal,
  NewResourceReqModal,
  AllocateResourceModal
} from "../features/modals/work-team-modals.tsx";
import {
  LogTimeModal,
  NewBudgetModal,
  NewBudgetRevisionModal,
  NewForecastModal,
  RecordCostFactModal,
  RecordCommitmentModal,
  NewBillingRuleModal,
  NewBillingPlanLineModal,
  CreateChargeModal,
  RequestInvoiceModal,
  NewChangeRequestModal,
  DecideChangeModal,
  CapitalHandoffModal,
  CreateBaselineModal,
  AddDocumentModal,
  AddCloseExceptionModal
} from "../features/modals/financial-billing-modals.tsx";
import type {
  ChangeRequestView,
  MilestoneActionKind,
  DeliverableActionKind,
  TimeActionKind
} from "../types.ts";
import {
  TasksKanbanIcon,
  TeamUsersIcon,
  ClockTimeIcon,
  CalculatorBudgetIcon,
  ReceiptBillingIcon,
  ChangeOrderIcon,
  CapitalAssetIcon,
  DocumentFolderIcon,
  CloseShieldIcon
} from "../components/projects-icons.tsx";

export type WorkspaceTabId =
  | "financials"
  | "tasks"
  | "team"
  | "time"
  | "billing"
  | "changes"
  | "capital"
  | "documents"
  | "closing";

export interface ProjectWorkspacePageProps {
  readonly projectId?: string;
  readonly onBack?: () => void;
}

export function ProjectWorkspacePage({
  projectId: propProjectId,
  onBack
}: ProjectWorkspacePageProps): React.JSX.Element {
  const { t } = useTranslation("projects");
  const params = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const isMobile = useIsMobile();

  const projectId = propProjectId || params.id || "";
  const activeTab = (searchParams.get("tab") as WorkspaceTabId | null) ?? "financials";

  const handleTabChange = (tab: WorkspaceTabId) => {
    setSearchParams((prev) => {
      const next = new URLSearchParams(prev);
      next.set("tab", tab);
      return next;
    });
  };

  const outletCtx = useOutletContext<WorkspaceOutletContext | undefined>();
  const runtimeContext = useOptionalLegalEntityContext();
  const permissions = outletCtx?.permissions ?? useOptionalPermissions();

  const isPermissionsLoaded = Boolean(permissions && permissions.toArray().length > 0);
  const canRead = !isPermissionsLoaded || Boolean(
    permissions?.hasPermission("projects.read") ||
      permissions?.hasPermission("admin") ||
      permissions?.hasPermission("*")
  );

  // Queries
  const projectQuery = useProjectDetail(projectId);
  const summaryQuery = useFinancialSummary(projectId);
  const tasksQuery = useProjectTasks(projectId);
  const milestonesQuery = useProjectMilestones(projectId);
  const deliverablesQuery = useProjectDeliverables(projectId);
  const teamQuery = useProjectTeam(projectId);
  const reqsQuery = useResourceRequirements(projectId);
  const allocationsQuery = useResourceAllocations(projectId);
  const timeQuery = useProjectTime(projectId);
  const baselinesQuery = useProjectBaselines(projectId);
  const docsQuery = useProjectDocuments(projectId);
  const timelineQuery = useProjectTimeline(projectId);
  const closeQuery = useCloseReadiness(projectId);
  const closeExceptionsQuery = useCloseExceptions(projectId);
  const changesQuery = useProjectChanges(projectId);

  const managerQuery = useEmployeeDetail(
    projectQuery.data?.projectManagerEmployeeId ?? "",
    projectQuery.data?.legalEntityId
  );
  const managerName =
    managerQuery.data?.arabicName ||
    managerQuery.data?.preferredName ||
    managerQuery.data?.englishName ||
    (projectQuery.data?.projectManagerEmployeeId ? t("kpis.managerAssigned") : null);

  // Lifecycle Mutations
  const transitionStateMutation = useTransitionProjectState();
  const milestoneActionMutation = useExecuteMilestoneAction();
  const deliverableActionMutation = useExecuteDeliverableAction();
  const timeActionMutation = useExecuteTimeAction();
  const generateChargesMutation = useGenerateBillingCharges();
  const resolveExceptionMutation = useResolveCloseException();
  const closeProjectMutation = useCloseProject();
  const reopenProjectMutation = useReopenProject();

  // Modals state
  const [isEditOpen, setIsEditOpen] = useState(false);
  const [isNewTaskOpen, setIsNewTaskOpen] = useState(false);
  const [isNewMilestoneOpen, setIsNewMilestoneOpen] = useState(false);
  const [isNewDeliverableOpen, setIsNewDeliverableOpen] = useState(false);
  const [isNewTeamOpen, setIsNewTeamOpen] = useState(false);
  const [isNewReqOpen, setIsNewReqOpen] = useState(false);
  const [isAllocateOpen, setIsAllocateOpen] = useState(false);
  const [isLogTimeOpen, setIsLogTimeOpen] = useState(false);
  const [isNewBudgetOpen, setIsNewBudgetOpen] = useState(false);
  const [isNewBudgetRevOpen, setIsNewBudgetRevOpen] = useState(false);
  const [isNewForecastOpen, setIsNewForecastOpen] = useState(false);
  const [isCostFactOpen, setIsCostFactOpen] = useState(false);
  const [isCommitmentOpen, setIsCommitmentOpen] = useState(false);
  const [isNewRuleOpen, setIsNewRuleOpen] = useState(false);
  const [isNewPlanLineOpen, setIsNewPlanLineOpen] = useState(false);
  const [isCreateChargeOpen, setIsCreateChargeOpen] = useState(false);
  const [isRequestInvoiceOpen, setIsRequestInvoiceOpen] = useState(false);
  const [isNewChangeOpen, setIsNewChangeOpen] = useState(false);
  const [selectedChange, setSelectedChange] = useState<ChangeRequestView | null>(null);
  const [isCapitalOpen, setIsCapitalOpen] = useState(false);
  const [isBaselineOpen, setIsBaselineOpen] = useState(false);
  const [isAddDocOpen, setIsAddDocOpen] = useState(false);
  const [isAddExceptionOpen, setIsAddExceptionOpen] = useState(false);

  const project = projectQuery.data;

  useEffect(() => {
    if (activeTab === "capital" && project && project.projectType !== "CapitalProject") {
      handleTabChange("financials");
    }
  }, [activeTab, project?.projectType]);

  // Fail closed if permissions exist, are loaded, and user cannot read
  if (isPermissionsLoaded && !canRead) {
    return (
      <Surface variant="card" padding="normal" style={{ textAlign: "center", margin: "2rem" }}>
        <h2 style={{ color: "var(--color-danger, #ef4444)" }}>{t("messages.unauthorized")}</h2>
      </Surface>
    );
  }

  if (projectQuery.isLoading) {
    return (
      <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted, #64748b)" }}>
        {t("actions.refresh")}...
      </div>
    );
  }

  if (!project) {
    return (
      <Surface variant="card" padding="normal" style={{ textAlign: "center", margin: "2rem" }}>
        <h2 style={{ color: "var(--color-content-primary, #0f172a)" }}>{t("messages.noProjectsFound")}</h2>
        <Button variant="secondary" onClick={() => (onBack ? onBack() : navigate("/projects"))}>
          {t("actions.backToList")}
        </Button>
      </Surface>
    );
  }

  const allTabs: readonly { id: WorkspaceTabId; label: string; icon: React.ReactNode }[] = [
    { id: "financials", label: t("tabs.financials"), icon: <CalculatorBudgetIcon size={16} /> },
    { id: "tasks", label: t("tabs.tasks"), icon: <TasksKanbanIcon size={16} /> },
    { id: "team", label: t("tabs.team"), icon: <TeamUsersIcon size={16} /> },
    { id: "time", label: t("tabs.time"), icon: <ClockTimeIcon size={16} /> },
    { id: "billing", label: t("tabs.billing"), icon: <ReceiptBillingIcon size={16} /> },
    { id: "changes", label: t("tabs.changes"), icon: <ChangeOrderIcon size={16} /> },
    { id: "capital", label: t("tabs.capital"), icon: <CapitalAssetIcon size={16} /> },
    { id: "documents", label: t("tabs.documents"), icon: <DocumentFolderIcon size={16} /> },
    { id: "closing", label: t("tabs.closing"), icon: <CloseShieldIcon size={16} /> }
  ];

  // Restrict capital tab strictly to CapitalProject
  const tabs = allTabs.filter(
    (tab) => tab.id !== "capital" || project.projectType === "CapitalProject"
  );

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem", padding: isMobile ? "0.75rem" : "1.5rem" }}>
      {/* 360 Header */}
      <ProjectsHeader
        project={project}
        onBack={() => (onBack ? onBack() : navigate("/projects"))}
        onEdit={() => setIsEditOpen(true)}
        onStart={() =>
          transitionStateMutation.mutate({ projectId, state: "Active", version: project.version })
        }
        onHold={() =>
          transitionStateMutation.mutate({ projectId, state: "OnHold", version: project.version })
        }
        onResume={() =>
          transitionStateMutation.mutate({ projectId, state: "Active", version: project.version })
        }
        onCompleteExecution={() =>
          transitionStateMutation.mutate({ projectId, state: "ExecutionCompleted", version: project.version })
        }
        onCloseProject={() => closeProjectMutation.mutate({ projectId })}
        onReopenProject={() => reopenProjectMutation.mutate({ projectId })}
        isActionPending={
          transitionStateMutation.isPending ||
          closeProjectMutation.isPending ||
          reopenProjectMutation.isPending
        }
      />

      {/* Compact Executive Overview & Context Strip */}
      <div data-testid="project-overview-strip">
        <Surface
          variant="card"
          padding="none"
          style={{
            background: "var(--color-surface-card, #ffffff)",
            border: "1px solid var(--color-border-default, #cbd5e1)",
            borderRadius: "var(--radius-lg, 12px)",
            boxShadow: "0 1px 3px rgba(0,0,0,0.05)"
          }}
        >
          <div
            style={{
              padding: "1rem 1.25rem",
              display: "grid",
              gridTemplateColumns: isMobile ? "1fr" : "repeat(5, 1fr)",
              gap: "1.25rem",
              alignItems: "center"
            }}
          >
            <div>
              <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted, #64748b)", fontWeight: 600, display: "block", marginBottom: "0.25rem" }}>
                {t("kpis.projectTypeAndModel")}
              </span>
              <div style={{ display: "flex", alignItems: "center", gap: "0.5rem", flexWrap: "wrap" }}>
                <ProjectTypeBadge type={project.projectType} size="small" />
                <span style={{ fontSize: "0.8125rem", color: "var(--color-content-secondary, #475569)", fontWeight: 500 }}>
                  {t(`billingModels.${project.billingModel}` as const)}
                </span>
              </div>
            </div>

            <div>
              <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted, #64748b)", fontWeight: 600, display: "block", marginBottom: "0.25rem" }}>
                {t("fields.executionHealth")}
              </span>
              <div style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
                <ProjectHealthBadge health={project.executionHealth} size="small" />
                <ProjectStatusBadge state={project.lifecycleState} size="small" />
              </div>
            </div>

            <div>
              <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted, #64748b)", fontWeight: 600, display: "block", marginBottom: "0.25rem" }}>
                {t("kpis.timelineSchedule")}
              </span>
              <span style={{ fontSize: "0.875rem", fontWeight: 600, color: "var(--color-content-primary, #0f172a)", fontFamily: "var(--font-mono, monospace)" }}>
                {project.startDate}{project.targetEndDate ? ` — ${project.targetEndDate}` : ""}
              </span>
            </div>

            <div>
              <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted, #64748b)", fontWeight: 600, display: "block", marginBottom: "0.25rem" }}>
                {t("fields.manager")}
              </span>
              <span style={{ fontSize: "0.875rem", fontWeight: 600, color: "var(--color-content-secondary, #475569)" }}>
                {managerName || "—"}
              </span>
            </div>

            <div>
              <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted, #64748b)", fontWeight: 600, display: "block", marginBottom: "0.25rem" }}>
                {t("kpis.totalBudget")}
              </span>
              <span style={{ fontSize: "1rem", fontWeight: 700, color: "var(--color-content-primary, #0f172a)" }}>
                <ProjectMoney amount={summaryQuery.data?.budget ?? project.budget} currency={project.currencyCode || undefined} />
              </span>
            </div>
          </div>
        </Surface>
      </div>

      {/* Tabs Bar */}
      <Surface
        variant="card"
        padding="none"
        style={{
          display: "flex",
          gap: "0.25rem",
          padding: "0.375rem",
          background: "var(--color-surface-card, #ffffff)",
          border: "1px solid var(--color-border-default, #cbd5e1)",
          borderRadius: "var(--radius-lg, 12px)",
          overflowX: "auto"
        }}
      >
        {tabs.map((tab) => {
          const isActive = activeTab === tab.id;
          return (
            <button
              key={tab.id}
              type="button"
              onClick={() => handleTabChange(tab.id)}
              data-testid={`tab-${tab.id}`}
              style={{
                display: "inline-flex",
                alignItems: "center",
                gap: "0.5rem",
                padding: "0.625rem 1rem",
                borderRadius: "var(--radius-md, 8px)",
                border: "none",
                background: isActive ? "var(--color-brand-primary, #102a43)" : "transparent",
                color: isActive ? "#ffffff" : "var(--color-content-secondary, #475569)",
                fontWeight: isActive ? 600 : 500,
                fontSize: "0.875rem",
                cursor: "pointer",
                whiteSpace: "nowrap",
                transition: "all 0.15s ease"
              }}
            >
              {tab.icon}
              {tab.label}
            </button>
          );
        })}
      </Surface>

      {/* Tab Panels */}
      {activeTab === "financials" && (
        <FinancialsSummaryTab
          project={project}
          summary={summaryQuery.data}
          isLoading={summaryQuery.isLoading}
          onCreateBudget={() => setIsNewBudgetOpen(true)}
          onReviseBudget={() => setIsNewBudgetRevOpen(true)}
          onCreateForecast={() => setIsNewForecastOpen(true)}
          onRecordCostFact={() => setIsCostFactOpen(true)}
          onRecordCommitment={() => setIsCommitmentOpen(true)}
        />
      )}

      {activeTab === "tasks" && (
        <TasksMilestonesTab
          tasks={tasksQuery.data?.items ?? []}
          milestones={milestonesQuery.data?.items ?? []}
          deliverables={deliverablesQuery.data?.items ?? []}
          isLoading={tasksQuery.isLoading || milestonesQuery.isLoading}
          onAddTask={() => setIsNewTaskOpen(true)}
          onAddMilestone={() => setIsNewMilestoneOpen(true)}
          onAddDeliverable={() => setIsNewDeliverableOpen(true)}
          onMilestoneAction={(milestoneId, action) =>
            milestoneActionMutation.mutate({
              projectId,
              milestoneId,
              action,
              request: {}
            })
          }
          onDeliverableAction={(deliverableId, action) =>
            deliverableActionMutation.mutate({
              projectId,
              deliverableId,
              action,
              request: {}
            })
          }
        />
      )}

      {activeTab === "team" && (
        <TeamResourcesTab
          team={teamQuery.data?.items ?? []}
          requirements={reqsQuery.data?.items ?? []}
          allocations={allocationsQuery.data?.items ?? []}
          isLoading={teamQuery.isLoading || reqsQuery.isLoading}
          onAddTeamMember={() => setIsNewTeamOpen(true)}
          onAddRequirement={() => setIsNewReqOpen(true)}
          onAllocateResource={() => setIsAllocateOpen(true)}
        />
      )}

      {activeTab === "time" && (
        <TimesheetsTab
          timeEntries={timeQuery.data?.items ?? []}
          isLoading={timeQuery.isLoading}
          currencyCode={project.currencyCode}
          onLogTime={() => setIsLogTimeOpen(true)}
          onTimeAction={(timeId, action, version) =>
            timeActionMutation.mutate({
              projectId,
              timeId,
              action,
              request: { version }
            })
          }
        />
      )}

      {activeTab === "billing" && (
        <BillingInvoicingTab
          project={project}
          isLoading={false}
          onNewBillingRule={() => setIsNewRuleOpen(true)}
          onAddPlanLine={() => setIsNewPlanLineOpen(true)}
          onGenerateCharges={() =>
            generateChargesMutation.mutate({
              projectId,
              billingRuleId: "default"
            })
          }
          onCreateCharge={() => setIsCreateChargeOpen(true)}
          onRequestInvoice={() => setIsRequestInvoiceOpen(true)}
          isGeneratingCharges={generateChargesMutation.isPending}
        />
      )}

      {activeTab === "changes" && (
        <ChangeOrdersTab
          changeRequests={changesQuery.data?.items ?? []}
          isLoading={changesQuery.isLoading}
          onNewChangeRequest={() => setIsNewChangeOpen(true)}
          onCreateBaseline={() => setIsBaselineOpen(true)}
          onDecideChange={(ch) => setSelectedChange(ch)}
        />
      )}

      {activeTab === "capital" && (
        <CapitalHandoffTab
          project={project}
          isLoading={false}
          onCapitalHandoff={() => setIsCapitalOpen(true)}
        />
      )}

      {activeTab === "documents" && (
        <DocumentsTimelineTab
          documents={docsQuery.data?.items ?? []}
          timeline={timelineQuery.data?.items ?? []}
          isLoading={docsQuery.isLoading || timelineQuery.isLoading}
          onAddDocument={() => setIsAddDocOpen(true)}
        />
      )}

      {activeTab === "closing" && (
        <ProjectClosingTab
          project={project}
          closeReadiness={closeQuery.data}
          closeExceptions={closeExceptionsQuery.data?.items ?? []}
          isLoading={closeQuery.isLoading || closeExceptionsQuery.isLoading}
          onAddException={() => setIsAddExceptionOpen(true)}
          onResolveException={(exId) =>
            resolveExceptionMutation.mutate({ projectId, exceptionId: exId })
          }
          onCloseProject={() => closeProjectMutation.mutate({ projectId })}
          onReopenProject={() => reopenProjectMutation.mutate({ projectId })}
        />
      )}

      {/* Modals & Dialogs */}
      <EditProjectModal
        isOpen={isEditOpen}
        project={project}
        onClose={() => setIsEditOpen(false)}
      />

      <NewTaskModal
        isOpen={isNewTaskOpen}
        projectId={projectId}
        onClose={() => setIsNewTaskOpen(false)}
      />

      <NewMilestoneModal
        isOpen={isNewMilestoneOpen}
        projectId={projectId}
        onClose={() => setIsNewMilestoneOpen(false)}
      />

      <NewDeliverableModal
        isOpen={isNewDeliverableOpen}
        projectId={projectId}
        onClose={() => setIsNewDeliverableOpen(false)}
      />

      <NewTeamMemberModal
        isOpen={isNewTeamOpen}
        projectId={projectId}
        onClose={() => setIsNewTeamOpen(false)}
      />

      <NewResourceReqModal
        isOpen={isNewReqOpen}
        projectId={projectId}
        onClose={() => setIsNewReqOpen(false)}
      />

      <AllocateResourceModal
        isOpen={isAllocateOpen}
        projectId={projectId}
        onClose={() => setIsAllocateOpen(false)}
      />

      <LogTimeModal
        isOpen={isLogTimeOpen}
        projectId={projectId}
        onClose={() => setIsLogTimeOpen(false)}
      />

      <NewBudgetModal
        isOpen={isNewBudgetOpen}
        projectId={projectId}
        currencyCode={project.currencyCode || undefined}
        onClose={() => setIsNewBudgetOpen(false)}
      />

      <NewBudgetRevisionModal
        isOpen={isNewBudgetRevOpen}
        projectId={projectId}
        budgetId={projectId}
        currentAmount={project.budget}
        onClose={() => setIsNewBudgetRevOpen(false)}
      />

      <NewForecastModal
        isOpen={isNewForecastOpen}
        projectId={projectId}
        onClose={() => setIsNewForecastOpen(false)}
      />

      <RecordCostFactModal
        isOpen={isCostFactOpen}
        projectId={projectId}
        currencyCode={project.currencyCode || undefined}
        onClose={() => setIsCostFactOpen(false)}
      />

      <RecordCommitmentModal
        isOpen={isCommitmentOpen}
        projectId={projectId}
        currencyCode={project.currencyCode || undefined}
        onClose={() => setIsCommitmentOpen(false)}
      />

      <NewBillingRuleModal
        isOpen={isNewRuleOpen}
        projectId={projectId}
        onClose={() => setIsNewRuleOpen(false)}
      />

      <NewBillingPlanLineModal
        isOpen={isNewPlanLineOpen}
        projectId={projectId}
        onClose={() => setIsNewPlanLineOpen(false)}
      />

      <CreateChargeModal
        isOpen={isCreateChargeOpen}
        projectId={projectId}
        currencyCode={project.currencyCode || undefined}
        onClose={() => setIsCreateChargeOpen(false)}
      />

      <RequestInvoiceModal
        isOpen={isRequestInvoiceOpen}
        projectId={projectId}
        eligibleAmount={project.billingEligible}
        currencyCode={project.currencyCode || undefined}
        onClose={() => setIsRequestInvoiceOpen(false)}
      />

      <NewChangeRequestModal
        isOpen={isNewChangeOpen}
        projectId={projectId}
        onClose={() => setIsNewChangeOpen(false)}
      />

      <DecideChangeModal
        isOpen={Boolean(selectedChange)}
        projectId={projectId}
        change={selectedChange}
        onClose={() => setSelectedChange(null)}
      />

      <CapitalHandoffModal
        isOpen={isCapitalOpen}
        projectId={projectId}
        currencyCode={project.currencyCode || undefined}
        onClose={() => setIsCapitalOpen(false)}
      />

      <CreateBaselineModal
        isOpen={isBaselineOpen}
        projectId={projectId}
        onClose={() => setIsBaselineOpen(false)}
      />

      <AddDocumentModal
        isOpen={isAddDocOpen}
        projectId={projectId}
        onClose={() => setIsAddDocOpen(false)}
      />

      <AddCloseExceptionModal
        isOpen={isAddExceptionOpen}
        projectId={projectId}
        onClose={() => setIsAddExceptionOpen(false)}
      />
    </div>
  );
}

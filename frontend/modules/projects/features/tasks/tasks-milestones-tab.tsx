import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { StatusBadge, type StatusIntent } from "../../../../components/fields/status-badge.tsx";
import {
  TasksKanbanIcon,
  MilestoneFlagIcon,
  PlusCircleIcon,
  CheckCircleIcon
} from "../../components/projects-icons.tsx";
import type {
  ProjectTaskView,
  MilestoneView,
  ProjectDeliverableView,
  MilestoneActionKind,
  DeliverableActionKind
} from "../../types.ts";

export interface TasksMilestonesTabProps {
  readonly tasks: readonly ProjectTaskView[];
  readonly milestones: readonly MilestoneView[];
  readonly deliverables: readonly ProjectDeliverableView[];
  readonly isLoading: boolean;
  readonly onAddTask: () => void;
  readonly onAddMilestone: () => void;
  readonly onAddDeliverable: () => void;
  readonly onMilestoneAction: (milestoneId: string, action: MilestoneActionKind) => void;
  readonly onDeliverableAction: (deliverableId: string, action: DeliverableActionKind) => void;
}

export function TasksMilestonesTab({
  tasks,
  milestones,
  deliverables,
  isLoading,
  onAddTask,
  onAddMilestone,
  onAddDeliverable,
  onMilestoneAction,
  onDeliverableAction
}: TasksMilestonesTabProps): React.JSX.Element {
  const { t } = useTranslation("projects");
  const [subTab, setSubTab] = useState<"tasks" | "milestones" | "deliverables">("tasks");

  function taskStatusIntent(status: string): StatusIntent {
    switch (status) {
      case "Completed":
        return "success";
      case "InProgress":
        return "info";
      case "Blocked":
        return "danger";
      case "Cancelled":
        return "neutral";
      default:
        return "neutral";
    }
  }

  function priorityIntent(priority: string): StatusIntent {
    switch (priority) {
      case "Urgent":
        return "danger";
      case "High":
        return "warning";
      case "Normal":
        return "neutral";
      default:
        return "neutral";
    }
  }

  function milestoneStatusIntent(status: string): StatusIntent {
    switch (status) {
      case "Accepted":
      case "Reached":
        return "success";
      case "Rejected":
        return "danger";
      case "Open":
      case "Planned":
        return "info";
      default:
        return "neutral";
    }
  }

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
      {/* Sub-tab Navigation Bar */}
      <Surface
        variant="card"
        style={{
          padding: "0.5rem 1rem",
          background: "var(--color-surface-card, #ffffff)",
          border: "1px solid var(--color-border-default, #cbd5e1)",
          borderRadius: "var(--radius-lg, 12px)",
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          flexWrap: "wrap",
          gap: "0.75rem"
        }}
      >
        <div style={{ display: "flex", gap: "0.5rem" }}>
          <button
            type="button"
            data-testid="subtab-tasks"
            onClick={() => setSubTab("tasks")}
            style={{
              padding: "0.5rem 1rem",
              borderRadius: "var(--radius-md, 8px)",
              border: "none",
              background: subTab === "tasks" ? "var(--color-brand-primary, #102a43)" : "transparent",
              color: subTab === "tasks" ? "#ffffff" : "var(--color-content-muted, #64748b)",
              fontWeight: 600,
              fontSize: "0.875rem",
              cursor: "pointer",
              display: "flex",
              alignItems: "center",
              gap: "0.5rem"
            }}
          >
            <TasksKanbanIcon size={16} />
            {t("tables.task")} ({tasks.length})
          </button>

          <button
            type="button"
            data-testid="subtab-milestones"
            onClick={() => setSubTab("milestones")}
            style={{
              padding: "0.5rem 1rem",
              borderRadius: "var(--radius-md, 8px)",
              border: "none",
              background: subTab === "milestones" ? "var(--color-brand-primary, #102a43)" : "transparent",
              color: subTab === "milestones" ? "#ffffff" : "var(--color-content-muted, #64748b)",
              fontWeight: 600,
              fontSize: "0.875rem",
              cursor: "pointer",
              display: "flex",
              alignItems: "center",
              gap: "0.5rem"
            }}
          >
            <MilestoneFlagIcon size={16} />
            {t("tables.milestone")} ({milestones.length})
          </button>

          <button
            type="button"
            data-testid="subtab-deliverables"
            onClick={() => setSubTab("deliverables")}
            style={{
              padding: "0.5rem 1rem",
              borderRadius: "var(--radius-md, 8px)",
              border: "none",
              background: subTab === "deliverables" ? "var(--color-brand-primary, #102a43)" : "transparent",
              color: subTab === "deliverables" ? "#ffffff" : "var(--color-content-muted, #64748b)",
              fontWeight: 600,
              fontSize: "0.875rem",
              cursor: "pointer",
              display: "flex",
              alignItems: "center",
              gap: "0.5rem"
            }}
          >
            <CheckCircleIcon size={16} />
            {t("actions.addDeliverable")} ({deliverables.length})
          </button>
        </div>

        <div>
          {subTab === "tasks" && (
            <Button variant="primary" size="compact" onClick={onAddTask} data-testid="btn-add-task">
              <PlusCircleIcon size={16} style={{ marginInlineEnd: "0.25rem" }} />
              {t("actions.addTask")}
            </Button>
          )}
          {subTab === "milestones" && (
            <Button variant="primary" size="compact" onClick={onAddMilestone} data-testid="btn-add-milestone">
              <PlusCircleIcon size={16} style={{ marginInlineEnd: "0.25rem" }} />
              {t("actions.addMilestone")}
            </Button>
          )}
          {subTab === "deliverables" && (
            <Button variant="primary" size="compact" onClick={onAddDeliverable} data-testid="btn-add-deliverable">
              <PlusCircleIcon size={16} style={{ marginInlineEnd: "0.25rem" }} />
              {t("actions.addDeliverable")}
            </Button>
          )}
        </div>
      </Surface>

      {/* Content Section */}
      <Surface
        variant="card"
        style={{
          background: "var(--color-surface-card, #ffffff)",
          border: "1px solid var(--color-border-default, #cbd5e1)",
          borderRadius: "var(--radius-lg, 12px)",
          overflow: "hidden"
        }}
      >
        {isLoading ? (
          <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted, #64748b)" }}>
            {t("actions.refresh")}...
          </div>
        ) : subTab === "tasks" ? (
          /* Tasks View */
          tasks.length === 0 ? (
            <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted, #64748b)" }}>
              {t("messages.noTasks")}
            </div>
          ) : (
            <div style={{ overflowX: "auto" }}>
              <table style={{ width: "100%", borderCollapse: "collapse", textAlign: "start", fontSize: "0.875rem" }}>
                <thead>
                  <tr style={{ background: "var(--color-surface-subtle, #f8fafc)", borderBottom: "1px solid var(--color-border-default, #cbd5e1)" }}>
                    <th style={{ padding: "0.75rem 1rem", fontWeight: 600, textAlign: "start" }}>#</th>
                    <th style={{ padding: "0.75rem 1rem", fontWeight: 600, textAlign: "start" }}>{t("fields.title")}</th>
                    <th style={{ padding: "0.75rem 1rem", fontWeight: 600, textAlign: "start" }}>{t("fields.status")}</th>
                    <th style={{ padding: "0.75rem 1rem", fontWeight: 600, textAlign: "start" }}>{t("fields.priority")}</th>
                    <th style={{ padding: "0.75rem 1rem", fontWeight: 600, textAlign: "start" }}>{t("tables.datesPlanned")}</th>
                    <th style={{ padding: "0.75rem 1rem", fontWeight: 600, textAlign: "start" }}>{t("fields.progressPercent")}</th>
                  </tr>
                </thead>
                <tbody>
                  {tasks.map((task, idx) => (
                    <tr key={task.id} style={{ borderBottom: "1px solid var(--color-border-subtle, #e2e8f0)" }}>
                      <td style={{ padding: "0.75rem 1rem", color: "var(--color-content-muted, #64748b)" }}>{idx + 1}</td>
                      <td style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-primary, #0f172a)" }}>
                        {task.title}
                        {task.description && (
                          <div style={{ fontSize: "0.75rem", fontWeight: 400, color: "var(--color-content-muted, #64748b)" }}>
                            {task.description}
                          </div>
                        )}
                      </td>
                      <td style={{ padding: "0.75rem 1rem" }}>
                        <StatusBadge intent={taskStatusIntent(task.status)} label={t(`taskStatuses.${task.status}` as const)} size="small" />
                      </td>
                      <td style={{ padding: "0.75rem 1rem" }}>
                        <StatusBadge intent={priorityIntent(task.priority)} label={t(`taskPriorities.${task.priority}` as const)} size="small" />
                      </td>
                      <td style={{ padding: "0.75rem 1rem", fontSize: "0.8125rem", color: "var(--color-content-muted, #64748b)" }}>
                        {task.plannedStart || "—"}{task.plannedEnd ? ` — ${task.plannedEnd}` : ""}
                      </td>
                      <td style={{ padding: "0.75rem 1rem" }}>
                        <div style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
                          <div style={{ flex: 1, maxWidth: "6rem", height: "0.5rem", background: "#e2e8f0", borderRadius: "9999px", overflow: "hidden" }}>
                            <div style={{ width: `${Math.min(100, Math.max(0, task.progressPercent))}%`, height: "100%", background: "var(--color-status-success, #059669)" }} />
                          </div>
                          <span style={{ fontSize: "0.75rem", fontWeight: 600 }}>{task.progressPercent}%</span>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )
        ) : subTab === "milestones" ? (
          /* Milestones View */
          milestones.length === 0 ? (
            <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted, #64748b)" }}>
              {t("messages.noMilestones")}
            </div>
          ) : (
            <div style={{ overflowX: "auto" }}>
              <table style={{ width: "100%", borderCollapse: "collapse", textAlign: "start", fontSize: "0.875rem" }}>
                <thead>
                  <tr style={{ background: "var(--color-surface-subtle, #f8fafc)", borderBottom: "1px solid var(--color-border-default, #cbd5e1)" }}>
                    <th style={{ padding: "0.75rem 1rem", fontWeight: 600, textAlign: "start" }}>{t("fields.milestoneName")}</th>
                    <th style={{ padding: "0.75rem 1rem", fontWeight: 600, textAlign: "start" }}>{t("fields.status")}</th>
                    <th style={{ padding: "0.75rem 1rem", fontWeight: 600, textAlign: "start" }}>{t("fields.dueDate")}</th>
                    <th style={{ padding: "0.75rem 1rem", fontWeight: 600, textAlign: "start" }}>{t("fields.acceptanceRef")}</th>
                    <th style={{ padding: "0.75rem 1rem", fontWeight: 600, textAlign: "end" }}>{t("tables.actions")}</th>
                  </tr>
                </thead>
                <tbody>
                  {milestones.map((m) => (
                    <tr key={m.id} style={{ borderBottom: "1px solid var(--color-border-subtle, #e2e8f0)" }}>
                      <td style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-primary, #0f172a)" }}>
                        {m.name}
                      </td>
                      <td style={{ padding: "0.75rem 1rem" }}>
                        <StatusBadge intent={milestoneStatusIntent(m.status)} label={t(`milestoneStatuses.${m.status}` as const)} size="small" />
                      </td>
                      <td style={{ padding: "0.75rem 1rem", color: "var(--color-content-muted, #64748b)" }}>
                        {m.dueDate || "—"}
                      </td>
                      <td style={{ padding: "0.75rem 1rem", color: "var(--color-content-muted, #64748b)" }}>
                        {m.acceptanceReference || "—"}
                      </td>
                      <td style={{ padding: "0.75rem 1rem", textAlign: "end" }}>
                        <div style={{ display: "flex", gap: "0.5rem", justifyContent: "flex-end" }}>
                          {(m.status === "Planned" || m.status === "Open") && (
                            <Button variant="secondary" size="compact" onClick={() => onMilestoneAction(m.id, "reach")} data-testid="btn-reach-milestone">
                              {t("actions.reach")}
                            </Button>
                          )}
                          {m.status === "Reached" && (
                            <>
                              <Button variant="primary" size="compact" onClick={() => onMilestoneAction(m.id, "accept")} data-testid="btn-accept-milestone">
                                {t("actions.approve")}
                              </Button>
                              <Button variant="secondary" size="compact" onClick={() => onMilestoneAction(m.id, "reject")} data-testid="btn-reject-milestone">
                                {t("actions.reject")}
                              </Button>
                            </>
                          )}
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )
        ) : (
          /* Deliverables View */
          deliverables.length === 0 ? (
            <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted, #64748b)" }}>
              {t("messages.noDeliverables")}
            </div>
          ) : (
            <div style={{ overflowX: "auto" }}>
              <table style={{ width: "100%", borderCollapse: "collapse", textAlign: "start", fontSize: "0.875rem" }}>
                <thead>
                  <tr style={{ background: "var(--color-surface-subtle, #f8fafc)", borderBottom: "1px solid var(--color-border-default, #cbd5e1)" }}>
                    <th style={{ padding: "0.75rem 1rem", fontWeight: 600, textAlign: "start" }}>{t("fields.title")}</th>
                    <th style={{ padding: "0.75rem 1rem", fontWeight: 600, textAlign: "start" }}>{t("fields.description")}</th>
                    <th style={{ padding: "0.75rem 1rem", fontWeight: 600, textAlign: "start" }}>{t("fields.status")}</th>
                    <th style={{ padding: "0.75rem 1rem", fontWeight: 600, textAlign: "end" }}>{t("tables.actions")}</th>
                  </tr>
                </thead>
                <tbody>
                  {deliverables.map((d) => (
                    <tr key={d.id} style={{ borderBottom: "1px solid var(--color-border-subtle, #e2e8f0)" }}>
                      <td style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-primary, #0f172a)" }}>
                        {d.name || (d as any).title}
                      </td>
                      <td style={{ padding: "0.75rem 1rem", color: "var(--color-content-muted, #64748b)" }}>
                        {d.description || "—"}
                      </td>
                      <td style={{ padding: "0.75rem 1rem" }}>
                        <StatusBadge
                          intent={d.status === "Accepted" ? "success" : d.status === "Submitted" ? "info" : "neutral"}
                          label={t(`deliverableStatuses.${d.status}` as const) || d.status}
                          size="small"
                        />
                      </td>
                      <td style={{ padding: "0.75rem 1rem", textAlign: "end" }}>
                        <div style={{ display: "flex", gap: "0.5rem", justifyContent: "flex-end" }}>
                          {d.status === "Draft" && (
                            <Button variant="secondary" size="compact" onClick={() => onDeliverableAction(d.id, "submit")} data-testid="btn-submit-deliverable-action">
                              {t("actions.submit")}
                            </Button>
                          )}
                          {d.status === "Submitted" && (
                            <>
                              <Button variant="primary" size="compact" onClick={() => onDeliverableAction(d.id, "accept")} data-testid="btn-accept-deliverable-action">
                                {t("actions.approve")}
                              </Button>
                              <Button variant="secondary" size="compact" onClick={() => onDeliverableAction(d.id, "reject")} data-testid="btn-reject-deliverable-action">
                                {t("actions.reject")}
                              </Button>
                            </>
                          )}
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )
        )}
      </Surface>
    </div>
  );
}

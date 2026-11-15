import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TeamUsersIcon, PlusCircleIcon } from "../../components/projects-icons.tsx";
import type {
  ProjectTeamMemberView,
  ProjectResourceRequirementView,
  ProjectResourceAllocationView
} from "../../types.ts";

export interface TeamResourcesTabProps {
  readonly team: readonly ProjectTeamMemberView[];
  readonly requirements: readonly ProjectResourceRequirementView[];
  readonly allocations: readonly ProjectResourceAllocationView[];
  readonly isLoading: boolean;
  readonly onAddTeamMember: () => void;
  readonly onAddRequirement: () => void;
  readonly onAllocateResource: () => void;
}

export function TeamResourcesTab({
  team,
  requirements,
  allocations,
  isLoading,
  onAddTeamMember,
  onAddRequirement,
  onAllocateResource
}: TeamResourcesTabProps): React.JSX.Element {
  const { t } = useTranslation("projects");
  const [subTab, setSubTab] = useState<"team" | "requirements" | "allocations">("team");

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
      {/* Sub-tab Navigation */}
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
            data-testid="subtab-team"
            onClick={() => setSubTab("team")}
            style={{
              padding: "0.5rem 1rem",
              borderRadius: "var(--radius-md, 8px)",
              border: "none",
              background: subTab === "team" ? "var(--color-brand-primary, #102a43)" : "transparent",
              color: subTab === "team" ? "#ffffff" : "var(--color-content-muted, #64748b)",
              fontWeight: 600,
              fontSize: "0.875rem",
              cursor: "pointer",
              display: "flex",
              alignItems: "center",
              gap: "0.5rem"
            }}
          >
            <TeamUsersIcon size={16} />
            {t("tabs.team")} ({team.length})
          </button>

          <button
            type="button"
            data-testid="subtab-requirements"
            onClick={() => setSubTab("requirements")}
            style={{
              padding: "0.5rem 1rem",
              borderRadius: "var(--radius-md, 8px)",
              border: "none",
              background: subTab === "requirements" ? "var(--color-brand-primary, #102a43)" : "transparent",
              color: subTab === "requirements" ? "#ffffff" : "var(--color-content-muted, #64748b)",
              fontWeight: 600,
              fontSize: "0.875rem",
              cursor: "pointer"
            }}
          >
            {t("dialogs.newResourceReqTitle")} ({requirements.length})
          </button>

          <button
            type="button"
            data-testid="subtab-allocations"
            onClick={() => setSubTab("allocations")}
            style={{
              padding: "0.5rem 1rem",
              borderRadius: "var(--radius-md, 8px)",
              border: "none",
              background: subTab === "allocations" ? "var(--color-brand-primary, #102a43)" : "transparent",
              color: subTab === "allocations" ? "#ffffff" : "var(--color-content-muted, #64748b)",
              fontWeight: 600,
              fontSize: "0.875rem",
              cursor: "pointer"
            }}
          >
            {t("dialogs.allocateResourceTitle")} ({allocations.length})
          </button>
        </div>

        <div>
          {subTab === "team" && (
            <Button variant="primary" size="compact" onClick={onAddTeamMember} data-testid="btn-add-team">
              <PlusCircleIcon size={16} style={{ marginInlineEnd: "0.25rem" }} />
              {t("actions.addTeamMember")}
            </Button>
          )}
          {subTab === "requirements" && (
            <Button variant="primary" size="compact" onClick={onAddRequirement} data-testid="btn-add-req">
              <PlusCircleIcon size={16} style={{ marginInlineEnd: "0.25rem" }} />
              {t("actions.addResourceReq")}
            </Button>
          )}
          {subTab === "allocations" && (
            <Button variant="primary" size="compact" onClick={onAllocateResource} data-testid="btn-allocate-res">
              <PlusCircleIcon size={16} style={{ marginInlineEnd: "0.25rem" }} />
              {t("actions.allocateResource")}
            </Button>
          )}
        </div>
      </Surface>

      {/* Content Table */}
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
        ) : subTab === "team" ? (
          team.length === 0 ? (
            <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted, #64748b)" }}>
              {t("messages.noTeam")}
            </div>
          ) : (
            <div style={{ overflowX: "auto" }}>
              <table style={{ width: "100%", borderCollapse: "collapse", textAlign: "start", fontSize: "0.875rem" }}>
                <thead>
                  <tr style={{ background: "var(--color-surface-subtle, #f8fafc)", borderBottom: "1px solid var(--color-border-default, #cbd5e1)" }}>
                    <th style={{ padding: "0.75rem 1rem", fontWeight: 600, textAlign: "start" }}>{t("tables.role")}</th>
                    <th style={{ padding: "0.75rem 1rem", fontWeight: 600, textAlign: "start" }}>{t("tables.allocation")}</th>
                    <th style={{ padding: "0.75rem 1rem", fontWeight: 600, textAlign: "start" }}>{t("tables.period")}</th>
                    <th style={{ padding: "0.75rem 1rem", fontWeight: 600, textAlign: "start" }}>{t("fields.isManager")}</th>
                  </tr>
                </thead>
                <tbody>
                  {team.map((member) => (
                    <tr key={member.id} style={{ borderBottom: "1px solid var(--color-border-subtle, #e2e8f0)" }}>
                      <td style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-primary, #0f172a)" }}>
                        {member.roleCode}
                      </td>
                      <td style={{ padding: "0.75rem 1rem" }}>
                        <span style={{ fontWeight: 600 }}>{member.allocationPercent}%</span>
                      </td>
                      <td style={{ padding: "0.75rem 1rem", color: "var(--color-content-muted, #64748b)" }}>
                        {member.activeFrom}{member.activeTo ? ` — ${member.activeTo}` : ""}
                      </td>
                      <td style={{ padding: "0.75rem 1rem" }}>
                        {member.isManager ? (
                          <span style={{ color: "var(--color-status-success, #059669)", fontWeight: 700 }}>✓</span>
                        ) : (
                          <span style={{ color: "var(--color-content-subtle, #94a3b8)" }}>—</span>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )
        ) : subTab === "requirements" ? (
          requirements.length === 0 ? (
            <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted, #64748b)" }}>
              {t("messages.noTeam")}
            </div>
          ) : (
            <div style={{ overflowX: "auto" }}>
              <table style={{ width: "100%", borderCollapse: "collapse", textAlign: "start", fontSize: "0.875rem" }}>
                <thead>
                  <tr style={{ background: "var(--color-surface-subtle, #f8fafc)", borderBottom: "1px solid var(--color-border-default, #cbd5e1)" }}>
                    <th style={{ padding: "0.75rem 1rem", fontWeight: 600, textAlign: "start" }}>{t("fields.roleCode")}</th>
                    <th style={{ padding: "0.75rem 1rem", fontWeight: 600, textAlign: "start" }}>{t("tables.amount")}</th>
                    <th style={{ padding: "0.75rem 1rem", fontWeight: 600, textAlign: "start" }}>{t("tables.period")}</th>
                    <th style={{ padding: "0.75rem 1rem", fontWeight: 600, textAlign: "start" }}>{t("fields.status")}</th>
                  </tr>
                </thead>
                <tbody>
                  {requirements.map((req) => (
                    <tr key={req.id} style={{ borderBottom: "1px solid var(--color-border-subtle, #e2e8f0)" }}>
                      <td style={{ padding: "0.75rem 1rem", fontWeight: 600 }}>{req.roleCode}</td>
                      <td style={{ padding: "0.75rem 1rem" }}>{req.quantity}</td>
                      <td style={{ padding: "0.75rem 1rem", color: "var(--color-content-muted, #64748b)" }}>
                        {req.requiredFrom || "—"}{req.requiredTo ? ` — ${req.requiredTo}` : ""}
                      </td>
                      <td style={{ padding: "0.75rem 1rem" }}>{req.status}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )
        ) : (
          allocations.length === 0 ? (
            <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted, #64748b)" }}>
              {t("messages.noTeam")}
            </div>
          ) : (
            <div style={{ overflowX: "auto" }}>
              <table style={{ width: "100%", borderCollapse: "collapse", textAlign: "start", fontSize: "0.875rem" }}>
                <thead>
                  <tr style={{ background: "var(--color-surface-subtle, #f8fafc)", borderBottom: "1px solid var(--color-border-default, #cbd5e1)" }}>
                    <th style={{ padding: "0.75rem 1rem", fontWeight: 600, textAlign: "start" }}>{t("tables.resource")}</th>
                    <th style={{ padding: "0.75rem 1rem", fontWeight: 600, textAlign: "start" }}>{t("tables.allocation")}</th>
                    <th style={{ padding: "0.75rem 1rem", fontWeight: 600, textAlign: "start" }}>{t("tables.period")}</th>
                    <th style={{ padding: "0.75rem 1rem", fontWeight: 600, textAlign: "start" }}>{t("fields.status")}</th>
                  </tr>
                </thead>
                <tbody>
                  {allocations.map((alloc) => (
                    <tr key={alloc.id} style={{ borderBottom: "1px solid var(--color-border-subtle, #e2e8f0)" }}>
                      <td style={{ padding: "0.75rem 1rem", fontWeight: 600 }}>
                        {alloc.resourceRef || alloc.employeeId || "—"}
                      </td>
                      <td style={{ padding: "0.75rem 1rem" }}>{alloc.allocationPercent}%</td>
                      <td style={{ padding: "0.75rem 1rem", color: "var(--color-content-muted, #64748b)" }}>
                        {alloc.allocatedFrom}{alloc.allocatedTo ? ` — ${alloc.allocatedTo}` : ""}
                      </td>
                      <td style={{ padding: "0.75rem 1rem" }}>{alloc.status}</td>
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

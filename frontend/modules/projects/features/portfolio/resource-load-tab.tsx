import React from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../../components/pages/surface.tsx";
import type { ProjectResourceLoadView } from "../../types.ts";

export interface ResourceLoadTabProps {
  readonly resourceLoad: readonly ProjectResourceLoadView[];
  readonly isLoading: boolean;
}

export function ResourceLoadTab({ resourceLoad, isLoading }: ResourceLoadTabProps): React.JSX.Element {
  const { t } = useTranslation("projects");
  const loadList = Array.isArray(resourceLoad) ? resourceLoad : [];

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
      <Surface
        variant="card"
        style={{
          background: "var(--color-surface-card, #ffffff)",
          border: "1px solid var(--color-border-default, #cbd5e1)",
          borderRadius: "var(--radius-lg, 12px)",
          overflow: "hidden"
        }}
      >
        <div style={{ padding: "1.25rem", borderBottom: "1px solid var(--color-border-default, #cbd5e1)" }}>
          <h2 style={{ margin: 0, fontSize: "1.125rem", fontWeight: 700, color: "var(--color-content-primary, #0f172a)" }}>
            {t("tabs.resourceLoad")}
          </h2>
        </div>

        {isLoading ? (
          <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted, #64748b)" }}>
            {t("actions.refresh")}...
          </div>
        ) : loadList.length === 0 ? (
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
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, textAlign: "start" }}>{t("tables.projectsCount")}</th>
                </tr>
              </thead>
              <tbody>
                {loadList.map((item) => {
                  const percent = Math.min(100, Math.max(0, item.allocationPercent));
                  const isOverallocated = item.allocationPercent > 100;
                  return (
                    <tr key={item.resourceKey} style={{ borderBottom: "1px solid var(--color-border-subtle, #e2e8f0)" }}>
                      <td style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-primary, #0f172a)" }}>
                        {item.resourceKey}
                      </td>
                      <td style={{ padding: "0.75rem 1rem" }}>
                        <div style={{ display: "flex", alignItems: "center", gap: "0.75rem" }}>
                          <div style={{ flex: 1, maxWidth: "12rem", height: "0.5rem", background: "#e2e8f0", borderRadius: "9999px", overflow: "hidden" }}>
                            <div
                              style={{
                                width: `${percent}%`,
                                height: "100%",
                                background: isOverallocated ? "var(--color-status-danger, #dc2626)" : "var(--color-brand-primary, #2563eb)"
                              }}
                            />
                          </div>
                          <span style={{ fontSize: "0.8125rem", fontWeight: 600, color: isOverallocated ? "var(--color-status-danger, #dc2626)" : "inherit" }}>
                            {item.allocationPercent}%
                          </span>
                        </div>
                      </td>
                      <td style={{ padding: "0.75rem 1rem", color: "var(--color-content-muted, #64748b)" }}>
                        {item.projectCount}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </Surface>
    </div>
  );
}

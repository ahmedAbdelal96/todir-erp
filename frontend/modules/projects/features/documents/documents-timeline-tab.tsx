import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { DocumentFolderIcon, PlusCircleIcon } from "../../components/projects-icons.tsx";
import type { ProjectDocumentView, ProjectTimelineEventView } from "../../types.ts";

export interface DocumentsTimelineTabProps {
  readonly documents: readonly ProjectDocumentView[];
  readonly timeline: readonly ProjectTimelineEventView[];
  readonly isLoading: boolean;
  readonly onAddDocument: () => void;
}

export function DocumentsTimelineTab({
  documents,
  timeline,
  isLoading,
  onAddDocument
}: DocumentsTimelineTabProps): React.JSX.Element {
  const { t } = useTranslation("projects");
  const [subTab, setSubTab] = useState<"documents" | "timeline">("documents");

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
      {/* Sub-tab navigation */}
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
            onClick={() => setSubTab("documents")}
            style={{
              padding: "0.5rem 1rem",
              borderRadius: "var(--radius-md, 8px)",
              border: "none",
              background: subTab === "documents" ? "var(--color-brand-primary, #102a43)" : "transparent",
              color: subTab === "documents" ? "#ffffff" : "var(--color-content-muted, #64748b)",
              fontWeight: 600,
              fontSize: "0.875rem",
              cursor: "pointer",
              display: "flex",
              alignItems: "center",
              gap: "0.5rem"
            }}
          >
            <DocumentFolderIcon size={16} />
            {t("tabs.documents")} ({documents.length})
          </button>

          <button
            type="button"
            onClick={() => setSubTab("timeline")}
            style={{
              padding: "0.5rem 1rem",
              borderRadius: "var(--radius-md, 8px)",
              border: "none",
              background: subTab === "timeline" ? "var(--color-brand-primary, #102a43)" : "transparent",
              color: subTab === "timeline" ? "#ffffff" : "var(--color-content-muted, #64748b)",
              fontWeight: 600,
              fontSize: "0.875rem",
              cursor: "pointer"
            }}
          >
            {t("tables.action")} ({timeline.length})
          </button>
        </div>

        {subTab === "documents" && (
          <Button variant="primary" size="compact" onClick={onAddDocument} data-testid="btn-add-doc">
            <PlusCircleIcon size={16} style={{ marginInlineEnd: "0.25rem" }} />
            {t("actions.addDocument")}
          </Button>
        )}
      </Surface>

      {/* Content */}
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
        ) : subTab === "documents" ? (
          documents.length === 0 ? (
            <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted, #64748b)" }}>
              {t("messages.noDocuments")}
            </div>
          ) : (
            <div style={{ overflowX: "auto" }}>
              <table style={{ width: "100%", borderCollapse: "collapse", textAlign: "start", fontSize: "0.875rem" }}>
                <thead>
                  <tr style={{ background: "var(--color-surface-subtle, #f8fafc)", borderBottom: "1px solid var(--color-border-default, #cbd5e1)" }}>
                    <th style={{ padding: "0.75rem 1rem", fontWeight: 600, textAlign: "start" }}>{t("fields.title")}</th>
                    <th style={{ padding: "0.75rem 1rem", fontWeight: 600, textAlign: "start" }}>{t("fields.documentRef")}</th>
                    <th style={{ padding: "0.75rem 1rem", fontWeight: 600, textAlign: "start" }}>{t("fields.documentType")}</th>
                    <th style={{ padding: "0.75rem 1rem", fontWeight: 600, textAlign: "start" }}>{t("fields.sensitivity")}</th>
                    <th style={{ padding: "0.75rem 1rem", fontWeight: 600, textAlign: "start" }}>{t("tables.date")}</th>
                  </tr>
                </thead>
                <tbody>
                  {documents.map((doc) => (
                    <tr key={doc.id} style={{ borderBottom: "1px solid var(--color-border-subtle, #e2e8f0)" }}>
                      <td style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-primary, #0f172a)" }}>
                        {doc.title || doc.documentRef}
                      </td>
                      <td style={{ padding: "0.75rem 1rem", fontFamily: "monospace", color: "var(--color-brand-primary, #2563eb)" }}>
                        {doc.documentRef}
                      </td>
                      <td style={{ padding: "0.75rem 1rem" }}>{doc.documentType}</td>
                      <td style={{ padding: "0.75rem 1rem" }}>{doc.sensitivity}</td>
                      <td style={{ padding: "0.75rem 1rem", color: "var(--color-content-muted, #64748b)", fontSize: "0.8125rem" }}>
                        {doc.createdAt}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )
        ) : (
          timeline.length === 0 ? (
            <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted, #64748b)" }}>
              {t("messages.noDocuments")}
            </div>
          ) : (
            <div style={{ overflowX: "auto" }}>
              <table style={{ width: "100%", borderCollapse: "collapse", textAlign: "start", fontSize: "0.875rem" }}>
                <thead>
                  <tr style={{ background: "var(--color-surface-subtle, #f8fafc)", borderBottom: "1px solid var(--color-border-default, #cbd5e1)" }}>
                    <th style={{ padding: "0.75rem 1rem", fontWeight: 600, textAlign: "start" }}>{t("tables.action")}</th>
                    <th style={{ padding: "0.75rem 1rem", fontWeight: 600, textAlign: "start" }}>{t("tables.occurredAt")}</th>
                  </tr>
                </thead>
                <tbody>
                  {timeline.map((ev) => (
                    <tr key={ev.id} style={{ borderBottom: "1px solid var(--color-border-subtle, #e2e8f0)" }}>
                      <td style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-primary, #0f172a)" }}>
                        {ev.action}
                      </td>
                      <td style={{ padding: "0.75rem 1rem", color: "var(--color-content-muted, #64748b)", fontSize: "0.8125rem" }}>
                        {ev.occurredAt}
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

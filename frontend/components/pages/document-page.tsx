import React, { type ReactNode } from "react";
import { Breadcrumbs, type BreadcrumbItem } from "../../app/shell/breadcrumbs.tsx";
import { StatusBadge, type StatusVariant } from "../fields/status-badge.tsx";
import clsx from "clsx";

export interface DocumentSummaryItem {
  readonly label: string;
  readonly value: ReactNode;
  readonly isEmphasized?: boolean | undefined;
}

export interface DocumentStatusGroup {
  readonly lifecycle?: StatusVariant | string | undefined;
  readonly approval?: StatusVariant | string | undefined;
  readonly accounting?: StatusVariant | string | undefined;
  readonly compliance?: StatusVariant | string | undefined;
  readonly settlement?: StatusVariant | string | undefined;
  readonly isLocked?: boolean | undefined;
  readonly lockedReason?: string | undefined;
}

export interface DocumentPageProps {
  readonly documentType: string;
  readonly documentNumber: string;
  readonly status?: StatusVariant | string | undefined;
  readonly statuses?: DocumentStatusGroup | undefined;
  readonly breadcrumbs?: readonly BreadcrumbItem[] | undefined;
  readonly headerActions?: ReactNode | undefined;
  readonly metadataSection: ReactNode;
  readonly linesSection: ReactNode;
  readonly summaryItems?: readonly DocumentSummaryItem[] | undefined;
  readonly notesSection?: ReactNode | undefined;
  readonly children?: ReactNode | undefined;
  readonly className?: string | undefined;
}

export function DocumentPage({
  documentType,
  documentNumber,
  status,
  statuses,
  breadcrumbs,
  headerActions,
  metadataSection,
  linesSection,
  summaryItems = [],
  notesSection,
  children,
  className
}: DocumentPageProps): React.JSX.Element {
  return (
    <div
      className={clsx("ui-document-page", className)}
      style={{
        display: "flex",
        flexDirection: "column",
        gap: "1.5rem",
        padding: "1.5rem",
        maxWidth: "1400px",
        margin: "0 auto",
        width: "100%",
        boxSizing: "border-box"
      }}
    >
      {breadcrumbs && <Breadcrumbs items={breadcrumbs} />}

      <header
        style={{
          display: "flex",
          alignItems: "center",
          justifyContent: "space-between",
          flexWrap: "wrap",
          gap: "1rem",
          paddingBottom: "1rem",
          borderBottom: "1px solid var(--color-border-default)"
        }}
      >
        <div>
          <div style={{ display: "flex", alignItems: "center", gap: "0.5rem", flexWrap: "wrap" }}>
            <span style={{ fontSize: "0.875rem", color: "var(--color-content-muted)", fontWeight: 500 }}>
              {documentType}
            </span>

            {/* Single status fallback */}
            {status && !statuses && <StatusBadge status={status} size="small" />}

            {/* Independent multi-dimension statuses */}
            {statuses && (
              <>
                {statuses.lifecycle && (
                  <StatusBadge
                    status={statuses.lifecycle}
                    label={`Lifecycle: ${statuses.lifecycle}`}
                    size="small"
                  />
                )}
                {statuses.approval && (
                  <StatusBadge
                    status={statuses.approval === "Approved" ? "success" : "warning"}
                    label={`Approval: ${statuses.approval}`}
                    size="small"
                  />
                )}
                {statuses.accounting && (
                  <StatusBadge
                    intent={statuses.accounting === "Posted" ? "success" : "neutral"}
                    label={`GL: ${statuses.accounting}`}
                    size="small"
                  />
                )}
                {statuses.compliance && (
                  <StatusBadge
                    status={statuses.compliance === "Valid" ? "success" : "info"}
                    label={`Compliance: ${statuses.compliance}`}
                    size="small"
                  />
                )}
                {statuses.settlement && (
                  <StatusBadge
                    status={statuses.settlement === "Paid" ? "success" : "warning"}
                    label={`Settlement: ${statuses.settlement}`}
                    size="small"
                  />
                )}
                {statuses.isLocked && (
                  <span
                    style={{
                      fontSize: "0.75rem",
                      fontWeight: 600,
                      padding: "0.125rem 0.5rem",
                      borderRadius: "var(--radius-sm)",
                      backgroundColor: "var(--color-surface-subtle)",
                      color: "var(--color-content-muted)",
                      border: "1px solid var(--color-border-default)"
                    }}
                  >
                    🔒 {statuses.lockedReason ?? "Locked"}
                  </span>
                )}
              </>
            )}
          </div>

          <h1
            style={{
              margin: "0.25rem 0 0 0",
              fontSize: "1.75rem",
              fontWeight: 700,
              color: "var(--color-content-primary)",
              fontFamily: "var(--font-sans)"
            }}
          >
            {documentNumber}
          </h1>
        </div>

        {headerActions && (
          <div style={{ display: "flex", alignItems: "center", gap: "0.75rem" }}>
            {headerActions}
          </div>
        )}
      </header>

      {/* Document Metadata Header */}
      <section
        style={{
          backgroundColor: "var(--color-surface-card)",
          border: "1px solid var(--color-border-default)",
          borderRadius: "var(--radius-lg, 0.5rem)",
          padding: "1.25rem"
        }}
      >
        {metadataSection}
      </section>

      {/* Line Items Area */}
      <section
        style={{
          backgroundColor: "var(--color-surface-card)",
          border: "1px solid var(--color-border-default)",
          borderRadius: "var(--radius-lg, 0.5rem)",
          overflow: "hidden"
        }}
      >
        {linesSection}
      </section>

      {/* Bottom Area: Notes & Totals */}
      <div
        style={{
          display: "grid",
          gridTemplateColumns: "repeat(auto-fit, minmax(300px, 1fr))",
          gap: "1.5rem",
          alignItems: "start"
        }}
      >
        <div>{notesSection}</div>

        {summaryItems.length > 0 && (
          <div
            style={{
              backgroundColor: "var(--color-surface-card)",
              border: "1px solid var(--color-border-default)",
              borderRadius: "var(--radius-lg, 0.5rem)",
              padding: "1.25rem",
              display: "flex",
              flexDirection: "column",
              gap: "0.75rem"
            }}
          >
            {summaryItems.map((item, idx) => (
              <div
                key={idx}
                style={{
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "space-between",
                  paddingBottom: item.isEmphasized ? "0" : "0.5rem",
                  borderBottom: item.isEmphasized ? "none" : "1px solid var(--color-border-subtle, #f1f5f9)",
                  fontWeight: item.isEmphasized ? 700 : 400,
                  fontSize: item.isEmphasized ? "1.125rem" : "0.875rem",
                  color: item.isEmphasized ? "var(--color-content-primary)" : "var(--color-content-secondary)"
                }}
              >
                <span>{item.label}</span>
                <span>{item.value}</span>
              </div>
            ))}
          </div>
        )}
      </div>

      {children}
    </div>
  );
}

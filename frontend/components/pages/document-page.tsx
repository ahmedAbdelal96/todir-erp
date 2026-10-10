import React, { type ReactNode } from "react";
import { Breadcrumbs, type BreadcrumbItem } from "../../app/shell/breadcrumbs.tsx";
import { StatusBadge, type StatusVariant } from "../fields/status-badge.tsx";
import { LockIcon } from "../primitives/icons.tsx";
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

function getLocalizedLifecycle(status: string): { label: string; intent: StatusVariant } {
  const s = status.toLowerCase();
  if (s === "posted" || s === "confirmed" || s === "active") return { label: "مؤكد", intent: "success" };
  if (s === "draft") return { label: "مسودة", intent: "neutral" };
  if (s === "cancelled" || s === "voided") return { label: "ملغى", intent: "danger" };
  return { label: status, intent: "neutral" };
}

function getLocalizedAccounting(status: string): { label: string; intent: StatusVariant } {
  if (status === "Posted") return { label: "مرحّل محاسبياً", intent: "success" };
  return { label: "غير مرحّل", intent: "warning" };
}

function getLocalizedApproval(status: string): { label: string; intent: StatusVariant } {
  if (status === "Approved") return { label: "معتمد إدارياً", intent: "success" };
  if (status === "Rejected") return { label: "مرفوض", intent: "danger" };
  return { label: "بانتظار الاعتماد", intent: "warning" };
}

function getLocalizedCompliance(status: string): { label: string; intent: StatusVariant } {
  if (status === "Valid") return { label: "مطابق ضريبياً (ZATCA)", intent: "success" };
  return { label: `الامتثال: ${status}`, intent: "info" };
}

function getLocalizedSettlement(status: string): { label: string; intent: StatusVariant } {
  if (status === "Paid") return { label: "مسدد بالكامل", intent: "success" };
  if (status === "Partial") return { label: "مسدد جزئياً", intent: "warning" };
  return { label: "غير مسدد", intent: "neutral" };
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
          paddingBottom: "1.25rem",
          borderBottom: "1px solid var(--color-border-default)"
        }}
      >
        <div>
          <div style={{ display: "flex", alignItems: "center", gap: "0.5rem", flexWrap: "wrap", marginBottom: "0.375rem" }}>
            <span
              style={{
                fontSize: "0.8125rem",
                fontWeight: 600,
                color: "var(--color-content-muted)",
                backgroundColor: "var(--color-surface-subtle)",
                padding: "0.2rem 0.5rem",
                borderRadius: "var(--radius-sm)",
                border: "1px solid var(--color-border-default)"
              }}
            >
              {documentType}
            </span>

            {/* Single status fallback */}
            {status && !statuses && <StatusBadge status={status} size="small" />}

            {/* Independent multi-dimension statuses */}
            {statuses && (
              <>
                {statuses.lifecycle && (
                  (() => {
                    const { label, intent } = getLocalizedLifecycle(String(statuses.lifecycle));
                    return <StatusBadge intent={intent} label={label} size="small" />;
                  })()
                )}
                {statuses.approval && (
                  (() => {
                    const { label, intent } = getLocalizedApproval(String(statuses.approval));
                    return <StatusBadge intent={intent} label={label} size="small" />;
                  })()
                )}
                {statuses.accounting && (
                  (() => {
                    const { label, intent } = getLocalizedAccounting(String(statuses.accounting));
                    return <StatusBadge intent={intent} label={label} size="small" />;
                  })()
                )}
                {statuses.compliance && (
                  (() => {
                    const { label, intent } = getLocalizedCompliance(String(statuses.compliance));
                    return <StatusBadge intent={intent} label={label} size="small" />;
                  })()
                )}
                {statuses.settlement && (
                  (() => {
                    const { label, intent } = getLocalizedSettlement(String(statuses.settlement));
                    return <StatusBadge intent={intent} label={label} size="small" />;
                  })()
                )}
                {statuses.isLocked && (
                  <span
                    style={{
                      display: "inline-flex",
                      alignItems: "center",
                      gap: "0.25rem",
                      fontSize: "0.75rem",
                      fontWeight: 600,
                      padding: "0.2rem 0.5rem",
                      borderRadius: "var(--radius-sm)",
                      backgroundColor: "var(--color-surface-subtle)",
                      color: "var(--color-content-muted)",
                      border: "1px solid var(--color-border-default)"
                    }}
                  >
                    <LockIcon size={12} />
                    <span>{statuses.lockedReason ?? "مستند مقفل"}</span>
                  </span>
                )}
              </>
            )}
          </div>

          <h1
            style={{
              margin: 0,
              fontSize: "1.75rem",
              fontWeight: 800,
              color: "var(--color-content-primary)",
              fontFamily: "var(--font-sans)",
              letterSpacing: "-0.02em"
            }}
          >
            {documentNumber}
          </h1>
        </div>

        {headerActions && (
          <div style={{ display: "flex", alignItems: "center", gap: "0.75rem", flexWrap: "wrap" }}>
            {headerActions}
          </div>
        )}
      </header>

      {/* Document Metadata Header */}
      <section
        style={{
          backgroundColor: "var(--color-surface-card)",
          border: "1px solid var(--color-border-default)",
          borderRadius: "var(--radius-lg, 0.75rem)",
          padding: "1.25rem",
          boxShadow: "var(--shadow-xs, 0 1px 2px rgba(0, 0, 0, 0.04))"
        }}
      >
        {metadataSection}
      </section>

      {/* Line Items Area */}
      <section
        style={{
          backgroundColor: "var(--color-surface-card)",
          border: "1px solid var(--color-border-default)",
          borderRadius: "var(--radius-lg, 0.75rem)",
          overflow: "hidden",
          boxShadow: "var(--shadow-xs, 0 1px 2px rgba(0, 0, 0, 0.04))"
        }}
      >
        {linesSection}
      </section>

      {/* Bottom Area: Notes & Totals */}
      <div
        style={{
          display: "grid",
          gridTemplateColumns: "repeat(auto-fit, minmax(320px, 1fr))",
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
              borderRadius: "var(--radius-lg, 0.75rem)",
              padding: "1.25rem",
              display: "flex",
              flexDirection: "column",
              gap: "0.75rem",
              boxShadow: "var(--shadow-xs, 0 1px 2px rgba(0, 0, 0, 0.04))"
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
                  fontWeight: item.isEmphasized ? 700 : 500,
                  fontSize: item.isEmphasized ? "1.125rem" : "0.875rem",
                  color: item.isEmphasized ? "var(--color-content-primary)" : "var(--color-content-secondary)",
                  paddingTop: item.isEmphasized ? "0.5rem" : "0"
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

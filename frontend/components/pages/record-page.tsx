import React, { type ReactNode } from "react";
import { Breadcrumbs, type BreadcrumbItem } from "../../app/shell/breadcrumbs.tsx";
import { StatusBadge, type StatusVariant } from "../fields/status-badge.tsx";
import { PageHelpButton, type PageHelpContent } from "./page-help.tsx";
import clsx from "clsx";
import { useTranslation } from "react-i18next";
import { BidiText } from "../primitives/bidi-text.tsx";

export interface RecordPageProps {
  readonly title: string;
  readonly recordCode?: string | undefined;
  readonly status?: StatusVariant | string | undefined;
  readonly statusLabel?: ReactNode | undefined;
  readonly breadcrumbs?: readonly BreadcrumbItem[] | undefined;
  readonly headerActions?: ReactNode | undefined;
  readonly help?: PageHelpContent | undefined;
  readonly kpiCards?: ReactNode | undefined;
  readonly tabs?: ReactNode | undefined;
  readonly sidebar?: ReactNode | undefined;
  readonly auditInfo?: {
    readonly createdAt?: ReactNode | undefined;
    readonly createdBy?: string | undefined;
    readonly updatedAt?: ReactNode | undefined;
    readonly updatedBy?: string | undefined;
  } | undefined;
  readonly children: ReactNode;
  readonly className?: string | undefined;
}

export function RecordPage({
  title,
  recordCode,
  status,
  statusLabel,
  breadcrumbs,
  headerActions,
  help,
  kpiCards,
  tabs,
  sidebar,
  auditInfo,
  children,
  className
}: RecordPageProps): React.JSX.Element {
  const { t } = useTranslation("common");
  return (
    <div
      className={clsx("ui-record-page", className)}
      style={{
        display: "flex",
        flexDirection: "column",
        gap: "1.25rem",
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
          borderBottom: "1px solid var(--color-border-default, #e2e8f0)"
        }}
      >
        <div style={{ display: "flex", alignItems: "center", gap: "1rem" }}>
          <div>
            <div style={{ display: "flex", alignItems: "center", gap: "0.5rem", flexWrap: "wrap" }}>
              {recordCode && (
                <span
                  style={{
                    fontFamily: "var(--font-mono, monospace)",
                    fontSize: "0.875rem",
                    fontWeight: 600,
                    color: "var(--color-content-muted, #64748b)"
                  }}
                >
                  <bdi>{recordCode}</bdi>
                </span>
              )}
              {status && <StatusBadge status={status} label={statusLabel} size="small" />}
            </div>
            <h1
              style={{
                margin: "0.25rem 0 0 0",
                fontSize: "1.5rem",
                fontWeight: 800,
                letterSpacing: "-0.01em",
                color: "var(--color-content-primary, #0f172a)",
                fontFamily: "var(--font-sans)"
              }}
            >
              {title}
            </h1>
          </div>
        </div>

        {(headerActions || help) && (
          <div style={{ display: "flex", alignItems: "center", gap: "0.75rem", flexWrap: "wrap" }}>
            {help && <PageHelpButton content={help} />}
            {headerActions}
          </div>
        )}
      </header>

      {/* Optional Top KPI / Metrics Summary Row */}
      {kpiCards && (
        <div
          style={{
            display: "grid",
            gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))",
            gap: "1rem",
            width: "100%"
          }}
        >
          {kpiCards}
        </div>
      )}

      {/* Optional Tab Navigation */}
      {tabs && (
        <div style={{ width: "100%" }}>
          {tabs}
        </div>
      )}

      <div
        className="ui-record-page__body"
        style={{
          display: "grid",
          gridTemplateColumns: sidebar ? "1fr 340px" : "1fr",
          gap: "1.5rem",
          alignItems: "start"
        }}
      >
        <main style={{ display: "flex", flexDirection: "column", gap: "1.5rem", minWidth: 0 }}>
          {children}
        </main>

        {sidebar && (
          <aside
            style={{
              display: "flex",
              flexDirection: "column",
              gap: "1rem",
              position: "sticky",
              top: "1rem"
            }}
          >
            {sidebar}
          </aside>
        )}
      </div>

      {auditInfo && (
        <footer
          style={{
            marginTop: "2rem",
            paddingTop: "1rem",
            borderTop: "1px solid var(--color-border-subtle, #f1f5f9)",
            display: "flex",
            alignItems: "center",
            justifyContent: "space-between",
            fontSize: "0.75rem",
            color: "var(--color-content-muted, #64748b)",
            flexWrap: "wrap",
            gap: "0.5rem"
          }}
        >
          {auditInfo.createdAt && (
            <span>
              {t("components.createdAt")} {typeof auditInfo.createdAt === "string" ? <BidiText>{auditInfo.createdAt}</BidiText> : auditInfo.createdAt} {auditInfo.createdBy ? t("components.byActor", { actor: auditInfo.createdBy }) : ""}
            </span>
          )}
          {auditInfo.updatedAt && (
            <span>
              {t("components.updatedAt")} {typeof auditInfo.updatedAt === "string" ? <BidiText>{auditInfo.updatedAt}</BidiText> : auditInfo.updatedAt} {auditInfo.updatedBy ? t("components.byActor", { actor: auditInfo.updatedBy }) : ""}
            </span>
          )}
        </footer>
      )}
    </div>
  );
}

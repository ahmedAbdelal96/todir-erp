import React, { type ReactNode } from "react";
import { Breadcrumbs, type BreadcrumbItem } from "../../app/shell/breadcrumbs.tsx";
import { StatusBadge, type StatusVariant } from "../fields/status-badge.tsx";
import clsx from "clsx";

export interface RecordPageProps {
  readonly title: string;
  readonly recordCode?: string | undefined;
  readonly status?: StatusVariant | string | undefined;
  readonly breadcrumbs?: readonly BreadcrumbItem[] | undefined;
  readonly headerActions?: ReactNode | undefined;
  readonly sidebar?: ReactNode | undefined;
  readonly auditInfo?: {
    readonly createdAt?: string | undefined;
    readonly createdBy?: string | undefined;
    readonly updatedAt?: string | undefined;
    readonly updatedBy?: string | undefined;
  } | undefined;
  readonly children: ReactNode;
  readonly className?: string | undefined;
}

export function RecordPage({
  title,
  recordCode,
  status,
  breadcrumbs,
  headerActions,
  sidebar,
  auditInfo,
  children,
  className
}: RecordPageProps): React.JSX.Element {
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
          borderBottom: "1px solid var(--color-border-default)"
        }}
      >
        <div style={{ display: "flex", alignItems: "center", gap: "1rem" }}>
          <div>
            <div style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
              {recordCode && (
                <span
                  style={{
                    fontFamily: "var(--font-mono, monospace)",
                    fontSize: "0.875rem",
                    color: "var(--color-content-muted)"
                  }}
                >
                  {recordCode}
                </span>
              )}
              {status && <StatusBadge status={status} size="small" />}
            </div>
            <h1
              style={{
                margin: "0.25rem 0 0 0",
                fontSize: "1.5rem",
                fontWeight: 700,
                color: "var(--color-content-primary)",
                fontFamily: "var(--font-sans)"
              }}
            >
              {title}
            </h1>
          </div>
        </div>

        {headerActions && (
          <div style={{ display: "flex", alignItems: "center", gap: "0.75rem" }}>
            {headerActions}
          </div>
        )}
      </header>

      <div
        style={{
          display: "grid",
          gridTemplateColumns: sidebar ? "1fr 320px" : "1fr",
          gap: "1.5rem",
          alignItems: "start"
        }}
      >
        <main style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
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
            borderTop: "1px solid var(--color-border-default)",
            display: "flex",
            alignItems: "center",
            justifyContent: "space-between",
            fontSize: "0.75rem",
            color: "var(--color-content-muted)",
            flexWrap: "wrap",
            gap: "0.5rem"
          }}
        >
          {auditInfo.createdAt && (
            <span>
              Created: {auditInfo.createdAt} {auditInfo.createdBy ? `by ${auditInfo.createdBy}` : ""}
            </span>
          )}
          {auditInfo.updatedAt && (
            <span>
              Last modified: {auditInfo.updatedAt} {auditInfo.updatedBy ? `by ${auditInfo.updatedBy}` : ""}
            </span>
          )}
        </footer>
      )}
    </div>
  );
}

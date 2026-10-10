import React, { type ReactNode } from "react";
import { PageHeader, type PageAction } from "./page-header.tsx";
import type { BreadcrumbItem } from "../../app/shell/breadcrumbs.tsx";
import clsx from "clsx";

export interface ListPageProps {
  readonly title: string;
  readonly subtitle?: string | undefined;
  readonly breadcrumbs?: readonly BreadcrumbItem[] | undefined;
  readonly primaryAction?: PageAction | undefined;
  readonly secondaryActions?: ReactNode | undefined;
  readonly filterBar?: ReactNode | undefined;
  readonly pagination?: ReactNode | undefined;
  readonly children: ReactNode;
  readonly className?: string | undefined;
}

export function ListPage({
  title,
  subtitle,
  breadcrumbs,
  primaryAction,
  secondaryActions,
  filterBar,
  pagination,
  children,
  className
}: ListPageProps): React.JSX.Element {
  return (
    <div
      className={clsx("ui-list-page", className)}
      style={{
        display: "flex",
        flexDirection: "column",
        gap: "1.25rem",
        width: "100%",
        boxSizing: "border-box"
      }}
    >
      <PageHeader
        title={title}
        subtitle={subtitle}
        breadcrumbs={breadcrumbs}
        primaryAction={primaryAction}
        secondaryActions={secondaryActions}
      />

      {filterBar && (
        <div style={{ width: "100%" }}>
          {filterBar}
        </div>
      )}

      <main style={{ width: "100%", display: "flex", flexDirection: "column", gap: "1rem" }}>
        {children}
        {pagination && (
          <div style={{ width: "100%", marginTop: "0.25rem" }}>
            {pagination}
          </div>
        )}
      </main>
    </div>
  );
}

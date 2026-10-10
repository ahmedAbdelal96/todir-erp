import React, { type ReactNode } from "react";
import { Breadcrumbs, type BreadcrumbItem } from "../../app/shell/breadcrumbs.tsx";
import { Button } from "../primitives/button.tsx";
import clsx from "clsx";

export interface ListPageProps {
  readonly title: string;
  readonly subtitle?: string | undefined;
  readonly breadcrumbs?: readonly BreadcrumbItem[] | undefined;
  readonly primaryAction?: {
    readonly label: string;
    readonly onClick: () => void;
    readonly icon?: ReactNode | undefined;
    readonly isDisabled?: boolean | undefined;
  } | undefined;
  readonly secondaryActions?: ReactNode | undefined;
  readonly filterBar?: ReactNode | undefined;
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
        padding: "1.5rem",
        height: "100%",
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
          gap: "1rem"
        }}
      >
        <div>
          <h1
            style={{
              margin: 0,
              fontSize: "1.5rem",
              fontWeight: 700,
              color: "var(--color-content-primary)",
              fontFamily: "var(--font-sans)"
            }}
          >
            {title}
          </h1>
          {subtitle && (
            <p
              style={{
                margin: "0.25rem 0 0 0",
                fontSize: "0.875rem",
                color: "var(--color-content-muted)"
              }}
            >
              {subtitle}
            </p>
          )}
        </div>

        <div style={{ display: "flex", alignItems: "center", gap: "0.75rem" }}>
          {secondaryActions}
          {primaryAction && (
            <Button
              variant="primary"
              onClick={primaryAction.onClick}
              isDisabled={primaryAction.isDisabled}
              startIcon={primaryAction.icon}
            >
              {primaryAction.label}
            </Button>
          )}
        </div>
      </header>

      {filterBar && (
        <div
          style={{
            padding: "0.75rem 1rem",
            backgroundColor: "var(--color-surface-card)",
            border: "1px solid var(--color-border-default)",
            borderRadius: "var(--radius-md)",
            display: "flex",
            alignItems: "center",
            gap: "1rem",
            flexWrap: "wrap"
          }}
        >
          {filterBar}
        </div>
      )}

      <main style={{ flex: 1, minHeight: 0, display: "flex", flexDirection: "column" }}>
        {children}
      </main>
    </div>
  );
}

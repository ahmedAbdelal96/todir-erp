import React, { type ReactNode } from "react";
import { Breadcrumbs, type BreadcrumbItem } from "../../app/shell/breadcrumbs.tsx";
import { Button } from "../primitives/button.tsx";
import { PageHelpButton, type PageHelpContent } from "./page-help.tsx";
import clsx from "clsx";

export interface PageAction {
  readonly label: string;
  readonly onClick?: (() => void) | undefined;
  readonly onPress?: (() => void) | undefined;
  readonly icon?: ReactNode | undefined;
  readonly startIcon?: ReactNode | undefined;
  readonly isDisabled?: boolean | undefined;
  readonly isLoading?: boolean | undefined;
  readonly variant?: "primary" | "secondary" | "danger" | "ghost" | undefined;
}

export interface PageHeaderProps {
  readonly title: ReactNode;
  readonly subtitle?: ReactNode | undefined;
  readonly description?: ReactNode | undefined;
  readonly breadcrumbs?: readonly BreadcrumbItem[] | undefined;
  readonly primaryAction?: PageAction | undefined;
  readonly secondaryActions?: ReactNode | undefined;
  readonly status?: ReactNode | undefined;
  readonly help?: PageHelpContent | undefined;
  readonly className?: string | undefined;
  readonly style?: React.CSSProperties | undefined;
}

export function PageHeader({
  title,
  subtitle,
  description,
  breadcrumbs,
  primaryAction,
  secondaryActions,
  status,
  help,
  className,
  style
}: PageHeaderProps): React.JSX.Element {
  const effectiveSubtitle = subtitle ?? description;

  const handlePrimaryPress = () => {
    if (primaryAction?.onPress) {
      primaryAction.onPress();
    } else if (primaryAction?.onClick) {
      primaryAction.onClick();
    }
  };

  return (
    <div
      className={clsx("ui-page-header", className)}
      style={{
        display: "flex",
        flexDirection: "column",
        gap: "0.5rem",
        width: "100%",
        ...style
      }}
    >
      {breadcrumbs && breadcrumbs.length > 0 && (
        <div style={{ marginBottom: "0.25rem" }}>
          <Breadcrumbs items={breadcrumbs} />
        </div>
      )}

      <div
        style={{
          display: "flex",
          alignItems: "center",
          justifyContent: "space-between",
          flexWrap: "wrap",
          gap: "1rem",
          minHeight: "40px"
        }}
      >
        <div style={{ display: "flex", alignItems: "center", gap: "0.75rem", flexWrap: "wrap" }}>
          <div>
            <div style={{ display: "flex", alignItems: "center", gap: "0.625rem" }}>
              <h1
                style={{
                  margin: 0,
                  fontSize: "1.375rem",
                  fontWeight: 700,
                  color: "var(--color-content-primary, #0f172a)",
                  fontFamily: "var(--font-sans, system-ui, sans-serif)",
                  lineHeight: 1.3
                }}
              >
                {title}
              </h1>
              {status}
            </div>

            {effectiveSubtitle && (
              <p
                style={{
                  margin: "0.25rem 0 0 0",
                  fontSize: "0.875rem",
                  color: "var(--color-content-muted, #64748b)",
                  lineHeight: 1.4
                }}
              >
                {effectiveSubtitle}
              </p>
            )}
          </div>
        </div>

        <div
          style={{
            display: "flex",
            alignItems: "center",
            gap: "0.625rem",
            flexWrap: "wrap"
          }}
        >
          {secondaryActions}
          {help && <PageHelpButton content={help} />}
          {primaryAction && (
            <Button
              variant={primaryAction.variant ?? "primary"}
              onPress={handlePrimaryPress}
              isDisabled={primaryAction.isDisabled}
              isLoading={primaryAction.isLoading}
              startIcon={primaryAction.startIcon ?? primaryAction.icon}
            >
              {primaryAction.label}
            </Button>
          )}
        </div>
      </div>
    </div>
  );
}

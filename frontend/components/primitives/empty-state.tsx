import React, { type ReactNode } from "react";
import { InboxIcon } from "./icons.tsx";
import clsx from "clsx";

export interface EmptyStateProps {
  readonly icon?: ReactNode | undefined;
  readonly title: string;
  readonly description?: string | undefined;
  readonly action?: ReactNode | undefined;
  readonly className?: string | undefined;
  readonly style?: React.CSSProperties | undefined;
}

export function EmptyState({
  icon,
  title,
  description,
  action,
  className,
  style
}: EmptyStateProps): React.JSX.Element {
  return (
    <div
      className={clsx("ui-empty-state", className)}
      style={{
        display: "flex",
        flexDirection: "column",
        alignItems: "center",
        justifyContent: "center",
        textAlign: "center",
        padding: "3.5rem 1.5rem",
        backgroundColor: "var(--color-surface-card, #ffffff)",
        border: "1px solid var(--color-border-default, #e2e8f0)",
        borderRadius: "var(--radius-md, 0.5rem)",
        margin: "0 auto",
        width: "100%",
        boxSizing: "border-box",
        ...style
      }}
    >
      <div
        style={{
          width: "48px",
          height: "48px",
          borderRadius: "50%",
          backgroundColor: "var(--color-surface-subtle, #f1f5f9)",
          color: "var(--color-content-muted, #64748b)",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          marginBottom: "1rem"
        }}
      >
        {icon ?? <InboxIcon size={24} />}
      </div>

      <h3
        style={{
          margin: "0 0 0.375rem 0",
          fontSize: "1.0625rem",
          fontWeight: 700,
          color: "var(--color-content-primary, #0f172a)",
          fontFamily: "var(--font-sans)"
        }}
      >
        {title}
      </h3>

      {description && (
        <p
          style={{
            margin: "0 0 1.25rem 0",
            fontSize: "0.875rem",
            color: "var(--color-content-muted, #64748b)",
            maxWidth: "380px",
            lineHeight: 1.5
          }}
        >
          {description}
        </p>
      )}

      {action && (
        <div style={{ marginTop: description ? 0 : "0.5rem" }}>
          {action}
        </div>
      )}
    </div>
  );
}

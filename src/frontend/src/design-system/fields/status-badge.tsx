import React, { type ReactNode } from "react";
import clsx from "clsx";

export type StatusVariant =
  | "success"
  | "warning"
  | "danger"
  | "info"
  | "neutral"
  | "draft"
  | "posted"
  | "archived";

export interface StatusBadgeProps {
  readonly status: StatusVariant | string;
  readonly label?: ReactNode;
  readonly size?: "small" | "medium";
  readonly className?: string;
}

export function StatusBadge({
  status,
  label,
  size = "medium",
  className
}: StatusBadgeProps): React.JSX.Element {
  const normalizedStatus = status.toLowerCase() as StatusVariant;
  const styles = getStatusStyles(normalizedStatus);

  return (
    <span
      className={clsx("ui-status-badge", `ui-status-badge--${normalizedStatus}`, className)}
      style={{
        display: "inline-flex",
        alignItems: "center",
        gap: "0.375rem",
        paddingInline: size === "small" ? "0.375rem" : "0.5rem",
        paddingBlock: size === "small" ? "0.125rem" : "0.25rem",
        borderRadius: "var(--radius-sm, 0.25rem)",
        fontSize: size === "small" ? "0.75rem" : "0.8125rem",
        fontWeight: 600,
        fontFamily: "var(--font-sans)",
        backgroundColor: styles.backgroundColor,
        color: styles.color,
        border: `1px solid ${styles.borderColor}`,
        whiteSpace: "nowrap"
      }}
    >
      <span
        style={{
          width: "0.375rem",
          height: "0.375rem",
          borderRadius: "50%",
          backgroundColor: styles.dotColor
        }}
      />
      <span>{label ?? status}</span>
    </span>
  );
}

function getStatusStyles(status: StatusVariant): {
  backgroundColor: string;
  color: string;
  borderColor: string;
  dotColor: string;
} {
  switch (status) {
    case "success":
    case "posted":
      return {
        backgroundColor: "rgba(34, 197, 94, 0.1)",
        color: "#15803d",
        borderColor: "rgba(34, 197, 94, 0.2)",
        dotColor: "#22c55e"
      };
    case "warning":
    case "draft":
      return {
        backgroundColor: "rgba(245, 158, 11, 0.1)",
        color: "#b45309",
        borderColor: "rgba(245, 158, 11, 0.2)",
        dotColor: "#f59e0b"
      };
    case "danger":
      return {
        backgroundColor: "rgba(239, 68, 68, 0.1)",
        color: "#b91c1c",
        borderColor: "rgba(239, 68, 68, 0.2)",
        dotColor: "#ef4444"
      };
    case "info":
      return {
        backgroundColor: "rgba(59, 130, 246, 0.1)",
        color: "#1d4ed8",
        borderColor: "rgba(59, 130, 246, 0.2)",
        dotColor: "#3b82f6"
      };
    case "archived":
    case "neutral":
    default:
      return {
        backgroundColor: "var(--color-surface-subtle)",
        color: "var(--color-content-muted)",
        borderColor: "var(--color-border-default)",
        dotColor: "var(--color-content-muted)"
      };
  }
}

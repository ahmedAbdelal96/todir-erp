import React, { type ReactNode } from "react";
import clsx from "clsx";

export type StatusIntent = "neutral" | "info" | "success" | "warning" | "danger";
export type StatusVariant = StatusIntent;

export interface StatusBadgeProps {
  readonly intent?: StatusIntent | undefined;
  /**
   * Semantic intent or fallback status text when label is omitted.
   * If status matches a standard StatusIntent, it is used as the default intent.
   */
  readonly status?: StatusIntent | string | undefined;
  readonly label?: ReactNode | undefined;
  readonly icon?: ReactNode | undefined;
  readonly size?: "small" | "medium" | undefined;
  readonly className?: string | undefined;
}

const VALID_INTENTS: ReadonlySet<string> = new Set<StatusIntent>([
  "neutral",
  "info",
  "success",
  "warning",
  "danger"
]);

export function StatusBadge({
  intent,
  status,
  label,
  icon,
  size = "medium",
  className
}: StatusBadgeProps): React.JSX.Element {
  const resolvedIntent: StatusIntent =
    intent ??
    (status && VALID_INTENTS.has(status.toLowerCase())
      ? (status.toLowerCase() as StatusIntent)
      : "neutral");

  const styles = getIntentStyles(resolvedIntent);
  const displayContent = label ?? status ?? "";

  return (
    <span
      className={clsx("ui-status-badge", `ui-status-badge--${resolvedIntent}`, className)}
      style={{
        display: "inline-flex",
        alignItems: "center",
        gap: "0.375rem",
        paddingInline: size === "small" ? "0.625rem" : "0.75rem",
        paddingBlock: size === "small" ? "0.1875rem" : "0.3125rem",
        borderRadius: "var(--radius-full, 9999px)",
        fontSize: size === "small" ? "0.75rem" : "0.8125rem",
        fontWeight: 600,
        fontFamily: "var(--font-sans)",
        backgroundColor: styles.backgroundColor,
        color: styles.color,
        border: `1px solid ${styles.borderColor}`,
        boxShadow: "0 1px 2px 0 rgba(0, 0, 0, 0.04)",
        whiteSpace: "nowrap"
      }}
    >
      {icon ? (
        <span style={{ display: "inline-flex", alignItems: "center" }}>{icon}</span>
      ) : (
        <span
          style={{
            width: "0.4375rem",
            height: "0.4375rem",
            borderRadius: "50%",
            backgroundColor: styles.dotColor,
            boxShadow: `0 0 0 2px ${styles.backgroundColor}, 0 0 4px ${styles.dotColor}`
          }}
        />
      )}
      <span>{displayContent}</span>
    </span>
  );
}

function getIntentStyles(intent: StatusIntent): {
  backgroundColor: string;
  color: string;
  borderColor: string;
  dotColor: string;
} {
  switch (intent) {
    case "success":
      return {
        backgroundColor: "var(--color-status-success-subtle)",
        color: "var(--color-status-success-content)",
        borderColor: "var(--color-status-success-border)",
        dotColor: "var(--color-status-success)"
      };
    case "warning":
      return {
        backgroundColor: "var(--color-status-warning-subtle)",
        color: "var(--color-status-warning-content)",
        borderColor: "var(--color-status-warning-border)",
        dotColor: "var(--color-status-warning)"
      };
    case "danger":
      return {
        backgroundColor: "var(--color-status-danger-subtle)",
        color: "var(--color-status-danger-content)",
        borderColor: "var(--color-status-danger-border)",
        dotColor: "var(--color-status-danger)"
      };
    case "info":
      return {
        backgroundColor: "var(--color-status-info-subtle)",
        color: "var(--color-status-info-content)",
        borderColor: "var(--color-status-info-border)",
        dotColor: "var(--color-status-info)"
      };
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

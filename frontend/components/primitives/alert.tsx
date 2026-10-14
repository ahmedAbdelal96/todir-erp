import React, { type ReactNode } from "react";
import clsx from "clsx";
import { useTranslation } from "react-i18next";

export type AlertVariant = "info" | "success" | "warning" | "danger";

export interface AlertProps {
  readonly variant?: AlertVariant | undefined;
  readonly title?: string | undefined;
  readonly children: ReactNode;
  readonly isDismissible?: boolean | undefined;
  readonly onDismiss?: (() => void) | undefined;
  readonly icon?: ReactNode | undefined;
  readonly className?: string | undefined;
}

export function Alert({
  variant = "info",
  title,
  children,
  isDismissible = false,
  onDismiss,
  icon,
  className
}: AlertProps): React.JSX.Element {
  const { t } = useTranslation("common");
  const defaultIcons: Record<AlertVariant, string> = {
    info: "ℹ️",
    success: "✓",
    warning: "⚠️",
    danger: "✕"
  };

  const borderColors: Record<AlertVariant, string> = {
    info: "var(--color-brand-primary)",
    success: "var(--color-status-success)",
    warning: "var(--color-status-warning)",
    danger: "var(--color-status-danger)"
  };

  const bgColors: Record<AlertVariant, string> = {
    info: "var(--color-surface-primary-subtle, #eff6ff)",
    success: "var(--color-surface-success-subtle, #ecfdf5)",
    warning: "var(--color-surface-warning-subtle, #fef3c7)",
    danger: "var(--color-surface-danger-subtle, #fef2f2)"
  };

  const isCritical = variant === "danger" || variant === "warning";

  return (
    <div
      className={clsx("ui-alert", `ui-alert--${variant}`, className)}
      role={isCritical ? "alert" : "status"}
      aria-live={isCritical ? "assertive" : "polite"}
      style={{
        display: "flex",
        alignItems: "flex-start",
        gap: "0.75rem",
        padding: "0.875rem 1rem",
        borderRadius: "var(--radius-md)",
        backgroundColor: bgColors[variant],
        border: `1px solid var(--color-border-default)`,
        borderInlineStart: `4px solid ${borderColors[variant]}`,
        color: "var(--color-content-default)",
        fontSize: "0.875rem"
      }}
    >
      <span style={{ fontSize: "1.125rem", lineHeight: 1, flexShrink: 0 }} aria-hidden="true">
        {icon ?? defaultIcons[variant]}
      </span>

      <div style={{ flex: 1, minWidth: 0 }}>
        {title && (
          <div style={{ fontWeight: 600, marginBottom: children ? "0.25rem" : 0 }}>
            {title}
          </div>
        )}
        <div style={{ fontSize: "0.8125rem", lineHeight: 1.5 }}>
          {children}
        </div>
      </div>

      {isDismissible && onDismiss && (
        <button
          type="button"
          onClick={onDismiss}
          aria-label={t("components.closeAlert")}
          style={{
            background: "none",
            border: "none",
            padding: "0.25rem",
            cursor: "pointer",
            color: "var(--color-content-muted)",
            fontSize: "0.75rem",
            borderRadius: "var(--radius-sm)",
            lineHeight: 1
          }}
        >
          ✕
        </button>
      )}
    </div>
  );
}

import React from "react";
import clsx from "clsx";
import { useTranslation } from "react-i18next";

export interface ProgressBarProps {
  readonly value?: number | undefined; // 0 to maxValue, undefined for indeterminate
  readonly maxValue?: number | undefined; // default 100
  readonly isIndeterminate?: boolean | undefined;
  readonly label?: string | undefined;
  readonly showValueLabel?: boolean | undefined;
  readonly size?: "comfortable" | "compact" | undefined;
  readonly variant?: "primary" | "success" | "warning" | "danger" | undefined;
  readonly className?: string | undefined;
}

export function ProgressBar({
  value,
  maxValue = 100,
  isIndeterminate = false,
  label,
  showValueLabel = true,
  size = "comfortable",
  variant = "primary",
  className
}: ProgressBarProps): React.JSX.Element {
  const { t } = useTranslation("common");
  const isActualIndeterminate = isIndeterminate || value === undefined;
  const clampedValue = value !== undefined ? Math.max(0, Math.min(value, maxValue)) : 0;
  const percentage = Math.round((clampedValue / maxValue) * 100);

  const height = size === "compact" ? "6px" : "10px";

  const variantColors: Record<string, string> = {
    primary: "var(--color-brand-primary)",
    success: "var(--color-status-success)",
    warning: "var(--color-status-warning)",
    danger: "var(--color-status-danger)"
  };

  return (
    <div
      className={clsx("ui-progress-bar", className)}
      style={{ display: "flex", flexDirection: "column", gap: "0.375rem", width: "100%" }}
    >
      {(label || (!isActualIndeterminate && showValueLabel)) && (
        <div
          style={{
            display: "flex",
            justifyContent: "space-between",
            fontSize: size === "compact" ? "0.75rem" : "0.8125rem",
            color: "var(--color-content-muted)"
          }}
        >
          {label && <span>{label}</span>}
          {!isActualIndeterminate && showValueLabel && (
            <span style={{ fontWeight: 600, color: "var(--color-content-default)" }}>
              {percentage}%
            </span>
          )}
        </div>
      )}

      <div
        role="progressbar"
        aria-valuenow={isActualIndeterminate ? undefined : clampedValue}
        aria-valuemin={0}
        aria-valuemax={maxValue}
        aria-label={label ?? t("components.progress")}
        style={{
          width: "100%",
          height,
          backgroundColor: "var(--color-surface-subtle)",
          borderRadius: "var(--radius-full, 9999px)",
          overflow: "hidden",
          position: "relative"
        }}
      >
        <div
          style={{
            height: "100%",
            width: isActualIndeterminate ? "40%" : `${percentage}%`,
            backgroundColor: variantColors[variant],
            borderRadius: "inherit",
            transition: isActualIndeterminate ? "none" : "width 0.3s ease",
            animation: isActualIndeterminate ? "ui-progress-indeterminate 1.5s infinite ease-in-out" : "none"
          }}
        />
      </div>
    </div>
  );
}

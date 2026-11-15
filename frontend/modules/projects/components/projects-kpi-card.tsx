import React from "react";
import { Surface } from "../../../components/pages/surface.tsx";

export interface ProjectsKpiCardProps {
  readonly title: string;
  readonly value: React.ReactNode;
  readonly subtitle?: string | undefined;
  readonly tone?: "default" | "primary" | "success" | "warning" | "danger" | undefined;
  readonly icon?: React.ReactNode | undefined;
  readonly onClick?: (() => void) | undefined;
  readonly testId?: string | undefined;
  readonly "data-testid"?: string | undefined;
}

export function ProjectsKpiCard({
  title,
  value,
  subtitle,
  tone = "default",
  icon,
  onClick,
  testId,
  "data-testid": dataTestId
}: ProjectsKpiCardProps): React.JSX.Element {
  let borderColor = "var(--color-border-default, #cbd5e1)";
  let valueColor = "var(--color-content-primary, #0f172a)";
  let iconBg = "var(--color-surface-subtle, #f8fafc)";
  let iconColor = "var(--color-content-muted, #64748b)";

  if (tone === "primary") {
    valueColor = "var(--color-brand-primary, #102a43)";
    borderColor = "var(--color-brand-primary, #102a43)";
    iconBg = "rgba(16, 42, 67, 0.08)";
    iconColor = "var(--color-brand-primary, #102a43)";
  } else if (tone === "success") {
    valueColor = "var(--color-status-success, #059669)";
    borderColor = "var(--color-status-success, #059669)";
    iconBg = "#ecfdf5";
    iconColor = "#059669";
  } else if (tone === "warning") {
    valueColor = "var(--color-status-warning, #d97706)";
    borderColor = "var(--color-status-warning, #d97706)";
    iconBg = "#fffbeb";
    iconColor = "#d97706";
  } else if (tone === "danger") {
    valueColor = "var(--color-status-danger, #dc2626)";
    borderColor = "var(--color-status-danger, #dc2626)";
    iconBg = "#fef2f2";
    iconColor = "#dc2626";
  }

  return (
    <div
      data-testid={dataTestId || testId}
      onClick={onClick}
      style={{
        display: "flex",
        flexDirection: "column",
        gap: "0.5rem",
        padding: "1rem",
        borderRadius: "var(--radius-lg, 12px)",
        border: `1px solid ${borderColor}`,
        background: "var(--color-surface-card, #ffffff)",
        boxShadow: "0 1px 3px rgba(0,0,0,0.06), 0 1px 2px rgba(0,0,0,0.04)",
        cursor: onClick ? "pointer" : "default",
        transition: "transform 0.15s ease, box-shadow 0.15s ease"
      }}
    >
      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: "0.5rem" }}>
        <span
          style={{
            fontSize: "0.8125rem",
            fontWeight: 600,
            color: "var(--color-content-muted, #64748b)"
          }}
        >
          {title}
        </span>
        {icon && (
          <div
            style={{
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              width: "2rem",
              height: "2rem",
              borderRadius: "var(--radius-md, 8px)",
              background: iconBg,
              color: iconColor,
              flexShrink: 0
            }}
          >
            {icon}
          </div>
        )}
      </div>

      <div
        style={{
          fontSize: "1.375rem",
          fontWeight: 700,
          color: valueColor,
          fontVariantNumeric: "tabular-nums",
          lineHeight: 1.2
        }}
      >
        {value}
      </div>

      {subtitle && (
        <span
          style={{
            fontSize: "0.75rem",
            color: "var(--color-content-subtle, #94a3b8)"
          }}
        >
          {subtitle}
        </span>
      )}
    </div>
  );
}

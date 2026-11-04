import React from "react";
import { Surface } from "../../../components/pages/surface.tsx";

export interface PayrollKpiCardProps {
  readonly title: string;
  readonly value: React.ReactNode;
  readonly subtitle?: string | undefined;
  readonly tone?: "default" | "primary" | "success" | "warning" | "danger" | undefined;
  readonly icon?: React.ReactNode;
}

export function PayrollKpiCard({ title, value, subtitle, tone = "default", icon }: PayrollKpiCardProps): React.JSX.Element {
  let borderColor = "var(--color-border-default)";
  let valueColor = "var(--color-content-primary)";
  let iconBg = "var(--color-surface-subtle)";
  let iconColor = "var(--color-content-muted)";

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
    <Surface
      style={{
        flex: "1 1 200px",
        padding: "1.25rem",
        display: "flex",
        flexDirection: "column",
        gap: "0.5rem",
        borderTop: `3px solid ${borderColor}`,
        borderRadius: "var(--radius-lg, 0.75rem)",
        boxShadow: "var(--shadow-sm)"
      }}
    >
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
        <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)", fontWeight: 600 }}>
          {title}
        </span>
        {icon && (
          <div
            style={{
              width: "32px",
              height: "32px",
              borderRadius: "var(--radius-md, 0.5rem)",
              backgroundColor: iconBg,
              color: iconColor,
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              border: `1px solid ${borderColor}`
            }}
          >
            {icon}
          </div>
        )}
      </div>
      <span style={{ fontSize: "1.75rem", fontWeight: 700, color: valueColor, lineHeight: 1.2 }}>
        {value}
      </span>
      {subtitle && (
        <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
          {subtitle}
        </span>
      )}
    </Surface>
  );
}

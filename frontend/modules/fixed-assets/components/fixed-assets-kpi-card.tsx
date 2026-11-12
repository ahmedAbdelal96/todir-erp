import React from "react";
import { Surface } from "../../../components/pages/surface.tsx";

export interface FixedAssetsKpiCardProps {
  readonly title: string;
  readonly value: React.ReactNode;
  readonly subtitle?: string;
  readonly tone?: "default" | "primary" | "success" | "warning" | "danger";
  readonly icon?: React.ReactNode;
  readonly onClick?: () => void;
}

export function FixedAssetsKpiCard({
  title,
  value,
  subtitle,
  tone = "default",
  icon,
  onClick
}: FixedAssetsKpiCardProps): React.JSX.Element {
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
      style={{
        flex: "1 1 200px",
        cursor: onClick ? "pointer" : "default"
      }}
      onClick={onClick}
    >
      <Surface
        style={{
          padding: "1.25rem",
          display: "flex",
          flexDirection: "column",
          gap: "0.5rem",
          borderTop: `3px solid ${borderColor}`,
          borderRadius: "var(--radius-lg, 0.75rem)",
          boxShadow: "var(--shadow-sm, 0 1px 3px 0 rgba(15, 23, 42, 0.08))",
          backgroundColor: "var(--color-surface-card, #ffffff)",
          height: "100%",
          boxSizing: "border-box"
        }}
      >
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
          <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted, #64748b)", fontWeight: 600 }}>
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
                flexShrink: 0
              }}
            >
              {icon}
            </div>
          )}
        </div>
        <div style={{ fontSize: "1.5rem", fontWeight: 700, color: valueColor, lineHeight: 1.2 }}>
          {value}
        </div>
        {subtitle && (
          <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted, #64748b)" }}>
            {subtitle}
          </span>
        )}
      </Surface>
    </div>
  );
}

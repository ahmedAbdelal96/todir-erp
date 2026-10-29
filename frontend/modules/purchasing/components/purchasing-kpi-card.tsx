import React from "react";

export interface PurchasingKpiCardProps {
  readonly title: string;
  readonly value: string | number;
  readonly subtitle?: string | undefined;
  readonly trend?: string | undefined;
  readonly tone?: "default" | "success" | "warning" | "danger" | "primary" | undefined;
}

export function PurchasingKpiCard({
  title,
  value,
  subtitle,
  trend,
  tone = "default"
}: PurchasingKpiCardProps): React.JSX.Element {
  let valueColor = "var(--color-content-primary, #0f172a)";
  let borderColor = "var(--color-border-default, #cbd5e1)";

  switch (tone) {
    case "success":
      valueColor = "var(--color-status-success, #16a34a)";
      borderColor = "#86efac";
      break;
    case "warning":
      valueColor = "var(--color-status-warning, #d97706)";
      borderColor = "#fde68a";
      break;
    case "danger":
      valueColor = "var(--color-status-danger, #dc2626)";
      borderColor = "#fca5a5";
      break;
    case "primary":
      valueColor = "var(--color-brand-primary, #1d4ed8)";
      borderColor = "#93c5fd";
      break;
  }

  return (
    <div
      style={{
        backgroundColor: "var(--color-surface-card, #ffffff)",
        border: `1px solid ${borderColor}`,
        borderRadius: "var(--radius-lg, 0.75rem)",
        padding: "1.25rem 1.5rem",
        display: "flex",
        flexDirection: "column",
        gap: "0.5rem",
        boxShadow: "0 1px 3px 0 rgba(0, 0, 0, 0.05), 0 1px 2px -1px rgba(0, 0, 0, 0.05)",
        flex: "1 1 200px"
      }}
    >
      <span style={{ fontSize: "0.8125rem", fontWeight: 600, color: "var(--color-content-muted, #64748b)" }}>
        {title}
      </span>
      <div style={{ display: "flex", alignItems: "baseline", justifyContent: "space-between", gap: "0.5rem" }}>
        <span style={{ fontSize: "1.75rem", fontWeight: 700, color: valueColor, letterSpacing: "-0.025em" }}>
          {value}
        </span>
        {trend && (
          <span style={{ fontSize: "0.75rem", fontWeight: 600, color: "var(--color-content-muted, #64748b)" }}>
            {trend}
          </span>
        )}
      </div>
      {subtitle && (
        <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted, #64748b)" }}>
          {subtitle}
        </span>
      )}
    </div>
  );
}

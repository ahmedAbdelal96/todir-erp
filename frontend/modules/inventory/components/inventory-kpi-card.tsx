import React from "react";

export interface InventoryKpiCardProps {
  readonly title: string;
  readonly value: React.ReactNode;
  readonly subtitle?: string;
  readonly tone?: "default" | "success" | "warning" | "danger" | "info";
  readonly icon?: React.ReactNode;
}

export function InventoryKpiCard({
  title,
  value,
  subtitle,
  tone = "default",
  icon
}: InventoryKpiCardProps): React.JSX.Element {
  let accentColor = "var(--color-brand-primary, #102a43)";
  if (tone === "success") accentColor = "var(--color-status-success, #16a34a)";
  if (tone === "warning") accentColor = "var(--color-status-warning, #d97706)";
  if (tone === "danger") accentColor = "var(--color-status-danger, #dc2626)";
  if (tone === "info") accentColor = "var(--color-status-info, #2563eb)";

  return (
    <div
      style={{
        flex: "1 1 200px",
        minWidth: "180px",
        padding: "1rem 1.25rem",
        backgroundColor: "var(--color-surface-card, #ffffff)",
        border: "1px solid var(--color-border-default, #cbd5e1)",
        borderTop: `3px solid ${accentColor}`,
        borderRadius: "var(--radius-md, 0.5rem)",
        boxShadow: "0 1px 3px rgba(0, 0, 0, 0.05), 0 1px 2px rgba(0, 0, 0, 0.03)",
        display: "flex",
        flexDirection: "column",
        gap: "0.375rem"
      }}
    >
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
        <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted, #64748b)", fontWeight: 500 }}>
          {title}
        </span>
        {icon && <span style={{ color: accentColor }}>{icon}</span>}
      </div>
      <div style={{ fontSize: "1.5rem", fontWeight: 700, color: "var(--color-content-primary, #0f172a)", lineHeight: 1.2 }}>
        {value}
      </div>
      {subtitle && (
        <span style={{ fontSize: "0.75rem", color: "var(--color-content-subtle, #94a3b8)" }}>
          {subtitle}
        </span>
      )}
    </div>
  );
}

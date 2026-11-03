import React from "react";
import { Surface } from "../../../components/pages/surface.tsx";

export interface HrKpiCardProps {
  readonly title: string;
  readonly value: string | number;
  readonly subtitle?: string | undefined;
  readonly tone?: "default" | "primary" | "success" | "warning" | "danger" | undefined;
}

export function HrKpiCard({ title, value, subtitle, tone = "default" }: HrKpiCardProps): React.JSX.Element {
  let borderColor = "var(--color-border-default)";
  let valueColor = "var(--color-content-primary)";

  if (tone === "primary") {
    valueColor = "var(--color-brand-primary)";
  } else if (tone === "success") {
    valueColor = "var(--color-status-success)";
  } else if (tone === "warning") {
    valueColor = "var(--color-status-warning)";
    borderColor = "var(--color-status-warning)";
  } else if (tone === "danger") {
    valueColor = "var(--color-status-danger)";
    borderColor = "var(--color-status-danger)";
  }

  return (
    <Surface
      style={{
        flex: "1 1 200px",
        padding: "1.25rem",
        display: "flex",
        flexDirection: "column",
        gap: "0.375rem",
        borderTop: `3px solid ${borderColor}`
      }}
    >
      <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)", fontWeight: 500 }}>
        {title}
      </span>
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

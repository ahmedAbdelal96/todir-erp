import React, { type ReactNode } from "react";
import { Surface } from "../../../components/pages/surface.tsx";
import { BidiText } from "../../../components/primitives/bidi-text.tsx";

export interface HrKpiCardProps {
  readonly title: string;
  readonly value: ReactNode;
  readonly subtitle?: string | undefined;
  readonly tone?: "default" | "primary" | "success" | "warning" | "danger" | undefined;
  readonly testId?: string | undefined;
}

export function HrKpiCard({ title, value, subtitle, tone = "default", testId }: HrKpiCardProps): React.JSX.Element {
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
    <div {...(testId ? { "data-testid": testId } : {})} style={{ minWidth: 0 }}>
      <Surface
        padding="compact"
        style={{
          minHeight: "5.75rem",
          borderTop: `3px solid ${borderColor}`
        }}
      >
      <div style={{ display: "flex", flexDirection: "column", gap: "0.35rem", height: "100%" }}>
        <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", fontWeight: 600, lineHeight: 1.35 }}>
          {title}
        </span>
        <strong style={{ fontSize: "1.5rem", fontWeight: 750, color: valueColor, lineHeight: 1.05 }}>
          <BidiText direction="ltr">{value}</BidiText>
        </strong>
        {subtitle && (
          <span style={{ marginTop: "auto", fontSize: "0.6875rem", color: "var(--color-content-muted)", lineHeight: 1.35 }}>
            {subtitle}
          </span>
        )}
      </div>
      </Surface>
    </div>
  );
}

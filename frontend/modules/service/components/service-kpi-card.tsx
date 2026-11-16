import React from "react";

type ServiceKpiTone = "default" | "primary" | "success" | "warning" | "danger";

type ServiceKpiCardProps = Readonly<{
  title?: string | undefined;
  label?: string | undefined;
  value: string | number;
  subtitle?: string | undefined;
  trend?: string | undefined;
  trendDirection?: string | undefined;
  badgeText?: string | undefined;
  tone?: ServiceKpiTone | undefined;
  icon?: React.ReactNode | undefined;
  testId?: string | undefined;
}>;

const toneColors: Record<ServiceKpiTone, { accent: string; bgBadge: string; textBadge: string }> = {
  default: { accent: "#475569", bgBadge: "#f8fafc", textBadge: "#334155" },
  primary: { accent: "#1d4ed8", bgBadge: "#eff6ff", textBadge: "#1e40af" },
  success: { accent: "#15803d", bgBadge: "#f0fdf4", textBadge: "#166534" },
  warning: { accent: "#b45309", bgBadge: "#fffbeb", textBadge: "#92400e" },
  danger: { accent: "#b91c1c", bgBadge: "#fef2f2", textBadge: "#991b1b" }
};

export function ServiceKpiCard({
  title,
  label,
  value,
  subtitle,
  badgeText,
  tone = "default",
  icon,
  testId
}: ServiceKpiCardProps): React.JSX.Element {
  const displayTitle = title ?? label ?? "";
  const current = toneColors[tone];

  return (
    <article
      data-testid={testId ?? `service-kpi-${displayTitle.toLowerCase().replace(/\s+/g, "-")}`}
      style={{
        backgroundColor: "#ffffff",
        border: "1px solid #cbd5e1",
        borderInlineStart: `4px solid ${current.accent}`,
        borderRadius: "0.5rem",
        padding: "1rem 1.25rem",
        boxShadow: "0 1px 3px 0 rgba(0, 0, 0, 0.08), 0 1px 2px -1px rgba(0, 0, 0, 0.05)",
        display: "flex",
        flexDirection: "column",
        justifyContent: "space-between",
        gap: "0.5rem",
        minWidth: "180px",
        flex: "1 1 calc(20% - 1rem)"
      }}
    >
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
        <span style={{ fontSize: "0.8125rem", fontWeight: 600, color: "#64748b" }}>{displayTitle}</span>
        {icon && (
          <span
            style={{
              padding: "0.35rem",
              borderRadius: "0.375rem",
              backgroundColor: current.bgBadge,
              color: current.textBadge,
              display: "inline-flex"
            }}
          >
            {icon}
          </span>
        )}
      </div>

      <div style={{ display: "flex", alignItems: "baseline", gap: "0.5rem" }}>
        <span
          style={{
            fontSize: "1.75rem",
            fontWeight: 700,
            color: "#0f172a",
            fontVariantNumeric: "tabular-nums",
            lineHeight: 1.1
          }}
        >
          {value}
        </span>
      </div>

      {subtitle && (
        <span style={{ fontSize: "0.75rem", color: "#64748b", marginBlockStart: "0.125rem" }}>
          {subtitle}
        </span>
      )}
    </article>
  );
}

import React from "react";
import { useTranslation } from "react-i18next";
import { BdiText } from "../../../components/formatting/bidi-text.tsx";

export interface MetricItem {
  readonly id: string;
  readonly label: string;
  readonly value: string | number;
  readonly helperText?: string;
  readonly intent?: "neutral" | "info" | "success" | "warning" | "danger";
}

export interface ManufacturingMetricRibbonProps {
  readonly metrics: readonly MetricItem[];
}

export function ManufacturingMetricRibbon({ metrics }: ManufacturingMetricRibbonProps): React.JSX.Element {
  const { t } = useTranslation("manufacturing");
  const getIntentColors = (intent: MetricItem["intent"] = "neutral") => {
    switch (intent) {
      case "danger":
        return { border: "#ef4444", text: "#b91c1c", bg: "#fef2f2" };
      case "warning":
        return { border: "#f59e0b", text: "#b45309", bg: "#fffbeb" };
      case "success":
        return { border: "#10b981", text: "#047857", bg: "#ecfdf5" };
      case "info":
        return { border: "#3b82f6", text: "#1d4ed8", bg: "#eff6ff" };
      case "neutral":
      default:
        return { border: "#cbd5e1", text: "#102a43", bg: "#ffffff" };
    }
  };

  return (
    <section
      aria-label={t("common.metricRibbonAria")}
      style={{
        display: "grid",
        gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))",
        gap: "0.75rem",
        marginBottom: "1rem"
      }}
    >
      {metrics.map((m) => {
        const colors = getIntentColors(m.intent);
        return (
          <div
            key={m.id}
            data-testid={`metric-card-${m.id}`}
            style={{
              padding: "0.625rem 0.875rem",
              background: colors.bg,
              border: `1px solid ${colors.border}`,
              borderRadius: "var(--radius-md, 0.5rem)",
              boxShadow: "0 1px 3px rgba(0, 0, 0, 0.05)",
              display: "flex",
              flexDirection: "column",
              gap: "0.25rem"
            }}
          >
            <span
              style={{
                fontSize: "0.75rem",
                fontWeight: 600,
                color: "var(--color-content-muted, #486581)",
                textTransform: "uppercase",
                letterSpacing: "0.025em"
              }}
            >
              {m.label}
            </span>
            <div
              style={{
                fontSize: "1.375rem",
                fontWeight: 700,
                color: colors.text,
                lineHeight: 1.2
              }}
            >
              <BdiText>{String(m.value)}</BdiText>
            </div>
            {m.helperText ? (
              <span
                style={{
                  fontSize: "0.6875rem",
                  color: "var(--color-content-muted, #627d98)"
                }}
              >
                {m.helperText}
              </span>
            ) : null}
          </div>
        );
      })}
    </section>
  );
}

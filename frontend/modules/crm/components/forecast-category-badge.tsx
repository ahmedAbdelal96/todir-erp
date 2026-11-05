import React from "react";
import { useTranslation } from "react-i18next";
import type { ForecastCategory } from "../types.ts";

export interface ForecastCategoryBadgeProps {
  readonly category: ForecastCategory;
}

export function ForecastCategoryBadge({ category }: ForecastCategoryBadgeProps): React.JSX.Element {
  const { t } = useTranslation("crm");

  const getStyle = (): { bg: string; text: string; border: string } => {
    switch (category) {
      case "Commit":
        return {
          bg: "var(--color-status-success-subtle, #ecfdf5)",
          text: "var(--color-status-success, #059669)",
          border: "#a7f3d0"
        };
      case "BestCase":
        return {
          bg: "var(--color-status-info-subtle, #eff6ff)",
          text: "var(--color-status-info, #2563eb)",
          border: "#bfdbfe"
        };
      case "Closed":
        return {
          bg: "var(--color-surface-subtle, #f8fafc)",
          text: "var(--color-content-muted, #64748b)",
          border: "var(--color-border-default, #cbd5e1)"
        };
      case "Pipeline":
      default:
        return {
          bg: "var(--color-surface-card, #ffffff)",
          text: "var(--color-content-primary, #0f172a)",
          border: "var(--color-border-default, #cbd5e1)"
        };
    }
  };

  const style = getStyle();

  return (
    <span
      style={{
        display: "inline-flex",
        alignItems: "center",
        padding: "0.125rem 0.5rem",
        borderRadius: "var(--radius-sm, 0.25rem)",
        fontSize: "0.75rem",
        fontWeight: 600,
        backgroundColor: style.bg,
        color: style.text,
        border: `1px solid ${style.border}`
      }}
    >
      {t(`forecastCategory.${category === "BestCase" ? "bestCase" : category.toLowerCase()}` as any, category)}
    </span>
  );
}

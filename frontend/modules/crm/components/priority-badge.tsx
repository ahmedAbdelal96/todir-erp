import React from "react";
import { useTranslation } from "react-i18next";
import type { LeadPriority } from "../types.ts";

export interface PriorityBadgeProps {
  readonly priority: LeadPriority;
}

export function PriorityBadge({ priority }: PriorityBadgeProps): React.JSX.Element {
  const { t } = useTranslation("crm");

  const getStyle = (): { bg: string; text: string; border: string } => {
    switch (priority) {
      case "High":
        return {
          bg: "var(--color-status-danger-subtle, #fef2f2)",
          text: "var(--color-status-danger, #dc2626)",
          border: "#fecaca"
        };
      case "Normal":
        return {
          bg: "var(--color-surface-subtle, #f8fafc)",
          text: "var(--color-content-primary, #0f172a)",
          border: "var(--color-border-default, #cbd5e1)"
        };
      case "Low":
      default:
        return {
          bg: "var(--color-surface-page, #f0f4f8)",
          text: "var(--color-content-muted, #64748b)",
          border: "#e2e8f0"
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
      {t(`priority.${priority.toLowerCase()}` as any, priority)}
    </span>
  );
}

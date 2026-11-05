import React from "react";
import { useTranslation } from "react-i18next";
import type { DealHealthStatus } from "../types.ts";

export interface DealHealthBadgeProps {
  readonly status: DealHealthStatus;
  readonly score?: number | undefined;
}

export function DealHealthBadge({ status, score }: DealHealthBadgeProps): React.JSX.Element {
  const { t } = useTranslation("crm");

  const getStyle = (): { bg: string; text: string; border: string } => {
    switch (status) {
      case "Good":
        return {
          bg: "var(--color-status-success-subtle, #ecfdf5)",
          text: "var(--color-status-success, #059669)",
          border: "#a7f3d0"
        };
      case "Fair":
        return {
          bg: "var(--color-status-info-subtle, #eff6ff)",
          text: "var(--color-status-info, #2563eb)",
          border: "#bfdbfe"
        };
      case "AtRisk":
        return {
          bg: "var(--color-status-warning-subtle, #fffbeb)",
          text: "var(--color-status-warning, #d97706)",
          border: "#fde68a"
        };
      case "Stale":
      default:
        return {
          bg: "var(--color-status-danger-subtle, #fef2f2)",
          text: "var(--color-status-danger, #dc2626)",
          border: "#fecaca"
        };
    }
  };

  const style = getStyle();

  return (
    <span
      style={{
        display: "inline-flex",
        alignItems: "center",
        gap: "0.25rem",
        padding: "0.1875rem 0.5rem",
        borderRadius: "9999px",
        fontSize: "0.75rem",
        fontWeight: 600,
        backgroundColor: style.bg,
        color: style.text,
        border: `1px solid ${style.border}`
      }}
    >
      <span
        style={{
          width: "6px",
          height: "6px",
          borderRadius: "50%",
          backgroundColor: style.text
        }}
      />
      {t(`dealHealth.${status === "AtRisk" ? "atRisk" : status.toLowerCase()}` as any, status)}
      {score !== undefined && (
        <span style={{ opacity: 0.85, marginInlineStart: "0.125rem" }}>({score})</span>
      )}
    </span>
  );
}

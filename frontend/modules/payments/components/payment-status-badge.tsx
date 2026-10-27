import React from "react";
import { useTranslation } from "react-i18next";
import type { PaymentBusinessState } from "../types.ts";

interface ToneStyle {
  readonly bg: string;
  readonly text: string;
  readonly border: string;
}

const defaultToneStyle: ToneStyle = { bg: "#f1f5f9", text: "#475569", border: "#cbd5e1" };

const toneStyles: Record<string, ToneStyle> = {
  success: { bg: "#ecfdf5", text: "#065f46", border: "#a7f3d0" },
  warning: { bg: "#fffbeb", text: "#92400e", border: "#fde68a" },
  info: { bg: "#eff6ff", text: "#1e40af", border: "#bfdbfe" },
  danger: { bg: "#fef2f2", text: "#991b1b", border: "#fecaca" },
  neutral: defaultToneStyle
};

export interface PaymentStatusBadgeProps {
  readonly state: PaymentBusinessState | string;
}

export function PaymentStatusBadge({ state }: PaymentStatusBadgeProps): React.JSX.Element {
  const { t } = useTranslation("payments");

  let tone = "neutral";
  let labelKey = "state.unknown";

  switch (state) {
    case "Completed":
      tone = "success";
      labelKey = "state.completed";
      break;
    case "Pending":
      tone = "warning";
      labelKey = "state.pending";
      break;
    case "Draft":
      tone = "info";
      labelKey = "state.draft";
      break;
    case "Failed":
      tone = "danger";
      labelKey = "state.failed";
      break;
    case "Cancelled":
      tone = "neutral";
      labelKey = "state.cancelled";
      break;
    case "Reversed":
      tone = "danger";
      labelKey = "state.reversed";
      break;
    default:
      tone = "neutral";
      labelKey = "state.unknown";
      break;
  }

  const current = toneStyles[tone] ?? defaultToneStyle;

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
        backgroundColor: current.bg,
        color: current.text,
        border: `1px solid ${current.border}`
      }}
    >
      <span
        style={{
          width: "6px",
          height: "6px",
          borderRadius: "50%",
          backgroundColor: current.text
        }}
      />
      {(t as any)(labelKey)}
    </span>
  );
}

export interface ApplicationStatusBadgeProps {
  readonly status: "Unapplied" | "PartiallyApplied" | "Applied" | string;
}

export function ApplicationStatusBadge({ status }: ApplicationStatusBadgeProps): React.JSX.Element {
  const { t } = useTranslation("payments");

  let tone = "neutral";
  let labelKey = "application.unapplied";

  switch (status) {
    case "Applied":
      tone = "success";
      labelKey = "application.applied";
      break;
    case "PartiallyApplied":
      tone = "warning";
      labelKey = "application.partiallyapplied";
      break;
    case "Unapplied":
    default:
      tone = "info";
      labelKey = "application.unapplied";
      break;
  }

  const current = toneStyles[tone] ?? defaultToneStyle;

  return (
    <span
      style={{
        display: "inline-flex",
        alignItems: "center",
        padding: "0.125rem 0.4375rem",
        borderRadius: "4px",
        fontSize: "0.6875rem",
        fontWeight: 600,
        backgroundColor: current.bg,
        color: current.text,
        border: `1px solid ${current.border}`
      }}
    >
      {(t as any)(labelKey)}
    </span>
  );
}

export interface ProviderStatusBadgeProps {
  readonly state?: string | null | undefined;
}

export function ProviderStatusBadge({ state }: ProviderStatusBadgeProps): React.JSX.Element {
  const { t } = useTranslation("payments");
  if (!state) return <span>-</span>;

  let tone = "neutral";
  let labelKey = "provider.unknown";

  switch (state) {
    case "Succeeded":
    case "Completed":
    case "Captured":
      tone = "success";
      labelKey = "provider.succeeded";
      break;
    case "Pending":
    case "Processing":
      tone = "info";
      labelKey = "provider.processing";
      break;
    case "RequiresAction":
      tone = "warning";
      labelKey = "provider.requiresaction";
      break;
    case "Failed":
    case "Declined":
      tone = "danger";
      labelKey = "provider.failed";
      break;
    case "Cancelled":
      tone = "neutral";
      labelKey = "provider.cancelled";
      break;
    default:
      tone = "neutral";
      labelKey = "provider.checking";
      break;
  }

  const current = toneStyles[tone] ?? defaultToneStyle;

  return (
    <span
      style={{
        display: "inline-flex",
        alignItems: "center",
        padding: "0.125rem 0.4375rem",
        borderRadius: "4px",
        fontSize: "0.6875rem",
        fontWeight: 600,
        backgroundColor: current.bg,
        color: current.text,
        border: `1px solid ${current.border}`
      }}
    >
      {(t as any)(labelKey)}
    </span>
  );
}

export interface ReconciliationStatusBadgeProps {
  readonly status?: "Unmatched" | "Matched" | "Reconciled" | string | null | undefined;
}

export function ReconciliationStatusBadge({ status }: ReconciliationStatusBadgeProps): React.JSX.Element {
  const { t } = useTranslation("payments");
  const currentStatus = status || "Unmatched";

  let tone = "neutral";
  let labelKey = "reconciliation.unmatched";

  switch (currentStatus) {
    case "Reconciled":
      tone = "success";
      labelKey = "reconciliation.reconciled";
      break;
    case "Matched":
      tone = "info";
      labelKey = "reconciliation.matched";
      break;
    case "Unmatched":
    default:
      tone = "neutral";
      labelKey = "reconciliation.unmatched";
      break;
  }

  const current = toneStyles[tone] ?? defaultToneStyle;

  return (
    <span
      style={{
        display: "inline-flex",
        alignItems: "center",
        padding: "0.125rem 0.375rem",
        borderRadius: "4px",
        fontSize: "0.6875rem",
        backgroundColor: current.bg,
        color: current.text,
        border: `1px solid ${current.border}`
      }}
    >
      {(t as any)(labelKey)}
    </span>
  );
}

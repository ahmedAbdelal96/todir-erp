import React from "react";
import { useTranslation } from "react-i18next";
import { StatusBadge, type StatusIntent } from "../../../components/fields/status-badge.tsx";

const STATUS_INTENTS: Record<string, StatusIntent> = {
  Draft: "neutral",
  Open: "info",
  Collecting: "info",
  Review: "warning",
  Submitted: "info",
  Returned: "warning",
  Approved: "success",
  Published: "success",
  Closed: "neutral",
  Archived: "neutral",
  Locked: "neutral",
  Active: "success",
  Retired: "neutral",
  Shared: "info",
  Private: "neutral",
  Pending: "warning",
  Running: "info",
  Completed: "success",
  Failed: "danger",
  Rejected: "danger",
  Warn: "warning",
  Block: "danger",
  RequireApproval: "warning",
  Allow: "success",
  Allowed: "success",
  NoControl: "neutral",
  Prioritized: "info",
  Favorable: "success",
  Unfavorable: "danger"
};

export interface PlanningStatusBadgeProps {
  readonly status: string;
}

export function PlanningStatusBadge({ status }: PlanningStatusBadgeProps): React.JSX.Element {
  const { t } = useTranslation("planning");
  const intent = STATUS_INTENTS[status] ?? "neutral";
  const label = status === "Favorable"
    ? t("kpi.favorable")
    : status === "Unfavorable"
      ? t("kpi.unfavorable")
      : t(`statuses.${status}`, status);

  return <StatusBadge label={label} intent={intent} />;
}

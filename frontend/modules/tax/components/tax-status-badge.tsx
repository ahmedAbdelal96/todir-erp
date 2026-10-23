import React from "react";
import { useTranslation } from "react-i18next";
import { StatusBadge, type StatusIntent } from "../../../components/fields/status-badge.tsx";

export interface TaxStatusBadgeProps {
  readonly status: string;
  readonly type?: "capability" | "health" | "ruleSet" | "registration" | "review";
}

export function TaxStatusBadge({ status, type = "health" }: TaxStatusBadgeProps): React.JSX.Element {
  const { t } = useTranslation("tax");

  let intent: StatusIntent = "neutral";
  let labelKey = `status.${status.toLowerCase()}`;

  switch (type) {
    case "capability":
      if (status === "Enabled") {
        intent = "success";
        labelKey = "capability.enabled";
      } else {
        intent = "neutral";
        labelKey = "capability.disabled";
      }
      break;

    case "health":
      if (status === "Healthy" || status === "Ready") {
        intent = "success";
        labelKey = "health.healthy";
      } else if (status === "Warning" || status === "NeedsAttention") {
        intent = "warning";
        labelKey = "health.warning";
      } else if (status === "Blocked") {
        intent = "danger";
        labelKey = "health.blocked";
      }
      break;

    case "ruleSet":
      if (status === "Active") {
        intent = "success";
        labelKey = "ruleSetStatus.active";
      } else if (status === "Draft") {
        intent = "info";
        labelKey = "ruleSetStatus.draft";
      } else if (status === "Approved") {
        intent = "info";
        labelKey = "ruleSetStatus.approved";
      } else if (status === "Scheduled") {
        intent = "warning";
        labelKey = "ruleSetStatus.scheduled";
      } else if (status === "Superseded" || status === "Retired") {
        intent = "neutral";
        labelKey = `ruleSetStatus.${status.toLowerCase()}`;
      }
      break;

    case "registration":
      if (status === "Registered") {
        intent = "success";
        labelKey = "registrationStatus.registered";
      } else if (status === "Pending") {
        intent = "warning";
        labelKey = "registrationStatus.pending";
      } else if (status === "Suspended") {
        intent = "danger";
        labelKey = "registrationStatus.suspended";
      } else if (status === "Deregistered") {
        intent = "neutral";
        labelKey = "registrationStatus.deregistered";
      }
      break;

    case "review":
      if (status === "Approved" || status === "Reviewed") {
        intent = "success";
        labelKey = "reviewStatus.approved";
      } else if (status === "Pending" || status === "Unreviewed") {
        intent = "warning";
        labelKey = "reviewStatus.pending";
      }
      break;
  }

  const translatedLabel = t(labelKey as any, { defaultValue: status });

  return <StatusBadge status={status} intent={intent} label={translatedLabel} />;
}

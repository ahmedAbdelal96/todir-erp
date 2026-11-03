import React from "react";
import { useTranslation } from "react-i18next";
import { StatusBadge } from "../../../components/fields/status-badge.tsx";
import type { EmployeeStatus, EmploymentStatus } from "../types.ts";

export interface HrStatusBadgeProps {
  readonly status: EmployeeStatus | EmploymentStatus | string;
  readonly label?: string | undefined;
}

export function HrStatusBadge({ status, label }: HrStatusBadgeProps): React.JSX.Element {
  const { t } = useTranslation("hr");
  let tone: "success" | "warning" | "danger" | "neutral" | "info" = "neutral";

  switch (status) {
    case "Active":
    case "Approved":
    case "Verified":
      tone = "success";
      break;
    case "PreHire":
    case "Probationary":
    case "Pending":
    case "NoticePeriod":
      tone = "warning";
      break;
    case "Terminated":
    case "Suspended":
    case "Archived":
    case "Rejected":
    case "Cancelled":
    case "Revoked":
      tone = "danger";
      break;
    case "Ended":
    case "Inactive":
      tone = "neutral";
      break;
    default:
      tone = "info";
      break;
  }

  const localizedLabel = label ?? t(`statuses.${status}`, { defaultValue: status });
  return <StatusBadge intent={tone} label={localizedLabel} />;
}

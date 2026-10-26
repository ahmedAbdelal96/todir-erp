import React from "react";
import { useTranslation } from "react-i18next";
import { StatusBadge, type StatusIntent } from "../../../components/fields/status-badge.tsx";

export interface FinancialStatusBadgeProps {
  readonly status: string;
  readonly isOverdue?: boolean | undefined;
  readonly className?: string | undefined;
}

export function FinancialStatusBadge({
  status,
  isOverdue = false,
  className
}: FinancialStatusBadgeProps): React.JSX.Element {
  const { t } = useTranslation("subledger");

  if (isOverdue && (status === "Open" || status === "PartiallySettled")) {
    return (
      <StatusBadge
        intent="danger"
        label={t("status.overdue")}
        className={className}
      />
    );
  }

  let intent: StatusIntent = "neutral";
  let labelKey = "status.open";

  switch (status.toLowerCase()) {
    case "open":
      intent = "success";
      labelKey = "status.open";
      break;
    case "partiallysettled":
    case "partially_settled":
      intent = "warning";
      labelKey = "status.partiallySettled";
      break;
    case "settled":
    case "closed":
      intent = "neutral";
      labelKey = "status.settled";
      break;
    case "creditavailable":
    case "credit_available":
      intent = "info";
      labelKey = "status.creditAvailable";
      break;
    case "overdue":
      intent = "danger";
      labelKey = "status.overdue";
      break;
    case "reversed":
      intent = "neutral";
      labelKey = "status.reversed";
      break;
    case "pendingapproval":
    case "pending_approval":
      intent = "warning";
      labelKey = "status.pendingApproval";
      break;
    default:
      intent = "neutral";
      labelKey = `status.${status.toLowerCase()}`;
  }

  return (
    <StatusBadge
      intent={intent}
      label={t(labelKey, { defaultValue: status })}
      className={className}
    />
  );
}

import React from "react";
import { useTranslation } from "react-i18next";
import { StatusBadge, type StatusIntent } from "../../../components/fields/status-badge.tsx";

export type PurchasingBadgeKind = "commercial" | "approval" | "receipt" | "invoicing" | "matching" | "return";

export interface PurchasingStatusBadgeProps {
  readonly state: string;
  readonly kind?: PurchasingBadgeKind | undefined;
}

export function PurchasingStatusBadge({
  state,
  kind = "commercial"
}: PurchasingStatusBadgeProps): React.JSX.Element {
  const { t } = useTranslation("purchasing");

  let intent: StatusIntent = "neutral";
  const labelKey = `status.${state}` as const;

  switch (state) {
    case "Draft":
      intent = "neutral";
      break;
    case "PendingApproval":
    case "Pending":
      intent = "warning";
      break;
    case "Approved":
    case "Executed":
      intent = "success";
      break;
    case "Sent":
    case "Open":
      intent = "info";
      break;
    case "PartiallyCompleted":
    case "PartiallyReceived":
    case "PartiallyInvoiced":
    case "WithinTolerance":
      intent = "info";
      break;
    case "Completed":
    case "Received":
    case "Invoiced":
    case "Matched":
      intent = "success";
      break;
    case "Variance":
      intent = "warning";
      break;
    case "Blocked":
    case "Rejected":
    case "Cancelled":
      intent = "danger";
      break;
    case "Closed":
      intent = "neutral";
      break;
    case "ApprovedException":
      intent = "warning";
      break;
    case "NotReceived":
    case "NotInvoiced":
    case "NotRequired":
    default:
      intent = "neutral";
      break;
  }

  const label = (t as any)(labelKey, { defaultValue: state });

  return <StatusBadge intent={intent} label={label} />;
}

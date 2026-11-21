import React from "react";
import { useTranslation } from "react-i18next";
import { StatusBadge, type StatusIntent } from "../../../components/fields/status-badge.tsx";

export interface ManufacturingStatusBadgeProps {
  readonly status?: string | undefined | null;
  readonly state?: string | undefined | null;
  readonly type?: "order" | "material" | "operation" | "quality" | "cost" | "bom" | "supply";
}

export function ManufacturingStatusBadge({ status, state, type = "order" }: ManufacturingStatusBadgeProps): React.JSX.Element {
  const { t } = useTranslation("manufacturing");
  const resolvedStatus = status ?? state;

  if (!resolvedStatus) {
    return <StatusBadge intent="neutral" label="—" />;
  }

  let intent: StatusIntent = "neutral";

  switch (resolvedStatus) {
    case "Released":
    case "Approved":
    case "Active":
      intent = "info";
      break;
    case "InProgress":
    case "PartiallyIssued":
    case "Accumulating":
    case "Suggested":
      intent = "warning";
      break;
    case "Completed":
    case "Closed":
    case "FullyIssued":
    case "Consumed":
    case "Passed":
    case "Reconciled":
    case "Firmed":
      intent = "success";
      break;
    case "OnHold":
    case "Paused":
    case "Failed":
    case "Blocked":
    case "Cancelled":
    case "Superseded":
    case "Stale":
      intent = "danger";
      break;
    case "Draft":
    case "Pending":
    case "Ready":
    case "Estimated":
    case "Exempt":
    case "Archived":
    default:
      intent = "neutral";
      break;
  }

  const label = t(`status.${resolvedStatus}` as any, resolvedStatus);

  return (
    <StatusBadge
      data-testid={`status-badge-${resolvedStatus.toLowerCase()}`}
      intent={intent}
      label={label}
      size="small"
    />
  );
}

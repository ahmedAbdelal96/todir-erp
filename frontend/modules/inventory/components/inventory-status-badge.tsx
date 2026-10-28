import React from "react";
import { useTranslation } from "react-i18next";
import { StatusBadge } from "../../../components/fields/status-badge.tsx";
import type { StockStatus, WarehouseStatus, AdjustmentRequestStatus, TransferMode } from "../types.ts";

export interface InventoryStatusBadgeProps {
  readonly status: string;
  readonly type?: "stock" | "warehouse" | "adjustment" | "count" | "transfer";
}

export function InventoryStatusBadge({ status, type = "stock" }: InventoryStatusBadgeProps): React.JSX.Element {
  const { t } = useTranslation("inventory");

  if (type === "warehouse") {
    const isAct = status === "Active";
    const isSusp = status === "Suspended";
    const tone = isAct ? "success" : isSusp ? "warning" : "neutral";
    const text = isAct
      ? t("warehouses.statuses.Active")
      : isSusp
      ? t("warehouses.statuses.Suspended")
      : t("warehouses.statuses.Archived");
    return <StatusBadge status={tone} label={text} />;
  }

  if (type === "adjustment") {
    let tone: "success" | "warning" | "danger" | "info" | "neutral" = "neutral";
    let key: string = "adjustments.statuses.Draft";
    if (status === "Draft") {
      tone = "neutral";
      key = "adjustments.statuses.Draft";
    } else if (status === "PendingApproval") {
      tone = "warning";
      key = "adjustments.statuses.PendingApproval";
    } else if (status === "Approved") {
      tone = "info";
      key = "adjustments.statuses.Approved";
    } else if (status === "Posted") {
      tone = "success";
      key = "adjustments.statuses.Posted";
    } else if (status === "Rejected") {
      tone = "danger";
      key = "adjustments.statuses.Rejected";
    } else if (status === "Cancelled") {
      tone = "neutral";
      key = "adjustments.statuses.Cancelled";
    }
    return <StatusBadge status={tone} label={t(key as any)} />;
  }

  if (type === "count") {
    let tone: "success" | "warning" | "danger" | "info" | "neutral" = "neutral";
    let key: string = "counts.statuses.Draft";
    if (status === "Draft") {
      tone = "neutral";
      key = "counts.statuses.Draft";
    } else if (status === "InProgress") {
      tone = "warning";
      key = "counts.statuses.InProgress";
    } else if (status === "Completed") {
      tone = "info";
      key = "counts.statuses.Completed";
    } else if (status === "Posted") {
      tone = "success";
      key = "counts.statuses.Posted";
    } else if (status === "Cancelled") {
      tone = "neutral";
      key = "counts.statuses.Cancelled";
    }
    return <StatusBadge status={tone} label={t(key as any)} />;
  }

  if (type === "transfer") {
    let tone: "success" | "warning" | "danger" | "info" | "neutral" = "neutral";
    let key: string = "transfers.statuses.Draft";
    if (status === "Draft") {
      tone = "neutral";
      key = "transfers.statuses.Draft";
    } else if (status === "Shipped") {
      tone = "warning";
      key = "transfers.statuses.Shipped";
    } else if (status === "PartiallyReceived") {
      tone = "info";
      key = "transfers.statuses.PartiallyReceived";
    } else if (status === "Completed") {
      tone = "success";
      key = "transfers.statuses.Completed";
    } else if (status === "Cancelled") {
      tone = "neutral";
      key = "transfers.statuses.Cancelled";
    }
    return <StatusBadge status={tone} label={t(key as any)} />;
  }

  // Stock status
  let tone: "success" | "warning" | "danger" | "info" | "neutral" = "neutral";
  let key = `stockStatuses.${status}`;
  if (status === "Available") {
    tone = "success";
  } else if (status === "QualityHold" || status === "Quarantine" || status === "ReturnsInspection") {
    tone = "warning";
  } else if (status === "Damaged" || status === "Blocked" || status === "ScrapPending") {
    tone = "danger";
  } else if (status === "InTransit") {
    tone = "info";
  }

  return <StatusBadge status={tone} label={t(key as any, { defaultValue: status })} />;
}

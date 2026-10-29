import React from "react";
import { useTranslation } from "react-i18next";
import { StatusBadge, type StatusIntent } from "../../../components/fields/status-badge.tsx";
import type {
  QuotationState,
  SalesCreditState,
  SalesOrderCommercialState,
  SalesOrderFulfillmentState,
  SalesOrderInvoicingState,
  SalesReturnState
} from "../types.ts";

export type SalesBadgeKind =
  | "quotationState"
  | "commercialState"
  | "fulfillmentState"
  | "invoicingState"
  | "creditState"
  | "returnState";

export interface SalesStatusBadgeProps {
  readonly kind: SalesBadgeKind;
  readonly status:
    | QuotationState
    | SalesOrderCommercialState
    | SalesOrderFulfillmentState
    | SalesOrderInvoicingState
    | SalesCreditState
    | SalesReturnState
    | string;
}

export function SalesStatusBadge({ kind, status }: SalesStatusBadgeProps): React.JSX.Element {
  const { t } = useTranslation("sales");

  let intent: StatusIntent = "neutral";
  let label = status;

  switch (kind) {
    case "quotationState": {
      switch (status) {
        case "Draft":
          intent = "neutral";
          label = t("status.draft");
          break;
        case "Open":
          intent = "info";
          label = t("status.open");
          break;
        case "Accepted":
          intent = "success";
          label = t("status.accepted");
          break;
        case "Converted":
          intent = "success";
          label = t("status.converted");
          break;
        case "Declined":
          intent = "danger";
          label = t("status.declined");
          break;
        case "Expired":
          intent = "warning";
          label = t("status.expired");
          break;
        case "Cancelled":
          intent = "danger";
          label = t("status.cancelled");
          break;
      }
      break;
    }

    case "commercialState": {
      switch (status) {
        case "Draft":
          intent = "neutral";
          label = t("status.draft");
          break;
        case "Confirmed":
          intent = "success";
          label = t("status.confirmed");
          break;
        case "Invoiced":
          intent = "success";
          label = t("status.invoiced");
          break;
        case "Cancelled":
          intent = "danger";
          label = t("status.cancelled");
          break;
      }
      break;
    }

    case "fulfillmentState": {
      switch (status) {
        case "NotRequired":
          intent = "neutral";
          label = t("status.notRequired");
          break;
        case "Unfulfilled":
        case "Pending":
          intent = "warning";
          label = t("status.pending");
          break;
        case "PartiallyFulfilled":
          intent = "info";
          label = t("status.partiallyFulfilled");
          break;
        case "Fulfilled":
          intent = "success";
          label = t("status.fulfilled");
          break;
        case "Backordered":
          intent = "danger";
          label = t("status.backordered");
          break;
      }
      break;
    }

    case "invoicingState": {
      switch (status) {
        case "Uninvoiced":
        case "NotInvoiced":
          intent = "neutral";
          label = t("status.notInvoiced");
          break;
        case "PartiallyInvoiced":
          intent = "info";
          label = t("status.partiallyInvoiced");
          break;
        case "Invoiced":
          intent = "success";
          label = t("status.invoiced");
          break;
      }
      break;
    }

    case "creditState": {
      switch (status) {
        case "NotChecked":
          intent = "neutral";
          label = t("status.notChecked");
          break;
        case "Approved":
        case "Allowed":
          intent = "success";
          label = t("status.allowed");
          break;
        case "Warn":
          intent = "warning";
          label = t("status.warn");
          break;
        case "Blocked":
          intent = "danger";
          label = t("status.blocked");
          break;
        case "ApprovalRequired":
          intent = "warning";
          label = t("status.approvalRequired");
          break;
      }
      break;
    }

    case "returnState": {
      switch (status) {
        case "Requested":
          intent = "warning";
          label = t("status.requested");
          break;
        case "Approved":
          intent = "info";
          label = t("status.approved");
          break;
        case "Executed":
          intent = "success";
          label = t("status.executed");
          break;
        case "Rejected":
          intent = "danger";
          label = t("status.rejected");
          break;
        case "Cancelled":
          intent = "danger";
          label = t("status.cancelled");
          break;
      }
      break;
    }
  }

  return <StatusBadge intent={intent} label={label} />;
}

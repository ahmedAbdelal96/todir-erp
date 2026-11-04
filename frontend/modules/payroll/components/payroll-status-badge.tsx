import React from "react";
import { useTranslation } from "react-i18next";
import { StatusBadge, type StatusVariant } from "../../../components/fields/status-badge.tsx";
import type {
  CalculationState,
  FinalizationState,
  PaymentState,
  ApprovalState,
  StatutoryMode,
  ReceivableStatus
} from "../types.ts";

export interface PayrollStatusBadgeProps {
  readonly status: CalculationState | FinalizationState | PaymentState | ApprovalState | StatutoryMode | ReceivableStatus | string;
  readonly type?: "calculation" | "finalization" | "payment" | "approval" | "statutory" | "receivable";
}

export function PayrollStatusBadge({ status, type = "calculation" }: PayrollStatusBadgeProps): React.JSX.Element {
  const { t } = useTranslation("payroll");

  const resolveBadgeConfig = (): { variant: StatusVariant; label: string } => {
    switch (status) {
      // Calculation States
      case "Draft":
        return { variant: "neutral", label: t("statuses.draft") };
      case "Captured":
        return { variant: "info", label: t("statuses.captured") };
      case "Calculating":
        return { variant: "warning", label: t("statuses.calculating") };
      case "Calculated":
        return { variant: "success", label: t("statuses.calculated") };
      case "Failed":
        return { variant: "danger", label: t("statuses.failed") };

      // Finalization States
      case "Open":
        return { variant: "info", label: t("statuses.open") };
      case "Finalized":
        return { variant: "success", label: t("statuses.finalized") };
      case "Cancelled":
        return { variant: "neutral", label: t("statuses.cancelled") };
      case "Consumed":
        return { variant: "neutral", label: t("statuses.consumed") };

      // Payment States
      case "NotReady":
        return { variant: "neutral", label: t("statuses.notReady") };
      case "Ready":
        return { variant: "info", label: t("statuses.ready") };
      case "Requested":
        return { variant: "warning", label: t("statuses.requested") };
      case "Partial":
        return { variant: "warning", label: t("statuses.partial") };
      case "Paid":
        return { variant: "success", label: t("statuses.paid") };

      // Approval States
      case "NotRequired":
        return { variant: "neutral", label: t("statuses.notRequired") };
      case "Pending":
        return { variant: "warning", label: t("statuses.pending") };
      case "Approved":
        return { variant: "success", label: t("statuses.approved") };
      case "Rejected":
        return { variant: "danger", label: t("statuses.rejected") };

      // Statutory Modes
      case "NotManaged":
        return { variant: "neutral", label: t("statutoryModes.notManaged") };
      case "Manual":
        return { variant: "warning", label: t("statutoryModes.manual") };
      case "Automatic":
        return { variant: "success", label: t("statutoryModes.automatic") };

      // Receivable States
      case "Disbursed":
        return { variant: "success", label: t("statuses.disbursed") };
      case "Paused":
        return { variant: "warning", label: t("statuses.paused") };
      case "Settled":
        return { variant: "success", label: t("statuses.settled") };
      case "WrittenOff":
        return { variant: "danger", label: t("statuses.writtenOff") };

      default:
        return { variant: "neutral", label: status };
    }
  };

  const { variant, label } = resolveBadgeConfig();

  return <StatusBadge status={variant} label={label} />;
}

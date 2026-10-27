import React from "react";
import { useTranslation } from "react-i18next";
import { QuickPeekDrawer } from "../../../components/primitives/quick-peek-drawer.tsx";
import { MoneyDisplay } from "../../../components/fields/money-display.tsx";
import { PaymentStatusBadge } from "./payment-status-badge.tsx";
import type { PaymentRecord, PaymentDirection, PaymentMethod } from "../types.ts";

export interface PaymentDetailDrawerProps {
  readonly payment: PaymentRecord | null;
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly onOpenFull: (payment: PaymentRecord) => void;
}

export function PaymentDetailDrawer({
  payment,
  isOpen,
  onClose,
  onOpenFull
}: PaymentDetailDrawerProps): React.JSX.Element {
  const { t } = useTranslation("payments");

  if (!payment) return <></>;

  const getDirectionLabel = (dir: PaymentDirection) => {
    switch (dir) {
      case "Incoming":
        return t("direction.incoming");
      case "Outgoing":
        return t("direction.outgoing");
      case "InternalTransfer":
      default:
        return t("direction.internaltransfer");
    }
  };

  const getMethodLabel = (m: PaymentMethod) => {
    switch (m) {
      case "Cash":
        return t("method.cash");
      case "BankTransfer":
        return t("method.banktransfer");
      case "Cheque":
        return t("method.cheque");
      case "Card":
        return t("method.card");
      case "PaymentGateway":
        return t("method.paymentgateway");
      case "Wallet":
        return t("method.wallet");
      case "Other":
      default:
        return t("method.other");
    }
  };

  return (
    <QuickPeekDrawer
      isOpen={isOpen}
      onClose={onClose}
      title={payment.number}
      subtitle={getDirectionLabel(payment.direction)}
      onOpenFullRecord={() => onOpenFull(payment)}
      fullRecordLabel={t("list.openFullRecord")}
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {/* Amount Card */}
        <div
          style={{
            backgroundColor: "#f8fafc",
            border: "1px solid #cbd5e1",
            borderRadius: "6px",
            padding: "1rem",
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center"
          }}
        >
          <span style={{ fontSize: "0.875rem", color: "#64748b" }}>{t("list.columnAmount")}:</span>
          <span style={{ fontSize: "1.25rem", fontWeight: 700, color: "#102a43" }}>
            <MoneyDisplay value={{ amount: payment.amount, currency: payment.currencyCode }} />
          </span>
        </div>

        {/* Status */}
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
          <span style={{ fontSize: "0.875rem", color: "#64748b" }}>{t("list.columnState")}:</span>
          <PaymentStatusBadge state={payment.state} />
        </div>

        {/* Method */}
        <div style={{ display: "flex", justifyContent: "space-between" }}>
          <span style={{ fontSize: "0.875rem", color: "#64748b" }}>{t("list.columnMethod")}:</span>
          <span style={{ fontWeight: 600 }}>{getMethodLabel(payment.method)}</span>
        </div>

        {/* Date */}
        <div style={{ display: "flex", justifyContent: "space-between" }}>
          <span style={{ fontSize: "0.875rem", color: "#64748b" }}>{t("list.columnDate")}:</span>
          <span>{payment.paymentDate}</span>
        </div>

        {/* References */}
        {(payment.bankReference || payment.customerReference || payment.supplierReference) && (
          <div style={{ display: "flex", justifyContent: "space-between" }}>
            <span style={{ fontSize: "0.875rem", color: "#64748b" }}>{t("list.columnReference")}:</span>
            <span>{payment.bankReference || payment.customerReference || payment.supplierReference}</span>
          </div>
        )}
      </div>
    </QuickPeekDrawer>
  );
}

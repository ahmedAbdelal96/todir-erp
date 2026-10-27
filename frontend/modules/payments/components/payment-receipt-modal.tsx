import React from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { MoneyDisplay } from "../../../components/fields/money-display.tsx";
import type { PaymentRecord, PaymentMethod, PaymentBusinessState } from "../types.ts";

export interface PaymentReceiptModalProps {
  readonly payment: PaymentRecord | null;
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly partyName?: string | undefined;
  readonly accountName?: string | undefined;
}

export function PaymentReceiptModal({
  payment,
  isOpen,
  onClose,
  partyName,
  accountName
}: PaymentReceiptModalProps): React.JSX.Element {
  const { t } = useTranslation("payments");

  if (!payment) return <></>;

  const isIncoming = payment.direction === "Incoming";
  const voucherTitle = isIncoming ? t("receipt.receiptVoucher") : t("receipt.paymentVoucher");

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

  const getStateLabel = (s: PaymentBusinessState) => {
    switch (s) {
      case "Completed":
        return t("state.completed");
      case "Pending":
        return t("state.pending");
      case "Draft":
        return t("state.draft");
      case "Failed":
        return t("state.failed");
      case "Cancelled":
        return t("state.cancelled");
      case "Reversed":
        return t("state.reversed");
      default:
        return t("state.unknown");
    }
  };

  const handlePrint = () => {
    window.print();
  };

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open) => {
        if (!open) onClose();
      }}
      title={voucherTitle}
      size="large"
    >
      <div
        id="payment-voucher-content"
        style={{
          padding: "1.5rem",
          backgroundColor: "#ffffff",
          color: "#0f172a",
          fontSize: "0.875rem",
          fontFamily: "inherit"
        }}
      >
        {/* Header */}
        <div
          style={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "flex-start",
            borderBottom: "2px solid #102a43",
            paddingBottom: "1rem",
            marginBottom: "1.5rem"
          }}
        >
          <div>
            <h2 style={{ margin: 0, fontSize: "1.25rem", color: "#102a43", fontWeight: 800 }}>
              {voucherTitle}
            </h2>
            <div style={{ color: "#64748b", marginTop: "0.25rem", fontSize: "0.8125rem" }}>
              {t("receipt.voucherSubhead")}
            </div>
          </div>
          <div style={{ textAlign: "end" }}>
            <div style={{ fontWeight: 700, fontSize: "1rem", color: "#102a43" }}>
              <bdi>{payment.number}</bdi>
            </div>
            <div style={{ color: "#64748b", fontSize: "0.8125rem", marginTop: "0.25rem" }}>
              {t("receipt.date")}: {payment.paymentDate}
            </div>
          </div>
        </div>

        {/* Amount Box */}
        <div
          style={{
            backgroundColor: "#f8fafc",
            border: "1px solid #cbd5e1",
            borderRadius: "6px",
            padding: "1rem 1.25rem",
            marginBottom: "1.5rem",
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center"
          }}
        >
          <span style={{ fontWeight: 600, color: "#334155" }}>{t("receipt.totalAmount")}:</span>
          <span style={{ fontSize: "1.25rem", fontWeight: 800, color: "#102a43" }}>
            <MoneyDisplay value={{ amount: payment.amount, currency: payment.currencyCode }} />
          </span>
        </div>

        {/* Details Grid */}
        <div
          style={{
            display: "grid",
            gridTemplateColumns: "repeat(2, 1fr)",
            gap: "1rem",
            marginBottom: "2rem",
            lineHeight: 1.6
          }}
        >
          <div>
            <span style={{ color: "#64748b", display: "block", fontSize: "0.75rem" }}>
              {isIncoming ? t("receipt.receivedFrom") : t("receipt.paidTo")}:
            </span>
            <span style={{ fontWeight: 700, color: "#0f172a" }}>
              {partyName || payment.partyId || t("receipt.unspecifiedParty")}
            </span>
          </div>

          <div>
            <span style={{ color: "#64748b", display: "block", fontSize: "0.75rem" }}>
              {t("receipt.paymentMethod")}:
            </span>
            <span style={{ fontWeight: 600 }}>{getMethodLabel(payment.method)}</span>
          </div>

          <div>
            <span style={{ color: "#64748b", display: "block", fontSize: "0.75rem" }}>
              {isIncoming ? t("receipt.destinationAccount") : t("receipt.sourceAccount")}:
            </span>
            <span style={{ fontWeight: 600 }}>{accountName || payment.paymentAccountId}</span>
          </div>

          <div>
            <span style={{ color: "#64748b", display: "block", fontSize: "0.75rem" }}>
              {t("receipt.bankReference")}:
            </span>
            <span>{payment.bankReference || payment.customerReference || payment.supplierReference || "—"}</span>
          </div>

          <div>
            <span style={{ color: "#64748b", display: "block", fontSize: "0.75rem" }}>
              {t("receipt.postingDate")}:
            </span>
            <span>{payment.postingDate}</span>
          </div>

          <div>
            <span style={{ color: "#64748b", display: "block", fontSize: "0.75rem" }}>
              {t("receipt.status")}:
            </span>
            <span style={{ fontWeight: 600 }}>{getStateLabel(payment.state)}</span>
          </div>
        </div>

        {/* Signatures */}
        <div
          style={{
            display: "grid",
            gridTemplateColumns: "repeat(3, 1fr)",
            gap: "1.5rem",
            marginTop: "3rem",
            borderTop: "1px dashed #cbd5e1",
            paddingTop: "1.5rem",
            textAlign: "center"
          }}
        >
          <div>
            <div style={{ color: "#64748b", fontSize: "0.75rem" }}>{t("receipt.preparedBy")}</div>
            <div style={{ marginTop: "2rem", borderBottom: "1px solid #94a3b8" }} />
          </div>
          <div>
            <div style={{ color: "#64748b", fontSize: "0.75rem" }}>{t("receipt.auditedBy")}</div>
            <div style={{ marginTop: "2rem", borderBottom: "1px solid #94a3b8" }} />
          </div>
          <div>
            <div style={{ color: "#64748b", fontSize: "0.75rem" }}>{t("receipt.authorizedSignature")}</div>
            <div style={{ marginTop: "2rem", borderBottom: "1px solid #94a3b8" }} />
          </div>
        </div>
      </div>

      <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "1.5rem" }}>
        <Button variant="secondary" onPress={onClose}>
          {t("receipt.close")}
        </Button>
        <Button variant="primary" onPress={handlePrint}>
          {t("receipt.printAction")}
        </Button>
      </div>
    </Dialog>
  );
}

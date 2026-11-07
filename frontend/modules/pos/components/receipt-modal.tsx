import React from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { ReceiptIcon } from "./pos-icons.tsx";
import { BidiText } from "../../../components/primitives/bidi-text.tsx";
import type { CartLineResponse, CheckoutResponse } from "../types.ts";

export interface ReceiptModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly checkout: CheckoutResponse | null;
  readonly lines: readonly CartLineResponse[];
  readonly subtotal: number;
  readonly taxTotal: number;
  readonly discountTotal: number;
  readonly grandTotal: number;
  readonly currencyCode?: string | undefined;
  readonly registerCode?: string | undefined;
  readonly cashierName?: string | undefined;
  readonly onNewSale: () => void;
}

export function ReceiptModal({
  isOpen,
  onClose,
  checkout,
  lines,
  subtotal,
  taxTotal,
  discountTotal,
  grandTotal,
  currencyCode = "EGP",
  registerCode = "MAIN",
  cashierName = "Cashier",
  onNewSale,
}: ReceiptModalProps): React.JSX.Element | null {
  const { t } = useTranslation("pos");

  const handlePrint = () => {
    window.print();
  };

  const handleDone = () => {
    onClose();
    onNewSale();
  };

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open) => !open && onClose()}
      title={t("receipt.title")}
      description={t("receipt.subtitle")}
      size="medium"
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {/* Thermal Receipt Paper Container */}
        <div
          id="pos-thermal-receipt"
          style={{
            background: "#ffffff",
            color: "#1e293b",
            padding: "1.5rem",
            borderRadius: "var(--radius-sm)",
            border: "1px dashed #cbd5e1",
            fontFamily: "monospace, system-ui, sans-serif",
            maxWidth: "24rem",
            margin: "0 auto",
            boxShadow: "0 4px 6px -1px rgba(0, 0, 0, 0.05)",
          }}
        >
          {/* Header */}
          <div style={{ textAlign: "center", borderBottom: "1px dashed #94a3b8", paddingBottom: "0.75rem", marginBottom: "0.75rem" }}>
            <h2 style={{ fontSize: "1.25rem", fontWeight: 800, margin: "0 0 0.25rem" }}>
              {t("receipt.storeName")}
            </h2>
            <div style={{ fontSize: "0.75rem", color: "#64748b" }}>
              {t("receipt.taxInvoice")}
            </div>
            <div style={{ fontSize: "0.75rem", color: "#64748b", marginTop: "0.25rem" }}>
              {new Date().toLocaleString()}
            </div>
          </div>

          {/* Metadata */}
          <div style={{ fontSize: "0.75rem", marginBottom: "0.75rem", display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.25rem" }}>
            <div>
              <span>{t("receipt.register")}: </span>
              <strong><BidiText>{registerCode}</BidiText></strong>
            </div>
            <div>
              <span>{t("receipt.cashier")}: </span>
              <strong><BidiText>{cashierName}</BidiText></strong>
            </div>
            {checkout?.saleId && (
              <div style={{ gridColumn: "span 2" }}>
                <span>{t("receipt.saleId")}: </span>
                <strong><BidiText>{checkout.saleId.slice(0, 8)}</BidiText></strong>
              </div>
            )}
          </div>

          {/* Items */}
          <table style={{ width: "100%", fontSize: "0.75rem", borderCollapse: "collapse", marginBottom: "0.75rem" }}>
            <thead>
              <tr style={{ borderBottom: "1px solid #cbd5e1", textAlign: "left" }}>
                <th style={{ padding: "0.25rem 0" }}>{t("receipt.item")}</th>
                <th style={{ padding: "0.25rem 0", textAlign: "center" }}>{t("receipt.qty")}</th>
                <th style={{ padding: "0.25rem 0", textAlign: "right" }}>{t("receipt.price")}</th>
                <th style={{ padding: "0.25rem 0", textAlign: "right" }}>{t("receipt.total")}</th>
              </tr>
            </thead>
            <tbody>
              {lines.map((line) => (
                <tr key={line.id} style={{ borderBottom: "1px dashed #e2e8f0" }}>
                  <td style={{ padding: "0.35rem 0" }}>
                    <BidiText>{line.description}</BidiText>
                  </td>
                  <td style={{ padding: "0.35rem 0", textAlign: "center" }}>
                    <BidiText>{line.quantity}</BidiText>
                  </td>
                  <td style={{ padding: "0.35rem 0", textAlign: "right" }}>
                    <BidiText>{line.unitPrice.toFixed(2)}</BidiText>
                  </td>
                  <td style={{ padding: "0.35rem 0", textAlign: "right", fontWeight: 600 }}>
                    <BidiText>{line.lineTotal.toFixed(2)}</BidiText>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>

          {/* Totals */}
          <div style={{ borderTop: "1px dashed #94a3b8", paddingTop: "0.5rem", fontSize: "0.8125rem", display: "flex", flexDirection: "column", gap: "0.25rem" }}>
            <div style={{ display: "flex", justifyContent: "space-between" }}>
              <span>{t("receipt.subtotal")}:</span>
              <span><BidiText>{subtotal.toFixed(2)} {currencyCode}</BidiText></span>
            </div>
            {discountTotal > 0 && (
              <div style={{ display: "flex", justifyContent: "space-between", color: "#16a34a" }}>
                <span>{t("receipt.discount")}:</span>
                <span>-<BidiText>{discountTotal.toFixed(2)} {currencyCode}</BidiText></span>
              </div>
            )}
            <div style={{ display: "flex", justifyContent: "space-between" }}>
              <span>{t("receipt.vat")}:</span>
              <span><BidiText>{taxTotal.toFixed(2)} {currencyCode}</BidiText></span>
            </div>
            <div style={{ display: "flex", justifyContent: "space-between", fontSize: "1rem", fontWeight: 800, marginTop: "0.25rem", borderTop: "1px solid #1e293b", paddingTop: "0.25rem" }}>
              <span>{t("receipt.grandTotal")}:</span>
              <span><BidiText>{grandTotal.toFixed(2)} {currencyCode}</BidiText></span>
            </div>
          </div>

          {/* Footer Thank You */}
          <div style={{ textAlign: "center", marginTop: "1rem", paddingTop: "0.75rem", borderTop: "1px dashed #94a3b8", fontSize: "0.75rem", color: "#64748b" }}>
            <div>{t("receipt.thankYou")}</div>
            <div style={{ marginTop: "0.25rem", fontWeight: 600 }}>{t("receipt.systemVersion")}</div>
          </div>
        </div>

        {/* Modal Actions */}
        <div style={{ display: "flex", justifyContent: "space-between", marginTop: "0.5rem" }}>
          <Button variant="secondary" onClick={handlePrint} style={{ display: "flex", gap: "0.5rem", alignItems: "center" }}>
            <ReceiptIcon size={18} />
            <span>{t("receipt.printReceipt")}</span>
          </Button>

          <Button variant="primary" onClick={handleDone}>
            {t("receipt.newSale")}
          </Button>
        </div>
      </div>
    </Dialog>
  );
}

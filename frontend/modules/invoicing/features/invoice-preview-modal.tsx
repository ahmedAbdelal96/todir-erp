import React from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { MoneyDisplay } from "../../../components/fields/money-display.tsx";
import { InvoiceScheduleTable } from "../components/invoice-schedule-table.tsx";
import {
  AlertCircleIcon,
  CheckCircleIcon,
  ShieldCheckIcon
} from "../../../components/primitives/icons.tsx";
import type { InvoicePreview } from "../types.ts";
import type { CurrencyCode } from "../../../components/contracts/domain-values.ts";

export interface InvoicePreviewModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly preview?: InvoicePreview | null | undefined;
  readonly isLoading?: boolean | undefined;
  readonly isFinalizing?: boolean | undefined;
  readonly onConfirmFinalize: () => void;
  readonly currencyCode: CurrencyCode;
  readonly hasAccountingPreviewPermission?: boolean | undefined;
}

export function InvoicePreviewModal({
  isOpen,
  onClose,
  preview,
  isLoading = false,
  isFinalizing = false,
  onConfirmFinalize,
  currencyCode,
  hasAccountingPreviewPermission = false
}: InvoicePreviewModalProps): React.JSX.Element {
  const { t } = useTranslation("invoicing");

  if (!isOpen) return <></>;

  const rawBlockers = preview?.blockers ?? [];
  const normalizedBlockers: { code: string; message: string }[] = rawBlockers
    .map((b: unknown) => {
      if (typeof b === "string") return { code: "", message: b };
      if (b && typeof b === "object") {
        const item = b as { code?: string; message?: string };
        return { code: item.code ?? "", message: item.message ?? "" };
      }
      return { code: "", message: "" };
    })
    .filter((b) => Boolean(b.message.trim() || b.code.trim()));

  const rawWarnings = preview?.warnings ?? [];
  const normalizedWarnings: string[] = rawWarnings
    .map((w: unknown) => {
      if (typeof w === "string") return w;
      if (w && typeof w === "object") {
        const item = w as { code?: string; message?: string };
        return item.message || item.code || "";
      }
      return "";
    })
    .filter((msg) => Boolean(msg && msg.trim()));

  const isReady = (preview?.readyToFinalize ?? false) && normalizedBlockers.length === 0;

  const mapErrorMessage = (code: string, fallback: string): string => {
    switch (code) {
      case "invoicing.price_missing":
        return t("errors.priceMissing");
      case "invoicing.tax_not_ready":
        return t("errors.taxNotReady");
      case "invoicing.accounting_not_ready":
        return t("errors.accountingNotReady");
      case "invoicing.product_not_ready":
        return t("errors.productNotReady");
      case "invoicing.duplicate_supplier_invoice":
        return t("errors.duplicateSupplierInvoice");
      default:
        return fallback || t("errors.generic");
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open) => { if (!open) onClose(); }}
      title={t("preview.dialogTitle")}
      size="large"
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem", padding: "0.5rem 0" }}>
        {/* Subtitle */}
        <p style={{ margin: 0, fontSize: "0.875rem", color: "var(--color-content-secondary, #475569)" }}>
          {t("preview.dialogSubtitle")}
        </p>

        {isLoading ? (
          <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-secondary, #475569)" }}>
            <p>{t("preview.checkingStatus")}</p>
          </div>
        ) : (
          <>
            {/* Status Indicator Banner */}
            <div
              style={{
                display: "flex",
                alignItems: "center",
                gap: "0.75rem",
                padding: "0.875rem 1rem",
                borderRadius: "var(--radius-md)",
                backgroundColor: isReady ? "#ecfdf5" : "#fef2f2",
                border: `1px solid ${isReady ? "#a7f3d0" : "#fecaca"}`,
                color: isReady ? "#065f46" : "#991b1b"
              }}
            >
              {isReady ? <CheckCircleIcon size={20} /> : <AlertCircleIcon size={20} />}
              <div style={{ flex: 1 }}>
                <strong style={{ fontSize: "0.9375rem" }}>
                  {isReady ? t("preview.readyBadge") : t("preview.blockedBadge")}
                </strong>
                {!isReady && (
                  <p style={{ margin: "0.25rem 0 0", fontSize: "0.8125rem" }}>
                    {t("preview.blockedDescription")}
                  </p>
                )}
              </div>
            </div>

            {/* Blockers Section */}
            {normalizedBlockers.length > 0 && (
              <div
                style={{
                  padding: "0.875rem 1rem",
                  borderRadius: "var(--radius-md)",
                  backgroundColor: "#fff1f2",
                  border: "1px solid #fda4af",
                  display: "flex",
                  flexDirection: "column",
                  gap: "0.5rem"
                }}
              >
                <div style={{ display: "flex", alignItems: "center", gap: "0.5rem", color: "#9f1239", fontWeight: 700 }}>
                  <AlertCircleIcon size={16} />
                  <span>{t("preview.blockersTitle")}</span>
                </div>
                <ul style={{ margin: 0, paddingInlineStart: "1.25rem", fontSize: "0.8125rem", color: "#881337" }}>
                  {normalizedBlockers.map((b, i) => (
                    <li key={i} style={{ marginBottom: "0.25rem" }}>
                      {mapErrorMessage(b.code, b.message)}
                    </li>
                  ))}
                </ul>
              </div>
            )}

            {/* Warnings Section */}
            {normalizedWarnings.length > 0 && (
              <div
                style={{
                  padding: "0.875rem 1rem",
                  borderRadius: "var(--radius-md)",
                  backgroundColor: "#fffbeb",
                  border: "1px solid #fde68a",
                  display: "flex",
                  flexDirection: "column",
                  gap: "0.5rem"
                }}
              >
                <div style={{ display: "flex", alignItems: "center", gap: "0.5rem", color: "#92400e", fontWeight: 700 }}>
                  <AlertCircleIcon size={16} />
                  <span>{t("preview.warningsTitle")}</span>
                </div>
                <ul style={{ margin: 0, paddingInlineStart: "1.25rem", fontSize: "0.8125rem", color: "#78350f" }}>
                  {normalizedWarnings.map((msg, i) => (
                    <li key={i} style={{ marginBottom: "0.25rem" }}>
                      {msg}
                    </li>
                  ))}
                </ul>
              </div>
            )}

            {/* Totals Summary */}
            {preview?.totals && (
              <div
                style={{
                  display: "grid",
                  gridTemplateColumns: "repeat(auto-fit, minmax(130px, 1fr))",
                  gap: "0.75rem",
                  padding: "0.875rem",
                  backgroundColor: "#f8fafc",
                  borderRadius: "var(--radius-md)",
                  border: "1px solid #e2e8f0"
                }}
              >
                <div>
                  <span style={{ fontSize: "0.75rem", color: "#64748b" }}>{t("draft.subtotal")}</span>
                  <div style={{ fontWeight: 700, fontSize: "0.9375rem" }}>
                    <MoneyDisplay value={{ amount: preview.totals.netAmount, currency: currencyCode }} />
                  </div>
                </div>
                <div>
                  <span style={{ fontSize: "0.75rem", color: "#64748b" }}>{t("draft.totalTax")}</span>
                  <div style={{ fontWeight: 700, fontSize: "0.9375rem" }}>
                    {parseFloat(preview.totals.taxAmount) > 0 ? (
                      <MoneyDisplay value={{ amount: preview.totals.taxAmount, currency: currencyCode }} />
                    ) : (
                      <span style={{ fontSize: "0.8125rem", color: "#64748b", fontStyle: "italic", fontWeight: 600 }}>
                        {t("draft.nonTaxable")}
                      </span>
                    )}
                  </div>
                </div>
                <div>
                  <span style={{ fontSize: "0.75rem", color: "#64748b" }}>{t("draft.grandTotal")}</span>
                  <div style={{ fontWeight: 900, fontSize: "1.0625rem", color: "#102a43" }}>
                    <MoneyDisplay value={{ amount: preview.totals.grossAmount, currency: currencyCode }} />
                  </div>
                </div>
              </div>
            )}

            {/* Payment Schedule */}
            {preview?.paymentSchedule && preview.paymentSchedule.length > 0 && (
              <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem" }}>
                <span style={{ fontWeight: 700, fontSize: "0.875rem", color: "#334155" }}>
                  {t("preview.scheduleTitle")}
                </span>
                <InvoiceScheduleTable schedule={preview.paymentSchedule} />
              </div>
            )}

            {/* Accounting Preview (Protected by permission) */}
            {hasAccountingPreviewPermission && preview?.accountingPreview?.lines && (
              <div
                style={{
                  display: "flex",
                  flexDirection: "column",
                  gap: "0.5rem",
                  padding: "0.875rem",
                  backgroundColor: "#f0fdf4",
                  borderRadius: "var(--radius-md)",
                  border: "1px solid #bbf7d0"
                }}
              >
                <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                  <div style={{ display: "flex", alignItems: "center", gap: "0.375rem", fontWeight: 700, fontSize: "0.875rem", color: "#166534" }}>
                    <ShieldCheckIcon size={16} />
                    <span>{t("preview.accountingPreviewTitle")}</span>
                  </div>
                  {preview.accountingPreview.isBalanced && (
                    <span style={{ fontSize: "0.75rem", color: "#15803d", fontWeight: 600 }}>
                      ✓ {t("preview.balancedCheck")}
                    </span>
                  )}
                </div>

                <div style={{ overflowX: "auto" }}>
                  <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "0.75rem", textAlign: "start" }}>
                    <thead>
                      <tr style={{ borderBottom: "1px solid #86efac", color: "#14532d" }}>
                        <th style={{ padding: "0.375rem" }}>{t("preview.accountingAccount")}</th>
                        <th style={{ padding: "0.375rem", textAlign: "end" }}>{t("preview.accountingDebit")}</th>
                        <th style={{ padding: "0.375rem", textAlign: "end" }}>{t("preview.accountingCredit")}</th>
                      </tr>
                    </thead>
                    <tbody>
                      {preview.accountingPreview.lines.map((l, i) => (
                        <tr key={i} style={{ borderBottom: "1px solid #dcfce7" }}>
                          <td style={{ padding: "0.375rem" }}>
                            <strong>{l.accountCode}</strong> — {l.accountName}
                          </td>
                          <td style={{ padding: "0.375rem", textAlign: "end" }}>
                            {parseFloat(l.debit) > 0 ? <MoneyDisplay value={{ amount: l.debit, currency: currencyCode }} /> : "—"}
                          </td>
                          <td style={{ padding: "0.375rem", textAlign: "end" }}>
                            {parseFloat(l.credit) > 0 ? <MoneyDisplay value={{ amount: l.credit, currency: currencyCode }} /> : "—"}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
            )}

            {/* Finalization Notice */}
            <p style={{ margin: 0, fontSize: "0.75rem", color: "var(--color-content-muted, #64748b)" }}>
              {t("preview.finalizeConfirmText")}
            </p>
          </>
        )}

        {/* Modal Actions */}
        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", borderTop: "1px solid #e2e8f0", paddingTop: "0.75rem" }}>
          <Button variant="secondary" onPress={onClose} isDisabled={isFinalizing}>
            {t("preview.cancelModal")}
          </Button>
          <Button
            variant="primary"
            onPress={onConfirmFinalize}
            isDisabled={!isReady || isFinalizing || isLoading}
          >
            {isFinalizing ? t("preview.finalizingProgress") : t("preview.confirmFinalizeButton")}
          </Button>
        </div>
      </div>
    </Dialog>
  );
}

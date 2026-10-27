import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { MoneyDisplay } from "../../../components/fields/money-display.tsx";
import { AlertCircleIcon } from "../../../components/primitives/icons.tsx";
import type { InvoiceDetail, InvoiceCorrectionRequest, InvoiceCorrectionLineInput } from "../types.ts";

export interface InvoiceCorrectionDialogProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly invoice: InvoiceDetail;
  readonly documentType?: "CreditNote" | "DebitNote" | undefined;
  readonly onSubmit: (request: InvoiceCorrectionRequest) => Promise<void>;
  readonly isSubmitting?: boolean | undefined;
}

export function InvoiceCorrectionDialog({
  isOpen,
  onClose,
  invoice,
  documentType = "CreditNote",
  onSubmit,
  isSubmitting = false
}: InvoiceCorrectionDialogProps): React.JSX.Element {
  const { t } = useTranslation("invoicing");
  const [creditMode, setCreditMode] = useState<"full" | "partial">("full");
  const [reason, setReason] = useState("");
  const [lineQuantities, setLineQuantities] = useState<Record<string, string>>(() => {
    const init: Record<string, string> = {};
    for (const line of invoice.lines) {
      init[line.id] = line.quantity;
    }
    return init;
  });
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  if (!isOpen) return <></>;

  const isCredit = documentType === "CreditNote";
  const title = isCredit ? t("correction.dialogTitleCredit") : t("correction.dialogTitleDebit");
  const submitText = isCredit ? t("correction.submitCreditNote") : t("correction.submitDebitNote");

  const handleQuantityChange = (lineId: string, val: string) => {
    setLineQuantities((prev) => ({ ...prev, [lineId]: val }));
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMessage(null);

    if (!reason.trim()) {
      setErrorMessage(t("correction.reasonLabel") + ": مطلوب إدخال سبب الاعتماد والتصحيح.");
      return;
    }

    const lines: InvoiceCorrectionLineInput[] = [];
    for (const line of invoice.lines) {
      const qtyStr = creditMode === "full" ? line.quantity : (lineQuantities[line.id] ?? "0");
      const qty = parseFloat(qtyStr);
      if (qty > 0) {
        lines.push({
          originalLineId: line.id,
          quantity: qtyStr
        });
      }
    }

    if (lines.length === 0) {
      setErrorMessage("يجب تحديد بند واحد على الأقل بكمية صالحة لإصدار الإشعار.");
      return;
    }

    const today = new Date().toISOString().slice(0, 10);
    const request: InvoiceCorrectionRequest = {
      documentType,
      reason,
      documentDate: today,
      postingDate: today,
      taxDate: today,
      idempotencyKey: `corr-${invoice.id}-${Date.now()}`,
      lines
    };

    try {
      await onSubmit(request);
      onClose();
    } catch (err: unknown) {
      const code = (err as { problemDetails?: { extensions?: { code?: string } } })?.problemDetails?.extensions?.code;
      if (code === "invoicing.over_credit") {
        setErrorMessage(t("errors.overCredit"));
      } else {
        setErrorMessage(t("errors.generic"));
      }
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open) => { if (!open) onClose(); }}
      title={title}
      size="large"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1.25rem", padding: "0.5rem 0" }}>
        {/* Subtitle / Reference notice */}
        <p style={{ margin: 0, fontSize: "0.875rem", color: "var(--color-content-secondary, #475569)" }}>
          {t("correction.dialogSubtitle", { number: invoice.documentNumber || "—" })}
        </p>

        {errorMessage && (
          <div
            style={{
              padding: "0.75rem 1rem",
              borderRadius: "var(--radius-md)",
              backgroundColor: "#fef2f2",
              border: "1px solid #fecaca",
              color: "#991b1b",
              fontSize: "0.8125rem",
              display: "flex",
              alignItems: "center",
              gap: "0.5rem"
            }}
          >
            <AlertCircleIcon size={16} />
            <span>{errorMessage}</span>
          </div>
        )}

        {/* Correction Mode Choice */}
        {isCredit && (
          <div style={{ display: "flex", gap: "1rem", backgroundColor: "#f8fafc", padding: "0.75rem", borderRadius: "var(--radius-md)", border: "1px solid #e2e8f0" }}>
            <label style={{ display: "flex", alignItems: "center", gap: "0.375rem", fontSize: "0.875rem", cursor: "pointer", fontWeight: creditMode === "full" ? 700 : 400 }}>
              <input
                type="radio"
                name="creditMode"
                value="full"
                checked={creditMode === "full"}
                onChange={() => setCreditMode("full")}
              />
              <span>{t("correction.fullCreditOption")}</span>
            </label>
            <label style={{ display: "flex", alignItems: "center", gap: "0.375rem", fontSize: "0.875rem", cursor: "pointer", fontWeight: creditMode === "partial" ? 700 : 400 }}>
              <input
                type="radio"
                name="creditMode"
                value="partial"
                checked={creditMode === "partial"}
                onChange={() => setCreditMode("partial")}
              />
              <span>{t("correction.partialCreditOption")}</span>
            </label>
          </div>
        )}

        {/* Lines selection if partial credit */}
        {creditMode === "partial" && (
          <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem" }}>
            <span style={{ fontSize: "0.875rem", fontWeight: 700, color: "#334155" }}>
              {t("correction.linesToCreditTitle")}
            </span>
            <div style={{ overflowX: "auto" }}>
              <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "0.8125rem", textAlign: "start" }}>
                <thead>
                  <tr style={{ backgroundColor: "#f1f5f9", borderBottom: "1px solid #cbd5e1" }}>
                    <th style={{ padding: "0.5rem" }}>{t("correction.itemColumn")}</th>
                    <th style={{ padding: "0.5rem", textAlign: "center" }}>{t("correction.originalQty")}</th>
                    <th style={{ padding: "0.5rem", textAlign: "end" }}>{t("correction.priceColumn")}</th>
                    <th style={{ padding: "0.5rem", width: "120px" }}>{t("correction.creditQty")}</th>
                  </tr>
                </thead>
                <tbody>
                  {invoice.lines.map((l) => (
                    <tr key={l.id} style={{ borderBottom: "1px solid #e2e8f0" }}>
                      <td style={{ padding: "0.5rem", fontWeight: 600 }}>{l.description}</td>
                      <td style={{ padding: "0.5rem", textAlign: "center" }}>{l.quantity} {l.unitCode}</td>
                      <td style={{ padding: "0.5rem", textAlign: "end" }}>
                        <MoneyDisplay value={{ amount: l.finalUnitPrice, currency: invoice.currencyCode }} />
                      </td>
                      <td style={{ padding: "0.5rem" }}>
                        <TextField
                          size="compact"
                          value={lineQuantities[l.id] ?? ""}
                          onChange={(v) => handleQuantityChange(l.id, v)}
                          aria-label={t("correction.creditQty")}
                        />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        )}

        {/* Reason Input */}
        <div>
          <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 700, color: "#334155", marginBottom: "0.25rem" }}>
            {t("correction.reasonLabel")} <span style={{ color: "#ef4444" }}>*</span>
          </label>
          <TextField
            value={reason}
            onChange={setReason}
            placeholder={t("correction.reasonPlaceholder")}
            aria-label={t("correction.reasonLabel")}
            isRequired
          />
        </div>

        {/* Modal Actions */}
        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", borderTop: "1px solid #e2e8f0", paddingTop: "0.75rem" }}>
          <Button variant="secondary" onPress={onClose} isDisabled={isSubmitting}>
            {t("correction.cancel")}
          </Button>
          <Button variant="primary" type="submit" isDisabled={isSubmitting}>
            {isSubmitting ? t("correction.issuingProgress") : submitText}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

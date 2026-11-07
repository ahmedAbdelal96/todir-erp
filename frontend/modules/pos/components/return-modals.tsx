import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { Select } from "../../../components/primitives/select.tsx";
import { BidiText } from "../../../components/primitives/bidi-text.tsx";
import type { CreateReturnRequest, ReturnLineRequest } from "../types.ts";

export interface ReturnModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly sessionId: string;
  readonly sales?: readonly any[] | undefined;
  readonly onCreateReturn: (request: CreateReturnRequest) => Promise<void>;
  readonly isSubmitting?: boolean | undefined;
}

export function ReturnModal({
  isOpen,
  onClose,
  sessionId,
  sales = [],
  onCreateReturn,
  isSubmitting = false,
}: ReturnModalProps): React.JSX.Element | null {
  const { t } = useTranslation("pos");
  const [saleId, setSaleId] = useState<string>("");
  const [selectedSaleSummary, setSelectedSaleSummary] = useState<string | null>(null);
  const [reason, setReason] = useState<string>("Defective");
  const [disposition, setDisposition] = useState<"ReturnToStock" | "Scrap">("ReturnToStock");
  const [error, setError] = useState<string | null>(null);

  // Return line inputs
  const [lineSaleId, setLineSaleId] = useState<string>("");
  const [quantityStr, setQuantityStr] = useState<string>("1");
  const [amountStr, setAmountStr] = useState<string>("0");

  const quantity = parseFloat(quantityStr) || 0;
  const amount = parseFloat(amountStr) || 0;

  const handleSelectSaleFromList = (selectedId: string) => {
    setSaleId(selectedId);
    setError(null);
    const found = sales.find((s) => s.id === selectedId);
    if (found) {
      setAmountStr(String(found.total || 0));
      setLineSaleId(found.id);
      const saleRef = found.receiptNumber || (found.code ?? t("returns.saleNumberRef", { index: 1 }));
      setSelectedSaleSummary(
        `${saleRef} • ${found.businessDate || ""} • ${found.total || 0} ${found.currency || ""}`
      );
    } else {
      setSelectedSaleSummary(null);
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!saleId.trim()) {
      setError(t("returns.saleIdRequired"));
      return;
    }
    if (quantity <= 0) {
      setError(t("returns.invalidQuantity"));
      return;
    }
    setError(null);

    const returnLine: ReturnLineRequest = {
      originalSaleLineId: lineSaleId.trim() || saleId.trim(),
      quantity,
      amount,
    };

    const req: CreateReturnRequest = {
      originalSaleId: saleId.trim(),
      sessionId,
      lines: [returnLine],
      reason,
      disposition,
    };

    try {
      await onCreateReturn(req);
      onClose();
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : t("returns.createFailed"));
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open) => !open && onClose()}
      title={t("returns.modalTitle")}
      description={t("returns.modalSubtitle")}
      size="medium"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {/* Sale Lookup from Recent Completed Sales */}
        {sales.length > 0 && (
          <div style={{ display: "flex", flexDirection: "column", gap: "0.35rem" }}>
            <label style={{ fontSize: "0.875rem", fontWeight: 600, color: "var(--color-content-default)" }}>
              {t("returns.selectOriginalSale", "اختر الفاتورة الأصلية من المبيعات المكتملة")}
            </label>
            <select
              value={saleId}
              onChange={(e) => handleSelectSaleFromList(e.target.value)}
              style={{
                padding: "0.625rem",
                borderRadius: "var(--radius-md)",
                border: "1px solid var(--color-border-default)",
                backgroundColor: "var(--color-surface-default)",
                color: "var(--color-content-default)",
                fontSize: "0.875rem",
              }}
            >
              <option value="">{t("returns.chooseSalePrompt", "-- اختر الفاتورة المطلوب إرجاعها --")}</option>
              {sales.map((s, idx) => (
                <option key={s.id} value={s.id}>
                  {(s.receiptNumber || s.code || t("returns.saleNumberRef", { index: idx + 1 }))} • {s.businessDate} • {s.total} {s.currency}
                </option>
              ))}
            </select>
          </div>
        )}

        <TextField
          label={t("returns.originalSaleIdLabel")}
          placeholder={t("returns.originalSaleIdPlaceholder")}
          value={saleId}
          onChange={(v) => {
            setSaleId(v);
            setError(null);
          }}
          autoFocus={sales.length === 0}
        />

        {selectedSaleSummary && (
          <div
            style={{
              padding: "0.5rem 0.75rem",
              borderRadius: "var(--radius-sm)",
              backgroundColor: "rgba(16, 185, 129, 0.1)",
              border: "1px solid rgba(16, 185, 129, 0.3)",
              fontSize: "0.8125rem",
              color: "var(--color-status-success)",
            }}
          >
            ✓ <strong>{t("returns.selectedSale", "الفاتورة المحددة")}:</strong> {selectedSaleSummary}
          </div>
        )}

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <Select
            label={t("returns.reasonLabel")}
            selectedKey={reason}
            onSelectionChange={(val) => setReason(String(val))}
            options={[
              { id: "Defective", label: t("returns.reasonDefective") },
              { id: "WrongItem", label: t("returns.reasonWrongItem") },
              { id: "CustomerChangedMind", label: t("returns.reasonChangedMind") },
              { id: "Other", label: t("returns.reasonOther") },
            ]}
          />

          <Select
            label={t("returns.dispositionLabel")}
            selectedKey={disposition}
            onSelectionChange={(val) => setDisposition(String(val) as "ReturnToStock" | "Scrap")}
            options={[
              { id: "ReturnToStock", label: t("returns.dispositionRestock") },
              { id: "Scrap", label: t("returns.dispositionScrap") },
            ]}
          />
        </div>

        <div
          style={{
            border: "1px solid var(--color-border-default)",
            borderRadius: "var(--radius-md)",
            padding: "1rem",
            background: "var(--color-surface-hover)",
          }}
        >
          <div style={{ fontWeight: 600, fontSize: "0.875rem", marginBottom: "0.5rem" }}>
            {t("returns.lineDetails")}
          </div>
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
            <TextField
              label={t("returns.quantityLabel")}
              type="number"
              value={quantityStr}
              onChange={(v) => setQuantityStr(v)}
            />
            <TextField
              label={t("returns.refundAmountLabel")}
              type="number"
              value={amountStr}
              onChange={(v) => setAmountStr(v)}
            />
          </div>
        </div>

        {error && (
          <div style={{ color: "var(--color-status-danger)", fontSize: "0.875rem" }} role="alert">
            {error}
          </div>
        )}

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "0.5rem" }}>
          <Button variant="secondary" type="button" onClick={onClose} isDisabled={isSubmitting}>
            {t("common.cancel")}
          </Button>
          <Button variant="primary" type="submit" isDisabled={isSubmitting || !saleId.trim() || quantity <= 0}>
            {isSubmitting ? t("common.submitting") : t("returns.createReturnAction")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

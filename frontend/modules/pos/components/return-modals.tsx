import React, { useState, useEffect } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { BidiText } from "../../../components/primitives/bidi-text.tsx";
import { posApi } from "../services/pos-api.ts";
import { toScaledBigInt, fromScaledBigInt, scaledToNumber } from "../utils/exact-money-math.ts";
import type { CreateReturnRequest, ReturnLineRequest, PosSaleLineDetail } from "../types.ts";

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
  const [currencyCode, setCurrencyCode] = useState<string>("EGP");

  // Sale lines state
  const [saleLines, setSaleLines] = useState<readonly PosSaleLineDetail[]>([]);
  const [selectedLineId, setSelectedLineId] = useState<string>("");
  const [isLoadingLines, setIsLoadingLines] = useState<boolean>(false);

  // Return options
  const [reason, setReason] = useState<string>("Defective");
  const [disposition, setDisposition] = useState<"ReturnToStock" | "Scrap">("ReturnToStock");
  const [quantityStr, setQuantityStr] = useState<string>("1");
  const [error, setError] = useState<string | null>(null);

  // Reset or load lines when saleId changes
  useEffect(() => {
    if (!saleId || !saleId.trim()) {
      setSaleLines([]);
      setSelectedLineId("");
      return;
    }

    let isCancelled = false;
    setIsLoadingLines(true);
    setError(null);

    posApi
      .getSaleLines(saleId.trim())
      .then((lines) => {
        if (isCancelled) return;
        setSaleLines(lines);
        const availableLine = lines.find((l) => l.quantity - l.returnedQuantity > 0) || lines[0];
        if (availableLine) {
          setSelectedLineId(availableLine.id);
          const maxAvailable = Math.max(1, availableLine.quantity - availableLine.returnedQuantity);
          setQuantityStr(String(Math.min(1, maxAvailable)));
        } else {
          setSelectedLineId("");
        }
      })
      .catch((err) => {
        if (isCancelled) return;
        setSaleLines([]);
        setSelectedLineId("");
        // Fallback: don't block if sale lines cannot be loaded
        setError(err instanceof Error ? err.message : t("returns.loadLinesFailed", "تعذر تحميل بنود الفاتورة الأصلية"));
      })
      .finally(() => {
        if (!isCancelled) setIsLoadingLines(false);
      });

    return () => {
      isCancelled = true;
    };
  }, [saleId, t]);

  const activeLine = saleLines.find((l) => l.id === selectedLineId);
  const maxReturnQty = activeLine ? Math.max(0, activeLine.quantity - activeLine.returnedQuantity) : 1;

  // Exact Authoritative Refund Amount calculation
  const parsedQty = parseFloat(quantityStr) || 0;
  const unitPriceScaled = activeLine ? toScaledBigInt(activeLine.unitPrice) : 0n;
  const refundAmountScaled = (unitPriceScaled * toScaledBigInt(parsedQty)) / 10000n;
  const refundAmount = scaledToNumber(refundAmountScaled);
  const refundAmountStr = fromScaledBigInt(refundAmountScaled, 2);

  const handleSelectSaleFromList = (selectedId: string) => {
    setSaleId(selectedId);
    setError(null);
    const found = sales.find((s) => s.id === selectedId);
    if (found) {
      setCurrencyCode(found.currency || "EGP");
      const saleRef = found.receiptNumber || (found.code ?? t("returns.saleNumberRef", { index: 1 }));
      setSelectedSaleSummary(
        `${saleRef} • ${found.businessDate || ""} • ${found.total || 0} ${found.currency || ""}`
      );
    } else {
      setSelectedSaleSummary(null);
    }
  };

  const handleLineSelect = (lineId: string) => {
    setSelectedLineId(lineId);
    const found = saleLines.find((l) => l.id === lineId);
    if (found) {
      const maxAvailable = Math.max(1, found.quantity - found.returnedQuantity);
      setQuantityStr(String(Math.min(1, maxAvailable)));
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!saleId.trim()) {
      setError(t("returns.saleIdRequired", "يرجى اختيار أو إدخال معرّف الفاتورة الأصلية"));
      return;
    }
    if (parsedQty <= 0) {
      setError(t("returns.invalidQuantity", "الكمية المرتجعة يجب أن تكون أكبر من الصفر"));
      return;
    }
    if (activeLine && parsedQty > maxReturnQty) {
      setError(
        t("returns.quantityExceeded", "الكمية المطلوبة تتجاوز الكمية المتاحة للإرجاع (الحد الأقصى: {{max}})", {
          max: maxReturnQty,
        })
      );
      return;
    }
    setError(null);

    const returnLine: ReturnLineRequest = {
      originalSaleLineId: selectedLineId.trim() || saleId.trim(),
      quantity: parsedQty,
      amount: refundAmount,
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
      setError(err instanceof Error ? err.message : t("returns.createFailed", "فشل تسجيل طلب المرتجع"));
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open) => !open && onClose()}
      title={t("returns.modalTitle", "تسجيل مرتجع مبيعات")}
      description={t("returns.modalSubtitle", "استرجاع منتجات من فاتورة مبيعات سابقة مع التحقق من الكميات المصرح بها")}
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
          label={t("returns.originalSaleIdLabel", "رقم أو معرّف الفاتورة الأصلية")}
          placeholder={t("returns.originalSaleIdPlaceholder", "أدخل معرّف الفاتورة أو اختر من القائمة أعلاه")}
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
              backgroundColor: "var(--color-surface-hover)",
              fontSize: "0.8125rem",
              color: "var(--color-content-default)",
              fontWeight: 500,
            }}
          >
            {t("returns.selectedSale", "الفاتورة المختارة")}: <BidiText>{selectedSaleSummary}</BidiText>
          </div>
        )}

        {/* Original Sale Lines Selection */}
        {isLoadingLines ? (
          <div style={{ fontSize: "0.875rem", color: "var(--color-content-muted)", padding: "0.5rem 0" }}>
            {t("returns.loadingLines", "جاري استرجاع بنود الفاتورة الأصلية المعتمدة...")}
          </div>
        ) : saleLines.length > 0 ? (
          <div style={{ display: "flex", flexDirection: "column", gap: "0.35rem" }}>
            <label style={{ fontSize: "0.875rem", fontWeight: 600, color: "var(--color-content-default)" }}>
              {t("returns.selectSaleLine", "البند المراد إرجاعه من الفاتورة")}
            </label>
            <select
              value={selectedLineId}
              onChange={(e) => handleLineSelect(e.target.value)}
              style={{
                padding: "0.625rem",
                borderRadius: "var(--radius-md)",
                border: "1px solid var(--color-border-default)",
                backgroundColor: "var(--color-surface-default)",
                color: "var(--color-content-default)",
                fontSize: "0.875rem",
              }}
            >
              {saleLines.map((line) => {
                const available = line.quantity - line.returnedQuantity;
                return (
                  <option key={line.id} value={line.id} disabled={available <= 0}>
                    {line.description} • {line.quantity} {line.uomCode} • {line.unitPrice} {currencyCode}
                    {available <= 0 ? ` (${t("returns.fullyReturned", "تم إرجاعه بالكامل")})` : ` (${t("returns.availableQty", "المتاح")}: ${available})`}
                  </option>
                );
              })}
            </select>
          </div>
        ) : null}

        {/* Return Quantity and Disposition Grid */}
        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
          <TextField
            label={t("returns.quantityLabel", "الكمية المرتجعة")}
            type="number"
            value={quantityStr}
            onChange={(v) => {
              setQuantityStr(v);
              setError(null);
            }}
            description={
              activeLine
                ? `${t("returns.maxAllowed", "الحد الأقصى للإرجاع")}: ${maxReturnQty} ${activeLine.uomCode}`
                : undefined
            }
          />

          <div style={{ display: "flex", flexDirection: "column", gap: "0.35rem" }}>
            <label style={{ fontSize: "0.875rem", fontWeight: 600, color: "var(--color-content-default)" }}>
              {t("returns.dispositionLabel", "حالة البضاعة المرتجعة")}
            </label>
            <select
              value={disposition}
              onChange={(e) => setDisposition(e.target.value as "ReturnToStock" | "Scrap")}
              style={{
                padding: "0.625rem",
                borderRadius: "var(--radius-md)",
                border: "1px solid var(--color-border-default)",
                backgroundColor: "var(--color-surface-default)",
                color: "var(--color-content-default)",
                fontSize: "0.875rem",
              }}
            >
              <option value="ReturnToStock">{t("returns.dispRestock", "إعادة للمخزن الصالح (Restock)")}</option>
              <option value="Scrap">{t("returns.dispScrap", "هالك / إعدام (Scrap)")}</option>
            </select>
          </div>
        </div>

        {/* Reason for Return */}
        <div style={{ display: "flex", flexDirection: "column", gap: "0.35rem" }}>
          <label style={{ fontSize: "0.875rem", fontWeight: 600, color: "var(--color-content-default)" }}>
            {t("returns.reasonLabel", "سبب الإرجاع")}
          </label>
          <select
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            style={{
              padding: "0.625rem",
              borderRadius: "var(--radius-md)",
              border: "1px solid var(--color-border-default)",
              backgroundColor: "var(--color-surface-default)",
              color: "var(--color-content-default)",
              fontSize: "0.875rem",
            }}
          >
            <option value="Defective">{t("returns.reasonDefective", "عيب صناعة / تالف")}</option>
            <option value="WrongItem">{t("returns.reasonWrongItem", "صنف غير مطابق للطلب")}</option>
            <option value="CustomerChange">{t("returns.reasonCustomerChange", "رغبة العميل (بحالة ممتازة)")}</option>
            <option value="DamagedInTransit">{t("returns.reasonDamaged", "تلف أثناء الاستلام أو النقل")}</option>
          </select>
        </div>

        {/* Authoritative Refund Amount Display */}
        <div
          style={{
            padding: "0.75rem 1rem",
            borderRadius: "var(--radius-md)",
            backgroundColor: "var(--color-surface-hover)",
            border: "1px solid var(--color-border-default)",
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
          }}
        >
          <span style={{ fontSize: "0.875rem", fontWeight: 600, color: "var(--color-content-default)" }}>
            {t("returns.refundableTotal", "المبلغ المالي المستحق للإرجاع")}:
          </span>
          <span style={{ fontSize: "1.125rem", fontWeight: 800, color: "var(--color-brand-primary)" }}>
            <BidiText>{refundAmountStr} {currencyCode}</BidiText>
          </span>
        </div>

        {error && (
          <div
            style={{
              padding: "0.5rem 0.75rem",
              borderRadius: "var(--radius-sm)",
              backgroundColor: "rgba(239, 68, 68, 0.1)",
              border: "1px solid rgba(239, 68, 68, 0.3)",
              color: "var(--color-status-danger)",
              fontSize: "0.875rem",
            }}
            role="alert"
          >
            {error}
          </div>
        )}

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "0.5rem" }}>
          <Button variant="ghost" type="button" onClick={onClose} isDisabled={isSubmitting}>
            {t("common.cancel", "إلغاء")}
          </Button>
          <Button variant="primary" type="submit" isDisabled={isSubmitting || parsedQty <= 0}>
            {isSubmitting ? t("common.saving", "جاري الحفظ...") : t("returns.submitReturn", "تأكيد تسجيل المرتجع")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Select, type SelectOption } from "../../../../components/primitives/select.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import {
  usePrepareDepreciationRunMutation,
  useReverseDepreciationRunMutation,
  useAssetBooksQuery
} from "../../hooks/use-fixed-assets-queries.ts";

export interface PrepareDepreciationRunModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
}

export function PrepareDepreciationRunModal({
  isOpen,
  onClose,
  legalEntityId
}: PrepareDepreciationRunModalProps): React.JSX.Element {
  const { t } = useTranslation("fixedAssets");
  const booksQuery = useAssetBooksQuery(legalEntityId);
  const prepareMutation = usePrepareDepreciationRunMutation();

  const now = new Date();
  const [periodYear, setPeriodYear] = useState(String(now.getFullYear()));
  const [periodMonth, setPeriodMonth] = useState(String(now.getMonth() + 1));
  const [cutoffDate, setCutoffDate] = useState(() => now.toISOString().slice(0, 10));
  const [description, setDescription] = useState("");
  const [error, setError] = useState<string | null>(null);

  const monthOptions: SelectOption[] = Array.from({ length: 12 }, (_, i) => ({
    id: String(i + 1),
    label: t("depreciation.monthLabel", "{{num}} - شهر {{num}}", { num: i + 1 })
  }));

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    const year = parseInt(periodYear, 10);
    const month = parseInt(periodMonth, 10);
    if (isNaN(year) || year < 2000 || year > 2100) {
      setError(t("validation.validYearRequired", "يجب إدخال سنة مالية صحيحة"));
      return;
    }

    const books = booksQuery.data ?? [];
    const bookId = books[0]?.id;
    if (!bookId) {
      setError(t("validation.bookRequired", "لا يوجد دفتر أصول مهيأ للكيان القانوني"));
      return;
    }

    try {
      const monthStr = month < 10 ? `0${month}` : `${month}`;
      const periodStart = `${year}-${monthStr}-01`;
      const lastDay = new Date(year, month, 0).getDate();
      const periodEnd = cutoffDate || `${year}-${monthStr}-${lastDay}`;

      await prepareMutation.mutateAsync({
        legalEntityId,
        bookId,
        periodStart,
        periodEnd,
        idempotencyKey: `depr-${legalEntityId}-${year}-${monthStr}`
      });
      onClose();
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : t("common.unknownError", "فشل تجهيز مسير الإهلاك");
      setError(msg);
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("depreciation.prepareModalTitle", "تجهيز مسير إهلاك شهري جديد")}
      size="medium"
      data-testid="prepare-depreciation-modal"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {error && (
          <div data-testid="prepare-depr-error" style={{ padding: "0.75rem", backgroundColor: "#fef2f2", color: "#991b1b", borderRadius: "0.375rem" }}>
            {error}
          </div>
        )}

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <TextField
            label={t("fields.periodYear", "السنة المالية")}
            type="number"
            value={periodYear}
            onChange={setPeriodYear}
            isRequired
            data-testid="period-year-input"
          />
          <div>
            <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 500, marginBottom: "0.25rem", color: "#334155" }}>
              {t("fields.periodMonth", "الشهر المحاسبي")} *
            </label>
            <Select
              options={monthOptions}
              selectedKey={periodMonth}
              onSelectionChange={(k) => setPeriodMonth(String(k ?? "1"))}
              data-testid="period-month-select"
            />
          </div>
        </div>

        <TextField
          label={t("fields.cutoffDate", "تاريخ الإقفال / القيد")}
          type="date"
          value={cutoffDate}
          onChange={setCutoffDate}
          isRequired
          data-testid="cutoff-date-input"
        />

        <TextField
          label={t("fields.runDescription", "وصف المسير (اختياري)")}
          value={description}
          onChange={setDescription}
          placeholder={t("fields.runDescPlaceholder", "مسير إهلاك شهر أكتوبر لكافة الأصول الثابتة...")}
          data-testid="description-input"
        />

        <div style={{ padding: "0.75rem", backgroundColor: "#f0f4f8", borderRadius: "0.375rem", fontSize: "0.8125rem", color: "#475569" }}>
          {t("depreciation.runCalculationNotice", "يقوم النظام باحتساب أقساط الإهلاك لكافة الأصول النشطة المؤهلة وإنشاء أسطر المسير لمراجعتها واعتمادها قبل الترحيل المالي.")}
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "0.5rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("common.cancel", "إلغاء")}
          </Button>
          <Button variant="primary" type="submit" isLoading={prepareMutation.isPending} data-testid="submit-prepare-btn">
            {t("depreciation.startCalculation", "بدء احتساب الإهلاك")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

export interface ReverseDepreciationRunModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly runId: string | null;
  readonly periodLabel?: string | undefined;
}

export function ReverseDepreciationRunModal({
  isOpen,
  onClose,
  legalEntityId,
  runId,
  periodLabel
}: ReverseDepreciationRunModalProps): React.JSX.Element {
  const { t } = useTranslation("fixedAssets");
  const reverseMutation = useReverseDepreciationRunMutation();

  const [reason, setReason] = useState("");
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!runId) return;
    setError(null);

    if (!reason.trim()) {
      setError(t("validation.reversalReasonRequired", "يجب إدخال سبب عكس قيد المسير"));
      return;
    }

    try {
      await reverseMutation.mutateAsync({
        runId,
        legalEntityId,
        request: {
          reversalDate: new Date().toISOString().slice(0, 10),
          reason: reason.trim(),
          idempotencyKey: `rev-${runId}-${Date.now()}`
        }
      });
      onClose();
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : t("common.unknownError", "فشل عكس مسير الإهلاك");
      setError(msg);
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("depreciation.reverseModalTitle", "عكس مسير الإهلاك المرحل (Reversal)")}
      size="medium"
      data-testid="reverse-depreciation-modal"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {periodLabel && (
          <div style={{ padding: "0.75rem", backgroundColor: "#fef2f2", border: "1px solid #fecaca", borderRadius: "0.375rem", fontSize: "0.875rem", color: "#991b1b" }}>
            <strong>{t("depreciation.targetRun", "المسير المستهدف")}:</strong> {periodLabel}
          </div>
        )}

        {error && (
          <div data-testid="reverse-depr-error" style={{ padding: "0.75rem", backgroundColor: "#fef2f2", color: "#991b1b", borderRadius: "0.375rem" }}>
            {error}
          </div>
        )}

        <div style={{ fontSize: "0.875rem", color: "#475569" }}>
          {t("depreciation.reverseWarning", "سيتم إنشاء قيد يومية عكسي في دفتر الأستاذ العام وتعديل مجمع إهلاك الأصول المتأثرة.")}
        </div>

        <TextField
          label={t("fields.reversalReason", "سبب ومبرر الإلغاء والعكس")}
          value={reason}
          onChange={setReason}
          isRequired
          placeholder={t("fields.reversalReasonPlaceholder", "خطأ في تاريخ إدخال أصل رأسمالي يستلزم إعادة احتساب المسير...")}
          data-testid="reversal-reason-input"
        />

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "0.5rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("common.cancel", "إلغاء")}
          </Button>
          <Button variant="danger" type="submit" isLoading={reverseMutation.isPending} data-testid="submit-reverse-btn">
            {t("depreciation.confirmReversal", "تأكيد عكس المسير والقيد")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

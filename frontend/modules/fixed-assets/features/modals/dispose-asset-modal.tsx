import React, { useState, useMemo } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Select, type SelectOption } from "../../../../components/primitives/select.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { useDisposeAssetMutation } from "../../hooks/use-fixed-assets-queries.ts";
import type { DisposeAssetRequest, DisposalType } from "../../types.ts";

export interface DisposeAssetModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly assetId: string | null;
  readonly assetName?: string;
  readonly currentNetBookValue?: number;
}

export function DisposeAssetModal({
  isOpen,
  onClose,
  legalEntityId,
  assetId,
  assetName,
  currentNetBookValue = 0
}: DisposeAssetModalProps): React.JSX.Element {
  const { t } = useTranslation("fixedAssets");
  const disposeMutation = useDisposeAssetMutation(legalEntityId);

  const [disposalType, setDisposalType] = useState<DisposalType>("Scrapped");
  const [disposalDate, setDisposalDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [proceedsAmount, setProceedsAmount] = useState("0");
  const [notes, setNotes] = useState("");
  const [error, setError] = useState<string | null>(null);

  const disposalOptions: SelectOption[] = [
    { value: "Scrapped", label: t("disposal.typeScrapped", "تخريد واستبعاد (Scrapped)") },
    { value: "Sold", label: t("disposal.typeSold", "بيع أصل رأسمالي (Sold)") },
    { value: "Lost", label: t("disposal.typeLost", "فقدان أو هلاك كلي (Lost)") },
    { value: "Donated", label: t("disposal.typeDonated", "هبة / تبرع (Donated)") }
  ];

  const numericProceeds = parseFloat(proceedsAmount) || 0;
  const gainOrLoss = useMemo(() => {
    return numericProceeds - currentNetBookValue;
  }, [numericProceeds, currentNetBookValue]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!assetId) return;
    setError(null);

    try {
      const payload: DisposeAssetRequest = {
        disposalType,
        disposalDate,
        proceedsAmount: numericProceeds,
        notes: notes.trim() || undefined
      };

      await disposeMutation.mutateAsync({ assetId, payload });
      onClose();
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : t("common.unknownError", "فشل استبعاد الأصل");
      setError(msg);
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("disposal.modalTitle", "استبعاد أو بيع أصل رأسمالي")}
      width="560px"
      data-testid="dispose-asset-modal"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {assetName && (
          <div
            style={{
              padding: "0.75rem",
              backgroundColor: "#f8fafc",
              border: "1px solid #cbd5e1",
              borderRadius: "0.375rem",
              fontSize: "0.875rem",
              color: "#334155"
            }}
          >
            <div><strong>{t("fields.asset", "الأصل المختار")}:</strong> {assetName}</div>
            <div style={{ marginTop: "0.25rem", color: "#64748b" }}>
              {t("fields.netBookValue", "صافي القيمة الدفترية الحالية")}: <strong>{currentNetBookValue.toLocaleString("ar-EG", { minimumFractionDigits: 2 })}</strong>
            </div>
          </div>
        )}

        {error && (
          <div
            data-testid="dispose-error"
            style={{
              padding: "0.75rem",
              borderRadius: "0.375rem",
              backgroundColor: "#fef2f2",
              border: "1px solid #f87171",
              color: "#991b1b",
              fontSize: "0.875rem"
            }}
          >
            {error}
          </div>
        )}

        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 500, marginBottom: "0.25rem", color: "#334155" }}>
            {t("fields.disposalType", "طبيعة الاستبعاد")} *
          </label>
          <Select
            options={disposalOptions}
            value={disposalType}
            onChange={(val) => setDisposalType(val as DisposalType)}
            data-testid="disposal-type-select"
          />
        </div>

        <TextField
          label={t("fields.disposalDate", "تاريخ الاستبعاد المحاسبي")}
          type="date"
          value={disposalDate}
          onChange={(e) => setDisposalDate(e.target.value)}
          required
          data-testid="disposal-date-input"
        />

        <TextField
          label={t("fields.proceedsAmount", "قيمة المتحصلات أو سعر البيع (إن وجد)")}
          type="number"
          step="0.01"
          value={proceedsAmount}
          onChange={(e) => setProceedsAmount(e.target.value)}
          data-testid="proceeds-amount-input"
        />

        {/* Financial gain/loss preview card */}
        <div
          data-testid="gain-loss-preview"
          style={{
            padding: "0.75rem",
            borderRadius: "0.375rem",
            backgroundColor: gainOrLoss >= 0 ? "#f0fdf4" : "#fef2f2",
            border: `1px solid ${gainOrLoss >= 0 ? "#86efac" : "#fca5a5"}`,
            fontSize: "0.875rem"
          }}
        >
          <div style={{ fontWeight: 600, color: gainOrLoss >= 0 ? "#15803d" : "#b91c1c" }}>
            {gainOrLoss >= 0
              ? t("disposal.gainOnDisposal", "أرباح رأسمالية متوقعة من البيع")
              : t("disposal.lossOnDisposal", "خسائر رأسمالية ناتجة عن الاستبعاد")}
            : {Math.abs(gainOrLoss).toLocaleString("ar-EG", { minimumFractionDigits: 2 })}
          </div>
          <div style={{ fontSize: "0.75rem", color: "#64748b", marginTop: "0.25rem" }}>
            {t("disposal.gainLossExplanation", "يتم إنشاء قيد استبعاد آلي في دفتر الأستاذ العام يقفل حساب الأصل ومجمع الإهلاك.")}
          </div>
        </div>

        <TextField
          label={t("fields.disposalNotes", "سبب الاستبعاد وملاحظات المحضر")}
          value={notes}
          onChange={(e) => setNotes(e.target.value)}
          placeholder={t("fields.disposalNotesPlaceholder", "بناء على قرار اللجنة الفنية للتخريد رقم...")}
          data-testid="disposal-notes-input"
        />

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "0.5rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("common.cancel", "إلغاء")}
          </Button>
          <Button variant="danger" type="submit" isLoading={disposeMutation.isPending} data-testid="submit-dispose-btn">
            {t("disposal.confirmDisposal", "تأكيد الاستبعاد وترحيل القيد")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

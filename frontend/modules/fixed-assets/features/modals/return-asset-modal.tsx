import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Select, type SelectOption } from "../../../../components/primitives/select.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import {
  useFixedAssetLocationsQuery,
  useReturnCustodyMutation
} from "../../hooks/use-fixed-assets-queries.ts";
import type { ReturnCustodyRequest } from "../../types.ts";

export interface ReturnAssetModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly assetId: string | null;
  readonly assetName?: string;
}

export function ReturnAssetModal({
  isOpen,
  onClose,
  legalEntityId,
  assetId,
  assetName
}: ReturnAssetModalProps): React.JSX.Element {
  const { t } = useTranslation("fixedAssets");
  const locationsQuery = useFixedAssetLocationsQuery(legalEntityId);
  const returnMutation = useReturnCustodyMutation(legalEntityId);

  const [targetLocationId, setTargetLocationId] = useState("");
  const [condition, setCondition] = useState("Good");
  const [handoverNote, setHandoverNote] = useState("");
  const [error, setError] = useState<string | null>(null);

  const locationOptions: SelectOption[] =
    locationsQuery.data?.items?.map((loc) => ({
      value: loc.id,
      label: `${loc.locationName} (${loc.building ?? ""})`
    })) ?? [];

  const conditionOptions: SelectOption[] = [
    { value: "Excellent", label: t("custody.conditionExcellent", "ممتازة (كالجديد)") },
    { value: "Good", label: t("custody.conditionGood", "جيدة (صالح للاستخدام)") },
    { value: "Fair", label: t("custody.conditionFair", "مقبولة (تظهر عليه علامات استهلاك)") },
    { value: "Damaged", label: t("custody.conditionDamaged", "تالف / بحاجة لصيانة") }
  ];

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!assetId) return;
    setError(null);

    try {
      const payload: ReturnCustodyRequest = {
        targetLocationId: targetLocationId || undefined,
        condition: condition.trim() || undefined,
        handoverNote: handoverNote.trim() || undefined
      };

      await returnMutation.mutateAsync({ assetId, payload });
      onClose();
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : t("common.unknownError", "فشل استرداد العهدة");
      setError(msg);
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("custody.returnModalTitle", "إرجاع الأصل إلى المستودع / إنهاء العهدة")}
      width="540px"
      data-testid="return-asset-modal"
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
            <strong>{t("fields.asset", "الأصل المختار")}:</strong> {assetName}
          </div>
        )}

        {error && (
          <div
            data-testid="return-error"
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
            {t("fields.returnLocation", "مستودع أو موقع الإرجاع")}
          </label>
          <Select
            options={locationOptions}
            value={targetLocationId}
            onChange={(val) => setTargetLocationId(val)}
            placeholder={t("fields.selectLocation", "اختر المستودع / الموقع...")}
            data-testid="return-location-select"
          />
        </div>

        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 500, marginBottom: "0.25rem", color: "#334155" }}>
            {t("fields.assetCondition", "الحالة الفنية للأصل عند الإرجاع")}
          </label>
          <Select
            options={conditionOptions}
            value={condition}
            onChange={(val) => setCondition(val)}
            data-testid="return-condition-select"
          />
        </div>

        <TextField
          label={t("fields.handoverNote", "ملاحظات الفحص والاستلام")}
          value={handoverNote}
          onChange={(e) => setHandoverNote(e.target.value)}
          placeholder={t("fields.returnNotePlaceholder", "تم استلام الأصل والملحقات بحالة جيدة...")}
          data-testid="return-note-input"
        />

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "0.5rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("common.cancel", "إلغاء")}
          </Button>
          <Button variant="primary" type="submit" isLoading={returnMutation.isPending} data-testid="submit-return-btn">
            {t("custody.submitReturn", "تأكيد إرجاع العهدة")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

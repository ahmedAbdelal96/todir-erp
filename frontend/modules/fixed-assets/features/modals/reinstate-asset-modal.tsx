import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { useReinstateAssetMutation } from "../../hooks/use-fixed-assets-queries.ts";
import type { ReinstateAssetRequest } from "../../types.ts";

export interface ReinstateAssetModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly assetId: string | null;
  readonly assetName?: string;
}

export function ReinstateAssetModal({
  isOpen,
  onClose,
  legalEntityId,
  assetId,
  assetName
}: ReinstateAssetModalProps): React.JSX.Element {
  const { t } = useTranslation("fixedAssets");
  const reinstateMutation = useReinstateAssetMutation(legalEntityId);

  const [reinstatementDate, setReinstatementDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [reason, setReason] = useState("");
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!assetId) return;
    setError(null);

    if (!reason.trim()) {
      setError(t("validation.reinstateReasonRequired", "يجب كتابة مبرر إعادة قيد الأصل للخدمة"));
      return;
    }

    try {
      const payload: ReinstateAssetRequest = {
        reinstatementDate,
        reason: reason.trim()
      };

      await reinstateMutation.mutateAsync({ assetId, payload });
      onClose();
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : t("common.unknownError", "فشل إعادة قيد الأصل");
      setError(msg);
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("disposal.reinstateModalTitle", "إعادة قيد الأصل واستئناف الخدمة")}
      width="520px"
      data-testid="reinstate-asset-modal"
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
            data-testid="reinstate-error"
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

        <div style={{ fontSize: "0.875rem", color: "#475569", lineHeight: 1.5 }}>
          {t("disposal.reinstateNotice", "تؤدي إعادة القيد إلى استرجاع حالة الأصل إلى 'نشط (Active)' واستئناف احتساب الإهلاك الدوري.")}
        </div>

        <TextField
          label={t("fields.reinstatementDate", "تاريخ إعادة القيد")}
          type="date"
          value={reinstatementDate}
          onChange={(e) => setReinstatementDate(e.target.value)}
          required
          data-testid="reinstatement-date-input"
        />

        <TextField
          label={t("fields.reinstatementReason", "مبرر ومستند إعادة القيد")}
          value={reason}
          onChange={(e) => setReason(e.target.value)}
          required
          placeholder={t("fields.reinstatePlaceholder", "العثور على الأصل المفقود / التراجع عن قرار التخريد بموجب محضر...")}
          data-testid="reinstatement-reason-input"
        />

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "0.5rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("common.cancel", "إلغاء")}
          </Button>
          <Button variant="primary" type="submit" isLoading={reinstateMutation.isPending} data-testid="submit-reinstate-btn">
            {t("disposal.confirmReinstate", "تأكيد إعادة القيد")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

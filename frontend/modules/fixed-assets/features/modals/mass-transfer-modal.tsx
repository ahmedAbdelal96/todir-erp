import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Select, type SelectOption } from "../../../../components/primitives/select.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import {
  useFixedAssetLocationsQuery,
  useMassTransferMutation
} from "../../hooks/use-fixed-assets-queries.ts";
import type { MassTransferCustodyRequest, FixedAssetDto } from "../../types.ts";

export interface MassTransferModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly selectedAssetIds: string[];
  readonly availableAssets: FixedAssetDto[];
}

export function MassTransferModal({
  isOpen,
  onClose,
  legalEntityId,
  selectedAssetIds,
  availableAssets
}: MassTransferModalProps): React.JSX.Element {
  const { t } = useTranslation("fixedAssets");
  const locationsQuery = useFixedAssetLocationsQuery(legalEntityId);
  const massTransferMutation = useMassTransferMutation(legalEntityId);

  const [targetCustodianId, setTargetCustodianId] = useState("");
  const [targetDepartment, setTargetDepartment] = useState("");
  const [targetLocationId, setTargetLocationId] = useState("");
  const [handoverNote, setHandoverNote] = useState("");
  const [error, setError] = useState<string | null>(null);

  const locationOptions: SelectOption[] =
    locationsQuery.data?.items?.map((loc) => ({
      id: loc.id,
      label: `${loc.locationName} (${loc.locationCode})`
    })) ?? [];

  const selectedAssets = availableAssets.filter((a) => selectedAssetIds.includes(a.id));

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    if (selectedAssetIds.length === 0) {
      setError(t("validation.selectAssetsRequired", "يجب تحديد أصل واحد على الأقل للنقل الجماعي"));
      return;
    }
    if (!targetCustodianId.trim() && !targetLocationId) {
      setError(t("validation.custodianOrLocationRequired", "يجب تحديد الموظف المستلم أو الموقع الجديد"));
      return;
    }

    try {
      const payload: MassTransferCustodyRequest = {
        assetIds: selectedAssetIds,
        targetCustodianId: targetCustodianId.trim() || undefined,
        targetDepartment: targetDepartment.trim() || undefined,
        targetLocationId: targetLocationId || undefined,
        handoverNote: handoverNote.trim() || undefined
      };

      await massTransferMutation.mutateAsync(payload);
      onClose();
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : t("common.unknownError", "فشل النقل الجماعي للعهدة");
      setError(msg);
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("custody.massTransferModalTitle", "نقل جماعي للعهدة والمواقع (Mass Transfer)")}
      size="medium"
      data-testid="mass-transfer-modal"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {error && (
          <div data-testid="mass-transfer-error" style={{ padding: "0.75rem", backgroundColor: "#fef2f2", color: "#991b1b", borderRadius: "0.375rem" }}>
            {error}
          </div>
        )}

        <div style={{ padding: "0.75rem", backgroundColor: "#f8fafc", borderRadius: "0.375rem", border: "1px solid #cbd5e1" }}>
          <div style={{ fontWeight: 600, fontSize: "0.875rem", color: "#1e293b", marginBottom: "0.5rem" }}>
            {t("custody.selectedAssetsCount", "عدد الأصول المحددة للنقل")}: {selectedAssetIds.length}
          </div>
          <div style={{ maxHeight: "120px", overflowY: "auto", fontSize: "0.8125rem", color: "#64748b" }}>
            {selectedAssets.map((a) => (
              <div key={a.id} style={{ display: "flex", justifyContent: "space-between", padding: "0.15rem 0" }}>
                <span>{a.assetName} ({a.assetCode})</span>
                <span>{a.department ?? "-"}</span>
              </div>
            ))}
          </div>
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <TextField
            label={t("fields.targetCustodian", "الموظف المستلم للعهدة")}
            value={targetCustodianId}
            onChange={setTargetCustodianId}
            placeholder={t("fields.custodianPlaceholder", "EMP-1024")}
            data-testid="mass-target-custodian-input"
          />
          <TextField
            label={t("fields.targetDepartment", "القسم الجديد")}
            value={targetDepartment}
            onChange={setTargetDepartment}
            placeholder={t("fields.deptPlaceholder", "إدارة التسويق")}
            data-testid="mass-target-dept-input"
          />
        </div>

        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 500, marginBottom: "0.25rem", color: "#334155" }}>
            {t("fields.targetLocation", "الموقع الجغرافي الجديد")}
          </label>
          <Select
            options={locationOptions}
            selectedKey={targetLocationId || null}
            onSelectionChange={(k) => setTargetLocationId(String(k ?? ""))}
            placeholder={t("fields.selectLocation", "اختر الموقع...")}
            data-testid="mass-target-location-select"
          />
        </div>

        <TextField
          label={t("fields.handoverNote", "ملاحظات إذن التسليم والتسلم الجماعي")}
          value={handoverNote}
          onChange={setHandoverNote}
          placeholder={t("fields.massTransferNotePlaceholder", "نقل عهدة مكاتب الإدارة إلى المقر الفرعي الجديد...")}
          data-testid="mass-handover-note-input"
        />

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "0.5rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("common.cancel", "إلغاء")}
          </Button>
          <Button variant="primary" type="submit" isLoading={massTransferMutation.isPending} data-testid="submit-mass-transfer-btn">
            {t("custody.executeMassTransfer", "تنفيذ النقل الجماعي")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Select, type SelectOption } from "../../../../components/primitives/select.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import {
  useFixedAssetLocationsQuery,
  useTransferCustodyMutation
} from "../../hooks/use-fixed-assets-queries.ts";
import type { TransferCustodyRequest } from "../../types.ts";

export interface AssignTransferModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly assetId: string | null;
  readonly assetName?: string;
}

export function AssignTransferModal({
  isOpen,
  onClose,
  legalEntityId,
  assetId,
  assetName
}: AssignTransferModalProps): React.JSX.Element {
  const { t } = useTranslation("fixedAssets");
  const locationsQuery = useFixedAssetLocationsQuery(legalEntityId);
  const transferMutation = useTransferCustodyMutation(legalEntityId);

  const [targetCustodianId, setTargetCustodianId] = useState("");
  const [targetDepartment, setTargetDepartment] = useState("");
  const [targetLocationId, setTargetLocationId] = useState("");
  const [targetRoom, setTargetRoom] = useState("");
  const [handoverNote, setHandoverNote] = useState("");
  const [error, setError] = useState<string | null>(null);

  const locationOptions: SelectOption[] =
    locationsQuery.data?.items?.map((loc) => ({
      value: loc.id,
      label: `${loc.locationName} (${loc.building ?? ""})`
    })) ?? [];

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!assetId) return;
    setError(null);

    if (!targetCustodianId.trim() && !targetLocationId) {
      setError(t("validation.custodianOrLocationRequired", "يجب تحديد الموظف المستلم أو الموقع الجديد"));
      return;
    }

    try {
      const payload: TransferCustodyRequest = {
        targetCustodianId: targetCustodianId.trim() || undefined,
        targetDepartment: targetDepartment.trim() || undefined,
        targetLocationId: targetLocationId || undefined,
        targetRoom: targetRoom.trim() || undefined,
        handoverNote: handoverNote.trim() || undefined
      };

      await transferMutation.mutateAsync({ assetId, payload });
      onClose();
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : t("common.unknownError", "فشل نقل العهدة");
      setError(msg);
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("custody.transferModalTitle", "نقل عهدة وموقع الأصل")}
      width="540px"
      data-testid="assign-transfer-modal"
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
            data-testid="transfer-error"
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

        <TextField
          label={t("fields.targetCustodian", "الموظف المستلم للعهدة (معرّف الموظف)")}
          value={targetCustodianId}
          onChange={(e) => setTargetCustodianId(e.target.value)}
          placeholder="EMP-1024 / UUID"
          data-testid="target-custodian-input"
        />

        <TextField
          label={t("fields.targetDepartment", "القسم / الإدارة الجديدة")}
          value={targetDepartment}
          onChange={(e) => setTargetDepartment(e.target.value)}
          placeholder={t("fields.deptPlaceholder", "مثال: قسم تقنية المعلومات")}
          data-testid="target-department-input"
        />

        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 500, marginBottom: "0.25rem", color: "#334155" }}>
            {t("fields.targetLocation", "الموقع الجغرافي الجديد")}
          </label>
          <Select
            options={locationOptions}
            value={targetLocationId}
            onChange={(val) => setTargetLocationId(val)}
            placeholder={t("fields.selectLocation", "اختر الموقع...")}
            data-testid="target-location-select"
          />
        </div>

        <TextField
          label={t("fields.targetRoom", "الغرفة / المكتب / الرف")}
          value={targetRoom}
          onChange={(e) => setTargetRoom(e.target.value)}
          placeholder={t("fields.roomPlaceholder", "مكتب 302 - الدور الثالث")}
          data-testid="target-room-input"
        />

        <TextField
          label={t("fields.handoverNote", "ملاحظات إذن التسليم والتسلم")}
          value={handoverNote}
          onChange={(e) => setHandoverNote(e.target.value)}
          placeholder={t("fields.handoverNotePlaceholder", "تسليم بموجب محضر استلام عمل...")}
          data-testid="handover-note-input"
        />

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "0.5rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("common.cancel", "إلغاء")}
          </Button>
          <Button variant="primary" type="submit" isLoading={transferMutation.isPending} data-testid="submit-transfer-btn">
            {t("custody.submitTransfer", "تأكيد نقل العهدة")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { useAttendanceCorrectionMutation } from "../../hooks/use-hr-queries.ts";
import { mapHrError } from "../../services/hr-error-map.ts";
import type { AttendanceRecordView } from "../../types.ts";

export interface AttendanceCorrectionModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly record: AttendanceRecordView | null;
  readonly legalEntityId: string;
  readonly onCorrected?: () => void;
}

export function AttendanceCorrectionModal({
  isOpen,
  onClose,
  record,
  legalEntityId,
  onCorrected
}: AttendanceCorrectionModalProps): React.JSX.Element {
  const { t } = useTranslation("hr");
  const correctMutation = useAttendanceCorrectionMutation(legalEntityId);

  const [checkoutTime, setCheckoutTime] = useState("17:00:00");
  const [reason, setReason] = useState("");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const reset = () => {
    setCheckoutTime("17:00:00");
    setReason("");
    setErrorMessage(null);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!record) return;

    if (!reason.trim()) {
      setErrorMessage(t("errors.attendanceCorrectionReasonRequired"));
      return;
    }

    try {
      const fullIsoOut = `${record.workDate}T${checkoutTime}Z`;
      await correctMutation.mutateAsync({
        recordId: record.id,
        request: {
          expectedVersion: record.version,
          firstIn: record.firstIn ?? null,
          lastOut: fullIsoOut,
          reasonCode: "ManualPunchMissing",
          reason: reason.trim()
        }
      });

      reset();
      onClose();
      if (onCorrected) onCorrected();
    } catch (err: unknown) {
      const mapped = mapHrError(err);
      setErrorMessage(t(mapped.messageKey, mapped.defaultMessage));
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open: boolean) => {
        if (!open) {
          reset();
          onClose();
        }
      }}
      title={t("attendance.correctionModal.title")}
      size="medium"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {errorMessage && (
          <div
            role="alert"
            style={{
              padding: "0.75rem",
              borderRadius: "var(--radius-sm)",
              background: "var(--color-status-danger-subtle, #fee2e2)",
              color: "var(--color-status-danger, #b91c1c)",
              fontSize: "0.875rem"
            }}
          >
            {errorMessage}
          </div>
        )}

        <p style={{ margin: 0, fontSize: "0.875rem", color: "var(--color-content-muted)" }}>
          {t("attendance.correctionModal.recordDate")}: <strong>{record?.workDate}</strong>
        </p>

        <TextField
          label={t("attendance.correctionModal.checkoutTime")}
          value={checkoutTime}
          onChange={setCheckoutTime}
          isRequired
        />

        <TextField
          label={t("attendance.correctionModal.reason")}
          value={reason}
          onChange={setReason}
          isRequired
          placeholder={t("attendance.correctionModal.reasonPlaceholder")}
        />

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "1rem", borderTop: "1px solid #e2e8f0", paddingTop: "1rem" }}>
          <Button variant="secondary" onPress={() => { reset(); onClose(); }} isDisabled={correctMutation.isPending}>
            {t("actions.cancel")}
          </Button>
          <Button variant="primary" type="submit" isDisabled={correctMutation.isPending}>
            {correctMutation.isPending ? t("actions.saving") : t("actions.confirm")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

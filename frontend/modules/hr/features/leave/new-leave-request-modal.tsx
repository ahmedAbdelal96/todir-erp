import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { LocalDateField } from "../../../../components/fields/local-date-field.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Select } from "../../../../components/primitives/select.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { useLeaveTypes, useCreateLeaveRequestMutation } from "../../hooks/use-hr-queries.ts";
import { mapHrError } from "../../services/hr-error-map.ts";

export interface NewLeaveRequestModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly employmentId: string;
  readonly legalEntityId: string;
  readonly onCreated?: () => void;
}

export function NewLeaveRequestModal({
  isOpen,
  onClose,
  employmentId,
  legalEntityId,
  onCreated
}: NewLeaveRequestModalProps): React.JSX.Element {
  const { t } = useTranslation("hr");
  const leaveTypesQuery = useLeaveTypes(legalEntityId);
  const leaveTypes = leaveTypesQuery.data ?? [];

  const createLeaveMutation = useCreateLeaveRequestMutation(legalEntityId, employmentId);

  const [leaveTypeId, setLeaveTypeId] = useState("");
  const [fromDate, setFromDate] = useState(new Date().toISOString().slice(0, 10));
  const [toDate, setToDate] = useState(new Date().toISOString().slice(0, 10));
  const [reason, setReason] = useState("");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const reset = () => {
    setLeaveTypeId("");
    setFromDate(new Date().toISOString().slice(0, 10));
    setToDate(new Date().toISOString().slice(0, 10));
    setReason("");
    setErrorMessage(null);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const effectiveLeaveTypeId = leaveTypeId || (leaveTypes[0]?.id ?? "");
    if (!effectiveLeaveTypeId) {
      setErrorMessage(t("leave.typeRequired"));
      return;
    }

    try {
      await createLeaveMutation.mutateAsync({
        legalEntityId,
        leaveTypeId: effectiveLeaveTypeId,
        from: fromDate,
        to: toDate,
        reason: reason.trim() || null,
        idempotencyKey: `leave-req-${Date.now()}`
      });

      reset();
      onClose();
      if (onCreated) onCreated();
    } catch (err: unknown) {
      const mapped = mapHrError(err);
      setErrorMessage(t(mapped.messageKey, mapped.defaultMessage));
    }
  };

  const leaveOptions = leaveTypes.map((lt) => ({
    id: lt.id,
    label: `${lt.nameAr} (${lt.category})`
  }));

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open: boolean) => {
        if (!open) {
          reset();
          onClose();
        }
      }}
      title={t("leave.newModal.title")}
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

        <Select
          label={t("leave.newModal.leaveType")}
          selectedKey={leaveTypeId || (leaveTypes[0]?.id ?? "")}
          onSelectionChange={(k) => {
            if (k) setLeaveTypeId(String(k));
          }}
          options={leaveOptions}
          isRequired
        />

        <LocalDateField
          label={t("leave.newModal.fromDate")}
          value={fromDate}
          onChange={setFromDate}
          isRequired
        />

        <LocalDateField
          label={t("leave.newModal.toDate")}
          value={toDate}
          onChange={setToDate}
          isRequired
        />

        <TextField
          label={t("leave.newModal.reasonOptional")}
          value={reason}
          onChange={setReason}
          placeholder={t("leave.newModal.reasonPlaceholder")}
        />

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "1rem", borderTop: "1px solid #e2e8f0", paddingTop: "1rem" }}>
          <Button variant="secondary" onPress={() => { reset(); onClose(); }} isDisabled={createLeaveMutation.isPending}>
            {t("actions.cancel")}
          </Button>
          <Button variant="primary" type="submit" isDisabled={createLeaveMutation.isPending}>
            {createLeaveMutation.isPending ? t("actions.saving") : t("actions.confirm")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

import React, { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { useUpdateEmployeeMutation } from "../../hooks/use-hr-queries.ts";
import { mapHrError } from "../../services/hr-error-map.ts";
import type { EmployeeProfile } from "../../types.ts";

export interface EmployeeProfileEditModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly employee: EmployeeProfile;
  readonly legalEntityId?: string | undefined;
}

export function EmployeeProfileEditModal({ isOpen, onClose, employee, legalEntityId }: EmployeeProfileEditModalProps): React.JSX.Element {
  const { t } = useTranslation("hr");
  const updateMutation = useUpdateEmployeeMutation(employee.id, legalEntityId);
  const [arabicName, setArabicName] = useState(employee.arabicName);
  const [englishName, setEnglishName] = useState(employee.englishName ?? "");
  const [preferredName, setPreferredName] = useState(employee.preferredName ?? "");
  const [reason, setReason] = useState("");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  useEffect(() => {
    if (!isOpen) return;
    setArabicName(employee.arabicName);
    setEnglishName(employee.englishName ?? "");
    setPreferredName(employee.preferredName ?? "");
    setReason("");
    setErrorMessage(null);
  }, [employee, isOpen]);

  const handleSubmit = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!arabicName.trim() || !reason.trim()) {
      setErrorMessage(!arabicName.trim() ? t("errors.employeeArabicNameRequired") : t("errors.employeeProfileReasonRequired"));
      return;
    }
    try {
      await updateMutation.mutateAsync({
        arabicName: arabicName.trim(),
        englishName: englishName.trim() || null,
        preferredName: preferredName.trim() || null,
        expectedVersion: employee.version,
        reason: reason.trim()
      });
      onClose();
    } catch (error: unknown) {
      const mapped = mapHrError(error);
      setErrorMessage(t(mapped.messageKey, mapped.defaultMessage));
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open: boolean) => { if (!open && !updateMutation.isPending) onClose(); }}
      title={t("workspace.profileEdit.title")}
      size="medium"
      footer={
        <>
          <Button variant="secondary" onPress={onClose} isDisabled={updateMutation.isPending}>{t("actions.cancel")}</Button>
          <Button variant="primary" type="submit" form="employee-profile-edit-form" isDisabled={updateMutation.isPending}>
            {updateMutation.isPending ? t("actions.saving") : t("workspace.profileEdit.submit")}
          </Button>
        </>
      }
    >
      <form id="employee-profile-edit-form" onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "0.875rem" }}>
        {errorMessage && <div role="alert" style={{ padding: "0.625rem 0.875rem", borderRadius: "var(--radius-sm)", background: "var(--color-status-danger-subtle, #fee2e2)", color: "var(--color-status-danger, #b91c1c)", fontSize: "0.8125rem" }}>{errorMessage}</div>}
        <TextField label={t("workspace.profileEdit.arabicName")} value={arabicName} onChange={setArabicName} isRequired />
        <TextField label={t("workspace.profileEdit.englishName")} value={englishName} onChange={setEnglishName} inputDir="ltr" />
        <TextField label={t("workspace.profileEdit.preferredName")} value={preferredName} onChange={setPreferredName} />
        <TextField label={t("workspace.profileEdit.reason")} value={reason} onChange={setReason} isRequired />
      </form>
    </Dialog>
  );
}

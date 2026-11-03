import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Select } from "../../../../components/primitives/select.tsx";
import { LocalDateField } from "../../../../components/fields/local-date-field.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { useCreateEmployeeMutation } from "../../hooks/use-hr-queries.ts";
import { mapHrError } from "../../services/hr-error-map.ts";
import type { EmploymentType } from "../../types.ts";

export interface NewEmployeeModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly onCreated?: (employeeId: string) => void;
}

export function NewEmployeeModal({
  isOpen,
  onClose,
  legalEntityId,
  onCreated
}: NewEmployeeModalProps): React.JSX.Element {
  const { t } = useTranslation("hr");
  const createMutation = useCreateEmployeeMutation();

  const [arabicName, setArabicName] = useState("");
  const [englishName, setEnglishName] = useState("");
  const [preferredName, setPreferredName] = useState("");
  const [employmentType, setEmploymentType] = useState<EmploymentType>("FullTime");
  const [startDate, setStartDate] = useState(new Date().toISOString().slice(0, 10));
  const [endDate, setEndDate] = useState("");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const reset = () => {
    setArabicName("");
    setEnglishName("");
    setPreferredName("");
    setEmploymentType("FullTime");
    setStartDate(new Date().toISOString().slice(0, 10));
    setEndDate("");
    setErrorMessage(null);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!arabicName.trim()) {
      setErrorMessage(t("errors.employeeArabicNameRequired"));
      return;
    }

    try {
      const created = await createMutation.mutateAsync({
        arabicName: arabicName.trim(),
        englishName: englishName.trim() || null,
        preferredName: preferredName.trim() || null,
        legalEntityId,
        employmentType,
        startDate: startDate || null,
        endDate: endDate.trim() || null
      });

      reset();
      onClose();
      if (onCreated && created.id) {
        onCreated(created.id);
      }
    } catch (err: unknown) {
      const mapped = mapHrError(err);
      setErrorMessage(t(mapped.messageKey, mapped.defaultMessage));
    }
  };

  const employmentTypeOptions = [
    { id: "FullTime", label: t("employmentType.FullTime") },
    { id: "PartTime", label: t("employmentType.PartTime") },
    { id: "Temporary", label: t("employmentType.Temporary") },
    { id: "FixedTerm", label: t("employmentType.FixedTerm") },
    { id: "Probationary", label: t("employmentType.Probationary") },
    { id: "Intern", label: t("employmentType.Intern") }
  ];

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open: boolean) => {
        if (!open) {
          reset();
          onClose();
        }
      }}
      title={t("directory.newEmployeeModal.title")}
      size="medium"
      footer={
        <>
          <Button
            variant="secondary"
            onPress={() => { reset(); onClose(); }}
            isDisabled={createMutation.isPending}
          >
            {t("actions.cancel")}
          </Button>
          <Button
            variant="primary"
            type="submit"
            form="new-employee-form"
            isDisabled={createMutation.isPending}
          >
            {createMutation.isPending ? t("actions.saving") : t("directory.newEmployeeModal.submit")}
          </Button>
        </>
      }
    >
      <form id="new-employee-form" onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "0.875rem" }}>
        {errorMessage && (
          <div
            role="alert"
            style={{
              padding: "0.625rem 0.875rem",
              borderRadius: "var(--radius-sm)",
              background: "var(--color-status-danger-subtle, #fee2e2)",
              color: "var(--color-status-danger, #b91c1c)",
              fontSize: "0.8125rem"
            }}
          >
            {errorMessage}
          </div>
        )}

        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", gap: "0.75rem" }}>
          <TextField
            label={t("directory.newEmployeeModal.arabicName")}
            value={arabicName}
            onChange={setArabicName}
            isRequired
            placeholder={t("directory.newEmployeeModal.arabicNamePlaceholder")}
          />

          <TextField
            label={t("directory.newEmployeeModal.englishName")}
            value={englishName}
            onChange={setEnglishName}
            placeholder={t("directory.newEmployeeModal.englishNamePlaceholder")}
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", gap: "0.75rem" }}>
          <TextField
            label={t("directory.newEmployeeModal.preferredName")}
            value={preferredName}
            onChange={setPreferredName}
            placeholder={t("directory.newEmployeeModal.preferredNamePlaceholder")}
          />

          <Select
            label={t("directory.newEmployeeModal.employmentType")}
            selectedKey={employmentType}
            onSelectionChange={(val) => {
              if (val) setEmploymentType(String(val) as EmploymentType);
            }}
            options={employmentTypeOptions}
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", gap: "0.75rem" }}>
          <LocalDateField
            label={t("directory.newEmployeeModal.startDate")}
            value={startDate}
            onChange={setStartDate}
            isRequired
          />

          <LocalDateField
            label={t("directory.newEmployeeModal.endDateOptional")}
            value={endDate}
            onChange={setEndDate}
          />
        </div>
      </form>
    </Dialog>
  );
}

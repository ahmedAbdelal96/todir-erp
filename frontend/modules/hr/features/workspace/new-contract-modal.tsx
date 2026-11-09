import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { LocalDateField } from "../../../../components/fields/local-date-field.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Select } from "../../../../components/primitives/select.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { useCreateContractMutation, useActivateContractMutation } from "../../hooks/use-hr-queries.ts";
import { mapHrError } from "../../services/hr-error-map.ts";

export interface NewContractModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly employmentId: string;
  readonly legalEntityId: string;
  readonly onCreated?: () => void;
}

export function NewContractModal({
  isOpen,
  onClose,
  employmentId,
  legalEntityId,
  onCreated
}: NewContractModalProps): React.JSX.Element {
  const { t } = useTranslation("hr");
  const createContractMutation = useCreateContractMutation(employmentId, legalEntityId);
  const activateContractMutation = useActivateContractMutation(employmentId, legalEntityId);

  const [contractType, setContractType] = useState("Definite");
  const [startDate, setStartDate] = useState(new Date().toISOString().slice(0, 10));
  const [endDate, setEndDate] = useState("");
  const [probationEnd, setProbationEnd] = useState("");
  // Keep the technical default in the API contract, but do not expose its
  // internal code to HR users. An empty field means the standard hours basis.
  const [workingHoursBasis, setWorkingHoursBasis] = useState("");
  const [autoActivate, setAutoActivate] = useState(true);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const reset = () => {
    setContractType("Definite");
    setStartDate(new Date().toISOString().slice(0, 10));
    setEndDate("");
    setProbationEnd("");
    setWorkingHoursBasis("");
    setAutoActivate(true);
    setErrorMessage(null);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      const created = await createContractMutation.mutateAsync({
        legalEntityId,
        contractType,
        startDate: startDate || null,
        endDate: endDate || null,
        probationEnd: probationEnd || null,
        workingHoursBasis: workingHoursBasis.trim() || "FullTimeStandard"
      });

      if (autoActivate && created.id) {
        await activateContractMutation.mutateAsync({
          contractId: created.id,
          version: created.version
        });
      }

      reset();
      onClose();
      if (onCreated) onCreated();
    } catch (err: unknown) {
      const mapped = mapHrError(err);
      setErrorMessage(t(mapped.messageKey, mapped.defaultMessage));
    }
  };

  const contractTypeOptions = [
    { id: "Definite", label: t("workspace.contracts.types.Definite") },
    { id: "Indefinite", label: t("workspace.contracts.types.Indefinite") },
    { id: "ProjectBased", label: t("workspace.contracts.types.ProjectBased") },
    { id: "Seasonal", label: t("workspace.contracts.types.Seasonal") }
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
      title={t("workspace.contracts.newModalTitle")}
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
          label={t("workspace.contracts.type")}
          selectedKey={contractType}
          onSelectionChange={(k) => {
            if (k) setContractType(String(k));
          }}
          options={contractTypeOptions}
        />

        <LocalDateField
          label={t("workspace.contracts.startDate")}
          value={startDate}
          onChange={setStartDate}
          isRequired
        />

        <LocalDateField
          label={t("workspace.contracts.endDate")}
          value={endDate}
          onChange={setEndDate}
        />

        <LocalDateField
          label={t("workspace.contracts.probationEndOptional")}
          value={probationEnd}
          onChange={setProbationEnd}
        />

        <TextField
          label={t("workspace.contracts.workingHoursBasis")}
          description={t("workspace.contracts.workingHoursBasisDescription")}
          placeholder={t("workspace.contracts.workingHoursBasisPlaceholder")}
          value={workingHoursBasis}
          onChange={setWorkingHoursBasis}
        />

        <label style={{ display: "flex", alignItems: "center", gap: "0.5rem", fontSize: "0.875rem", cursor: "pointer" }}>
          <input
            type="checkbox"
            checked={autoActivate}
            onChange={(e) => setAutoActivate(e.target.checked)}
          />
          <span>{t("workspace.contracts.autoActivateLabel")}</span>
        </label>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "1rem", borderTop: "1px solid #e2e8f0", paddingTop: "1rem" }}>
          <Button variant="secondary" onPress={() => { reset(); onClose(); }} isDisabled={createContractMutation.isPending}>
            {t("actions.cancel")}
          </Button>
          <Button variant="primary" type="submit" isDisabled={createContractMutation.isPending}>
            {createContractMutation.isPending ? t("actions.saving") : t("actions.confirm")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

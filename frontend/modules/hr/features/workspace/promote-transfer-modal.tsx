import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { LocalDateField } from "../../../../components/fields/local-date-field.tsx";
import { Select } from "../../../../components/primitives/select.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { HrDepartmentPicker } from "../../components/hr-department-picker.tsx";
import { HrPositionPicker } from "../../components/hr-position-picker.tsx";
import { HrEmployeePicker } from "../../components/hr-employee-picker.tsx";
import { useCreateAssignmentMutation } from "../../hooks/use-hr-queries.ts";
import { mapHrError } from "../../services/hr-error-map.ts";

export interface PromoteTransferModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly employmentId: string;
  readonly legalEntityId: string;
  readonly isPromotion?: boolean | undefined;
}

export function PromoteTransferModal({
  isOpen,
  onClose,
  employmentId,
  legalEntityId,
  isPromotion = true
}: PromoteTransferModalProps): React.JSX.Element {
  const { t } = useTranslation("hr");
  const createAssignmentMutation = useCreateAssignmentMutation(employmentId, legalEntityId);

  const [departmentId, setDepartmentId] = useState("");
  const [positionId, setPositionId] = useState("");
  const [managerEmploymentId, setManagerEmploymentId] = useState("");
  const [effectiveFrom, setEffectiveFrom] = useState(new Date().toISOString().slice(0, 10));
  const [effectiveTo, setEffectiveTo] = useState("");
  const [reasonCode, setReasonCode] = useState(isPromotion ? "Promotion" : "Transfer");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const reset = () => {
    setDepartmentId("");
    setPositionId("");
    setManagerEmploymentId("");
    setEffectiveFrom(new Date().toISOString().slice(0, 10));
    setEffectiveTo("");
    setReasonCode(isPromotion ? "Promotion" : "Transfer");
    setErrorMessage(null);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!effectiveFrom) {
      setErrorMessage(t("errors.assignmentDateInvalid"));
      return;
    }

    try {
      await createAssignmentMutation.mutateAsync({
        legalEntityId,
        departmentId: departmentId || null,
        positionId: positionId || null,
        managerEmploymentId: managerEmploymentId || null,
        effectiveFrom,
        effectiveTo: effectiveTo || null,
        reasonCode
      });
      reset();
      onClose();
    } catch (err: unknown) {
      const mapped = mapHrError(err);
      setErrorMessage(t(mapped.messageKey, mapped.defaultMessage));
    }
  };

  const reasonOptions = [
    { id: "Promotion", label: t("workspace.assignment.reasons.Promotion") },
    { id: "Transfer", label: t("workspace.assignment.reasons.Transfer") },
    { id: "DepartmentRestructure", label: t("workspace.assignment.reasons.Restructure") },
    { id: "InitialHiring", label: t("workspace.assignment.reasons.InitialHiring") }
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
      title={isPromotion ? t("workspace.assignment.promotionModalTitle") : t("workspace.assignment.transferModalTitle")}
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
          label={t("workspace.assignment.reasonCode")}
          selectedKey={reasonCode}
          onSelectionChange={(k) => {
            if (k) setReasonCode(String(k));
          }}
          options={reasonOptions}
        />

        <HrDepartmentPicker
          label={t("workspace.assignment.department")}
          value={departmentId}
          onChange={setDepartmentId}
          legalEntityId={legalEntityId}
        />

        <HrPositionPicker
          label={t("workspace.assignment.position")}
          value={positionId}
          onChange={setPositionId}
          legalEntityId={legalEntityId}
        />

        <HrEmployeePicker
          label={t("workspace.assignment.manager")}
          value={managerEmploymentId}
          onChange={setManagerEmploymentId}
          legalEntityId={legalEntityId}
        />

        <LocalDateField
          label={t("workspace.assignment.effectiveFrom")}
          value={effectiveFrom}
          onChange={setEffectiveFrom}
          isRequired
        />

        <LocalDateField
          label={t("workspace.assignment.effectiveToOptional")}
          value={effectiveTo}
          onChange={setEffectiveTo}
        />

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "1rem", borderTop: "1px solid #e2e8f0", paddingTop: "1rem" }}>
          <Button variant="secondary" onPress={() => { reset(); onClose(); }} isDisabled={createAssignmentMutation.isPending}>
            {t("actions.cancel")}
          </Button>
          <Button variant="primary" type="submit" isDisabled={createAssignmentMutation.isPending}>
            {createAssignmentMutation.isPending ? t("actions.saving") : t("actions.confirm")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

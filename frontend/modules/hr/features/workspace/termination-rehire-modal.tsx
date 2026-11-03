import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { LocalDateField } from "../../../../components/fields/local-date-field.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Select } from "../../../../components/primitives/select.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { useChangeEmploymentStatusMutation, useCreateEmploymentMutation } from "../../hooks/use-hr-queries.ts";
import { mapHrError } from "../../services/hr-error-map.ts";
import type { EmploymentView, EmploymentType } from "../../types.ts";

export interface TerminationRehireModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly employeeId: string;
  readonly legalEntityId: string;
  readonly currentEmployment: EmploymentView | null;
  readonly mode: "terminate" | "rehire";
  readonly onDone?: () => void;
}

export function TerminationRehireModal({
  isOpen,
  onClose,
  employeeId,
  legalEntityId,
  currentEmployment,
  mode,
  onDone
}: TerminationRehireModalProps): React.JSX.Element {
  const { t } = useTranslation("hr");
  const changeStatusMutation = useChangeEmploymentStatusMutation(employeeId, currentEmployment?.id ?? "", legalEntityId);
  const createEmploymentMutation = useCreateEmploymentMutation(employeeId, legalEntityId);

  // Terminate states
  const [effectiveDate, setEffectiveDate] = useState(new Date().toISOString().slice(0, 10));
  const [reasonCode, setReasonCode] = useState("Resignation");

  // Rehire states: must strictly follow prior termination date
  const getTomorrow = () => {
    const d = new Date();
    d.setDate(d.getDate() + 1);
    return d.toISOString().slice(0, 10);
  };
  const [rehireStartDate, setRehireStartDate] = useState(getTomorrow());
  const [rehireType, setRehireType] = useState<EmploymentType>("FullTime");
  const [rehireNumber, setRehireNumber] = useState("");

  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const reset = () => {
    setEffectiveDate(new Date().toISOString().slice(0, 10));
    setReasonCode("Resignation");
    setRehireStartDate(getTomorrow());
    setRehireType("FullTime");
    setRehireNumber("");
    setErrorMessage(null);
  };

  const mapOffboardingReasonType = (code: string): string => {
    switch (code) {
      case "Resignation":
        return "Resignation";
      case "Dismissal":
      case "MutualAgreement":
        return "Termination";
      case "ContractExpiration":
        return "ContractExpiry";
      case "Retirement":
        return "Retirement";
      default:
        return "Other";
    }
  };

  const handleTerminate = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!currentEmployment) return;

    try {
      await changeStatusMutation.mutateAsync({
        newStatus: "Terminated",
        effectiveOn: effectiveDate,
        reasonCode,
        expectedVersion: currentEmployment.version,
        offboarding: {
          reasonType: mapOffboardingReasonType(reasonCode),
          lastWorkingDate: effectiveDate,
          finalPayrollRequired: true,
          accessDeactivationTiming: "LastWorkingDay"
        }
      });
      reset();
      onClose();
      if (onDone) onDone();
    } catch (err: unknown) {
      const mapped = mapHrError(err);
      setErrorMessage(t(mapped.messageKey, mapped.defaultMessage));
    }
  };

  const handleRehire = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      await createEmploymentMutation.mutateAsync({
        legalEntityId,
        employmentNumber: rehireNumber.trim() || null,
        type: rehireType,
        startDate: rehireStartDate
      });
      reset();
      onClose();
      if (onDone) onDone();
    } catch (err: unknown) {
      const mapped = mapHrError(err);
      setErrorMessage(t(mapped.messageKey, mapped.defaultMessage));
    }
  };

  const terminationReasonOptions = [
    { id: "Resignation", label: t("workspace.termination.reasons.Resignation") },
    { id: "MutualAgreement", label: t("workspace.termination.reasons.MutualAgreement") },
    { id: "ContractExpiration", label: t("workspace.termination.reasons.ContractExpiration") },
    { id: "Dismissal", label: t("workspace.termination.reasons.Dismissal") },
    { id: "Retirement", label: t("workspace.termination.reasons.Retirement") }
  ];

  const employmentTypeOptions = [
    { id: "FullTime", label: t("employmentType.FullTime") },
    { id: "PartTime", label: t("employmentType.PartTime") },
    { id: "Temporary", label: t("employmentType.Temporary") },
    { id: "FixedTerm", label: t("employmentType.FixedTerm") }
  ];

  const isPending = changeStatusMutation.isPending || createEmploymentMutation.isPending;

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open: boolean) => {
        if (!open) {
          reset();
          onClose();
        }
      }}
      title={mode === "terminate" ? t("workspace.termination.modalTitle") : t("workspace.rehire.modalTitle")}
      size="medium"
    >
      <form onSubmit={mode === "terminate" ? handleTerminate : handleRehire} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
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

        {mode === "terminate" ? (
          <>
            <p style={{ margin: 0, fontSize: "0.875rem", color: "var(--color-content-muted)" }}>
              {t("workspace.termination.disclaimer")}
            </p>

            <Select
              label={t("workspace.termination.reason")}
              selectedKey={reasonCode}
              onSelectionChange={(k) => {
                if (k) setReasonCode(String(k));
              }}
              options={terminationReasonOptions}
            />

            <LocalDateField
              label={t("workspace.termination.effectiveDate")}
              value={effectiveDate}
              onChange={setEffectiveDate}
              isRequired
            />
          </>
        ) : (
          <>
            <p style={{ margin: 0, fontSize: "0.875rem", color: "var(--color-content-muted)" }}>
              {t("workspace.rehire.disclaimer")}
            </p>

            <TextField
              label={t("workspace.rehire.employmentNumber")}
              value={rehireNumber}
              onChange={setRehireNumber}
              placeholder={t("workspace.rehire.employmentNumberPlaceholder")}
            />

            <Select
              label={t("workspace.rehire.employmentType")}
              selectedKey={rehireType}
              onSelectionChange={(val) => {
                if (val) setRehireType(String(val) as EmploymentType);
              }}
              options={employmentTypeOptions}
            />

            <LocalDateField
              label={t("workspace.rehire.startDate")}
              value={rehireStartDate}
              onChange={setRehireStartDate}
              isRequired
            />
          </>
        )}

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "1rem", borderTop: "1px solid #e2e8f0", paddingTop: "1rem" }}>
          <Button variant="secondary" onPress={() => { reset(); onClose(); }} isDisabled={isPending}>
            {t("actions.cancel")}
          </Button>
          <Button
            variant={mode === "terminate" ? "danger" : "primary"}
            type="submit"
            isDisabled={isPending}
          >
            {isPending ? t("actions.saving") : t("actions.confirm")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

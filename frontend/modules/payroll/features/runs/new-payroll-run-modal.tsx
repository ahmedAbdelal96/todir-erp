import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { Select } from "../../../../components/primitives/select.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { LocalDateField } from "../../../../components/fields/local-date-field.tsx";
import { PayrollGroupPicker } from "../../components/payroll-group-picker.tsx";
import { PayrollPeriodPicker } from "../../components/payroll-period-picker.tsx";
import { HrEmployeePicker } from "../../../hr/public.ts";
import { useCreatePayrollRunMutation, usePayrollRuns } from "../../hooks/use-payroll-queries.ts";
import { mapPayrollError } from "../../services/payroll-error-map.ts";
import type { PayrollRunType } from "../../types.ts";

export interface NewPayrollRunModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly onCreated?: (runId: string) => void;
}

export function NewPayrollRunModal({
  isOpen,
  onClose,
  legalEntityId,
  onCreated
}: NewPayrollRunModalProps): React.JSX.Element {
  const { t } = useTranslation("payroll");
  const createMutation = useCreatePayrollRunMutation(legalEntityId);
  const existingRunsQuery = usePayrollRuns(legalEntityId, { pageSize: 50 });
  const finalizedRuns = existingRunsQuery.data?.filter((r) => r.finalizationState === "Finalized") ?? [];

  const [runType, setRunType] = useState<PayrollRunType>("Regular");
  const [groupId, setGroupId] = useState("");
  const [periodId, setPeriodId] = useState("");
  const [sourceRunId, setSourceRunId] = useState("");
  const [settlementEmploymentId, setSettlementEmploymentId] = useState("");
  const [effectiveDate, setEffectiveDate] = useState("");
  const [reasonCode, setReasonCode] = useState("");
  const [notes, setNotes] = useState("");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const reset = () => {
    setRunType("Regular");
    setGroupId("");
    setPeriodId("");
    setSourceRunId("");
    setSettlementEmploymentId("");
    setEffectiveDate("");
    setReasonCode("");
    setNotes("");
    setErrorMessage(null);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!groupId) {
      setErrorMessage(t("runs.workspace.selectGroup"));
      return;
    }

    try {
      const created = await createMutation.mutateAsync({
        payrollGroupId: groupId,
        periodId: periodId || null,
        runType,
        sourceRunId: sourceRunId || null,
        settlementEmploymentId: settlementEmploymentId || null,
        effectiveDate: effectiveDate || null,
        reasonCode: reasonCode.trim() || null,
        notes: notes.trim() || null
      });

      reset();
      onClose();
      if (onCreated && created.id) {
        onCreated(created.id);
      }
    } catch (err: unknown) {
      const mapped = mapPayrollError(err);
      setErrorMessage(t(mapped.messageKey, mapped.defaultMessage));
    }
  };

  const runTypeOptions = [
    { id: "Regular", label: t("runs.types.regular") },
    { id: "OffCycle", label: t("runs.types.offCycle") },
    { id: "BonusOnly", label: t("runs.types.bonusOnly") },
    { id: "Retro", label: t("runs.types.retro") },
    { id: "FinalSettlement", label: t("runs.types.finalSettlement") }
  ];

  const sourceRunOptions = finalizedRuns.map((r) => ({
    id: r.id,
    label: `${r.runNumber} (${r.runType} - ${r.employeeCount})`
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
      title={t("runs.workspace.newRunModalTitle")}
      description={t("runs.workspace.newRunModalDesc")}
      size="medium"
      footer={
        <>
          <Button
            variant="secondary"
            onPress={() => {
              reset();
              onClose();
            }}
            isDisabled={createMutation.isPending}
          >
            {t("actions.cancel")}
          </Button>
          <Button
            variant="primary"
            type="submit"
            form="new-payroll-run-form"
            isDisabled={createMutation.isPending}
          >
            {createMutation.isPending ? t("actions.saving") : t("runs.workspace.createRunAction")}
          </Button>
        </>
      }
    >
      <form id="new-payroll-run-form" onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
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

        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))", gap: "0.875rem" }}>
          <Select
            label={t("runs.type")}
            selectedKey={runType}
            onSelectionChange={(val) => {
              if (val) setRunType(String(val) as PayrollRunType);
            }}
            options={runTypeOptions}
            isRequired
          />

          <PayrollGroupPicker
            label={t("runs.group")}
            value={groupId}
            onChange={setGroupId}
            legalEntityId={legalEntityId}
            isRequired
          />
        </div>

        {groupId && (
          <PayrollPeriodPicker
            label={t("runs.period")}
            value={periodId}
            onChange={setPeriodId}
            legalEntityId={legalEntityId}
            groupId={groupId}
            isRequired={runType === "Regular" || runType === "OffCycle" || runType === "BonusOnly"}
          />
        )}

        {/* Conditional Fields based on RunType */}
        {(runType === "Retro" || runType === "Retroactive") && (
          <Select
            label={t("runs.workspace.retroSourceRun")}
            selectedKey={sourceRunId}
            onSelectionChange={(val) => {
              if (val) setSourceRunId(String(val));
            }}
            options={sourceRunOptions}
            isRequired
          />
        )}

        {runType === "FinalSettlement" && (
          <HrEmployeePicker
            label={t("runs.workspace.finalSettlementEmployee")}
            value={settlementEmploymentId}
            onChange={setSettlementEmploymentId}
            legalEntityId={legalEntityId}
            isRequired
          />
        )}

        {runType !== "Regular" && (
          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))", gap: "0.875rem" }}>
            <LocalDateField
              label={t("runs.payDate")}
              value={effectiveDate}
              onChange={setEffectiveDate}
            />

            <TextField
              label={t("runs.workspace.reasonCode")}
              value={reasonCode}
              onChange={setReasonCode}
              placeholder={t("runs.reasonCodePlaceholder", "مثال: مكافأة سنوية، تسوية نهاية خدمة")}
            />
          </div>
        )}

        <TextField
          label={t("loans.notes")}
          value={notes}
          onChange={setNotes}
        />
      </form>
    </Dialog>
  );
}

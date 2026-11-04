import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Select } from "../../../../components/primitives/select.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { LocalDateField } from "../../../../components/fields/local-date-field.tsx";
import { HrEmployeePicker } from "../../../hr/public.ts";
import {
  usePayrollGroups,
  useSalaryStructures,
  useCreateEmployeeAssignmentMutation
} from "../../hooks/use-payroll-queries.ts";
import { mapPayrollError } from "../../services/payroll-error-map.ts";

export interface NewPayrollAssignmentModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
}

export function NewPayrollAssignmentModal({
  isOpen,
  onClose,
  legalEntityId
}: NewPayrollAssignmentModalProps): React.JSX.Element {
  const { t } = useTranslation("payroll");
  const createMutation = useCreateEmployeeAssignmentMutation(legalEntityId);

  const groupsQuery = usePayrollGroups(legalEntityId);
  const groups = groupsQuery.data ?? [];

  const structuresQuery = useSalaryStructures(legalEntityId);
  const structures = structuresQuery.data ?? [];

  const [employmentId, setEmploymentId] = useState("");
  const [payrollGroupId, setPayrollGroupId] = useState(groups[0]?.id ?? "");
  const [salaryStructureVersionId, setSalaryStructureVersionId] = useState("cc0db884-6c11-5051-81bb-e0cdf38d2a56");
  const [baseSalary, setBaseSalary] = useState<number>(15000);
  const [currencyCode, setCurrencyCode] = useState("EGP");
  const [effectiveFrom, setEffectiveFrom] = useState(new Date().toISOString().slice(0, 10));
  const [effectiveTo, setEffectiveTo] = useState("");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  React.useEffect(() => {
    const firstGroup = groups[0];
    if (firstGroup && !payrollGroupId) {
      setPayrollGroupId(firstGroup.id);
      if (firstGroup.currencyCode) setCurrencyCode(firstGroup.currencyCode);
    }
  }, [groups, payrollGroupId]);

  const reset = () => {
    setEmploymentId("");
    setPayrollGroupId(groups[0]?.id ?? "");
    setSalaryStructureVersionId("cc0db884-6c11-5051-81bb-e0cdf38d2a56");
    setBaseSalary(15000);
    setCurrencyCode("EGP");
    setEffectiveFrom(new Date().toISOString().slice(0, 10));
    setEffectiveTo("");
    setErrorMessage(null);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!employmentId) {
      setErrorMessage(t("setup.assignments.employeeRequired", "يجب تحديد الموظف"));
      return;
    }
    if (!payrollGroupId) {
      setErrorMessage(t("setup.assignments.groupRequired", "يجب اختيار مجموعة الرواتب"));
      return;
    }
    if (baseSalary <= 0) {
      setErrorMessage(t("setup.assignments.salaryPositiveRequired", "يجب أن يكون الراتب الأساسي أكبر من صفر"));
      return;
    }
    if (!effectiveFrom) {
      setErrorMessage(t("setup.assignments.effectiveFromRequired", "تاريخ البدء مطلوب"));
      return;
    }

    try {
      await createMutation.mutateAsync({
        employmentId,
        request: {
          legalEntityId,
          payrollGroupId,
          salaryStructureVersionId: salaryStructureVersionId || "cc0db884-6c11-5051-81bb-e0cdf38d2a56",
          effectiveFrom,
          effectiveTo: effectiveTo.trim() || null,
          baseSalary,
          currencyCode: currencyCode.trim().toUpperCase()
        }
      });
      reset();
      onClose();
    } catch (err: unknown) {
      const mapped = mapPayrollError(err);
      setErrorMessage(t(mapped.messageKey, mapped.defaultMessage));
    }
  };

  const groupOptions = groups.map((g) => ({
    id: g.id,
    label: `${g.name} (${g.code})`
  }));

  const structureOptions = structures.length > 0
    ? structures.map((s) => ({
        id: "cc0db884-6c11-5051-81bb-e0cdf38d2a56",
        label: `${s.name} (${s.code})`
      }))
    : [
        {
          id: "cc0db884-6c11-5051-81bb-e0cdf38d2a56",
          label: t("setup.structures.standardStructure", "هيكل الرواتب القياسي (STANDARD-EG)")
        }
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
      title={t("setup.assignments.newAssignmentModalTitle", "تسكين موظف على دورة الرواتب")}
      description={t("setup.assignments.newAssignmentModalDesc", "ربط الموظف بمجموعة الرواتب وتحديد الراتب الأساسي وتاريخ السريان")}
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
            form="new-assignment-form"
            isDisabled={createMutation.isPending}
          >
            {createMutation.isPending ? t("actions.saving") : t("actions.save")}
          </Button>
        </>
      }
    >
      <form
        id="new-assignment-form"
        onSubmit={handleSubmit}
        style={{ display: "flex", flexDirection: "column", gap: "1rem" }}
      >
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

        <HrEmployeePicker
          label={t("setup.assignmentsEmployee", "الموظف")}
          value={employmentId}
          onChange={setEmploymentId}
          legalEntityId={legalEntityId}
          isRequired
          placeholder={t("setup.assignments.selectEmployeePlaceholder", "اختر الموظف لتسكينه...")}
        />

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
          <Select
            label={t("setup.assignmentsGroup", "مجموعة الرواتب")}
            selectedKey={payrollGroupId}
            onSelectionChange={(val) => {
              if (val) {
                setPayrollGroupId(String(val));
                const found = groups.find((g) => g.id === String(val));
                if (found?.currencyCode) setCurrencyCode(found.currencyCode);
              }
            }}
            options={groupOptions}
            isRequired
          />

          <Select
            label={t("setup.salaryStructuresSectionTitle", "هيكل الرواتب")}
            selectedKey={salaryStructureVersionId}
            onSelectionChange={(val) => {
              if (val) setSalaryStructureVersionId(String(val));
            }}
            options={structureOptions}
            isRequired
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "2fr 1fr", gap: "0.75rem" }}>
          <TextField
            label={t("setup.assignmentsBaseSalary", "الراتب الأساسي")}
            type="number"
            value={String(baseSalary)}
            onChange={(val) => setBaseSalary(Number(val) || 0)}
            isRequired
          />

          <TextField
            label={t("common.currency", "العملة")}
            value={currencyCode}
            onChange={setCurrencyCode}
            isRequired
            isDisabled
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
          <LocalDateField
            label={t("setup.assignmentsEffectiveFrom", "تاريخ سريان التسكين")}
            value={effectiveFrom}
            onChange={setEffectiveFrom}
            isRequired
          />

          <LocalDateField
            label={t("setup.assignmentsEffectiveTo", "تاريخ الانتهاء (اختياري)")}
            value={effectiveTo}
            onChange={setEffectiveTo}
          />
        </div>
      </form>
    </Dialog>
  );
}

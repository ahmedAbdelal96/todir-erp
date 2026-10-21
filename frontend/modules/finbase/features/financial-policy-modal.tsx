import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { Select } from "../../../components/primitives/select.tsx";
import { Switch } from "../../../components/primitives/switch.tsx";
import { Alert } from "../../../components/primitives/alert.tsx";
import {
  useFinancialPolicy,
  useRoundingPolicies,
  useUpdateFinancialPolicyMutation
} from "../hooks/finbase-queries.ts";

export interface FinancialPolicyModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly entityId: string;
}

export function FinancialPolicyModal({
  isOpen,
  onClose,
  entityId
}: FinancialPolicyModalProps): React.JSX.Element | null {
  const { t } = useTranslation("finbase");
  const { data: policy } = useFinancialPolicy(entityId);
  const { data: roundingPolicies = [] } = useRoundingPolicies();
  const updateMutation = useUpdateFinancialPolicyMutation();

  const [roundingPolicyId, setRoundingPolicyId] = useState(
    policy?.roundingPolicyId || roundingPolicies[0]?.id || ""
  );
  const [maxAgeDays, setMaxAgeDays] = useState(
    policy ? String(policy.exchangeRateMaxAgeDays) : "7"
  );
  const [manualOverride, setManualOverride] = useState(
    policy?.manualRateOverrideAllowed ?? false
  );
  const [errorMessage, setErrorMessage] = useState<string>();

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!policy) return;
    setErrorMessage(undefined);

    const parsedDays = parseInt(maxAgeDays, 10);
    if (isNaN(parsedDays) || parsedDays < 0) {
      setErrorMessage(t("policies.modal.invalidDaysError"));
      return;
    }

    try {
      await updateMutation.mutateAsync({
        entityId,
        request: {
          roundingPolicyId: roundingPolicyId || policy.roundingPolicyId,
          exchangeRateMaxAgeDays: parsedDays,
          manualRateOverrideAllowed: manualOverride,
          version: policy.version
        }
      });
      onClose();
    } catch {
      setErrorMessage(t("policies.modal.submitError"));
    }
  };

  const roundingOptions = roundingPolicies.map((rp) => ({
    id: rp.id,
    label: `${rp.code} — ${rp.nameAr} (${rp.nameEn})`
  }));

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open) => {
        if (!open) onClose();
      }}
      title={t("policies.modal.title")}
      description={t("policies.modal.description")}
      size="medium"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "var(--space-4)" }}>
        {errorMessage ? (
          <Alert variant="danger" title={t("policies.modal.errorTitle")}>
            {errorMessage}
          </Alert>
        ) : null}

        <Select
          label={t("policies.modal.roundingPolicyLabel")}
          selectedKey={roundingPolicyId || policy?.roundingPolicyId}
          onSelectionChange={(k) => setRoundingPolicyId(String(k))}
          options={roundingOptions}
        />

        <TextField
          label={t("policies.modal.maxAgeDaysLabel")}
          description={t("policies.modal.maxAgeDaysDescription")}
          value={maxAgeDays}
          onChange={setMaxAgeDays}
          inputDir="ltr"
          isRequired
        />

        <div style={{ padding: "var(--space-3)", backgroundColor: "var(--color-surface-subtle)", borderRadius: "var(--radius-md)", border: "1px solid var(--color-border-subtle)" }}>
          <Switch
            isSelected={manualOverride}
            onChange={setManualOverride}
            label={t("policies.modal.manualOverrideLabel")}
            description={t("policies.modal.manualOverrideDescription")}
          />
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "var(--space-2)", marginTop: "var(--space-4)" }}>
          <Button variant="secondary" type="button" onPress={onClose}>
            {t("policies.modal.cancel")}
          </Button>
          <Button
            variant="primary"
            type="submit"
            isDisabled={updateMutation.isPending || !policy}
          >
            {updateMutation.isPending
              ? t("policies.modal.saving")
              : t("policies.modal.submit")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

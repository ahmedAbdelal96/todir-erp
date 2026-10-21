import React, { useState, useEffect } from "react";
import { useTranslation } from "react-i18next";
import { usePricingPolicy, useUpdatePricingPolicyMutation } from "../hooks/pricing-queries.ts";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { Select } from "../../../components/primitives/select.tsx";
import { Checkbox } from "../../../components/primitives/checkbox.tsx";
import { Alert } from "../../../components/primitives/alert.tsx";
import type { PricingCurrencyPolicy } from "../types.ts";

export interface PricingPolicyModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
}

export function PricingPolicyModal({
  isOpen,
  onClose,
  legalEntityId
}: PricingPolicyModalProps): React.JSX.Element | null {
  const { t } = useTranslation("pricing");
  const { data: policy } = usePricingPolicy(legalEntityId);
  const updateMutation = useUpdatePricingPolicyMutation();

  const [currencyPolicy, setCurrencyPolicy] = useState<PricingCurrencyPolicy>("AllowFinBaseConversion");
  const [allowUomDerived, setAllowUomDerived] = useState(true);
  const [errorMsg, setErrorMsg] = useState<string>();

  useEffect(() => {
    if (policy) {
      setCurrencyPolicy((policy.currencyPolicy as PricingCurrencyPolicy) || "AllowFinBaseConversion");
      setAllowUomDerived(policy.allowUomDerivedPrice ?? true);
    }
  }, [policy]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMsg(undefined);

    try {
      await updateMutation.mutateAsync({
        legalEntityId,
        request: {
          currencyPolicy,
          allowUomDerivedPrice: allowUomDerived,
          expectedVersion: policy?.version ?? 0
        }
      });
      onClose();
    } catch (err: unknown) {
      const errObj = err as { message?: string };
      setErrorMsg(errObj.message || "تعذر تحديث السياسة السعرية للمنشأة.");
    }
  };

  const policyOptions = [
    { id: "ExactCurrency", label: t("lists.currencyPolicies.ExactCurrency") },
    { id: "AllowFinBaseConversion", label: t("lists.currencyPolicies.AllowFinBaseConversion") }
  ];

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open) => {
        if (!open) onClose();
      }}
      title={t("policies.policyCardTitle")}
      description={t("policies.description")}
    >
      <form onSubmit={(e) => void handleSubmit(e)}>
        <div style={{ display: "flex", flexDirection: "column", gap: "var(--space-3)" }}>
          {errorMsg ? <Alert variant="danger">{errorMsg}</Alert> : null}

          <Select
            label={t("policies.currencyPolicyLabel")}
            selectedKey={currencyPolicy}
            onSelectionChange={(k) => setCurrencyPolicy(k as PricingCurrencyPolicy)}
            options={policyOptions}
          />

          <div style={{ marginTop: "var(--space-2)" }}>
            <Checkbox isSelected={allowUomDerived} onChange={setAllowUomDerived}>
              {t("policies.allowUomDerivedLabel")}
            </Checkbox>
          </div>

          <div style={{ display: "flex", justifyContent: "flex-end", gap: "var(--space-2)", marginTop: "var(--space-3)" }}>
            <Button variant="secondary" type="button" onPress={onClose}>
              {t("entryModal.cancel")}
            </Button>
            <Button variant="primary" type="submit" isDisabled={updateMutation.isPending}>
              {updateMutation.isPending ? t("policies.saving") : t("policies.savePolicyAction")}
            </Button>
          </div>
        </div>
      </form>
    </Dialog>
  );
}

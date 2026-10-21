import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { useCreatePriceListMutation } from "../hooks/pricing-queries.ts";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { Select } from "../../../components/primitives/select.tsx";
import { Checkbox } from "../../../components/primitives/checkbox.tsx";
import { Alert } from "../../../components/primitives/alert.tsx";
import type { PricingDirection, PricingCurrencyPolicy } from "../types.ts";

export interface CreatePriceListModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly defaultDirection?: PricingDirection;
}

export function CreatePriceListModal({
  isOpen,
  onClose,
  legalEntityId,
  defaultDirection = "Selling"
}: CreatePriceListModalProps): React.JSX.Element | null {
  const { t } = useTranslation("pricing");
  const createMutation = useCreatePriceListMutation();

  const [code, setCode] = useState("");
  const [name, setName] = useState("");
  const [direction, setDirection] = useState<PricingDirection>(defaultDirection);
  const [currencyCode, setCurrencyCode] = useState("EGP");
  const [currencyPolicy, setCurrencyPolicy] = useState<PricingCurrencyPolicy>("ExactCurrency");
  const [isDefault, setIsDefault] = useState(false);
  const [effectiveFrom, setEffectiveFrom] = useState(() => new Date().toISOString().slice(0, 10));
  const [errorMsg, setErrorMsg] = useState<string>();

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMsg(undefined);

    if (!code.trim() || !name.trim()) {
      setErrorMsg("يرجى ملء كافة الحقول الإلزامية.");
      return;
    }

    try {
      await createMutation.mutateAsync({
        legalEntityId,
        code: code.trim().toUpperCase(),
        name: name.trim(),
        direction,
        currencyCode: currencyCode.trim().toUpperCase(),
        currencyPolicy,
        isDefault,
        effectiveFrom
      });
      onClose();
    } catch (err: unknown) {
      const errObj = err as { message?: string };
      setErrorMsg(errObj.message || "تعذر إنشاء قائمة الأسعار.");
    }
  };

  const directionOptions = [
    { id: "Selling", label: t("lists.directions.Selling") },
    { id: "Buying", label: t("lists.directions.Buying") }
  ];

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
      title={direction === "Selling" ? t("lists.createSellingAction") : t("lists.createBuyingAction")}
    >
      <form onSubmit={(e) => void handleSubmit(e)}>
        <div style={{ display: "flex", flexDirection: "column", gap: "var(--space-3)" }}>
          {errorMsg ? <Alert variant="danger">{errorMsg}</Alert> : null}

          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "var(--space-3)" }}>
            <TextField
              label={t("lists.table.codeHeader") + " *"}
              value={code}
              onChange={setCode}
              inputDir="ltr"
              isRequired
            />
            <TextField
              label={t("lists.table.nameHeader") + " *"}
              value={name}
              onChange={setName}
              isRequired
            />
          </div>

          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "var(--space-3)" }}>
            <Select
              label={t("lists.table.directionHeader") + " *"}
              selectedKey={direction}
              onSelectionChange={(k) => setDirection(k as PricingDirection)}
              options={directionOptions}
            />
            <TextField
              label={t("lists.table.currencyHeader") + " *"}
              value={currencyCode}
              onChange={(v) => setCurrencyCode(v.toUpperCase())}
              inputDir="ltr"
              isRequired
            />
          </div>

          <Select
            label={t("lists.table.currencyPolicyHeader") + " *"}
            selectedKey={currencyPolicy}
            onSelectionChange={(k) => setCurrencyPolicy(k as PricingCurrencyPolicy)}
            options={policyOptions}
          />

          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "var(--space-3)", alignItems: "center" }}>
            <TextField
              label={t("versions.effectiveFromLabel") + " *"}
              value={effectiveFrom}
              onChange={setEffectiveFrom}
              inputDir="ltr"
              isRequired
            />
            <div style={{ marginTop: "var(--space-3)" }}>
              <Checkbox isSelected={isDefault} onChange={setIsDefault}>
                {t("lists.table.defaultBadge")}
              </Checkbox>
            </div>
          </div>

          <div style={{ display: "flex", justifyContent: "flex-end", gap: "var(--space-2)", marginTop: "var(--space-3)" }}>
            <Button variant="secondary" type="button" onPress={onClose}>
              {t("entryModal.cancel")}
            </Button>
            <Button variant="primary" type="submit" isDisabled={createMutation.isPending || !code || !name}>
              {createMutation.isPending ? t("entryModal.saving") : t("entryModal.submit")}
            </Button>
          </div>
        </div>
      </form>
    </Dialog>
  );
}

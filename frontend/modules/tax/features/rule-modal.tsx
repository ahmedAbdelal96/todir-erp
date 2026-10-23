import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { Select } from "../../../components/primitives/select.tsx";
import { LocalDateField } from "../../../components/fields/local-date-field.tsx";
import { taxApi } from "../services/tax-api.ts";
import { extractTaxError } from "../services/tax-error-map.ts";
import type { TaxCategoryItem, TaxRuleDto } from "../types.ts";

export interface RuleModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly ruleSetId: string;
  readonly legalEntityId: string;
  readonly rule?: TaxRuleDto | null | undefined;
  readonly categories: readonly TaxCategoryItem[];
  readonly defaultEffectiveDate: string;
  readonly onSaved?: () => void;
}

export function RuleModal({
  isOpen,
  onClose,
  ruleSetId,
  legalEntityId,
  rule,
  categories,
  defaultEffectiveDate,
  onSaved
}: RuleModalProps): React.JSX.Element | null {
  const { t } = useTranslation("tax");

  const [priority, setPriority] = useState(String(rule?.priority ?? 0));
  const [treatment, setTreatment] = useState(rule?.treatment || "StandardRated");
  const [direction, setDirection] = useState("Selling");
  const [placeOfSupplyCountryCode, setPlaceOfSupplyCountryCode] = useState("EG");
  const [productCategoryId, setProductCategoryId] = useState("");
  const [partyCategoryId, setPartyCategoryId] = useState("");
  const [legalReasonCode, setLegalReasonCode] = useState("");
  const [sourceReference, setSourceReference] = useState("Manual rule entry");
  const [effectiveFrom, setEffectiveFrom] = useState(rule?.effectiveFrom || defaultEffectiveDate);
  const [effectiveTo, setEffectiveTo] = useState(rule?.effectiveTo || "");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [isPending, setIsPending] = useState(false);

  const productCategories = categories.filter((c) => c.scope === "Product");
  const partyCategories = categories.filter((c) => c.scope === "Party");

  const handleSave = async () => {
    setErrorMessage(null);
    setIsPending(true);
    try {
      const payload = {
        priority: parseInt(priority, 10) || 0,
        treatment,
        direction: direction || null,
        placeOfSupplyCountryCode: placeOfSupplyCountryCode ? placeOfSupplyCountryCode.toUpperCase() : null,
        productCategoryId: productCategoryId || null,
        partyCategoryId: partyCategoryId || null,
        legalReasonCode: legalReasonCode || null,
        sourceReference: sourceReference || "Tenant rule configuration",
        effectiveFrom,
        effectiveTo: effectiveTo || null,
        lockedByCountryPack: false
      };

      if (rule?.id) {
        await taxApi.updateRule(rule.id, legalEntityId, payload);
      } else {
        await taxApi.addRule(ruleSetId, legalEntityId, payload);
      }
      onSaved?.();
      onClose();
    } catch (err) {
      const mapped = extractTaxError(err);
      setErrorMessage(t(mapped.messageKey, { defaultValue: mapped.fallbackEn }));
    } finally {
      setIsPending(false);
    }
  };

  const footer = (
    <div style={{ display: "flex", gap: "var(--space-2)", justifyContent: "flex-end" }}>
      <Button variant="secondary" onPress={onClose}>
        {t("actions.cancel")}
      </Button>
      <Button variant="primary" onPress={() => void handleSave()} isLoading={isPending}>
        {t("actions.save")}
      </Button>
    </div>
  );

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open) => {
        if (!open) onClose();
      }}
      title={rule?.id ? t("rules.editRuleTitle") : t("rules.addRuleTitle")}
      description={t("rules.ruleModalDescription")}
      footer={footer}
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {errorMessage && (
          <div
            role="alert"
            style={{
              padding: "0.75rem",
              borderRadius: "var(--radius-md)",
              backgroundColor: "var(--color-status-danger-subtle, #fee2e2)",
              color: "var(--color-status-danger-text, #991b1b)",
              fontSize: "0.875rem"
            }}
          >
            {errorMessage}
          </div>
        )}

        <TextField
          label={t("rules.priority")}
          type="number"
          value={priority}
          onChange={setPriority}
          isRequired
        />

        <Select
          label={t("rules.treatment")}
          selectedKey={treatment}
          onSelectionChange={(key) => setTreatment(key as string)}
          options={[
            { id: "StandardRated", label: t("treatment.standardRated") },
            { id: "ZeroRated", label: t("treatment.zeroRated") },
            { id: "Exempt", label: t("treatment.exempt") },
            { id: "OutOfScope", label: t("treatment.outOfScope") },
            { id: "ReverseCharge", label: t("treatment.reverseCharge") }
          ]}
        />

        <Select
          label={t("rules.direction")}
          selectedKey={direction}
          onSelectionChange={(key) => setDirection(key as string)}
          options={[
            { id: "Selling", label: t("rules.directionSelling") },
            { id: "Buying", label: t("rules.directionBuying") }
          ]}
        />

        <TextField
          label={t("rules.placeOfSupply")}
          value={placeOfSupplyCountryCode}
          onChange={setPlaceOfSupplyCountryCode}
        />

        <Select
          label={t("rules.productCategory")}
          selectedKey={productCategoryId}
          onSelectionChange={(key) => setProductCategoryId(key as string)}
          options={[
            { id: "", label: t("actions.all") },
            ...productCategories.map((c) => ({ id: c.id, label: `${c.name} (${c.code})` }))
          ]}
        />

        <Select
          label={t("rules.partyCategory")}
          selectedKey={partyCategoryId}
          onSelectionChange={(key) => setPartyCategoryId(key as string)}
          options={[
            { id: "", label: t("actions.all") },
            ...partyCategories.map((c) => ({ id: c.id, label: `${c.name} (${c.code})` }))
          ]}
        />

        <TextField
          label={t("rules.legalReasonCode")}
          value={legalReasonCode}
          onChange={setLegalReasonCode}
        />

        <LocalDateField
          label={t("rules.effectiveFrom")}
          value={effectiveFrom}
          onChange={setEffectiveFrom}
          isRequired
        />

        <LocalDateField
          label={t("rules.effectiveTo")}
          value={effectiveTo}
          onChange={setEffectiveTo}
        />
      </div>
    </Dialog>
  );
}

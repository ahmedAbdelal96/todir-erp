import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { useAddPriceEntryMutation } from "../hooks/pricing-queries.ts";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { Select } from "../../../components/primitives/select.tsx";
import { Alert } from "../../../components/primitives/alert.tsx";
import { normalizeDecimalInput } from "../../../components/formatting/decimal-parser.ts";
import type { PricingPartyScope } from "../types.ts";

export interface AddEntryModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly listId: string;
  readonly versionId: string;
  readonly legalEntityId: string;
}

export function AddEntryModal({
  isOpen,
  onClose,
  listId,
  versionId,
  legalEntityId
}: AddEntryModalProps): React.JSX.Element | null {
  const { t } = useTranslation("pricing");
  const addMutation = useAddPriceEntryMutation();

  const [variantId, setVariantId] = useState("");
  const [uomId, setUomId] = useState("");
  const [partyScope, setPartyScope] = useState<PricingPartyScope>("AllParties");
  const [partyId, setPartyId] = useState("");
  const [priceGroupId, setPriceGroupId] = useState("");
  const [minQuantity, setMinQuantity] = useState("1");
  const [maxQuantity, setMaxQuantity] = useState("");
  const [unitPrice, setUnitPrice] = useState("");
  const [validFrom, setValidFrom] = useState("");
  const [validTo, setValidTo] = useState("");
  const [priority, setPriority] = useState("0");
  const [errorMsg, setErrorMsg] = useState<string>();

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMsg(undefined);

    const normalizedPrice = normalizeDecimalInput(unitPrice);
    if (!normalizedPrice || isNaN(Number(normalizedPrice)) || Number(normalizedPrice) < 0) {
      setErrorMsg("يرجى إدخال سعر وحدة صحيح وموجب.");
      return;
    }

    if (!variantId.trim() || !uomId.trim()) {
      setErrorMsg("يرجى تحديد المنتج ووحدة القياس.");
      return;
    }

    try {
      await addMutation.mutateAsync({
        listId,
        versionId,
        legalEntityId,
        request: {
          variantId: variantId.trim(),
          uomId: uomId.trim(),
          partyId: partyScope === "SpecificParty" && partyId ? partyId.trim() : null,
          priceGroupId: partyScope === "PriceGroup" && priceGroupId ? priceGroupId.trim() : null,
          minQuantity: Number(minQuantity) || 1,
          maxQuantity: maxQuantity ? Number(maxQuantity) : null,
          unitPrice: normalizedPrice,
          validFrom: validFrom || null,
          validTo: validTo || null,
          priority: Number(priority) || 0
        }
      });
      onClose();
    } catch (err: unknown) {
      const errObj = err as { message?: string };
      setErrorMsg(errObj.message || "تعذر إضافة بند السعر.");
    }
  };

  const scopeOptions = [
    { id: "AllParties", label: t("entryModal.allPartiesOption") },
    { id: "SpecificParty", label: t("entryModal.specificPartyOption") },
    { id: "PriceGroup", label: t("entryModal.priceGroupOption") }
  ];

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open) => {
        if (!open) onClose();
      }}
      title={t("entryModal.title")}
      description={t("entryModal.description")}
    >
      <form onSubmit={(e) => void handleSubmit(e)}>
        <div style={{ display: "flex", flexDirection: "column", gap: "var(--space-3)" }}>
          {errorMsg ? <Alert variant="danger">{errorMsg}</Alert> : null}

          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "var(--space-3)" }}>
            <TextField
              label={t("entryModal.variantLabel")}
              value={variantId}
              onChange={setVariantId}
              inputDir="ltr"
              placeholder={t("entryModal.variantPlaceholder")}
              isRequired
            />
            <TextField
              label={t("entryModal.uomLabel")}
              value={uomId}
              onChange={setUomId}
              inputDir="ltr"
              placeholder={t("entryModal.uomPlaceholder")}
              isRequired
            />
          </div>

          <Select
            label={t("entryModal.partyScopeLabel")}
            selectedKey={partyScope}
            onSelectionChange={(k) => setPartyScope(k as PricingPartyScope)}
            options={scopeOptions}
          />

          {partyScope === "SpecificParty" && (
            <TextField
              label={t("entryModal.partyLabel") + " *"}
              value={partyId}
              onChange={setPartyId}
              inputDir="ltr"
              placeholder={t("entryModal.partyPlaceholder")}
              isRequired
            />
          )}

          {partyScope === "PriceGroup" && (
            <TextField
              label={t("entryModal.groupLabel") + " *"}
              value={priceGroupId}
              onChange={setPriceGroupId}
              inputDir="ltr"
              placeholder={t("entryModal.groupPlaceholder")}
              isRequired
            />
          )}

          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "var(--space-3)" }}>
            <TextField
              label={t("entryModal.unitPriceLabel")}
              value={unitPrice}
              onChange={setUnitPrice}
              inputDir="ltr"
              placeholder={t("entryModal.unitPricePlaceholder")}
              isRequired
            />
            <TextField
              label={t("entryModal.priorityLabel")}
              value={priority}
              onChange={setPriority}
              inputDir="ltr"
              placeholder="0"
            />
          </div>

          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "var(--space-3)" }}>
            <TextField
              label={t("entryModal.minQtyLabel")}
              value={minQuantity}
              onChange={setMinQuantity}
              inputDir="ltr"
              placeholder="1"
              isRequired
            />
            <TextField
              label={t("entryModal.maxQtyLabel")}
              value={maxQuantity}
              onChange={setMaxQuantity}
              inputDir="ltr"
              placeholder="999"
            />
          </div>

          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "var(--space-3)" }}>
            <TextField
              label={t("entryModal.validFromLabel")}
              value={validFrom}
              onChange={setValidFrom}
              inputDir="ltr"
            />
            <TextField
              label={t("entryModal.validToLabel")}
              value={validTo}
              onChange={setValidTo}
              inputDir="ltr"
            />
          </div>

          <div style={{ display: "flex", justifyContent: "flex-end", gap: "var(--space-2)", marginTop: "var(--space-3)" }}>
            <Button variant="secondary" type="button" onPress={onClose}>
              {t("entryModal.cancel")}
            </Button>
            <Button variant="primary" type="submit" isDisabled={addMutation.isPending || !unitPrice || !variantId}>
              {addMutation.isPending ? t("entryModal.saving") : t("entryModal.submit")}
            </Button>
          </div>
        </div>
      </form>
    </Dialog>
  );
}

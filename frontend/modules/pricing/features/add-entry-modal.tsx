import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { useAddPriceEntryMutation, useLookupProducts, useLookupUoms, useLookupParties } from "../hooks/pricing-queries.ts";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { Select } from "../../../components/primitives/select.tsx";
import { Alert } from "../../../components/primitives/alert.tsx";
import { normalizeDecimalInput } from "../../../components/formatting/decimal-parser.ts";
import type { PricingPartyScope } from "../types.ts";

interface LookupItem {
  readonly id: string;
  readonly code?: string;
  readonly displayName?: string;
  readonly name?: string;
  readonly nameAr?: string;
  readonly nameEn?: string;
  readonly sku?: string;
  readonly defaultVariantId?: string;
}

function lookupItems(raw: unknown): readonly LookupItem[] {
  if (Array.isArray(raw)) return raw as readonly LookupItem[];
  if (raw && typeof raw === "object" && "items" in raw) {
    return ((raw as { items?: readonly LookupItem[] }).items ?? []);
  }
  return [];
}

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
  const productsQuery = useLookupProducts(legalEntityId);
  const uomsQuery = useLookupUoms();
  const partiesQuery = useLookupParties(legalEntityId);

  const [variantId, setVariantId] = useState("");
  const [uomId, setUomId] = useState("");
  const [partyScope, setPartyScope] = useState<PricingPartyScope>("AllParties");
  const [partyId, setPartyId] = useState("");
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
      setErrorMsg(t("entryModal.invalidPrice"));
      return;
    }

    if (!variantId.trim() || !uomId.trim()) {
      setErrorMsg(t("entryModal.productAndUnitRequired"));
      return;
    }
    if (partyScope === "SpecificParty" && !partyId) {
      setErrorMsg(t("entryModal.partyRequired"));
      return;
    }
    if (partyScope === "PriceGroup") {
      setErrorMsg(t("entryModal.groupLookupUnavailable"));
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
          priceGroupId: null,
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
      setErrorMsg(t("entryModal.saveFailed"));
    }
  };

  const products = lookupItems(productsQuery.data)
    .filter((product) => Boolean(product.defaultVariantId))
    .map((product) => ({
      id: product.defaultVariantId as string,
      label: product.sku ? `${product.displayName || product.name} (${product.sku})` : (product.displayName || product.name || "")
    }));
  const parties = lookupItems(partiesQuery.data)
    .map((party) => ({ id: party.id, label: party.displayName || party.name || "" }));
  const units = lookupItems(uomsQuery.data)
    .map((uom) => ({ id: uom.id, label: `${uom.nameAr || uom.nameEn || uom.code || ""} (${uom.code || ""})` }));

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
            <Select
              label={t("entryModal.variantLabel")}
              selectedKey={variantId}
              onSelectionChange={(key) => setVariantId(key as string)}
              options={products}
            />
            <Select
              label={t("entryModal.uomLabel")}
              selectedKey={uomId}
              onSelectionChange={(key) => setUomId(key as string)}
              options={units}
            />
          </div>

          <Select
            label={t("entryModal.partyScopeLabel")}
            selectedKey={partyScope}
            onSelectionChange={(k) => setPartyScope(k as PricingPartyScope)}
            options={scopeOptions}
          />

          {partyScope === "SpecificParty" && (
            <Select
              label={t("entryModal.partyLabel") + " *"}
              selectedKey={partyId}
              onSelectionChange={(key) => setPartyId(key as string)}
              options={parties}
            />
          )}

          {partyScope === "PriceGroup" && (
            <Alert variant="warning">{t("entryModal.groupLookupUnavailable")}</Alert>
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
            <Button variant="primary" type="submit" isDisabled={addMutation.isPending || !unitPrice || !variantId || !uomId || (partyScope === "SpecificParty" && !partyId) || partyScope === "PriceGroup"}>
              {addMutation.isPending ? t("entryModal.saving") : t("entryModal.submit")}
            </Button>
          </div>
        </div>
      </form>
    </Dialog>
  );
}

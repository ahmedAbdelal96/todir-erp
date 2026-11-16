import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { useAddPartRequirementMutation } from "../../hooks/use-service-queries.ts";

export interface AddPartRequirementModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly workOrderId: string;
}

export function AddPartRequirementModal({
  isOpen,
  onClose,
  legalEntityId,
  workOrderId
}: AddPartRequirementModalProps): React.JSX.Element {
  const { t } = useTranslation("service");
  const addPartMutation = useAddPartRequirementMutation(legalEntityId);

  const [itemId, setItemId] = useState("");
  const [description, setDescription] = useState("");
  const [quantity, setQuantity] = useState("1");
  const [uom, setUom] = useState("PCS");
  const [estimatedUnitCost, setEstimatedUnitCost] = useState("0");
  const [currency, setCurrency] = useState("SAR");
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!itemId.trim()) {
      setErrorMsg(t("validation.itemIdRequired"));
      return;
    }

    const qty = Number(quantity);
    if (isNaN(qty) || qty <= 0) {
      setErrorMsg(t("validation.invalidQuantity"));
      return;
    }

    try {
      setErrorMsg(null);
      await addPartMutation.mutateAsync({
        legalEntityId,
        workOrderId,
        itemId: itemId.trim(),
        description: description.trim() || undefined,
        requiredQuantity: qty,
        uom: uom.trim() || "PCS",
        estimatedUnitCost: Number(estimatedUnitCost) || 0,
        currency: currency.trim() || "SAR"
      });
      onClose();
    } catch (err: unknown) {
      setErrorMsg(err instanceof Error ? err.message : t("common.genericError"));
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("parts.addPartRequirementTitle")}
      size="medium"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {errorMsg && (
          <div
            style={{
              padding: "0.75rem 1rem",
              borderRadius: "var(--radius-sm, 0.375rem)",
              backgroundColor: "#fef2f2",
              border: "1px solid #fecaca",
              color: "#b91c1c",
              fontSize: "0.875rem"
            }}
          >
            {errorMsg}
          </div>
        )}

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
              {t("parts.itemId")} *
            </label>
            <TextField
              value={itemId}
              onChange={(val) => setItemId(val)}
              placeholder={t("parts.itemCodePlaceholder", "مثال: ITEM-BEARING-01")}
              isRequired
            />
          </div>

          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
              {t("requests.description")}
            </label>
            <TextField
              value={description}
              onChange={(val) => setDescription(val)}
              placeholder={t("parts.descriptionPlaceholder")}
            />
          </div>
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
              {t("parts.requiredQuantity")} *
            </label>
            <TextField
              value={quantity}
              onChange={(val) => setQuantity(val)}
              placeholder="1"
              isRequired
            />
          </div>

          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
              {t("workOrders.uom")} *
            </label>
            <TextField
              value={uom}
              onChange={(val) => setUom(val)}
              placeholder={t("parts.uomPlaceholder", "PCS, SET, KG, M")}
              isRequired
            />
          </div>
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
              {t("parts.estimatedUnitCost")}
            </label>
            <TextField
              value={estimatedUnitCost}
              onChange={(val) => setEstimatedUnitCost(val)}
              placeholder="0.00"
            />
          </div>

          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
              {t("billing.currency")}
            </label>
            <TextField
              value={currency}
              onChange={(val) => setCurrency(val)}
              placeholder="SAR"
            />
          </div>
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "1rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("common.cancel")}
          </Button>
          <Button variant="primary" type="submit" isLoading={addPartMutation.isPending}>
            {t("parts.addRequirement")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

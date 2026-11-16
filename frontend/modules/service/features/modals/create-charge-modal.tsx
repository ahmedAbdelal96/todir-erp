import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { ServiceSelect as Select } from "../../components/service-select.tsx";
import { useCreateChargeMutation } from "../../hooks/use-service-queries.ts";
import type { ChargeType } from "../../types.ts";

export interface CreateChargeModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly workOrderId: string;
}

export function CreateChargeModal({
  isOpen,
  onClose,
  legalEntityId,
  workOrderId
}: CreateChargeModalProps): React.JSX.Element {
  const { t } = useTranslation("service");
  const createChargeMutation = useCreateChargeMutation(legalEntityId);

  const [chargeType, setChargeType] = useState<ChargeType>("labor");
  const [description, setDescription] = useState("");
  const [quantity, setQuantity] = useState("1");
  const [unitPrice, setUnitPrice] = useState("150");
  const [currency, setCurrency] = useState("SAR");
  const [isBillable, setIsBillable] = useState(true);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!description.trim()) {
      setErrorMsg(t("validation.descriptionRequired"));
      return;
    }

    const qty = Number(quantity);
    const price = Number(unitPrice);
    if (isNaN(qty) || qty <= 0 || isNaN(price) || price < 0) {
      setErrorMsg(t("validation.invalidAmount"));
      return;
    }

    try {
      setErrorMsg(null);
      await createChargeMutation.mutateAsync({
        legalEntityId,
        workOrderId,
        chargeType,
        description: description.trim(),
        quantity: qty,
        unitPrice: price,
        totalAmount: qty * price,
        currency: currency.trim() || "SAR",
        isBillable
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
      title={t("billing.newChargeTitle")}
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

        <div>
          <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
            {t("billing.chargeType")} *
          </label>
          <Select
            value={chargeType}
            onChange={(val) => setChargeType(val as ChargeType)}
            options={[
              { value: "labor", label: t("billing.types.labor") },
              { value: "parts", label: t("billing.types.parts") },
              { value: "travel", label: t("billing.types.travel") },
              { value: "fixed_fee", label: t("billing.types.fixedFee") }
            ]}
          />
        </div>

        <div>
          <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
            {t("requests.description")} *
          </label>
          <TextField
            value={description}
            onChange={(val) => setDescription(val)}
            placeholder={t("billing.chargeDescriptionPlaceholder")}
            isRequired
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
              {t("parts.quantity")} *
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
              {t("billing.unitPrice")} ({currency}) *
            </label>
            <TextField
              value={unitPrice}
              onChange={(val) => setUnitPrice(val)}
              placeholder="150.00"
              isRequired
            />
          </div>
        </div>

        <div
          style={{
            padding: "0.75rem 1rem",
            backgroundColor: "var(--color-surface-subtle, #f8fafc)",
            borderRadius: "var(--radius-sm, 0.375rem)",
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            fontSize: "0.875rem"
          }}
        >
          <span style={{ color: "var(--color-content-secondary, #475569)" }}>{t("billing.total")}:</span>
          <strong style={{ fontSize: "1rem", color: "#0f172a" }}>
            {((Number(quantity) || 0) * (Number(unitPrice) || 0)).toLocaleString("en-US", {
              minimumFractionDigits: 2,
              maximumFractionDigits: 2
            })}{" "}
            {currency}
          </strong>
        </div>

        <div style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
          <input
            type="checkbox"
            id="charge-billable-check"
            checked={isBillable}
            onChange={(e) => setIsBillable(e.target.checked)}
            style={{ width: "1rem", height: "1rem", accentColor: "#2563eb", cursor: "pointer" }}
          />
          <label htmlFor="charge-billable-check" style={{ fontSize: "0.875rem", cursor: "pointer", fontWeight: 500 }}>
            {t("billing.customerLiability")} ({t("labor.isBillable")})
          </label>
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "1rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("common.cancel")}
          </Button>
          <Button variant="primary" type="submit" isLoading={createChargeMutation.isPending}>
            {t("billing.addCharge")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

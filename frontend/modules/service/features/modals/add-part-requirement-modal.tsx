import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { ProductPicker } from "../../../catalog/public.ts";
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

  const [productId, setProductId] = useState("");
  const [description, setDescription] = useState("");
  const [quantity, setQuantity] = useState("1");
  const [uomCode, setUomCode] = useState("PCS");
  const [warehouseId, setWarehouseId] = useState("");
  const [locationId, setLocationId] = useState("");
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!productId.trim()) {
      setErrorMsg(t("validation.itemIdRequired", "يرجى تحديد كود أو معرف الصنف"));
      return;
    }

    const qty = Number(quantity);
    if (isNaN(qty) || qty <= 0) {
      setErrorMsg(t("validation.invalidQuantity", "يرجى إدخال كمية صحيحة أكبر من صفر"));
      return;
    }

    try {
      setErrorMsg(null);
      await addPartMutation.mutateAsync({
        workOrderId,
        request: {
          productId: productId.trim(),
          quantity: qty,
          uomCode: uomCode.trim() || "PCS",
          description: description.trim() || undefined,
          legalEntityId,
          warehouseId: warehouseId.trim() || undefined,
          locationId: locationId.trim() || undefined
        }
      });
      onClose();
      setProductId("");
      setDescription("");
      setQuantity("1");
      setUomCode("PCS");
      setWarehouseId("");
      setLocationId("");
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
            <ProductPicker
              label={t("parts.itemId")}
              value={productId || undefined}
              onChange={(selection) => {
                setProductId(selection.variantId);
                setDescription(selection.displayName);
                if (selection.baseUomCode) setUomCode(selection.baseUomCode);
              }}
              isRequired
              enableQuickCreate={false}
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
              value={uomCode}
              onChange={(val) => setUomCode(val)}
              placeholder={t("parts.uomPlaceholder", "PCS, SET, KG, M")}
              isRequired
            />
          </div>
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
              {t("parts.warehouseLocation", "معرف المستودع (WarehouseId)")}
            </label>
            <TextField
              value={warehouseId}
              onChange={(val) => setWarehouseId(val)}
              placeholder={t("parts.warehousePlaceholder", "WH-MAIN-01")}
            />
          </div>

          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
              {t("parts.locationId", "معرف الموقع الداخلي (LocationId)")}
            </label>
            <TextField
              value={locationId}
              onChange={(val) => setLocationId(val)}
              placeholder={t("parts.locationPlaceholder", "LOC-BIN-A")}
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

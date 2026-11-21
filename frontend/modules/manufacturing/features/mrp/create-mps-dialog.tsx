import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { ProductPicker, type ProductPickerSelection } from "../../../catalog/public.ts";
import { useCreateMpsPlanMutation, usePlantsQuery } from "../../hooks/use-manufacturing.ts";

export interface CreateMpsDialogProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
}

export function CreateMpsDialog({ isOpen, onClose, legalEntityId }: CreateMpsDialogProps): React.JSX.Element {
  const { t } = useTranslation("manufacturing");
  const createMpsMutation = useCreateMpsPlanMutation();
  const plantsQuery = usePlantsQuery(legalEntityId);

  const [plantId, setPlantId] = useState("");
  const [productVariantId, setProductVariantId] = useState("");
  const [quantity, setQuantity] = useState("1000");
  const [uomCode, setUomCode] = useState("EA");
  const [bucketStart, setBucketStart] = useState(new Date().toISOString().slice(0, 10));
  const [bucketEnd, setBucketEnd] = useState(() => {
    const d = new Date();
    d.setDate(d.getDate() + 7);
    return d.toISOString().slice(0, 10);
  });
  const [sourceRef, setSourceRef] = useState("MPS-WEEKLY-TARGET");

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    const effectivePlantId = plantId || plantsQuery.data?.[0]?.id;
    const qtyVal = parseFloat(quantity);
    if (!effectivePlantId || !productVariantId.trim() || isNaN(qtyVal) || qtyVal <= 0) return;

    createMpsMutation.mutate(
      {
        legalEntityId,
        plantId: effectivePlantId,
        productVariantId: productVariantId.trim(),
        bucketStart,
        bucketEnd,
        quantity: qtyVal,
        uomCode: uomCode.trim().toUpperCase(),
        sourceRef: sourceRef.trim() || undefined
      },
      {
        onSuccess: () => {
          onClose();
        }
      }
    );
  };

  const isLoading = createMpsMutation.isPending;

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("mrp.createMps")}
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "16px" }}>
        <div style={{ display: "flex", flexDirection: "column", gap: "6px" }}>
          <label style={{ fontSize: "13px", fontWeight: 600, color: "#1e293b" }}>
            {t("orders.plant")}
          </label>
          <select
            value={plantId || plantsQuery.data?.[0]?.id || ""}
            onChange={(e) => setPlantId(e.target.value)}
            style={{
              padding: "8px 12px",
              borderRadius: "6px",
              border: "1px solid #cbd5e1",
              fontSize: "14px",
              background: "#ffffff"
            }}
            required
          >
            {plantsQuery.data?.map((plant) => (
              <option key={plant.id} value={plant.id}>
                {plant.code} - {plant.name}
              </option>
            ))}
          </select>
        </div>

        <ProductPicker
          label={t("mrp.targetProduct")}
          value={productVariantId}
          isRequired
          onChange={(sel: ProductPickerSelection) => {
            setProductVariantId(sel.variantId);
            if (sel.baseUomCode && (!uomCode || uomCode === "EA")) {
              setUomCode(sel.baseUomCode);
            }
          }}
        />

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "12px" }}>
          <TextField
            label={t("mrp.targetQty")}
            type="number"
            value={quantity}
            onChange={(val) => setQuantity(val)}
            required
          />
          <TextField
            label={t("orders.uom")}
            value={uomCode}
            onChange={(val) => setUomCode(val)}
            required
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "12px" }}>
          <TextField
            label={t("mrp.horizonStart")}
            type="date"
            value={bucketStart}
            onChange={(val) => setBucketStart(val)}
            required
          />
          <TextField
            label={t("mrp.horizonEnd")}
            type="date"
            value={bucketEnd}
            onChange={(val) => setBucketEnd(val)}
            required
          />
        </div>

        <TextField
          label={t("mrp.planningRef")}
          value={sourceRef}
          onChange={(val) => setSourceRef(val)}
        />

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "8px", marginTop: "12px" }}>
          <Button type="button" variant="secondary" onClick={onClose} disabled={isLoading}>
            {t("common.cancel")}
          </Button>
          <Button type="submit" variant="primary" disabled={isLoading || !productVariantId || !quantity}>
            {isLoading ? t("common.loading") : t("common.save")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

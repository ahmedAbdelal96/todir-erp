import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { useMaterialActionMutation } from "../../hooks/use-manufacturing.ts";

export interface MaterialActionDialogProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly orderId: string;
  readonly requirementId: string;
  readonly actionType: "Reserved" | "Issued" | "Consumed" | "Returned";
  readonly uomCode?: string | undefined;
  readonly initialQty?: number | undefined;
}

export function MaterialActionDialog({
  isOpen,
  onClose,
  orderId,
  requirementId,
  actionType,
  uomCode = "EA",
  initialQty
}: MaterialActionDialogProps): React.JSX.Element {
  const { t } = useTranslation("manufacturing");
  const materialMutation = useMaterialActionMutation();

  const [quantity, setQuantity] = useState(() => (initialQty != null && initialQty > 0 ? String(initialQty) : "1"));
  const [warehouseId, setWarehouseId] = useState("");
  const [substituteVariantId, setSubstituteVariantId] = useState("");
  const [idempotencyKey] = useState(() => `mat-${Date.now()}`);

  const getActionTitle = () => {
    switch (actionType) {
      case "Reserved":
        return t("materials.reserved");
      case "Issued":
        return t("materials.stageIssue");
      case "Consumed":
        return t("materials.directConsume");
      case "Returned":
        return t("materials.returnMaterial");
    }
  };

  const getRoute = (): "reserve" | "issue" | "consume" | "return" => {
    switch (actionType) {
      case "Reserved":
        return "reserve";
      case "Issued":
        return "issue";
      case "Consumed":
        return "consume";
      case "Returned":
        return "return";
    }
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    const qty = parseFloat(quantity);
    if (isNaN(qty) || qty <= 0) return;

    materialMutation.mutate(
      {
        orderId,
        route: getRoute(),
        request: {
          requirementId,
          quantity: qty,
          uomCode,
          eventType: actionType,
          idempotencyKey,
          warehouseId: warehouseId || undefined,
          substituteProductVariantId: substituteVariantId || undefined
        }
      },
      {
        onSuccess: () => {
          onClose();
        }
      }
    );
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={getActionTitle()}
      description={t("materials.idempotencyNotice")}
      size="small"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {materialMutation.isError ? (
          <div role="alert" style={{ padding: "0.75rem", background: "#fef2f2", border: "1px solid #ef4444", borderRadius: "var(--radius-sm)", color: "#b91c1c", fontSize: "0.875rem" }}>
            {t("errors.materialInsufficient")}
          </div>
        ) : null}

        <TextField
          label={t("common.quantity")}
          type="number"
          value={quantity}
          onChange={(val) => setQuantity(val)}
          min="0.0001"
          step="any"
          required
        />

        <TextField
          label={t("materials.warehouse")}
          value={warehouseId}
          onChange={(val) => setWarehouseId(val)}
        />

        <TextField
          label={t("definitions.substituteVariantId")}
          value={substituteVariantId}
          onChange={(val) => setSubstituteVariantId(val)}
        />

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "1rem" }}>
          <Button type="button" variant="outline" onPress={onClose}>
            {t("common.cancel")}
          </Button>
          <Button type="submit" variant="primary" isDisabled={materialMutation.isPending || !quantity}>
            {materialMutation.isPending ? t("common.loading") : getActionTitle()}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { LocalDateField } from "../../../../components/fields/local-date-field.tsx";
import { InventoryProductPicker } from "../../components/inventory-product-picker.tsx";
import { useInventoryMutations, useLocations } from "../../hooks/use-inventory-queries.ts";
import { mapInventoryError } from "../../services/inventory-error-map.ts";
import type { AdjustmentDirection, AdjustmentReason, Warehouse } from "../../types.ts";
import { InventoryProfileSetupModal } from "../operations/inventory-profile-setup-modal.tsx";

export interface CreateAdjustmentModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly warehouses: readonly Warehouse[];
  readonly canManageProfile?: boolean;
  readonly isApprovalRequired?: boolean;
  readonly initialProductVariantId?: string;
  readonly onAdjustmentCreated?: (adj: {
    id: string;
    productVariantId: string;
    warehouseName: string;
    quantity: number;
    direction: AdjustmentDirection;
    reason: AdjustmentReason;
    status: string;
  }) => void;
}

export function CreateAdjustmentModal({
  isOpen,
  onClose,
  legalEntityId,
  warehouses,
  canManageProfile = false,
  isApprovalRequired = false,
  initialProductVariantId,
  onAdjustmentCreated
}: CreateAdjustmentModalProps): React.JSX.Element | null {
  const { t } = useTranslation("inventory");
  const today = new Date().toISOString().slice(0, 10);

  const [productId, setProductId] = useState(initialProductVariantId ?? "");
  const [warehouseId, setWarehouseId] = useState(warehouses[0]?.id ?? "");
  const [locationId, setLocationId] = useState("");
  const [direction, setDirection] = useState<AdjustmentDirection>("Increase");
  const [reason, setReason] = useState<AdjustmentReason>("FoundStock");
  const [quantity, setQuantity] = useState("1");
  const [uomId, setUomId] = useState("2b12a81b-ea82-a151-af7f-4c60a017f44d");
  const [opDate, setOpDate] = useState(today);
  const [postDate, setPostDate] = useState(today);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [successInfo, setSuccessInfo] = useState<string | null>(null);
  const [requiresProfileSetup, setRequiresProfileSetup] = useState(false);
  const [isProfileSetupOpen, setIsProfileSetupOpen] = useState(false);

  const selectedWarehouse = warehouses.find(w => w.id === warehouseId);
  const hasLocations = selectedWarehouse && selectedWarehouse.locationMode !== "WarehouseOnly";
  const { data: locations = [] } = useLocations(legalEntityId, hasLocations ? warehouseId : undefined);

  const { adjustStock, createAdjustmentRequest } = useInventoryMutations(legalEntityId);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMessage(null);
    setSuccessInfo(null);
    setRequiresProfileSetup(false);

    const numQty = parseFloat(quantity);
    if (isNaN(numQty) || numQty <= 0) {
      setErrorMessage(t("errors.quantityInvalid"));
      return;
    }

    try {
      const idempotencyKey = `adj-${Date.now()}-${Math.random().toString(36).slice(2, 7)}`;

      if (isApprovalRequired) {
        const req = await createAdjustmentRequest.mutateAsync({
          productVariantId: productId,
          warehouseId,
          locationId: hasLocations && locationId ? locationId : null,
          enteredUomId: uomId,
          quantity: numQty,
          direction,
          reason,
          operationalDate: opDate,
          postingDate: postDate,
          idempotencyKey
        });

        if (onAdjustmentCreated) {
          onAdjustmentCreated({
            id: req.id,
            productVariantId: productId,
            warehouseName: selectedWarehouse?.name ?? "المخزن الرئيسي",
            quantity: numQty,
            direction,
            reason,
            status: "PendingApproval"
          });
        }
      } else {
        const res = await adjustStock.mutateAsync({
          productVariantId: productId,
          warehouseId,
          locationId: hasLocations && locationId ? locationId : null,
          enteredUomId: uomId,
          quantity: numQty,
          direction,
          reason,
          operationalDate: opDate,
          postingDate: postDate,
          idempotencyKey
        });

        if (onAdjustmentCreated) {
          onAdjustmentCreated({
            id: res.operationId,
            productVariantId: productId,
            warehouseName: selectedWarehouse?.name ?? "المخزن الرئيسي",
            quantity: numQty,
            direction,
            reason,
            status: "Applied"
          });
        }
      }

      onClose();
    } catch (err) {
      const mapped = mapInventoryError(err);
      setErrorMessage(t(mapped.messageKey as any, { defaultValue: mapped.fallbackAr }));
      if (mapped.code === "inventory.profile_not_found" && canManageProfile) {
        setRequiresProfileSetup(true);
      }
    }
  };

  const reasonOptions: AdjustmentReason[] = [
    "Damage",
    "Loss",
    "CountDifference",
    "FoundStock",
    "DataCorrection",
    "Scrap",
    "UomCorrection",
    "OtherApproved"
  ];

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open) => !open && onClose()}
      title={t("adjustments.newAdjustment")}
      size="medium"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {errorMessage && (
          <div role="alert" style={{ padding: "0.75rem", backgroundColor: "#fee2e2", border: "1px solid #ef4444", borderRadius: "var(--radius-sm)", color: "#b91c1c", fontSize: "0.875rem", display: "flex", flexDirection: "column", gap: "0.75rem" }}>
            <span>{errorMessage}</span>
            {requiresProfileSetup ? (
              <Button variant="secondary" type="button" onPress={() => setIsProfileSetupOpen(true)}>
                {t("adjustments.configureProductForInventory")}
              </Button>
            ) : null}
          </div>
        )}
        {successInfo && (
          <div role="status" style={{ padding: "0.75rem", backgroundColor: "#dcfce7", border: "1px solid #22c55e", borderRadius: "var(--radius-sm)", color: "#15803d", fontSize: "0.875rem" }}>
            {successInfo}
          </div>
        )}

        <InventoryProductPicker
          label={t("adjustments.product")}
          value={productId}
          onChange={setProductId}
          isRequired
        />

        <div>
          <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.25rem" }}>
            {t("adjustments.warehouse")}
          </label>
          <select
            value={warehouseId}
            onChange={(e) => setWarehouseId(e.target.value)}
            style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "var(--radius-md, 0.5rem)", border: "1px solid var(--color-border-default)", backgroundColor: "var(--color-surface-card)" }}
          >
            {warehouses.map(w => (
              <option key={w.id} value={w.id}>{w.code} - {w.name}</option>
            ))}
          </select>
        </div>

        {hasLocations && locations.length > 0 && (
          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.25rem" }}>
              {t("adjustments.location")}
            </label>
            <select
              value={locationId}
              onChange={(e) => setLocationId(e.target.value)}
              style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "var(--radius-md, 0.5rem)", border: "1px solid var(--color-border-default)", backgroundColor: "var(--color-surface-card)" }}
            >
              <option value="">{t("common.filter")}</option>
              {locations.map(loc => (
                <option key={loc.id} value={loc.id}>{loc.code} - {loc.name}</option>
              ))}
            </select>
          </div>
        )}

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.25rem" }}>
              {t("adjustments.direction")}
            </label>
            <select
              value={direction}
              onChange={(e) => setDirection(e.target.value as AdjustmentDirection)}
              style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "var(--radius-md, 0.5rem)", border: "1px solid var(--color-border-default)", backgroundColor: "var(--color-surface-card)" }}
            >
              <option value="Increase">{t("adjustments.directions.Increase")}</option>
              <option value="Decrease">{t("adjustments.directions.Decrease")}</option>
            </select>
          </div>
          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.25rem" }}>
              {t("adjustments.reason")}
            </label>
            <select
              value={reason}
              onChange={(e) => setReason(e.target.value as AdjustmentReason)}
              style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "var(--radius-md, 0.5rem)", border: "1px solid var(--color-border-default)", backgroundColor: "var(--color-surface-card)" }}
            >
              {reasonOptions.map(r => (
                <option key={r} value={r}>
                  {t(`adjustments.reasons.${r}` as any)}
                </option>
              ))}
            </select>
          </div>
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
          <TextField
            label={t("adjustments.quantity")}
            value={quantity}
            onChange={setQuantity}
            isRequired
          />
          <LocalDateField
            label={t("operations.operationalDate")}
            value={opDate}
            onChange={(val) => setOpDate(val ?? today)}
          />
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "0.5rem" }}>
          <Button variant="secondary" onPress={onClose}>
            {t("common.cancel")}
          </Button>
          <Button variant="primary" type="submit">
            {t("adjustments.newAdjustment")}
          </Button>
        </div>
      </form>
      <InventoryProfileSetupModal
        isOpen={isProfileSetupOpen}
        onClose={() => setIsProfileSetupOpen(false)}
        onSaved={() => {
          setRequiresProfileSetup(false);
          setErrorMessage(null);
          setSuccessInfo(t("adjustments.profileConfiguredNotice"));
        }}
        legalEntityId={legalEntityId}
        productVariantId={productId}
        warehouses={warehouses}
        initialWarehouseId={warehouseId}
      />
    </Dialog>
  );
}

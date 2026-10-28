import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { LocalDateField } from "../../../../components/fields/local-date-field.tsx";
import { QuantityDisplay } from "../../../../components/fields/quantity-display.tsx";
import { useInventoryMutations, useLocations, useStockBalances, usePickingSuggestions } from "../../hooks/use-inventory-queries.ts";
import { mapInventoryError } from "../../services/inventory-error-map.ts";
import { toQty } from "../../utils.ts";
import type { Warehouse } from "../../types.ts";

export interface IssueStockModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly warehouses: readonly Warehouse[];
  readonly canOverrideNegative?: boolean;
  readonly initialProductVariantId?: string;
  readonly initialWarehouseId?: string;
}

export function IssueStockModal({
  isOpen,
  onClose,
  legalEntityId,
  warehouses,
  canOverrideNegative = false,
  initialProductVariantId,
  initialWarehouseId
}: IssueStockModalProps): React.JSX.Element | null {
  const { t } = useTranslation("inventory");
  const today = new Date().toISOString().slice(0, 10);

  const [productId, setProductId] = useState(initialProductVariantId ?? "");
  const [warehouseId, setWarehouseId] = useState(initialWarehouseId ?? (warehouses[0]?.id ?? ""));
  const [locationId, setLocationId] = useState<string>("");
  const [quantity, setQuantity] = useState<string>("1");
  const [uomId, setUomId] = useState<string>("2b12a81b-ea82-a151-af7f-4c60a017f44d");
  const [opDate, setOpDate] = useState<string>(today);
  const [postDate, setPostDate] = useState<string>(today);
  const [allowNegativeOverride, setAllowNegativeOverride] = useState<boolean>(false);
  const [sourceDoc, setSourceDoc] = useState<string>("");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [successInfo, setSuccessInfo] = useState<string | null>(null);

  const selectedWarehouse = warehouses.find(w => w.id === warehouseId);
  const hasLocations = selectedWarehouse && selectedWarehouse.locationMode !== "WarehouseOnly";
  const { data: locations = [] } = useLocations(legalEntityId, hasLocations ? warehouseId : undefined);

  // Balances to show available
  const { data: balances = [] } = useStockBalances(legalEntityId, {
    ...(productId ? { productVariantId: productId } : {}),
    ...(warehouseId ? { warehouseId } : {})
  });
  const currentAvailable = balances
    .filter(b => (!hasLocations || !locationId || b.locationId === locationId) && b.stockStatus === "Available")
    .reduce((sum, b) => sum + b.availableBaseQuantity, 0);

  // Picking suggestions
  const { data: pickingSuggestions = [] } = usePickingSuggestions(
    legalEntityId,
    productId || undefined,
    warehouseId || undefined
  );

  const { issueStock } = useInventoryMutations(legalEntityId);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMessage(null);
    setSuccessInfo(null);

    const numQty = parseFloat(quantity);
    if (isNaN(numQty) || numQty <= 0) {
      setErrorMessage(t("errors.quantityInvalid"));
      return;
    }

    if (numQty > currentAvailable && !allowNegativeOverride) {
      setErrorMessage(t("operations.insufficientWarning"));
      return;
    }

    try {
      const idempotencyKey = `iss-${Date.now()}-${Math.random().toString(36).slice(2, 7)}`;
      const result = await issueStock.mutateAsync({
        productVariantId: productId || "a6315416-cffe-bf52-bcf0-d5e590d3bf94",
        warehouseId,
        locationId: hasLocations && locationId ? locationId : null,
        enteredUomId: uomId,
        enteredQuantity: numQty,
        operationalDate: opDate,
        postingDate: postDate,
        idempotencyKey,
        sourceModule: "inventory",
        sourceDocumentId: null
      });

      setSuccessInfo(`${t("operations.operationSuccess")} ${result.operationNumber}`);
      setTimeout(() => {
        onClose();
      }, 1200);
    } catch (err) {
      const mapped = mapInventoryError(err);
      setErrorMessage(t(mapped.messageKey as any, { defaultValue: mapped.fallbackAr }));
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open) => !open && onClose()}
      title={t("operations.issueTitle")}
      size="medium"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {errorMessage && (
          <div role="alert" style={{ padding: "0.75rem", backgroundColor: "#fee2e2", border: "1px solid #ef4444", borderRadius: "var(--radius-sm)", color: "#b91c1c", fontSize: "0.875rem" }}>
            {errorMessage}
          </div>
        )}
        {successInfo && (
          <div role="status" style={{ padding: "0.75rem", backgroundColor: "#dcfce7", border: "1px solid #22c55e", borderRadius: "var(--radius-sm)", color: "#15803d", fontSize: "0.875rem" }}>
            {successInfo}
          </div>
        )}

        <TextField
          label={t("operations.product")}
          value={productId}
          onChange={setProductId}
          isRequired
        />

        <div>
          <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.25rem" }}>
            {t("operations.warehouse")}
          </label>
          <select
            value={warehouseId}
            onChange={(e) => setWarehouseId(e.target.value)}
            style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "var(--radius-md, 0.5rem)", border: "1px solid var(--color-border-default)", backgroundColor: "var(--color-surface-card)" }}
          >
            {warehouses.map(w => (
              <option key={w.id} value={w.id}>
                {w.code} - {w.name}
              </option>
            ))}
          </select>
        </div>

        {hasLocations && locations.length > 0 && (
          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.25rem" }}>
              {t("operations.location")}
            </label>
            <select
              value={locationId}
              onChange={(e) => setLocationId(e.target.value)}
              style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "var(--radius-md, 0.5rem)", border: "1px solid var(--color-border-default)", backgroundColor: "var(--color-surface-card)" }}
            >
              <option value="">{t("common.filter")}</option>
              {locations.map(loc => (
                <option key={loc.id} value={loc.id}>
                  {loc.code} - {loc.name}
                </option>
              ))}
            </select>
          </div>
        )}

        {/* Available quantity display */}
        <div style={{ padding: "0.625rem 0.875rem", backgroundColor: "var(--color-surface-subtle, #f8fafc)", border: "1px solid var(--color-border-default)", borderRadius: "var(--radius-sm)", display: "flex", justifyContent: "space-between", alignItems: "center" }}>
          <span style={{ fontSize: "0.875rem", color: "var(--color-content-muted)" }}>{t("operations.availableBeforeIssue")}:</span>
          <span style={{ fontSize: "1rem", fontWeight: 700, color: currentAvailable > 0 ? "var(--color-status-success)" : "var(--color-status-danger)" }}>
            <QuantityDisplay value={toQty(currentAvailable)} />
          </span>
        </div>

        {/* Picking suggestions banner if available */}
        {pickingSuggestions.length > 0 && (
          <div style={{ padding: "0.625rem", backgroundColor: "#eff6ff", border: "1px solid #bfdbfe", borderRadius: "var(--radius-sm)", fontSize: "0.8125rem", color: "#1e40af" }}>
            <div style={{ fontWeight: 600, marginBottom: "0.25rem" }}>{t("locator.suggestedPicking")}:</div>
            {pickingSuggestions.map((sug, idx) => (
              <div key={idx} style={{ display: "flex", justifyContent: "space-between", margin: "0.125rem 0" }}>
                <span>{sug.locationCode} ({sug.reason}):</span>
                <span style={{ fontWeight: 600 }}>{sug.availableQuantity ?? 0}</span>
              </div>
            ))}
          </div>
        )}

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
          <TextField
            label={t("operations.quantity")}
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

        <TextField
          label={t("operations.sourceDocument")}
          value={sourceDoc}
          onChange={setSourceDoc}
        />

        {canOverrideNegative && (
          <div style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
            <input
              type="checkbox"
              id="issue-negative-override"
              checked={allowNegativeOverride}
              onChange={(e) => setAllowNegativeOverride(e.target.checked)}
            />
            <label htmlFor="issue-negative-override" style={{ fontSize: "0.8125rem", color: "var(--color-status-warning, #d97706)", fontWeight: 500 }}>
              {t("operations.negativeOverrideCheckbox")}
            </label>
          </div>
        )}

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "0.5rem" }}>
          <Button variant="secondary" onPress={onClose}>
            {t("common.cancel")}
          </Button>
          <Button variant="primary" type="submit" isLoading={issueStock.isPending}>
            {issueStock.isPending ? t("common.saving") : t("operations.confirmIssue")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

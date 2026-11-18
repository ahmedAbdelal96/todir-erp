import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { Select } from "../../../../components/primitives/select.tsx";
import { Alert } from "../../../../components/primitives/alert.tsx";
import { useInventoryMutations } from "../../hooks/use-inventory-queries.ts";
import { mapInventoryError } from "../../services/inventory-error-map.ts";
import type {
  CostingMethod,
  NegativeStockPolicy,
  ReservationMode,
  TrackingMode,
  ValuationMode,
  Warehouse
} from "../../types.ts";

export interface InventoryProfileSetupModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly onSaved: () => void;
  readonly legalEntityId: string;
  readonly productVariantId: string;
  readonly warehouses: readonly Warehouse[];
  readonly initialWarehouseId?: string | undefined;
}

export function InventoryProfileSetupModal({
  isOpen,
  onClose,
  onSaved,
  legalEntityId,
  productVariantId,
  warehouses,
  initialWarehouseId
}: InventoryProfileSetupModalProps): React.JSX.Element | null {
  const { t } = useTranslation("inventory");
  const [tracking, setTracking] = useState<TrackingMode>("Quantity");
  const [valuation, setValuation] = useState<ValuationMode>("NonValuated");
  const [costing, setCosting] = useState<CostingMethod>("MovingAverage");
  const [negativeStock, setNegativeStock] = useState<NegativeStockPolicy>("Disallow");
  const [reservationMode, setReservationMode] = useState<ReservationMode>("Disabled");
  const [defaultWarehouseId, setDefaultWarehouseId] = useState(
    initialWarehouseId ?? warehouses[0]?.id ?? ""
  );
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const { saveProfile } = useInventoryMutations(legalEntityId);

  const warehouseOptions = warehouses.map((warehouse) => ({
    id: warehouse.id,
    label: `${warehouse.code} — ${warehouse.name}`
  }));
  const trackingOptions = (Object.keys({ Quantity: true, Lot: true, Serial: true }) as TrackingMode[]).map((key) => ({
    id: key,
    label: t(`trackingModes.${key}`)
  }));
  const valuationOptions = (Object.keys({ NonValuated: true, Perpetual: true }) as ValuationMode[]).map((key) => ({
    id: key,
    label: t(`valuationModes.${key}`)
  }));
  const costingOptions = (Object.keys({ MovingAverage: true, FIFO: true }) as CostingMethod[]).map((key) => ({
    id: key,
    label: t(`costingMethods.${key}`)
  }));
  const negativeStockOptions = (Object.keys({ Disallow: true, AllowWithWarningAndPermission: true, Allow: true }) as NegativeStockPolicy[]).map((key) => ({
    id: key,
    label: t(`negativeStockPolicies.${key}`)
  }));
  const reservationOptions = (Object.keys({ Disabled: true, Advisory: true, Enforced: true }) as ReservationMode[]).map((key) => ({
    id: key,
    label: t(`reservationModes.${key}`)
  }));

  const handleSubmit = async (event: React.FormEvent) => {
    event.preventDefault();
    setErrorMessage(null);

    try {
      await saveProfile.mutateAsync({
        productVariantId,
        request: {
          productVariantId,
          state: "StockTracked",
          tracking,
          valuation,
          costing: valuation === "Perpetual" ? costing : null,
          negativeStock,
          defaultWarehouseId: defaultWarehouseId || null,
          reservationMode
        }
      });
      onSaved();
      onClose();
    } catch (error) {
      const mapped = mapInventoryError(error);
      setErrorMessage(t(mapped.messageKey as any, { defaultValue: mapped.fallbackAr }));
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open) => {
        if (!open) onClose();
      }}
      title={t("profileSetup.title")}
      description={t("profileSetup.description")}
      size="medium"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "var(--space-4)" }}>
        {errorMessage ? <Alert variant="danger" title={t("profileSetup.errorTitle")}>{errorMessage}</Alert> : null}

        <Alert variant="info" title={t("profileSetup.noticeTitle")}>
          {t("profileSetup.noticeDescription")}
        </Alert>

        <Select
          label={t("profileSetup.defaultWarehouse")}
          selectedKey={defaultWarehouseId}
          onSelectionChange={(key) => setDefaultWarehouseId(String(key))}
          options={warehouseOptions}
          isRequired
        />
        <Select
          label={t("profileSetup.tracking")}
          selectedKey={tracking}
          onSelectionChange={(key) => setTracking(String(key) as TrackingMode)}
          options={trackingOptions}
          isRequired
        />
        <Select
          label={t("profileSetup.valuation")}
          selectedKey={valuation}
          onSelectionChange={(key) => setValuation(String(key) as ValuationMode)}
          options={valuationOptions}
          isRequired
        />
        {valuation === "Perpetual" ? (
          <Select
            label={t("profileSetup.costing")}
            selectedKey={costing}
            onSelectionChange={(key) => setCosting(String(key) as CostingMethod)}
            options={costingOptions}
            isRequired
          />
        ) : null}
        <Select
          label={t("profileSetup.negativeStock")}
          selectedKey={negativeStock}
          onSelectionChange={(key) => setNegativeStock(String(key) as NegativeStockPolicy)}
          options={negativeStockOptions}
          isRequired
        />
        <Select
          label={t("profileSetup.reservations")}
          selectedKey={reservationMode}
          onSelectionChange={(key) => setReservationMode(String(key) as ReservationMode)}
          options={reservationOptions}
          isRequired
        />

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "var(--space-2)" }}>
          <Button variant="secondary" type="button" onPress={onClose}>
            {t("common.cancel")}
          </Button>
          <Button variant="primary" type="submit" isDisabled={saveProfile.isPending || !defaultWarehouseId}>
            {saveProfile.isPending ? t("common.saving") : t("profileSetup.save")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

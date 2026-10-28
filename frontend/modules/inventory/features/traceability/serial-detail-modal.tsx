import React from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { LocalDateDisplay } from "../../../../components/fields/local-date-display.tsx";
import { QuantityDisplay } from "../../../../components/fields/quantity-display.tsx";
import { InventoryProductDisplay } from "../../components/inventory-product-display.tsx";
import { InventoryStatusBadge } from "../../components/inventory-status-badge.tsx";
import { toQty } from "../../utils.ts";
import type { SerialView } from "./traceability-tab.tsx";

export interface SerialDetailModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly serial: SerialView;
}

export function SerialDetailModal({
  isOpen,
  onClose,
  serial
}: SerialDetailModalProps): React.JSX.Element {
  const { t } = useTranslation("inventory");
  const samplePurchaseOrderDoc = "PO-2026-0089";

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open) => !open && onClose()}
      title={t("traceability.serialDetailTitle")}
      size="medium"
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        <p style={{ margin: 0, fontSize: "0.875rem", color: "var(--color-content-muted)" }}>
          {t("traceability.serialDetailSubtitle")}
        </p>

        {/* Master Serial Overview Card */}
        <div
          style={{
            padding: "1rem",
            backgroundColor: "var(--color-surface-subtle, #f8fafc)",
            border: "1px solid var(--color-border-default)",
            borderRadius: "var(--radius-md)",
            display: "grid",
            gridTemplateColumns: "1fr 1fr",
            gap: "0.75rem",
            fontSize: "0.875rem"
          }}
        >
          <div>
            <span style={{ color: "var(--color-content-muted)", display: "block", fontSize: "0.75rem" }}>
              {t("traceability.serialNumber")}
            </span>
            <strong style={{ color: "var(--color-brand-primary)", fontSize: "1rem", fontFamily: "monospace" }}>
              {serial.serialNumber}
            </strong>
          </div>
          <div>
            <span style={{ color: "var(--color-content-muted)", display: "block", fontSize: "0.75rem" }}>
              {t("traceability.currentStatus")}
            </span>
            <InventoryStatusBadge status={serial.status} />
          </div>
          <div>
            <span style={{ color: "var(--color-content-muted)", display: "block", fontSize: "0.75rem" }}>
              {t("traceability.product")}
            </span>
            <InventoryProductDisplay productVariantId={serial.productVariantId} />
          </div>
          <div>
            <span style={{ color: "var(--color-content-muted)", display: "block", fontSize: "0.75rem" }}>
              {t("traceability.lifecycleStage")}
            </span>
            <strong style={{ color: "var(--color-status-success)" }}>
              {serial.lifecycleStage}
            </strong>
          </div>
          <div>
            <span style={{ color: "var(--color-content-muted)", display: "block", fontSize: "0.75rem" }}>
              {t("stock.warehouse")}
            </span>
            <strong>{serial.warehouseName}</strong>
          </div>
          <div>
            <span style={{ color: "var(--color-content-muted)", display: "block", fontSize: "0.75rem" }}>
              {t("stock.location")}
            </span>
            <strong>{serial.locationName}</strong>
          </div>
          <div>
            <span style={{ color: "var(--color-content-muted)", display: "block", fontSize: "0.75rem" }}>
              {t("traceability.receivedSource")}
            </span>
            <span>{t("sourceModules.purchasing")}{" "}{samplePurchaseOrderDoc}</span>
          </div>
          <div>
            <span style={{ color: "var(--color-content-muted)", display: "block", fontSize: "0.75rem" }}>
              {t("stock.onHand")}
            </span>
            <strong>
              <QuantityDisplay value={toQty(1)} />
            </strong>
          </div>
        </div>

        {/* Movement History Timeline */}
        <div>
          <h4 style={{ margin: "0 0 0.5rem", fontSize: "0.875rem", color: "var(--color-content-primary)" }}>
            {t("traceability.movementHistoryTitle")}
          </h4>
          <div
            style={{
              padding: "0.75rem 1rem",
              border: "1px solid var(--color-border-subtle)",
              borderRadius: "var(--radius-sm)",
              backgroundColor: "var(--color-surface-card)",
              display: "flex",
              flexDirection: "column",
              gap: "0.5rem",
              fontSize: "0.8125rem"
            }}
          >
            <div style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
              <span style={{ color: "var(--color-status-success)" }}>●</span>
              <span><strong>{t("movementTypes.Receipt")}: </strong><LocalDateDisplay value="2026-10-01" /> — {t("sourceModules.purchasing")}{" "}{samplePurchaseOrderDoc}</span>
            </div>
            <div style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
              <span style={{ color: "var(--color-brand-primary)" }}>●</span>
              <span><strong>{t("operations.title")}: </strong>{t("stockStatuses.Available")} — {serial.warehouseName}</span>
            </div>
          </div>
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", marginTop: "0.5rem" }}>
          <Button variant="primary" onPress={onClose} type="button">
            {t("common.close")}
          </Button>
        </div>
      </div>
    </Dialog>
  );
}

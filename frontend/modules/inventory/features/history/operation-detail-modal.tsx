import React from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { LocalDateDisplay } from "../../../../components/fields/local-date-display.tsx";
import { QuantityDisplay } from "../../../../components/fields/quantity-display.tsx";
import { InventoryStatusBadge } from "../../components/inventory-status-badge.tsx";
import { toQty } from "../../utils.ts";
import type { InventoryMovement } from "../../types.ts";

export interface OperationDetailModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly movement: InventoryMovement;
  readonly warehouseName?: string | undefined;
  readonly onReverseClick?: ((movement: InventoryMovement) => void) | undefined;
}

export function OperationDetailModal({
  isOpen,
  onClose,
  movement,
  warehouseName,
  onReverseClick
}: OperationDetailModalProps): React.JSX.Element | null {
  const { t } = useTranslation("inventory");

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open) => !open && onClose()}
      title={t("history.operationDetailTitle")}
      size="medium"
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        <p style={{ margin: 0, fontSize: "0.875rem", color: "var(--color-content-muted)" }}>
          {t("history.operationDetailSubtitle")}
        </p>

        {/* Overview Box */}
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
              {t("history.operationNumber")}
            </span>
            <strong style={{ color: "var(--color-brand-primary)" }}>{movement.operationId}</strong>
          </div>
          <div>
            <span style={{ color: "var(--color-content-muted)", display: "block", fontSize: "0.75rem" }}>
              {t("history.operationType")}
            </span>
            <strong>{movement.movementType}</strong>
          </div>
          <div>
            <span style={{ color: "var(--color-content-muted)", display: "block", fontSize: "0.75rem" }}>
              {t("history.date")}
            </span>
            <LocalDateDisplay value={movement.operationalDate} />
          </div>
          <div>
            <span style={{ color: "var(--color-content-muted)", display: "block", fontSize: "0.75rem" }}>
              {t("history.stockStatus")}
            </span>
            <InventoryStatusBadge status={movement.stockStatus} />
          </div>
          <div>
            <span style={{ color: "var(--color-content-muted)", display: "block", fontSize: "0.75rem" }}>
              {t("stock.product")}
            </span>
            <span>{movement.productVariantId}</span>
          </div>
          <div>
            <span style={{ color: "var(--color-content-muted)", display: "block", fontSize: "0.75rem" }}>
              {t("stock.warehouse")}
            </span>
            <span>{warehouseName ?? movement.warehouseId}</span>
          </div>
          <div>
            <span style={{ color: "var(--color-content-muted)", display: "block", fontSize: "0.75rem" }}>
              {t("history.quantity")}
            </span>
            <strong style={{ color: movement.signedBaseQuantity >= 0 ? "var(--color-status-success)" : "var(--color-status-danger)" }}>
              <QuantityDisplay value={toQty(movement.signedBaseQuantity)} />
            </strong>
          </div>
          <div>
            <span style={{ color: "var(--color-content-muted)", display: "block", fontSize: "0.75rem" }}>
              {t("history.source")}
            </span>
            <span>{movement.sourceModule} {movement.sourceDocumentId ?? ""}</span>
          </div>
        </div>

        {/* Timeline Visualization */}
        <div>
          <h4 style={{ margin: "0 0 0.5rem", fontSize: "0.875rem", color: "var(--color-content-primary)" }}>
            {t("history.timelineTitle")}
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
              <span><strong>{t("operations.operationalDate")}: </strong><LocalDateDisplay value={movement.operationalDate} /></span>
            </div>
            <div style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
              <span style={{ color: "var(--color-brand-primary)" }}>●</span>
              <span><strong>{t("operations.postingDate")}: </strong><LocalDateDisplay value={movement.postingDate} /></span>
            </div>
            <div style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
              <span style={{ color: "var(--color-content-muted)" }}>●</span>
              <span><strong>{t("history.date")}: </strong>{movement.effectiveAt} ({movement.sequence})</span>
            </div>
          </div>
        </div>

        {/* Actions */}
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginTop: "0.5rem" }}>
          {onReverseClick ? (
            <Button
              variant="secondary"
              onPress={() => {
                onClose();
                onReverseClick(movement);
              }}
            >
              {t("history.reverseOperation")}
            </Button>
          ) : <div />}
          <Button variant="primary" onPress={onClose} type="button">
            {t("common.close")}
          </Button>
        </div>
      </div>
    </Dialog>
  );
}

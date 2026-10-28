import React from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { LocalDateDisplay } from "../../../../components/fields/local-date-display.tsx";
import { QuantityDisplay } from "../../../../components/fields/quantity-display.tsx";
import { InventoryProductDisplay } from "../../components/inventory-product-display.tsx";
import { toQty } from "../../utils.ts";
import type { LotView } from "./traceability-tab.tsx";

export interface LotDetailModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly lot: LotView;
}

export function LotDetailModal({
  isOpen,
  onClose,
  lot
}: LotDetailModalProps): React.JSX.Element {
  const { t } = useTranslation("inventory");
  const samplePurchaseOrderDoc = "PO-2026-0045";

  let badgeBg = "#dcfce7";
  let badgeColor = "#15803d";
  let labelKey = "traceability.expiryIndicators.active";
  if (lot.status === "ExpiringSoon") {
    badgeBg = "#fef3c7";
    badgeColor = "#b45309";
    labelKey = "traceability.expiryIndicators.expiringSoon";
  } else if (lot.status === "Expired") {
    badgeBg = "#fee2e2";
    badgeColor = "#b91c1c";
    labelKey = "traceability.expiryIndicators.expired";
  }

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open) => !open && onClose()}
      title={t("traceability.lotDetailTitle")}
      size="medium"
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        <p style={{ margin: 0, fontSize: "0.875rem", color: "var(--color-content-muted)" }}>
          {t("traceability.lotDetailSubtitle")}
        </p>

        {/* Master Lot Overview Card */}
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
              {t("traceability.lotNumber")}
            </span>
            <strong style={{ color: "var(--color-brand-primary)", fontSize: "1rem" }}>
              {lot.lotNumber}
            </strong>
          </div>
          <div>
            <span style={{ color: "var(--color-content-muted)", display: "block", fontSize: "0.75rem" }}>
              {t("traceability.currentStatus")}
            </span>
            <span
              style={{
                display: "inline-block",
                padding: "0.25rem 0.625rem",
                borderRadius: "var(--radius-full, 9999px)",
                fontSize: "0.75rem",
                fontWeight: 600,
                backgroundColor: badgeBg,
                color: badgeColor
              }}
            >
              {t(labelKey as any)}
            </span>
          </div>
          <div>
            <span style={{ color: "var(--color-content-muted)", display: "block", fontSize: "0.75rem" }}>
              {t("traceability.product")}
            </span>
            <InventoryProductDisplay productVariantId={lot.productVariantId} />
          </div>
          <div>
            <span style={{ color: "var(--color-content-muted)", display: "block", fontSize: "0.75rem" }}>
              {t("traceability.supplierRef")}
            </span>
            <strong>{lot.supplierReference ?? "—"}</strong>
          </div>
          <div>
            <span style={{ color: "var(--color-content-muted)", display: "block", fontSize: "0.75rem" }}>
              {t("traceability.manufacturedDate")}
            </span>
            {lot.manufacturedOn ? <LocalDateDisplay value={lot.manufacturedOn} /> : "—"}
          </div>
          <div>
            <span style={{ color: "var(--color-content-muted)", display: "block", fontSize: "0.75rem" }}>
              {t("traceability.expiryDate")}
            </span>
            {lot.expiresOn ? <LocalDateDisplay value={lot.expiresOn} /> : "—"}
          </div>
          <div>
            <span style={{ color: "var(--color-content-muted)", display: "block", fontSize: "0.75rem" }}>
              {t("stock.onHand")}
            </span>
            <strong style={{ fontSize: "1rem", color: "var(--color-brand-primary)" }}>
              <QuantityDisplay value={toQty(lot.quantity)} />
            </strong>
          </div>
          <div>
            <span style={{ color: "var(--color-content-muted)", display: "block", fontSize: "0.75rem" }}>
              {t("traceability.receivedSource")}
            </span>
            <span>{t("sourceModules.purchasing")}{" "}{samplePurchaseOrderDoc}</span>
          </div>
        </div>

        {/* Warehouse Distribution */}
        <div>
          <h4 style={{ margin: "0 0 0.5rem", fontSize: "0.875rem", color: "var(--color-content-primary)" }}>
            {t("traceability.distributionTitle")}
          </h4>
          <div
            style={{
              padding: "0.75rem 1rem",
              border: "1px solid var(--color-border-subtle)",
              borderRadius: "var(--radius-sm)",
              backgroundColor: "var(--color-surface-card)",
              display: "flex",
              justifyContent: "space-between",
              alignItems: "center",
              fontSize: "0.8125rem"
            }}
          >
            <div>
              <strong>{t("traceability.sampleWarehouse")}</strong>
              <span style={{ color: "var(--color-content-muted)", display: "block", fontSize: "0.75rem" }}>
                {t("traceability.sampleLocation")}
              </span>
            </div>
            <span style={{ fontWeight: 700, color: "var(--color-brand-primary)" }}>
              <QuantityDisplay value={toQty(lot.quantity)} />
            </span>
          </div>
        </div>

        {/* Movement History Table */}
        <div>
          <h4 style={{ margin: "0 0 0.5rem", fontSize: "0.875rem", color: "var(--color-content-primary)" }}>
            {t("traceability.movementHistoryTitle")}
          </h4>
          <table style={{ width: "100%", fontSize: "0.8125rem", borderCollapse: "collapse", border: "1px solid var(--color-border-subtle)" }}>
            <thead>
              <tr style={{ backgroundColor: "var(--color-surface-subtle)", borderBottom: "1px solid var(--color-border-subtle)" }}>
                <th style={{ padding: "0.5rem 0.75rem", textAlign: "start" }}>{t("history.date")}</th>
                <th style={{ padding: "0.5rem 0.75rem", textAlign: "start" }}>{t("history.operationType")}</th>
                <th style={{ padding: "0.5rem 0.75rem", textAlign: "end" }}>{t("history.quantity")}</th>
                <th style={{ padding: "0.5rem 0.75rem", textAlign: "start" }}>{t("history.source")}</th>
              </tr>
            </thead>
            <tbody>
              <tr style={{ borderBottom: "1px solid var(--color-border-subtle)" }}>
                <td style={{ padding: "0.5rem 0.75rem" }}><LocalDateDisplay value={lot.manufacturedOn ?? "2026-01-15"} /></td>
                <td style={{ padding: "0.5rem 0.75rem" }}>{t("movementTypes.Receipt")}</td>
                <td style={{ padding: "0.5rem 0.75rem", textAlign: "end", fontWeight: 600, color: "var(--color-status-success)" }}>
                  +{lot.quantity}
                </td>
                <td style={{ padding: "0.5rem 0.75rem" }}>{t("sourceModules.purchasing")}{" "}{samplePurchaseOrderDoc}</td>
              </tr>
            </tbody>
          </table>
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

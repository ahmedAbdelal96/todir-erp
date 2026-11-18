import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { InventoryKpiCard } from "../../components/inventory-kpi-card.tsx";
import { InventoryDataTable } from "../../components/inventory-data-table.tsx";
import { InventoryStatusBadge } from "../../components/inventory-status-badge.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { QuantityDisplay } from "../../../../components/fields/quantity-display.tsx";
import { EmptyState } from "../../../../components/primitives/empty-state.tsx";
import { InventoryProductDisplay } from "../../components/inventory-product-display.tsx";
import { ProductStockCardModal } from "../stock/product-stock-card-modal.tsx";
import { toQty } from "../../utils.ts";
import type { Warehouse, StockBalance, NegativeStockCondition } from "../../types.ts";

export interface OverviewTabProps {
  readonly legalEntityId: string;
  readonly balances: readonly StockBalance[];
  readonly warehouses: readonly Warehouse[];
  readonly negativeConditions: readonly NegativeStockCondition[];
  readonly canReadCost: boolean;
  readonly onOpenReceive: () => void;
  readonly onOpenIssue: () => void;
  readonly onOpenTransfer: () => void;
  readonly onOpenCount: () => void;
  readonly onOpenAdjust: () => void;
  readonly onSelectProduct?: (variantId: string) => void;
}

export function OverviewTab({
  legalEntityId,
  balances,
  warehouses,
  negativeConditions,
  canReadCost,
  onOpenReceive,
  onOpenIssue,
  onOpenTransfer,
  onOpenCount,
  onOpenAdjust,
  onSelectProduct
}: OverviewTabProps): React.JSX.Element {
  const { t } = useTranslation("inventory");

  const [search, setSearch] = useState("");
  const [selectedVariantId, setSelectedVariantId] = useState<string | null>(null);

  const itemsWithStockCount = new Set(balances.filter(b => b.onHandBaseQuantity > 0).map(b => b.productVariantId)).size;
  const itemsWithReservationsCount = new Set(balances.filter(b => b.reservedBaseQuantity > 0).map(b => b.productVariantId)).size;
  const lowOrCriticalCount = new Set(
    balances
      .filter(b => b.stockStatus === "Damaged" || b.stockStatus === "Quarantine" || b.availableBaseQuantity <= 0)
      .map(b => b.productVariantId)
  ).size;
  const inTransitCount = new Set(
    balances.filter(b => b.stockStatus === "InTransit").map(b => b.productVariantId)
  ).size;

  const activeNegatives = negativeConditions.filter(n => n.status === "Active");

  const filteredBalances = balances.filter(b => {
    if (!search) return true;
    const s = search.toLowerCase();
    const w = warehouses.find(wh => wh.id === b.warehouseId);
    return (
      b.productVariantId.toLowerCase().includes(s) ||
      b.stockStatus.toLowerCase().includes(s) ||
      (w && (w.name.toLowerCase().includes(s) || w.code.toLowerCase().includes(s)))
    );
  }).slice(0, 15);

  const warehouseName = (id: string) => warehouses.find(w => w.id === id)?.name ?? id;

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
      {/* KPI Cards Row — Dimension-Safe Counts */}
      <div style={{ display: "flex", flexWrap: "wrap", gap: "1rem" }}>
        <InventoryKpiCard
          title={t("kpis.activeStockItems")}
          value={itemsWithStockCount}
          tone="default"
        />
        <InventoryKpiCard
          title={t("kpis.itemsWithReservations")}
          value={itemsWithReservationsCount}
          tone="warning"
        />
        <InventoryKpiCard
          title={t("kpis.lowOrCriticalItems")}
          value={lowOrCriticalCount}
          tone="danger"
        />
        <InventoryKpiCard
          title={t("kpis.inTransitTransfers")}
          value={inTransitCount}
          tone="info"
        />
        <InventoryKpiCard
          title={t("kpis.attentionNeeded")}
          value={activeNegatives.length}
          subtitle={activeNegatives.length > 0 ? t("kpis.negativeConditions") : t("kpis.noAttention")}
          tone={activeNegatives.length > 0 ? "danger" : "default"}
        />
      </div>

      {/* Negative Stock Alert Banner if active */}
      {activeNegatives.length > 0 && (
        <div
          role="alert"
          style={{
            padding: "1rem",
            backgroundColor: "#fef2f2",
            border: "1px solid #f87171",
            borderRadius: "var(--radius-md)",
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            flexWrap: "wrap",
            gap: "0.5rem"
          }}
        >
          <div>
            <strong style={{ color: "#b91c1c" }}>{t("kpis.negativeConditions")}: </strong>
            <span style={{ color: "#7f1d1d", fontSize: "0.875rem" }}>
              {activeNegatives.length} {t("stock.warehouseBreakdown")}
            </span>
          </div>
          <Button variant="secondary" onPress={onOpenAdjust}>
            {t("quickActions.adjust")}
          </Button>
        </div>
      )}

      {/* Fast Action Bar */}
      <div
        style={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          flexWrap: "wrap",
          gap: "1rem",
          padding: "1rem",
          backgroundColor: "var(--color-surface-card)",
          border: "1px solid var(--color-border-default)",
          borderRadius: "var(--radius-md)"
        }}
      >
        <div style={{ display: "flex", gap: "0.5rem", flexWrap: "wrap" }}>
          <Button variant="primary" onPress={onOpenReceive}>
            + {t("quickActions.receive")}
          </Button>
          <Button variant="secondary" onPress={onOpenIssue}>
            {t("quickActions.issue")}
          </Button>
          <Button variant="secondary" onPress={onOpenTransfer}>
            {t("quickActions.transfer")}
          </Button>
          <Button variant="secondary" onPress={onOpenCount}>
            {t("quickActions.count")}
          </Button>
          <Button variant="secondary" onPress={onOpenAdjust}>
            {t("quickActions.adjust")}
          </Button>
        </div>

        <div style={{ minWidth: "260px", maxWidth: "380px", flexGrow: 1 }}>
          <TextField
            value={search}
            onChange={setSearch}
            placeholder={t("quickActions.quickSearchPlaceholder")}
            aria-label={t("quickActions.quickSearchPlaceholder")}
          />
        </div>
      </div>

      {/* Overview Table / Recent Balances */}
      <div>
        <h3 style={{ margin: "0 0 0.75rem", fontSize: "1.125rem", color: "var(--color-brand-primary)" }}>
          {t("stock.title")}
        </h3>

        {filteredBalances.length === 0 ? (
          <EmptyState
            title={t("stock.title")}
            description={t("stock.noBalances")}
          />
        ) : (
          <InventoryDataTable minWidth="850px" ariaLabel={t("stock.title")}>
            <thead>
              <tr style={{ backgroundColor: "var(--color-surface-subtle, #f8fafc)", borderBottom: "1px solid var(--color-border-default)" }}>
                <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("stock.product")}</th>
                <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("stock.warehouse")}</th>
                <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("stock.status")}</th>
                <th style={{ padding: "0.75rem 1rem", textAlign: "end" }}>{t("stock.onHand")}</th>
                <th style={{ padding: "0.75rem 1rem", textAlign: "end" }}>{t("stock.reserved")}</th>
                <th style={{ padding: "0.75rem 1rem", textAlign: "end" }}>{t("stock.available")}</th>
                <th style={{ padding: "0.75rem 1rem", textAlign: "center" }}>{t("stock.actions")}</th>
              </tr>
            </thead>
            <tbody>
              {filteredBalances.map((b, idx) => (
                <tr
                  key={`${b.productVariantId}-${b.warehouseId}-${b.locationId}-${idx}`}
                  style={{ borderBottom: "1px solid var(--color-border-subtle, #e2e8f0)" }}
                >
                  <td style={{ padding: "0.75rem 1rem", fontWeight: 600 }}>
                    <button
                      type="button"
                      onClick={() => setSelectedVariantId(b.productVariantId)}
                      style={{
                        background: "none",
                        border: "none",
                        padding: 0,
                        color: "var(--color-brand-primary)",
                        cursor: "pointer",
                        textDecoration: "underline",
                        fontWeight: 600,
                        fontSize: "0.875rem",
                        textAlign: "start"
                      }}
                    >
                      <InventoryProductDisplay
                        productVariantId={b.productVariantId}
                        displayName={b.productDisplayName}
                        sku={b.productSku}
                      />
                    </button>
                  </td>
                  <td style={{ padding: "0.75rem 1rem" }}>{warehouseName(b.warehouseId)}</td>
                  <td style={{ padding: "0.75rem 1rem" }}>
                    <InventoryStatusBadge status={b.stockStatus} />
                  </td>
                  <td style={{ padding: "0.75rem 1rem", textAlign: "end", fontWeight: 700 }}>
                    <QuantityDisplay value={toQty(b.onHandBaseQuantity)} />
                  </td>
                  <td style={{ padding: "0.75rem 1rem", textAlign: "end", color: "var(--color-status-warning)" }}>
                    <QuantityDisplay value={toQty(b.reservedBaseQuantity)} />
                  </td>
                  <td style={{ padding: "0.75rem 1rem", textAlign: "end", fontWeight: 700, color: "var(--color-status-success)" }}>
                    <QuantityDisplay value={toQty(b.availableBaseQuantity)} />
                  </td>
                  <td style={{ padding: "0.75rem 1rem", textAlign: "center" }}>
                    <Button
                      variant="secondary"
                      onPress={() => setSelectedVariantId(b.productVariantId)}
                    >
                      {t("stock.viewCard")}
                    </Button>
                  </td>
                </tr>
              ))}
            </tbody>
          </InventoryDataTable>
        )}
      </div>

      {/* Stock Card Drilldown Modal */}
      {selectedVariantId && (
        <ProductStockCardModal
          isOpen={Boolean(selectedVariantId)}
          onClose={() => setSelectedVariantId(null)}
          productVariantId={selectedVariantId}
          balances={balances}
          warehouses={warehouses}
          onReceiveClick={onOpenReceive}
          onIssueClick={onOpenIssue}
          onTransferClick={onOpenTransfer}
        />
      )}
    </div>
  );
}

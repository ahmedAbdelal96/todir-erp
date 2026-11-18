import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { InventoryDataTable } from "../../components/inventory-data-table.tsx";
import { InventoryStatusBadge } from "../../components/inventory-status-badge.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { QuantityDisplay } from "../../../../components/fields/quantity-display.tsx";
import { EmptyState } from "../../../../components/primitives/empty-state.tsx";
import { InventoryProductDisplay } from "../../components/inventory-product-display.tsx";
import { ProductStockCardModal } from "./product-stock-card-modal.tsx";
import { toQty } from "../../utils.ts";
import type { Warehouse, StockBalance } from "../../types.ts";

export interface StockTabProps {
  readonly legalEntityId: string;
  readonly balances: readonly StockBalance[];
  readonly warehouses: readonly Warehouse[];
  readonly onOpenReceive: () => void;
  readonly onOpenIssue: () => void;
  readonly onOpenTransfer: () => void;
}

export function StockTab({
  legalEntityId,
  balances,
  warehouses,
  onOpenReceive,
  onOpenIssue,
  onOpenTransfer
}: StockTabProps): React.JSX.Element {
  const { t } = useTranslation("inventory");

  const [selectedWarehouseId, setSelectedWarehouseId] = useState<string>("");
  const [selectedStatus, setSelectedStatus] = useState<string>("");
  const [searchTerm, setSearchTerm] = useState<string>("");
  const [activeStockCardVariantId, setActiveStockCardVariantId] = useState<string | null>(null);

  const warehouseName = (id: string) => warehouses.find(w => w.id === id)?.name ?? id;

  const filteredBalances = balances.filter(b => {
    if (selectedWarehouseId && b.warehouseId !== selectedWarehouseId) return false;
    if (selectedStatus && b.stockStatus !== selectedStatus) return false;
    if (searchTerm) {
      const s = searchTerm.toLowerCase();
      const w = warehouses.find(wh => wh.id === b.warehouseId);
      const matchProduct = [b.productDisplayName, b.productSku].some(value => value?.toLowerCase().includes(s));
      const matchWarehouse = w && (w.name.toLowerCase().includes(s) || w.code.toLowerCase().includes(s));
      if (!matchProduct && !matchWarehouse) return false;
    }
    return true;
  });

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
      {/* Top Filter and Action Bar */}
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
        <div style={{ display: "flex", gap: "0.75rem", flexWrap: "wrap", flexGrow: 1, alignItems: "center" }}>
          <div style={{ minWidth: "200px" }}>
            <TextField
              value={searchTerm}
              onChange={setSearchTerm}
              placeholder={t("stock.searchPlaceholder")}
              aria-label={t("stock.searchPlaceholder")}
            />
          </div>

          <div style={{ minWidth: "180px" }}>
            <select
              value={selectedWarehouseId}
              onChange={(e) => setSelectedWarehouseId(e.target.value)}
              aria-label={t("stock.filterWarehouse")}
              style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "var(--radius-md, 0.5rem)", border: "1px solid var(--color-border-default)", backgroundColor: "var(--color-surface-card)" }}
            >
              <option value="">{t("stock.allWarehouses")}</option>
              {warehouses.map(w => (
                <option key={w.id} value={w.id}>
                  {w.code} - {w.name}
                </option>
              ))}
            </select>
          </div>

          <div style={{ minWidth: "160px" }}>
            <select
              value={selectedStatus}
              onChange={(e) => setSelectedStatus(e.target.value)}
              aria-label={t("stock.filterStatus")}
              style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "var(--radius-md, 0.5rem)", border: "1px solid var(--color-border-default)", backgroundColor: "var(--color-surface-card)" }}
            >
              <option value="">{t("stock.allStatuses")}</option>
              <option value="Available">{t("stockStatuses.Available")}</option>
              <option value="QualityHold">{t("stockStatuses.QualityHold")}</option>
              <option value="Quarantine">{t("stockStatuses.Quarantine")}</option>
              <option value="Damaged">{t("stockStatuses.Damaged")}</option>
              <option value="Blocked">{t("stockStatuses.Blocked")}</option>
              <option value="InTransit">{t("stockStatuses.InTransit")}</option>
            </select>
          </div>
        </div>

        <div style={{ display: "flex", gap: "0.5rem" }}>
          <Button variant="primary" onPress={onOpenReceive}>
            + {t("quickActions.receive")}
          </Button>
          <Button variant="secondary" onPress={onOpenIssue}>
            {t("quickActions.issue")}
          </Button>
        </div>
      </div>

      {/* Balances Data Table */}
      {filteredBalances.length === 0 ? (
        <EmptyState
          title={t("stock.title")}
          description={t("stock.noBalances")}
        />
      ) : (
        <InventoryDataTable minWidth="920px" ariaLabel={t("stock.title")}>
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
                key={`${b.productVariantId}-${b.warehouseId}-${b.locationId}-${b.stockStatus}-${idx}`}
                style={{ borderBottom: "1px solid var(--color-border-subtle, #e2e8f0)" }}
              >
                <td style={{ padding: "0.75rem 1rem", fontWeight: 600 }}>
                  <button
                    type="button"
                    onClick={() => setActiveStockCardVariantId(b.productVariantId)}
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
                    onPress={() => setActiveStockCardVariantId(b.productVariantId)}
                  >
                    {t("stock.viewCard")}
                  </Button>
                </td>
              </tr>
            ))}
          </tbody>
        </InventoryDataTable>
      )}

      {/* Stock Card Drilldown Modal */}
      {activeStockCardVariantId && (
        <ProductStockCardModal
          isOpen={Boolean(activeStockCardVariantId)}
          onClose={() => setActiveStockCardVariantId(null)}
          productVariantId={activeStockCardVariantId}
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

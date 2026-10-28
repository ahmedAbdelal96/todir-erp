import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { InventoryDataTable } from "../../components/inventory-data-table.tsx";
import { InventoryStatusBadge } from "../../components/inventory-status-badge.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { LocalDateDisplay } from "../../../../components/fields/local-date-display.tsx";
import { LocalDateField } from "../../../../components/fields/local-date-field.tsx";
import { QuantityDisplay } from "../../../../components/fields/quantity-display.tsx";
import { EmptyState } from "../../../../components/primitives/empty-state.tsx";
import { InventoryProductDisplay } from "../../components/inventory-product-display.tsx";
import { OperationDetailModal } from "./operation-detail-modal.tsx";
import { useMovementHistory, useStockAsOf, useInventoryMutations } from "../../hooks/use-inventory-queries.ts";
import { mapInventoryError } from "../../services/inventory-error-map.ts";
import { toQty } from "../../utils.ts";
import type { InventoryMovement, Warehouse } from "../../types.ts";

export interface HistoryTabProps {
  readonly legalEntityId: string;
  readonly warehouses: readonly Warehouse[];
  readonly canReverse?: boolean;
}

export function HistoryTab({ legalEntityId, warehouses, canReverse = true }: HistoryTabProps): React.JSX.Element {
  const { t } = useTranslation("inventory");
  const today = new Date().toISOString().slice(0, 10);

  const [selectedWarehouseId, setSelectedWarehouseId] = useState<string>("");
  const [selectedMovement, setSelectedMovement] = useState<InventoryMovement | null>(null);
  const [asOfDate, setAsOfDate] = useState<string>(today);
  const [isQueryingAsOf, setIsQueryingAsOf] = useState<boolean>(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const { data: historyPage } = useMovementHistory(legalEntityId, {
    ...(selectedWarehouseId ? { warehouseId: selectedWarehouseId } : {}),
    limit: 50
  });

  const { data: asOfResults = [] } = useStockAsOf(
    legalEntityId,
    new Date(`${asOfDate}T00:00:00Z`).toISOString(),
    { ...(selectedWarehouseId ? { warehouseId: selectedWarehouseId } : {}) },
    isQueryingAsOf
  );

  const movements = historyPage?.items ?? [];
  const warehouseName = (id: string) => warehouses.find(w => w.id === id)?.name ?? id;

  const { reverseOperation } = useInventoryMutations(legalEntityId);

  const handleReverse = async (m: InventoryMovement) => {
    setErrorMessage(null);
    try {
      const idempotencyKey = `rev-${Date.now()}-${Math.random().toString(36).slice(2, 7)}`;
      await reverseOperation.mutateAsync({
        operationId: m.operationId,
        operationalDate: today,
        postingDate: today,
        idempotencyKey
      });
    } catch (err) {
      const mapped = mapInventoryError(err);
      setErrorMessage(t(mapped.messageKey as any, { defaultValue: mapped.fallbackAr }));
    }
  };

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
      {errorMessage && (
        <div role="alert" style={{ padding: "0.75rem", backgroundColor: "#fee2e2", border: "1px solid #ef4444", borderRadius: "var(--radius-sm)", color: "#b91c1c", fontSize: "0.875rem" }}>
          {errorMessage}
        </div>
      )}

      {/* As-Of Historical Query Card */}
      <div
        style={{
          padding: "1rem 1.25rem",
          backgroundColor: "var(--color-surface-card)",
          border: "1px solid var(--color-border-default)",
          borderRadius: "var(--radius-md)",
          boxShadow: "0 1px 3px rgba(0, 0, 0, 0.05)"
        }}
      >
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: "0.75rem" }}>
          <div>
            <h4 style={{ margin: "0 0 0.25rem", fontSize: "0.9375rem", color: "var(--color-brand-primary)" }}>
              {t("history.asOfQueryTitle")}
            </h4>
            <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
              {t("history.subtitle")}
            </span>
          </div>

          <div style={{ display: "flex", gap: "0.5rem", alignItems: "center", flexWrap: "wrap" }}>
            <div style={{ width: "160px" }}>
              <LocalDateField
                value={asOfDate}
                onChange={(val) => {
                  setAsOfDate(val ?? today);
                  setIsQueryingAsOf(false);
                }}
              />
            </div>
            <Button
              variant="secondary"
              onPress={() => setIsQueryingAsOf(true)}
            >
              {t("history.queryAsOfButton")}
            </Button>
          </div>
        </div>

        {/* As-Of Historical Results if active */}
        {isQueryingAsOf && asOfResults.length > 0 && (
          <div style={{ marginTop: "1rem", padding: "0.75rem", backgroundColor: "var(--color-surface-subtle)", borderRadius: "var(--radius-sm)", border: "1px solid var(--color-border-subtle)" }}>
            <div style={{ fontWeight: 600, fontSize: "0.8125rem", color: "var(--color-brand-primary)", marginBottom: "0.5rem" }}>
              {t("history.historicalOnHand")} ({asOfDate}):
            </div>
            <div style={{ display: "flex", gap: "1rem", flexWrap: "wrap" }}>
              {asOfResults.map((res, idx) => (
                <div key={idx} style={{ padding: "0.5rem 0.75rem", backgroundColor: "#ffffff", borderRadius: "var(--radius-sm)", border: "1px solid var(--color-border-default)", fontSize: "0.8125rem", display: "flex", alignItems: "center", gap: "0.5rem" }}>
                  <InventoryProductDisplay productVariantId={res.productVariantId} inline />
                  <span>: </span>
                  <strong style={{ color: "var(--color-status-success)" }}>
                    <QuantityDisplay value={toQty(res.onHandBaseQuantity)} />
                  </strong>
                </div>
              ))}
            </div>
          </div>
        )}
      </div>

      {/* Movement Ledger Table */}
      <div
        style={{
          padding: "1.25rem",
          backgroundColor: "var(--color-surface-card)",
          border: "1px solid var(--color-border-default)",
          borderRadius: "var(--radius-md)",
          boxShadow: "0 1px 3px rgba(0, 0, 0, 0.05)"
        }}
      >
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "1rem", flexWrap: "wrap", gap: "0.75rem" }}>
          <h3 style={{ margin: 0, fontSize: "1.125rem", color: "var(--color-brand-primary)" }}>
            {t("history.title")}
          </h3>

          <div style={{ minWidth: "180px" }}>
            <select
              value={selectedWarehouseId}
              onChange={(e) => setSelectedWarehouseId(e.target.value)}
              aria-label={t("stock.filterWarehouse")}
              style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "var(--radius-md, 0.5rem)", border: "1px solid var(--color-border-default)", backgroundColor: "var(--color-surface-card)" }}
            >
              <option value="">{t("stock.allWarehouses")}</option>
              {warehouses.map(w => (
                <option key={w.id} value={w.id}>{w.code} - {w.name}</option>
              ))}
            </select>
          </div>
        </div>

        {movements.length === 0 ? (
          <EmptyState title={t("history.title")} description={t("common.empty")} />
        ) : (
          <InventoryDataTable minWidth="950px" ariaLabel={t("history.title")}>
            <thead>
              <tr style={{ backgroundColor: "var(--color-surface-subtle, #f8fafc)", borderBottom: "1px solid var(--color-border-default)" }}>
                <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("history.date")}</th>
                <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("history.operationNumber")}</th>
                <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("history.operationType")}</th>
                <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("history.product")}</th>
                <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("history.warehouseLocation")}</th>
                <th style={{ padding: "0.75rem 1rem", textAlign: "end" }}>{t("history.quantity")}</th>
                <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("history.stockStatus")}</th>
                <th style={{ padding: "0.75rem 1rem", textAlign: "center" }}>{t("history.actions")}</th>
              </tr>
            </thead>
            <tbody>
              {movements.map((m) => (
                <tr key={m.id} style={{ borderBottom: "1px solid var(--color-border-subtle, #e2e8f0)" }}>
                  <td style={{ padding: "0.75rem 1rem" }}>
                    <LocalDateDisplay value={m.operationalDate} />
                  </td>
                  <td style={{ padding: "0.75rem 1rem", fontWeight: 600 }}>
                    <button
                      type="button"
                      onClick={() => setSelectedMovement(m)}
                      style={{
                        background: "none",
                        border: "none",
                        padding: 0,
                        color: "var(--color-brand-primary)",
                        cursor: "pointer",
                        fontWeight: 600,
                        fontSize: "0.875rem",
                        textDecoration: "underline"
                      }}
                    >
                      {m.operationId.slice(0, 18)}...
                    </button>
                  </td>
                  <td style={{ padding: "0.75rem 1rem" }}>
                    {t(`movementTypes.${m.movementType}` as any, { defaultValue: m.movementType })}
                  </td>
                  <td style={{ padding: "0.75rem 1rem" }}>
                    <InventoryProductDisplay productVariantId={m.productVariantId} />
                  </td>
                  <td style={{ padding: "0.75rem 1rem" }}>{warehouseName(m.warehouseId)}</td>
                  <td style={{ padding: "0.75rem 1rem", textAlign: "end", fontWeight: 700, color: m.signedBaseQuantity >= 0 ? "var(--color-status-success)" : "var(--color-status-danger)" }}>
                    <QuantityDisplay value={toQty(m.signedBaseQuantity)} />
                  </td>
                  <td style={{ padding: "0.75rem 1rem" }}>
                    <InventoryStatusBadge status={m.stockStatus} />
                  </td>
                  <td style={{ padding: "0.75rem 1rem", textAlign: "center" }}>
                    <Button variant="secondary" onPress={() => setSelectedMovement(m)}>
                      {t("history.viewDetails")}
                    </Button>
                  </td>
                </tr>
              ))}
            </tbody>
          </InventoryDataTable>
        )}
      </div>

      {/* Operation Detail Modal */}
      {selectedMovement && (
        <OperationDetailModal
          isOpen={Boolean(selectedMovement)}
          onClose={() => setSelectedMovement(null)}
          movement={selectedMovement}
          warehouseName={warehouseName(selectedMovement.warehouseId)}
          onReverseClick={canReverse ? handleReverse : undefined}
        />
      )}
    </div>
  );
}

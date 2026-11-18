import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { InventoryDataTable } from "../../components/inventory-data-table.tsx";
import { InventoryStatusBadge } from "../../components/inventory-status-badge.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { QuantityDisplay } from "../../../../components/fields/quantity-display.tsx";
import { EmptyState } from "../../../../components/primitives/empty-state.tsx";
import { CreateCountModal } from "./create-count-modal.tsx";
import { CountDetailModal } from "./count-detail-modal.tsx";
import { toQty } from "../../utils.ts";
import type { PhysicalCount, Warehouse } from "../../types.ts";

export interface PhysicalCountItem {
  readonly id: string;
  readonly number: string;
  readonly warehouseId: string;
  readonly warehouseName: string;
  readonly mode: string;
  readonly status: string;
  readonly lines: number;
  readonly countedLines: number;
  readonly varianceQuantity: number;
}

export interface CountsTabProps {
  readonly legalEntityId: string;
  readonly warehouses: readonly Warehouse[];
}

export function CountsTab({ legalEntityId, warehouses }: CountsTabProps): React.JSX.Element {
  const { t } = useTranslation("inventory");

  const [isNewCountOpen, setIsNewCountOpen] = useState(false);
  const [selectedCount, setSelectedCount] = useState<PhysicalCountItem | null>(null);

  // Seeded / active counts list
  const [countsList, setCountsList] = useState<readonly PhysicalCountItem[]>([
    {
      id: "cnt-seed-001",
      number: "COUNT-SEED-001",
      warehouseId: warehouses[0]?.id ?? "",
      warehouseName: warehouses[0]?.name ?? "المخزن الرئيسي",
      mode: "Simple",
      status: "Posted",
      lines: 1,
      countedLines: 1,
      varianceQuantity: -1
    }
  ]);

  const handleCountCreated = (newCount: PhysicalCount & { warehouseId: string; mode: string }) => {
    const wh = warehouses.find(w => w.id === newCount.warehouseId);
    setCountsList(prev => [
      {
        id: newCount.id,
        number: newCount.number,
        warehouseId: newCount.warehouseId,
        warehouseName: wh?.name ?? "المخزن الرئيسي",
        mode: newCount.mode,
        status: newCount.status,
        lines: newCount.lines,
        countedLines: newCount.countedLines,
        varianceQuantity: newCount.varianceQuantity
      },
      ...prev
    ]);
  };

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
      {/* Header and Action */}
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
        <div>
          <h3 style={{ margin: "0 0 0.25rem", fontSize: "1.125rem", color: "var(--color-brand-primary)" }}>
            {t("counts.title")}
          </h3>
        </div>
        <Button variant="primary" onPress={() => setIsNewCountOpen(true)}>
          + {t("counts.newCount")}
        </Button>
      </div>

      {/* Counts Table */}
      {countsList.length === 0 ? (
        <EmptyState title={t("counts.title")} description={t("common.empty")} />
      ) : (
        <InventoryDataTable minWidth="850px" ariaLabel={t("counts.title")}>
          <thead>
            <tr style={{ backgroundColor: "var(--color-surface-subtle, #f8fafc)", borderBottom: "1px solid var(--color-border-default)" }}>
              <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("counts.countNumber")}</th>
              <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("counts.warehouse")}</th>
              <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("counts.mode")}</th>
              <th style={{ padding: "0.75rem 1rem", textAlign: "center" }}>{t("counts.status")}</th>
              <th style={{ padding: "0.75rem 1rem", textAlign: "end" }}>{t("counts.totalLines")}</th>
              <th style={{ padding: "0.75rem 1rem", textAlign: "end" }}>{t("counts.countedLines")}</th>
              <th style={{ padding: "0.75rem 1rem", textAlign: "end" }}>{t("counts.varianceQuantity")}</th>
              <th style={{ padding: "0.75rem 1rem", textAlign: "center" }}>{t("stock.actions")}</th>
            </tr>
          </thead>
          <tbody>
            {countsList.map(cnt => (
              <tr key={cnt.id} style={{ borderBottom: "1px solid var(--color-border-subtle, #e2e8f0)" }}>
                <td style={{ padding: "0.75rem 1rem", fontWeight: 700, color: "var(--color-brand-primary)" }}>
                  {cnt.number}
                </td>
                <td style={{ padding: "0.75rem 1rem" }}>{cnt.warehouseName}</td>
                <td style={{ padding: "0.75rem 1rem" }}>
                  {t(`counts.modes.${cnt.mode}` as any, { defaultValue: cnt.mode })}
                </td>
                <td style={{ padding: "0.75rem 1rem", textAlign: "center" }}>
                  <InventoryStatusBadge status={cnt.status} type="count" />
                </td>
                <td style={{ padding: "0.75rem 1rem", textAlign: "end" }}>{cnt.lines}</td>
                <td style={{ padding: "0.75rem 1rem", textAlign: "end" }}>{cnt.countedLines}</td>
                <td style={{ padding: "0.75rem 1rem", textAlign: "end", fontWeight: 700, color: cnt.varianceQuantity < 0 ? "var(--color-status-danger)" : cnt.varianceQuantity > 0 ? "var(--color-status-success)" : "var(--color-content-muted)" }}>
                  <QuantityDisplay value={toQty(cnt.varianceQuantity)} />
                </td>
                <td style={{ padding: "0.75rem 1rem", textAlign: "center" }}>
                  <Button variant="secondary" onPress={() => setSelectedCount(cnt)}>
                    {t("counts.enterCount")}
                  </Button>
                </td>
              </tr>
            ))}
          </tbody>
        </InventoryDataTable>
      )}

      {/* Modals */}
      {isNewCountOpen && (
        <CreateCountModal
          isOpen={isNewCountOpen}
          onClose={() => setIsNewCountOpen(false)}
          legalEntityId={legalEntityId}
          warehouses={warehouses}
          onCountCreated={handleCountCreated}
        />
      )}

      {selectedCount && (
        <CountDetailModal
          isOpen={Boolean(selectedCount)}
          onClose={() => setSelectedCount(null)}
          legalEntityId={legalEntityId}
          countId={selectedCount.id}
          countNumber={selectedCount.number}
          countMode={selectedCount.mode}
          countStatus={selectedCount.status}
          totalLines={selectedCount.lines}
          countedLines={selectedCount.countedLines}
          onCountPosted={() => {
            setCountsList(prev => prev.map(c => c.id === selectedCount.id ? { ...c, status: "Posted" } : c));
          }}
        />
      )}
    </div>
  );
}

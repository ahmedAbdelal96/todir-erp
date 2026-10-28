import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { InventoryDataTable } from "../../components/inventory-data-table.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { QuantityDisplay } from "../../../../components/fields/quantity-display.tsx";
import { EmptyState } from "../../../../components/primitives/empty-state.tsx";
import { InventoryProductDisplay } from "../../components/inventory-product-display.tsx";
import { PostOpeningModal } from "./post-opening-modal.tsx";
import { toQty } from "../../utils.ts";
import type { Warehouse } from "../../types.ts";

export interface OpeningRecordView {
  readonly id: string;
  readonly productVariantId: string;
  readonly warehouseName: string;
  readonly quantity: number;
  readonly legacyRef: string;
}

export interface OpeningTabProps {
  readonly legalEntityId: string;
  readonly warehouses: readonly Warehouse[];
  readonly canReadCost: boolean;
}

export function OpeningTab({ legalEntityId, warehouses, canReadCost }: OpeningTabProps): React.JSX.Element {
  const { t } = useTranslation("inventory");

  const [isPostModalOpen, setIsPostModalOpen] = useState(false);

  // Seeded / recorded opening batches
  const [openingRecords, setOpeningRecords] = useState<readonly OpeningRecordView[]>([
    {
      id: "INV-SEED-OPEN-001",
      productVariantId: "a6315416-cffe-bf52-bcf0-d5e590d3bf94",
      warehouseName: "المخزن الرئيسي (MAIN)",
      quantity: 10,
      legacyRef: "LEGACY-ERP-OPEN-001"
    }
  ]);

  const handleOpeningPosted = (op: { id: string; productVariantId: string; warehouseName: string; quantity: number; legacyRef: string }) => {
    setOpeningRecords(prev => [op, ...prev]);
  };

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
      {/* Notice Banner */}
      <div
        style={{
          padding: "1rem",
          backgroundColor: "#eff6ff",
          border: "1px solid #bfdbfe",
          borderRadius: "var(--radius-md)",
          display: "flex",
          alignItems: "center",
          gap: "0.75rem"
        }}
      >
        <span style={{ fontSize: "0.875rem", color: "#1e3a8a", lineHeight: 1.5 }}>
          {t("opening.notice")}
        </span>
      </div>

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
            {t("opening.title")}
          </h3>
          <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
            {t("opening.subtitle")}
          </span>
        </div>
        <Button variant="primary" onPress={() => setIsPostModalOpen(true)}>
          + {t("opening.postOpening")}
        </Button>
      </div>

      {/* Table */}
      {openingRecords.length === 0 ? (
        <EmptyState title={t("opening.title")} description={t("common.empty")} />
      ) : (
        <InventoryDataTable minWidth="850px" ariaLabel={t("opening.title")}>
          <thead>
            <tr style={{ backgroundColor: "var(--color-surface-subtle, #f8fafc)", borderBottom: "1px solid var(--color-border-default)" }}>
              <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("history.operationNumber")}</th>
              <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("opening.product")}</th>
              <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("opening.warehouse")}</th>
              <th style={{ padding: "0.75rem 1rem", textAlign: "end" }}>{t("opening.quantity")}</th>
              <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("opening.legacyReference")}</th>
            </tr>
          </thead>
          <tbody>
            {openingRecords.map((op) => (
              <tr key={op.id} style={{ borderBottom: "1px solid var(--color-border-subtle, #e2e8f0)" }}>
                <td style={{ padding: "0.75rem 1rem", fontWeight: 700, color: "var(--color-brand-primary)" }}>
                  {op.id}
                </td>
                <td style={{ padding: "0.75rem 1rem" }}>
                  <InventoryProductDisplay productVariantId={op.productVariantId} />
                </td>
                <td style={{ padding: "0.75rem 1rem" }}>{op.warehouseName}</td>
                <td style={{ padding: "0.75rem 1rem", textAlign: "end", fontWeight: 700, color: "var(--color-status-success)" }}>
                  <QuantityDisplay value={toQty(op.quantity)} />
                </td>
                <td style={{ padding: "0.75rem 1rem", color: "var(--color-content-muted)" }}>
                  {op.legacyRef}
                </td>
              </tr>
            ))}
          </tbody>
        </InventoryDataTable>
      )}

      {/* Modal */}
      {isPostModalOpen && (
        <PostOpeningModal
          isOpen={isPostModalOpen}
          onClose={() => setIsPostModalOpen(false)}
          legalEntityId={legalEntityId}
          warehouses={warehouses}
          canReadCost={canReadCost}
          onOpeningPosted={handleOpeningPosted}
        />
      )}
    </div>
  );
}

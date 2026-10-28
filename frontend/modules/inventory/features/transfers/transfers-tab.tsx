import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { InventoryDataTable } from "../../components/inventory-data-table.tsx";
import { InventoryStatusBadge } from "../../components/inventory-status-badge.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { QuantityDisplay } from "../../../../components/fields/quantity-display.tsx";
import { EmptyState } from "../../../../components/primitives/empty-state.tsx";
import { InventoryProductDisplay } from "../../components/inventory-product-display.tsx";
import { TransferModal } from "./transfer-modal.tsx";
import { ReceiveTransferModal } from "./receive-transfer-modal.tsx";
import { useInventoryMutations } from "../../hooks/use-inventory-queries.ts";
import { mapInventoryError } from "../../services/inventory-error-map.ts";
import { toQty } from "../../utils.ts";
import type { Warehouse } from "../../types.ts";

export interface TransferItemView {
  readonly id: string;
  readonly productVariantId: string;
  readonly sourceWarehouseName: string;
  readonly destWarehouseName: string;
  readonly mode: string;
  readonly requestedQuantity: number;
  readonly shippedQuantity: number;
  readonly receivedQuantity: number;
  readonly status: string;
}

export interface TransfersTabProps {
  readonly legalEntityId: string;
  readonly warehouses: readonly Warehouse[];
}

export function TransfersTab({ legalEntityId, warehouses }: TransfersTabProps): React.JSX.Element {
  const { t } = useTranslation("inventory");

  const [isNewTransferOpen, setIsNewTransferOpen] = useState(false);
  const [receivingTransfer, setReceivingTransfer] = useState<{ id: string; remaining: number } | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  // In-transit transfers state or sample tracked transfers
  const [transfersList, setTransfersList] = useState<readonly TransferItemView[]>([
    {
      id: "trf-seed-001",
      productVariantId: "a6315416-cffe-bf52-bcf0-d5e590d3bf94",
      sourceWarehouseName: "المخزن الرئيسي (MAIN)",
      destWarehouseName: "مركز توزيع القاهرة (CAIRO-DC)",
      mode: "InTransit",
      requestedQuantity: 5,
      shippedQuantity: 5,
      receivedQuantity: 2,
      status: "PartiallyReceived"
    }
  ]);

  const { shipTransfer } = useInventoryMutations(legalEntityId);

  const handleShip = async (transferId: string) => {
    setErrorMessage(null);
    try {
      await shipTransfer.mutateAsync(transferId);
      setTransfersList(prev => prev.map(tr => tr.id === transferId ? { ...tr, status: "Shipped", shippedQuantity: tr.requestedQuantity } : tr));
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
            {t("transfers.title")}
          </h3>
          <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
            {t("transfers.modes.InTransit")}
          </span>
        </div>
        <Button variant="primary" onPress={() => setIsNewTransferOpen(true)}>
          + {t("transfers.newTransfer")}
        </Button>
      </div>

      {/* Transfers Data Table */}
      {transfersList.length === 0 ? (
        <EmptyState title={t("transfers.title")} description={t("common.empty")} />
      ) : (
        <InventoryDataTable minWidth="900px" ariaLabel={t("transfers.title")}>
          <thead>
            <tr style={{ backgroundColor: "var(--color-surface-subtle, #f8fafc)", borderBottom: "1px solid var(--color-border-default)" }}>
              <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("transfers.transferId")}</th>
              <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("operations.product")}</th>
              <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("transfers.sourceWarehouse")}</th>
              <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("transfers.destWarehouse")}</th>
              <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("transfers.status")}</th>
              <th style={{ padding: "0.75rem 1rem", textAlign: "end" }}>{t("transfers.sentQuantity")}</th>
              <th style={{ padding: "0.75rem 1rem", textAlign: "end" }}>{t("transfers.receivedQuantity")}</th>
              <th style={{ padding: "0.75rem 1rem", textAlign: "end" }}>{t("transfers.remainingInTransit")}</th>
              <th style={{ padding: "0.75rem 1rem", textAlign: "center" }}>{t("stock.actions")}</th>
            </tr>
          </thead>
          <tbody>
            {transfersList.map((tr) => {
              const remaining = Math.max(0, tr.shippedQuantity - tr.receivedQuantity);
              return (
                <tr key={tr.id} style={{ borderBottom: "1px solid var(--color-border-subtle, #e2e8f0)" }}>
                  <td style={{ padding: "0.75rem 1rem", fontWeight: 700, color: "var(--color-brand-primary)" }}>{tr.id}</td>
                  <td style={{ padding: "0.75rem 1rem" }}>
                    <InventoryProductDisplay productVariantId={tr.productVariantId} />
                  </td>
                  <td style={{ padding: "0.75rem 1rem" }}>{tr.sourceWarehouseName}</td>
                  <td style={{ padding: "0.75rem 1rem" }}>{tr.destWarehouseName}</td>
                  <td style={{ padding: "0.75rem 1rem" }}>
                    <InventoryStatusBadge status={tr.status} type="transfer" />
                  </td>
                  <td style={{ padding: "0.75rem 1rem", textAlign: "end", fontWeight: 600 }}>
                    <QuantityDisplay value={toQty(tr.shippedQuantity)} />
                  </td>
                  <td style={{ padding: "0.75rem 1rem", textAlign: "end", color: "var(--color-status-success)" }}>
                    <QuantityDisplay value={toQty(tr.receivedQuantity)} />
                  </td>
                  <td style={{ padding: "0.75rem 1rem", textAlign: "end", fontWeight: 700, color: remaining > 0 ? "var(--color-status-warning)" : "var(--color-content-muted)" }}>
                    <QuantityDisplay value={toQty(remaining)} />
                  </td>
                  <td style={{ padding: "0.75rem 1rem", textAlign: "center" }}>
                    <div style={{ display: "flex", gap: "0.375rem", justifyContent: "center" }}>
                      {tr.status === "Draft" && (
                        <Button variant="primary" onPress={() => handleShip(tr.id)}>
                          {t("transfers.shipAction")}
                        </Button>
                      )}
                      {(tr.status === "Shipped" || tr.status === "PartiallyReceived") && remaining > 0 && (
                        <Button
                          variant="secondary"
                          onPress={() => setReceivingTransfer({ id: tr.id, remaining })}
                        >
                          {t("transfers.receiveAction")}
                        </Button>
                      )}
                    </div>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </InventoryDataTable>
      )}

      {/* Modals */}
      {isNewTransferOpen && (
        <TransferModal
          isOpen={isNewTransferOpen}
          onClose={() => setIsNewTransferOpen(false)}
          legalEntityId={legalEntityId}
          warehouses={warehouses}
        />
      )}

      {receivingTransfer && (
        <ReceiveTransferModal
          isOpen={Boolean(receivingTransfer)}
          onClose={() => setReceivingTransfer(null)}
          legalEntityId={legalEntityId}
          transferId={receivingTransfer.id}
          remainingQuantity={receivingTransfer.remaining}
        />
      )}
    </div>
  );
}

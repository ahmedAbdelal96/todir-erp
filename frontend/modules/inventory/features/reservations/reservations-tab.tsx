import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { InventoryDataTable } from "../../components/inventory-data-table.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { QuantityDisplay } from "../../../../components/fields/quantity-display.tsx";
import { EmptyState } from "../../../../components/primitives/empty-state.tsx";
import { ReserveModal } from "./reserve-modal.tsx";
import { useInventoryMutations } from "../../hooks/use-inventory-queries.ts";
import { mapInventoryError } from "../../services/inventory-error-map.ts";
import { toQty } from "../../utils.ts";
import type { Warehouse } from "../../types.ts";

export interface ReservationItem {
  readonly id: string;
  readonly productVariantId: string;
  readonly warehouseName: string;
  readonly requested: number;
  readonly reserved: number;
  readonly source: string;
  readonly status: string;
  readonly expiresAt?: string;
}

export interface ReservationsTabProps {
  readonly legalEntityId: string;
  readonly warehouses: readonly Warehouse[];
}

export function ReservationsTab({ legalEntityId, warehouses }: ReservationsTabProps): React.JSX.Element {
  const { t } = useTranslation("inventory");

  const [isReserveModalOpen, setIsReserveModalOpen] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  // Seeded / active reservation list
  const [reservations, setReservations] = useState<readonly ReservationItem[]>([
    {
      id: "res-seed-001",
      productVariantId: "a6315416-cffe-bf52-bcf0-d5e590d3bf94",
      warehouseName: "المخزن الرئيسي (MAIN)",
      requested: 2,
      reserved: 2,
      source: "SO-2026-SEED-01",
      status: "Active"
    }
  ]);

  const { releaseReservation, cancelReservation } = useInventoryMutations(legalEntityId);

  const handleRelease = async (reservationId: string) => {
    setErrorMessage(null);
    try {
      await releaseReservation.mutateAsync(reservationId);
      setReservations(prev => prev.map(r => r.id === reservationId ? { ...r, status: "Released" } : r));
    } catch (err) {
      const mapped = mapInventoryError(err);
      setErrorMessage(t(mapped.messageKey as any, { defaultValue: mapped.fallbackAr }));
    }
  };

  const handleCancel = async (reservationId: string) => {
    setErrorMessage(null);
    try {
      await cancelReservation.mutateAsync(reservationId);
      setReservations(prev => prev.map(r => r.id === reservationId ? { ...r, status: "Cancelled" } : r));
    } catch (err) {
      const mapped = mapInventoryError(err);
      setErrorMessage(t(mapped.messageKey as any, { defaultValue: mapped.fallbackAr }));
    }
  };

  const handleCreated = (item: { id: string; requested: number; reserved: number }) => {
    setReservations(prev => [
      {
        id: item.id,
        productVariantId: "a6315416-cffe-bf52-bcf0-d5e590d3bf94",
        warehouseName: warehouses[0]?.name ?? "المخزن الرئيسي",
        requested: item.requested,
        reserved: item.reserved,
        source: "Manual Reservation",
        status: "Active"
      },
      ...prev
    ]);
  };

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
      {errorMessage && (
        <div role="alert" style={{ padding: "0.75rem", backgroundColor: "#fee2e2", border: "1px solid #ef4444", borderRadius: "var(--radius-sm)", color: "#b91c1c", fontSize: "0.875rem" }}>
          {errorMessage}
        </div>
      )}

      {/* Domain Rule Explanation Banner */}
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
        <span style={{ fontSize: "1.25rem" }}>{"ℹ️"}</span>
        <span style={{ fontSize: "0.875rem", color: "#1e3a8a", lineHeight: 1.5 }}>
          {t("reservations.ruleNotice")}
        </span>
      </div>

      {/* Header & New Reservation */}
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
            {t("reservations.title")}
          </h3>
        </div>
        <Button variant="primary" onPress={() => setIsReserveModalOpen(true)}>
          + {t("reservations.newReservation")}
        </Button>
      </div>

      {/* Table */}
      {reservations.length === 0 ? (
        <EmptyState title={t("reservations.title")} description={t("common.empty")} />
      ) : (
        <InventoryDataTable minWidth="850px" ariaLabel={t("reservations.title")}>
          <thead>
            <tr style={{ backgroundColor: "var(--color-surface-subtle, #f8fafc)", borderBottom: "1px solid var(--color-border-default)" }}>
              <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("reservations.source")}</th>
              <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("reservations.product")}</th>
              <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("reservations.warehouse")}</th>
              <th style={{ padding: "0.75rem 1rem", textAlign: "end" }}>{t("reservations.requested")}</th>
              <th style={{ padding: "0.75rem 1rem", textAlign: "end" }}>{t("reservations.reserved")}</th>
              <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("reservations.status")}</th>
              <th style={{ padding: "0.75rem 1rem", textAlign: "center" }}>{t("reservations.actions")}</th>
            </tr>
          </thead>
          <tbody>
            {reservations.map(res => (
              <tr key={res.id} style={{ borderBottom: "1px solid var(--color-border-subtle, #e2e8f0)" }}>
                <td style={{ padding: "0.75rem 1rem", fontWeight: 600 }}>{res.source}</td>
                <td style={{ padding: "0.75rem 1rem" }}>{res.productVariantId}</td>
                <td style={{ padding: "0.75rem 1rem" }}>{res.warehouseName}</td>
                <td style={{ padding: "0.75rem 1rem", textAlign: "end" }}>
                  <QuantityDisplay value={toQty(res.requested)} />
                </td>
                <td style={{ padding: "0.75rem 1rem", textAlign: "end", fontWeight: 700, color: "var(--color-status-warning)" }}>
                  <QuantityDisplay value={toQty(res.reserved)} />
                </td>
                <td style={{ padding: "0.75rem 1rem" }}>
                  <span style={{
                    padding: "0.25rem 0.5rem",
                    borderRadius: "var(--radius-sm)",
                    fontSize: "0.75rem",
                    fontWeight: 600,
                    backgroundColor: res.status === "Active" ? "#fef3c7" : "#f1f5f9",
                    color: res.status === "Active" ? "#92400e" : "#475569"
                  }}>
                    {res.status}
                  </span>
                </td>
                <td style={{ padding: "0.75rem 1rem", textAlign: "center" }}>
                  {res.status === "Active" && (
                    <div style={{ display: "flex", gap: "0.375rem", justifyContent: "center" }}>
                      <Button variant="secondary" onPress={() => handleRelease(res.id)}>
                        {t("reservations.release")}
                      </Button>
                      <Button variant="secondary" onPress={() => handleCancel(res.id)}>
                        {t("reservations.cancel")}
                      </Button>
                    </div>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </InventoryDataTable>
      )}

      {/* Modal */}
      {isReserveModalOpen && (
        <ReserveModal
          isOpen={isReserveModalOpen}
          onClose={() => setIsReserveModalOpen(false)}
          legalEntityId={legalEntityId}
          warehouses={warehouses}
          onReservationCreated={handleCreated}
        />
      )}
    </div>
  );
}

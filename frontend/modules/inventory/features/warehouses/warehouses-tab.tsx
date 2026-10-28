import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { InventoryDataTable } from "../../components/inventory-data-table.tsx";
import { InventoryStatusBadge } from "../../components/inventory-status-badge.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { EmptyState } from "../../../../components/primitives/empty-state.tsx";
import { CreateWarehouseModal } from "./create-warehouse-modal.tsx";
import { CreateLocationModal } from "./create-location-modal.tsx";
import { useLocations, useInventoryMutations } from "../../hooks/use-inventory-queries.ts";
import { mapInventoryError } from "../../services/inventory-error-map.ts";
import type { Warehouse, WarehouseStatus, InventoryLocation } from "../../types.ts";

export interface WarehousesTabProps {
  readonly legalEntityId: string;
  readonly warehouses: readonly Warehouse[];
}

export function WarehousesTab({ legalEntityId, warehouses }: WarehousesTabProps): React.JSX.Element {
  const { t } = useTranslation("inventory");

  const [isAddWarehouseOpen, setIsAddWarehouseOpen] = useState(false);
  const [selectedWarehouseForLocations, setSelectedWarehouseForLocations] = useState<string | null>(
    warehouses.find(w => w.locationMode !== "WarehouseOnly")?.id ?? (warehouses[0]?.id ?? null)
  );
  const [isAddLocationOpen, setIsAddLocationOpen] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const selectedWarehouse = warehouses.find(w => w.id === selectedWarehouseForLocations);
  const isWarehouseOnly = selectedWarehouse?.locationMode === "WarehouseOnly";

  const { data: locations = [] } = useLocations(
    legalEntityId,
    !isWarehouseOnly && selectedWarehouseForLocations ? selectedWarehouseForLocations : undefined
  );

  const { changeWarehouseStatus, archiveLocation } = useInventoryMutations(legalEntityId);

  const handleStatusChange = async (warehouseId: string, status: WarehouseStatus) => {
    setErrorMessage(null);
    try {
      await changeWarehouseStatus.mutateAsync({ warehouseId, request: { status } });
    } catch (err) {
      const mapped = mapInventoryError(err);
      setErrorMessage(t(mapped.messageKey as any, { defaultValue: mapped.fallbackAr }));
    }
  };

  const handleArchiveLocation = async (locationId: string) => {
    setErrorMessage(null);
    try {
      await archiveLocation.mutateAsync(locationId);
    } catch (err) {
      const mapped = mapInventoryError(err);
      setErrorMessage(t(mapped.messageKey as any, { defaultValue: mapped.fallbackAr }));
    }
  };

  // Visible locations: filter out hidden default logical location
  const visibleLocations = locations.filter(l => !l.hidden);

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
      {errorMessage && (
        <div role="alert" style={{ padding: "0.75rem", backgroundColor: "#fee2e2", border: "1px solid #ef4444", borderRadius: "var(--radius-sm)", color: "#b91c1c", fontSize: "0.875rem" }}>
          {errorMessage}
        </div>
      )}

      {/* Warehouses Card */}
      <div
        style={{
          padding: "1.25rem",
          backgroundColor: "var(--color-surface-card)",
          border: "1px solid var(--color-border-default)",
          borderRadius: "var(--radius-md)",
          boxShadow: "0 1px 3px rgba(0, 0, 0, 0.05)"
        }}
      >
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "1rem" }}>
          <div>
            <h3 style={{ margin: "0 0 0.25rem", fontSize: "1.125rem", color: "var(--color-brand-primary)" }}>
              {t("warehouses.title")}
            </h3>
          </div>
          <Button variant="primary" onPress={() => setIsAddWarehouseOpen(true)}>
            + {t("warehouses.addWarehouse")}
          </Button>
        </div>

        {warehouses.length === 0 ? (
          <EmptyState title={t("warehouses.title")} description={t("common.empty")} />
        ) : (
          <InventoryDataTable minWidth="850px" ariaLabel={t("warehouses.title")}>
            <thead>
              <tr style={{ backgroundColor: "var(--color-surface-subtle, #f8fafc)", borderBottom: "1px solid var(--color-border-default)" }}>
                <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("warehouses.code")}</th>
                <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("warehouses.name")}</th>
                <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("warehouses.mode")}</th>
                <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("warehouses.status")}</th>
                <th style={{ padding: "0.75rem 1rem", textAlign: "center" }}>{t("warehouses.actions")}</th>
              </tr>
            </thead>
            <tbody>
              {warehouses.map(w => {
                const isSelected = w.id === selectedWarehouseForLocations;
                return (
                  <tr
                    key={w.id}
                    style={{
                      borderBottom: "1px solid var(--color-border-subtle, #e2e8f0)",
                      backgroundColor: isSelected ? "var(--color-brand-subtle, rgba(16, 42, 67, 0.04))" : "transparent"
                    }}
                  >
                    <td style={{ padding: "0.75rem 1rem", fontWeight: 600 }}>{w.code}</td>
                    <td style={{ padding: "0.75rem 1rem" }}>
                      <button
                        type="button"
                        onClick={() => setSelectedWarehouseForLocations(w.id)}
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
                        {w.name}
                      </button>
                    </td>
                    <td style={{ padding: "0.75rem 1rem" }}>
                      {t(`warehouses.modes.${w.locationMode}` as any, { defaultValue: w.locationMode })}
                    </td>
                    <td style={{ padding: "0.75rem 1rem" }}>
                      <InventoryStatusBadge status={w.status} type="warehouse" />
                    </td>
                    <td style={{ padding: "0.75rem 1rem", textAlign: "center" }}>
                      <div style={{ display: "flex", gap: "0.375rem", justifyContent: "center" }}>
                        {w.status !== "Active" && (
                          <Button variant="secondary" onPress={() => handleStatusChange(w.id, "Active")}>
                            {t("warehouses.activate")}
                          </Button>
                        )}
                        {w.status === "Active" && (
                          <Button variant="secondary" onPress={() => handleStatusChange(w.id, "Suspended")}>
                            {t("warehouses.suspend")}
                          </Button>
                        )}
                        {w.status !== "Archived" && (
                          <Button variant="secondary" onPress={() => handleStatusChange(w.id, "Archived")}>
                            {t("warehouses.archive")}
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
      </div>

      {/* Locations Section for Selected Warehouse */}
      {selectedWarehouse && (
        <div
          style={{
            padding: "1.25rem",
            backgroundColor: "var(--color-surface-card)",
            border: "1px solid var(--color-border-default)",
            borderRadius: "var(--radius-md)",
            boxShadow: "0 1px 3px rgba(0, 0, 0, 0.05)"
          }}
        >
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "1rem" }}>
            <div>
              <h4 style={{ margin: "0 0 0.25rem", fontSize: "1rem", color: "var(--color-brand-primary)" }}>
                {t("warehouses.locationsTitle")} ({selectedWarehouse.name})
              </h4>
              <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
                {t(`warehouses.modes.${selectedWarehouse.locationMode}` as any)}
              </span>
            </div>
            {!isWarehouseOnly && (
              <Button variant="primary" onPress={() => setIsAddLocationOpen(true)}>
                + {t("warehouses.addLocation")}
              </Button>
            )}
          </div>

          {isWarehouseOnly ? (
            <div
              style={{
                padding: "1.5rem",
                textAlign: "center",
                backgroundColor: "var(--color-surface-subtle, #f8fafc)",
                borderRadius: "var(--radius-md)",
                border: "1px dashed var(--color-border-default)",
                color: "var(--color-content-muted)"
              }}
            >
              <p style={{ margin: 0, fontWeight: 500 }}>
                {t("warehouses.defaultLocationNote")}
              </p>
            </div>
          ) : visibleLocations.length === 0 ? (
            <EmptyState
              title={t("warehouses.locationsTitle")}
              description={t("common.empty")}
            />
          ) : (
            <InventoryDataTable minWidth="750px" ariaLabel={t("warehouses.locationsTitle")}>
              <thead>
                <tr style={{ backgroundColor: "var(--color-surface-subtle, #f8fafc)", borderBottom: "1px solid var(--color-border-default)" }}>
                  <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("warehouses.locationCode")}</th>
                  <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("warehouses.locationName")}</th>
                  <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("warehouses.locationType")}</th>
                  <th style={{ padding: "0.75rem 1rem", textAlign: "center" }}>{t("warehouses.canHoldStock")}</th>
                  <th style={{ padding: "0.75rem 1rem", textAlign: "center" }}>{t("warehouses.canReceive")}</th>
                  <th style={{ padding: "0.75rem 1rem", textAlign: "center" }}>{t("warehouses.canPick")}</th>
                  <th style={{ padding: "0.75rem 1rem", textAlign: "center" }}>{t("warehouses.actions")}</th>
                </tr>
              </thead>
              <tbody>
                {visibleLocations.map(loc => (
                  <tr key={loc.id} style={{ borderBottom: "1px solid var(--color-border-subtle, #e2e8f0)" }}>
                    <td style={{ padding: "0.75rem 1rem", fontWeight: 600 }}>{loc.code}</td>
                    <td style={{ padding: "0.75rem 1rem" }}>{loc.name}</td>
                    <td style={{ padding: "0.75rem 1rem" }}>{loc.locationType ? t(`warehouses.locationTypes.${loc.locationType}` as any) : "—"}</td>
                    <td style={{ padding: "0.75rem 1rem", textAlign: "center" }}>
                      {loc.canHoldStock ? "✓" : "✕"}
                    </td>
                    <td style={{ padding: "0.75rem 1rem", textAlign: "center" }}>
                      {loc.canReceiveInto ? "✓" : "✕"}
                    </td>
                    <td style={{ padding: "0.75rem 1rem", textAlign: "center" }}>
                      {loc.canPickFrom ? "✓" : "✕"}
                    </td>
                    <td style={{ padding: "0.75rem 1rem", textAlign: "center" }}>
                      <Button variant="secondary" onPress={() => handleArchiveLocation(loc.id)}>
                        {t("warehouses.archiveLocation")}
                      </Button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </InventoryDataTable>
          )}
        </div>
      )}

      {/* Modals */}
      {isAddWarehouseOpen && (
        <CreateWarehouseModal
          isOpen={isAddWarehouseOpen}
          onClose={() => setIsAddWarehouseOpen(false)}
          legalEntityId={legalEntityId}
        />
      )}

      {isAddLocationOpen && selectedWarehouseForLocations && (
        <CreateLocationModal
          isOpen={isAddLocationOpen}
          onClose={() => setIsAddLocationOpen(false)}
          legalEntityId={legalEntityId}
          warehouseId={selectedWarehouseForLocations}
          existingLocations={visibleLocations}
        />
      )}
    </div>
  );
}

import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { InventoryDataTable } from "../../components/inventory-data-table.tsx";
import { InventoryStatusBadge } from "../../components/inventory-status-badge.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { QuantityDisplay } from "../../../../components/fields/quantity-display.tsx";
import { EmptyState } from "../../../../components/primitives/empty-state.tsx";
import { InventoryProductDisplay } from "../../components/inventory-product-display.tsx";
import { InventoryProductPicker } from "../../components/inventory-product-picker.tsx";
import {
  useProductLocator,
  usePutawaySuggestion,
  usePickingSuggestions
} from "../../hooks/use-inventory-queries.ts";
import { toQty } from "../../utils.ts";

export interface LocatorTabProps {
  readonly legalEntityId: string;
  readonly canReadCost: boolean;
}

export function LocatorTab({ legalEntityId }: LocatorTabProps): React.JSX.Element {
  const { t } = useTranslation("inventory");

  const [activeProductId, setActiveProductId] = useState("");

  const { data: locations = [], isLoading } = useProductLocator(legalEntityId, activeProductId);
  const { data: putaway } = usePutawaySuggestion(legalEntityId, activeProductId);
  const { data: pickings = [] } = usePickingSuggestions(legalEntityId, activeProductId);

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
      {/* Header and Search Card */}
      <div
        style={{
          padding: "1.25rem",
          backgroundColor: "var(--color-surface-card)",
          border: "1px solid var(--color-border-default)",
          borderRadius: "var(--radius-md)",
          boxShadow: "0 1px 3px rgba(0, 0, 0, 0.05)"
        }}
      >
        <h3 style={{ margin: "0 0 0.25rem", fontSize: "1.125rem", color: "var(--color-brand-primary)" }}>
          {t("locator.title")}
        </h3>
        <p style={{ margin: "0 0 1rem", fontSize: "0.875rem", color: "var(--color-content-muted)" }}>
          {t("locator.subtitle")}
        </p>

        <div style={{ maxWidth: "550px" }}>
          <InventoryProductPicker
            label={t("locator.searchLabel")}
            value={activeProductId}
            onChange={setActiveProductId}
          />
        </div>
      </div>

      {/* Putaway & Picking suggestions row if available */}
      {(putaway || pickings.length > 0) && (
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(280px, 1fr))", gap: "1rem" }}>
          {putaway && (
            <div
              style={{
                padding: "1rem",
                backgroundColor: "#f0fdf4",
                border: "1px solid #bbf7d0",
                borderRadius: "var(--radius-md)"
              }}
            >
              <div style={{ fontWeight: 600, fontSize: "0.875rem", color: "#166534", marginBottom: "0.25rem" }}>
                {t("locator.suggestedPutaway")}
              </div>
              <div style={{ fontSize: "1.125rem", fontWeight: 700, color: "#14532d" }}>
                {putaway.locationCode}
              </div>
              <div style={{ fontSize: "0.8125rem", color: "#166534", marginTop: "0.25rem" }}>
                {putaway.reason}
              </div>
            </div>
          )}

          {pickings.length > 0 && (
            <div
              style={{
                padding: "1rem",
                backgroundColor: "#eff6ff",
                border: "1px solid #bfdbfe",
                borderRadius: "var(--radius-md)"
              }}
            >
              <div style={{ fontWeight: 600, fontSize: "0.875rem", color: "#1e40af", marginBottom: "0.25rem" }}>
                {t("locator.suggestedPicking")}
              </div>
              <div style={{ display: "flex", flexDirection: "column", gap: "0.25rem", marginTop: "0.25rem" }}>
                {pickings.map((p, idx) => (
                  <div key={idx} style={{ display: "flex", justifyContent: "space-between", fontSize: "0.875rem" }}>
                    <span style={{ fontWeight: 600, color: "#1e3a8a" }}>{p.locationCode}</span>
                    <span style={{ color: "#1d4ed8" }}>
                      {t("locator.takeQuantity")}: <strong>{p.availableQuantity ?? 0}</strong>
                    </span>
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>
      )}

      {/* Locator Results Table */}
      {locations.length === 0 ? (
        <EmptyState
          title={t("locator.title")}
          description={t("locator.noLocationsFound")}
        />
      ) : (
        <InventoryDataTable minWidth="800px" ariaLabel={t("locator.title")}>
          <thead>
            <tr style={{ backgroundColor: "var(--color-surface-subtle, #f8fafc)", borderBottom: "1px solid var(--color-border-default)" }}>
              <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("locator.warehouse")}</th>
              <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("locator.locationPath")}</th>
              <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("locator.status")}</th>
              <th style={{ padding: "0.75rem 1rem", textAlign: "end" }}>{t("locator.onHandQty")}</th>
              <th style={{ padding: "0.75rem 1rem", textAlign: "end" }}>{t("locator.availableQty")}</th>
              <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("locator.lotSerial")}</th>
            </tr>
          </thead>
          <tbody>
            {locations.map((loc, idx) => {
              const isInternalDefault =
                !loc.locationPath ||
                loc.locationPath.trim().toUpperCase() === "DEFAULT" ||
                loc.locationPath.trim().toUpperCase() === "DEFAULTLOCATION" ||
                loc.locationId === "loc-main" ||
                loc.locationId?.toLowerCase().includes("default");

              return (
                <tr
                  key={`${loc.warehouseId}-${loc.locationId}-${idx}`}
                  style={{ borderBottom: "1px solid var(--color-border-subtle, #e2e8f0)" }}
                >
                  <td style={{ padding: "0.75rem 1rem", fontWeight: 600 }}>{loc.warehouseCode}</td>
                  <td style={{ padding: "0.75rem 1rem" }}>
                    {isInternalDefault ? (
                      <span style={{ color: "var(--color-content-muted)", fontStyle: "italic", fontSize: "0.8125rem" }}>
                        {t("locator.noInternalLocation")}
                      </span>
                    ) : (
                      <span style={{ padding: "0.25rem 0.5rem", backgroundColor: "var(--color-surface-subtle)", borderRadius: "var(--radius-sm)", border: "1px solid var(--color-border-subtle)", fontFamily: "monospace", fontSize: "0.8125rem" }}>
                        {loc.locationPath || loc.locationId}
                      </span>
                    )}
                  </td>
                  <td style={{ padding: "0.75rem 1rem" }}>
                    <InventoryStatusBadge status={loc.stockStatus} />
                  </td>
                  <td style={{ padding: "0.75rem 1rem", textAlign: "end", fontWeight: 700 }}>
                    <QuantityDisplay value={toQty(loc.onHand)} />
                  </td>
                  <td style={{ padding: "0.75rem 1rem", textAlign: "end", fontWeight: 700, color: "var(--color-status-success)" }}>
                    <QuantityDisplay value={toQty(loc.available)} />
                  </td>
                  <td style={{ padding: "0.75rem 1rem", color: "var(--color-content-muted)" }}>
                    {loc.lotId || loc.serialId ? `${loc.lotId ?? ""}${loc.serialId ?? ""}` : "—"}
                  </td>
                </tr>
              );
            })}
          </tbody>
        </InventoryDataTable>
      )}
    </div>
  );
}

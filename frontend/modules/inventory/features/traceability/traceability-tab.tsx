import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { InventoryDataTable } from "../../components/inventory-data-table.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { LocalDateDisplay } from "../../../../components/fields/local-date-display.tsx";
import { EmptyState } from "../../../../components/primitives/empty-state.tsx";
import { InventoryProductDisplay } from "../../components/inventory-product-display.tsx";
import { CreateLotModal } from "./create-lot-modal.tsx";
import { CreateSerialModal } from "./create-serial-modal.tsx";
import { LotDetailModal } from "./lot-detail-modal.tsx";
import { SerialDetailModal } from "./serial-detail-modal.tsx";

export interface LotView {
  readonly id: string;
  readonly lotNumber: string;
  readonly productVariantId: string;
  readonly manufacturedOn?: string | null;
  readonly expiresOn?: string | null;
  readonly supplierReference?: string | null;
  readonly status: "Active" | "ExpiringSoon" | "Expired";
  readonly quantity: number;
}

export interface SerialView {
  readonly id: string;
  readonly serialNumber: string;
  readonly productVariantId: string;
  readonly status: string;
  readonly warehouseName: string;
  readonly locationName: string;
  readonly lifecycleStage: string;
}

export interface TraceabilityTabProps {
  readonly legalEntityId: string;
}

export function TraceabilityTab({ legalEntityId }: TraceabilityTabProps): React.JSX.Element {
  const { t } = useTranslation("inventory");

  const [activeSubTab, setActiveSubTab] = useState<"lots" | "serials">("lots");
  const [isAddLotOpen, setIsAddLotOpen] = useState(false);
  const [isAddSerialOpen, setIsAddSerialOpen] = useState(false);
  const [selectedLot, setSelectedLot] = useState<LotView | null>(null);
  const [selectedSerial, setSelectedSerial] = useState<SerialView | null>(null);

  // Seeded / active lots
  const [lots, setLots] = useState<readonly LotView[]>([
    {
      id: "lot-01",
      lotNumber: "LOT-2026-MED-01",
      productVariantId: "a6315416-cffe-bf52-bcf0-d5e590d3bf94",
      manufacturedOn: "2026-01-15",
      expiresOn: "2027-01-15",
      supplierReference: "PHARMA-SUP-88",
      status: "Active",
      quantity: 50
    },
    {
      id: "lot-02",
      lotNumber: "LOT-2026-MED-02",
      productVariantId: "a6315416-cffe-bf52-bcf0-d5e590d3bf94",
      manufacturedOn: "2025-10-01",
      expiresOn: "2026-11-10",
      supplierReference: "PHARMA-SUP-88",
      status: "ExpiringSoon",
      quantity: 20
    }
  ]);

  // Seeded / active serials
  const [serials, setSerials] = useState<readonly SerialView[]>([
    {
      id: "sn-01",
      serialNumber: "SN-90081-A",
      productVariantId: "a6315416-cffe-bf52-bcf0-d5e590d3bf94",
      status: "Available",
      warehouseName: "مركز توزيع القاهرة (CAIRO-DC)",
      locationName: "رف أ-01",
      lifecycleStage: "Received"
    },
    {
      id: "sn-02",
      serialNumber: "SN-90082-A",
      productVariantId: "a6315416-cffe-bf52-bcf0-d5e590d3bf94",
      status: "Available",
      warehouseName: "مركز توزيع القاهرة (CAIRO-DC)",
      locationName: "رف أ-01",
      lifecycleStage: "Received"
    }
  ]);

  const handleLotCreated = (newLot: { id: string; number: string; expiry?: string }) => {
    setLots(prev => [
      {
        id: newLot.id,
        lotNumber: newLot.number,
        productVariantId: "a6315416-cffe-bf52-bcf0-d5e590d3bf94",
        manufacturedOn: "2026-10-28",
        expiresOn: newLot.expiry ?? "2027-10-28",
        supplierReference: "Direct Register",
        status: "Active",
        quantity: 10
      },
      ...prev
    ]);
  };

  const handleSerialCreated = (newSerial: { id: string; reference: string }) => {
    setSerials(prev => [
      {
        id: newSerial.id,
        serialNumber: newSerial.reference,
        productVariantId: "a6315416-cffe-bf52-bcf0-d5e590d3bf94",
        status: "Available",
        warehouseName: "المخزن الرئيسي (MAIN)",
        locationName: "DEFAULT",
        lifecycleStage: "Received"
      },
      ...prev
    ]);
  };

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
      {/* Subtab Toggle Bar */}
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
        <div style={{ display: "flex", gap: "0.5rem" }}>
          <Button
            variant={activeSubTab === "lots" ? "primary" : "secondary"}
            onPress={() => setActiveSubTab("lots")}
          >
            {t("traceability.lotsTab")}
          </Button>
          <Button
            variant={activeSubTab === "serials" ? "primary" : "secondary"}
            onPress={() => setActiveSubTab("serials")}
          >
            {t("traceability.serialsTab")}
          </Button>
        </div>

        <div>
          {activeSubTab === "lots" ? (
            <Button variant="primary" onPress={() => setIsAddLotOpen(true)}>
              + {t("traceability.addLot")}
            </Button>
          ) : (
            <Button variant="primary" onPress={() => setIsAddSerialOpen(true)}>
              + {t("traceability.addSerial")}
            </Button>
          )}
        </div>
      </div>

      {/* Lots Table */}
      {activeSubTab === "lots" && (
        <div>
          {lots.length === 0 ? (
            <EmptyState title={t("traceability.lotsTab")} description={t("common.empty")} />
          ) : (
            <InventoryDataTable minWidth="850px" ariaLabel={t("traceability.lotsTab")}>
              <thead>
                <tr style={{ backgroundColor: "var(--color-surface-subtle, #f8fafc)", borderBottom: "1px solid var(--color-border-default)" }}>
                  <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("traceability.lotNumber")}</th>
                  <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("traceability.product")}</th>
                  <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("traceability.manufacturedDate")}</th>
                  <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("traceability.expiryDate")}</th>
                  <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("traceability.supplierRef")}</th>
                  <th style={{ padding: "0.75rem 1rem", textAlign: "center" }}>{t("traceability.currentStatus")}</th>
                  <th style={{ padding: "0.75rem 1rem", textAlign: "center" }}>{t("stock.actions")}</th>
                </tr>
              </thead>
              <tbody>
                {lots.map(lot => {
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
                    <tr key={lot.id} style={{ borderBottom: "1px solid var(--color-border-subtle, #e2e8f0)" }}>
                      <td style={{ padding: "0.75rem 1rem", fontWeight: 700, color: "var(--color-brand-primary)" }}>
                        <button
                          type="button"
                          onClick={() => setSelectedLot(lot)}
                          style={{
                            background: "none",
                            border: "none",
                            padding: 0,
                            color: "var(--color-brand-primary)",
                            cursor: "pointer",
                            fontWeight: 700,
                            fontSize: "0.875rem",
                            textDecoration: "underline"
                          }}
                        >
                          {lot.lotNumber}
                        </button>
                      </td>
                      <td style={{ padding: "0.75rem 1rem" }}>
                        <InventoryProductDisplay productVariantId={lot.productVariantId} />
                      </td>
                      <td style={{ padding: "0.75rem 1rem" }}>
                        {lot.manufacturedOn ? <LocalDateDisplay value={lot.manufacturedOn} /> : "—"}
                      </td>
                      <td style={{ padding: "0.75rem 1rem" }}>
                        {lot.expiresOn ? <LocalDateDisplay value={lot.expiresOn} /> : "—"}
                      </td>
                      <td style={{ padding: "0.75rem 1rem", color: "var(--color-content-muted)" }}>
                        {lot.supplierReference ?? "—"}
                      </td>
                      <td style={{ padding: "0.75rem 1rem", textAlign: "center" }}>
                        <span style={{
                          padding: "0.25rem 0.625rem",
                          borderRadius: "var(--radius-sm)",
                          fontSize: "0.75rem",
                          fontWeight: 600,
                          backgroundColor: badgeBg,
                          color: badgeColor
                        }}>
                          {t(labelKey as any)}
                        </span>
                      </td>
                      <td style={{ padding: "0.75rem 1rem", textAlign: "center" }}>
                        <Button variant="secondary" onPress={() => setSelectedLot(lot)}>
                          {t("traceability.viewDetails")}
                        </Button>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </InventoryDataTable>
          )}
        </div>
      )}

      {/* Serials Table */}
      {activeSubTab === "serials" && (
        <div>
          {serials.length === 0 ? (
            <EmptyState title={t("traceability.serialsTab")} description={t("common.empty")} />
          ) : (
            <InventoryDataTable minWidth="850px" ariaLabel={t("traceability.serialsTab")}>
              <thead>
                <tr style={{ backgroundColor: "var(--color-surface-subtle, #f8fafc)", borderBottom: "1px solid var(--color-border-default)" }}>
                  <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("traceability.serialNumber")}</th>
                  <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("traceability.product")}</th>
                  <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("warehouses.name")}</th>
                  <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("warehouses.locationName")}</th>
                  <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("traceability.currentStatus")}</th>
                  <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("traceability.lifecycleHistory")}</th>
                  <th style={{ padding: "0.75rem 1rem", textAlign: "center" }}>{t("stock.actions")}</th>
                </tr>
              </thead>
              <tbody>
                {serials.map(sn => (
                  <tr key={sn.id} style={{ borderBottom: "1px solid var(--color-border-subtle, #e2e8f0)" }}>
                    <td style={{ padding: "0.75rem 1rem", fontWeight: 700, color: "var(--color-brand-primary)" }}>
                      <button
                        type="button"
                        onClick={() => setSelectedSerial(sn)}
                        style={{
                          background: "none",
                          border: "none",
                          padding: 0,
                          color: "var(--color-brand-primary)",
                          cursor: "pointer",
                          fontWeight: 700,
                          fontSize: "0.875rem",
                          textDecoration: "underline"
                        }}
                      >
                        {sn.serialNumber}
                      </button>
                    </td>
                    <td style={{ padding: "0.75rem 1rem" }}>
                      <InventoryProductDisplay productVariantId={sn.productVariantId} />
                    </td>
                    <td style={{ padding: "0.75rem 1rem" }}>{sn.warehouseName}</td>
                    <td style={{ padding: "0.75rem 1rem" }}>{sn.locationName}</td>
                    <td style={{ padding: "0.75rem 1rem" }}>
                      <span style={{
                        padding: "0.25rem 0.5rem",
                        borderRadius: "var(--radius-sm)",
                        fontSize: "0.75rem",
                        fontWeight: 600,
                        backgroundColor: "#dcfce7",
                        color: "#15803d"
                      }}>
                        {sn.status}
                      </span>
                    </td>
                    <td style={{ padding: "0.75rem 1rem" }}>
                      <span style={{
                        padding: "0.25rem 0.5rem",
                        borderRadius: "var(--radius-sm)",
                        fontSize: "0.75rem",
                        backgroundColor: "var(--color-surface-subtle)",
                        border: "1px solid var(--color-border-subtle)",
                        color: "var(--color-content-primary)"
                      }}>
                        {t(`traceability.lifecycleStages.${sn.lifecycleStage}` as any, { defaultValue: sn.lifecycleStage })} — {t("stockStatuses.Available")}
                      </span>
                    </td>
                    <td style={{ padding: "0.75rem 1rem", textAlign: "center" }}>
                      <Button variant="secondary" onPress={() => setSelectedSerial(sn)}>
                        {t("traceability.viewDetails")}
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
      {isAddLotOpen && (
        <CreateLotModal
          isOpen={isAddLotOpen}
          onClose={() => setIsAddLotOpen(false)}
          legalEntityId={legalEntityId}
          onLotCreated={handleLotCreated}
        />
      )}

      {isAddSerialOpen && (
        <CreateSerialModal
          isOpen={isAddSerialOpen}
          onClose={() => setIsAddSerialOpen(false)}
          legalEntityId={legalEntityId}
          onSerialCreated={handleSerialCreated}
        />
      )}

      {selectedLot && (
        <LotDetailModal
          isOpen={Boolean(selectedLot)}
          onClose={() => setSelectedLot(null)}
          lot={selectedLot}
        />
      )}

      {selectedSerial && (
        <SerialDetailModal
          isOpen={Boolean(selectedSerial)}
          onClose={() => setSelectedSerial(null)}
          serial={selectedSerial}
        />
      )}
    </div>
  );
}

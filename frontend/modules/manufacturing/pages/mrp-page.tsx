import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { ManufacturingHeader } from "../components/manufacturing-header.tsx";
import { ManufacturingStatusBadge } from "../components/manufacturing-status-badge.tsx";
import { NoLegalEntityPrompt } from "../components/no-legal-entity-prompt.tsx";
import { useManufacturingScope } from "../hooks/use-manufacturing-scope.ts";
import { CreatePlanningRunDialog } from "../features/mrp/create-planning-run-dialog.tsx";
import { CreateMpsDialog } from "../features/mrp/create-mps-dialog.tsx";
import { FirmProductionDialog } from "../features/mrp/firm-production-dialog.tsx";
import { usePlanningRunsQuery, usePlannedSuppliesQuery, useMpsPlansQuery } from "../hooks/use-manufacturing.ts";
import { useCatalogProducts } from "../hooks/use-catalog-products.ts";
import { Button } from "../../../components/primitives/button.tsx";
import { BdiText } from "../../../components/formatting/bidi-text.tsx";
import type { PlannedSupply } from "../types.ts";

export function MrpPage(): React.JSX.Element {
  const { t } = useTranslation("manufacturing");
  const { legalEntityId, isReady } = useManufacturingScope();
  const { getProductDisplayName, getProductSku } = useCatalogProducts();

  const [isRunOpen, setIsRunOpen] = useState(false);
  const [isMpsOpen, setIsMpsOpen] = useState(false);
  const [selectedSupplyForFirm, setSelectedSupplyForFirm] = useState<PlannedSupply | null>(null);

  const { data: runs = [], isLoading: runsLoading } = usePlanningRunsQuery(legalEntityId);
  const [selectedRunId, setSelectedRunId] = useState<string>("");
  const activeRunId = selectedRunId || runs[0]?.id || "";

  const { data: supplies = [], isLoading: suppliesLoading } = usePlannedSuppliesQuery(activeRunId);
  const { data: mpsPlans = [] } = useMpsPlansQuery(legalEntityId);

  if (!isReady) {
    return <NoLegalEntityPrompt />;
  }

  return (
    <div
      data-testid="manufacturing-mrp-page"
      style={{
        padding: "1rem",
        width: "100%",
        maxWidth: "1600px",
        margin: "0 auto",
        boxSizing: "border-box",
        overflowX: "hidden"
      }}
    >
      <ManufacturingHeader
        title={t("mrp.title")}
        subtitle={t("mrp.subtitle")}
        primaryAction={
          <div style={{ display: "flex", gap: "8px", flexWrap: "wrap" }}>
            <Button variant="secondary" onClick={() => setIsMpsOpen(true)}>
              + {t("mrp.createMps")}
            </Button>
            <Button variant="primary" onClick={() => setIsRunOpen(true)}>
              + {t("mrp.launchRun")}
            </Button>
          </div>
        }
      />

      {/* Demand & Netting Logic Explanation Card */}
      <div
        style={{
          background: "#ffffff",
          border: "1px solid #cbd5e1",
          borderRadius: "8px",
          padding: "16px",
          marginBottom: "16px",
          boxShadow: "0 1px 3px rgba(0,0,0,0.05)"
        }}
      >
        <h3 style={{ margin: "0 0 8px", fontSize: "15px", fontWeight: 700, color: "#102a43" }}>
          {t("mrp.nettingRulesTitle")}
        </h3>
        <p style={{ margin: "0 0 12px", fontSize: "13px", color: "#475569" }}>
          {t("mrp.nettingRulesDescription")}
        </p>

        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", gap: "12px" }}>
          <div style={{ background: "#f8fafc", padding: "10px", borderRadius: "6px", border: "1px solid #e2e8f0" }}>
            <div style={{ fontSize: "12px", color: "#64748b" }}>{t("mrp.forecastDemand")}</div>
            <div style={{ fontSize: "15px", fontWeight: 700, color: "#0f172a" }}>{t("mrp.forecastConsumptionNote")}</div>
          </div>
          <div style={{ background: "#f8fafc", padding: "10px", borderRadius: "6px", border: "1px solid #e2e8f0" }}>
            <div style={{ fontSize: "12px", color: "#64748b" }}>{t("mrp.salesDemand")}</div>
            <div style={{ fontSize: "15px", fontWeight: 700, color: "#0f172a" }}>{t("mrp.salesPriorityNote")}</div>
          </div>
          <div style={{ background: "#f8fafc", padding: "10px", borderRadius: "6px", border: "1px solid #e2e8f0" }}>
            <div style={{ fontSize: "12px", color: "#64748b" }}>{t("mrp.approvedMpsCount")}</div>
            <div style={{ fontSize: "15px", fontWeight: 700, color: "#0284c7" }}>{mpsPlans.length} {t("mrp.plan")}</div>
          </div>
        </div>
      </div>

      {/* Planning Runs Selector & Suggested Supplies */}
      <div
        style={{
          background: "#ffffff",
          border: "1px solid #cbd5e1",
          borderRadius: "8px",
          padding: "16px",
          boxShadow: "0 1px 3px rgba(0,0,0,0.05)"
        }}
      >
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "16px", flexWrap: "wrap", gap: "12px" }}>
          <div style={{ display: "flex", gap: "8px", alignItems: "center", flexWrap: "wrap" }}>
            <label style={{ fontSize: "13px", fontWeight: 600, color: "#475569" }}>
              {t("mrp.planningCycle")}:
            </label>
            <select
              value={activeRunId}
              onChange={(e) => setSelectedRunId(e.target.value)}
              style={{
                padding: "8px 12px",
                borderRadius: "6px",
                border: "1px solid #cbd5e1",
                fontSize: "14px",
                background: "#ffffff",
                maxWidth: "100%"
              }}
            >
              {runs.map((r) => (
                <option key={r.id} value={r.id}>
                  {r.runNumber ?? r.id.slice(0, 8)} ({r.horizonStart.slice(0, 10)} {t("mrp.to", "إلى")} {r.horizonEnd.slice(0, 10)})
                </option>
              ))}
            </select>
          </div>

          <div style={{ fontSize: "13px", color: "#64748b" }}>
            {t("mrp.totalSuggestions")}: <strong>{supplies.length}</strong>
          </div>
        </div>

        {supplies.length === 0 ? (
          <div style={{ padding: "32px 0", textAlign: "center", color: "#64748b" }}>
            {suppliesLoading || runsLoading ? t("common.loading") : t("mrp.noSuggestions")}
          </div>
        ) : (
          <div style={{ overflowX: "auto", WebkitOverflowScrolling: "touch", width: "100%" }}>
            <table style={{ width: "100%", minWidth: "850px", borderCollapse: "collapse", fontSize: "13px", textAlign: "right" }}>
              <thead>
                <tr style={{ background: "#f8fafc", borderBottom: "1px solid #e2e8f0", color: "#475569" }}>
                  <th style={{ padding: "12px 14px" }}>{t("mrp.supplyType")}</th>
                  <th style={{ padding: "12px 14px" }}>{t("orders.product")}</th>
                  <th style={{ padding: "12px 14px" }}>{t("mrp.suggestedQty")}</th>
                  <th style={{ padding: "12px 14px" }}>{t("mrp.dueDate")}</th>
                  <th style={{ padding: "12px 14px" }}>{t("mrp.planningState")}</th>
                  <th style={{ padding: "12px 14px", textAlign: "center" }}>{t("common.actions")}</th>
                </tr>
              </thead>
              <tbody>
                {supplies.map((sup) => {
                  const productName = getProductDisplayName(sup.productVariantId);
                  const sku = getProductSku(sup.productVariantId);
                  const isFirmed = sup.state === "Firmed";

                  return (
                    <tr key={sup.id} style={{ borderBottom: "1px solid #f1f5f9" }}>
                      <td style={{ padding: "12px 14px", fontWeight: 700 }}>
                        <span style={{
                          display: "inline-block",
                          padding: "3px 8px",
                          borderRadius: "4px",
                          fontSize: "12px",
                          background: (sup.supplyType === "Production" || sup.supplyType === "Manufacture") ? "#e0f2fe" : "#fef3c7",
                          color: (sup.supplyType === "Production" || sup.supplyType === "Manufacture") ? "#0369a1" : "#b45309"
                        }}>
                          {(sup.supplyType === "Production" || sup.supplyType === "Manufacture")
                            ? t("mrp.supplyManufacture")
                            : sup.supplyType === "Purchase"
                            ? t("mrp.supplyPurchase")
                            : t("mrp.supplyTransfer")}
                        </span>
                      </td>
                      <td style={{ padding: "12px 14px" }}>
                        <div style={{ display: "flex", flexDirection: "column", gap: "2px" }}>
                          <span style={{ fontWeight: 700, color: "#102a43", fontSize: "14px" }}>
                            {productName}
                          </span>
                          {sku ? (
                            <span style={{ fontSize: "11px", color: "#64748b" }}>
                              <BdiText>{`${t("common.sku")}: ${sku}`}</BdiText>
                            </span>
                          ) : (
                            <span style={{ fontSize: "11px", color: "#94a3b8" }}>
                              <BdiText>{sup.productVariantId.slice(0, 8)}</BdiText>
                            </span>
                          )}
                        </div>
                      </td>
                      <td style={{ padding: "12px 14px", fontWeight: 600 }}>
                        <BdiText>{`${sup.quantity} ${sup.uomCode}`}</BdiText>
                      </td>
                      <td style={{ padding: "12px 14px", color: "#475569" }}>
                        {(sup.dueDate ?? sup.needDate).slice(0, 10)}
                      </td>
                      <td style={{ padding: "12px 14px" }}>
                        <ManufacturingStatusBadge state={sup.state} />
                        {isFirmed && sup.firmedOrderId && (
                          <div style={{ fontSize: "11px", color: "#047857", fontWeight: 600, marginTop: "2px" }}>
                            {t("mrp.firmedOrderBadge")}: <BdiText>{sup.firmedOrderId.slice(0, 8)}</BdiText>
                          </div>
                        )}
                      </td>
                      <td style={{ padding: "12px 14px", textAlign: "center" }}>
                        {sup.state === "Suggested" && (sup.supplyType === "Production" || sup.supplyType === "Manufacture") ? (
                          <Button
                            variant="primary"
                            size="small"
                            onClick={() => setSelectedSupplyForFirm(sup)}
                          >
                            {t("mrp.firmProductionOrder")}
                          </Button>
                        ) : isFirmed ? (
                          <span style={{ color: "#047857", fontSize: "12px", fontWeight: 600 }}>
                            ✓ {t("mrp.firmedOrderBadge")}
                          </span>
                        ) : null}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </div>

      <CreatePlanningRunDialog
        isOpen={isRunOpen}
        onClose={() => setIsRunOpen(false)}
        legalEntityId={legalEntityId ?? ""}
      />

      <CreateMpsDialog
        isOpen={isMpsOpen}
        onClose={() => setIsMpsOpen(false)}
        legalEntityId={legalEntityId ?? ""}
      />

      {selectedSupplyForFirm && (
        <FirmProductionDialog
          isOpen={Boolean(selectedSupplyForFirm)}
          onClose={() => setSelectedSupplyForFirm(null)}
          legalEntityId={legalEntityId ?? ""}
          plannedSupply={selectedSupplyForFirm}
        />
      )}
    </div>
  );
}

import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { useOutletContext } from "react-router-dom";
import type { WorkspaceOutletContext } from "../../../app/router/route-contract.ts";
import { ManufacturingHeader } from "../components/manufacturing-header.tsx";
import { ManufacturingStatusBadge } from "../components/manufacturing-status-badge.tsx";
import { CreatePlanningRunDialog } from "../features/mrp/create-planning-run-dialog.tsx";
import { CreateMpsDialog } from "../features/mrp/create-mps-dialog.tsx";
import { FirmProductionDialog } from "../features/mrp/firm-production-dialog.tsx";
import { usePlanningRunsQuery, usePlannedSuppliesQuery, useMpsPlansQuery } from "../hooks/use-manufacturing.ts";
import { Button } from "../../../components/primitives/button.tsx";
import type { PlannedSupply } from "../types.ts";

export function MrpPage(): React.JSX.Element {
  const { t } = useTranslation("manufacturing");
  const outletCtx = useOutletContext<WorkspaceOutletContext | undefined>();
  const legalEntityId = outletCtx?.legalEntityId ?? "00000000-0000-0000-0000-000000000001";

  const [isRunOpen, setIsRunOpen] = useState(false);
  const [isMpsOpen, setIsMpsOpen] = useState(false);
  const [selectedSupplyForFirm, setSelectedSupplyForFirm] = useState<PlannedSupply | null>(null);

  const { data: runs = [], isLoading: runsLoading } = usePlanningRunsQuery(legalEntityId);
  const [selectedRunId, setSelectedRunId] = useState<string>("");
  const activeRunId = selectedRunId || runs[0]?.id || "";

  const { data: supplies = [], isLoading: suppliesLoading } = usePlannedSuppliesQuery(activeRunId);
  const { data: mpsPlans = [] } = useMpsPlansQuery(legalEntityId);

  return (
    <div
      data-testid="manufacturing-mrp-page"
      style={{
        padding: "1.25rem 1.5rem",
        width: "100%",
        maxWidth: "1600px",
        margin: "0 auto",
        boxSizing: "border-box"
      }}
    >
      <ManufacturingHeader
        title={t("mrp.title")}
        subtitle={t("mrp.subtitle")}
        primaryAction={
          <div style={{ display: "flex", gap: "8px" }}>
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
            <div style={{ fontSize: "16px", fontWeight: 700, color: "#0f172a" }}>{t("mrp.forecastConsumptionNote")}</div>
          </div>
          <div style={{ background: "#f8fafc", padding: "10px", borderRadius: "6px", border: "1px solid #e2e8f0" }}>
            <div style={{ fontSize: "12px", color: "#64748b" }}>{t("mrp.salesDemand")}</div>
            <div style={{ fontSize: "16px", fontWeight: 700, color: "#0f172a" }}>{t("mrp.salesPriorityNote")}</div>
          </div>
          <div style={{ background: "#f8fafc", padding: "10px", borderRadius: "6px", border: "1px solid #e2e8f0" }}>
            <div style={{ fontSize: "12px", color: "#64748b" }}>{t("mrp.approvedMpsCount")}</div>
            <div style={{ fontSize: "16px", fontWeight: 700, color: "#0284c7" }}>{mpsPlans.length} {t("mrp.plan")}</div>
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
          <div style={{ display: "flex", gap: "8px", alignItems: "center" }}>
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
                background: "#ffffff"
              }}
            >
              {runs.map((r) => (
                <option key={r.id} value={r.id}>
                  {r.runNumber} ({r.horizonStart.slice(0, 10)} {t("mrp.to")} {r.horizonEnd.slice(0, 10)})
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
            {suppliesLoading ? t("common.loading") : t("mrp.noSuggestions")}
          </div>
        ) : (
          <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "14px", textAlign: "right" }}>
            <thead>
              <tr style={{ background: "#f8fafc", borderBottom: "1px solid #e2e8f0", color: "#475569" }}>
                <th style={{ padding: "12px 16px" }}>{t("mrp.supplyType")}</th>
                <th style={{ padding: "12px 16px" }}>{t("orders.product")}</th>
                <th style={{ padding: "12px 16px" }}>{t("mrp.suggestedQty")}</th>
                <th style={{ padding: "12px 16px" }}>{t("mrp.dueDate")}</th>
                <th style={{ padding: "12px 16px" }}>{t("mrp.planningState")}</th>
                <th style={{ padding: "12px 16px", textAlign: "center" }}>{t("common.actions")}</th>
              </tr>
            </thead>
            <tbody>
              {supplies.map((sup) => (
                <tr key={sup.id} style={{ borderBottom: "1px solid #f1f5f9" }}>
                  <td style={{ padding: "12px 16px", fontWeight: 700 }}>
                    {sup.supplyType === "Manufacture" ? t("mrp.supplyManufacture") : sup.supplyType === "Purchase" ? t("mrp.supplyPurchase") : t("mrp.supplyTransfer")}
                  </td>
                  <td style={{ padding: "12px 16px" }}>
                    {sup.productVariantId.slice(0, 8)}...
                  </td>
                  <td style={{ padding: "12px 16px", fontWeight: 600 }}>
                    {sup.quantity} {sup.uomCode}
                  </td>
                  <td style={{ padding: "12px 16px", color: "#475569" }}>
                    {sup.dueDate.slice(0, 10)}
                  </td>
                  <td style={{ padding: "12px 16px" }}>
                    <ManufacturingStatusBadge state={sup.state} />
                  </td>
                  <td style={{ padding: "12px 16px", textAlign: "center" }}>
                    {sup.state === "Suggested" && sup.supplyType === "Manufacture" && (
                      <Button
                        variant="primary"
                        onClick={() => setSelectedSupplyForFirm(sup)}
                      >
                        {t("mrp.firmProductionOrder")}
                      </Button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      <CreatePlanningRunDialog
        isOpen={isRunOpen}
        onClose={() => setIsRunOpen(false)}
        legalEntityId={legalEntityId}
      />

      <CreateMpsDialog
        isOpen={isMpsOpen}
        onClose={() => setIsMpsOpen(false)}
        legalEntityId={legalEntityId}
      />

      {selectedSupplyForFirm && (
        <FirmProductionDialog
          isOpen={Boolean(selectedSupplyForFirm)}
          onClose={() => setSelectedSupplyForFirm(null)}
          legalEntityId={legalEntityId}
          plannedSupply={selectedSupplyForFirm}
        />
      )}
    </div>
  );
}

import React, { useState } from "react";
import { useParams, Link } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { ManufacturingStatusBadge } from "../components/manufacturing-status-badge.tsx";
import { CloseReadinessPanel } from "../features/orders/close-readiness-panel.tsx";
import { OrderActionDialog } from "../features/orders/order-action-dialog.tsx";
import { MaterialActionDialog } from "../features/orders/material-action-dialog.tsx";
import { OutputReportDialog } from "../features/orders/output-report-dialog.tsx";
import { ScrapReportDialog } from "../features/orders/scrap-report-dialog.tsx";
import { QualityGateDialog } from "../features/orders/quality-gate-dialog.tsx";
import { RecordCostFactDialog } from "../features/costing/record-cost-fact-dialog.tsx";
import { AllocateCostDialog } from "../features/costing/allocate-cost-dialog.tsx";
import {
  useOrderQuery,
  useMaterialsQuery,
  useOperationsQuery,
  useQualityGatesQuery,
  useCostQuery
} from "../hooks/use-manufacturing.ts";
import { Button } from "../../../components/primitives/button.tsx";
import type { ProductionMaterial } from "../types.ts";

export function ProductionOrderWorkspacePage(): React.JSX.Element {
  const { orderId } = useParams<{ orderId: string }>();
  const { t } = useTranslation("manufacturing");

  const [activeTab, setActiveTab] = useState<"snapshot" | "materials" | "operations" | "quality" | "costing" | "outputs">("materials");

  // Dialog triggers
  const [orderAction, setOrderAction] = useState<"release" | "start" | "hold" | "resume" | "complete" | "close" | "cancel" | null>(null);
  const [selectedMaterial, setSelectedMaterial] = useState<{ material: ProductionMaterial; action: "Reserved" | "Issued" | "Consumed" | "Returned" } | null>(null);
  const [isOutputOpen, setIsOutputOpen] = useState(false);
  const [isScrapOpen, setIsScrapOpen] = useState(false);
  const [isQualityGateOpen, setIsQualityGateOpen] = useState(false);
  const [isRecordCostOpen, setIsRecordCostOpen] = useState(false);
  const [selectedOutputForCost, setSelectedOutputForCost] = useState<string | null>(null);

  const { data: order, isLoading: orderLoading } = useOrderQuery(orderId ?? "");
  const { data: materials = [] } = useMaterialsQuery(orderId ?? "");
  const { data: operations = [] } = useOperationsQuery(orderId ?? "");
  const { data: qualityGates = [] } = useQualityGatesQuery(orderId ?? "");
  const { data: cost } = useCostQuery(orderId ?? "");

  if (orderLoading) {
    return (
      <div style={{ padding: "40px", textAlign: "center", color: "#64748b" }}>
        {t("common.loading")}
      </div>
    );
  }

  if (!order) {
    return (
      <div style={{ padding: "40px", textAlign: "center" }}>
        <h2>{t("common.notFound")}</h2>
        <Link to="/manufacturing/orders" style={{ color: "#0284c7" }}>
          {t("common.backToOrders")} ←
        </Link>
      </div>
    );
  }

  return (
    <div
      data-testid="manufacturing-order-workspace"
      style={{
        padding: "1.25rem 1.5rem",
        width: "100%",
        maxWidth: "1600px",
        margin: "0 auto",
        boxSizing: "border-box"
      }}
    >
      {/* Workspace Header */}
      <header
        style={{
          background: "#ffffff",
          border: "1px solid #cbd5e1",
          borderRadius: "8px",
          padding: "1.25rem",
          marginBottom: "16px",
          boxShadow: "0 1px 3px rgba(0,0,0,0.05)"
        }}
      >
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", flexWrap: "wrap", gap: "12px" }}>
          <div>
            <nav style={{ fontSize: "0.8125rem", color: "#64748b", marginBottom: "0.25rem" }}>
              <Link to="/manufacturing/orders" style={{ color: "inherit", textDecoration: "none" }}>
                {t("orders.title")}
              </Link>
              {" / "}
              <span style={{ color: "#0f172a", fontWeight: 600 }}>{order.orderNumber}</span>
            </nav>
            <div style={{ display: "flex", alignItems: "center", gap: "12px" }}>
              <h1 style={{ margin: 0, fontSize: "1.5rem", color: "#102a43", fontWeight: 700 }}>
                {order.orderNumber}
              </h1>
              <ManufacturingStatusBadge state={order.state} />
            </div>
            <p style={{ margin: "4px 0 0", fontSize: "0.875rem", color: "#64748b" }}>
              {t("orders.orderedQtyLabel")}: {order.orderedQty} {order.uomCode} • {t("orders.completedQtyLabel")}: {order.completedQty} {order.uomCode} • {t("orders.scrapQtyLabel")}: {order.scrapQty} {order.uomCode}
            </p>
          </div>

          {/* Quick Action Triggers based on Order State */}
          <div style={{ display: "flex", gap: "8px", flexWrap: "wrap" }}>
            {order.state === "Draft" && (
              <Button variant="primary" onClick={() => setOrderAction("release")}>
                {t("orders.actions.release")}
              </Button>
            )}
            {order.state === "Released" && (
              <Button variant="primary" onClick={() => setOrderAction("start")}>
                {t("orders.actions.start")}
              </Button>
            )}
            {order.state === "InProgress" && (
              <>
                <Button variant="secondary" onClick={() => setOrderAction("hold")}>
                  {t("orders.actions.hold")}
                </Button>
                <Button variant="primary" onClick={() => setOrderAction("complete")}>
                  {t("orders.actions.complete")}
                </Button>
              </>
            )}
            {order.state === "OnHold" && (
              <Button variant="primary" onClick={() => setOrderAction("resume")}>
                {t("orders.actions.resume")}
              </Button>
            )}
            {order.state === "Completed" && (
              <Button variant="primary" onClick={() => setOrderAction("close")}>
                {t("orders.actions.close")}
              </Button>
            )}
          </div>
        </div>

        {/* Tab Navigation */}
        <div style={{ display: "flex", gap: "6px", borderTop: "1px solid #f1f5f9", marginTop: "16px", paddingTop: "12px", overflowX: "auto" }}>
          {[
            { id: "materials" as const, label: t("workspace.materialsTab") },
            { id: "operations" as const, label: t("workspace.operationsTab") },
            { id: "outputs" as const, label: t("workspace.outputsTab") },
            { id: "quality" as const, label: t("workspace.qualityTab") },
            { id: "costing" as const, label: t("workspace.costingTab") },
            { id: "snapshot" as const, label: t("workspace.definitionSnapshot") }
          ].map((tab) => (
            <button
              key={tab.id}
              onClick={() => setActiveTab(tab.id)}
              style={{
                padding: "8px 16px",
                border: "none",
                borderRadius: "6px",
                fontSize: "14px",
                fontWeight: activeTab === tab.id ? 700 : 500,
                background: activeTab === tab.id ? "#e0f2fe" : "transparent",
                color: activeTab === tab.id ? "#0284c7" : "#475569",
                cursor: "pointer"
              }}
            >
              {tab.label}
            </button>
          ))}
        </div>
      </header>

      {/* Main Tab Content */}
      <div style={{ display: "grid", gridTemplateColumns: "1fr 340px", gap: "16px", alignItems: "flex-start" }}>
        {/* Left Column: Tab Panels */}
        <div style={{ background: "#ffffff", border: "1px solid #cbd5e1", borderRadius: "8px", padding: "16px", minHeight: "400px" }}>
          {/* Materials Tab */}
          {activeTab === "materials" && (
            <div>
              <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "12px" }}>
                <h3 style={{ margin: 0, fontSize: "16px", fontWeight: 700, color: "#102a43" }}>
                  {t("materials.title")}
                </h3>
              </div>
              <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "13px", textAlign: "right" }}>
                <thead>
                  <tr style={{ background: "#f8fafc", borderBottom: "1px solid #e2e8f0", color: "#475569" }}>
                    <th style={{ padding: "8px 12px" }}>{t("orders.component")}</th>
                    <th style={{ padding: "8px 12px" }}>{t("materials.required")}</th>
                    <th style={{ padding: "8px 12px" }}>{t("materials.reserved")}</th>
                    <th style={{ padding: "8px 12px" }}>{t("materials.issued")}</th>
                    <th style={{ padding: "8px 12px" }}>{t("materials.consumed")}</th>
                    <th style={{ padding: "8px 12px" }}>{t("materials.returned")}</th>
                    <th style={{ padding: "8px 12px" }}>{t("orders.stateLabel")}</th>
                    <th style={{ padding: "8px 12px", textAlign: "center" }}>{t("common.actions")}</th>
                  </tr>
                </thead>
                <tbody>
                  {materials.map((mat) => (
                    <tr key={mat.id} style={{ borderBottom: "1px solid #f1f5f9" }}>
                      <td style={{ padding: "10px 12px", fontWeight: 600 }}>{mat.componentProductVariantId.slice(0, 8)}...</td>
                      <td style={{ padding: "10px 12px" }}>{mat.requiredQty} {mat.uomCode}</td>
                      <td style={{ padding: "10px 12px" }}>{mat.reservedQty}</td>
                      <td style={{ padding: "10px 12px", color: "#0284c7" }}>{mat.issuedQty}</td>
                      <td style={{ padding: "10px 12px", color: "#15803d" }}>{mat.consumedQty}</td>
                      <td style={{ padding: "10px 12px", color: "#b45309" }}>{mat.returnedQty}</td>
                      <td style={{ padding: "10px 12px" }}><ManufacturingStatusBadge state={mat.state} /></td>
                      <td style={{ padding: "10px 12px", textAlign: "center" }}>
                        <div style={{ display: "inline-flex", gap: "4px" }}>
                          <Button variant="secondary" onClick={() => setSelectedMaterial({ material: mat, action: "Issued" })}>
                            {t("materials.stageIssue")}
                          </Button>
                          <Button variant="secondary" onClick={() => setSelectedMaterial({ material: mat, action: "Consumed" })}>
                            {t("materials.directConsume")}
                          </Button>
                          <Button variant="secondary" onClick={() => setSelectedMaterial({ material: mat, action: "Returned" })}>
                            {t("materials.returnMaterial")}
                          </Button>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          {/* Operations Tab */}
          {activeTab === "operations" && (
            <div>
              <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "12px" }}>
                <h3 style={{ margin: 0, fontSize: "16px", fontWeight: 700, color: "#102a43" }}>
                  {t("shopFloor.title")}
                </h3>
              </div>
              <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "13px", textAlign: "right" }}>
                <thead>
                  <tr style={{ background: "#f8fafc", borderBottom: "1px solid #e2e8f0", color: "#475569" }}>
                    <th style={{ padding: "8px 12px" }}>{t("shopFloor.sequence")}</th>
                    <th style={{ padding: "8px 12px" }}>{t("common.code")}</th>
                    <th style={{ padding: "8px 12px" }}>{t("shopFloor.operationName")}</th>
                    <th style={{ padding: "8px 12px" }}>{t("shopFloor.standardMinutes")}</th>
                    <th style={{ padding: "8px 12px" }}>{t("shopFloor.actualMinutes")}</th>
                    <th style={{ padding: "8px 12px" }}>{t("orders.stateLabel")}</th>
                  </tr>
                </thead>
                <tbody>
                  {operations.map((op) => (
                    <tr key={op.id} style={{ borderBottom: "1px solid #f1f5f9" }}>
                      <td style={{ padding: "10px 12px", fontWeight: 700 }}>#{op.sequenceNo}</td>
                      <td style={{ padding: "10px 12px" }}>{op.code}</td>
                      <td style={{ padding: "10px 12px", fontWeight: 600 }}>{op.name}</td>
                      <td style={{ padding: "10px 12px" }}>{op.standardSetupMinutes + op.standardRunMinutesPerUnit} {t("shopFloor.minutesUnit")}</td>
                      <td style={{ padding: "10px 12px" }}>{op.actualLaborMinutes + op.actualMachineMinutes} {t("shopFloor.minutesUnit")}</td>
                      <td style={{ padding: "10px 12px" }}><ManufacturingStatusBadge state={op.state} /></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          {/* Outputs Tab */}
          {activeTab === "outputs" && (
            <div>
              <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "12px" }}>
                <h3 style={{ margin: 0, fontSize: "16px", fontWeight: 700, color: "#102a43" }}>
                  {t("workspace.outputsTab")}
                </h3>
                <div style={{ display: "flex", gap: "8px" }}>
                  <Button variant="primary" onClick={() => setIsOutputOpen(true)}>
                    + {t("shopFloor.recordOutput")}
                  </Button>
                  <Button variant="secondary" onClick={() => setIsScrapOpen(true)}>
                    + {t("shopFloor.reportScrap")}
                  </Button>
                </div>
              </div>
              <div style={{ background: "#f8fafc", padding: "16px", borderRadius: "6px", border: "1px solid #e2e8f0" }}>
                <p style={{ margin: "0 0 8px", fontSize: "14px", fontWeight: 600 }}>
                  {t("orders.totalGoodOutput")}: {order.completedQty} {order.uomCode}
                </p>
                <p style={{ margin: 0, fontSize: "14px", fontWeight: 600, color: "#dc2626" }}>
                  {t("orders.totalScrapOutput")}: {order.scrapQty} {order.uomCode}
                </p>
              </div>
            </div>
          )}

          {/* Quality Gates Tab */}
          {activeTab === "quality" && (
            <div>
              <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "12px" }}>
                <h3 style={{ margin: 0, fontSize: "16px", fontWeight: 700, color: "#102a43" }}>
                  {t("quality.title")}
                </h3>
                <Button variant="secondary" onClick={() => setIsQualityGateOpen(true)}>
                  {t("quality.updatePolicy")}
                </Button>
              </div>
              {qualityGates.length === 0 ? (
                <p style={{ color: "#64748b" }}>{t("orders.noQualityGates")}</p>
              ) : (
                <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "13px", textAlign: "right" }}>
                  <thead>
                    <tr style={{ background: "#f8fafc", borderBottom: "1px solid #e2e8f0" }}>
                      <th style={{ padding: "8px 12px" }}>{t("orders.gateLabel")}</th>
                      <th style={{ padding: "8px 12px" }}>{t("orders.mandatoryLabel")}</th>
                      <th style={{ padding: "8px 12px" }}>{t("orders.decisionLabel")}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {qualityGates.map((qg) => (
                      <tr key={qg.id} style={{ borderBottom: "1px solid #f1f5f9" }}>
                        <td style={{ padding: "10px 12px" }}>{qg.id.slice(0, 8)}</td>
                        <td style={{ padding: "10px 12px" }}>{qg.required ? t("orders.mandatory") : t("orders.optional")}</td>
                        <td style={{ padding: "10px 12px" }}><ManufacturingStatusBadge state={qg.decisionState} /></td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </div>
          )}

          {/* Costing Tab */}
          {activeTab === "costing" && (
            <div>
              <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "12px" }}>
                <h3 style={{ margin: 0, fontSize: "16px", fontWeight: 700, color: "#102a43" }}>
                  {t("costing.title")}
                </h3>
                <Button variant="primary" onClick={() => setIsRecordCostOpen(true)}>
                  + {t("costing.recordCostFact")}
                </Button>
              </div>
              <div style={{ display: "grid", gridTemplateColumns: "repeat(3, 1fr)", gap: "12px", marginBottom: "16px" }}>
                <div style={{ background: "#f8fafc", padding: "12px", borderRadius: "6px", border: "1px solid #e2e8f0" }}>
                  <div style={{ fontSize: "12px", color: "#64748b" }}>{t("costing.materialCost")}</div>
                  <div style={{ fontSize: "18px", fontWeight: 700, color: "#0f172a" }}>{cost?.actualMaterialCost ?? 0} {cost?.currencyCode ?? "SAR"}</div>
                </div>
                <div style={{ background: "#f8fafc", padding: "12px", borderRadius: "6px", border: "1px solid #e2e8f0" }}>
                  <div style={{ fontSize: "12px", color: "#64748b" }}>{t("costing.laborCost")}</div>
                  <div style={{ fontSize: "18px", fontWeight: 700, color: "#0f172a" }}>{cost?.actualLaborCost ?? 0} {cost?.currencyCode ?? "SAR"}</div>
                </div>
                <div style={{ background: "#f8fafc", padding: "12px", borderRadius: "6px", border: "1px solid #e2e8f0" }}>
                  <div style={{ fontSize: "12px", color: "#64748b" }}>{t("costing.overheadCost")}</div>
                  <div style={{ fontSize: "18px", fontWeight: 700, color: "#0f172a" }}>{cost?.actualOverheadCost ?? 0} {cost?.currencyCode ?? "SAR"}</div>
                </div>
              </div>
            </div>
          )}

          {/* Snapshot Tab */}
          {activeTab === "snapshot" && (
            <div>
              <h3 style={{ margin: "0 0 12px", fontSize: "16px", fontWeight: 700, color: "#102a43" }}>
                {t("workspace.definitionSnapshot")}
              </h3>
              <p style={{ fontSize: "13px", color: "#64748b" }}>
                {t("workspace.immutableNotice")}
              </p>
              <div style={{ background: "#f8fafc", padding: "12px", borderRadius: "6px", border: "1px solid #e2e8f0", fontSize: "13px" }}>
                <div><strong>{t("orders.definitionId")}:</strong> {order.productionDefinitionId ?? t("orders.autoDefinition")}</div>
                <div><strong>{t("orders.plantLabel")}:</strong> {order.plantId}</div>
                <div><strong>{t("orders.plannedStart")}:</strong> {order.plannedStart?.slice(0, 10) ?? t("orders.unspecified")}</div>
                <div><strong>{t("orders.plannedEnd")}:</strong> {order.plannedEnd?.slice(0, 10) ?? t("orders.unspecified")}</div>
              </div>
            </div>
          )}
        </div>

        {/* Right Column: Close Readiness Panel */}
        <div>
          <CloseReadinessPanel orderId={order.id} />
        </div>
      </div>

      {/* Modal Dialogs */}
      {orderAction && (
        <OrderActionDialog
          isOpen={Boolean(orderAction)}
          onClose={() => setOrderAction(null)}
          order={order}
          action={orderAction}
        />
      )}

      {selectedMaterial && (
        <MaterialActionDialog
          isOpen={Boolean(selectedMaterial)}
          onClose={() => setSelectedMaterial(null)}
          orderId={order.id}
          requirementId={selectedMaterial.material.id}
          actionType={selectedMaterial.action}
          uomCode={selectedMaterial.material.uomCode}
        />
      )}

      <OutputReportDialog
        isOpen={isOutputOpen}
        onClose={() => setIsOutputOpen(false)}
        orderId={order.id}
        defaultUom={order.uomCode}
      />

      <ScrapReportDialog
        isOpen={isScrapOpen}
        onClose={() => setIsScrapOpen(false)}
        orderId={order.id}
        defaultUom={order.uomCode}
      />

      <QualityGateDialog
        isOpen={isQualityGateOpen}
        onClose={() => setIsQualityGateOpen(false)}
        orderId={order.id}
      />

      <RecordCostFactDialog
        isOpen={isRecordCostOpen}
        onClose={() => setIsRecordCostOpen(false)}
        orderId={order.id}
      />

      {selectedOutputForCost && (
        <AllocateCostDialog
          isOpen={Boolean(selectedOutputForCost)}
          onClose={() => setSelectedOutputForCost(null)}
          orderId={order.id}
          outputId={selectedOutputForCost}
        />
      )}
    </div>
  );
}

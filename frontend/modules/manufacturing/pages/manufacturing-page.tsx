import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Link, useOutletContext } from "react-router-dom";
import type { WorkspaceOutletContext } from "../../../app/router/route-contract.ts";
import { ManufacturingHeader } from "../components/manufacturing-header.tsx";
import { ManufacturingMetricRibbon } from "../components/manufacturing-metric-ribbon.tsx";
import { ManufacturingStatusBadge } from "../components/manufacturing-status-badge.tsx";
import { CreateOrderDialog } from "../features/orders/create-order-dialog.tsx";
import { useOrdersQuery, usePlanningRunsQuery } from "../hooks/use-manufacturing.ts";
import { Button } from "../../../components/primitives/button.tsx";

export function ManufacturingPage(): React.JSX.Element {
  const { t } = useTranslation("manufacturing");
  const outletCtx = useOutletContext<WorkspaceOutletContext | undefined>();
  const legalEntityId = outletCtx?.legalEntityId ?? "00000000-0000-0000-0000-000000000001";

  const [isCreateOpen, setIsCreateOpen] = useState(false);

  const { data: orders = [], isLoading: ordersLoading } = useOrdersQuery(legalEntityId);
  const { data: runs = [] } = usePlanningRunsQuery(legalEntityId);

  // Compute active & priority stats
  const inProgressOrders = orders.filter((o) => o.state === "InProgress");
  const releasedOrders = orders.filter((o) => o.state === "Released");
  const onHoldOrders = orders.filter((o) => o.state === "OnHold");

  const totalOrderedQty = orders.reduce((sum, o) => sum + o.orderedQty, 0);
  const totalCompletedQty = orders.reduce((sum, o) => sum + o.completedQty, 0);

  const metrics = [
    {
      id: "active-orders",
      label: t("metrics.activeOrders"),
      value: inProgressOrders.length,
      helperText: `${releasedOrders.length} ${t("status.Released")}`
    },
    {
      id: "waiting-execution",
      label: t("metrics.waitingExecution"),
      value: releasedOrders.length
    },
    {
      id: "on-hold",
      label: t("orders.onHoldOrders"),
      value: onHoldOrders.length,
      variant: onHoldOrders.length > 0 ? ("warning" as const) : ("neutral" as const)
    },
    {
      id: "planned-output",
      label: t("metrics.plannedOutput"),
      value: totalOrderedQty.toLocaleString()
    },
    {
      id: "completed-output",
      label: t("metrics.completedGoodOutput"),
      value: totalCompletedQty.toLocaleString(),
      variant: "positive" as const
    }
  ];

  return (
    <div
      data-testid="manufacturing-overview-page"
      style={{
        padding: "1.25rem 1.5rem",
        width: "100%",
        maxWidth: "1600px",
        margin: "0 auto",
        boxSizing: "border-box"
      }}
    >
      <ManufacturingHeader
        title={t("tabs.overview")}
        subtitle={t("subtitle")}
        primaryAction={
          <Button variant="primary" onClick={() => setIsCreateOpen(true)}>
            + {t("orders.createOrder")}
          </Button>
        }
      />

      <ManufacturingMetricRibbon metrics={metrics} />

      {/* Operational Priority Cards */}
      <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(320px, 1fr))", gap: "16px", marginTop: "1rem" }}>
        {/* Urgent Orders Alert */}
        <div
          style={{
            background: "#ffffff",
            border: "1px solid #cbd5e1",
            borderRadius: "8px",
            padding: "16px",
            boxShadow: "0 1px 3px rgba(0,0,0,0.05)"
          }}
        >
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "12px" }}>
            <h2 style={{ fontSize: "15px", fontWeight: 700, margin: 0, color: "#102a43" }}>
              {t("orders.actionRequiredTitle")}
            </h2>
            <Link to="/manufacturing/orders" style={{ fontSize: "13px", color: "#0284c7", textDecoration: "none", fontWeight: 600 }}>
              {t("orders.viewAll")} ({orders.length}) ←
            </Link>
          </div>

          {orders.length === 0 ? (
            <div style={{ padding: "24px 0", textAlign: "center", color: "#64748b", fontSize: "14px" }}>
              {ordersLoading ? t("common.loading") : t("orders.emptyTitle")}
            </div>
          ) : (
            <div style={{ display: "flex", flexDirection: "column", gap: "8px" }}>
              {orders.slice(0, 5).map((order) => (
                <Link
                  key={order.id}
                  to={`/manufacturing/orders/${order.id}`}
                  style={{
                    display: "flex",
                    justifyContent: "space-between",
                    alignItems: "center",
                    padding: "10px 12px",
                    background: "#f8fafc",
                    borderRadius: "6px",
                    border: "1px solid #e2e8f0",
                    textDecoration: "none",
                    color: "inherit"
                  }}
                >
                  <div>
                    <div style={{ fontSize: "14px", fontWeight: 600, color: "#0f172a" }}>
                      {order.orderNumber}
                    </div>
                    <div style={{ fontSize: "12px", color: "#64748b" }}>
                      {order.orderedQty} {order.uomCode} • {t("orders.needDatePrefix")}: {order.needDate?.slice(0, 10)}
                    </div>
                  </div>
                  <ManufacturingStatusBadge state={order.state} />
                </Link>
              ))}
            </div>
          )}
        </div>

        {/* Planning & MRP Overview */}
        <div
          style={{
            background: "#ffffff",
            border: "1px solid #cbd5e1",
            borderRadius: "8px",
            padding: "16px",
            boxShadow: "0 1px 3px rgba(0,0,0,0.05)"
          }}
        >
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "12px" }}>
            <h2 style={{ fontSize: "15px", fontWeight: 700, margin: 0, color: "#102a43" }}>
              {t("tabs.mrp")}
            </h2>
            <Link to="/manufacturing/mrp" style={{ fontSize: "13px", color: "#0284c7", textDecoration: "none", fontWeight: 600 }}>
              {t("common.workspace")} ←
            </Link>
          </div>

          <p style={{ fontSize: "13px", color: "#475569", margin: "0 0 12px" }}>
            {t("mrp.subtitle")}
          </p>

          <div style={{ background: "#f8fafc", padding: "12px", borderRadius: "6px", border: "1px solid #e2e8f0" }}>
            <div style={{ fontSize: "13px", color: "#64748b", marginBottom: "4px" }}>
              {t("mrp.lastRun")}:
            </div>
            {runs.length > 0 ? (
              <div>
                <div style={{ fontSize: "14px", fontWeight: 600, color: "#0f172a" }}>
                  {runs[0].runNumber}
                </div>
                <div style={{ fontSize: "12px", color: "#64748b" }}>
                  {runs[0].horizonStart.slice(0, 10)} {t("mrp.to")} {runs[0].horizonEnd.slice(0, 10)}
                </div>
              </div>
            ) : (
              <div style={{ fontSize: "13px", color: "#94a3b8" }}>
                {t("mrp.noRuns")}
              </div>
            )}
          </div>
        </div>
      </div>

      <CreateOrderDialog
        isOpen={isCreateOpen}
        onClose={() => setIsCreateOpen(false)}
        legalEntityId={legalEntityId}
      />
    </div>
  );
}

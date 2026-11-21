import React, { useState, useMemo } from "react";
import { useTranslation } from "react-i18next";
import { useNavigate, useOutletContext } from "react-router-dom";
import type { WorkspaceOutletContext } from "../../../app/router/route-contract.ts";
import { ManufacturingHeader } from "../components/manufacturing-header.tsx";
import { ManufacturingStatusBadge } from "../components/manufacturing-status-badge.tsx";
import { CreateOrderDialog } from "../features/orders/create-order-dialog.tsx";
import { OrderActionDialog } from "../features/orders/order-action-dialog.tsx";
import { ProductionOrderDetailDrawer } from "../features/orders/production-order-detail-drawer.tsx";
import { useOrdersQuery, usePlantsQuery } from "../hooks/use-manufacturing.ts";
import { Button } from "../../../components/primitives/button.tsx";
import type { ProductionOrder } from "../types.ts";

export function ProductionOrdersPage(): React.JSX.Element {
  const { t } = useTranslation("manufacturing");
  const navigate = useNavigate();
  const outletCtx = useOutletContext<WorkspaceOutletContext | undefined>();
  const legalEntityId = outletCtx?.legalEntityId ?? "00000000-0000-0000-0000-000000000001";

  const [selectedState, setSelectedState] = useState<string>("ALL");
  const [selectedPlantId, setSelectedPlantId] = useState<string>("ALL");
  const [searchQuery, setSearchQuery] = useState("");

  const [isCreateOpen, setIsCreateOpen] = useState(false);
  const [selectedOrderForDrawer, setSelectedOrderForDrawer] = useState<ProductionOrder | null>(null);
  const [orderForAction, setOrderForAction] = useState<{ order: ProductionOrder; action: "release" | "start" | "hold" | "resume" | "complete" | "close" | "cancel" } | null>(null);

  const { data: orders = [], isLoading } = useOrdersQuery(legalEntityId);
  const { data: plants = [] } = usePlantsQuery(legalEntityId);

  const filteredOrders = useMemo(() => {
    return orders.filter((order) => {
      if (selectedState !== "ALL" && order.state !== selectedState) return false;
      if (selectedPlantId !== "ALL" && order.plantId !== selectedPlantId) return false;
      if (searchQuery.trim()) {
        const q = searchQuery.toLowerCase();
        const matchNumber = order.orderNumber.toLowerCase().includes(q);
        const matchProduct = order.productVariantId.toLowerCase().includes(q);
        if (!matchNumber && !matchProduct) return false;
      }
      return true;
    });
  }, [orders, selectedState, selectedPlantId, searchQuery]);

  return (
    <div
      data-testid="manufacturing-orders-page"
      style={{
        padding: "1.25rem 1.5rem",
        width: "100%",
        maxWidth: "1600px",
        margin: "0 auto",
        boxSizing: "border-box"
      }}
    >
      <ManufacturingHeader
        title={t("orders.title")}
        subtitle={t("orders.subtitle")}
        primaryAction={
          <Button variant="primary" onClick={() => setIsCreateOpen(true)}>
            + {t("orders.createOrder")}
          </Button>
        }
      />

      {/* Filter Toolbar */}
      <div
        style={{
          display: "flex",
          flexWrap: "wrap",
          gap: "12px",
          alignItems: "center",
          background: "#ffffff",
          border: "1px solid #cbd5e1",
          borderRadius: "8px",
          padding: "12px 16px",
          marginBottom: "16px",
          boxShadow: "0 1px 2px rgba(0,0,0,0.04)"
        }}
      >
        <div style={{ flex: "1 1 240px" }}>
          <input
            type="text"
            placeholder={t("orders.searchPlaceholder")}
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            style={{
              width: "100%",
              padding: "8px 12px",
              borderRadius: "6px",
              border: "1px solid #cbd5e1",
              fontSize: "14px",
              boxSizing: "border-box"
            }}
          />
        </div>

        <div style={{ display: "flex", gap: "8px", alignItems: "center" }}>
          <label style={{ fontSize: "13px", fontWeight: 600, color: "#475569" }}>
            {t("common.filterByState")}
          </label>
          <select
            value={selectedState}
            onChange={(e) => setSelectedState(e.target.value)}
            style={{
              padding: "8px 12px",
              borderRadius: "6px",
              border: "1px solid #cbd5e1",
              fontSize: "14px",
              background: "#ffffff"
            }}
          >
            <option value="ALL">{t("common.allStates")}</option>
            <option value="Draft">{t("status.Draft")}</option>
            <option value="Released">{t("status.Released")}</option>
            <option value="InProgress">{t("status.InProgress")}</option>
            <option value="OnHold">{t("status.OnHold")}</option>
            <option value="Completed">{t("status.Completed")}</option>
            <option value="Closed">{t("status.Closed")}</option>
            <option value="Cancelled">{t("status.Cancelled")}</option>
          </select>
        </div>

        <div style={{ display: "flex", gap: "8px", alignItems: "center" }}>
          <label style={{ fontSize: "13px", fontWeight: 600, color: "#475569" }}>
            {t("common.filterByPlant")}
          </label>
          <select
            value={selectedPlantId}
            onChange={(e) => setSelectedPlantId(e.target.value)}
            style={{
              padding: "8px 12px",
              borderRadius: "6px",
              border: "1px solid #cbd5e1",
              fontSize: "14px",
              background: "#ffffff"
            }}
          >
            <option value="ALL">{t("common.allPlants")}</option>
            {plants.map((p) => (
              <option key={p.id} value={p.id}>
                {p.code} - {p.name}
              </option>
            ))}
          </select>
        </div>
      </div>

      {/* Orders Table Card */}
      <div
        style={{
          background: "#ffffff",
          border: "1px solid #cbd5e1",
          borderRadius: "8px",
          overflow: "hidden",
          boxShadow: "0 1px 3px rgba(0,0,0,0.05)"
        }}
      >
        <div style={{ overflowX: "auto" }}>
          <table style={{ width: "100%", borderCollapse: "collapse", textAlign: "right", fontSize: "14px" }}>
            <thead>
              <tr style={{ background: "#f8fafc", borderBottom: "1px solid #e2e8f0", color: "#475569", fontWeight: 600 }}>
                <th style={{ padding: "12px 16px" }}>{t("orders.orderNumberHeader")}</th>
                <th style={{ padding: "12px 16px" }}>{t("orders.plant")}</th>
                <th style={{ padding: "12px 16px" }}>{t("orders.quantity")}</th>
                <th style={{ padding: "12px 16px" }}>{t("orders.completedQty")}</th>
                <th style={{ padding: "12px 16px" }}>{t("orders.needDate")}</th>
                <th style={{ padding: "12px 16px" }}>{t("orders.stateLabel")}</th>
                <th style={{ padding: "12px 16px", textAlign: "center" }}>{t("common.actions")}</th>
              </tr>
            </thead>
            <tbody>
              {filteredOrders.length === 0 ? (
                <tr>
                  <td colSpan={7} style={{ padding: "32px 16px", textAlign: "center", color: "#64748b" }}>
                    {isLoading ? t("common.loading") : t("orders.noMatchingOrders")}
                  </td>
                </tr>
              ) : (
                filteredOrders.map((order) => {
                  const plant = plants.find((p) => p.id === order.plantId);
                  return (
                    <tr
                      key={order.id}
                      onClick={() => setSelectedOrderForDrawer(order)}
                      style={{
                        borderBottom: "1px solid #f1f5f9",
                        cursor: "pointer",
                        transition: "background 0.1s ease"
                      }}
                      onMouseEnter={(e) => (e.currentTarget.style.background = "#f8fafc")}
                      onMouseLeave={(e) => (e.currentTarget.style.background = "transparent")}
                    >
                      <td style={{ padding: "12px 16px", fontWeight: 700, color: "#0284c7" }}>
                        {order.orderNumber}
                      </td>
                      <td style={{ padding: "12px 16px", color: "#334155" }}>
                        {plant ? `${plant.code} - ${plant.name}` : order.plantId.slice(0, 8)}
                      </td>
                      <td style={{ padding: "12px 16px", fontWeight: 600 }}>
                        {order.orderedQty} {order.uomCode}
                      </td>
                      <td style={{ padding: "12px 16px", color: order.completedQty > 0 ? "#15803d" : "#64748b" }}>
                        {order.completedQty} {order.uomCode}
                      </td>
                      <td style={{ padding: "12px 16px", color: "#475569" }}>
                        {order.needDate?.slice(0, 10)}
                      </td>
                      <td style={{ padding: "12px 16px" }}>
                        <ManufacturingStatusBadge state={order.state} />
                      </td>
                      <td
                        style={{ padding: "12px 16px", textAlign: "center" }}
                        onClick={(e) => e.stopPropagation()}
                      >
                        <div style={{ display: "inline-flex", gap: "6px" }}>
                          {order.state === "Draft" && (
                            <Button
                              variant="secondary"
                              onClick={() => setOrderForAction({ order, action: "release" })}
                            >
                              {t("orders.actions.release")}
                            </Button>
                          )}
                          {order.state === "Released" && (
                            <Button
                              variant="secondary"
                              onClick={() => setOrderForAction({ order, action: "start" })}
                            >
                              {t("orders.actions.start")}
                            </Button>
                          )}
                          <Button
                            variant="secondary"
                            onClick={() => navigate(`/manufacturing/orders/${order.id}`)}
                          >
                            {t("common.workspace")} ←
                          </Button>
                        </div>
                      </td>
                    </tr>
                  );
                })
              )}
            </tbody>
          </table>
        </div>
      </div>

      <CreateOrderDialog
        isOpen={isCreateOpen}
        onClose={() => setIsCreateOpen(false)}
        legalEntityId={legalEntityId}
      />

      <ProductionOrderDetailDrawer
        order={selectedOrderForDrawer}
        isOpen={Boolean(selectedOrderForDrawer)}
        onClose={() => setSelectedOrderForDrawer(null)}
        onActionTrigger={(order, action) => {
          setSelectedOrderForDrawer(null);
          setOrderForAction({ order, action });
        }}
      />

      {orderForAction && (
        <OrderActionDialog
          isOpen={Boolean(orderForAction)}
          onClose={() => setOrderForAction(null)}
          order={orderForAction.order}
          action={orderForAction.action}
        />
      )}
    </div>
  );
}

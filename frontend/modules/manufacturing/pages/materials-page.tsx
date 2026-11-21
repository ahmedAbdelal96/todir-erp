import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { useOutletContext } from "react-router-dom";
import type { WorkspaceOutletContext } from "../../../app/router/route-contract.ts";
import { ManufacturingHeader } from "../components/manufacturing-header.tsx";
import { ManufacturingStatusBadge } from "../components/manufacturing-status-badge.tsx";
import { MaterialActionDialog } from "../features/orders/material-action-dialog.tsx";
import { useOrdersQuery } from "../hooks/use-manufacturing.ts";
import { Button } from "../../../components/primitives/button.tsx";

export function MaterialsPage(): React.JSX.Element {
  const { t } = useTranslation("manufacturing");
  const outletCtx = useOutletContext<WorkspaceOutletContext | undefined>();
  const legalEntityId = outletCtx?.legalEntityId ?? "00000000-0000-0000-0000-000000000001";

  const { data: orders = [] } = useOrdersQuery(legalEntityId);
  const activeOrders = orders.filter((o) => o.state === "Released" || o.state === "InProgress");

  const [selectedAction, setSelectedAction] = useState<{ orderId: string; requirementId: string; action: "Reserved" | "Issued" | "Consumed" | "Returned"; uomCode: string } | null>(null);

  return (
    <div
      data-testid="manufacturing-materials-page"
      style={{
        padding: "1.25rem 1.5rem",
        width: "100%",
        maxWidth: "1600px",
        margin: "0 auto",
        boxSizing: "border-box"
      }}
    >
      <ManufacturingHeader
        title={t("materials.title")}
        subtitle={t("materials.subtitle")}
      />

      <div
        style={{
          background: "#ffffff",
          border: "1px solid #cbd5e1",
          borderRadius: "8px",
          padding: "16px",
          boxShadow: "0 1px 3px rgba(0,0,0,0.05)"
        }}
      >
        <h3 style={{ margin: "0 0 12px", fontSize: "16px", fontWeight: 700, color: "#102a43" }}>
          {t("orders.activeMaterialsOrders")} ({activeOrders.length})
        </h3>

        {activeOrders.length === 0 ? (
          <div style={{ padding: "32px 0", textAlign: "center", color: "#64748b" }}>
            {t("orders.noActiveMaterials")}
          </div>
        ) : (
          <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "14px", textAlign: "right" }}>
            <thead>
              <tr style={{ background: "#f8fafc", borderBottom: "1px solid #e2e8f0", color: "#475569" }}>
                <th style={{ padding: "12px 16px" }}>{t("common.order")}</th>
                <th style={{ padding: "12px 16px" }}>{t("orders.quantity")}</th>
                <th style={{ padding: "12px 16px" }}>{t("orders.materialStatus")}</th>
                <th style={{ padding: "12px 16px" }}>{t("orders.stateLabel")}</th>
                <th style={{ padding: "12px 16px", textAlign: "center" }}>{t("orders.quickIssue")}</th>
              </tr>
            </thead>
            <tbody>
              {activeOrders.map((order) => (
                <tr key={order.id} style={{ borderBottom: "1px solid #f1f5f9" }}>
                  <td style={{ padding: "12px 16px", fontWeight: 700, color: "#0284c7" }}>
                    {order.orderNumber}
                  </td>
                  <td style={{ padding: "12px 16px" }}>
                    {order.orderedQty} {order.uomCode}
                  </td>
                  <td style={{ padding: "12px 16px" }}>
                    <ManufacturingStatusBadge state={order.materialStatus} />
                  </td>
                  <td style={{ padding: "12px 16px" }}>
                    <ManufacturingStatusBadge state={order.state} />
                  </td>
                  <td style={{ padding: "12px 16px", textAlign: "center" }}>
                    <div style={{ display: "inline-flex", gap: "6px" }}>
                      <Button
                        variant="secondary"
                        onClick={() => setSelectedAction({ orderId: order.id, requirementId: "MAT-REQ-01", action: "Issued", uomCode: order.uomCode })}
                      >
                        {t("materials.stageIssue")}
                      </Button>
                      <Button
                        variant="secondary"
                        onClick={() => setSelectedAction({ orderId: order.id, requirementId: "MAT-REQ-01", action: "Consumed", uomCode: order.uomCode })}
                      >
                        {t("materials.directConsume")}
                      </Button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {selectedAction && (
        <MaterialActionDialog
          isOpen={Boolean(selectedAction)}
          onClose={() => setSelectedAction(null)}
          orderId={selectedAction.orderId}
          requirementId={selectedAction.requirementId}
          actionType={selectedAction.action}
          uomCode={selectedAction.uomCode}
        />
      )}
    </div>
  );
}

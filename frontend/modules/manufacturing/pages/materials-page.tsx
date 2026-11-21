import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { ManufacturingHeader } from "../components/manufacturing-header.tsx";
import { ManufacturingStatusBadge } from "../components/manufacturing-status-badge.tsx";
import { NoLegalEntityPrompt } from "../components/no-legal-entity-prompt.tsx";
import { MaterialActionDialog } from "../features/orders/material-action-dialog.tsx";
import { useManufacturingScope } from "../hooks/use-manufacturing-scope.ts";
import { useOrdersQuery, useOrderMaterialsQuery } from "../hooks/use-manufacturing.ts";
import { Button } from "../../../components/primitives/button.tsx";

export function MaterialsPage(): React.JSX.Element {
  const { t } = useTranslation("manufacturing");
  const { legalEntityId, isReady } = useManufacturingScope();

  const { data: orders = [] } = useOrdersQuery(legalEntityId);
  const activeOrders = orders.filter((o) => o.state === "Released" || o.state === "InProgress" || o.state === "Draft");

  const [selectedOrderId, setSelectedOrderId] = useState<string>("");
  const effectiveOrderId = selectedOrderId || activeOrders[0]?.id || "";

  const { data: materials = [], isLoading: materialsLoading } = useOrderMaterialsQuery(effectiveOrderId);

  const [selectedAction, setSelectedAction] = useState<{
    orderId: string;
    requirementId: string;
    action: "Reserved" | "Issued" | "Consumed" | "Returned";
    uomCode: string;
    initialQty?: number;
  } | null>(null);

  if (!isReady) {
    return <NoLegalEntityPrompt />;
  }

  const currentOrder = orders.find((o) => o.id === effectiveOrderId);

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

      {/* Order Selector */}
      <div
        style={{
          display: "flex",
          gap: "12px",
          alignItems: "center",
          background: "#ffffff",
          border: "1px solid #cbd5e1",
          borderRadius: "8px",
          padding: "12px 16px",
          marginBottom: "16px"
        }}
      >
        <label style={{ fontSize: "13px", fontWeight: 600, color: "#475569" }}>
          {t("materials.orderMaterialsTitle")}:
        </label>
        <select
          value={effectiveOrderId}
          onChange={(e) => setSelectedOrderId(e.target.value)}
          style={{
            padding: "8px 12px",
            borderRadius: "6px",
            border: "1px solid #cbd5e1",
            fontSize: "14px",
            background: "#ffffff",
            minWidth: "300px"
          }}
        >
          {activeOrders.length === 0 ? (
            <option value="">{t("orders.noActiveMaterials")}</option>
          ) : (
            activeOrders.map((o) => (
              <option key={o.id} value={o.id}>
                {o.orderNumber} ({t(`status.${o.state}`)}) - {o.orderedQty} {o.uomCode}
              </option>
            ))
          )}
        </select>
        {currentOrder && (
          <div style={{ display: "flex", gap: "8px", alignItems: "center", marginInlineStart: "auto" }}>
            <span style={{ fontSize: "13px", color: "#64748b" }}>{t("orders.materialStatus")}:</span>
            <ManufacturingStatusBadge state={currentOrder.materialStatus} />
          </div>
        )}
      </div>

      {/* Materials Requirements Table */}
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
          {t("materials.orderMaterialsTitle")} ({materials.length})
        </h3>

        {!effectiveOrderId ? (
          <div style={{ padding: "32px 0", textAlign: "center", color: "#64748b" }}>
            {t("materials.selectOrderPrompt")}
          </div>
        ) : materialsLoading ? (
          <div style={{ padding: "32px 0", textAlign: "center", color: "#64748b" }}>
            {t("common.loading")}
          </div>
        ) : materials.length === 0 ? (
          <div style={{ padding: "32px 0", textAlign: "center", color: "#64748b" }}>
            {t("materials.noRequirements")}
          </div>
        ) : (
          <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "14px", textAlign: "right" }}>
            <thead>
              <tr style={{ background: "#f8fafc", borderBottom: "1px solid #e2e8f0", color: "#475569" }}>
                <th style={{ padding: "12px 16px" }}>{t("materials.component")}</th>
                <th style={{ padding: "12px 16px" }}>{t("common.state")}</th>
                <th style={{ padding: "12px 16px" }}>{t("materials.required")}</th>
                <th style={{ padding: "12px 16px" }}>{t("materials.reserved")}</th>
                <th style={{ padding: "12px 16px" }}>{t("materials.issued")}</th>
                <th style={{ padding: "12px 16px" }}>{t("materials.consumed")}</th>
                <th style={{ padding: "12px 16px" }}>{t("materials.returned")}</th>
                <th style={{ padding: "12px 16px", textAlign: "center" }}>{t("orders.quickIssue")}</th>
              </tr>
            </thead>
            <tbody>
              {materials.map((mat) => {
                const unreserved = Math.max(0, mat.requiredQty - mat.reservedQty);
                const unissued = Math.max(0, mat.requiredQty - mat.issuedQty);
                const unconsumed = Math.max(0, mat.issuedQty - mat.consumedQty);
                return (
                  <tr key={mat.id} style={{ borderBottom: "1px solid #f1f5f9" }}>
                    <td style={{ padding: "12px 16px", fontWeight: 700, color: "#0284c7" }}>
                      {mat.preferredProductVariantId || mat.productVariantId}
                    </td>
                    <td style={{ padding: "12px 16px" }}>
                      <ManufacturingStatusBadge state={mat.state} />
                    </td>
                    <td style={{ padding: "12px 16px" }}>
                      {mat.requiredQty} {mat.uomCode}
                    </td>
                    <td style={{ padding: "12px 16px" }}>
                      {mat.reservedQty} {mat.uomCode}
                    </td>
                    <td style={{ padding: "12px 16px", color: mat.issuedQty > 0 ? "#15803d" : undefined }}>
                      {mat.issuedQty} {mat.uomCode}
                    </td>
                    <td style={{ padding: "12px 16px", color: mat.consumedQty > 0 ? "#0284c7" : undefined }}>
                      {mat.consumedQty} {mat.uomCode}
                    </td>
                    <td style={{ padding: "12px 16px" }}>
                      {mat.returnedQty} {mat.uomCode}
                    </td>
                    <td style={{ padding: "12px 16px", textAlign: "center" }}>
                      <div style={{ display: "inline-flex", gap: "6px" }}>
                        <Button
                          variant="secondary"
                          size="small"
                          onClick={() =>
                            setSelectedAction({
                              orderId: effectiveOrderId,
                              requirementId: mat.id,
                              action: "Reserved",
                              uomCode: mat.uomCode,
                              initialQty: unreserved > 0 ? unreserved : mat.requiredQty
                            })
                          }
                        >
                          {t("materials.reserved")}
                        </Button>
                        <Button
                          variant="secondary"
                          size="small"
                          onClick={() =>
                            setSelectedAction({
                              orderId: effectiveOrderId,
                              requirementId: mat.id,
                              action: "Issued",
                              uomCode: mat.uomCode,
                              initialQty: unissued > 0 ? unissued : mat.requiredQty
                            })
                          }
                        >
                          {t("materials.stageIssue")}
                        </Button>
                        <Button
                          variant="secondary"
                          size="small"
                          onClick={() =>
                            setSelectedAction({
                              orderId: effectiveOrderId,
                              requirementId: mat.id,
                              action: "Consumed",
                              uomCode: mat.uomCode,
                              initialQty: unconsumed > 0 ? unconsumed : mat.issuedQty
                            })
                          }
                        >
                          {t("materials.directConsume")}
                        </Button>
                        {mat.issuedQty > 0 && (
                          <Button
                            variant="secondary"
                            size="small"
                            onClick={() =>
                              setSelectedAction({
                                orderId: effectiveOrderId,
                                requirementId: mat.id,
                                action: "Returned",
                                uomCode: mat.uomCode,
                                initialQty: unconsumed > 0 ? unconsumed : 1
                              })
                            }
                          >
                            {t("materials.returnMaterial")}
                          </Button>
                        )}
                      </div>
                    </td>
                  </tr>
                );
              })}
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
          initialQty={selectedAction.initialQty}
        />
      )}
    </div>
  );
}

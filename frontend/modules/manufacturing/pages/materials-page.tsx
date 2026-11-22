import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { ManufacturingHeader } from "../components/manufacturing-header.tsx";
import { ManufacturingStatusBadge } from "../components/manufacturing-status-badge.tsx";
import { NoLegalEntityPrompt } from "../components/no-legal-entity-prompt.tsx";
import { MaterialActionDialog } from "../features/orders/material-action-dialog.tsx";
import { useManufacturingScope } from "../hooks/use-manufacturing-scope.ts";
import { useOrdersQuery, useOrderMaterialsQuery } from "../hooks/use-manufacturing.ts";
import { useCatalogProducts } from "../hooks/use-catalog-products.ts";
import { Button } from "../../../components/primitives/button.tsx";
import { BdiText } from "../../../components/formatting/bidi-text.tsx";

export function MaterialsPage(): React.JSX.Element {
  const { t } = useTranslation("manufacturing");
  const { legalEntityId, isReady } = useManufacturingScope();
  const { getProductDisplayName, getProductSku } = useCatalogProducts();

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
        padding: "1rem",
        width: "100%",
        maxWidth: "1600px",
        margin: "0 auto",
        boxSizing: "border-box",
        overflowX: "hidden"
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
          flexWrap: "wrap",
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
            flex: "1 1 280px",
            maxWidth: "100%"
          }}
        >
          {activeOrders.length === 0 ? (
            <option value="">{t("orders.noActiveMaterials")}</option>
          ) : (
            activeOrders.map((o) => (
              <option key={o.id} value={o.id}>
                {o.orderNumber ?? o.number} ({t(`status.${o.state}`)}) - {o.orderedQty} {o.uomCode}
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
          <div style={{ overflowX: "auto", WebkitOverflowScrolling: "touch", width: "100%" }}>
            <table style={{ width: "100%", minWidth: "850px", borderCollapse: "collapse", fontSize: "13px", textAlign: "right" }}>
              <thead>
                <tr style={{ background: "#f8fafc", borderBottom: "1px solid #e2e8f0", color: "#475569" }}>
                  <th style={{ padding: "12px 14px" }}>{t("materials.component")}</th>
                  <th style={{ padding: "12px 14px" }}>{t("common.state")}</th>
                  <th style={{ padding: "12px 14px" }}>{t("materials.required")}</th>
                  <th style={{ padding: "12px 14px" }}>{t("materials.reserved")}</th>
                  <th style={{ padding: "12px 14px" }}>{t("materials.issued")}</th>
                  <th style={{ padding: "12px 14px" }}>{t("materials.consumed")}</th>
                  <th style={{ padding: "12px 14px" }}>{t("materials.shortageQty")}</th>
                  <th style={{ padding: "12px 14px", textAlign: "center" }}>{t("common.actions")}</th>
                </tr>
              </thead>
              <tbody>
                {materials.map((mat) => {
                  const variantId = mat.preferredProductVariantId || mat.productVariantId;
                  const productName = getProductDisplayName(variantId);
                  const sku = getProductSku(variantId);
                  const unreserved = Math.max(0, mat.requiredQty - mat.reservedQty);
                  const unissued = Math.max(0, mat.requiredQty - mat.issuedQty);
                  const unconsumed = Math.max(0, mat.issuedQty - mat.consumedQty);

                  return (
                    <tr key={mat.id} style={{ borderBottom: "1px solid #f1f5f9" }}>
                      <td style={{ padding: "12px 14px" }}>
                        <div style={{ display: "flex", flexDirection: "column", gap: "2px" }}>
                          <span style={{ fontWeight: 700, color: "#102a43", fontSize: "14px" }}>
                            {productName}
                          </span>
                          {sku ? (
                            <span style={{ fontSize: "11px", color: "#64748b" }}>
                              <BdiText>{`${t("common.sku")}: ${sku}`}</BdiText>
                            </span>
                          ) : productName !== variantId ? (
                            <span style={{ fontSize: "11px", color: "#94a3b8" }}>
                              <BdiText>{variantId}</BdiText>
                            </span>
                          ) : null}
                        </div>
                      </td>
                      <td style={{ padding: "12px 14px" }}>
                        <ManufacturingStatusBadge state={mat.state} />
                      </td>
                      <td style={{ padding: "12px 14px", fontWeight: 600 }}>
                        <BdiText>{`${mat.requiredQty} ${mat.uomCode}`}</BdiText>
                      </td>
                      <td style={{ padding: "12px 14px" }}>
                        <BdiText>{`${mat.reservedQty} ${mat.uomCode}`}</BdiText>
                      </td>
                      <td style={{ padding: "12px 14px", color: mat.issuedQty > 0 ? "#15803d" : undefined, fontWeight: mat.issuedQty > 0 ? 600 : 400 }}>
                        <BdiText>{`${mat.issuedQty} ${mat.uomCode}`}</BdiText>
                      </td>
                      <td style={{ padding: "12px 14px", color: mat.consumedQty > 0 ? "#0284c7" : undefined, fontWeight: mat.consumedQty > 0 ? 600 : 400 }}>
                        <BdiText>{`${mat.consumedQty} ${mat.uomCode}`}</BdiText>
                      </td>
                      <td style={{ padding: "12px 14px" }}>
                        {unissued > 0 ? (
                          <span style={{ color: "#d97706", fontWeight: 600, background: "#fef3c7", padding: "2px 6px", borderRadius: "4px" }}>
                            <BdiText>{`${unissued} ${mat.uomCode}`}</BdiText>
                          </span>
                        ) : (
                          <span style={{ color: "#15803d", fontWeight: 600 }}>
                            ✓ {t("materials.fullyAccounted")}
                          </span>
                        )}
                      </td>
                      <td style={{ padding: "12px 14px", textAlign: "center" }}>
                        <div style={{ display: "inline-flex", gap: "6px", alignItems: "center" }}>
                          {unissued > 0 ? (
                            <Button
                              variant="primary"
                              size="small"
                              onClick={() =>
                                setSelectedAction({
                                  orderId: effectiveOrderId,
                                  requirementId: mat.id,
                                  action: "Issued",
                                  uomCode: mat.uomCode,
                                  initialQty: unissued
                                })
                              }
                            >
                              {t("materials.stageIssue")}
                            </Button>
                          ) : unconsumed > 0 ? (
                            <Button
                              variant="primary"
                              size="small"
                              onClick={() =>
                                setSelectedAction({
                                  orderId: effectiveOrderId,
                                  requirementId: mat.id,
                                  action: "Consumed",
                                  uomCode: mat.uomCode,
                                  initialQty: unconsumed
                                })
                              }
                            >
                              {t("materials.directConsume")}
                            </Button>
                          ) : null}

                          <select
                            defaultValue=""
                            onChange={(e) => {
                              const act = e.target.value as "Reserved" | "Issued" | "Consumed" | "Returned";
                              if (!act) return;
                              const initQty = act === "Reserved" ? (unreserved > 0 ? unreserved : mat.requiredQty)
                                : act === "Issued" ? (unissued > 0 ? unissued : mat.requiredQty)
                                : act === "Consumed" ? (unconsumed > 0 ? unconsumed : mat.issuedQty)
                                : (unconsumed > 0 ? unconsumed : 1);
                              setSelectedAction({
                                orderId: effectiveOrderId,
                                requirementId: mat.id,
                                action: act,
                                uomCode: mat.uomCode,
                                initialQty: initQty
                              });
                              e.target.value = "";
                            }}
                            style={{
                              padding: "4px 8px",
                              fontSize: "12px",
                              borderRadius: "4px",
                              border: "1px solid #cbd5e1",
                              background: "#ffffff",
                              cursor: "pointer"
                            }}
                            aria-label={t("materials.actionsMenu")}
                          >
                            <option value="" disabled>{t("materials.actionsMenu")}</option>
                            <option value="Reserved">{t("materials.reserved")}</option>
                            <option value="Issued">{t("materials.stageIssue")}</option>
                            <option value="Consumed">{t("materials.directConsume")}</option>
                            {mat.issuedQty > 0 && <option value="Returned">{t("materials.returnMaterial")}</option>}
                          </select>
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
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

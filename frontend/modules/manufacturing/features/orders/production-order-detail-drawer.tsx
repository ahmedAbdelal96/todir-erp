import React from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router-dom";
import { Button } from "../../../../components/primitives/button.tsx";
import { BdiText } from "../../../../components/formatting/bidi-text.tsx";
import { ManufacturingStatusBadge } from "../../components/manufacturing-status-badge.tsx";
import {
  useMaterialsQuery,
  useOperationsQuery,
  useQualityGatesQuery,
  useCostQuery,
  useCloseReadinessQuery
} from "../../hooks/use-manufacturing.ts";
import { useCatalogProducts } from "../../hooks/use-catalog-products.ts";
import type { ProductionOrder } from "../../types.ts";

export interface ProductionOrderDetailDrawerProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly order: ProductionOrder | null;
  readonly onActionTrigger?: (order: ProductionOrder, action: "release" | "start" | "hold" | "resume" | "complete" | "close" | "cancel") => void;
}

export function ProductionOrderDetailDrawer({
  isOpen,
  onClose,
  order,
  onActionTrigger
}: ProductionOrderDetailDrawerProps): React.JSX.Element | null {
  const { t } = useTranslation("manufacturing");

  const orderId = order?.id ?? "";
  const { data: materials = [] } = useMaterialsQuery(orderId);
  const { data: operations = [] } = useOperationsQuery(orderId);
  const { data: qualityGates = [] } = useQualityGatesQuery(orderId);
  const { data: cost } = useCostQuery(orderId);
  const { data: closeReadiness } = useCloseReadinessQuery(orderId);

  const materialVariantIds = React.useMemo(
    () => materials.map((m) => m.preferredProductVariantId || m.productVariantId).filter(Boolean),
    [materials]
  );
  const { getProductDisplayName } = useCatalogProducts(materialVariantIds);

  if (!isOpen || !order) return null;

  return (
    <div
      role="dialog"
      aria-modal="true"
      style={{
        position: "fixed",
        inset: 0,
        zIndex: 1000,
        display: "flex",
        justifyContent: "flex-end",
        backgroundColor: "rgba(16, 42, 67, 0.4)",
        backdropFilter: "blur(2px)"
      }}
      onClick={onClose}
    >
      <aside
        style={{
          width: "100%",
          maxWidth: "600px",
          height: "100%",
          background: "var(--color-surface-card, #ffffff)",
          borderInlineStart: "1px solid var(--color-border-default, #cbd5e1)",
          boxShadow: "-4px 0 24px rgba(0, 0, 0, 0.15)",
          display: "flex",
          flexDirection: "column",
          boxSizing: "border-box"
        }}
        onClick={(e) => e.stopPropagation()}
      >
        {/* Header */}
        <header
          style={{
            padding: "1.25rem 1.5rem",
            borderBottom: "1px solid var(--color-border-default)",
            display: "flex",
            alignItems: "center",
            justifyContent: "space-between",
            background: "var(--color-surface-subtle, #f8fafc)"
          }}
        >
          <div>
            <div style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", fontWeight: 600 }}>
              {t("orders.title")}
            </div>
            <h2 style={{ margin: "0.25rem 0 0", fontSize: "1.25rem", color: "var(--color-brand-primary, #102a43)" }}>
              <BdiText>{order.orderNumber ?? order.number}</BdiText>
            </h2>
          </div>
          <button
            type="button"
            onClick={onClose}
            aria-label={t("common.close")}
            style={{
              background: "none",
              border: "none",
              fontSize: "1.5rem",
              cursor: "pointer",
              color: "var(--color-content-muted)"
            }}
          >
            ×
          </button>
        </header>

        {/* Content body */}
        <div style={{ flex: 1, overflowY: "auto", padding: "1.25rem 1.5rem", display: "flex", flexDirection: "column", gap: "1.25rem" }}>
          {/* Status Ribbon */}
          <div style={{ display: "flex", flexWrap: "wrap", gap: "0.5rem" }}>
            <ManufacturingStatusBadge state={order.state} />
            <ManufacturingStatusBadge state={order.materialStatus} />
            <ManufacturingStatusBadge state={order.operationStatus} />
            <ManufacturingStatusBadge state={order.qualityStatus} />
            <ManufacturingStatusBadge state={order.costStatus} />
          </div>

          {/* Key Facts */}
          <section style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem", padding: "0.875rem", background: "var(--color-surface-page, #f0f4f8)", borderRadius: "var(--radius-md)" }}>
            <div>
              <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>{t("orders.quantity")}:</span>
              <div style={{ fontWeight: 700 }}><BdiText>{`${order.orderedQty} ${order.uomCode}`}</BdiText></div>
            </div>
            <div>
              <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>{t("orders.completedQty")}:</span>
              <div style={{ fontWeight: 700, color: "#047857" }}><BdiText>{`${order.completedQty} ${order.uomCode}`}</BdiText></div>
            </div>
            <div>
              <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>{t("orders.scrapQty")}:</span>
              <div style={{ fontWeight: 700, color: order.scrapQty > 0 ? "#b91c1c" : "inherit" }}><BdiText>{`${order.scrapQty} ${order.uomCode}`}</BdiText></div>
            </div>
            <div>
              <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>{t("orders.needDate")}:</span>
              <div style={{ fontWeight: 700 }}><BdiText>{order.needDate?.slice(0, 10)}</BdiText></div>
            </div>
          </section>

          {/* Quick Actions */}
          {onActionTrigger && (
            <section>
              <h3 style={{ fontSize: "0.875rem", margin: "0 0 0.5rem", color: "var(--color-brand-primary)" }}>{t("common.actions")}</h3>
              <div style={{ display: "flex", flexWrap: "wrap", gap: "0.5rem" }}>
                {order.state === "Draft" ? (
                  <Button size="small" variant="primary" onPress={() => onActionTrigger(order, "release")}>
                    {t("orders.actions.release")}
                  </Button>
                ) : null}
                {order.state === "Released" ? (
                  <Button size="small" variant="primary" onPress={() => onActionTrigger(order, "start")}>
                    {t("orders.actions.start")}
                  </Button>
                ) : null}
                {order.state === "InProgress" ? (
                  <>
                    <Button size="small" variant="outline" onPress={() => onActionTrigger(order, "hold")}>
                      {t("orders.actions.hold")}
                    </Button>
                    <Button size="small" variant="primary" onPress={() => onActionTrigger(order, "complete")}>
                      {t("orders.actions.complete")}
                    </Button>
                  </>
                ) : null}
                {order.state === "OnHold" ? (
                  <Button size="small" variant="primary" onPress={() => onActionTrigger(order, "resume")}>
                    {t("orders.actions.resume")}
                  </Button>
                ) : null}
                {order.state === "Completed" ? (
                  <Button size="small" variant="primary" onPress={() => onActionTrigger(order, "close")}>
                    {t("orders.actions.close")}
                  </Button>
                ) : null}
                {order.state !== "Closed" && order.state !== "Cancelled" ? (
                  <Button size="small" variant="destructive" onPress={() => onActionTrigger(order, "cancel")}>
                    {t("orders.actions.cancel")}
                  </Button>
                ) : null}
              </div>
            </section>
          )}

          {/* Materials Summary */}
          <section>
            <h3 style={{ fontSize: "0.875rem", margin: "0 0 0.5rem", color: "var(--color-brand-primary)" }}>{t("workspace.materialsTab")} ({materials.length})</h3>
            <div style={{ fontSize: "0.8125rem", border: "1px solid var(--color-border-default)", borderRadius: "var(--radius-sm)", overflow: "hidden" }}>
              {materials.slice(0, 4).map((m) => (
                <div key={m.id} style={{ padding: "0.5rem 0.75rem", borderBottom: "1px solid var(--color-border-default)", display: "flex", justifyContent: "space-between" }}>
                  <span>{getProductDisplayName(m.preferredProductVariantId || m.componentProductVariantId || m.productVariantId)}</span>
                  <span><BdiText>{`${m.consumedQty} / ${m.requiredQty} ${m.uomCode}`}</BdiText></span>
                </div>
              ))}
            </div>
          </section>

          {/* Operations Summary */}
          <section>
            <h3 style={{ fontSize: "0.875rem", margin: "0 0 0.5rem", color: "var(--color-brand-primary)" }}>{t("workspace.operationsTab")} ({operations.length})</h3>
            <div style={{ fontSize: "0.8125rem", border: "1px solid var(--color-border-default)", borderRadius: "var(--radius-sm)", overflow: "hidden" }}>
              {operations.map((op) => (
                <div key={op.id} style={{ padding: "0.5rem 0.75rem", borderBottom: "1px solid var(--color-border-default)", display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                  <span>{op.sequenceNo}. {op.name}</span>
                  <ManufacturingStatusBadge state={op.state} />
                </div>
              ))}
            </div>
          </section>

          {/* Close Readiness Blocker Notice if exists */}
          {closeReadiness && !(closeReadiness.canClose ?? (closeReadiness.state === "Ready" || closeReadiness.state === "Reconciled")) ? (
            <div role="alert" style={{ padding: "0.75rem", background: "#fef2f2", border: "1px solid #ef4444", borderRadius: "var(--radius-sm)", color: "#b91c1c", fontSize: "0.8125rem" }}>
              <strong>{t("costing.closeBlockersTitle")}:</strong> {t("costing.unpostedLedgerWarning")}
            </div>
          ) : null}
        </div>

        {/* Footer */}
        <footer
          style={{
            padding: "1rem 1.5rem",
            borderTop: "1px solid var(--color-border-default)",
            display: "flex",
            justifyContent: "space-between",
            background: "var(--color-surface-subtle)"
          }}
        >
          <Button variant="outline" onPress={onClose}>
            {t("common.close")}
          </Button>
          <Link
            to={`/manufacturing/orders/${order.id}`}
            style={{
              display: "inline-flex",
              alignItems: "center",
              padding: "0.5rem 1rem",
              background: "var(--color-brand-primary, #102a43)",
              color: "#ffffff",
              borderRadius: "var(--radius-sm)",
              textDecoration: "none",
              fontWeight: 600,
              fontSize: "0.875rem"
            }}
          >
            {t("orders.actions.inspect")}
          </Link>
        </footer>
      </aside>
    </div>
  );
}

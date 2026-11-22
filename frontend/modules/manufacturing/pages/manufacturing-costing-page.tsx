import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { ManufacturingHeader } from "../components/manufacturing-header.tsx";
import { ManufacturingStatusBadge } from "../components/manufacturing-status-badge.tsx";
import { NoLegalEntityPrompt } from "../components/no-legal-entity-prompt.tsx";
import { RecordCostFactDialog } from "../features/costing/record-cost-fact-dialog.tsx";
import { CloseReadinessPanel } from "../features/orders/close-readiness-panel.tsx";
import { useManufacturingScope } from "../hooks/use-manufacturing-scope.ts";
import { useOrdersQuery, useCostingSummaryQuery } from "../hooks/use-manufacturing.ts";
import { Button } from "../../../components/primitives/button.tsx";
import { BdiText } from "../../../components/formatting/bidi-text.tsx";

export function ManufacturingCostingPage(): React.JSX.Element {
  const { t } = useTranslation("manufacturing");
  const { legalEntityId, isReady } = useManufacturingScope();

  const { data: orders = [], isLoading: isOrdersLoading } = useOrdersQuery(legalEntityId);
  const { data: costingSummary, isLoading: isSummaryLoading, isError: isSummaryError } = useCostingSummaryQuery(legalEntityId);
  const [selectedOrderIdForCost, setSelectedOrderIdForCost] = useState<string | null>(null);
  const [inspectedOrderId, setInspectedOrderId] = useState<string | null>(null);

  if (!isReady) {
    return <NoLegalEntityPrompt />;
  }

  return (
    <div
      data-testid="manufacturing-costing-page"
      style={{
        padding: "1.25rem 1.5rem",
        width: "100%",
        maxWidth: "1600px",
        margin: "0 auto",
        boxSizing: "border-box"
      }}
    >
      <ManufacturingHeader
        title={t("costing.title", "تكاليف الإنتاج وإقفال الـ WIP")}
        subtitle={t("costing.subtitle", "تحليل عناصر التكلفة المعيارية والفعلية، وفروق التكاليف، وإقفال حسابات التشغيل")}
      />

      {/* Overview Cards */}
      <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(260px, 1fr))", gap: "16px", marginBottom: "16px" }}>
        <div style={{ background: "#ffffff", border: "1px solid #cbd5e1", borderRadius: "8px", padding: "16px", boxShadow: "0 1px 3px rgba(0,0,0,0.05)" }}>
          <div style={{ fontSize: "12px", color: "#64748b", fontWeight: 600 }}>{t("costing.operationalWipEstimate", "رصيد الـ WIP التشغيلي التقديري")}</div>
          <div data-testid="kpi-wip-balance" style={{ fontSize: "20px", fontWeight: 700, color: "#102a43", marginTop: "4px" }}>
            <BdiText>
              {costingSummary
                ? `${costingSummary.totalWipAmount.toLocaleString()} ${costingSummary.currencyCode}`
                : isSummaryLoading
                ? t("common.loading", "جاري التحميل...")
                : "—"}
            </BdiText>
          </div>
          <div style={{ fontSize: "12px", color: "#64748b", marginTop: "4px" }}>
            {costingSummary ? (
              <>
                <span>{costingSummary.accumulatingOrdersCount} {t("costing.ordersCount", "أمر")} — {t("costing.accumulatingOrders", "أوامر قيد تجميع التكاليف")}</span>
                <div style={{ fontSize: "11px", color: "#94a3b8", marginTop: "2px" }}>
                  {t("costing.operationalWipNote", "حساب تشغيلي تقديري (غير مطابق دفترياً حتى اكتمال قيود الأستاذ العام)")}
                </div>
              </>
            ) : isSummaryError ? (
              <span style={{ color: "#dc2626" }}>{t("costing.wipUnavailable", "غير متاح حالياً / تعذر جلب رصيد الـ WIP")}</span>
            ) : null}
          </div>
        </div>

        <div style={{ background: "#ffffff", border: "1px solid #cbd5e1", borderRadius: "8px", padding: "16px", boxShadow: "0 1px 3px rgba(0,0,0,0.05)" }}>
          <div style={{ fontSize: "12px", color: "#64748b", fontWeight: 600 }}>{t("costing.eligibleForClose", "الأوامر المؤهلة للإقفال المالي")}</div>
          <div data-testid="kpi-eligible-close" style={{ fontSize: "20px", fontWeight: 700, color: "#15803d", marginTop: "4px" }}>
            {costingSummary?.eligibleForCloseOrdersCount ?? 0} {t("costing.ordersCount", "أمر")}
          </div>
          <div style={{ fontSize: "12px", color: "#15803d", marginTop: "4px" }}>
            {t("costing.reconciliationSuccess", "مطابقة محاسبية مكتملة")}
          </div>
        </div>

        <div style={{ background: "#ffffff", border: "1px solid #cbd5e1", borderRadius: "8px", padding: "16px", boxShadow: "0 1px 3px rgba(0,0,0,0.05)" }}>
          <div style={{ fontSize: "12px", color: "#64748b", fontWeight: 600 }}>{t("costing.completedBlocked", "أوامر مكتملة محظورة الإقفال")}</div>
          <div data-testid="kpi-completed-blocked" style={{ fontSize: "20px", fontWeight: 700, color: "#b45309", marginTop: "4px" }}>
            {costingSummary?.completedBlockedOrdersCount ?? 0} {t("costing.ordersCount", "أمر")}
          </div>
          <div style={{ fontSize: "12px", color: "#b45309", marginTop: "4px" }}>
            {t("costing.unpostedLedgerWarning", "تتطلب تسوية خامات أو قيود")}
          </div>
        </div>

        <div style={{ background: "#ffffff", border: "1px solid #cbd5e1", borderRadius: "8px", padding: "16px", boxShadow: "0 1px 3px rgba(0,0,0,0.05)" }}>
          <div style={{ fontSize: "12px", color: "#64748b", fontWeight: 600 }}>{t("costing.closedOrders", "أوامر مقفلة ومسواة بالكامل")}</div>
          <div data-testid="kpi-closed-orders" style={{ fontSize: "20px", fontWeight: 700, color: "#475569", marginTop: "4px" }}>
            {costingSummary?.closedReconciledOrdersCount ?? 0} {t("costing.ordersCount", "أمر")}
          </div>
          <div style={{ fontSize: "12px", color: "#64748b", marginTop: "4px" }}>
            {t("costing.activeAndDistributed", "نشط وموزع")}
          </div>
        </div>
      </div>

      {inspectedOrderId && (
        <div style={{ marginBottom: "16px" }}>
          <CloseReadinessPanel orderId={inspectedOrderId} />
        </div>
      )}

      {/* Orders Costing Table */}
      <div style={{ background: "#ffffff", border: "1px solid #cbd5e1", borderRadius: "8px", padding: "16px" }}>
        <h3 style={{ margin: "0 0 12px", fontSize: "15px", fontWeight: 700, color: "#102a43" }}>
          {t("costing.orderCostStatus", "حالة تكاليف أوامر الإنتاج")}
        </h3>

        {orders.length === 0 ? (
          <div style={{ padding: "32px 0", textAlign: "center", color: "#64748b" }}>
            {t("costing.noOrders", "لا توجد أوامر إنتاج مسجلة.")}
          </div>
        ) : (
          <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "14px", textAlign: "right" }}>
            <thead>
              <tr style={{ background: "#f8fafc", borderBottom: "1px solid #e2e8f0", color: "#475569" }}>
                <th style={{ padding: "12px 16px" }}>{t("costing.productionOrder", "أمر الإنتاج")}</th>
                <th style={{ padding: "12px 16px" }}>{t("costing.orderedQty", "الكمية المطلوبة")}</th>
                <th style={{ padding: "12px 16px" }}>{t("costing.completedQty", "المنجز التام")}</th>
                <th style={{ padding: "12px 16px" }}>{t("costing.costStatus", "حالة التكاليف")}</th>
                <th style={{ padding: "12px 16px" }}>{t("costing.orderState", "حالة الأمر")}</th>
                <th style={{ padding: "12px 16px", textAlign: "center" }}>{t("costing.actionRecordCost", "إجراء قيد تكلفة")}</th>
              </tr>
            </thead>
            <tbody>
              {orders.map((order) => (
                <tr
                  key={order.id}
                  style={{
                    borderBottom: "1px solid #f1f5f9",
                    cursor: "pointer",
                    background: inspectedOrderId === order.id ? "#f0fdf4" : undefined
                  }}
                  onClick={() => setInspectedOrderId(inspectedOrderId === order.id ? null : order.id)}
                >
                  <td style={{ padding: "12px 16px", fontWeight: 700, color: "#0284c7" }}>
                    {order.orderNumber ?? order.number}
                  </td>
                  <td style={{ padding: "12px 16px" }}>
                    {order.orderedQty} {order.uomCode}
                  </td>
                  <td style={{ padding: "12px 16px", color: "#15803d" }}>
                    {order.completedQty} {order.uomCode}
                  </td>
                  <td style={{ padding: "12px 16px" }}>
                    <ManufacturingStatusBadge state={order.costStatus} />
                  </td>
                  <td style={{ padding: "12px 16px" }}>
                    <ManufacturingStatusBadge state={order.state} />
                  </td>
                  <td style={{ padding: "12px 16px", textAlign: "center" }}>
                    <Button
                      variant="secondary"
                      onClick={(e) => {
                        e.stopPropagation();
                        setSelectedOrderIdForCost(order.id);
                      }}
                    >
                      + {t("costing.recordCostFact", "قيد تكلفة")}
                    </Button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {selectedOrderIdForCost && (
        <RecordCostFactDialog
          isOpen={Boolean(selectedOrderIdForCost)}
          onClose={() => setSelectedOrderIdForCost(null)}
          orderId={selectedOrderIdForCost}
        />
      )}
    </div>
  );
}

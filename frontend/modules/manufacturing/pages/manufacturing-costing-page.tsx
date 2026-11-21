import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { useOutletContext } from "react-router-dom";
import type { WorkspaceOutletContext } from "../../../app/router/route-contract.ts";
import { ManufacturingHeader } from "../components/manufacturing-header.tsx";
import { ManufacturingStatusBadge } from "../components/manufacturing-status-badge.tsx";
import { RecordCostFactDialog } from "../features/costing/record-cost-fact-dialog.tsx";
import { useOrdersQuery } from "../hooks/use-manufacturing.ts";
import { Button } from "../../../components/primitives/button.tsx";

export function ManufacturingCostingPage(): React.JSX.Element {
  const { t } = useTranslation("manufacturing");
  const outletCtx = useOutletContext<WorkspaceOutletContext | undefined>();
  const legalEntityId = outletCtx?.legalEntityId ?? "00000000-0000-0000-0000-000000000001";

  const { data: orders = [] } = useOrdersQuery(legalEntityId);
  const [selectedOrderIdForCost, setSelectedOrderIdForCost] = useState<string | null>(null);

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
        <div style={{ background: "#ffffff", border: "1px solid #cbd5e1", borderRadius: "8px", padding: "16px" }}>
          <div style={{ fontSize: "12px", color: "#64748b" }}>{t("costing.wipBalance", "رصيد التشغيل تحت التنفيذ (WIP)")}</div>
          <div style={{ fontSize: "20px", fontWeight: 700, color: "#102a43", marginTop: "4px" }}>
            نشط وموزع
          </div>
        </div>

        <div style={{ background: "#ffffff", border: "1px solid #cbd5e1", borderRadius: "8px", padding: "16px" }}>
          <div style={{ fontSize: "12px", color: "#64748b" }}>الأوامر المؤهلة للإقفال المالي</div>
          <div style={{ fontSize: "20px", fontWeight: 700, color: "#15803d", marginTop: "4px" }}>
            {orders.filter((o) => o.state === "Completed").length} أمر
          </div>
        </div>

        <div style={{ background: "#ffffff", border: "1px solid #cbd5e1", borderRadius: "8px", padding: "16px" }}>
          <div style={{ fontSize: "12px", color: "#64748b" }}>أوامر مقفلة ومسواة بالكامل</div>
          <div style={{ fontSize: "20px", fontWeight: 700, color: "#475569", marginTop: "4px" }}>
            {orders.filter((o) => o.state === "Closed").length} أمر
          </div>
        </div>
      </div>

      {/* Orders Costing Table */}
      <div style={{ background: "#ffffff", border: "1px solid #cbd5e1", borderRadius: "8px", padding: "16px" }}>
        <h3 style={{ margin: "0 0 12px", fontSize: "15px", fontWeight: 700, color: "#102a43" }}>
          حالة تكاليف أوامر الإنتاج
        </h3>

        {orders.length === 0 ? (
          <div style={{ padding: "32px 0", textAlign: "center", color: "#64748b" }}>
            لا توجد أوامر إنتاج مسجلة.
          </div>
        ) : (
          <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "14px", textAlign: "right" }}>
            <thead>
              <tr style={{ background: "#f8fafc", borderBottom: "1px solid #e2e8f0", color: "#475569" }}>
                <th style={{ padding: "12px 16px" }}>أمر الإنتاج</th>
                <th style={{ padding: "12px 16px" }}>الكمية المطلوبة</th>
                <th style={{ padding: "12px 16px" }}>المنجز التام</th>
                <th style={{ padding: "12px 16px" }}>حالة التكاليف</th>
                <th style={{ padding: "12px 16px" }}>حالة الأمر</th>
                <th style={{ padding: "12px 16px", textAlign: "center" }}>إجراء قيد تكلفة</th>
              </tr>
            </thead>
            <tbody>
              {orders.map((order) => (
                <tr key={order.id} style={{ borderBottom: "1px solid #f1f5f9" }}>
                  <td style={{ padding: "12px 16px", fontWeight: 700, color: "#0284c7" }}>
                    {order.orderNumber}
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
                      onClick={() => setSelectedOrderIdForCost(order.id)}
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

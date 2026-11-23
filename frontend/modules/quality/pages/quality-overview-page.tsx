import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router-dom";
import { QualityHeader } from "../components/quality-header.tsx";
import { QualityKpiRibbon } from "../components/quality-kpi-ribbon.tsx";
import { QualityStatusBadge } from "../components/quality-status-badge.tsx";
import { QualityNoScopePrompt } from "../components/quality-no-scope-prompt.tsx";
import { useQualityScope } from "../hooks/use-quality-scope.ts";
import {
  useQualityInspections,
  useQualityRestrictions,
  useQualityNcrs
} from "../hooks/use-quality-queries.ts";
import { Button } from "../../../components/primitives/button.tsx";
import { Surface } from "../../../components/pages/surface.tsx";
import { CreateInspectionModal } from "../components/inspection-modals.tsx";
import { CreateRestrictionModal } from "../components/hold-disposition-modals.tsx";
import { CreateNcrModal } from "../components/ncr-capa-modals.tsx";

export function QualityOverviewPage(): React.JSX.Element {
  const { t } = useTranslation("quality");
  const { legalEntityId, isReady } = useQualityScope();

  const [isInspectionOpen, setIsInspectionOpen] = useState(false);
  const [isRestrictionOpen, setIsRestrictionOpen] = useState(false);
  const [isNcrOpen, setIsNcrOpen] = useState(false);

  const { data: inspectionsPage, isLoading: inspectionsLoading } = useQualityInspections(legalEntityId);
  const { data: restrictionsPage, isLoading: restrictionsLoading } = useQualityRestrictions(legalEntityId);
  const { data: ncrsPage, isLoading: ncrsLoading } = useQualityNcrs(legalEntityId);

  if (!isReady) {
    return <QualityNoScopePrompt />;
  }

  const inspections = inspectionsPage?.items ?? [];
  const restrictions = restrictionsPage?.items ?? [];
  const ncrs = ncrsPage?.items ?? [];

  const pendingInspections = inspections.filter((i) => i.state === "Pending" || i.state === "InProgress");
  const failedInspections = inspections.filter((i) => i.state === "Failed");
  const activeHolds = restrictions.filter((r) => r.status === "Active");
  const openNcrs = ncrs.filter((n) => n.state !== "Closed");

  return (
    <div data-testid="quality-overview-page" style={{ padding: "0 0 2rem 0" }}>
      <QualityHeader
        title={t("navigation.overview", "لوحة التحكم والجودة")}
        subtitle={t("tabs.overview", "لوحة التحكم وأولويات العمل")}
        primaryAction={
          <Button variant="primary" onPress={() => setIsInspectionOpen(true)}>
            + {t("actions.createInspection", "إنشاء أمر فحص جديد")}
          </Button>
        }
        secondaryActions={
          <div style={{ display: "flex", gap: "8px" }}>
            <Button variant="outline" onPress={() => setIsRestrictionOpen(true)}>
              + {t("actions.createRestriction", "حظر واحتجاز الدفعة")}
            </Button>
            <Button variant="outline" onPress={() => setIsNcrOpen(true)}>
              + {t("actions.createNcr", "فتح تقرير عدم مطابقة (NCR)")}
            </Button>
          </div>
        }
      />

      {/* KPI Ribbon adhering to TODIR 3-headline KPI rule */}
      <QualityKpiRibbon
        pendingInspectionsCount={pendingInspections.length}
        activeHoldsCount={activeHolds.length}
        openIssuesNcrsCount={openNcrs.length}
      />

      {/* Main Grid: Priority Queues */}
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1.25rem", marginTop: "1.25rem" }}>
        {/* Left Column: Urgent Pending Inspections */}
        <Surface style={{ padding: "1.25rem", border: "1px solid #cbd5e1", borderRadius: "8px" }}>
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "1rem" }}>
            <h2 style={{ margin: 0, fontSize: "1.125rem", color: "#102a43", fontWeight: 700 }}>
              📋 {t("kpi.pendingInspections", "فحوصات بانتظار التنفيذ")} ({pendingInspections.length})
            </h2>
            <Link to="/quality/inspections" style={{ fontSize: "0.875rem", color: "#0284c7", textDecoration: "none", fontWeight: 600 }}>
              {t("actions.viewDetails", "عرض التفاصيل")} →
            </Link>
          </div>

          {inspectionsLoading ? (
            <div style={{ padding: "1.5rem", textAlign: "center", color: "#64748b" }}>{t("messages.loading", "جاري تحميل البيانات...")}</div>
          ) : pendingInspections.length === 0 ? (
            <div style={{ padding: "1.5rem", textAlign: "center", color: "#64748b" }}>{t("messages.noData", "لا توجد سجلات")}</div>
          ) : (
            <div style={{ overflowX: "auto" }}>
              <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "0.875rem" }}>
                <thead>
                  <tr style={{ borderBottom: "2px solid #e2e8f0", textAlign: "right", color: "#475569" }}>
                    <th style={{ padding: "8px 12px" }}>{t("fields.eventType", "نوع الحدث")}</th>
                    <th style={{ padding: "8px 12px" }}>{t("fields.lotReference", "رقم التشغيلة")}</th>
                    <th style={{ padding: "8px 12px" }}>{t("fields.lotQuantity", "الكمية")}</th>
                    <th style={{ padding: "8px 12px" }}>{t("fields.status", "الحالة")}</th>
                    <th style={{ padding: "8px 12px" }}>{t("actions.viewDetails", "الإجراء")}</th>
                  </tr>
                </thead>
                <tbody>
                  {pendingInspections.slice(0, 5).map((insp) => (
                    <tr key={insp.id} style={{ borderBottom: "1px solid #f1f5f9" }}>
                      <td style={{ padding: "8px 12px", fontWeight: 600, color: "#102a43" }}>{insp.eventType}</td>
                      <td style={{ padding: "8px 12px", color: "#475569" }}>{insp.number || "-"}</td>
                      <td style={{ padding: "8px 12px" }}>{insp.lotQuantity}</td>
                      <td style={{ padding: "8px 12px" }}>
                        <QualityStatusBadge status={insp.state} />
                      </td>
                      <td style={{ padding: "8px 12px" }}>
                        <Link
                          to={`/quality/inspections/${insp.id}`}
                          style={{
                            padding: "4px 8px",
                            borderRadius: "4px",
                            backgroundColor: "#f0f9ff",
                            color: "#0369a1",
                            textDecoration: "none",
                            fontWeight: 600,
                            fontSize: "0.75rem"
                          }}
                        >
                          {t("actions.openWorkspace", "فتح ورشة الفحص")}
                        </Link>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </Surface>

        {/* Right Column: Active Material Holds */}
        <Surface style={{ padding: "1.25rem", border: "1px solid #cbd5e1", borderRadius: "8px" }}>
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "1rem" }}>
            <h2 style={{ margin: 0, fontSize: "1.125rem", color: "#102a43", fontWeight: 700 }}>
              🛑 {t("kpi.activeHolds", "شحنات محتجزة ومحظورة")} ({activeHolds.length})
            </h2>
            <Link to="/quality/restrictions" style={{ fontSize: "0.875rem", color: "#0284c7", textDecoration: "none", fontWeight: 600 }}>
              {t("actions.viewDetails", "عرض التفاصيل")} →
            </Link>
          </div>

          {restrictionsLoading ? (
            <div style={{ padding: "1.5rem", textAlign: "center", color: "#64748b" }}>{t("messages.loading", "جاري تحميل البيانات...")}</div>
          ) : activeHolds.length === 0 ? (
            <div style={{ padding: "1.5rem", textAlign: "center", color: "#64748b" }}>{t("messages.noData", "لا توجد سجلات")}</div>
          ) : (
            <div style={{ overflowX: "auto" }}>
              <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "0.875rem" }}>
                <thead>
                  <tr style={{ borderBottom: "2px solid #e2e8f0", textAlign: "right", color: "#475569" }}>
                    <th style={{ padding: "8px 12px" }}>{t("fields.holdReason", "السبب")}</th>
                    <th style={{ padding: "8px 12px" }}>{t("fields.initialQuantity", "المحتجز")}</th>
                    <th style={{ padding: "8px 12px" }}>{t("fields.remainingQuantity", "المتبقي")}</th>
                    <th style={{ padding: "8px 12px" }}>{t("fields.status", "الحالة")}</th>
                  </tr>
                </thead>
                <tbody>
                  {activeHolds.slice(0, 5).map((hold) => (
                    <tr key={hold.id} style={{ borderBottom: "1px solid #f1f5f9" }}>
                      <td style={{ padding: "8px 12px", fontWeight: 600, color: "#102a43" }}>{hold.reasonCode}</td>
                      <td style={{ padding: "8px 12px" }}>{hold.quantity}</td>
                      <td style={{ padding: "8px 12px", fontWeight: 700, color: "#b91c1c" }}>
                        {Math.max(0, (hold.quantity ?? 0) - hold.releasedQuantity)}
                      </td>
                      <td style={{ padding: "8px 12px" }}>
                        <QualityStatusBadge status={hold.status} />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </Surface>
      </div>

      {/* Modals */}
      <CreateInspectionModal
        legalEntityId={legalEntityId}
        isOpen={isInspectionOpen}
        onClose={() => setIsInspectionOpen(false)}
      />

      <CreateRestrictionModal
        legalEntityId={legalEntityId}
        isOpen={isRestrictionOpen}
        onClose={() => setIsRestrictionOpen(false)}
      />

      <CreateNcrModal
        legalEntityId={legalEntityId}
        isOpen={isNcrOpen}
        onClose={() => setIsNcrOpen(false)}
      />
    </div>
  );
}

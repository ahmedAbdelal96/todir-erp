import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router-dom";
import { QualityHeader } from "../components/quality-header.tsx";
import { QualityStatusBadge } from "../components/quality-status-badge.tsx";
import { QualityNoScopePrompt } from "../components/quality-no-scope-prompt.tsx";
import { useQualityScope } from "../hooks/use-quality-scope.ts";
import { useQualityInspections } from "../hooks/use-quality-queries.ts";
import { Button } from "../../../components/primitives/button.tsx";
import { Select } from "../../../components/primitives/select.tsx";
import { Surface } from "../../../components/pages/surface.tsx";
import { CreateInspectionModal } from "../components/inspection-modals.tsx";

export function QualityInspectionsPage(): React.JSX.Element {
  const { t } = useTranslation("quality");
  const { legalEntityId, isReady } = useQualityScope();

  const [stateFilter, setStateFilter] = useState<string>("ALL");
  const [eventFilter, setEventFilter] = useState<string>("ALL");
  const [isCreateOpen, setIsCreateOpen] = useState(false);

  const { data: pageData, isLoading } = useQualityInspections(
    legalEntityId,
    stateFilter === "ALL" ? null : stateFilter,
    eventFilter === "ALL" ? null : eventFilter
  );

  if (!isReady) {
    return <QualityNoScopePrompt />;
  }

  const inspections = pageData?.items ?? [];

  return (
    <div data-testid="quality-inspections-page" style={{ padding: "0 0 2rem 0" }}>
      <QualityHeader
        title={t("navigation.inspections", "فحوصات الجودة والتشغيل")}
        subtitle={t("tabs.inspections", "فحوصات الجودة")}
        primaryAction={
          <Button variant="primary" onPress={() => setIsCreateOpen(true)}>
            + {t("actions.createInspection", "إنشاء أمر فحص جديد")}
          </Button>
        }
      />

      {/* Filter Bar */}
      <Surface style={{ padding: "1rem 1.25rem", marginBottom: "1.25rem", border: "1px solid #cbd5e1", borderRadius: "8px" }}>
        <div style={{ display: "flex", gap: "1rem", flexWrap: "wrap", alignItems: "flex-end" }}>
          <div style={{ minWidth: "180px" }}>
            <Select
              label={t("fields.status", "الحالة")}
              selectedKey={stateFilter}
              onSelectionChange={(key) => setStateFilter(String(key))}
              options={[
                { id: "ALL", label: "جميع الحالات" },
                { id: "Pending", label: t("status.Pending", "قيد الانتظار") },
                { id: "InProgress", label: t("status.InProgress", "قيد التنفيذ") },
                { id: "Passed", label: t("status.Passed", "مطابق ومقبول") },
                { id: "Failed", label: t("status.Failed", "غير مطابق") }
              ]}
            />
          </div>

          <div style={{ minWidth: "180px" }}>
            <Select
              label={t("fields.eventType", "نوع الحدث")}
              selectedKey={eventFilter}
              onSelectionChange={(key) => setEventFilter(String(key))}
              options={[
                { id: "ALL", label: "جميع المراحل" },
                { id: "Receiving", label: t("eventTypes.Receiving", "استلام بضاعة") },
                { id: "InProcess", label: t("eventTypes.InProcess", "أثناء التشغيل") },
                { id: "Final", label: t("eventTypes.Final", "فحص نهائي") }
              ]}
            />
          </div>

          <Button
            variant="outline"
            onPress={() => {
              setStateFilter("ALL");
              setEventFilter("ALL");
            }}
          >
            {t("actions.clearFilters", "إعادة ضبط")}
          </Button>
        </div>
      </Surface>

      {/* Inspections Table */}
      <Surface style={{ padding: "1.25rem", border: "1px solid #cbd5e1", borderRadius: "8px" }}>
        {isLoading ? (
          <div style={{ padding: "2.5rem", textAlign: "center", color: "#64748b" }}>{t("messages.loading", "جاري تحميل البيانات...")}</div>
        ) : inspections.length === 0 ? (
          <div style={{ padding: "2.5rem", textAlign: "center", color: "#64748b" }}>{t("messages.noData", "لا توجد سجلات مطابقة")}</div>
        ) : (
          <div style={{ overflowX: "auto" }}>
            <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "0.875rem" }}>
              <thead>
                <tr style={{ borderBottom: "2px solid #e2e8f0", textAlign: "right", color: "#475569" }}>
                  <th style={{ padding: "10px 12px" }}>{t("fields.eventType", "المرحلة")}</th>
                  <th style={{ padding: "10px 12px" }}>{t("fields.lotReference", "رقم التشغيلة")}</th>
                  <th style={{ padding: "10px 12px" }}>{t("fields.lotQuantity", "كمية الدفعة")}</th>
                  <th style={{ padding: "10px 12px" }}>{t("fields.sampleSize", "حجم العينة")}</th>
                  <th style={{ padding: "10px 12px" }}>{t("fields.status", "الحالة")}</th>
                  <th style={{ padding: "10px 12px" }}>{t("fields.evaluation", "التقييم")}</th>
                  <th style={{ padding: "10px 12px" }}>{t("actions.viewDetails", "ورشة العمل")}</th>
                </tr>
              </thead>
              <tbody>
                {inspections.map((insp) => (
                  <tr key={insp.id} style={{ borderBottom: "1px solid #f1f5f9" }}>
                    <td style={{ padding: "10px 12px", fontWeight: 700, color: "#102a43" }}>{insp.eventType}</td>
                    <td style={{ padding: "10px 12px", color: "#475569" }}>{insp.number || "-"}</td>
                    <td style={{ padding: "10px 12px" }}>{insp.lotQuantity}</td>
                    <td style={{ padding: "10px 12px" }}>{insp.sampleSize}</td>
                    <td style={{ padding: "10px 12px" }}>
                      <QualityStatusBadge status={insp.state} />
                    </td>
                    <td style={{ padding: "10px 12px" }}>
                      {insp.decisionState ? <QualityStatusBadge status={insp.decisionState} /> : <span style={{ color: "#94a3b8" }}>-</span>}
                    </td>
                    <td style={{ padding: "10px 12px" }}>
                      <Link
                        to={`/quality/inspections/${insp.id}`}
                        data-testid={`inspection-workspace-link-${insp.id}`}
                        style={{
                          display: "inline-flex",
                          alignItems: "center",
                          padding: "6px 12px",
                          borderRadius: "4px",
                          backgroundColor: "#0284c7",
                          color: "#ffffff",
                          textDecoration: "none",
                          fontWeight: 600,
                          fontSize: "0.8125rem"
                        }}
                      >
                        {t("actions.openWorkspace", "فتح ورشة الفحص")} →
                      </Link>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Surface>

      <CreateInspectionModal
        legalEntityId={legalEntityId}
        isOpen={isCreateOpen}
        onClose={() => setIsCreateOpen(false)}
      />
    </div>
  );
}

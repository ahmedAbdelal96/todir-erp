import React from "react";
import { useTranslation } from "react-i18next";

export interface QualityKpiRibbonProps {
  pendingInspectionsCount: number;
  activeHoldsCount: number;
  openIssuesNcrsCount: number;
  onFilterPendingInspections?: () => void;
  onFilterActiveHolds?: () => void;
  onFilterOpenNcrs?: () => void;
}

export const QualityKpiRibbon: React.FC<QualityKpiRibbonProps> = ({
  pendingInspectionsCount,
  activeHoldsCount,
  openIssuesNcrsCount,
  onFilterPendingInspections,
  onFilterActiveHolds,
  onFilterOpenNcrs
}) => {
  const { t } = useTranslation();

  return (
    <div
      data-testid="quality-kpi-ribbon"
      style={{
        display: "grid",
        gridTemplateColumns: "repeat(auto-fit, minmax(240px, 1fr))",
        gap: "12px",
        marginBottom: "16px"
      }}
    >
      {/* KPI 1: Pending Inspections */}
      <div
        onClick={onFilterPendingInspections}
        role={onFilterPendingInspections ? "button" : undefined}
        tabIndex={onFilterPendingInspections ? 0 : undefined}
        data-testid="kpi-pending-inspections"
        style={{
          backgroundColor: "#ffffff",
          border: "1px solid #cbd5e1",
          borderInlineStart: "4px solid #0284c7",
          borderRadius: "6px",
          padding: "10px 14px",
          boxShadow: "0 1px 3px rgba(0,0,0,0.06)",
          cursor: onFilterPendingInspections ? "pointer" : "default"
        }}
      >
        <div style={{ fontSize: "12px", color: "#64748b", fontWeight: 500, marginBottom: "4px" }}>
          {t("quality:kpi.pendingInspections", "فحوصات بانتظار التنفيذ")}
        </div>
        <div style={{ fontSize: "22px", fontWeight: 700, color: "#0f172a", lineHeight: 1.2 }}>
          {pendingInspectionsCount}
        </div>
        <div style={{ fontSize: "11px", color: "#94a3b8", marginTop: "2px" }}>
          {t("quality:messages.emptyQueueDescription", "تشمل الاستلام، المرحلي، والتام")}
        </div>
      </div>

      {/* KPI 2: Active Quality Holds */}
      <div
        onClick={onFilterActiveHolds}
        role={onFilterActiveHolds ? "button" : undefined}
        tabIndex={onFilterActiveHolds ? 0 : undefined}
        data-testid="kpi-active-holds"
        style={{
          backgroundColor: "#ffffff",
          border: "1px solid #cbd5e1",
          borderInlineStart: "4px solid #d97706",
          borderRadius: "6px",
          padding: "10px 14px",
          boxShadow: "0 1px 3px rgba(0,0,0,0.06)",
          cursor: onFilterActiveHolds ? "pointer" : "default"
        }}
      >
        <div style={{ fontSize: "12px", color: "#64748b", fontWeight: 500, marginBottom: "4px" }}>
          {t("quality:kpi.activeHolds", "شحنات محتجزة ومحظورة")}
        </div>
        <div style={{ fontSize: "22px", fontWeight: 700, color: "#d97706", lineHeight: 1.2 }}>
          {activeHoldsCount}
        </div>
        <div style={{ fontSize: "11px", color: "#94a3b8", marginTop: "2px" }}>
          {t("quality:messages.inspectionPassedNotice", "محظورة من الصرف حتى الإفراج")}
        </div>
      </div>

      {/* KPI 3: Open NCRs / Pending Actions */}
      <div
        onClick={onFilterOpenNcrs}
        role={onFilterOpenNcrs ? "button" : undefined}
        tabIndex={onFilterOpenNcrs ? 0 : undefined}
        data-testid="kpi-open-ncrs"
        style={{
          backgroundColor: "#ffffff",
          border: "1px solid #cbd5e1",
          borderInlineStart: "4px solid #dc2626",
          borderRadius: "6px",
          padding: "10px 14px",
          boxShadow: "0 1px 3px rgba(0,0,0,0.06)",
          cursor: onFilterOpenNcrs ? "pointer" : "default"
        }}
      >
        <div style={{ fontSize: "12px", color: "#64748b", fontWeight: 500, marginBottom: "4px" }}>
          {t("quality:kpi.openNcrs", "حالات عدم مطابقة وقرارات معلقة")}
        </div>
        <div style={{ fontSize: "22px", fontWeight: 700, color: "#dc2626", lineHeight: 1.2 }}>
          {openIssuesNcrsCount}
        </div>
        <div style={{ fontSize: "11px", color: "#94a3b8", marginTop: "2px" }}>
          {t("quality:status.ActionPending", "تتطلب إجراءات احتواء أو تصرف")}
        </div>
      </div>
    </div>
  );
};

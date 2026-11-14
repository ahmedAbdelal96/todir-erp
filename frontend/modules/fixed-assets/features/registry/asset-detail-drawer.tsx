import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { QuickPeekDrawer } from "../../../../components/primitives/quick-peek-drawer.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { AssetLifecycleBadge } from "../../components/asset-lifecycle-badge.tsx";
import {
  useFixedAssetDetailQuery,
  useAssetCustodyHistoryQuery,
  useAssetDepreciationScheduleQuery
} from "../../hooks/use-fixed-assets-queries.ts";
import {
  BuildingIcon,
  BookOpenIcon,
  HistoryIcon,
  TagIcon,
  ArrowRightLeftIcon,
  CornerDownLeftIcon,
  TrashIcon,
  RotateCcwIcon,
  CalculatorIcon,
  EditIcon
} from "../../components/fixed-assets-icons.tsx";

export type AssetDetailTab =
  | "overview"
  | "books"
  | "custody"
  | "schedule"
  | "documents"
  | "timeline"
  | "sources";

export interface AssetDetailDrawerProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly assetId: string | null;
  readonly onTransferClick?: () => void;
  readonly onReturnClick?: () => void;
  readonly onDisposeClick?: () => void;
  readonly onReinstateClick?: () => void;
  readonly onEstimateChangeClick?: () => void;
  readonly onAddCostClick?: () => void;
  readonly onImpairmentClick?: () => void;
}

export function AssetDetailDrawer({
  isOpen,
  onClose,
  legalEntityId,
  assetId,
  onTransferClick,
  onReturnClick,
  onDisposeClick,
  onReinstateClick,
  onEstimateChangeClick,
  onAddCostClick,
  onImpairmentClick
}: AssetDetailDrawerProps): React.JSX.Element {
  const { t } = useTranslation("fixedAssets");
  const [activeTab, setActiveTab] = useState<AssetDetailTab>("overview");

  const assetQuery = useFixedAssetDetailQuery(legalEntityId, assetId ?? "");
  const custodyQuery = useAssetCustodyHistoryQuery(legalEntityId, assetId ?? "");
  const scheduleQuery = useAssetDepreciationScheduleQuery(
    assetId ?? undefined,
    "book-corp",
    legalEntityId,
    "2026-01-01",
    "2030-12-31"
  );

  const asset = assetQuery.data;

  const tabs: { id: AssetDetailTab; label: string; icon: React.ReactNode }[] = [
    { id: "overview", label: t("drawer.tabOverview", "نظرة عامة"), icon: <BuildingIcon size={16} /> },
    { id: "books", label: t("drawer.tabBooks", "الدفاتر والإهلاك"), icon: <BookOpenIcon size={16} /> },
    { id: "custody", label: t("drawer.tabCustody", "سجل العهد والمواقع"), icon: <ArrowRightLeftIcon size={16} /> },
    { id: "schedule", label: t("drawer.tabSchedule", "جدول الإهلاك المستقبلي"), icon: <CalculatorIcon size={16} /> },
    { id: "documents", label: t("drawer.tabDocuments", "المستندات والوثائق"), icon: <TagIcon size={16} /> },
    { id: "timeline", label: t("drawer.tabTimeline", "سجل الحركات (Audit Log)"), icon: <HistoryIcon size={16} /> },
    { id: "sources", label: t("drawer.tabSources", "مصادر التكلفة والرسملة"), icon: <EditIcon size={16} /> }
  ];

  return (
    <QuickPeekDrawer
      isOpen={isOpen}
      onClose={onClose}
      title={asset ? `${asset.assetName} (${asset.assetCode})` : t("drawer.loading", "تفاصيل الأصل")}
      width="720px"
    >
      <div data-testid="asset-detail-drawer" style={{ display: "flex", flexDirection: "column", height: "100%", gap: "1rem" }}>
        {/* Top Header Card */}
        {asset && (
          <div
            style={{
              padding: "1rem",
              backgroundColor: "#ffffff",
              border: "1px solid #cbd5e1",
              borderRadius: "0.5rem",
              display: "grid",
              gridTemplateColumns: "1fr 1fr",
              gap: "1rem"
            }}
          >
            <div>
              <div style={{ display: "flex", alignItems: "center", gap: "0.5rem", marginBottom: "0.375rem" }}>
                <AssetLifecycleBadge state={asset.lifecycleState} />
                <span style={{ fontSize: "0.8125rem", color: "#64748b" }}>{asset.categoryName}</span>
              </div>
              <div style={{ fontSize: "1.125rem", fontWeight: 700, color: "#0f172a" }}>
                {asset.assetName}
              </div>
              <div style={{ fontSize: "0.8125rem", color: "#64748b", marginTop: "0.25rem" }}>
                {asset.assetCode} | {asset.serialNumber ? `${t("fields.serialNumberShort", "ر.ت:")} ${asset.serialNumber}` : ""}
              </div>
            </div>

            <div style={{ textAlign: "end", display: "flex", flexDirection: "column", justifyContent: "center" }}>
              <div style={{ fontSize: "0.8125rem", color: "#64748b" }}>{t("fields.netBookValue", "صافي القيمة الدفترية")}</div>
              <div style={{ fontSize: "1.375rem", fontWeight: 700, color: "#1e3a8a" }}>
                {(asset.netBookValue ?? 0).toLocaleString("ar-EG", { minimumFractionDigits: 2 })} {asset.currency}
              </div>
              <div style={{ fontSize: "0.75rem", color: "#64748b", marginTop: "0.15rem" }}>
                {t("fields.acquisitionCost", "التكلفة التاريخية")}: {(asset.acquisitionCost ?? 0).toLocaleString("ar-EG", { minimumFractionDigits: 2 })} {asset.currency}
              </div>
            </div>
          </div>
        )}

        {/* Tab Navigation */}
        <div
          style={{
            display: "flex",
            gap: "0.5rem",
            borderBottom: "1px solid #cbd5e1",
            paddingBottom: "0.5rem",
            overflowX: "auto"
          }}
        >
          {tabs.map((tab) => {
            const isActive = activeTab === tab.id;
            return (
              <button
                key={tab.id}
                type="button"
                data-testid={`drawer-tab-${tab.id}`}
                onClick={() => setActiveTab(tab.id)}
                style={{
                  display: "flex",
                  alignItems: "center",
                  gap: "0.375rem",
                  padding: "0.5rem 0.75rem",
                  fontSize: "0.8125rem",
                  fontWeight: isActive ? 600 : 500,
                  color: isActive ? "#1e3a8a" : "#64748b",
                  backgroundColor: isActive ? "#eff6ff" : "transparent",
                  border: "none",
                  borderBottom: isActive ? "2px solid #1e3a8a" : "2px solid transparent",
                  borderRadius: "0.25rem 0.25rem 0 0",
                  cursor: "pointer",
                  whiteSpace: "nowrap"
                }}
              >
                {tab.icon}
                <span>{tab.label}</span>
              </button>
            );
          })}
        </div>

        {/* Tab Content Panes */}
        {assetQuery.isLoading ? (
          <div style={{ padding: "2rem", textAlign: "center", color: "#64748b" }}>
            {t("common.loading", "جاري التحميل...")}
          </div>
        ) : !asset ? (
          <div style={{ padding: "2rem", textAlign: "center", color: "#64748b" }}>
            {t("common.noData", "لا توجد بيانات متاحة")}
          </div>
        ) : (
          <div style={{ flex: 1, overflowY: "auto" }}>
            {/* 1. Overview Tab */}
            {activeTab === "overview" && (
              <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }} data-testid="drawer-pane-overview">
                <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
                  <div style={{ padding: "0.75rem", backgroundColor: "#f8fafc", borderRadius: "0.375rem" }}>
                    <div style={{ fontSize: "0.75rem", color: "#64748b" }}>{t("fields.acquisitionDate", "تاريخ الاقتناء")}</div>
                    <div style={{ fontWeight: 600, color: "#1e293b", marginTop: "0.25rem" }}>{asset.acquisitionDate}</div>
                  </div>
                  <div style={{ padding: "0.75rem", backgroundColor: "#f8fafc", borderRadius: "0.375rem" }}>
                    <div style={{ fontSize: "0.75rem", color: "#64748b" }}>{t("fields.inServiceDate", "تاريخ بدء التشغيل")}</div>
                    <div style={{ fontWeight: 600, color: "#1e293b", marginTop: "0.25rem" }}>{asset.inServiceDate ?? "-"}</div>
                  </div>
                  <div style={{ padding: "0.75rem", backgroundColor: "#f8fafc", borderRadius: "0.375rem" }}>
                    <div style={{ fontSize: "0.75rem", color: "#64748b" }}>{t("fields.custodian", "المسؤول عن العهدة")}</div>
                    <div style={{ fontWeight: 600, color: "#1e293b", marginTop: "0.25rem" }}>{asset.custodianId ?? t("common.unassigned", "غير مسند")}</div>
                  </div>
                  <div style={{ padding: "0.75rem", backgroundColor: "#f8fafc", borderRadius: "0.375rem" }}>
                    <div style={{ fontSize: "0.75rem", color: "#64748b" }}>{t("fields.department", "القسم / المركز المالي")}</div>
                    <div style={{ fontWeight: 600, color: "#1e293b", marginTop: "0.25rem" }}>{asset.department ?? "-"}</div>
                  </div>
                  <div style={{ padding: "0.75rem", backgroundColor: "#f8fafc", borderRadius: "0.375rem" }}>
                    <div style={{ fontSize: "0.75rem", color: "#64748b" }}>{t("fields.location", "الموقع الجغرافي")}</div>
                    <div style={{ fontWeight: 600, color: "#1e293b", marginTop: "0.25rem" }}>{asset.locationName ?? "-"}</div>
                  </div>
                  <div style={{ padding: "0.75rem", backgroundColor: "#f8fafc", borderRadius: "0.375rem" }}>
                    <div style={{ fontSize: "0.75rem", color: "#64748b" }}>{t("fields.barcode", "الباركود / رمز التتبع")}</div>
                    <div style={{ fontWeight: 600, color: "#1e293b", marginTop: "0.25rem" }}>{asset.barcode ?? "-"}</div>
                  </div>
                </div>

                {asset.description && (
                  <div style={{ padding: "0.75rem", backgroundColor: "#f8fafc", borderRadius: "0.375rem" }}>
                    <div style={{ fontSize: "0.75rem", color: "#64748b" }}>{t("fields.description", "الوصف")}</div>
                    <div style={{ fontSize: "0.875rem", color: "#334155", marginTop: "0.25rem" }}>{asset.description}</div>
                  </div>
                )}
              </div>
            )}

            {/* 2. Books Tab */}
            {activeTab === "books" && (
              <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }} data-testid="drawer-pane-books">
                <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem" }}>
                  <Button variant="secondary" size="compact" onClick={onEstimateChangeClick} data-testid="drawer-estimate-btn">
                    {t("books.changeEstimate", "تعديل التقدير (عمر / خردة)")}
                  </Button>
                  <Button variant="secondary" size="compact" onClick={onAddCostClick} data-testid="drawer-add-cost-btn">
                    {t("books.addCost", "إضافة تكلفة رأسمالية")}
                  </Button>
                  <Button variant="secondary" size="compact" onClick={onImpairmentClick} data-testid="drawer-impairment-btn">
                    {t("books.proposeImpairment", "تسجيل اضمحلال")}
                  </Button>
                </div>

                <div
                  style={{
                    padding: "1rem",
                    backgroundColor: "#f8fafc",
                    border: "1px solid #cbd5e1",
                    borderRadius: "0.5rem"
                  }}
                >
                  <h4 style={{ margin: "0 0 0.75rem", fontSize: "0.9375rem", fontWeight: 600, color: "#0f172a" }}>
                    {t("books.corporateBookHeading", "دفتر الشركات المحاسبي (Corporate Book)")}
                  </h4>
                  <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr", gap: "1rem" }}>
                    <div>
                      <div style={{ fontSize: "0.75rem", color: "#64748b" }}>{t("fields.depreciationMethod", "طريقة الإهلاك")}</div>
                      <div style={{ fontWeight: 600, color: "#1e293b", marginTop: "0.25rem" }}>{asset.depreciationMethod}</div>
                    </div>
                    <div>
                      <div style={{ fontSize: "0.75rem", color: "#64748b" }}>{t("fields.usefulLifeMonths", "العمر الإنتاجي")}</div>
                      <div style={{ fontWeight: 600, color: "#1e293b", marginTop: "0.25rem" }}>{asset.usefulLifeMonths} {t("common.months", "شهر")}</div>
                    </div>
                    <div>
                      <div style={{ fontSize: "0.75rem", color: "#64748b" }}>{t("fields.salvageValue", "قيمة الخردة")}</div>
                      <div style={{ fontWeight: 600, color: "#1e293b", marginTop: "0.25rem" }}>{(asset.salvageValue ?? 0).toLocaleString("ar-EG")} {asset.currency}</div>
                    </div>
                    <div>
                      <div style={{ fontSize: "0.75rem", color: "#64748b" }}>{t("fields.accumulatedDepreciation", "مجمع الإهلاك")}</div>
                      <div style={{ fontWeight: 600, color: "#dc2626", marginTop: "0.25rem" }}>{(asset.accumulatedDepreciation ?? 0).toLocaleString("ar-EG")} {asset.currency}</div>
                    </div>
                    <div>
                      <div style={{ fontSize: "0.75rem", color: "#64748b" }}>{t("fields.netBookValue", "صافي القيمة الدفترية")}</div>
                      <div style={{ fontWeight: 600, color: "#16a34a", marginTop: "0.25rem" }}>{(asset.netBookValue ?? 0).toLocaleString("ar-EG")} {asset.currency}</div>
                    </div>
                    <div>
                      <div style={{ fontSize: "0.75rem", color: "#64748b" }}>{t("fields.monthlyDepreciation", "القسط الشهري التقديري")}</div>
                      <div style={{ fontWeight: 600, color: "#1e293b", marginTop: "0.25rem" }}>
                        {(asset.usefulLifeMonths ?? 0) > 0
                          ? (((asset.acquisitionCost ?? 0) - (asset.salvageValue ?? 0)) / (asset.usefulLifeMonths ?? 1)).toLocaleString("ar-EG", { minimumFractionDigits: 2 })
                          : "0.00"} {asset.currency}
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            )}

            {/* 3. Custody History Tab */}
            {activeTab === "custody" && (
              <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }} data-testid="drawer-pane-custody">
                {custodyQuery.isLoading ? (
                  <div style={{ textAlign: "center", padding: "1rem", color: "#64748b" }}>{t("common.loading", "جاري التحميل...")}</div>
                ) : (custodyQuery.data?.items?.length ?? 0) === 0 ? (
                  <div style={{ textAlign: "center", padding: "2rem", color: "#64748b" }}>
                    {t("custody.noHistory", "لا توجد حركات تسليم أو نقل عهدة سابقة")}
                  </div>
                ) : (
                  <div style={{ border: "1px solid #cbd5e1", borderRadius: "0.375rem", overflow: "hidden" }}>
                    <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "0.8125rem" }}>
                      <thead>
                        <tr style={{ backgroundColor: "#f8fafc", borderBottom: "1px solid #cbd5e1", textAlign: "start" }}>
                          <th style={{ padding: "0.5rem" }}>{t("custody.actionType", "نوع الحركة")}</th>
                          <th style={{ padding: "0.5rem" }}>{t("custody.custodian", "الموظف")}</th>
                          <th style={{ padding: "0.5rem" }}>{t("custody.location", "الموقع")}</th>
                          <th style={{ padding: "0.5rem" }}>{t("custody.date", "التاريخ")}</th>
                          <th style={{ padding: "0.5rem" }}>{t("custody.notes", "الملاحظات")}</th>
                        </tr>
                      </thead>
                      <tbody>
                        {custodyQuery.data?.items?.map((item) => (
                          <tr key={item.id} style={{ borderBottom: "1px solid #f1f5f9" }}>
                            <td style={{ padding: "0.5rem", fontWeight: 600 }}>{item.actionType}</td>
                            <td style={{ padding: "0.5rem" }}>{item.custodianId ?? "-"}</td>
                            <td style={{ padding: "0.5rem" }}>{item.locationName ?? "-"}</td>
                            <td style={{ padding: "0.5rem" }}>{new Date(item.actionDate).toLocaleDateString("ar-EG")}</td>
                            <td style={{ padding: "0.5rem", color: "#64748b" }}>{item.notes ?? "-"}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}
              </div>
            )}

            {/* 4. Schedule Tab */}
            {activeTab === "schedule" && (
              <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }} data-testid="drawer-pane-schedule">
                <div style={{ fontSize: "0.8125rem", color: "#475569" }}>
                  {t("depreciation.scheduleDesc", "جدول الإهلاك التقديري للفترات القادمة وحتى نهاية العمر الإنتاجي:")}
                </div>
                {scheduleQuery.isLoading ? (
                  <div style={{ textAlign: "center", padding: "1rem", color: "#64748b" }}>{t("common.loading", "جاري التحميل...")}</div>
                ) : (scheduleQuery.data?.length ?? 0) === 0 ? (
                  <div style={{ textAlign: "center", padding: "2rem", color: "#64748b" }}>
                    {t("depreciation.noSchedule", "الأصل لا يخضع لجدول إهلاك (أراضي أو أصل مستبعد)")}
                  </div>
                ) : (
                  <div style={{ border: "1px solid #cbd5e1", borderRadius: "0.375rem", overflow: "hidden", maxHeight: "350px", overflowY: "auto" }}>
                    <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "0.8125rem" }}>
                      <thead>
                        <tr style={{ backgroundColor: "#f8fafc", borderBottom: "1px solid #cbd5e1", textAlign: "start", position: "sticky", top: 0 }}>
                          <th style={{ padding: "0.5rem" }}>{t("depreciation.period", "الفترة")}</th>
                          <th style={{ padding: "0.5rem" }}>{t("depreciation.deprAmount", "قسط الإهلاك")}</th>
                          <th style={{ padding: "0.5rem" }}>{t("depreciation.accDepr", "مجمع الإهلاك")}</th>
                          <th style={{ padding: "0.5rem" }}>{t("depreciation.closingNbv", "القيمة الدفترية المتبقية")}</th>
                        </tr>
                      </thead>
                      <tbody>
                        {scheduleQuery.data?.map((line, idx) => (
                          <tr key={idx} style={{ borderBottom: "1px solid #f1f5f9" }}>
                            <td style={{ padding: "0.5rem", fontWeight: 600 }}>{line.periodEnd}</td>
                            <td style={{ padding: "0.5rem" }}>{line.depreciationAmount.toLocaleString("ar-EG", { minimumFractionDigits: 2 })}</td>
                            <td style={{ padding: "0.5rem", color: "#dc2626" }}>{line.cumulativeDepreciation.toLocaleString("ar-EG", { minimumFractionDigits: 2 })}</td>
                            <td style={{ padding: "0.5rem", color: "#16a34a" }}>{line.projectedNetBookValue.toLocaleString("ar-EG", { minimumFractionDigits: 2 })}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}
              </div>
            )}

            {/* 5. Documents Tab */}
            {activeTab === "documents" && (
              <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }} data-testid="drawer-pane-documents">
                <div style={{ padding: "0.75rem", backgroundColor: "#f8fafc", borderRadius: "0.375rem", border: "1px solid #cbd5e1" }}>
                  <div style={{ fontWeight: 600, fontSize: "0.875rem", marginBottom: "0.5rem" }}>
                    {t("fields.purchaseDocHeading", "بيانات الشراء والضمان")}
                  </div>
                  <div style={{ fontSize: "0.8125rem", color: "#475569", display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.5rem" }}>
                    <div><strong>{t("fields.vendor", "المورد")}:</strong> {asset.manufacturer ?? "-"}</div>
                    <div><strong>{t("fields.model", "الموديل")}:</strong> {asset.modelNumber ?? "-"}</div>
                    <div><strong>{t("fields.warrantyExpiry", "انتهاء الضمان")}:</strong> {asset.warrantyExpiryDate ?? t("common.none", "غير محدد")}</div>
                    <div><strong>{t("fields.serial", "السيريال")}:</strong> {asset.serialNumber ?? "-"}</div>
                  </div>
                </div>

                {asset.customAttributes && (
                  <div style={{ padding: "0.75rem", backgroundColor: "#f8fafc", borderRadius: "0.375rem", border: "1px solid #cbd5e1" }}>
                    <div style={{ fontWeight: 600, fontSize: "0.875rem", marginBottom: "0.5rem" }}>
                      {t("fields.customAttributes", "بيانات الملكية العقارية / المجمعة")}
                    </div>
                    <pre style={{ margin: 0, fontSize: "0.75rem", color: "#334155", overflowX: "auto" }}>
                      {JSON.stringify(asset.customAttributes, null, 2)}
                    </pre>
                  </div>
                )}
              </div>
            )}

            {/* 6. Timeline Tab */}
            {activeTab === "timeline" && (
              <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }} data-testid="drawer-pane-timeline">
                <div style={{ fontSize: "0.8125rem", color: "#475569" }}>
                  {t("timeline.auditLogNotice", "سجل التغييرات المعتمدة والأحداث المحاسبية الموثقة:")}
                </div>
                <div style={{ display: "flex", flexDirection: "column", gap: "0.75rem" }}>
                  <div style={{ padding: "0.75rem", backgroundColor: "#f8fafc", borderRadius: "0.375rem", borderInlineStart: "3px solid #1e3a8a" }}>
                    <div style={{ fontWeight: 600, fontSize: "0.8125rem" }}>{t("timeline.acquisitionCreated", "تسجيل وقيد الأصل الرأسمالي")}</div>
                    <div style={{ fontSize: "0.75rem", color: "#64748b" }}>{asset.acquisitionDate} {t("timeline.bySystem", "- بواسطة النظام المالي")}</div>
                  </div>
                  {asset.inServiceDate && (
                    <div style={{ padding: "0.75rem", backgroundColor: "#f8fafc", borderRadius: "0.375rem", borderInlineStart: "3px solid #16a34a" }}>
                      <div style={{ fontWeight: 600, fontSize: "0.8125rem" }}>{t("timeline.inServiceStarted", "دخول الخدمة وبدء الإهلاك")}</div>
                      <div style={{ fontSize: "0.75rem", color: "#64748b" }}>{asset.inServiceDate}</div>
                    </div>
                  )}
                  {asset.lifecycleState === "Disposed" && (
                    <div style={{ padding: "0.75rem", backgroundColor: "#fef2f2", borderRadius: "0.375rem", borderInlineStart: "3px solid #dc2626" }}>
                      <div style={{ fontWeight: 600, fontSize: "0.8125rem", color: "#991b1b" }}>{t("timeline.disposed", "استبعاد وتخريد الأصل")}</div>
                      <div style={{ fontSize: "0.75rem", color: "#64748b" }}>{t("timeline.closedAccounts", "تم إقفال حسابات الأصل ومجمع الإهلاك")}</div>
                    </div>
                  )}
                </div>
              </div>
            )}

            {/* 7. Cost Source Drilldown Tab */}
            {activeTab === "sources" && (
              <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }} data-testid="drawer-pane-sources">
                <div style={{ padding: "0.75rem", backgroundColor: "#f8fafc", borderRadius: "0.375rem", border: "1px solid #cbd5e1" }}>
                  <div style={{ fontWeight: 600, fontSize: "0.875rem", marginBottom: "0.5rem" }}>
                    {t("sources.heading", "أصل التكلفة الرأسمالية (Capitalization Trace)")}
                  </div>
                  <p style={{ margin: 0, fontSize: "0.8125rem", lineHeight: 1.6, color: "#334155" }}>
                    {t("sources.explanation", "تمت رسملة هذا الأصل بتكلفة أولية قدرها ")}
                    <strong>{(asset.acquisitionCost ?? 0).toLocaleString("ar-EG")} {asset.currency}</strong>
                    {t("sources.explanationEnd", " مطابقة لقيود الشراء أو المشاريع الرأسمالية المرتبطة.")}
                  </p>
                </div>
              </div>
            )}
          </div>
        )}

        {/* Bottom Action Footer */}
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", width: "100%", borderTop: "1px solid #cbd5e1", paddingTop: "0.75rem" }}>
          <div style={{ display: "flex", gap: "0.5rem" }}>
            {asset?.lifecycleState === "Disposed" ? (
              <Button variant="secondary" size="compact" onClick={onReinstateClick} data-testid="drawer-reinstate-btn">
                <RotateCcwIcon size={14} style={{ marginInlineEnd: "0.375rem" }} />
                {t("disposal.reinstate", "إعادة قيد")}
              </Button>
            ) : (
              <>
                <Button variant="secondary" size="compact" onClick={onTransferClick} data-testid="drawer-transfer-btn">
                  <ArrowRightLeftIcon size={14} style={{ marginInlineEnd: "0.375rem" }} />
                  {t("custody.transfer", "نقل العهدة")}
                </Button>
                <Button variant="secondary" size="compact" onClick={onReturnClick} data-testid="drawer-return-btn">
                  <CornerDownLeftIcon size={14} style={{ marginInlineEnd: "0.375rem" }} />
                  {t("custody.return", "إرجاع للمستودع")}
                </Button>
                <Button variant="danger" size="compact" onClick={onDisposeClick} data-testid="drawer-dispose-btn">
                  <TrashIcon size={14} style={{ marginInlineEnd: "0.375rem" }} />
                  {t("disposal.dispose", "استبعاد / بيع")}
                </Button>
              </>
            )}
          </div>
          <Button variant="secondary" size="compact" onClick={onClose} data-testid="close-detail-drawer-btn">
            {t("common.close", "إغلاق")}
          </Button>
        </div>
      </div>
    </QuickPeekDrawer>
  );
}

import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import {
  useCrmForecastQuery,
  useCreateForecastSnapshotMutation,
  useCrmReportsSummaryQuery,
  useCrmPipelineInspectionQuery,
  useCrmTeamsQuery,
  useCrmTeamMembersQuery,
  useCrmPipelinesQuery,
  useCrmCustomFieldsQuery,
  useCrmAuditQuery,
} from "../../hooks/use-crm-queries.ts";
import { Pipeline, Team } from "../../types.ts";
import { DealHealthBadge } from "../../components/deal-health-badge.tsx";
import { ForecastCategoryBadge } from "../../components/forecast-category-badge.tsx";
import { CrmKpiCard } from "../../components/crm-kpi-card.tsx";
import { NewPipelineModal } from "./new-pipeline-modal.tsx";
import { NewStageModal } from "./new-stage-modal.tsx";
import { NewTeamModal } from "./new-team-modal.tsx";
import { AddTeamMemberModal } from "./add-team-member-modal.tsx";
import { NewCustomFieldModal } from "./new-custom-field-modal.tsx";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { Table, TableHeader, TableRow, TableHead, TableBody, TableCell } from "../../../../components/primitives/table.tsx";
import {
  BarChart3,
  Search,
  Users,
  GitBranch,
  Sliders,
  History,
  Camera,
  CheckCircle,
  ChevronRight,
  TrendingUp,
} from "../../components/crm-icons.tsx";

interface CrmManagementTabProps {
  readonly legalEntityId: string;
}

export const CrmManagementTab: React.FC<CrmManagementTabProps> = ({ legalEntityId }) => {
  const { t, i18n } = useTranslation("crm");

  // Sub-tabs
  const [subTab, setSubTab] = useState<"forecast" | "inspection" | "teams" | "pipelines" | "settings">("forecast");

  // Selection states
  const [selectedTeam, setSelectedTeam] = useState<Team | null>(null);
  const [selectedPipeline, setSelectedPipeline] = useState<Pipeline | null>(null);

  // Modals
  const [isNewPipelineOpen, setIsNewPipelineOpen] = useState(false);
  const [isNewStageOpen, setIsNewStageOpen] = useState(false);
  const [isNewTeamOpen, setIsNewTeamOpen] = useState(false);
  const [isAddMemberOpen, setIsAddMemberOpen] = useState(false);
  const [isNewCustomFieldOpen, setIsNewCustomFieldOpen] = useState(false);

  // Queries
  const { data: forecastData } = useCrmForecastQuery(legalEntityId);
  const { data: reportsSummary } = useCrmReportsSummaryQuery(legalEntityId);
  const { data: inspectionDeals, isLoading: loadingInspection } = useCrmPipelineInspectionQuery(legalEntityId);
  const { data: teams, isLoading: loadingTeams } = useCrmTeamsQuery(legalEntityId);
  const { data: pipelines, isLoading: loadingPipelines } = useCrmPipelinesQuery(legalEntityId);
  const { data: customFields, isLoading: loadingCustomFields } = useCrmCustomFieldsQuery(legalEntityId);
  const { data: auditLogs, isLoading: loadingAudit } = useCrmAuditQuery(legalEntityId);

  // Team members for selected team
  const activeTeamId = selectedTeam?.id || teams?.[0]?.id;
  const { data: teamMembers, isLoading: loadingMembers } = useCrmTeamMembersQuery(legalEntityId, activeTeamId);

  // Pipeline stages for selected pipeline
  const activePipeline = selectedPipeline || pipelines?.[0];

  // Snapshot mutation
  const snapshotMutation = useCreateForecastSnapshotMutation(legalEntityId);

  const formatCurrency = (val: number) => {
    return new Intl.NumberFormat(i18n.language, {
      style: "currency",
      currency: "EGP",
      maximumFractionDigits: 0,
    }).format(val || 0);
  };

  const handleTakeSnapshot = () => {
    snapshotMutation.mutate({
      asOfDate: new Date().toISOString().slice(0, 10),
      currencyCode: "EGP",
    });
  };

  const subNavItems = [
    { id: "forecast", label: t("crm.mgmt.tabs.forecast", "توقعات المبيعات والتقارير"), icon: <BarChart3 style={{ width: "1rem", height: "1rem" }} /> },
    { id: "inspection", label: t("crm.mgmt.tabs.inspection", "فحص المسار البيعي"), icon: <Search style={{ width: "1rem", height: "1rem" }} /> },
    { id: "teams", label: t("crm.mgmt.tabs.teams", "فرق المبيعات"), icon: <Users style={{ width: "1rem", height: "1rem" }} /> },
    { id: "pipelines", label: t("crm.mgmt.tabs.pipelines", "المسارات والمراحل"), icon: <GitBranch style={{ width: "1rem", height: "1rem" }} /> },
    { id: "settings", label: t("crm.mgmt.tabs.settings", "الحقول المخصصة وسجل العمليات"), icon: <Sliders style={{ width: "1rem", height: "1rem" }} /> },
  ] as const;

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
      {/* Management Sub-Navigation Bar */}
      <Surface padding="none" variant="card">
        <nav
          style={{
            display: "flex",
            gap: "0.5rem",
            overflowX: "auto",
            padding: "0.5rem 0.75rem",
            borderBottom: "1px solid var(--color-border-default)"
          }}
          aria-label={t("crm.mgmt.tabsLabel", "تبويبات الإدارة")}
        >
          {subNavItems.map((item) => {
            const isActive = subTab === item.id;
            return (
              <button
                key={item.id}
                type="button"
                onClick={() => setSubTab(item.id)}
                style={{
                  display: "inline-flex",
                  alignItems: "center",
                  gap: "0.5rem",
                  padding: "0.5rem 0.875rem",
                  borderRadius: "var(--radius-md)",
                  border: "none",
                  backgroundColor: isActive ? "var(--color-surface-subtle, #f1f5f9)" : "transparent",
                  color: isActive ? "var(--color-brand-primary, #102a43)" : "var(--color-content-muted, #64748b)",
                  fontWeight: isActive ? 700 : 500,
                  fontSize: "0.8125rem",
                  cursor: "pointer",
                  whiteSpace: "nowrap",
                  transition: "all 0.15s ease"
                }}
              >
                {item.icon}
                <span>{item.label}</span>
              </button>
            );
          })}
        </nav>
      </Surface>

      {/* SUB-TAB 1: FORECAST & REPORTS */}
      {subTab === "forecast" && (
        <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
          {/* Top Actions: Take Snapshot */}
          <Surface padding="normal" variant="card">
            <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", flexWrap: "wrap", gap: "1rem" }}>
              <div>
                <h2 style={{ margin: 0, fontSize: "0.9375rem", fontWeight: 700, color: "var(--color-content-primary, #0f172a)" }}>
                  {t("crm.forecast.headerTitle", "التوقعات البيعية الموزونة للفترة الحالية")}
                </h2>
                <p style={{ margin: "0.25rem 0 0", fontSize: "0.8125rem", color: "var(--color-content-muted, #64748b)" }}>
                  {t("crm.forecast.headerSubtitle", "حساب المبيعات المتوقعة وفقاً لاحتمالية نجاح كل مرحلة في المسار")}
                </p>
              </div>
              <Button
                variant="secondary"
                size="comfortable"
                onPress={handleTakeSnapshot}
                isDisabled={snapshotMutation.isPending}
              >
                <Camera style={{ width: "1rem", height: "1rem", marginInlineEnd: "0.375rem" }} />
                {snapshotMutation.isPending ? t("crm.common.saving", "جاري الحفظ...") : t("crm.forecast.takeSnapshot", "حفظ لقطة للتوقعات")}
              </Button>
            </div>
          </Surface>

          {/* Forecast Categories Cards */}
          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))", gap: "1rem" }}>
            <CrmKpiCard
              title={t("crm.forecast.catPipeline", "في المسار (Pipeline)")}
              value={<bdi>{formatCurrency(forecastData?.pipelineAmount || 0)}</bdi>}
              subtitle={`${t("crm.forecast.weighted", "القيمة الموزونة:")} ${formatCurrency(forecastData?.weightedAmount || 0)}`}
              tone="primary"
            />
            <CrmKpiCard
              title={t("crm.forecast.catBestCase", "أفضل سيناريو (Best Case)")}
              value={<bdi>{formatCurrency(forecastData?.bestCaseAmount || 0)}</bdi>}
              subtitle={t("forecastCategories.BestCase", "أفضل سيناريو")}
              tone="warning"
            />
            <CrmKpiCard
              title={t("crm.forecast.catCommit", "مؤكدة للإغلاق (Commit)")}
              value={<bdi>{formatCurrency(forecastData?.commitAmount || 0)}</bdi>}
              subtitle={t("forecastCategories.Commit", "مؤكدة للإغلاق")}
              tone="success"
            />
            <CrmKpiCard
              title={t("crm.forecast.catClosed", "صفقات مغلقة وناجحة")}
              value={<bdi>{formatCurrency(forecastData?.wonAmount || 0)}</bdi>}
              subtitle={t("crm.forecast.actualAchieved", "محقق فعلياً")}
              tone="default"
            />
          </div>

          {/* Performance Summary KPIs */}
          <Surface padding="normal" variant="card">
            <h3 style={{ margin: "0 0 1rem", fontSize: "0.8125rem", fontWeight: 700, color: "var(--color-content-muted)", textTransform: "uppercase", letterSpacing: "0.05em" }}>
              {t("crm.reports.kpiTitle", "مؤشرات أداء تحويل المبيعات (Sales KPIs)")}
            </h3>
            <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(180px, 1fr))", gap: "1.5rem" }}>
              <div>
                <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
                  {t("crm.reports.winRate", "معدل التحويل (Conversion Rate)")}
                </span>
                <div style={{ fontSize: "1.5rem", fontWeight: 800, color: "var(--color-content-primary)", marginTop: "0.25rem" }}>
                  {reportsSummary?.leadConversionRate || 0}%
                </div>
              </div>

              <div>
                <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
                  {t("crm.reports.totalPipeline", "إجمالي الصفقات الناجحة")}
                </span>
                <div style={{ fontSize: "1.5rem", fontWeight: 800, color: "var(--color-status-success, #059669)", marginTop: "0.25rem" }}>
                  <bdi>{formatCurrency(reportsSummary?.wonAmount || 0)}</bdi>
                </div>
              </div>

              <div>
                <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
                  {t("crm.reports.openDealsCount", "عدد الصفقات النشطة")}
                </span>
                <div style={{ fontSize: "1.5rem", fontWeight: 800, color: "var(--color-content-primary)", marginTop: "0.25rem" }}>
                  {reportsSummary?.openOpportunityCount || 0}
                </div>
              </div>

              <div>
                <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
                  {t("crm.reports.avgDealSize", "عدد العملاء المحتملين")}
                </span>
                <div style={{ fontSize: "1.5rem", fontWeight: 800, color: "var(--color-content-primary)", marginTop: "0.25rem" }}>
                  {reportsSummary?.leadCount || 0}
                </div>
              </div>
            </div>
          </Surface>
        </div>
      )}

      {/* SUB-TAB 2: PIPELINE INSPECTION */}
      {subTab === "inspection" && (
        <Surface padding="none" variant="card">
          <div style={{ padding: "1rem 1.25rem", backgroundColor: "var(--color-surface-subtle)", borderBottom: "1px solid var(--color-border-default)" }}>
            <h2 style={{ margin: 0, fontSize: "0.9375rem", fontWeight: 700, color: "var(--color-content-primary)" }}>
              {t("crm.inspection.title", "فحص صحة الصفقات وتراجع تواريخ الإغلاق")}
            </h2>
            <p style={{ margin: "0.25rem 0 0", fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
              {t("crm.inspection.subtitle", "كشف الصفقات المتوقفة أو المعرضة للخطر التي لم تشهد نشاطاً مؤخراً")}
            </p>
          </div>

          <Table aria-label={t("crm.inspection.title", "فحص صحة الصفقات")}>
            <TableHeader>
              <TableRow>
                <TableHead>{t("crm.opportunities.titleLabel", "الصفقة")}</TableHead>
                <TableHead>{t("crm.opportunities.value", "القيمة")}</TableHead>
                <TableHead>{t("crm.opportunities.health", "صحة الصفقة")}</TableHead>
                <TableHead>{t("crm.opportunities.forecastCategory", "تصنيف التوقع")}</TableHead>
                <TableHead>{t("crm.opportunities.closeDate", "تاريخ الإغلاق")}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {loadingInspection ? (
                <TableRow>
                  <TableCell colSpan={5} style={{ textAlign: "center", padding: "2.5rem" }}>
                    {t("crm.common.loading", "جاري فحص الصفقات...")}
                  </TableCell>
                </TableRow>
              ) : !inspectionDeals?.items || inspectionDeals.items.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={5} style={{ textAlign: "center", padding: "3rem 1.5rem" }}>
                    <CheckCircle style={{ margin: "0 auto 0.5rem", width: "2.5rem", height: "2.5rem", color: "var(--color-status-success)" }} />
                    <p style={{ margin: 0, fontWeight: 700, color: "var(--color-content-primary)" }}>
                      {t("crm.inspection.allHealthy", "المسار البيعي في حالة ممتازة!")}
                    </p>
                    <p style={{ margin: "0.25rem 0 0", fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
                      {t("crm.inspection.allHealthyDesc", "لا توجد صفقات متوقفة أو متأخرة بدون متابعة.")}
                    </p>
                  </TableCell>
                </TableRow>
              ) : (
                inspectionDeals.items.map((deal) => (
                  <TableRow key={deal.id}>
                    <TableCell>
                      <span style={{ fontWeight: 600, color: "var(--color-content-primary)" }}>{deal.name}</span>
                    </TableCell>
                    <TableCell>
                      <span style={{ fontWeight: 700, color: "var(--color-content-primary)" }}>{formatCurrency(deal.estimatedAmount)}</span>
                    </TableCell>
                    <TableCell>
                      <DealHealthBadge status={deal.healthStatus} score={deal.healthScore} />
                    </TableCell>
                    <TableCell>
                      <ForecastCategoryBadge category={deal.forecastCategory} />
                    </TableCell>
                    <TableCell style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
                      {deal.expectedCloseDate ? new Date(deal.expectedCloseDate).toLocaleDateString(i18n.language) : "—"}
                    </TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>
        </Surface>
      )}

      {/* SUB-TAB 3: TEAMS & MEMBERS */}
      {subTab === "teams" && (
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(320px, 1fr))", gap: "1.5rem", alignItems: "start" }}>
          {/* Teams List */}
          <Surface padding="none" variant="card">
            <div style={{ padding: "1rem 1.25rem", backgroundColor: "var(--color-surface-subtle)", borderBottom: "1px solid var(--color-border-default)", display: "flex", alignItems: "center", justifyContent: "space-between" }}>
              <h2 style={{ margin: 0, fontSize: "0.9375rem", fontWeight: 700, color: "var(--color-content-primary)" }}>
                {t("crm.teams.listTitle", "فرق المبيعات")}
              </h2>
              <Button variant="primary" size="compact" onPress={() => setIsNewTeamOpen(true)}>
                + {t("crm.teams.createButton", "فريق جديد")}
              </Button>
            </div>

            <div style={{ display: "flex", flexDirection: "column" }}>
              {loadingTeams ? (
                <div style={{ padding: "2rem", textAlign: "center", color: "var(--color-content-muted)", fontSize: "0.8125rem" }}>
                  {t("crm.common.loading", "جاري التحميل...")}
                </div>
              ) : teams?.map((tm) => {
                const isSelected = (selectedTeam?.id || teams[0]?.id) === tm.id;
                return (
                  <div
                    key={tm.id}
                    onClick={() => setSelectedTeam(tm)}
                    style={{
                      padding: "0.875rem 1.25rem",
                      borderBottom: "1px solid var(--color-border-subtle)",
                      display: "flex",
                      alignItems: "center",
                      justifyContent: "space-between",
                      cursor: "pointer",
                      backgroundColor: isSelected ? "var(--color-surface-subtle, #f1f5f9)" : "transparent",
                      borderInlineStart: isSelected ? "4px solid var(--color-brand-primary)" : "4px solid transparent"
                    }}
                  >
                    <div>
                      <div style={{ fontSize: "0.875rem", fontWeight: 700, color: "var(--color-content-primary)" }}>{tm.name}</div>
                      <div style={{ fontSize: "0.75rem", fontFamily: "var(--font-mono)", color: "var(--color-content-muted)", textTransform: "uppercase" }}>{tm.code}</div>
                    </div>
                    <ChevronRight style={{ width: "1rem", height: "1rem", color: "var(--color-content-muted)" }} />
                  </div>
                );
              })}
            </div>
          </Surface>

          {/* Team Members */}
          <Surface padding="none" variant="card">
            <div style={{ padding: "1rem 1.25rem", backgroundColor: "var(--color-surface-subtle)", borderBottom: "1px solid var(--color-border-default)", display: "flex", alignItems: "center", justifyContent: "space-between", flexWrap: "wrap", gap: "0.75rem" }}>
              <div>
                <h2 style={{ margin: 0, fontSize: "0.9375rem", fontWeight: 700, color: "var(--color-content-primary)" }}>
                  {t("crm.teams.membersTitle", "أعضاء الفريق:")} {selectedTeam?.name || teams?.[0]?.name}
                </h2>
                <p style={{ margin: "0.25rem 0 0", fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
                  {t("crm.teams.membersSubtitle", "مسؤولو المبيعات الملحقون بهذا الفريق وتحديد أدوارهم")}
                </p>
              </div>
              <Button variant="primary" size="compact" onPress={() => setIsAddMemberOpen(true)}>
                + {t("crm.teams.addMemberBtn", "إضافة عضو")}
              </Button>
            </div>

            <Table aria-label={t("crm.teams.membersTitle", "أعضاء الفريق")}>
              <TableHeader>
                <TableRow>
                  <TableHead>{t("crm.teams.memberId", "المستخدم")}</TableHead>
                  <TableHead>{t("crm.teams.memberRole", "الدور")}</TableHead>
                  <TableHead>{t("crm.teams.isLeader", "قائد الفريق")}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {loadingMembers ? (
                  <TableRow>
                    <TableCell colSpan={3} style={{ textAlign: "center", padding: "2rem" }}>
                      {t("crm.common.loading", "جاري التحميل...")}
                    </TableCell>
                  </TableRow>
                ) : !teamMembers || teamMembers.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={3} style={{ textAlign: "center", padding: "2rem", color: "var(--color-content-muted)", fontSize: "0.8125rem" }}>
                      {t("crm.teams.noMembers", "لا يوجد أعضاء في هذا الفريق بعد. قم بإضافة مسؤول مبيعات.")}
                    </TableCell>
                  </TableRow>
                ) : (
                  teamMembers.map((m) => (
                    <TableRow key={m.userId}>
                      <TableCell style={{ fontFamily: "var(--font-mono)", fontSize: "0.75rem" }}>{m.userId}</TableCell>
                      <TableCell style={{ fontWeight: 600 }}>{m.role}</TableCell>
                      <TableCell>
                        {m.role === "Lead" ? (
                          <span style={{ padding: "0.125rem 0.5rem", borderRadius: "var(--radius-sm)", fontSize: "0.6875rem", fontWeight: 700, backgroundColor: "#ecfdf5", color: "#065f46" }}>
                            {t("crm.teams.leaderBadge", "قائد الفريق")}
                          </span>
                        ) : (
                          <span style={{ color: "var(--color-content-muted)" }}>—</span>
                        )}
                      </TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </Surface>
        </div>
      )}

      {/* SUB-TAB 4: PIPELINES & STAGES */}
      {subTab === "pipelines" && (
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(320px, 1fr))", gap: "1.5rem", alignItems: "start" }}>
          {/* Pipelines */}
          <Surface padding="none" variant="card">
            <div style={{ padding: "1rem 1.25rem", backgroundColor: "var(--color-surface-subtle)", borderBottom: "1px solid var(--color-border-default)", display: "flex", alignItems: "center", justifyContent: "space-between" }}>
              <h2 style={{ margin: 0, fontSize: "0.9375rem", fontWeight: 700, color: "var(--color-content-primary)" }}>
                {t("crm.pipelines.listTitle", "المسارات البيعية")}
              </h2>
              <Button variant="primary" size="compact" onPress={() => setIsNewPipelineOpen(true)}>
                + {t("crm.pipelines.createBtn", "مسار جديد")}
              </Button>
            </div>

            <div style={{ display: "flex", flexDirection: "column" }}>
              {loadingPipelines ? (
                <div style={{ padding: "2rem", textAlign: "center", color: "var(--color-content-muted)", fontSize: "0.8125rem" }}>
                  {t("crm.common.loading", "جاري التحميل...")}
                </div>
              ) : pipelines?.map((pl) => {
                const isSelected = (selectedPipeline?.id || pipelines[0]?.id) === pl.id;
                return (
                  <div
                    key={pl.id}
                    onClick={() => setSelectedPipeline(pl)}
                    style={{
                      padding: "0.875rem 1.25rem",
                      borderBottom: "1px solid var(--color-border-subtle)",
                      display: "flex",
                      alignItems: "center",
                      justifyContent: "space-between",
                      cursor: "pointer",
                      backgroundColor: isSelected ? "var(--color-surface-subtle, #f1f5f9)" : "transparent",
                      borderInlineStart: isSelected ? "4px solid var(--color-brand-primary)" : "4px solid transparent"
                    }}
                  >
                    <div>
                      <div style={{ fontSize: "0.875rem", fontWeight: 700, color: "var(--color-content-primary)" }}>{pl.name}</div>
                      <div style={{ display: "flex", alignItems: "center", gap: "0.5rem", marginTop: "0.25rem" }}>
                        <span style={{ fontSize: "0.75rem", fontFamily: "var(--font-mono)", color: "var(--color-content-muted)", textTransform: "uppercase" }}>{pl.code}</span>
                        {pl.isDefault && (
                          <span style={{ padding: "0.125rem 0.375rem", borderRadius: "var(--radius-sm)", fontSize: "0.6875rem", backgroundColor: "var(--color-surface-subtle)", color: "var(--color-content-primary)", fontWeight: 600 }}>
                            {t("crm.pipelines.defaultBadge", "افتراضي")}
                          </span>
                        )}
                      </div>
                    </div>
                    <ChevronRight style={{ width: "1rem", height: "1rem", color: "var(--color-content-muted)" }} />
                  </div>
                );
              })}
            </div>
          </Surface>

          {/* Stages */}
          <Surface padding="none" variant="card">
            <div style={{ padding: "1rem 1.25rem", backgroundColor: "var(--color-surface-subtle)", borderBottom: "1px solid var(--color-border-default)", display: "flex", alignItems: "center", justifyContent: "space-between", flexWrap: "wrap", gap: "0.75rem" }}>
              <div>
                <h2 style={{ margin: 0, fontSize: "0.9375rem", fontWeight: 700, color: "var(--color-content-primary)" }}>
                  {t("crm.stages.listTitle", "مراحل المسار:")} {activePipeline?.name}
                </h2>
                <p style={{ margin: "0.25rem 0 0", fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
                  {t("crm.stages.listSubtitle", "الترتيب واحتمالية الفوز المعتمدة لكل مرحلة في التوقعات")}
                </p>
              </div>
              {activePipeline && (
                <Button variant="primary" size="compact" onPress={() => setIsNewStageOpen(true)}>
                  + {t("crm.stages.createBtn", "إضافة مرحلة")}
                </Button>
              )}
            </div>

            <Table aria-label={t("crm.stages.listTitle", "مراحل المسار")}>
              <TableHeader>
                <TableRow>
                  <TableHead>{t("crm.stages.order", "الترتيب")}</TableHead>
                  <TableHead>{t("crm.stages.name", "اسم المرحلة")}</TableHead>
                  <TableHead>{t("crm.stages.code", "الرمز")}</TableHead>
                  <TableHead>{t("crm.stages.probability", "الاحتمالية")}</TableHead>
                  <TableHead>{t("crm.stages.type", "النوع")}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {!activePipeline?.stages || activePipeline.stages.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={5} style={{ textAlign: "center", padding: "2rem", color: "var(--color-content-muted)", fontSize: "0.8125rem" }}>
                      {t("crm.stages.noStages", "لا توجد مراحل محددة لهذا المسار بعد.")}
                    </TableCell>
                  </TableRow>
                ) : (
                  activePipeline.stages.map((stg) => (
                    <TableRow key={stg.id}>
                      <TableCell style={{ fontWeight: 700, color: "var(--color-content-muted)" }}>{stg.sequence}</TableCell>
                      <TableCell style={{ fontWeight: 600 }}>{stg.name}</TableCell>
                      <TableCell style={{ fontFamily: "var(--font-mono)", fontSize: "0.75rem", textTransform: "uppercase" }}>{stg.status}</TableCell>
                      <TableCell style={{ fontWeight: 700 }}>{stg.probability}%</TableCell>
                      <TableCell>
                        {stg.isWon ? (
                          <span style={{ padding: "0.125rem 0.5rem", borderRadius: "var(--radius-sm)", fontSize: "0.6875rem", fontWeight: 700, backgroundColor: "#ecfdf5", color: "#065f46" }}>
                            {t("crm.stages.wonBadge", "فوز الصفقة")}
                          </span>
                        ) : stg.isLost ? (
                          <span style={{ padding: "0.125rem 0.5rem", borderRadius: "var(--radius-sm)", fontSize: "0.6875rem", fontWeight: 700, backgroundColor: "#fef2f2", color: "#991b1b" }}>
                            {t("crm.stages.lostBadge", "خسارة الصفقة")}
                          </span>
                        ) : (
                          <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>{t("crm.stages.inProgress", "قيد التقدم")}</span>
                        )}
                      </TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </Surface>
        </div>
      )}

      {/* SUB-TAB 5: CUSTOM FIELDS & AUDIT */}
      {subTab === "settings" && (
        <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
          {/* Custom Fields Card */}
          <Surface padding="none" variant="card">
            <div style={{ padding: "1rem 1.25rem", backgroundColor: "var(--color-surface-subtle)", borderBottom: "1px solid var(--color-border-default)", display: "flex", alignItems: "center", justifyContent: "space-between", flexWrap: "wrap", gap: "0.75rem" }}>
              <div>
                <h2 style={{ margin: 0, fontSize: "0.9375rem", fontWeight: 700, color: "var(--color-content-primary)" }}>
                  {t("crm.customFields.title", "الحقول المخصصة (Custom Fields)")}
                </h2>
                <p style={{ margin: "0.25rem 0 0", fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
                  {t("crm.customFields.subtitle", "تخصيص نماذج العملاء والصفقات بحقول وبيانات إضافية")}
                </p>
              </div>
              <Button variant="primary" size="compact" onPress={() => setIsNewCustomFieldOpen(true)}>
                + {t("crm.customFields.createBtn", "تعريف حقل مخصص")}
              </Button>
            </div>

            <Table aria-label={t("crm.customFields.title", "الحقول المخصصة")}>
              <TableHeader>
                <TableRow>
                  <TableHead>{t("crm.customFields.targetEntity", "الجهة")}</TableHead>
                  <TableHead>{t("crm.customFields.fieldLabel", "التسمية")}</TableHead>
                  <TableHead>{t("crm.customFields.fieldName", "المعرف البرمجي")}</TableHead>
                  <TableHead>{t("crm.customFields.fieldType", "النوع")}</TableHead>
                  <TableHead>{t("crm.customFields.isRequired", "إلزامي")}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {loadingCustomFields ? (
                  <TableRow>
                    <TableCell colSpan={5} style={{ textAlign: "center", padding: "2rem" }}>
                      {t("crm.common.loading", "جاري التحميل...")}
                    </TableCell>
                  </TableRow>
                ) : !customFields || customFields.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={5} style={{ textAlign: "center", padding: "2rem", color: "var(--color-content-muted)", fontSize: "0.8125rem" }}>
                      {t("crm.customFields.empty", "لم يتم تعريف أي حقول مخصصة حتى الآن.")}
                    </TableCell>
                  </TableRow>
                ) : (
                  customFields.map((cf) => (
                    <TableRow key={cf.id}>
                      <TableCell style={{ fontWeight: 700 }}>{cf.entityType}</TableCell>
                      <TableCell style={{ fontWeight: 600 }}>{cf.label}</TableCell>
                      <TableCell style={{ fontFamily: "var(--font-mono)", fontSize: "0.75rem", color: "var(--color-content-muted)" }}>{cf.fieldKey}</TableCell>
                      <TableCell style={{ fontSize: "0.8125rem" }}>{cf.dataType}</TableCell>
                      <TableCell style={{ fontWeight: 600 }}>
                        {cf.requiredStageId ? t("crm.common.yes", "نعم") : t("crm.common.no", "لا")}
                      </TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </Surface>

          {/* CRM Audit Log Card */}
          <Surface padding="none" variant="card">
            <div style={{ padding: "1rem 1.25rem", backgroundColor: "var(--color-surface-subtle)", borderBottom: "1px solid var(--color-border-default)", display: "flex", alignItems: "center", gap: "0.5rem" }}>
              <History style={{ width: "1rem", height: "1rem", color: "var(--color-content-muted)" }} />
              <h2 style={{ margin: 0, fontSize: "0.9375rem", fontWeight: 700, color: "var(--color-content-primary)" }}>
                {t("crm.audit.title", "سجل تدقيق عمليات CRM (Audit Log)")}
              </h2>
            </div>

            <Table aria-label={t("crm.audit.title", "سجل التدقيق")}>
              <TableHeader>
                <TableRow>
                  <TableHead>{t("crm.audit.date", "التاريخ")}</TableHead>
                  <TableHead>{t("crm.audit.action", "الإجراء")}</TableHead>
                  <TableHead>{t("crm.audit.entity", "الكيان")}</TableHead>
                  <TableHead>{t("crm.audit.details", "التفاصيل")}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {loadingAudit ? (
                  <TableRow>
                    <TableCell colSpan={4} style={{ textAlign: "center", padding: "2rem" }}>
                      {t("crm.common.loading", "جاري التحميل...")}
                    </TableCell>
                  </TableRow>
                ) : !auditLogs || auditLogs.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={4} style={{ textAlign: "center", padding: "2rem", color: "var(--color-content-muted)", fontSize: "0.8125rem" }}>
                      {t("crm.audit.empty", "لا توجد سجلات تدقيق حتى الآن.")}
                    </TableCell>
                  </TableRow>
                ) : (
                  auditLogs.slice(0, 10).map((log: any, idx: number) => (
                    <TableRow key={idx}>
                      <TableCell style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", whiteSpace: "nowrap" }}>
                        {log.timestamp ? new Date(log.timestamp).toLocaleString(i18n.language) : "—"}
                      </TableCell>
                      <TableCell style={{ fontWeight: 700, color: "var(--color-brand-primary)" }}>
                        {log.action || log.eventType}
                      </TableCell>
                      <TableCell style={{ fontSize: "0.8125rem" }}>{log.entityType || log.entity}</TableCell>
                      <TableCell style={{ fontSize: "0.8125rem", color: "var(--color-content-secondary)" }}>
                        {log.details || log.description || "—"}
                      </TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </Surface>
        </div>
      )}

      {/* Modals */}
      {isNewPipelineOpen && (
        <NewPipelineModal
          isOpen={isNewPipelineOpen}
          onClose={() => setIsNewPipelineOpen(false)}
          legalEntityId={legalEntityId}
        />
      )}

      {isNewStageOpen && activePipeline && (
        <NewStageModal
          isOpen={isNewStageOpen}
          onClose={() => setIsNewStageOpen(false)}
          legalEntityId={legalEntityId}
          pipelineId={activePipeline.id}
          currentStagesCount={activePipeline.stages?.length || 0}
        />
      )}

      {isNewTeamOpen && (
        <NewTeamModal
          isOpen={isNewTeamOpen}
          onClose={() => setIsNewTeamOpen(false)}
          legalEntityId={legalEntityId}
        />
      )}

      {isAddMemberOpen && activeTeamId && (
        <AddTeamMemberModal
          isOpen={isAddMemberOpen}
          onClose={() => setIsAddMemberOpen(false)}
          legalEntityId={legalEntityId}
          teamId={activeTeamId}
        />
      )}

      {isNewCustomFieldOpen && (
        <NewCustomFieldModal
          isOpen={isNewCustomFieldOpen}
          onClose={() => setIsNewCustomFieldOpen(false)}
          legalEntityId={legalEntityId}
        />
      )}
    </div>
  );
};

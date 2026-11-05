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
} from "../../hooks/use-crm-queries";
import { Pipeline, Team } from "../../types";
import { DealHealthBadge } from "../../components/deal-health-badge";
import { ForecastCategoryBadge } from "../../components/forecast-category-badge";
import { NewPipelineModal } from "./new-pipeline-modal";
import { NewStageModal } from "./new-stage-modal";
import { NewTeamModal } from "./new-team-modal";
import { AddTeamMemberModal } from "./add-team-member-modal";
import { NewCustomFieldModal } from "./new-custom-field-modal";
import {
  BarChart3,
  Search,
  Users,
  GitBranch,
  Sliders,
  History,
  Plus,
  Camera,
  Percent,
  CheckCircle,
  AlertCircle,
  Clock,
  Layers,
  ChevronRight,
} from "../../components/crm-icons.tsx";

interface CrmManagementTabProps {
  legalEntityId: string;
}

export const CrmManagementTab: React.FC<CrmManagementTabProps> = ({ legalEntityId }) => {
  const { t, i18n } = useTranslation("crm");
  const isRtl = i18n.dir() === "rtl";

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
  const { data: forecastData, isLoading: loadingForecast } = useCrmForecastQuery(legalEntityId);
  const { data: reportsSummary, isLoading: loadingReports } = useCrmReportsSummaryQuery(legalEntityId);
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

  return (
    <div className="space-y-6">
      {/* Management Sub-Navigation */}
      <div className="border-b border-slate-200 bg-white px-4 rounded-t-lg shadow-xs">
        <nav className="flex space-x-6 rtl:space-x-reverse" aria-label={t("crm.mgmt.tabsLabel", "تبويبات الإدارة")}>
          <button
            type="button"
            onClick={() => setSubTab("forecast")}
            className={`py-3.5 px-1 border-b-2 font-semibold text-xs inline-flex items-center gap-1.5 transition-colors cursor-pointer ${
              subTab === "forecast"
                ? "border-primary-600 text-primary-700"
                : "border-transparent text-slate-500 hover:text-slate-700 hover:border-slate-300"
            }`}
          >
            <BarChart3 className="h-4 w-4" />
            <span>{t("crm.mgmt.tabs.forecast", "توقعات المبيعات والتقارير")}</span>
          </button>

          <button
            type="button"
            onClick={() => setSubTab("inspection")}
            className={`py-3.5 px-1 border-b-2 font-semibold text-xs inline-flex items-center gap-1.5 transition-colors cursor-pointer ${
              subTab === "inspection"
                ? "border-primary-600 text-primary-700"
                : "border-transparent text-slate-500 hover:text-slate-700 hover:border-slate-300"
            }`}
          >
            <Search className="h-4 w-4" />
            <span>{t("crm.mgmt.tabs.inspection", "فحص المسار البيعي")}</span>
          </button>

          <button
            type="button"
            onClick={() => setSubTab("teams")}
            className={`py-3.5 px-1 border-b-2 font-semibold text-xs inline-flex items-center gap-1.5 transition-colors cursor-pointer ${
              subTab === "teams"
                ? "border-primary-600 text-primary-700"
                : "border-transparent text-slate-500 hover:text-slate-700 hover:border-slate-300"
            }`}
          >
            <Users className="h-4 w-4" />
            <span>{t("crm.mgmt.tabs.teams", "فرق المبيعات")}</span>
          </button>

          <button
            type="button"
            onClick={() => setSubTab("pipelines")}
            className={`py-3.5 px-1 border-b-2 font-semibold text-xs inline-flex items-center gap-1.5 transition-colors cursor-pointer ${
              subTab === "pipelines"
                ? "border-primary-600 text-primary-700"
                : "border-transparent text-slate-500 hover:text-slate-700 hover:border-slate-300"
            }`}
          >
            <GitBranch className="h-4 w-4" />
            <span>{t("crm.mgmt.tabs.pipelines", "المسارات والمراحل")}</span>
          </button>

          <button
            type="button"
            onClick={() => setSubTab("settings")}
            className={`py-3.5 px-1 border-b-2 font-semibold text-xs inline-flex items-center gap-1.5 transition-colors cursor-pointer ${
              subTab === "settings"
                ? "border-primary-600 text-primary-700"
                : "border-transparent text-slate-500 hover:text-slate-700 hover:border-slate-300"
            }`}
          >
            <Sliders className="h-4 w-4" />
            <span>{t("crm.mgmt.tabs.settings", "الحقول المخصصة وسجل العمليات")}</span>
          </button>
        </nav>
      </div>

      {/* SUB-TAB 1: FORECAST & REPORTS */}
      {subTab === "forecast" && (
        <div className="space-y-6">
          {/* Top Actions: Take Snapshot */}
          <div className="flex items-center justify-between bg-white p-4 rounded-lg border border-slate-200 shadow-xs">
            <div>
              <h3 className="text-sm font-bold text-slate-900">
                {t("crm.forecast.headerTitle", "التوقعات البيعية الموزونة للفترة الحالية")}
              </h3>
              <p className="text-xs text-slate-500">
                {t("crm.forecast.headerSubtitle", "حساب المبيعات المتوقعة وفقاً لاحتمالية نجاح كل مرحلة في المسار")}
              </p>
            </div>
            <button
              type="button"
              onClick={handleTakeSnapshot}
              disabled={snapshotMutation.isPending}
              className="inline-flex items-center gap-1.5 px-3 py-1.5 bg-slate-800 hover:bg-slate-900 text-white text-xs font-semibold rounded-md shadow-xs transition-colors cursor-pointer disabled:opacity-50"
            >
              <Camera className="h-3.5 w-3.5" />
              <span>{snapshotMutation.isPending ? t("crm.common.saving", "جاري الحفظ...") : t("crm.forecast.takeSnapshot", "حفظ لقطة للتوقعات")}</span>
            </button>
          </div>

          {/* Forecast Categories Cards */}
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
            {/* Pipeline Category */}
            <div className="bg-white p-4 rounded-lg border border-slate-200 shadow-xs">
              <div className="flex items-center justify-between mb-2">
                <span className="text-xs font-bold text-slate-600">{t("crm.forecast.catPipeline", "في المسار (Pipeline)")}</span>
                <span className="px-2 py-0.5 rounded text-[10px] font-bold bg-blue-100 text-blue-800">
                  {t("crm.forecast.dealsCount", "{{count}} صفقات", { count: forecastData?.openOpportunityCount || 0 })}
                </span>
              </div>
              <div className="text-xl font-bold text-slate-900">
                {formatCurrency(forecastData?.pipelineAmount || 0)}
              </div>
              <div className="text-xs text-slate-500 mt-1">
                {t("crm.forecast.weighted", "القيمة الموزونة:")}{" "}
                <span className="font-semibold text-slate-700">
                  {formatCurrency(forecastData?.weightedAmount || 0)}
                </span>
              </div>
            </div>

            {/* Best Case Category */}
            <div className="bg-white p-4 rounded-lg border border-slate-200 shadow-xs">
              <div className="flex items-center justify-between mb-2">
                <span className="text-xs font-bold text-indigo-700">{t("crm.forecast.catBestCase", "أفضل سيناريو (Best Case)")}</span>
              </div>
              <div className="text-xl font-bold text-indigo-900">
                {formatCurrency(forecastData?.bestCaseAmount || 0)}
              </div>
            </div>

            {/* Commit Category */}
            <div className="bg-white p-4 rounded-lg border border-slate-200 shadow-xs">
              <div className="flex items-center justify-between mb-2">
                <span className="text-xs font-bold text-emerald-700">{t("crm.forecast.catCommit", "مؤكدة للإغلاق (Commit)")}</span>
              </div>
              <div className="text-xl font-bold text-emerald-900">
                {formatCurrency(forecastData?.commitAmount || 0)}
              </div>
            </div>

            {/* Closed Won Category */}
            <div className="bg-white p-4 rounded-lg border border-slate-200 shadow-xs">
              <div className="flex items-center justify-between mb-2">
                <span className="text-xs font-bold text-teal-700">{t("crm.forecast.catClosed", "صفقات مغلقة وناجحة")}</span>
              </div>
              <div className="text-xl font-bold text-teal-900">
                {formatCurrency(forecastData?.wonAmount || 0)}
              </div>
              <div className="text-xs text-slate-500 mt-1">
                {t("crm.forecast.actualAchieved", "محقق فعلياً")}
              </div>
            </div>
          </div>

          {/* Performance Summary KPIs */}
          <div className="bg-white p-6 rounded-lg border border-slate-200 shadow-xs">
            <h4 className="text-xs font-bold text-slate-800 uppercase tracking-wider mb-4">
              {t("crm.reports.kpiTitle", "مؤشرات أداء تحويل المبيعات (Sales KPIs)")}
            </h4>
            <div className="grid grid-cols-2 sm:grid-cols-4 gap-4 divide-y sm:divide-y-0 sm:divide-x sm:divide-slate-200 rtl:space-x-reverse">
              <div className="pt-2 sm:pt-0">
                <div className="text-xs text-slate-500">{t("crm.reports.winRate", "معدل التحويل (Conversion Rate)")}</div>
                <div className="text-2xl font-black text-slate-900 mt-1">
                  {reportsSummary?.leadConversionRate || 0}%
                </div>
              </div>

              <div className="pt-2 sm:pt-0 sm:px-4">
                <div className="text-xs text-slate-500">{t("crm.reports.totalPipeline", "إجمالي الصفقات الناجحة")}</div>
                <div className="text-2xl font-black text-slate-900 mt-1">
                  {formatCurrency(reportsSummary?.wonAmount || 0)}
                </div>
              </div>

              <div className="pt-2 sm:pt-0 sm:px-4">
                <div className="text-xs text-slate-500">{t("crm.reports.openDealsCount", "عدد الصفقات النشطة")}</div>
                <div className="text-2xl font-black text-slate-900 mt-1">
                  {reportsSummary?.openOpportunityCount || 0}
                </div>
              </div>

              <div className="pt-2 sm:pt-0 sm:px-4">
                <div className="text-xs text-slate-500">{t("crm.reports.avgDealSize", "عدد العملاء المحتملين")}</div>
                <div className="text-2xl font-black text-slate-900 mt-1">
                  {reportsSummary?.leadCount || 0}
                </div>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* SUB-TAB 2: PIPELINE INSPECTION */}
      {subTab === "inspection" && (
        <div className="bg-white rounded-lg border border-slate-200 shadow-xs overflow-hidden">
          <div className="p-4 bg-slate-50 border-b border-slate-200 flex items-center justify-between">
            <div>
              <h3 className="text-sm font-bold text-slate-900">
                {t("crm.inspection.title", "فحص صحة الصفقات وتراجع تواريخ الإغلاق")}
              </h3>
              <p className="text-xs text-slate-500">
                {t("crm.inspection.subtitle", "كشف الصفقات المتوقفة أو المعرضة للخطر التي لم تشهد نشاطاً مؤخراً")}
              </p>
            </div>
          </div>

          {loadingInspection ? (
            <div className="p-8 text-center text-slate-500 text-sm">
              {t("crm.common.loading", "جاري فحص الصفقات...")}
            </div>
          ) : !inspectionDeals?.items || inspectionDeals.items.length === 0 ? (
            <div className="p-12 text-center">
              <CheckCircle className="mx-auto h-12 w-12 text-emerald-500 mb-2" />
              <h4 className="text-sm font-bold text-slate-800">
                {t("crm.inspection.allHealthy", "المسار البيعي في حالة ممتازة!")}
              </h4>
              <p className="text-xs text-slate-500 mt-1">
                {t("crm.inspection.allHealthyDesc", "لا توجد صفقات متوقفة أو متأخرة بدون متابعة.")}
              </p>
            </div>
          ) : (
            <div className="overflow-x-auto">
              <table className="min-w-full divide-y divide-slate-200 text-sm">
                <thead className="bg-slate-50 text-slate-700 font-medium">
                  <tr>
                    <th scope="col" className="px-4 py-3 text-start">{t("crm.opportunities.titleLabel", "الصفقة")}</th>
                    <th scope="col" className="px-4 py-3 text-start">{t("crm.opportunities.value", "القيمة")}</th>
                    <th scope="col" className="px-4 py-3 text-start">{t("crm.opportunities.health", "صحة الصفقة")}</th>
                    <th scope="col" className="px-4 py-3 text-start">{t("crm.opportunities.forecastCategory", "تصنيف التوقع")}</th>
                    <th scope="col" className="px-4 py-3 text-start">{t("crm.opportunities.closeDate", "تاريخ الإغلاق")}</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-200 bg-white">
                  {inspectionDeals.items.map((deal) => (
                    <tr key={deal.id} className="hover:bg-slate-50">
                      <td className="px-4 py-3 font-semibold text-slate-900">{deal.name}</td>
                      <td className="px-4 py-3 font-bold text-slate-800">{formatCurrency(deal.estimatedAmount)}</td>
                      <td className="px-4 py-3">
                        <DealHealthBadge status={deal.healthStatus} score={deal.healthScore} />
                      </td>
                      <td className="px-4 py-3">
                        <ForecastCategoryBadge category={deal.forecastCategory} />
                      </td>
                      <td className="px-4 py-3 text-xs text-slate-600">
                        {deal.expectedCloseDate
                          ? new Date(deal.expectedCloseDate).toLocaleDateString(i18n.language)
                          : "—"}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      )}

      {/* SUB-TAB 3: TEAMS & MEMBERS */}
      {subTab === "teams" && (
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
          {/* Teams List (4 cols) */}
          <div className="lg:col-span-4 bg-white rounded-lg border border-slate-200 shadow-xs overflow-hidden">
            <div className="p-4 bg-slate-50 border-b border-slate-200 flex items-center justify-between">
              <h3 className="text-sm font-bold text-slate-900">{t("crm.teams.listTitle", "فرق المبيعات")}</h3>
              <button
                type="button"
                onClick={() => setIsNewTeamOpen(true)}
                className="inline-flex items-center gap-1 text-xs px-2.5 py-1 bg-primary-600 hover:bg-primary-700 text-white rounded font-medium shadow-xs"
              >
                <Plus className="h-3.5 w-3.5" />
                <span>{t("crm.teams.createButton", "فريق جديد")}</span>
              </button>
            </div>

            <div className="divide-y divide-slate-100">
              {loadingTeams ? (
                <div className="p-6 text-center text-slate-400 text-xs">{t("crm.common.loading", "جاري التحميل...")}</div>
              ) : teams?.map((tm) => (
                <div
                  key={tm.id}
                  onClick={() => setSelectedTeam(tm)}
                  className={`p-3.5 flex items-center justify-between cursor-pointer transition-colors ${
                    (selectedTeam?.id || teams[0]?.id) === tm.id ? "bg-primary-50/70 border-s-4 border-primary-600" : "hover:bg-slate-50"
                  }`}
                >
                  <div>
                    <div className="text-sm font-bold text-slate-900">{tm.name}</div>
                    <div className="text-xs font-mono text-slate-500 uppercase">{tm.code}</div>
                  </div>
                  <ChevronRight className="h-4 w-4 text-slate-400" />
                </div>
              ))}
            </div>
          </div>

          {/* Team Members List (8 cols) */}
          <div className="lg:col-span-8 bg-white rounded-lg border border-slate-200 shadow-xs overflow-hidden">
            <div className="p-4 bg-slate-50 border-b border-slate-200 flex items-center justify-between">
              <div>
                <h3 className="text-sm font-bold text-slate-900">
                  {t("crm.teams.membersTitle", "أعضاء الفريق:")} {selectedTeam?.name || teams?.[0]?.name}
                </h3>
                <p className="text-xs text-slate-500">
                  {t("crm.teams.membersSubtitle", "مسؤولو المبيعات الملحقون بهذا الفريق وتحديد أدوارهم")}
                </p>
              </div>
              <button
                type="button"
                onClick={() => setIsAddMemberOpen(true)}
                className="inline-flex items-center gap-1 text-xs px-2.5 py-1 bg-primary-600 hover:bg-primary-700 text-white rounded font-medium shadow-xs"
              >
                <Plus className="h-3.5 w-3.5" />
                <span>{t("crm.teams.addMemberBtn", "إضافة عضو")}</span>
              </button>
            </div>

            <div className="p-0">
              {loadingMembers ? (
                <div className="p-8 text-center text-slate-400 text-xs">{t("crm.common.loading", "جاري التحميل...")}</div>
              ) : !teamMembers || teamMembers.length === 0 ? (
                <div className="p-8 text-center text-xs text-slate-500">
                  {t("crm.teams.noMembers", "لا يوجد أعضاء في هذا الفريق بعد. قم بإضافة مسؤول مبيعات.")}
                </div>
              ) : (
                <table className="min-w-full divide-y divide-slate-200 text-sm">
                  <thead className="bg-slate-50 text-slate-700 font-medium">
                    <tr>
                      <th scope="col" className="px-4 py-2.5 text-start">{t("crm.teams.memberId", "المستخدم")}</th>
                      <th scope="col" className="px-4 py-2.5 text-start">{t("crm.teams.memberRole", "الدور")}</th>
                      <th scope="col" className="px-4 py-2.5 text-start">{t("crm.teams.isLeader", "قائد الفريق")}</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-200">
                    {teamMembers.map((m) => (
                      <tr key={m.userId} className="hover:bg-slate-50">
                        <td className="px-4 py-2.5 font-mono text-xs text-slate-700">{m.userId}</td>
                        <td className="px-4 py-2.5 text-xs font-semibold text-slate-800">{m.role}</td>
                        <td className="px-4 py-2.5">
                          {m.role === "Lead" ? (
                            <span className="px-2 py-0.5 rounded text-[10px] font-bold bg-emerald-100 text-emerald-800">
                              {t("crm.teams.leaderBadge", "قائد الفريق")}
                            </span>
                          ) : (
                            <span className="text-xs text-slate-400">—</span>
                          )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </div>
          </div>
        </div>
      )}

      {/* SUB-TAB 4: PIPELINES & STAGES */}
      {subTab === "pipelines" && (
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
          {/* Pipelines List (4 cols) */}
          <div className="lg:col-span-4 bg-white rounded-lg border border-slate-200 shadow-xs overflow-hidden">
            <div className="p-4 bg-slate-50 border-b border-slate-200 flex items-center justify-between">
              <h3 className="text-sm font-bold text-slate-900">{t("crm.pipelines.listTitle", "المسارات البيعية")}</h3>
              <button
                type="button"
                onClick={() => setIsNewPipelineOpen(true)}
                className="inline-flex items-center gap-1 text-xs px-2.5 py-1 bg-primary-600 hover:bg-primary-700 text-white rounded font-medium shadow-xs"
              >
                <Plus className="h-3.5 w-3.5" />
                <span>{t("crm.pipelines.createBtn", "مسار جديد")}</span>
              </button>
            </div>

            <div className="divide-y divide-slate-100">
              {loadingPipelines ? (
                <div className="p-6 text-center text-slate-400 text-xs">{t("crm.common.loading", "جاري التحميل...")}</div>
              ) : pipelines?.map((pl) => (
                <div
                  key={pl.id}
                  onClick={() => setSelectedPipeline(pl)}
                  className={`p-3.5 flex items-center justify-between cursor-pointer transition-colors ${
                    (selectedPipeline?.id || pipelines[0]?.id) === pl.id ? "bg-primary-50/70 border-s-4 border-primary-600" : "hover:bg-slate-50"
                  }`}
                >
                  <div>
                    <div className="text-sm font-bold text-slate-900">{pl.name}</div>
                    <div className="flex items-center gap-2 mt-1">
                      <span className="text-xs font-mono text-slate-500 uppercase">{pl.code}</span>
                      {pl.isDefault && (
                        <span className="px-1.5 py-0.2 rounded text-[10px] bg-slate-200 text-slate-800 font-semibold">
                          {t("crm.pipelines.defaultBadge", "افتراضي")}
                        </span>
                      )}
                    </div>
                  </div>
                  <ChevronRight className="h-4 w-4 text-slate-400" />
                </div>
              ))}
            </div>
          </div>

          {/* Stages of selected pipeline (8 cols) */}
          <div className="lg:col-span-8 bg-white rounded-lg border border-slate-200 shadow-xs overflow-hidden">
            <div className="p-4 bg-slate-50 border-b border-slate-200 flex items-center justify-between">
              <div>
                <h3 className="text-sm font-bold text-slate-900">
                  {t("crm.stages.listTitle", "مراحل المسار:")} {activePipeline?.name}
                </h3>
                <p className="text-xs text-slate-500">
                  {t("crm.stages.listSubtitle", "الترتيب واحتمالية الفوز المعتمدة لكل مرحلة في التوقعات")}
                </p>
              </div>
              {activePipeline && (
                <button
                  type="button"
                  onClick={() => setIsNewStageOpen(true)}
                  className="inline-flex items-center gap-1 text-xs px-2.5 py-1 bg-primary-600 hover:bg-primary-700 text-white rounded font-medium shadow-xs"
                >
                  <Plus className="h-3.5 w-3.5" />
                  <span>{t("crm.stages.createBtn", "إضافة مرحلة")}</span>
                </button>
              )}
            </div>

            <div className="p-0">
              {!activePipeline?.stages || activePipeline.stages.length === 0 ? (
                <div className="p-8 text-center text-xs text-slate-500">
                  {t("crm.stages.noStages", "لا توجد مراحل محددة لهذا المسار بعد.")}
                </div>
              ) : (
                <table className="min-w-full divide-y divide-slate-200 text-sm">
                  <thead className="bg-slate-50 text-slate-700 font-medium">
                    <tr>
                      <th scope="col" className="px-4 py-2.5 text-start">{t("crm.stages.order", "الترتيب")}</th>
                      <th scope="col" className="px-4 py-2.5 text-start">{t("crm.stages.name", "اسم المرحلة")}</th>
                      <th scope="col" className="px-4 py-2.5 text-start">{t("crm.stages.code", "الرمز")}</th>
                      <th scope="col" className="px-4 py-2.5 text-start">{t("crm.stages.probability", "الاحتمالية")}</th>
                      <th scope="col" className="px-4 py-2.5 text-start">{t("crm.stages.type", "النوع")}</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-200">
                    {activePipeline.stages.map((stg) => (
                      <tr key={stg.id} className="hover:bg-slate-50">
                        <td className="px-4 py-2.5 font-bold text-xs text-slate-500">{stg.sequence}</td>
                        <td className="px-4 py-2.5 font-semibold text-slate-900">{stg.name}</td>
                        <td className="px-4 py-2.5 font-mono text-xs text-slate-500 uppercase">{stg.status}</td>
                        <td className="px-4 py-2.5 font-bold text-slate-700">{stg.probability}%</td>
                        <td className="px-4 py-2.5">
                          {stg.isWon ? (
                            <span className="px-2 py-0.5 rounded text-[10px] font-bold bg-emerald-100 text-emerald-800">
                              {t("crm.stages.wonBadge", "فوز الصفقة")}
                            </span>
                          ) : stg.isLost ? (
                            <span className="px-2 py-0.5 rounded text-[10px] font-bold bg-rose-100 text-rose-800">
                              {t("crm.stages.lostBadge", "خسارة الصفقة")}
                            </span>
                          ) : (
                            <span className="text-xs text-slate-500">{t("crm.stages.inProgress", "قيد التقدم")}</span>
                          )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </div>
          </div>
        </div>
      )}

      {/* SUB-TAB 5: CUSTOM FIELDS & AUDIT */}
      {subTab === "settings" && (
        <div className="space-y-6">
          {/* Custom Fields Card */}
          <div className="bg-white rounded-lg border border-slate-200 shadow-xs overflow-hidden">
            <div className="p-4 bg-slate-50 border-b border-slate-200 flex items-center justify-between">
              <div>
                <h3 className="text-sm font-bold text-slate-900">
                  {t("crm.customFields.title", "الحقول المخصصة (Custom Fields)")}
                </h3>
                <p className="text-xs text-slate-500">
                  {t("crm.customFields.subtitle", "تخصيص نماذج العملاء والصفقات بحقول وبيانات إضافية")}
                </p>
              </div>
              <button
                type="button"
                onClick={() => setIsNewCustomFieldOpen(true)}
                className="inline-flex items-center gap-1 text-xs px-2.5 py-1 bg-primary-600 hover:bg-primary-700 text-white rounded font-medium shadow-xs"
              >
                <Plus className="h-3.5 w-3.5" />
                <span>{t("crm.customFields.createBtn", "تعريف حقل مخصص")}</span>
              </button>
            </div>

            <div className="p-0">
              {loadingCustomFields ? (
                <div className="p-8 text-center text-slate-400 text-xs">{t("crm.common.loading", "جاري التحميل...")}</div>
              ) : !customFields || customFields.length === 0 ? (
                <div className="p-8 text-center text-xs text-slate-500">
                  {t("crm.customFields.empty", "لم يتم تعريف أي حقول مخصصة حتى الآن.")}
                </div>
              ) : (
                <table className="min-w-full divide-y divide-slate-200 text-sm">
                  <thead className="bg-slate-50 text-slate-700 font-medium">
                    <tr>
                      <th scope="col" className="px-4 py-2.5 text-start">{t("crm.customFields.targetEntity", "الجهة")}</th>
                      <th scope="col" className="px-4 py-2.5 text-start">{t("crm.customFields.fieldLabel", "التسمية")}</th>
                      <th scope="col" className="px-4 py-2.5 text-start">{t("crm.customFields.fieldName", "المعرف البرمجي")}</th>
                      <th scope="col" className="px-4 py-2.5 text-start">{t("crm.customFields.fieldType", "النوع")}</th>
                      <th scope="col" className="px-4 py-2.5 text-start">{t("crm.customFields.isRequired", "إلزامي")}</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-200">
                    {customFields.map((cf) => (
                      <tr key={cf.id} className="hover:bg-slate-50">
                        <td className="px-4 py-2.5 font-bold text-xs text-slate-700">{cf.entityType}</td>
                        <td className="px-4 py-2.5 font-semibold text-slate-900">{cf.label}</td>
                        <td className="px-4 py-2.5 font-mono text-xs text-slate-500">{cf.fieldKey}</td>
                        <td className="px-4 py-2.5 text-xs text-slate-600">{cf.dataType}</td>
                        <td className="px-4 py-2.5 text-xs font-semibold text-slate-700">
                          {cf.requiredStageId ? t("crm.common.yes", "نعم") : t("crm.common.no", "لا")}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </div>
          </div>

          {/* CRM Audit Log Card */}
          <div className="bg-white rounded-lg border border-slate-200 shadow-xs overflow-hidden">
            <div className="p-4 bg-slate-50 border-b border-slate-200 flex items-center justify-between">
              <div className="flex items-center gap-2">
                <History className="h-4 w-4 text-slate-600" />
                <h3 className="text-sm font-bold text-slate-900">
                  {t("crm.audit.title", "سجل تدقيق عمليات CRM (Audit Log)")}
                </h3>
              </div>
            </div>

            <div className="p-0">
              {loadingAudit ? (
                <div className="p-8 text-center text-slate-400 text-xs">{t("crm.common.loading", "جاري التحميل...")}</div>
              ) : !auditLogs || auditLogs.length === 0 ? (
                <div className="p-8 text-center text-xs text-slate-500">
                  {t("crm.audit.empty", "لا توجد سجلات تدقيق حتى الآن.")}
                </div>
              ) : (
                <table className="min-w-full divide-y divide-slate-200 text-sm">
                  <thead className="bg-slate-50 text-slate-700 font-medium">
                    <tr>
                      <th scope="col" className="px-4 py-2.5 text-start">{t("crm.audit.date", "التاريخ")}</th>
                      <th scope="col" className="px-4 py-2.5 text-start">{t("crm.audit.action", "الإجراء")}</th>
                      <th scope="col" className="px-4 py-2.5 text-start">{t("crm.audit.entity", "الكيان")}</th>
                      <th scope="col" className="px-4 py-2.5 text-start">{t("crm.audit.details", "التفاصيل")}</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-200">
                    {auditLogs.slice(0, 10).map((log: any, idx: number) => (
                      <tr key={idx} className="hover:bg-slate-50">
                        <td className="px-4 py-2.5 text-xs text-slate-500 whitespace-nowrap">
                          {log.timestamp ? new Date(log.timestamp).toLocaleString(i18n.language) : "—"}
                        </td>
                        <td className="px-4 py-2.5 font-bold text-xs text-primary-700">{log.action || log.eventType}</td>
                        <td className="px-4 py-2.5 text-xs text-slate-700">{log.entityType || log.entity}</td>
                        <td className="px-4 py-2.5 text-xs text-slate-600">{log.details || log.description || "—"}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </div>
          </div>
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

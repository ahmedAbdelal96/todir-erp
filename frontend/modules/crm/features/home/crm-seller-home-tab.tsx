import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import {
  useCrmSellerHomeQuery,
  useCrmSellerWorklistQuery,
  useCrmSellerAttentionQuery,
  useCrmPipelineInspectionQuery,
  useCrmReportsSummaryQuery,
  useCrmForecastQuery,
} from "../../hooks/use-crm-queries";
import { Activity, Lead, Opportunity } from "../../types";
import { ActivityTypeBadge } from "../../components/activity-type-badge";
import { DealHealthBadge } from "../../components/deal-health-badge";
import { PriorityBadge } from "../../components/priority-badge";
import { CompleteActivityModal } from "../activities/complete-activity-modal";
import { NewActivityModal } from "../activities/new-activity-modal";
import { QualifyLeadModal } from "../leads/qualify-lead-modal";
import { OpportunityWorkspaceModal } from "../opportunities/opportunity-workspace-modal";
import {
  CalendarCheck,
  AlertTriangle,
  Flame,
  CheckCircle2,
  Clock,
  ArrowRight,
  TrendingUp,
  UserCheck,
  DollarSign,
  Plus,
} from "../../components/crm-icons.tsx";

interface CrmSellerHomeTabProps {
  legalEntityId: string;
  onNavigateToLeads: () => void;
  onNavigateToOpportunities: () => void;
  onNavigateToActivities: () => void;
}

export const CrmSellerHomeTab: React.FC<CrmSellerHomeTabProps> = ({
  legalEntityId,
  onNavigateToLeads,
  onNavigateToOpportunities,
  onNavigateToActivities,
}) => {
  const { t, i18n } = useTranslation("crm");
  const isRtl = i18n.dir() === "rtl";

  // Modal states
  const [completeActivity, setCompleteActivity] = useState<Activity | null>(null);
  const [newActivityOpen, setNewActivityOpen] = useState(false);
  const [qualifyLead, setQualifyLead] = useState<Lead | null>(null);
  const [selectedOpportunity, setSelectedOpportunity] = useState<Opportunity | null>(null);

  // Queries
  const { data: reportsSummary } = useCrmReportsSummaryQuery(legalEntityId);
  const { data: forecastData } = useCrmForecastQuery(legalEntityId);
  const { data: worklistActivities, isLoading: loadingWorklist } = useCrmSellerWorklistQuery(legalEntityId);
  const { data: attentionLeads, isLoading: loadingAttention } = useCrmSellerAttentionQuery(legalEntityId);
  const { data: inspectionDeals, isLoading: loadingInspection } = useCrmPipelineInspectionQuery(legalEntityId);

  const formatCurrency = (val: number) => {
    return new Intl.NumberFormat(i18n.language, {
      style: "currency",
      currency: "EGP",
      maximumFractionDigits: 0,
    }).format(val || 0);
  };

  const activities = worklistActivities?.items || [];
  const leads = attentionLeads?.items || [];
  const staleDeals = inspectionDeals?.items || [];

  return (
    <div className="space-y-6">
      {/* Welcome & KPI Banner */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
        {/* KPI 1: Due & Overdue Tasks */}
        <div className="bg-white p-4 rounded-lg border border-slate-200 shadow-xs">
          <div className="flex items-center justify-between text-slate-500 mb-2">
            <span className="text-xs font-semibold">{t("crm.sellerHome.kpiDueTasks", "متابعات اليوم")}</span>
            <CalendarCheck className="h-4 w-4 text-primary-600" />
          </div>
          <div className="text-2xl font-bold text-slate-900">
            {activities.length}
          </div>
          <p className="text-xs text-slate-500 mt-1">
            {t("crm.sellerHome.kpiDueTasksHint", "تتطلب اتصال أو مقابلة اليوم")}
          </p>
        </div>

        {/* KPI 2: Attention Leads */}
        <div className="bg-white p-4 rounded-lg border border-slate-200 shadow-xs">
          <div className="flex items-center justify-between text-amber-600 mb-2">
            <span className="text-xs font-semibold text-slate-600">{t("crm.sellerHome.kpiAttentionLeads", "عملاء بحاجة لمتابعة")}</span>
            <AlertTriangle className="h-4 w-4 text-amber-500" />
          </div>
          <div className="text-2xl font-bold text-slate-900">
            {leads.length}
          </div>
          <p className="text-xs text-slate-500 mt-1">
            {t("crm.sellerHome.kpiAttentionLeadsHint", "عملاء جدد لم يُتصل بهم بعد")}
          </p>
        </div>

        {/* KPI 3: Pipeline Stale Deals */}
        <div className="bg-white p-4 rounded-lg border border-slate-200 shadow-xs">
          <div className="flex items-center justify-between text-rose-600 mb-2">
            <span className="text-xs font-semibold text-slate-600">{t("crm.sellerHome.kpiStaleDeals", "صفقات متوقفة")}</span>
            <Flame className="h-4 w-4 text-rose-500" />
          </div>
          <div className="text-2xl font-bold text-slate-900">
            {staleDeals.length}
          </div>
          <p className="text-xs text-slate-500 mt-1">
            {t("crm.sellerHome.kpiStaleDealsHint", "بلا نشاط مسجل لأكثر من 14 يوم")}
          </p>
        </div>

        {/* KPI 4: Open Pipeline Total */}
        <div className="bg-white p-4 rounded-lg border border-slate-200 shadow-xs">
          <div className="flex items-center justify-between text-emerald-600 mb-2">
            <span className="text-xs font-semibold text-slate-600">{t("crm.sellerHome.kpiOpenPipeline", "إجمالي قيد التفاوض")}</span>
            <DollarSign className="h-4 w-4 text-emerald-600" />
          </div>
          <div className="text-xl font-bold text-emerald-700 truncate">
            {formatCurrency(forecastData?.pipelineAmount || 0)}
          </div>
          <p className="text-xs text-slate-500 mt-1">
            {t("crm.sellerHome.kpiOpenPipelineHint", "قيمة الصفقات النشطة الخاصة بك")}
          </p>
        </div>
      </div>

      {/* Main Grid: Worklist on Left/Top, Attention Leads on Right/Bottom */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
        {/* Left Column (8 cols): Worklist of Follow-ups */}
        <div className="lg:col-span-7 space-y-4">
          <div className="bg-white rounded-lg border border-slate-200 shadow-xs overflow-hidden">
            <div className="p-4 bg-slate-50 border-b border-slate-200 flex items-center justify-between">
              <div className="flex items-center gap-2">
                <CalendarCheck className="h-5 w-5 text-primary-600" />
                <h3 className="text-sm font-bold text-slate-900">
                  {t("crm.sellerHome.worklistTitle", "متابعات ومهام اليوم")}
                </h3>
              </div>
              <div className="flex items-center gap-2">
                <button
                  type="button"
                  onClick={() => setNewActivityOpen(true)}
                  className="inline-flex items-center gap-1 text-xs px-2.5 py-1 bg-primary-600 hover:bg-primary-700 text-white rounded font-medium shadow-xs"
                >
                  <Plus className="h-3.5 w-3.5" />
                  <span>{t("crm.activities.createButton", "نشاط جديد")}</span>
                </button>
                <button
                  type="button"
                  onClick={onNavigateToActivities}
                  className="text-xs text-primary-600 hover:text-primary-800 font-semibold"
                >
                  {t("crm.common.viewAll", "عرض الكل")}
                </button>
              </div>
            </div>

            <div className="p-0 divide-y divide-slate-100">
              {loadingWorklist ? (
                <div className="p-8 text-center text-slate-400 text-sm">
                  {t("crm.common.loading", "جاري تحميل المهام...")}
                </div>
              ) : activities.length === 0 ? (
                <div className="p-8 text-center">
                  <CheckCircle2 className="mx-auto h-8 w-8 text-emerald-500 mb-2" />
                  <p className="text-sm font-medium text-slate-700">
                    {t("crm.sellerHome.noPendingActivities", "ممتاز! لا توجد مهام متأخرة أو مستحقة اليوم.")}
                  </p>
                  <p className="text-xs text-slate-400 mt-1">
                    {t("crm.sellerHome.allCaughtUp", "حافظ على زخم المبيعات بجدولة مكالمات أو عروض جديدة.")}
                  </p>
                </div>
              ) : (
                activities.slice(0, 5).map((act) => {
                  const isOverdue = act.dueAt && new Date(act.dueAt) < new Date();
                  return (
                    <div
                      key={act.id}
                      className="p-3.5 hover:bg-slate-50 transition-colors flex items-center justify-between gap-3"
                    >
                      <div className="flex items-start gap-3 min-w-0">
                        <div className="mt-0.5">
                          <ActivityTypeBadge type={act.type} />
                        </div>
                        <div className="min-w-0">
                          <div className="text-sm font-semibold text-slate-900 truncate">
                            {act.subject}
                          </div>
                          <div className="flex items-center gap-2 mt-1 text-xs text-slate-500">
                            {act.dueAt && (
                              <span className={`inline-flex items-center gap-1 ${isOverdue ? "text-rose-600 font-bold" : ""}`}>
                                <Clock className="h-3 w-3" />
                                {new Date(act.dueAt).toLocaleTimeString(i18n.language, { hour: "2-digit", minute: "2-digit" })}
                                {isOverdue && ` (${t("crm.activities.overdue", "متأخر")})`}
                              </span>
                            )}
                          </div>
                        </div>
                      </div>

                      <div className="flex items-center gap-1.5 shrink-0">
                        <button
                          type="button"
                          onClick={() => setCompleteActivity(act)}
                          className="inline-flex items-center gap-1 px-2.5 py-1 text-xs font-semibold rounded bg-emerald-50 text-emerald-700 hover:bg-emerald-100 border border-emerald-200"
                        >
                          <CheckCircle2 className="h-3.5 w-3.5" />
                          <span>{t("crm.activities.completeAction", "إكمال")}</span>
                        </button>
                      </div>
                    </div>
                  );
                })
              )}
            </div>
          </div>

          {/* Stale Opportunities Alert Section */}
          <div className="bg-white rounded-lg border border-slate-200 shadow-xs overflow-hidden">
            <div className="p-4 bg-slate-50 border-b border-slate-200 flex items-center justify-between">
              <div className="flex items-center gap-2">
                <Flame className="h-5 w-5 text-rose-500" />
                <h3 className="text-sm font-bold text-slate-900">
                  {t("crm.sellerHome.staleDealsTitle", "صفقات متوقفة بحاجة لتحريك")}
                </h3>
              </div>
              <button
                type="button"
                onClick={onNavigateToOpportunities}
                className="text-xs text-primary-600 hover:text-primary-800 font-semibold"
              >
                {t("crm.common.viewPipeline", "عرض المسار")}
              </button>
            </div>

            <div className="p-0 divide-y divide-slate-100">
              {loadingInspection ? (
                <div className="p-8 text-center text-slate-400 text-sm">
                  {t("crm.common.loading", "جاري فحص المسار...")}
                </div>
              ) : staleDeals.length === 0 ? (
                <div className="p-6 text-center text-xs text-slate-500">
                  {t("crm.sellerHome.noStaleDeals", "جميع الصفقات في المسار تتم متابعتها بانتظام.")}
                </div>
              ) : (
                staleDeals.slice(0, 4).map((opp) => (
                  <div
                    key={opp.id}
                    onClick={() => setSelectedOpportunity(opp)}
                    className="p-3 hover:bg-slate-50 transition-colors flex items-center justify-between gap-3 cursor-pointer"
                  >
                    <div className="min-w-0">
                      <div className="text-sm font-semibold text-slate-900 truncate">
                        {opp.name}
                      </div>
                      <div className="flex items-center gap-2 mt-1 text-xs text-slate-500">
                        <span className="font-bold text-slate-700">
                          {formatCurrency(opp.estimatedAmount)}
                        </span>
                        <span>•</span>
                        <DealHealthBadge status={opp.healthStatus} score={opp.healthScore} />
                      </div>
                    </div>
                    <div>
                      <span className="text-xs font-semibold text-primary-600 hover:underline">
                        {t("crm.common.openWorkspace", "فتح")}
                      </span>
                    </div>
                  </div>
                ))
              )}
            </div>
          </div>
        </div>

        {/* Right Column (5 cols): Attention Leads */}
        <div className="lg:col-span-5 space-y-4">
          <div className="bg-white rounded-lg border border-slate-200 shadow-xs overflow-hidden">
            <div className="p-4 bg-slate-50 border-b border-slate-200 flex items-center justify-between">
              <div className="flex items-center gap-2">
                <AlertTriangle className="h-5 w-5 text-amber-500" />
                <h3 className="text-sm font-bold text-slate-900">
                  {t("crm.sellerHome.attentionLeadsTitle", "عملاء محتملون بحاجة لمتابعة")}
                </h3>
              </div>
              <button
                type="button"
                onClick={onNavigateToLeads}
                className="text-xs text-primary-600 hover:text-primary-800 font-semibold"
              >
                {t("crm.common.viewInbox", "صندوق العملاء")}
              </button>
            </div>

            <div className="p-0 divide-y divide-slate-100">
              {loadingAttention ? (
                <div className="p-8 text-center text-slate-400 text-sm">
                  {t("crm.common.loading", "جاري التحميل...")}
                </div>
              ) : leads.length === 0 ? (
                <div className="p-8 text-center">
                  <UserCheck className="mx-auto h-8 w-8 text-slate-300 mb-2" />
                  <p className="text-sm font-medium text-slate-700">
                    {t("crm.sellerHome.noAttentionLeads", "لا يوجد عملاء جدد عالقون بدون تواصل.")}
                  </p>
                </div>
              ) : (
                leads.slice(0, 6).map((ld: Activity) => (
                  <div
                    key={ld.id}
                    className="p-3.5 hover:bg-slate-50 transition-colors flex items-center justify-between gap-2"
                  >
                    <div className="min-w-0">
                      <div className="text-sm font-semibold text-slate-900 truncate">
                        {ld.subject}
                      </div>
                      <div className="text-xs text-slate-500 truncate">
                        {ld.notes || t("crm.leads.noContact", "لا توجد تفاصيل")}
                      </div>
                    </div>

                    <div>
                      <button
                        type="button"
                        onClick={() => setCompleteActivity(ld)}
                        className="px-2.5 py-1 text-xs font-semibold rounded bg-primary-50 text-primary-700 hover:bg-primary-100 border border-primary-200 cursor-pointer"
                      >
                        {t("crm.activities.completeAction", "إكمال")}
                      </button>
                    </div>
                  </div>
                ))
              )}
            </div>
          </div>
        </div>
      </div>

      {/* Modals */}
      {completeActivity && (
        <CompleteActivityModal
          isOpen={!!completeActivity}
          onClose={() => setCompleteActivity(null)}
          legalEntityId={legalEntityId}
          activity={completeActivity}
        />
      )}

      {newActivityOpen && (
        <NewActivityModal
          isOpen={newActivityOpen}
          onClose={() => setNewActivityOpen(false)}
          legalEntityId={legalEntityId}
        />
      )}

      {qualifyLead && (
        <QualifyLeadModal
          isOpen={!!qualifyLead}
          onClose={() => setQualifyLead(null)}
          legalEntityId={legalEntityId}
          lead={qualifyLead}
        />
      )}

      {selectedOpportunity && (
        <OpportunityWorkspaceModal
          isOpen={!!selectedOpportunity}
          onClose={() => setSelectedOpportunity(null)}
          legalEntityId={legalEntityId}
          opportunity={selectedOpportunity}
        />
      )}
    </div>
  );
};

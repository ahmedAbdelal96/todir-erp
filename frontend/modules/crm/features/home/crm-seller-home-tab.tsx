import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import {
  useCrmSellerWorklistQuery,
  useLeads,
  useCrmPipelineInspectionQuery,
  useCrmForecastQuery,
} from "../../hooks/use-crm-queries.ts";
import { Activity, Lead, Opportunity } from "../../types.ts";
import { ActivityTypeBadge } from "../../components/activity-type-badge.tsx";
import { DealHealthBadge } from "../../components/deal-health-badge.tsx";
import { PriorityBadge } from "../../components/priority-badge.tsx";
import { CrmKpiCard } from "../../components/crm-kpi-card.tsx";
import { CompleteActivityModal } from "../activities/complete-activity-modal.tsx";
import { NewActivityModal } from "../activities/new-activity-modal.tsx";
import { QualifyLeadModal } from "../leads/qualify-lead-modal.tsx";
import { OpportunityWorkspaceModal } from "../opportunities/opportunity-workspace-modal.tsx";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import {
  CalendarCheck,
  AlertTriangle,
  Flame,
  CheckCircle2,
  Clock,
  TrendingUp,
  DollarSign,
  Plus,
} from "../../components/crm-icons.tsx";

interface CrmSellerHomeTabProps {
  readonly legalEntityId: string;
  readonly onNavigateToLeads: () => void;
  readonly onNavigateToOpportunities: () => void;
  readonly onNavigateToActivities: () => void;
}

export const CrmSellerHomeTab: React.FC<CrmSellerHomeTabProps> = ({
  legalEntityId,
  onNavigateToLeads,
  onNavigateToOpportunities,
  onNavigateToActivities,
}) => {
  const { t, i18n } = useTranslation("crm");

  // Modal states
  const [completeActivity, setCompleteActivity] = useState<Activity | null>(null);
  const [newActivityOpen, setNewActivityOpen] = useState(false);
  const [qualifyLead, setQualifyLead] = useState<Lead | null>(null);
  const [selectedOpportunity, setSelectedOpportunity] = useState<Opportunity | null>(null);

  // Queries
  const { data: forecastData } = useCrmForecastQuery(legalEntityId);
  const { data: worklistActivities, isLoading: loadingWorklist } = useCrmSellerWorklistQuery(legalEntityId);
  const { data: newLeadsData, isLoading: loadingAttention } = useLeads(legalEntityId, { status: "New", pageSize: 6 });
  const { data: inspectionDeals, isLoading: loadingInspection } = useCrmPipelineInspectionQuery(legalEntityId);

  const formatCurrency = (val: number) => {
    return new Intl.NumberFormat(i18n.language, {
      style: "currency",
      currency: "EGP",
      maximumFractionDigits: 0,
    }).format(val || 0);
  };

  const activities = worklistActivities?.items || [];
  const leads = newLeadsData?.items || [];
  const staleDeals = inspectionDeals?.items || [];

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
      {/* KPI Cards Row - Responsive Auto-fit Grid */}
      <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(240px, 1fr))", gap: "1rem" }}>
        {/* KPI 1: Due & Overdue Tasks */}
        <CrmKpiCard
          title={t("crm.sellerHome.kpiDueTasks", "متابعات اليوم")}
          value={activities.length}
          subtitle={t("crm.sellerHome.kpiDueTasksHint", "تتطلب اتصال أو مقابلة اليوم")}
          tone="primary"
          icon={<CalendarCheck style={{ width: "1.125rem", height: "1.125rem" }} />}
          onClick={onNavigateToActivities}
        />

        {/* KPI 2: Attention Leads */}
        <CrmKpiCard
          title={t("crm.sellerHome.kpiAttentionLeads", "عملاء بحاجة لمتابعة")}
          value={leads.length}
          subtitle={t("crm.sellerHome.kpiAttentionLeadsHint", "عملاء جدد لم يُتصل بهم بعد")}
          tone="warning"
          icon={<AlertTriangle style={{ width: "1.125rem", height: "1.125rem" }} />}
          onClick={onNavigateToLeads}
        />

        {/* KPI 3: Pipeline Stale Deals */}
        <CrmKpiCard
          title={t("crm.sellerHome.kpiStaleDeals", "صفقات متوقفة")}
          value={staleDeals.length}
          subtitle={t("crm.sellerHome.kpiStaleDealsHint", "بلا نشاط مسجل لأكثر من 14 يوم")}
          tone="danger"
          icon={<Flame style={{ width: "1.125rem", height: "1.125rem" }} />}
          onClick={onNavigateToOpportunities}
        />

        {/* KPI 4: Open Pipeline Total */}
        <CrmKpiCard
          title={t("crm.sellerHome.kpiOpenPipeline", "إجمالي قيد التفاوض")}
          value={<bdi>{formatCurrency(forecastData?.pipelineAmount || 0)}</bdi>}
          subtitle={t("crm.sellerHome.kpiOpenPipelineHint", "قيمة الصفقات النشطة الخاصة بك")}
          tone="success"
          icon={<DollarSign style={{ width: "1.125rem", height: "1.125rem" }} />}
          onClick={onNavigateToOpportunities}
        />
      </div>

      {/* Main Grid: Worklist on Left, Attention Leads & Stale Deals on Right */}
      <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(360px, 1fr))", gap: "1.5rem", alignItems: "start" }}>
        {/* Left Column: Worklist of Follow-ups */}
        <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
          <Surface padding="none" variant="card">
            {/* Header */}
            <div
              style={{
                padding: "1rem 1.25rem",
                backgroundColor: "var(--color-surface-subtle, #f8fafc)",
                borderBottom: "1px solid var(--color-border-default, #cbd5e1)",
                display: "flex",
                alignItems: "center",
                justifyContent: "space-between",
                flexWrap: "wrap",
                gap: "0.75rem"
              }}
            >
              <div style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
                <CalendarCheck style={{ width: "1.25rem", height: "1.25rem", color: "var(--color-brand-primary, #102a43)" }} />
                <h2 style={{ margin: 0, fontSize: "0.9375rem", fontWeight: 700, color: "var(--color-content-primary, #0f172a)" }}>
                  {t("crm.sellerHome.worklistTitle", "متابعات ومهام اليوم")}
                </h2>
              </div>
              <div style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
                <Button variant="primary" size="compact" onPress={() => setNewActivityOpen(true)}>
                  + {t("crm.activities.createButton", "نشاط جديد")}
                </Button>
                <Button variant="secondary" size="compact" onPress={onNavigateToActivities}>
                  {t("crm.common.viewAll", "عرض الكل")}
                </Button>
              </div>
            </div>

            {/* Content List */}
            <div style={{ display: "flex", flexDirection: "column" }}>
              {loadingWorklist ? (
                <div style={{ padding: "2rem", textAlign: "center", color: "var(--color-content-muted, #64748b)", fontSize: "0.875rem" }}>
                  {t("crm.common.loading", "جاري تحميل المهام...")}
                </div>
              ) : activities.length === 0 ? (
                <div style={{ padding: "2.5rem 1.5rem", textAlign: "center" }}>
                  <CheckCircle2 style={{ margin: "0 auto 0.5rem", width: "2rem", height: "2rem", color: "var(--color-status-success, #059669)" }} />
                  <p style={{ margin: 0, fontSize: "0.875rem", fontWeight: 600, color: "var(--color-content-primary, #0f172a)" }}>
                    {t("crm.sellerHome.noPendingActivities", "ممتاز! لا توجد مهام متأخرة أو مستحقة اليوم.")}
                  </p>
                  <p style={{ margin: "0.25rem 0 0", fontSize: "0.75rem", color: "var(--color-content-muted, #64748b)" }}>
                    {t("crm.sellerHome.allCaughtUp", "حافظ على زخم المبيعات بجدولة مكالمات أو عروض جديدة.")}
                  </p>
                </div>
              ) : (
                activities.slice(0, 5).map((act, index) => {
                  const isOverdue = act.dueAt && new Date(act.dueAt) < new Date();
                  return (
                    <div
                      key={act.id}
                      style={{
                        padding: "0.875rem 1.25rem",
                        borderBottom: index < activities.length - 1 ? "1px solid var(--color-border-subtle, #e2e8f0)" : "none",
                        display: "flex",
                        alignItems: "center",
                        justifyContent: "space-between",
                        gap: "0.75rem",
                        backgroundColor: isOverdue ? "rgba(254, 242, 242, 0.5)" : "transparent"
                      }}
                    >
                      <div style={{ display: "flex", alignItems: "flex-start", gap: "0.75rem", minWidth: 0, flex: 1 }}>
                        <div style={{ marginTop: "0.125rem" }}>
                          <ActivityTypeBadge type={act.type} />
                        </div>
                        <div style={{ minWidth: 0, flex: 1 }}>
                          <div style={{ fontSize: "0.875rem", fontWeight: 600, color: "var(--color-content-primary, #0f172a)", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                            {act.subject}
                          </div>
                          {act.dueAt && (
                            <div style={{ display: "flex", alignItems: "center", gap: "0.375rem", marginTop: "0.25rem", fontSize: "0.75rem", color: isOverdue ? "var(--color-status-danger, #dc2626)" : "var(--color-content-muted, #64748b)" }}>
                              <Clock style={{ width: "0.75rem", height: "0.75rem" }} />
                              <span style={{ fontWeight: isOverdue ? 700 : 400 }}>
                                {new Date(act.dueAt).toLocaleTimeString(i18n.language, { hour: "2-digit", minute: "2-digit" })}
                                {isOverdue && ` (${t("crm.activities.overdue", "متأخر")})`}
                              </span>
                            </div>
                          )}
                        </div>
                      </div>

                      <div style={{ flexShrink: 0 }}>
                        <Button
                          variant="secondary"
                          size="compact"
                          onPress={() => setCompleteActivity(act)}
                        >
                          <CheckCircle2 style={{ width: "0.875rem", height: "0.875rem", marginInlineEnd: "0.25rem", color: "var(--color-status-success, #059669)" }} />
                          {t("crm.activities.completeAction", "إكمال")}
                        </Button>
                      </div>
                    </div>
                  );
                })
              )}
            </div>
          </Surface>

          {/* Stale Deals Alert Card */}
          <Surface padding="none" variant="card">
            <div
              style={{
                padding: "1rem 1.25rem",
                backgroundColor: "var(--color-surface-subtle, #f8fafc)",
                borderBottom: "1px solid var(--color-border-default, #cbd5e1)",
                display: "flex",
                alignItems: "center",
                justifyContent: "space-between",
                flexWrap: "wrap",
                gap: "0.75rem"
              }}
            >
              <div style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
                <Flame style={{ width: "1.25rem", height: "1.25rem", color: "var(--color-status-danger, #dc2626)" }} />
                <h2 style={{ margin: 0, fontSize: "0.9375rem", fontWeight: 700, color: "var(--color-content-primary, #0f172a)" }}>
                  {t("crm.sellerHome.staleDealsTitle", "صفقات متوقفة بحاجة لتحريك")}
                </h2>
              </div>
              <Button variant="secondary" size="compact" onPress={onNavigateToOpportunities}>
                {t("crm.common.viewPipeline", "عرض المسار")}
              </Button>
            </div>

            <div style={{ display: "flex", flexDirection: "column" }}>
              {loadingInspection ? (
                <div style={{ padding: "2rem", textAlign: "center", color: "var(--color-content-muted, #64748b)", fontSize: "0.875rem" }}>
                  {t("crm.common.loading", "جاري فحص المسار...")}
                </div>
              ) : staleDeals.length === 0 ? (
                <div style={{ padding: "1.5rem", textAlign: "center", color: "var(--color-content-muted, #64748b)", fontSize: "0.8125rem" }}>
                  {t("crm.sellerHome.noStaleDeals", "جميع الصفقات في المسار تتم متابعتها بانتظام.")}
                </div>
              ) : (
                staleDeals.slice(0, 4).map((opp, index) => (
                  <div
                    key={opp.id}
                    onClick={() => setSelectedOpportunity(opp)}
                    style={{
                      padding: "0.875rem 1.25rem",
                      borderBottom: index < staleDeals.length - 1 ? "1px solid var(--color-border-subtle, #e2e8f0)" : "none",
                      display: "flex",
                      alignItems: "center",
                      justifyContent: "space-between",
                      gap: "0.75rem",
                      cursor: "pointer"
                    }}
                  >
                    <div style={{ minWidth: 0 }}>
                      <div style={{ fontSize: "0.875rem", fontWeight: 600, color: "var(--color-content-primary, #0f172a)", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                        {opp.name}
                      </div>
                      <div style={{ display: "flex", alignItems: "center", gap: "0.5rem", marginTop: "0.25rem", fontSize: "0.75rem", color: "var(--color-content-muted, #64748b)" }}>
                        <span style={{ fontWeight: 700, color: "var(--color-content-primary, #0f172a)" }}>
                          {formatCurrency(opp.estimatedAmount)}
                        </span>
                        <span>•</span>
                        <DealHealthBadge status={opp.healthStatus} score={opp.healthScore} />
                      </div>
                    </div>
                    <Button variant="secondary" size="compact" onPress={() => setSelectedOpportunity(opp)}>
                      {t("crm.common.openWorkspace", "فتح")}
                    </Button>
                  </div>
                ))
              )}
            </div>
          </Surface>
        </div>

        {/* Right Column: Attention Leads */}
        <div>
          <Surface padding="none" variant="card">
            <div
              style={{
                padding: "1rem 1.25rem",
                backgroundColor: "var(--color-surface-subtle, #f8fafc)",
                borderBottom: "1px solid var(--color-border-default, #cbd5e1)",
                display: "flex",
                alignItems: "center",
                justifyContent: "space-between",
                flexWrap: "wrap",
                gap: "0.75rem"
              }}
            >
              <div style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
                <AlertTriangle style={{ width: "1.25rem", height: "1.25rem", color: "var(--color-status-warning, #d97706)" }} />
                <h2 style={{ margin: 0, fontSize: "0.9375rem", fontWeight: 700, color: "var(--color-content-primary, #0f172a)" }}>
                  {t("crm.sellerHome.attentionLeadsTitle", "عملاء محتملون بحاجة لمتابعة")}
                </h2>
              </div>
              <Button variant="secondary" size="compact" onPress={onNavigateToLeads}>
                {t("crm.common.viewInbox", "صندوق العملاء")}
              </Button>
            </div>

            <div style={{ display: "flex", flexDirection: "column" }}>
              {loadingAttention ? (
                <div style={{ padding: "2rem", textAlign: "center", color: "var(--color-content-muted, #64748b)", fontSize: "0.875rem" }}>
                  {t("crm.common.loading", "جاري تحميل العملاء...")}
                </div>
              ) : leads.length === 0 ? (
                <div style={{ padding: "1.5rem", textAlign: "center", color: "var(--color-content-muted, #64748b)", fontSize: "0.8125rem" }}>
                  {t("crm.sellerHome.noAttentionLeads", "لا يوجد عملاء جدد عالقون بدون تواصل.")}
                </div>
              ) : (
                leads.slice(0, 6).map((lead, index) => (
                  <div
                    key={lead.id}
                    style={{
                      padding: "0.875rem 1.25rem",
                      borderBottom: index < leads.length - 1 ? "1px solid var(--color-border-subtle, #e2e8f0)" : "none",
                      display: "flex",
                      alignItems: "center",
                      justifyContent: "space-between",
                      gap: "0.75rem"
                    }}
                  >
                    <div style={{ minWidth: 0, flex: 1 }}>
                      <div style={{ fontSize: "0.875rem", fontWeight: 600, color: "var(--color-content-primary, #0f172a)", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                        {lead.displayName}
                      </div>
                      <div style={{ display: "flex", alignItems: "center", gap: "0.5rem", marginTop: "0.25rem", fontSize: "0.75rem", color: "var(--color-content-muted, #64748b)" }}>
                        {lead.companyName && <span>{lead.companyName}</span>}
                        {lead.companyName && <span>•</span>}
                        <PriorityBadge priority={lead.priority} />
                      </div>
                    </div>

                    <div style={{ flexShrink: 0 }}>
                      <Button
                        variant="secondary"
                        size="compact"
                        onPress={() => setQualifyLead(lead)}
                      >
                        {t("leads.actions.qualify", "تأهيل")}
                      </Button>
                    </div>
                  </div>
                ))
              )}
            </div>
          </Surface>
        </div>
      </div>

      {/* Active Workspace / Action Modals */}
      {completeActivity && (
        <CompleteActivityModal
          isOpen={Boolean(completeActivity)}
          onClose={() => setCompleteActivity(null)}
          activity={completeActivity}
          legalEntityId={legalEntityId}
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
          isOpen={Boolean(qualifyLead)}
          onClose={() => setQualifyLead(null)}
          lead={qualifyLead}
          legalEntityId={legalEntityId}
        />
      )}

      {selectedOpportunity && (
        <OpportunityWorkspaceModal
          isOpen={Boolean(selectedOpportunity)}
          onClose={() => setSelectedOpportunity(null)}
          opportunity={selectedOpportunity}
          legalEntityId={legalEntityId}
        />
      )}
    </div>
  );
};

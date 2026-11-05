import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { useCrmActivitiesQuery, useCancelActivityMutation } from "../../hooks/use-crm-queries.ts";
import { Activity, ActivityState } from "../../types.ts";
import { ActivityTypeBadge } from "../../components/activity-type-badge.tsx";
import { NewActivityModal } from "./new-activity-modal.tsx";
import { CompleteActivityModal } from "./complete-activity-modal.tsx";
import { FilterBar } from "../../../../components/primitives/filter-bar.tsx";
import { SearchField } from "../../../../components/primitives/search-field.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { Table, TableHeader, TableRow, TableHead, TableBody, TableCell } from "../../../../components/primitives/table.tsx";
import { Pagination } from "../../../../components/primitives/pagination.tsx";
import { CheckCircle2, XCircle, Clock } from "../../components/crm-icons.tsx";

interface CrmActivitiesTabProps {
  readonly legalEntityId: string;
  readonly canManage?: boolean;
}

export const CrmActivitiesTab: React.FC<CrmActivitiesTabProps> = ({
  legalEntityId,
  canManage = false,
}) => {
  const { t, i18n } = useTranslation("crm");

  // Filter & Pagination States
  const [scope, setScope] = useState<"mine" | "all">("mine");
  const [stateFilter, setStateFilter] = useState<string>("Pending");
  const [searchTerm, setSearchTerm] = useState<string>("");
  const [page, setPage] = useState<number>(1);
  const pageSize = 15;

  // Modals state
  const [isNewModalOpen, setIsNewModalOpen] = useState<boolean>(false);
  const [selectedActivityForComplete, setSelectedActivityForComplete] = useState<Activity | null>(null);

  // Queries & Mutations
  const { data, isLoading, isError } = useCrmActivitiesQuery(legalEntityId, {
    scope,
    state: stateFilter ? (stateFilter as ActivityState) : undefined,
    page,
    pageSize,
    search: searchTerm.trim() || undefined,
  });

  const [cancellingActivityId, setCancellingActivityId] = useState<string | null>(null);
  const cancelMutation = useCancelActivityMutation(legalEntityId, cancellingActivityId || "");

  const handleCancelActivity = (activity: Activity) => {
    if (!window.confirm(t("crm.activities.confirmCancel", "هل أنت متأكد من إلغاء هذا النشاط؟"))) {
      return;
    }
    setCancellingActivityId(activity.id);
    cancelMutation.mutate(activity.version);
  };

  const activities = data?.items || [];
  const totalCount = data?.totalCount || 0;
  const totalPages = Math.ceil(totalCount / pageSize) || 1;

  const stateTabs = [
    { id: "Pending", label: t("crm.activities.states.pending", "قيد الانتظار") },
    { id: "Completed", label: t("crm.activities.states.completed", "مكتملة") },
    { id: "", label: t("crm.activities.states.all", "الكل") },
  ];

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
      {/* Top Filter and Actions Bar */}
      <FilterBar>
        <div style={{ display: "flex", gap: "0.75rem", flex: 1, flexWrap: "wrap", alignItems: "center" }}>
          <div style={{ minWidth: "260px", maxWidth: "380px", flex: 1 }}>
            <SearchField
              value={searchTerm}
              onChange={(val) => {
                setSearchTerm(val);
                setPage(1);
              }}
              placeholder={t("crm.activities.searchPlaceholder", "بحث في الأنشطة والملاحظات...")}
            />
          </div>

          {/* State Filter Buttons */}
          <div style={{ display: "inline-flex", border: "1px solid var(--color-border-default)", borderRadius: "var(--radius-md)", overflow: "hidden" }}>
            {stateTabs.map((st, index) => {
              const isSelected = stateFilter === st.id;
              return (
                <button
                  key={st.id || "all"}
                  type="button"
                  onClick={() => { setStateFilter(st.id); setPage(1); }}
                  style={{
                    padding: "0.375rem 0.75rem",
                    fontSize: "0.8125rem",
                    fontWeight: 600,
                    border: "none",
                    borderInlineStart: index > 0 ? "1px solid var(--color-border-default)" : "none",
                    cursor: "pointer",
                    backgroundColor: isSelected ? "var(--color-surface-subtle, #f1f5f9)" : "var(--color-surface-card, #ffffff)",
                    color: isSelected ? "var(--color-brand-primary, #102a43)" : "var(--color-content-muted, #64748b)"
                  }}
                >
                  {st.label}
                </button>
              );
            })}
          </div>

          {/* Scope switch if manager */}
          {canManage && (
            <div style={{ display: "inline-flex", border: "1px solid var(--color-border-default)", borderRadius: "var(--radius-md)", overflow: "hidden" }}>
              <button
                type="button"
                onClick={() => { setScope("mine"); setPage(1); }}
                style={{
                  padding: "0.375rem 0.75rem",
                  fontSize: "0.8125rem",
                  fontWeight: 600,
                  border: "none",
                  cursor: "pointer",
                  backgroundColor: scope === "mine" ? "var(--color-surface-subtle)" : "var(--color-surface-card)",
                  color: scope === "mine" ? "var(--color-brand-primary)" : "var(--color-content-muted)"
                }}
              >
                {t("crm.common.myScope", "أنشطتي")}
              </button>
              <button
                type="button"
                onClick={() => { setScope("all"); setPage(1); }}
                style={{
                  padding: "0.375rem 0.75rem",
                  fontSize: "0.8125rem",
                  fontWeight: 600,
                  border: "none",
                  borderInlineStart: "1px solid var(--color-border-default)",
                  cursor: "pointer",
                  backgroundColor: scope === "all" ? "var(--color-surface-subtle)" : "var(--color-surface-card)",
                  color: scope === "all" ? "var(--color-brand-primary)" : "var(--color-content-muted)"
                }}
              >
                {t("crm.common.allTeamScope", "أنشطة الفريق")}
              </button>
            </div>
          )}
        </div>

        <Button variant="primary" size="comfortable" onPress={() => setIsNewModalOpen(true)}>
          + {t("crm.activities.createButton", "تسجيل نشاط جديد")}
        </Button>
      </FilterBar>

      {/* Activities Table */}
      <div style={{ background: "var(--color-surface-card)", border: "1px solid var(--color-border-default)", borderRadius: "var(--radius-md)", overflow: "hidden" }}>
        <Table aria-label={t("tabs.activities", "الأنشطة والمتابعات")}>
          <TableHeader>
            <TableRow>
              <TableHead>{t("crm.activities.typeAndSubject", "النوع والموضوع")}</TableHead>
              <TableHead>{t("crm.activities.linkedRecord", "الجهة المرتبطة")}</TableHead>
              <TableHead>{t("crm.activities.dueDate", "تاريخ الاستحقاق")}</TableHead>
              <TableHead>{t("crm.activities.status", "الحالة")}</TableHead>
              <TableHead style={{ textAlign: "end" }}>{t("crm.common.actions", "الإجراءات")}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {isLoading ? (
              <TableRow>
                <TableCell colSpan={5} style={{ textAlign: "center", padding: "2.5rem" }}>
                  {t("crm.common.loading", "جاري تحميل البيانات...")}
                </TableCell>
              </TableRow>
            ) : isError ? (
              <TableRow>
                <TableCell colSpan={5} style={{ textAlign: "center", padding: "2.5rem", color: "var(--color-status-danger)" }}>
                  {t("crm.common.errorLoading", "حدث خطأ أثناء تحميل الأنشطة.")}
                </TableCell>
              </TableRow>
            ) : activities.length === 0 ? (
              <TableRow>
                <TableCell colSpan={5} style={{ textAlign: "center", padding: "3rem 1.5rem" }}>
                  <Clock style={{ margin: "0 auto 0.75rem", width: "2.5rem", height: "2.5rem", color: "var(--color-content-muted)" }} />
                  <p style={{ margin: 0, fontWeight: 700, fontSize: "0.9375rem", color: "var(--color-content-primary)" }}>
                    {t("crm.activities.emptyTitle", "لا توجد أنشطة مجدولة")}
                  </p>
                  <p style={{ margin: "0.25rem 0 1rem", fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
                    {t("crm.activities.emptyDescription", "قم بجدولة اتصال أو اجتماع أو مهمة متابعة مع العميل لضمان سير الصفقة.")}
                  </p>
                  <Button variant="primary" size="compact" onPress={() => setIsNewModalOpen(true)}>
                    + {t("crm.activities.createFirst", "جدولة أول نشاط")}
                  </Button>
                </TableCell>
              </TableRow>
            ) : (
              activities.map((activity) => {
                const isPending = activity.state === "Pending" || (activity.state as string) === "Scheduled";
                const isOverdue = isPending && activity.dueAt && new Date(activity.dueAt) < new Date();

                return (
                  <TableRow key={activity.id} data-testid="crm-activity-row">
                    <TableCell>
                      <div style={{ display: "flex", alignItems: "center", gap: "0.625rem" }}>
                        <ActivityTypeBadge type={activity.type} />
                        <div>
                          <span style={{ fontWeight: 600, color: "var(--color-content-primary)" }}>
                            {activity.subject}
                          </span>
                          {activity.notes && (
                            <p style={{ margin: "0.125rem 0 0", fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
                              {activity.notes}
                            </p>
                          )}
                        </div>
                      </div>
                    </TableCell>

                    <TableCell>
                      {activity.leadId ? (
                        <span style={{ padding: "0.125rem 0.5rem", borderRadius: "var(--radius-sm)", fontSize: "0.75rem", fontWeight: 600, backgroundColor: "#fffbeb", color: "#92400e", border: "1px solid #fde68a" }}>
                          {t("crm.activities.leadContext", "عميل محتمل")}
                        </span>
                      ) : activity.opportunityId ? (
                        <span style={{ padding: "0.125rem 0.5rem", borderRadius: "var(--radius-sm)", fontSize: "0.75rem", fontWeight: 600, backgroundColor: "#eff6ff", color: "#1e40af", border: "1px solid #bfdbfe" }}>
                          {t("crm.activities.opportunityContext", "فرصة بيعية")}
                        </span>
                      ) : (
                        <span style={{ color: "var(--color-content-muted)" }}>—</span>
                      )}
                    </TableCell>

                    <TableCell style={{ whiteSpace: "nowrap" }}>
                      {activity.dueAt ? (
                        <span style={{ fontSize: "0.8125rem", fontWeight: isOverdue ? 700 : 500, color: isOverdue ? "var(--color-status-danger, #dc2626)" : "var(--color-content-primary)" }}>
                          {new Date(activity.dueAt).toLocaleDateString(i18n.language, {
                            year: "numeric",
                            month: "short",
                            day: "numeric",
                            hour: "2-digit",
                            minute: "2-digit",
                          })}
                          {isOverdue && (
                            <span style={{ marginInlineStart: "0.375rem", padding: "0.125rem 0.375rem", borderRadius: "var(--radius-sm)", fontSize: "0.6875rem", backgroundColor: "#fef2f2", color: "#dc2626" }}>
                              {t("crm.activities.overdue", "متأخر")}
                            </span>
                          )}
                        </span>
                      ) : (
                        <span style={{ color: "var(--color-content-muted)" }}>—</span>
                      )}
                    </TableCell>

                    <TableCell style={{ whiteSpace: "nowrap" }}>
                      <span
                        style={{
                          display: "inline-flex",
                          alignItems: "center",
                          gap: "0.25rem",
                          padding: "0.25rem 0.625rem",
                          borderRadius: "9999px",
                          fontSize: "0.75rem",
                          fontWeight: 600,
                          backgroundColor:
                            activity.state === "Completed" ? "var(--color-status-success-subtle, #ecfdf5)" :
                            activity.state === "Cancelled" ? "var(--color-surface-subtle, #f1f5f9)" :
                            "var(--color-status-info-subtle, #eff6ff)",
                          color:
                            activity.state === "Completed" ? "var(--color-status-success, #059669)" :
                            activity.state === "Cancelled" ? "var(--color-content-muted, #64748b)" :
                            "var(--color-brand-primary, #102a43)",
                          border: `1px solid ${
                            activity.state === "Completed" ? "#a7f3d0" :
                            activity.state === "Cancelled" ? "var(--color-border-default)" :
                            "#bfdbfe"
                          }`
                        }}
                      >
                        {activity.state === "Completed" && <CheckCircle2 style={{ width: "0.75rem", height: "0.75rem" }} />}
                        {activity.state === "Cancelled" && <XCircle style={{ width: "0.75rem", height: "0.75rem" }} />}
                        {(activity.state === "Pending" || (activity.state as string) === "Scheduled") && <Clock style={{ width: "0.75rem", height: "0.75rem" }} />}
                        {(activity.state as string) === "Scheduled" ? t("crm.activities.states.pending", "قيد الانتظار") : t(`crm.activities.states.${activity.state.toLowerCase()}`, activity.state)}
                      </span>
                    </TableCell>

                    <TableCell style={{ textAlign: "end", whiteSpace: "nowrap" }}>
                      {isPending && (
                        <div style={{ display: "inline-flex", gap: "0.375rem", alignItems: "center" }}>
                          <Button
                            variant="secondary"
                            size="compact"
                            onPress={() => setSelectedActivityForComplete(activity)}
                          >
                            <CheckCircle2 style={{ width: "0.75rem", height: "0.75rem", marginInlineEnd: "0.25rem", color: "var(--color-status-success)" }} />
                            {t("crm.activities.completeAction", "إكمال")}
                          </Button>
                          <Button
                            variant="secondary"
                            size="compact"
                            onPress={() => handleCancelActivity(activity)}
                          >
                            <XCircle style={{ width: "0.75rem", height: "0.75rem", marginInlineEnd: "0.25rem", color: "var(--color-status-danger)" }} />
                            {t("crm.activities.cancelAction", "إلغاء")}
                          </Button>
                        </div>
                      )}
                      {!isPending && activity.completedAt && (
                        <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
                          {t("crm.activities.completedAt", "أُنجز بتاريخ")}{" "}
                          {new Date(activity.completedAt).toLocaleDateString(i18n.language)}
                        </span>
                      )}
                    </TableCell>
                  </TableRow>
                );
              })
            )}
          </TableBody>
        </Table>

        {totalPages > 1 && (
          <div style={{ padding: "0.75rem 1.25rem", borderTop: "1px solid var(--color-border-default)" }}>
            <Pagination
              page={page}
              totalPages={totalPages}
              totalItems={totalCount}
              pageSize={pageSize}
              onPageChange={setPage}
            />
          </div>
        )}
      </div>

      {/* Modals */}
      {isNewModalOpen && (
        <NewActivityModal
          isOpen={isNewModalOpen}
          onClose={() => setIsNewModalOpen(false)}
          legalEntityId={legalEntityId}
        />
      )}

      {selectedActivityForComplete && (
        <CompleteActivityModal
          isOpen={Boolean(selectedActivityForComplete)}
          onClose={() => setSelectedActivityForComplete(null)}
          activity={selectedActivityForComplete}
          legalEntityId={legalEntityId}
        />
      )}
    </div>
  );
};

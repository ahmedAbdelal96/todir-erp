import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { useCrmActivitiesQuery, useCancelActivityMutation } from "../../hooks/use-crm-queries.ts";
import { Activity, ActivityState } from "../../types.ts";
import { ActivityTypeBadge } from "../../components/activity-type-badge.tsx";
import { NewActivityModal } from "./new-activity-modal.tsx";
import { CompleteActivityModal } from "./complete-activity-modal.tsx";
import { Search, Plus, CheckCircle2, XCircle, Clock } from "../../components/crm-icons.tsx";

interface CrmActivitiesTabProps {
  legalEntityId: string;
  canManage?: boolean;
}

export const CrmActivitiesTab: React.FC<CrmActivitiesTabProps> = ({
  legalEntityId,
  canManage = false,
}) => {
  const { t, i18n } = useTranslation("crm");
  const isRtl = i18n.dir() === "rtl";

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

  return (
    <div className="space-y-4">
      {/* Top Filter and Actions Bar */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-3 bg-white p-4 rounded-lg border border-slate-200 shadow-xs">
        <div className="flex flex-wrap items-center gap-2">
          {/* Search Box */}
          <div className="relative min-w-[240px]">
            <Search className={`absolute top-2.5 ${isRtl ? "right-3" : "left-3"} h-4 w-4 text-slate-400`} />
            <input
              type="text"
              value={searchTerm}
              onChange={(e) => {
                setSearchTerm(e.target.value);
                setPage(1);
              }}
              placeholder={t("crm.activities.searchPlaceholder", "بحث في الأنشطة والملاحظات...")}
              className={`w-full py-1.5 text-sm bg-slate-50 border border-slate-300 rounded-md focus:bg-white focus:outline-none focus:ring-2 focus:ring-primary-500 ${
                isRtl ? "pr-9 pl-3" : "pl-9 pr-3"
              }`}
            />
          </div>

          {/* State Filter Buttons */}
          <div className="inline-flex rounded-md border border-slate-200 p-0.5 bg-slate-100 text-xs">
            <button
              type="button"
              onClick={() => { setStateFilter("Pending"); setPage(1); }}
              className={`px-3 py-1 font-medium rounded-sm transition-colors ${
                stateFilter === "Pending" ? "bg-white text-slate-800 shadow-xs" : "text-slate-600 hover:text-slate-900"
              }`}
            >
              {t("crm.activities.states.pending", "قيد الانتظار")}
            </button>
            <button
              type="button"
              onClick={() => { setStateFilter("Completed"); setPage(1); }}
              className={`px-3 py-1 font-medium rounded-sm transition-colors ${
                stateFilter === "Completed" ? "bg-white text-slate-800 shadow-xs" : "text-slate-600 hover:text-slate-900"
              }`}
            >
              {t("crm.activities.states.completed", "مكتملة")}
            </button>
            <button
              type="button"
              onClick={() => { setStateFilter(""); setPage(1); }}
              className={`px-3 py-1 font-medium rounded-sm transition-colors ${
                stateFilter === "" ? "bg-white text-slate-800 shadow-xs" : "text-slate-600 hover:text-slate-900"
              }`}
            >
              {t("crm.activities.states.all", "الكل")}
            </button>
          </div>

          {/* Scope switch if manager */}
          {canManage && (
            <div className="inline-flex rounded-md border border-slate-200 p-0.5 bg-slate-100 text-xs">
              <button
                type="button"
                onClick={() => { setScope("mine"); setPage(1); }}
                className={`px-3 py-1 font-medium rounded-sm transition-colors ${
                  scope === "mine" ? "bg-white text-slate-800 shadow-xs" : "text-slate-600 hover:text-slate-900"
                }`}
              >
                {t("crm.common.myScope", "أنشطتي")}
              </button>
              <button
                type="button"
                onClick={() => { setScope("all"); setPage(1); }}
                className={`px-3 py-1 font-medium rounded-sm transition-colors ${
                  scope === "all" ? "bg-white text-slate-800 shadow-xs" : "text-slate-600 hover:text-slate-900"
                }`}
              >
                {t("crm.common.allTeamScope", "أنشطة الفريق")}
              </button>
            </div>
          )}
        </div>

        {/* Primary Action Button */}
        <div>
          <button
            type="button"
            onClick={() => setIsNewModalOpen(true)}
            className="inline-flex items-center gap-1.5 px-4 py-2 bg-primary-600 hover:bg-primary-700 text-white text-sm font-medium rounded-md shadow-xs transition-colors cursor-pointer"
          >
            <Plus className="h-4 w-4" />
            <span>{t("crm.activities.createButton", "تسجيل نشاط جديد")}</span>
          </button>
        </div>
      </div>

      {/* Activities Table */}
      <div className="bg-white rounded-lg border border-slate-200 shadow-xs overflow-hidden">
        {isLoading ? (
          <div className="p-8 text-center text-slate-500 text-sm">
            {t("crm.common.loading", "جاري تحميل البيانات...")}
          </div>
        ) : isError ? (
          <div className="p-8 text-center text-rose-600 text-sm font-medium">
            {t("crm.common.errorLoading", "حدث خطأ أثناء تحميل الأنشطة.")}
          </div>
        ) : activities.length === 0 ? (
          <div className="p-12 text-center">
            <Clock className="mx-auto h-12 w-12 text-slate-300 mb-3" />
            <h3 className="text-base font-semibold text-slate-800">
              {t("crm.activities.emptyTitle", "لا توجد أنشطة مجدولة")}
            </h3>
            <p className="text-sm text-slate-500 mt-1 max-w-sm mx-auto">
              {t("crm.activities.emptyDescription", "قم بجدولة اتصال أو اجتماع أو مهمة متابعة مع العميل لضمان سير الصفقة.")}
            </p>
            <button
              type="button"
              onClick={() => setIsNewModalOpen(true)}
              className="mt-4 inline-flex items-center gap-1.5 px-3.5 py-1.5 bg-primary-600 hover:bg-primary-700 text-white text-sm font-medium rounded-md shadow-xs transition-colors cursor-pointer"
            >
              <Plus className="h-4 w-4" />
              <span>{t("crm.activities.createFirst", "جدولة أول نشاط")}</span>
            </button>
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="min-w-full divide-y divide-slate-200 text-sm">
              <thead className="bg-slate-50 text-slate-700 font-medium">
                <tr>
                  <th scope="col" className="px-4 py-3 text-start">{t("crm.activities.typeAndSubject", "النوع والموضوع")}</th>
                  <th scope="col" className="px-4 py-3 text-start">{t("crm.activities.linkedRecord", "الجهة المرتبطة")}</th>
                  <th scope="col" className="px-4 py-3 text-start">{t("crm.activities.dueDate", "تاريخ الاستحقاق")}</th>
                  <th scope="col" className="px-4 py-3 text-start">{t("crm.activities.status", "الحالة")}</th>
                  <th scope="col" className="px-4 py-3 text-end">{t("crm.common.actions", "الإجراءات")}</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-200 bg-white">
                {activities.map((activity) => {
                  const isPending = activity.state === "Pending";
                  const isOverdue = isPending && activity.dueAt && new Date(activity.dueAt) < new Date();

                  return (
                    <tr key={activity.id} className="hover:bg-slate-50 transition-colors">
                      <td className="px-4 py-3">
                        <div className="flex items-center gap-2">
                          <ActivityTypeBadge type={activity.type} />
                          <div>
                            <div className="font-semibold text-slate-900">{activity.subject}</div>
                            {activity.notes && (
                              <div className="text-xs text-slate-500 line-clamp-1 max-w-xs">{activity.notes}</div>
                            )}
                          </div>
                        </div>
                      </td>
                      <td className="px-4 py-3">
                        {activity.leadId ? (
                          <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-amber-50 text-amber-700 border border-amber-200">
                            {t("crm.activities.leadContext", "عميل محتمل")}
                          </span>
                        ) : activity.opportunityId ? (
                          <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-sky-50 text-sky-700 border border-sky-200">
                            {t("crm.activities.opportunityContext", "فرصة بيعية")}
                          </span>
                        ) : (
                          <span className="text-xs text-slate-400">—</span>
                        )}
                      </td>
                      <td className="px-4 py-3 whitespace-nowrap">
                        {activity.dueAt ? (
                          <div className={`text-xs ${isOverdue ? "text-rose-600 font-bold" : "text-slate-700 font-medium"}`}>
                            {new Date(activity.dueAt).toLocaleDateString(i18n.language, {
                              year: "numeric",
                              month: "short",
                              day: "numeric",
                              hour: "2-digit",
                              minute: "2-digit",
                            })}
                            {isOverdue && (
                              <span className="ms-1.5 px-1.5 py-0.2 rounded text-[10px] bg-rose-100 text-rose-800">
                                {t("crm.activities.overdue", "متأخر")}
                              </span>
                            )}
                          </div>
                        ) : (
                          <span className="text-xs text-slate-400">—</span>
                        )}
                      </td>
                      <td className="px-4 py-3 whitespace-nowrap">
                        <span className={`inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-medium ${
                          activity.state === "Completed"
                            ? "bg-emerald-50 text-emerald-700 border border-emerald-200"
                            : activity.state === "Cancelled"
                            ? "bg-slate-100 text-slate-600 border border-slate-200 line-through"
                            : "bg-blue-50 text-blue-700 border border-blue-200"
                        }`}>
                          {activity.state === "Completed" && <CheckCircle2 className="h-3 w-3" />}
                          {activity.state === "Cancelled" && <XCircle className="h-3 w-3" />}
                          {activity.state === "Pending" && <Clock className="h-3 w-3" />}
                          {t(`crm.activities.states.${activity.state.toLowerCase()}`, activity.state)}
                        </span>
                      </td>
                      <td className="px-4 py-3 whitespace-nowrap text-end">
                        {isPending && (
                          <div className="flex items-center justify-end gap-1.5">
                            <button
                              type="button"
                              onClick={() => setSelectedActivityForComplete(activity)}
                              className="inline-flex items-center gap-1 px-2.5 py-1 text-xs font-semibold rounded bg-emerald-50 text-emerald-700 hover:bg-emerald-100 border border-emerald-200 transition-colors cursor-pointer"
                            >
                              <CheckCircle2 className="h-3.5 w-3.5" />
                              <span>{t("crm.activities.completeAction", "إكمال")}</span>
                            </button>
                            <button
                              type="button"
                              onClick={() => handleCancelActivity(activity)}
                              className="inline-flex items-center gap-1 px-2 py-1 text-xs font-medium rounded text-slate-600 hover:text-rose-600 hover:bg-rose-50 transition-colors cursor-pointer"
                              title={t("crm.activities.cancelAction", "إلغاء النشاط")}
                            >
                              <XCircle className="h-3.5 w-3.5" />
                            </button>
                          </div>
                        )}
                        {activity.state === "Completed" && activity.completedAt && (
                          <span className="text-[11px] text-slate-500">
                            {t("crm.activities.completedAt", "أُنجز بتاريخ")}{" "}
                            {new Date(activity.completedAt).toLocaleDateString(i18n.language)}
                          </span>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}

        {/* Pagination Bar */}
        {totalPages > 1 && (
          <div className="flex items-center justify-between px-4 py-3 border-t border-slate-200 bg-slate-50 text-xs text-slate-600">
            <div>
              {t("crm.common.pageInfo", "صفحة {{current}} من {{total}} (إجمالي {{count}})", {
                current: page,
                total: totalPages,
                count: totalCount,
              })}
            </div>
            <div className="flex items-center gap-1">
              <button
                type="button"
                disabled={page <= 1}
                onClick={() => setPage((p) => Math.max(1, p - 1))}
                className="px-2.5 py-1 rounded border border-slate-300 bg-white font-medium disabled:opacity-50 hover:bg-slate-50"
              >
                {t("crm.common.prev", "السابق")}
              </button>
              <button
                type="button"
                disabled={page >= totalPages}
                onClick={() => setPage((p) => p + 1)}
                className="px-2.5 py-1 rounded border border-slate-300 bg-white font-medium disabled:opacity-50 hover:bg-slate-50"
              >
                {t("crm.common.next", "التالي")}
              </button>
            </div>
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
          isOpen={!!selectedActivityForComplete}
          onClose={() => setSelectedActivityForComplete(null)}
          legalEntityId={legalEntityId}
          activity={selectedActivityForComplete}
        />
      )}
    </div>
  );
};

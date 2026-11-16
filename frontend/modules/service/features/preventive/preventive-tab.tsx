import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { Table } from "../../../../components/primitives/table.tsx";
import { ServiceLifecycleBadge } from "../../components/service-lifecycle-badge.tsx";
import {
  CalendarIcon,
  PlusIcon,
  RefreshIcon,
  WrenchIcon
} from "../../components/service-icons.tsx";
import {
  useMaintenancePlansQuery,
  useMaintenanceDueQuery,
  useGenerateMaintenanceDueMutation,
  useConvertDueToWorkOrderMutation
} from "../../hooks/use-service-queries.ts";
import { NewMaintenancePlanModal } from "../modals/new-maintenance-plan-modal.tsx";

export interface PreventiveTabProps {
  readonly legalEntityId: string;
}

export function PreventiveTab({ legalEntityId }: PreventiveTabProps): React.JSX.Element {
  const { t } = useTranslation("service");
  const [activeSection, setActiveSection] = useState<"plans" | "due">("plans");
  const [isNewPlanOpen, setIsNewPlanOpen] = useState(false);

  const plansQuery = useMaintenancePlansQuery(legalEntityId, { pageSize: 50 });
  const dueQuery = useMaintenanceDueQuery(legalEntityId, { pageSize: 50 });
  const generateDueMutation = useGenerateMaintenanceDueMutation(legalEntityId);
  const convertDueMutation = useConvertDueToWorkOrderMutation(legalEntityId);

  const plans = plansQuery.data?.items ?? [];
  const dueTasks = dueQuery.data?.items ?? [];

  const handleGenerateDue = async () => {
    try {
      await generateDueMutation.mutateAsync({
        legalEntityId,
        horizonDays: 30
      });
    } catch {}
  };

  const handleConvertDue = async (dueId: string) => {
    try {
      await convertDueMutation.mutateAsync({
        legalEntityId,
        dueId
      });
    } catch {}
  };

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
      {/* Ribbon Bar */}
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: "0.5rem" }}>
        <div style={{ display: "flex", gap: "0.5rem" }}>
          <Button
            variant={activeSection === "plans" ? "primary" : "secondary"}
            onClick={() => setActiveSection("plans")}
            startIcon={<CalendarIcon size={16} />}
          >
            {t("preventive.plansTitle")} ({plans.length})
          </Button>
          <Button
            variant={activeSection === "due" ? "primary" : "secondary"}
            onClick={() => setActiveSection("due")}
            startIcon={<WrenchIcon size={16} />}
          >
            {t("preventive.dueTasksTitle")} ({dueTasks.length})
          </Button>
        </div>

        <div style={{ display: "flex", gap: "0.5rem" }}>
          <Button
            variant="secondary"
            onClick={handleGenerateDue}
            isLoading={generateDueMutation.isPending}
            startIcon={<RefreshIcon size={16} />}
          >
            {t("preventive.generateDueButton")}
          </Button>
          <Button
            variant="primary"
            onClick={() => setIsNewPlanOpen(true)}
            startIcon={<PlusIcon size={16} />}
          >
            {t("preventive.newPlanTitle")}
          </Button>
        </div>
      </div>

      {activeSection === "plans" && (
        <Surface
          title={t("preventive.plansTitle")}
          description={t("preventive.plansDesc")}
          variant="card"
        >
          {plansQuery.isLoading ? (
            <div style={{ padding: "2rem", textAlign: "center", color: "#64748b" }}>
              {t("common.loading")}
            </div>
          ) : plans.length === 0 ? (
            <div style={{ padding: "3rem 1rem", textAlign: "center", color: "#64748b" }}>
              {t("preventive.noPlansFound")}
            </div>
          ) : (
            <div style={{ overflowX: "auto" }}>
              <Table isBordered isStriped>
                <thead>
                  <tr>
                    <th>{t("preventive.planCode")}</th>
                    <th>{t("preventive.planName")}</th>
                    <th>{t("preventive.scheduleType")}</th>
                    <th>{t("preventive.interval")}</th>
                    <th>{t("common.status")}</th>
                  </tr>
                </thead>
                <tbody>
                  {plans.map((p) => (
                    <tr key={p.id}>
                      <td><strong>{p.planCode || p.id.slice(0, 8)}</strong></td>
                      <td>{p.name}</td>
                      <td>{(t as any)(`preventive.scheduleTypes.${(p.scheduleType || p.triggerType || "calendar").toLowerCase()}`)}</td>
                      <td>
                        {p.triggerType === "Meter" || p.scheduleType === "meter"
                          ? `${p.meterThreshold || p.meterInterval || 0} ${t("preventive.meterUnits")}`
                          : `${p.intervalDays || p.calendarIntervalDays || 30} ${t("preventive.days")}`}
                      </td>
                      <td>
                        <ServiceLifecycleBadge state={p.state || (p.isActive ? "Active" : "Draft")} />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </Table>
            </div>
          )}
        </Surface>
      )}

      {activeSection === "due" && (
        <Surface
          title={t("preventive.dueTasksTitle")}
          description={t("preventive.dueTasksDesc")}
          variant="card"
        >
          {dueQuery.isLoading ? (
            <div style={{ padding: "2rem", textAlign: "center", color: "#64748b" }}>
              {t("common.loading")}
            </div>
          ) : dueTasks.length === 0 ? (
            <div style={{ padding: "3rem 1rem", textAlign: "center", color: "#64748b" }}>
              {t("preventive.noDueTasksFound")}
            </div>
          ) : (
            <div style={{ overflowX: "auto" }}>
              <Table isBordered isStriped>
                <thead>
                  <tr>
                    <th>{t("preventive.dueTask")}</th>
                    <th>{t("preventive.dueDate")}</th>
                    <th>{t("preventive.triggerReason")}</th>
                    <th>{t("common.status")}</th>
                    <th style={{ textAlign: "center" }}>{t("common.actions")}</th>
                  </tr>
                </thead>
                <tbody>
                  {dueTasks.map((d) => (
                    <tr key={d.id}>
                      <td><strong>{d.id.slice(0, 8)}</strong></td>
                      <td>{d.dueDateUtc || d.dueDate ? new Date(d.dueDateUtc || d.dueDate).toLocaleDateString() : "—"}</td>
                      <td>{d.triggerReason || d.reason || t("preventive.scheduledInterval", "فاصل زمني مجدول")}</td>
                      <td><ServiceLifecycleBadge state={d.state || d.status} /></td>
                      <td style={{ textAlign: "center" }}>
                        {(d.state === "Due" || d.status === "pending") && (
                          <Button
                            variant="primary"
                            size="compact"
                            onClick={() => handleConvertDue(d.id)}
                            isLoading={convertDueMutation.isPending}
                            startIcon={<WrenchIcon size={14} />}
                          >
                            {t("preventive.convertToWorkOrder")}
                          </Button>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </Table>
            </div>
          )}
        </Surface>
      )}

      {/* New Plan Modal */}
      {isNewPlanOpen && (
        <NewMaintenancePlanModal
          isOpen={isNewPlanOpen}
          onClose={() => setIsNewPlanOpen(false)}
          legalEntityId={legalEntityId}
        />
      )}
    </div>
  );
}

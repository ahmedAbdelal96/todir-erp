import React from "react";
import { useTranslation } from "react-i18next";
import { HrKpiCard } from "../../components/hr-kpi-card.tsx";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { StatusBadge } from "../../../../components/fields/status-badge.tsx";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "../../../../components/primitives/table.tsx";
import { LocalDateDisplay } from "../../../../components/fields/local-date-display.tsx";
import { useHrHomeSummary } from "../../hooks/use-hr-queries.ts";

export interface HrHomeTabProps {
  readonly legalEntityId: string;
  readonly onNavigateTab: (tabId: "directory" | "attendance" | "leave") => void;
  readonly onOpenNewEmployee: () => void;
}

export function HrHomeTab({ legalEntityId, onNavigateTab, onOpenNewEmployee }: HrHomeTabProps): React.JSX.Element {
  const { t } = useTranslation("hr");
  const homeQuery = useHrHomeSummary(legalEntityId);
  const data = homeQuery.data;

  const reminders = data?.reminders ?? [];

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
      {/* Quick Action Header */}
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: "1rem" }}>
        <div>
          <h2 style={{ margin: 0, fontSize: "1.25rem", color: "var(--color-content-primary)" }}>
            {t("home.title")}
          </h2>
          <p style={{ margin: "0.25rem 0 0", fontSize: "0.875rem", color: "var(--color-content-muted)" }}>
            {t("home.subtitle")}
          </p>
        </div>
        <div style={{ display: "flex", gap: "0.75rem" }}>
          <Button variant="primary" onPress={onOpenNewEmployee}>
            + {t("home.actions.newEmployee")}
          </Button>
          <Button variant="secondary" onPress={() => onNavigateTab("attendance")}>
            {t("home.actions.viewAttendance")}
          </Button>
        </div>
      </div>

      {/* Primary KPI Row */}
      <div style={{ display: "flex", flexWrap: "wrap", gap: "1rem" }}>
        <HrKpiCard
          title={t("home.kpi.activeEmployees")}
          value={data?.activeEmployeesCount ?? 0}
          subtitle={t("home.kpi.activeEmployeesDesc")}
          tone="primary"
        />
        <HrKpiCard
          title={t("home.kpi.pendingLeave")}
          value={data?.pendingLeaveApprovalsCount ?? 0}
          subtitle={t("home.kpi.pendingLeaveDesc")}
          tone={data?.pendingLeaveApprovalsCount ? "warning" : "default"}
        />
        <HrKpiCard
          title={t("home.kpi.missingAttendance")}
          value={data?.missingAttendanceCount ?? 0}
          subtitle={t("home.kpi.missingAttendanceDesc")}
          tone={data?.missingAttendanceCount ? "danger" : "default"}
        />
        <HrKpiCard
          title={t("home.kpi.expiringDocs")}
          value={data?.expiringDocumentsCount ?? 0}
          subtitle={t("home.kpi.expiringDocsDesc")}
          tone={data?.expiringDocumentsCount ? "warning" : "default"}
        />
      </div>

      {/* Critical Operational Reminders & Compliance Table */}
      <Surface title={t("home.reminders.title")}>
        {reminders.length === 0 ? (
          <p style={{ margin: "1rem 0", color: "var(--color-content-muted)", fontSize: "0.875rem" }}>
            {t("home.reminders.empty")}
          </p>
        ) : (
          <Table aria-label={t("home.reminders.title")}>
            <TableHeader>
              <TableRow>
                <TableHead>{t("home.reminders.category")}</TableHead>
                <TableHead>{t("home.reminders.task")}</TableHead>
                <TableHead>{t("home.reminders.employee")}</TableHead>
                <TableHead>{t("home.reminders.dueDate")}</TableHead>
                <TableHead>{t("home.reminders.severity")}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {reminders.map((rem) => (
                <TableRow key={rem.id}>
                  <TableCell>
                    {t(`home.reminders.categories.${rem.category}`, { defaultValue: rem.category })}
                  </TableCell>
                  <TableCell style={{ fontWeight: 600 }}>{rem.title}</TableCell>
                  <TableCell>{rem.employeeName ?? "—"}</TableCell>
                  <TableCell>
                    <LocalDateDisplay value={rem.dueDate} />
                  </TableCell>
                  <TableCell>
                    <StatusBadge
                      intent={rem.severity}
                      label={t(`home.reminders.severities.${rem.severity}`, { defaultValue: rem.severity })}
                    />
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </Surface>
    </div>
  );
}

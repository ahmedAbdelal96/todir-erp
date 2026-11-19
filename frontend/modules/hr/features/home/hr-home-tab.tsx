import React from "react";
import { useTranslation } from "react-i18next";
import { HrKpiCard } from "../../components/hr-kpi-card.tsx";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { StatusBadge } from "../../../../components/fields/status-badge.tsx";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "../../../../components/primitives/table.tsx";
import { LocalDateDisplay } from "../../../../components/fields/local-date-display.tsx";
import { BidiText } from "../../../../components/primitives/bidi-text.tsx";
import { formatDecimalNumber } from "../../../../components/formatting/decimal-parser.ts";
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
  const activeEmployees = data?.headcount ?? data?.activeEmployeesCount ?? null;
  const pendingLeaves = data?.pendingLeaveApprovals ?? data?.pendingLeaveApprovalsCount ?? null;
  const attendanceExceptions = data?.openAttendanceExceptions ?? data?.missingAttendanceCount ?? null;
  const expiringDocs = data?.documentsExpiringWithin30Days ?? data?.expiringDocumentsCount ?? null;
  const expiringContracts = data?.contractsExpiringWithin30Days ?? data?.expiringContractsCount ?? null;
  const formatCount = (value: number | null): string =>
    value === null
      ? "—"
      : formatDecimalNumber(String(value), {
          // Keep operational counts visually stable in RTL layouts; BidiText isolates them from Arabic copy.
          locale: "en-US",
          minFractionDigits: 0,
          maxFractionDigits: 0,
          useGrouping: true
        });

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

      {/* Primary KPI Row: keep the first glance focused on three decisions. */}
      <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))", gap: "1rem" }}>
        <HrKpiCard
          title={t("home.kpi.activeEmployees")}
          value={formatCount(activeEmployees)}
          subtitle={t("home.kpi.activeEmployeesDesc")}
          tone="primary"
          testId="hr-kpi-active-employees"
        />
        <HrKpiCard
          title={t("home.kpi.pendingLeave")}
          value={formatCount(pendingLeaves)}
          subtitle={t("home.kpi.pendingLeaveDesc")}
          tone={pendingLeaves !== null && pendingLeaves > 0 ? "warning" : "default"}
          testId="hr-kpi-pending-leave"
        />
        <HrKpiCard
          title={t("home.kpi.missingAttendance")}
          value={formatCount(attendanceExceptions)}
          subtitle={t("home.kpi.missingAttendanceDesc")}
          tone={attendanceExceptions !== null && attendanceExceptions > 0 ? "danger" : "default"}
          testId="hr-kpi-attendance-exceptions"
        />
      </div>

      <Surface title={t("home.compliance.title")} description={t("home.compliance.description")}>
        {expiringDocs === null && expiringContracts === null ? (
          <p style={{ margin: 0, color: "var(--color-content-muted)", fontSize: "0.875rem" }}>
            {t("home.compliance.noData")}
          </p>
        ) : (
          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))", gap: "0.75rem" }}>
            {expiringDocs !== null && (
              <div data-testid="hr-compliance-expiring-documents" style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: "1rem", padding: "0.875rem 1rem", border: "1px solid var(--color-border-subtle)", borderRadius: "var(--radius-md)", backgroundColor: "var(--color-surface-subtle)" }}>
                <div style={{ display: "flex", flexDirection: "column", gap: "0.25rem" }}>
                  <span style={{ fontWeight: 600, color: "var(--color-content-primary)" }}>{t("home.compliance.expiringDocuments")}</span>
                  <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>{t("home.kpi.expiringDocsDesc")}</span>
                </div>
                <strong style={{ fontSize: "1.5rem", color: expiringDocs > 0 ? "var(--color-status-warning)" : "var(--color-content-primary)" }}>
                  <BidiText direction="ltr">{formatCount(expiringDocs)}</BidiText>
                </strong>
              </div>
            )}
            {expiringContracts !== null && (
              <div data-testid="hr-compliance-expiring-contracts" style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: "1rem", padding: "0.875rem 1rem", border: "1px solid var(--color-border-subtle)", borderRadius: "var(--radius-md)", backgroundColor: "var(--color-surface-subtle)" }}>
                <div style={{ display: "flex", flexDirection: "column", gap: "0.25rem" }}>
                  <span style={{ fontWeight: 600, color: "var(--color-content-primary)" }}>{t("home.compliance.expiringContracts")}</span>
                  <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>{t("home.compliance.expiringContractsDesc")}</span>
                </div>
                <strong style={{ fontSize: "1.5rem", color: expiringContracts > 0 ? "var(--color-status-warning)" : "var(--color-content-primary)" }}>
                  <BidiText direction="ltr">{formatCount(expiringContracts)}</BidiText>
                </strong>
              </div>
            )}
          </div>
        )}
      </Surface>

      {/* Critical Operational Reminders & Compliance Table */}
      <Surface title={t("home.reminders.title")}>
        {homeQuery.isPending ? (
          <p role="status" style={{ margin: "1rem 0", color: "var(--color-content-muted)", fontSize: "0.875rem" }}>
            {t("home.states.loading")}
          </p>
        ) : homeQuery.isError ? (
          <div role="alert" style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: "1rem", flexWrap: "wrap" }}>
            <p style={{ margin: 0, color: "var(--color-status-danger)", fontSize: "0.875rem" }}>
              {t("home.states.error")}
            </p>
            <Button variant="secondary" size="compact" onPress={() => void homeQuery.refetch()}>
              {t("actions.retry")}
            </Button>
          </div>
        ) : reminders.length === 0 ? (
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

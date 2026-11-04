import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { StatusBadge } from "../../../../components/fields/status-badge.tsx";
import {
  usePayrollGroups,
  usePayrollPeriods,
  usePayrollComponents,
  useSalaryStructures,
  useEmployeeAssignments
} from "../../hooks/use-payroll-queries.ts";
import { NewPayrollGroupModal } from "./new-payroll-group-modal.tsx";
import { NewPayrollPeriodModal } from "./new-payroll-period-modal.tsx";
import type { PayrollGroup, PayrollPeriod, PayrollComponent, SalaryStructure, EmployeePayrollAssignment } from "../../types.ts";

export interface PayrollSetupTabProps {
  readonly legalEntityId: string;
}

export function PayrollSetupTab({ legalEntityId }: PayrollSetupTabProps): React.JSX.Element {
  const { t } = useTranslation("payroll");

  const [activeSubTab, setActiveSubTab] = useState<"groups" | "components" | "structures" | "assignments">("groups");
  const [selectedGroupId, setSelectedGroupId] = useState<string | null>(null);
  const [isNewGroupOpen, setIsNewGroupOpen] = useState(false);
  const [isNewPeriodOpen, setIsNewPeriodOpen] = useState(false);

  const groupsQuery = usePayrollGroups(legalEntityId);
  const groups = groupsQuery.data ?? [];

  const activeGroupId = selectedGroupId ?? groups[0]?.id ?? "";
  const periodsQuery = usePayrollPeriods(legalEntityId, activeGroupId);
  const periods = periodsQuery.data ?? [];

  const componentsQuery = usePayrollComponents(legalEntityId);
  const components = componentsQuery.data ?? [];

  const structuresQuery = useSalaryStructures(legalEntityId);
  const structures = structuresQuery.data ?? [];

  const assignmentsQuery = useEmployeeAssignments(legalEntityId);
  const assignments = assignmentsQuery.data ?? [];

  const subTabs = [
    { id: "groups", label: t("setup.groupsSectionTitle", "مجموعات وفترات الرواتب") },
    { id: "components", label: t("setup.componentsSectionTitle", "بنود الراتب (Components)") },
    { id: "structures", label: t("setup.salaryStructuresSectionTitle", "هياكل الرواتب (Structures)") },
    { id: "assignments", label: t("setup.assignmentsSectionTitle", "تسكين الموظفين (Assignments)") }
  ] as const;

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
      {/* Sub navigation pills */}
      <div
        role="tablist"
        style={{
          display: "flex",
          gap: "0.5rem",
          padding: "0.25rem",
          background: "var(--color-surface-subtle, #f1f5f9)",
          borderRadius: "var(--radius-md)",
          width: "fit-content",
          border: "1px solid var(--color-border-subtle, #e2e8f0)"
        }}
      >
        {subTabs.map((st) => {
          const isActive = activeSubTab === st.id;
          return (
            <button
              key={st.id}
              role="tab"
              aria-selected={isActive}
              onClick={() => setActiveSubTab(st.id)}
              style={{
                padding: "0.5rem 1rem",
                borderRadius: "var(--radius-sm)",
                border: "none",
                cursor: "pointer",
                fontWeight: isActive ? 600 : 500,
                fontSize: "0.875rem",
                background: isActive ? "var(--color-surface-card, #ffffff)" : "transparent",
                color: isActive ? "var(--color-content-primary, #0f172a)" : "var(--color-content-muted, #64748b)",
                boxShadow: isActive ? "var(--shadow-sm)" : "none",
                transition: "all 0.15s ease"
              }}
            >
              {st.label}
            </button>
          );
        })}
      </div>

      {activeSubTab === "groups" && (
        <>
          {/* Groups Surface */}
          <Surface style={{ padding: "1.25rem", display: "flex", flexDirection: "column", gap: "1rem" }}>
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: "1rem" }}>
              <div>
                <h2 style={{ margin: 0, fontSize: "1.25rem", fontWeight: 700, color: "var(--color-content-primary)" }}>
                  {t("setup.groupsSectionTitle")}
                </h2>
                <p style={{ margin: "0.25rem 0 0", fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
                  {t("setup.groupsSectionSubtitle")}
                </p>
              </div>

              <Button variant="primary" onPress={() => setIsNewGroupOpen(true)}>
                + {t("setup.newGroupAction")}
              </Button>
            </div>

            {groups.length === 0 ? (
              <div style={{ textAlign: "center", padding: "2.5rem 1rem", color: "var(--color-content-muted)" }}>
                <p>{t("setup.groups.emptyTitle")}</p>
                <p style={{ fontSize: "0.8125rem" }}>{t("setup.groups.emptyDesc")}</p>
              </div>
            ) : (
              <div style={{ overflowX: "auto" }}>
                <table style={{ width: "100%", borderCollapse: "collapse", textAlign: "start", fontSize: "0.875rem" }}>
                  <thead>
                    <tr style={{ borderBottom: "1px solid var(--color-border-subtle, #e2e8f0)", background: "var(--color-surface-subtle, #f8fafc)" }}>
                      <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>{t("setup.groups.code")}</th>
                      <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>{t("setup.groups.name")}</th>
                      <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>{t("setup.groups.frequency")}</th>
                      <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>{t("setup.payDayLabel", "يوم الصرف")}</th>
                      <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>{t("setup.groups.holiday")}</th>
                      <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>{t("setup.groups.actions")}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {groups.map((g: PayrollGroup) => {
                      const isSelected = g.id === activeGroupId;
                      return (
                        <tr
                          key={g.id}
                          onClick={() => setSelectedGroupId(g.id)}
                          style={{
                            borderBottom: "1px solid var(--color-border-subtle, #e2e8f0)",
                            cursor: "pointer",
                            background: isSelected ? "var(--color-brand-primary-subtle, #e0f2fe)" : "transparent",
                            transition: "background 0.15s ease"
                          }}
                        >
                          <td style={{ padding: "0.875rem 1rem", fontWeight: 600, color: "var(--color-content-primary)" }}>{g.code}</td>
                          <td style={{ padding: "0.875rem 1rem" }}>{g.name}</td>
                          <td style={{ padding: "0.875rem 1rem" }}>{t(`setup.frequencies.${g.frequency.toLowerCase()}`, g.frequency)}</td>
                          <td style={{ padding: "0.875rem 1rem", fontWeight: 600 }}>{g.payDay}</td>
                          <td style={{ padding: "0.875rem 1rem", color: "var(--color-content-secondary)" }}>
                            {g.holidayAdjustment ? t(`setup.holidayPolicies.${g.holidayAdjustment.toLowerCase()}`, g.holidayAdjustment) : "—"}
                          </td>
                          <td style={{ padding: "0.875rem 1rem" }}>
                            <Button
                              variant="secondary"
                              size="compact"
                              onPress={() => setSelectedGroupId(g.id)}
                            >
                              {isSelected ? t("setup.selectedGroup", "المجموعة الحالية") : t("setup.selectGroupAction", "عرض الفترات")}
                            </Button>
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            )}
          </Surface>

          {/* Periods Surface */}
          {activeGroupId && (
            <Surface style={{ padding: "1.25rem", display: "flex", flexDirection: "column", gap: "1rem" }}>
              <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: "1rem" }}>
                <div>
                  <h3 style={{ margin: 0, fontSize: "1.125rem", fontWeight: 700, color: "var(--color-content-primary)" }}>
                    {t("setup.periodsSectionTitle")}
                  </h3>
                  <p style={{ margin: "0.25rem 0 0", fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
                    {t("setup.periodsSectionSubtitle")}
                  </p>
                </div>

                <Button variant="secondary" onPress={() => setIsNewPeriodOpen(true)}>
                  + {t("setup.newPeriodAction")}
                </Button>
              </div>

              {periods.length === 0 ? (
                <div style={{ textAlign: "center", padding: "2rem 1rem", color: "var(--color-content-muted)" }}>
                  <p>{t("setup.periods.emptyTitle", "لا توجد فترات مسجلة لهذه المجموعة.")}</p>
                  <p style={{ fontSize: "0.8125rem" }}>{t("setup.periods.emptyDesc", "أضف فترة رواتب جديدة لبدء الدورات الشهرية.")}</p>
                </div>
              ) : (
                <div style={{ overflowX: "auto" }}>
                  <table style={{ width: "100%", borderCollapse: "collapse", textAlign: "start", fontSize: "0.875rem" }}>
                    <thead>
                      <tr style={{ borderBottom: "1px solid var(--color-border-subtle, #e2e8f0)", background: "var(--color-surface-subtle, #f8fafc)" }}>
                        <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>{t("setup.periodStartDate")}</th>
                        <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>{t("setup.periodEndDate")}</th>
                        <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>{t("setup.periods.cutoffDate")}</th>
                        <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>{t("setup.plannedPayDate")}</th>
                        <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>{t("setup.periods.status")}</th>
                      </tr>
                    </thead>
                    <tbody>
                      {periods.map((p: PayrollPeriod) => (
                        <tr key={p.id} style={{ borderBottom: "1px solid var(--color-border-subtle, #e2e8f0)" }}>
                          <td style={{ padding: "0.875rem 1rem", fontWeight: 600 }}>{p.periodStart}</td>
                          <td style={{ padding: "0.875rem 1rem", fontWeight: 600 }}>{p.periodEnd}</td>
                          <td style={{ padding: "0.875rem 1rem" }}>{p.inputCutoff}</td>
                          <td style={{ padding: "0.875rem 1rem" }}>{p.plannedPayDate}</td>
                          <td style={{ padding: "0.875rem 1rem" }}>
                            <StatusBadge
                              status={p.status === "Open" ? "info" : p.status === "Finalized" ? "success" : "neutral"}
                              label={p.status}
                            />
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </Surface>
          )}
        </>
      )}

      {activeSubTab === "components" && (
        <Surface style={{ padding: "1.25rem", display: "flex", flexDirection: "column", gap: "1rem" }}>
          <div>
            <h3 style={{ margin: 0, fontSize: "1.125rem", fontWeight: 700, color: "var(--color-content-primary)" }}>
              {t("setup.componentsSectionTitle")}
            </h3>
            <p style={{ margin: "0.25rem 0 0", fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
              {t("setup.componentsSectionSubtitle")}
            </p>
          </div>

          <div style={{ overflowX: "auto" }}>
            <table style={{ width: "100%", borderCollapse: "collapse", textAlign: "start", fontSize: "0.875rem" }}>
              <thead>
                <tr style={{ borderBottom: "1px solid var(--color-border-subtle, #e2e8f0)", background: "var(--color-surface-subtle, #f8fafc)" }}>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>الرمز (Code)</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>اسم البند</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>التصنيف</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>الاتجاه</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>طريقة الاحتساب</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>الحالة</th>
                </tr>
              </thead>
              <tbody>
                {components.map((c: PayrollComponent) => (
                  <tr key={c.id} style={{ borderBottom: "1px solid var(--color-border-subtle, #e2e8f0)" }}>
                    <td style={{ padding: "0.875rem 1rem", fontWeight: 600, fontFamily: "var(--font-mono)", fontSize: "0.8125rem" }}>{c.code}</td>
                    <td style={{ padding: "0.875rem 1rem", fontWeight: 600 }}>{c.name}</td>
                    <td style={{ padding: "0.875rem 1rem" }}>{c.category}</td>
                    <td style={{ padding: "0.875rem 1rem" }}>
                      <span style={{ color: c.direction === "Credit" ? "var(--color-status-success)" : c.direction === "Debit" ? "var(--color-status-danger)" : "var(--color-content-muted)", fontWeight: 600 }}>
                        {c.direction === "Credit" ? "+ استحقاق" : c.direction === "Debit" ? "- استقطاع" : "إحصائي"}
                      </span>
                    </td>
                    <td style={{ padding: "0.875rem 1rem" }}>{c.inputMode}</td>
                    <td style={{ padding: "0.875rem 1rem" }}>
                      <StatusBadge status={c.status === "Active" ? "success" : "neutral"} label={c.status} />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </Surface>
      )}

      {activeSubTab === "structures" && (
        <Surface style={{ padding: "1.25rem", display: "flex", flexDirection: "column", gap: "1rem" }}>
          <div>
            <h3 style={{ margin: 0, fontSize: "1.125rem", fontWeight: 700, color: "var(--color-content-primary)" }}>
              {t("setup.salaryStructuresSectionTitle")}
            </h3>
            <p style={{ margin: "0.25rem 0 0", fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
              {t("setup.salaryStructuresSectionSubtitle")}
            </p>
          </div>

          <div style={{ overflowX: "auto" }}>
            <table style={{ width: "100%", borderCollapse: "collapse", textAlign: "start", fontSize: "0.875rem" }}>
              <thead>
                <tr style={{ borderBottom: "1px solid var(--color-border-subtle, #e2e8f0)", background: "var(--color-surface-subtle, #f8fafc)" }}>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>الرمز</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>اسم الهيكل</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>عدد البنود</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>الحالة</th>
                </tr>
              </thead>
              <tbody>
                {structures.map((s: SalaryStructure) => (
                  <tr key={s.id} style={{ borderBottom: "1px solid var(--color-border-subtle, #e2e8f0)" }}>
                    <td style={{ padding: "0.875rem 1rem", fontWeight: 600, fontFamily: "var(--font-mono)" }}>{s.code}</td>
                    <td style={{ padding: "0.875rem 1rem", fontWeight: 600 }}>{s.name}</td>
                    <td style={{ padding: "0.875rem 1rem" }}>{s.componentsCount ? `${s.componentsCount} بند` : "—"}</td>
                    <td style={{ padding: "0.875rem 1rem" }}>
                      <StatusBadge status={s.status === "Active" ? "success" : "neutral"} label={s.status} />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </Surface>
      )}

      {activeSubTab === "assignments" && (
        <Surface style={{ padding: "1.25rem", display: "flex", flexDirection: "column", gap: "1rem" }}>
          <div>
            <h3 style={{ margin: 0, fontSize: "1.125rem", fontWeight: 700, color: "var(--color-content-primary)" }}>
              {t("setup.assignmentsSectionTitle")}
            </h3>
            <p style={{ margin: "0.25rem 0 0", fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
              {t("setup.assignmentsSectionSubtitle")}
            </p>
          </div>

          <div style={{ overflowX: "auto" }}>
            <table style={{ width: "100%", borderCollapse: "collapse", textAlign: "start", fontSize: "0.875rem" }}>
              <thead>
                <tr style={{ borderBottom: "1px solid var(--color-border-subtle, #e2e8f0)", background: "var(--color-surface-subtle, #f8fafc)" }}>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>الموظف</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>مجموعة الرواتب</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>الراتب الأساسي</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>تاريخ البدء</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>تاريخ الانتهاء</th>
                </tr>
              </thead>
              <tbody>
                {assignments.map((a: EmployeePayrollAssignment) => (
                  <tr key={a.id} style={{ borderBottom: "1px solid var(--color-border-subtle, #e2e8f0)" }}>
                    <td style={{ padding: "0.875rem 1rem", fontWeight: 600 }}>{a.employeeName || a.employmentId}</td>
                    <td style={{ padding: "0.875rem 1rem" }}>{a.payrollGroupName || a.payrollGroupId}</td>
                    <td style={{ padding: "0.875rem 1rem", fontWeight: 600 }}>{a.baseSalary ? `${a.baseSalary.toLocaleString()} ${a.currencyCode}` : "—"}</td>
                    <td style={{ padding: "0.875rem 1rem" }}>{a.effectiveFrom}</td>
                    <td style={{ padding: "0.875rem 1rem", color: "var(--color-content-secondary)" }}>{a.effectiveTo || "مستمر"}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </Surface>
      )}

      {/* Salary Structures Integration Notice */}
      <Surface style={{ padding: "1.25rem", background: "var(--color-surface-subtle, #f8fafc)" }}>
        <h4 style={{ margin: 0, fontSize: "0.9375rem", fontWeight: 700, color: "var(--color-content-primary)" }}>
          {t("setup.salaryStructuresNoticeTitle")}
        </h4>
        <p style={{ margin: "0.375rem 0 0", fontSize: "0.8125rem", color: "var(--color-content-muted)", lineHeight: 1.5 }}>
          {t("setup.salaryStructuresNoticeDescription")}
        </p>
      </Surface>

      {/* Modals */}
      <NewPayrollGroupModal
        isOpen={isNewGroupOpen}
        onClose={() => setIsNewGroupOpen(false)}
        legalEntityId={legalEntityId}
      />

      <NewPayrollPeriodModal
        isOpen={isNewPeriodOpen}
        onClose={() => setIsNewPeriodOpen(false)}
        legalEntityId={legalEntityId}
        defaultGroupId={activeGroupId}
      />
    </div>
  );
}

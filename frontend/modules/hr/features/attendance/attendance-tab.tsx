import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { FilterBar } from "../../../../components/primitives/filter-bar.tsx";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "../../../../components/primitives/table.tsx";
import { LocalDateField } from "../../../../components/fields/local-date-field.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { HrStatusBadge } from "../../components/hr-status-badge.tsx";
import { AttendanceCorrectionModal } from "./attendance-correction-modal.tsx";
import { useAttendanceRecords, useAttendancePunchMutation, useMyProfile } from "../../hooks/use-hr-queries.ts";
import type { AttendanceRecordView } from "../../types.ts";

export interface AttendanceTabProps {
  readonly legalEntityId: string;
  readonly defaultEmploymentId?: string | null;
}

export function AttendanceTab({ legalEntityId, defaultEmploymentId }: AttendanceTabProps): React.JSX.Element {
  const { t } = useTranslation("hr");

  const today = new Date().toISOString().slice(0, 10);
  const [fromDate, setFromDate] = useState(today);
  const [toDate, setToDate] = useState(today);
  const [selectedRecord, setSelectedRecord] = useState<AttendanceRecordView | null>(null);

  const myProfileQuery = useMyProfile(legalEntityId);
  const selfEmploymentId = myProfileQuery.data?.initialEmployment?.id ?? null;
  const authorizedPunchEmploymentId = defaultEmploymentId || selfEmploymentId || "";

  const attendanceQuery = useAttendanceRecords({
    legalEntityId,
    from: fromDate,
    to: toDate,
    employmentId: defaultEmploymentId || null
  });

  const items = attendanceQuery.data?.items ?? [];
  const punchMutation = useAttendancePunchMutation(authorizedPunchEmploymentId, legalEntityId);

  const handleSimulatePunch = async (direction: "In" | "Out") => {
    if (!authorizedPunchEmploymentId) return;
    try {
      await punchMutation.mutateAsync({
        legalEntityId,
        occurredAt: new Date().toISOString(),
        direction,
        sourceType: "WebSelfService",
        idempotencyKey: `punch-${direction}-${Date.now()}`
      });
      await attendanceQuery.refetch();
    } catch {
      // Handled by query refetch
    }
  };

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
      {/* Top Filter Bar */}
      <FilterBar>
        <div style={{ display: "flex", gap: "0.75rem", alignItems: "center", flexWrap: "wrap", flex: 1 }}>
          <h3 style={{ margin: 0, fontSize: "1rem", color: "var(--color-content-primary)", marginInlineEnd: "1rem" }}>
            {t("attendance.title")}
          </h3>
          <LocalDateField
            label={t("attendance.filters.fromDate")}
            value={fromDate}
            onChange={setFromDate}
          />
          <LocalDateField
            label={t("attendance.filters.toDate")}
            value={toDate}
            onChange={setToDate}
          />
        </div>
        <div style={{ display: "flex", gap: "0.5rem", alignItems: "center" }}>
          {!authorizedPunchEmploymentId && (
            <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", marginInlineEnd: "0.25rem" }}>
              {t("attendance.selectEmployeeHelper", "تسجيل الحضور يتطلب حساب موظف أو تحديد موظف")}
            </span>
          )}
          <Button
            variant="secondary"
            size="compact"
            isDisabled={!authorizedPunchEmploymentId || punchMutation.isPending}
            onPress={() => handleSimulatePunch("In")}
          >
            {punchMutation.isPending ? t("actions.saving") : t("attendance.actions.punchIn")}
          </Button>
          <Button
            variant="secondary"
            size="compact"
            isDisabled={!authorizedPunchEmploymentId || punchMutation.isPending}
            onPress={() => handleSimulatePunch("Out")}
          >
            {punchMutation.isPending ? t("actions.saving") : t("attendance.actions.punchOut")}
          </Button>
        </div>
      </FilterBar>

      {/* Attendance Records Table */}
      <div style={{ background: "var(--color-surface-card)", border: "1px solid var(--color-border-default)", borderRadius: "var(--radius-md)", overflow: "hidden" }}>
        <Table aria-label={t("attendance.tableAria")}>
          <TableHeader>
            <TableRow>
              <TableHead>{t("attendance.columns.date")}</TableHead>
              <TableHead>{t("attendance.columns.plannedSchedule")}</TableHead>
              <TableHead>{t("attendance.columns.firstIn")}</TableHead>
              <TableHead>{t("attendance.columns.lastOut")}</TableHead>
              <TableHead>{t("attendance.columns.workedHours")}</TableHead>
              <TableHead>{t("attendance.columns.status")}</TableHead>
              <TableHead>{t("attendance.columns.lockState")}</TableHead>
              <TableHead style={{ textAlign: "end" }}>{t("attendance.columns.actions")}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {attendanceQuery.isLoading ? (
              <TableRow>
                <TableCell colSpan={8} style={{ textAlign: "center", padding: "2rem" }}>
                  {t("attendance.loading")}
                </TableCell>
              </TableRow>
            ) : items.length === 0 ? (
              <TableRow>
                <TableCell colSpan={8} style={{ textAlign: "center", padding: "2rem", color: "var(--color-content-muted)" }}>
                  {t("attendance.emptyState")}
                </TableCell>
              </TableRow>
            ) : (
              items.map((rec) => (
                <TableRow key={rec.id}>
                  <TableCell style={{ fontWeight: 600 }}>{rec.workDate}</TableCell>
                  <TableCell>{rec.plannedStart && rec.plannedEnd ? [rec.plannedStart, rec.plannedEnd].join(" / ") : "—"}</TableCell>
                  <TableCell>{rec.firstIn ? new Date(rec.firstIn).toLocaleTimeString() : "—"}</TableCell>
                  <TableCell>{rec.lastOut ? new Date(rec.lastOut).toLocaleTimeString() : <span style={{ color: "var(--color-status-danger)", fontWeight: 600 }}>{t("attendance.missingCheckout")}</span>}</TableCell>
                  <TableCell>{(rec.workedMinutes / 60).toFixed(1)}</TableCell>
                  <TableCell>
                    <HrStatusBadge status={rec.status} label={t(`statuses.${rec.status}`, { defaultValue: rec.status })} />
                  </TableCell>
                  <TableCell>
                    <HrStatusBadge status={rec.lockState} label={t(`statuses.${rec.lockState}`, { defaultValue: rec.lockState })} />
                  </TableCell>
                  <TableCell style={{ textAlign: "end" }}>
                    <Button
                      variant="secondary"
                      size="compact"
                      onPress={() => setSelectedRecord(rec)}
                    >
                      {t("attendance.actions.correct")}
                    </Button>
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </div>

      {/* Correction Modal */}
      {selectedRecord && (
        <AttendanceCorrectionModal
          isOpen={Boolean(selectedRecord)}
          onClose={() => setSelectedRecord(null)}
          record={selectedRecord}
          legalEntityId={legalEntityId}
          onCorrected={() => attendanceQuery.refetch()}
        />
      )}
    </div>
  );
}

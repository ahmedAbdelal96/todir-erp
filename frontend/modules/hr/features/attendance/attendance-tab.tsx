import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "../../../../components/primitives/table.tsx";
import { LocalDateField } from "../../../../components/fields/local-date-field.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { Surface } from "../../../../components/pages/surface.tsx";
import { EmptyState } from "../../../../components/primitives/empty-state.tsx";
import { LocalDateDisplay } from "../../../../components/fields/local-date-display.tsx";
import { BidiText } from "../../../../components/primitives/bidi-text.tsx";
import { HrStatusBadge } from "../../components/hr-status-badge.tsx";
import { AttendanceCorrectionModal } from "./attendance-correction-modal.tsx";
import { useAttendanceRecords, useAttendancePunchMutation, useMyProfile } from "../../hooks/use-hr-queries.ts";
import { mapHrError } from "../../services/hr-error-map.ts";
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
  const attendanceError = attendanceQuery.error ? mapHrError(attendanceQuery.error) : null;
  const punchError = punchMutation.error ? mapHrError(punchMutation.error) : null;

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
    <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
      <Surface
        title={t("attendance.title")}
        description={t("attendance.subtitle")}
        padding="compact"
        headerActions={
          <div style={{ display: "flex", gap: "0.5rem", alignItems: "center", flexWrap: "wrap" }}>
            <Button
              variant="primary"
              size="compact"
              isDisabled={!authorizedPunchEmploymentId || punchMutation.isPending || attendanceQuery.isPending}
              onPress={() => handleSimulatePunch("In")}
            >
              {punchMutation.isPending ? t("actions.saving") : t("attendance.actions.punchIn")}
            </Button>
            <Button
              variant="secondary"
              size="compact"
              isDisabled={!authorizedPunchEmploymentId || punchMutation.isPending || attendanceQuery.isPending}
              onPress={() => handleSimulatePunch("Out")}
            >
              {punchMutation.isPending ? t("actions.saving") : t("attendance.actions.punchOut")}
            </Button>
          </div>
        }
      >
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))", gap: "0.75rem", maxWidth: "38rem" }}>
          <LocalDateField
            label={t("attendance.filters.fromDate")}
            value={fromDate}
            onChange={setFromDate}
            size="compact"
          />
          <LocalDateField
            label={t("attendance.filters.toDate")}
            value={toDate}
            onChange={setToDate}
            size="compact"
          />
        </div>
        {!authorizedPunchEmploymentId && (
          <p style={{ margin: "0.75rem 0 0", fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
            {t("attendance.selectEmployeeHelper")}
          </p>
        )}
        {punchError && (
          <div role="alert" style={{ marginTop: "0.75rem", padding: "0.625rem 0.75rem", borderRadius: "var(--radius-sm)", backgroundColor: "var(--color-status-danger-subtle)", color: "var(--color-status-danger-content)", fontSize: "0.8125rem" }}>
            {t(punchError.messageKey, punchError.defaultMessage)}
          </div>
        )}
      </Surface>

      <Surface title={t("attendance.recordsTitle")} description={t("attendance.recordsDescription")} padding="none">
        {attendanceQuery.isPending ? (
          <div role="status" style={{ padding: "2rem 1rem", textAlign: "center", color: "var(--color-content-muted)", fontSize: "0.875rem" }}>
            {t("attendance.loading")}
          </div>
        ) : attendanceQuery.isError ? (
          <div role="alert" style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: "1rem", flexWrap: "wrap", padding: "1rem" }}>
            <span style={{ color: "var(--color-status-danger)", fontSize: "0.875rem" }}>
              {attendanceError ? t(attendanceError.messageKey, attendanceError.defaultMessage) : t("attendance.loadError")}
            </span>
            <Button variant="secondary" size="compact" onPress={() => void attendanceQuery.refetch()}>
              {t("actions.retry")}
            </Button>
          </div>
        ) : items.length === 0 ? (
          <EmptyState
            title={t("attendance.emptyState")}
            description={t("attendance.noRecordsDescription")}
            style={{ margin: "1rem auto", maxWidth: "34rem", boxShadow: "none" }}
          />
        ) : (
          <Table density="compact" aria-label={t("attendance.tableAria")}>
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
            {items.map((rec) => (
                <TableRow key={rec.id}>
                  <TableCell style={{ fontWeight: 600 }}><LocalDateDisplay value={rec.workDate} /></TableCell>
                  <TableCell><BidiText direction="ltr">{rec.plannedStart && rec.plannedEnd ? [rec.plannedStart, rec.plannedEnd].join(" / ") : "—"}</BidiText></TableCell>
                  <TableCell><BidiText direction="ltr">{rec.firstIn ? new Date(rec.firstIn).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" }) : "—"}</BidiText></TableCell>
                  <TableCell>{rec.lastOut ? <BidiText direction="ltr">{new Date(rec.lastOut).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}</BidiText> : <span style={{ color: "var(--color-status-danger)", fontWeight: 600 }}>{t("attendance.missingCheckout")}</span>}</TableCell>
                  <TableCell><BidiText direction="ltr">{(rec.workedMinutes / 60).toFixed(1)}</BidiText></TableCell>
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
              ))}
          </TableBody>
          </Table>
        )}
      </Surface>

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

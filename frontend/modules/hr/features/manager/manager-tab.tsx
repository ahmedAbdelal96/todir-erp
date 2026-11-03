import React from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "../../../../components/primitives/table.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { HrStatusBadge } from "../../components/hr-status-badge.tsx";
import { LocalDateDisplay } from "../../../../components/fields/local-date-display.tsx";
import { useMyTeam, useMyTeamLeaveApprovals, useApproveLeaveRequestMutation, useRejectLeaveRequestMutation } from "../../hooks/use-hr-queries.ts";
import type { LeaveRequestView } from "../../types.ts";

export interface ManagerTabProps {
  readonly legalEntityId: string;
}

export function ManagerTab({ legalEntityId }: ManagerTabProps): React.JSX.Element {
  const { t } = useTranslation("hr");
  const teamQuery = useMyTeam(legalEntityId);
  const leaveApprovalsQuery = useMyTeamLeaveApprovals(legalEntityId);

  const approveMutation = useApproveLeaveRequestMutation(legalEntityId);
  const rejectMutation = useRejectLeaveRequestMutation(legalEntityId);

  const team = teamQuery.data ?? [];
  const pendingApprovals: readonly LeaveRequestView[] = leaveApprovalsQuery.data ?? [];

  const handleApprove = async (req: LeaveRequestView) => {
    try {
      await approveMutation.mutateAsync({
        requestId: req.id,
        decision: { expectedVersion: req.version }
      });
    } catch {
      // Invalidation handled by hook
    }
  };

  const handleReject = async (req: LeaveRequestView) => {
    try {
      await rejectMutation.mutateAsync({
        requestId: req.id,
        decision: { expectedVersion: req.version }
      });
    } catch {
      // Invalidation handled by hook
    }
  };

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
      {/* Pending Leave Approvals for Manager's Team */}
      <Surface title={t("manager.leaveApprovalsTitle")}>
        <Table aria-label={t("manager.leaveApprovalsTitle")}>
          <TableHeader>
            <TableRow>
              <TableHead>{t("manager.from")}</TableHead>
              <TableHead>{t("manager.to")}</TableHead>
              <TableHead>{t("manager.chargedDays")}</TableHead>
              <TableHead>{t("manager.reason")}</TableHead>
              <TableHead style={{ textAlign: "end" }}>{t("manager.actions")}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {pendingApprovals.length === 0 ? (
              <TableRow>
                <TableCell colSpan={5} style={{ textAlign: "center", color: "var(--color-content-muted)" }}>
                  {t("manager.noPendingApprovals")}
                </TableCell>
              </TableRow>
            ) : (
              pendingApprovals.map((req) => (
                <TableRow key={req.id}>
                  <TableCell><LocalDateDisplay value={req.from} /></TableCell>
                  <TableCell><LocalDateDisplay value={req.to} /></TableCell>
                  <TableCell style={{ fontWeight: 700 }}>{req.chargedQuantity}</TableCell>
                  <TableCell>{req.reason || "—"}</TableCell>
                  <TableCell style={{ textAlign: "end" }}>
                    <div style={{ display: "inline-flex", gap: "0.5rem" }}>
                      <Button variant="primary" size="compact" onPress={() => handleApprove(req)}>
                        {t("manager.approveAction")}
                      </Button>
                      <Button variant="danger" size="compact" onPress={() => handleReject(req)}>
                        {t("manager.rejectAction")}
                      </Button>
                    </div>
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </Surface>

      {/* Direct Report Team Members */}
      <Surface title={t("manager.teamTitle")}>
        <Table aria-label={t("manager.teamTitle")}>
          <TableHeader>
            <TableRow>
              <TableHead>{t("manager.employeeNumber")}</TableHead>
              <TableHead>{t("manager.name")}</TableHead>
              <TableHead>{t("manager.status")}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {team.length === 0 ? (
              <TableRow>
                <TableCell colSpan={3} style={{ textAlign: "center", color: "var(--color-content-muted)" }}>
                  {t("manager.noTeamMembers")}
                </TableCell>
              </TableRow>
            ) : (
              team.map((m) => (
                <TableRow key={m.id}>
                  <TableCell style={{ fontFamily: "monospace" }}>{m.employmentNumber || "—"}</TableCell>
                  <TableCell style={{ fontWeight: 600 }}>{m.displayName}</TableCell>
                  <TableCell>
                    <HrStatusBadge status={m.status} label={t(`employeeStatus.${m.status}`, { defaultValue: m.status })} />
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </Surface>
    </div>
  );
}

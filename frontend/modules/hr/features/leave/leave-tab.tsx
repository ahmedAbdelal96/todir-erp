import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { FilterBar } from "../../../../components/primitives/filter-bar.tsx";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "../../../../components/primitives/table.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { HrStatusBadge } from "../../components/hr-status-badge.tsx";
import { LocalDateDisplay } from "../../../../components/fields/local-date-display.tsx";
import { NewLeaveRequestModal } from "./new-leave-request-modal.tsx";
import {
  useLeaveRequests,
  useApproveLeaveRequestMutation,
  useCancelLeaveRequestMutation
} from "../../hooks/use-hr-queries.ts";
import type { LeaveRequestView } from "../../types.ts";

export interface LeaveTabProps {
  readonly legalEntityId: string;
  readonly defaultEmploymentId?: string | null;
}

export function LeaveTab({ legalEntityId, defaultEmploymentId }: LeaveTabProps): React.JSX.Element {
  const { t } = useTranslation("hr");
  const [isNewRequestOpen, setIsNewRequestOpen] = useState(false);

  const leaveRequestsQuery = useLeaveRequests(legalEntityId, defaultEmploymentId);
  const approveMutation = useApproveLeaveRequestMutation(legalEntityId);
  const cancelMutation = useCancelLeaveRequestMutation(legalEntityId);

  const requests: readonly LeaveRequestView[] = leaveRequestsQuery.data ?? [];
  const effectiveEmploymentId = defaultEmploymentId || requests[0]?.employmentId || "";

  const handleApprove = async (req: LeaveRequestView) => {
    try {
      await approveMutation.mutateAsync({
        requestId: req.id,
        decision: { expectedVersion: req.version }
      });
    } catch {
      // Query cache invalidates
    }
  };

  const handleCancel = async (req: LeaveRequestView) => {
    try {
      await cancelMutation.mutateAsync({
        requestId: req.id,
        decision: { expectedVersion: req.version }
      });
    } catch {
      // Query cache invalidates
    }
  };

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
      {/* Top Action Filter Bar */}
      <FilterBar>
        <div style={{ flex: 1 }}>
          <h3 style={{ margin: 0, fontSize: "1rem", color: "var(--color-content-primary)" }}>
            {t("leave.title")}
          </h3>
          <p style={{ margin: "0.25rem 0 0", fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
            {t("leave.subtitle")}
          </p>
        </div>
        <Button variant="primary" onPress={() => setIsNewRequestOpen(true)}>
          + {t("leave.actions.newRequest")}
        </Button>
      </FilterBar>

      {/* Leave Requests Table */}
      <div style={{ background: "var(--color-surface-card)", border: "1px solid var(--color-border-default)", borderRadius: "var(--radius-md)", overflowX: "auto" }}>
        <Table aria-label={t("leave.tableAria")}>
          <TableHeader>
            <TableRow>
              <TableHead style={{ width: "130px" }}>{t("leave.columns.from")}</TableHead>
              <TableHead style={{ width: "130px" }}>{t("leave.columns.to")}</TableHead>
              <TableHead style={{ width: "120px" }}>{t("leave.columns.chargedDays")}</TableHead>
              <TableHead>{t("leave.columns.reason")}</TableHead>
              <TableHead style={{ width: "130px" }}>{t("leave.columns.status")}</TableHead>
              <TableHead style={{ width: "210px", textAlign: "end" }}>{t("leave.columns.actions")}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {leaveRequestsQuery.isLoading ? (
              <TableRow>
                <TableCell colSpan={6} style={{ textAlign: "center", padding: "2rem" }}>
                  {t("leave.loading")}
                </TableCell>
              </TableRow>
            ) : requests.length === 0 ? (
              <TableRow>
                <TableCell colSpan={6} style={{ textAlign: "center", padding: "2rem", color: "var(--color-content-muted)" }}>
                  {t("leave.emptyState")}
                </TableCell>
              </TableRow>
            ) : (
              requests.map((req) => (
                <TableRow key={req.id}>
                  <TableCell style={{ fontWeight: 600 }}>
                    <LocalDateDisplay value={req.from} />
                  </TableCell>
                  <TableCell>
                    <LocalDateDisplay value={req.to} />
                  </TableCell>
                  <TableCell style={{ fontWeight: 700 }}>
                    {req.chargedQuantity} {t("leave.daysUnit")}
                  </TableCell>
                  <TableCell>{req.reason || "—"}</TableCell>
                  <TableCell>
                    <HrStatusBadge status={req.status} label={t(`statuses.${req.status}`, { defaultValue: req.status })} />
                  </TableCell>
                  <TableCell style={{ textAlign: "end", whiteSpace: "nowrap" }}>
                    <div style={{ display: "inline-flex", gap: "0.375rem", whiteSpace: "nowrap" }}>
                      {req.status === "Pending" && (
                        <Button
                          variant="primary"
                          size="compact"
                          style={{ whiteSpace: "nowrap" }}
                          onPress={() => handleApprove(req)}
                          isDisabled={approveMutation.isPending}
                        >
                          {t("leave.actions.approve")}
                        </Button>
                      )}
                      {(req.status === "Pending" || req.status === "Approved") && (
                        <Button
                          variant="secondary"
                          size="compact"
                          style={{ whiteSpace: "nowrap" }}
                          onPress={() => handleCancel(req)}
                          isDisabled={cancelMutation.isPending}
                        >
                          {t("leave.actions.cancel")}
                        </Button>
                      )}
                    </div>
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </div>

      {/* Creation Modal */}
      {isNewRequestOpen && effectiveEmploymentId && (
        <NewLeaveRequestModal
          isOpen={isNewRequestOpen}
          onClose={() => setIsNewRequestOpen(false)}
          employmentId={effectiveEmploymentId}
          legalEntityId={legalEntityId}
          onCreated={() => leaveRequestsQuery.refetch()}
        />
      )}
    </div>
  );
}

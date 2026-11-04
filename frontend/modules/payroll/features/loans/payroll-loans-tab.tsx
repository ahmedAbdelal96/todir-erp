import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Select } from "../../../../components/primitives/select.tsx";
import {
  useReceivables,
  useApproveReceivableMutation,
  useRequestDisbursementMutation
} from "../../hooks/use-payroll-queries.ts";
import { PayrollStatusBadge } from "../../components/payroll-status-badge.tsx";
import { NewLoanModal } from "./new-loan-modal.tsx";
import { LoanActionModal, type ModalLoanAction } from "./loan-action-modal.tsx";
import { mapPayrollError } from "../../services/payroll-error-map.ts";
import type { EmployeeReceivable } from "../../types.ts";

export interface PayrollLoansTabProps {
  readonly legalEntityId: string;
}

export function PayrollLoansTab({
  legalEntityId
}: PayrollLoansTabProps): React.JSX.Element {
  const { t } = useTranslation("payroll");
  const [searchTerm, setSearchTerm] = useState("");
  const [statusFilter, setStatusFilter] = useState<string>("all");
  const [isNewModalOpen, setIsNewModalOpen] = useState(false);
  const [actionModalOpen, setActionModalOpen] = useState(false);
  const [activeReceivable, setActiveReceivable] = useState<EmployeeReceivable | null>(null);
  const [activeActionType, setActiveActionType] = useState<ModalLoanAction>("pause");
  const [actionError, setActionError] = useState<string | null>(null);

  const { data: receivables = [], isLoading } = useReceivables(legalEntityId);
  const approveMutation = useApproveReceivableMutation(legalEntityId);
  const disburseMutation = useRequestDisbursementMutation(legalEntityId);

  const filtered = receivables.filter((item: EmployeeReceivable) => {
    if (statusFilter !== "all" && item.status !== statusFilter) return false;
    if (!searchTerm.trim()) return true;
    const term = searchTerm.toLowerCase();
    return (
      item.employmentId.toLowerCase().includes(term) ||
      item.type.toLowerCase().includes(term)
    );
  });

  const handleApprove = async (id: string) => {
    setActionError(null);
    try {
      await approveMutation.mutateAsync(id);
    } catch (err: unknown) {
      const mapped = mapPayrollError(err);
      setActionError(t(mapped.messageKey, mapped.defaultMessage));
    }
  };

  const handleDisburse = async (id: string) => {
    setActionError(null);
    try {
      await disburseMutation.mutateAsync(id);
    } catch (err: unknown) {
      const mapped = mapPayrollError(err);
      setActionError(t(mapped.messageKey, mapped.defaultMessage));
    }
  };

  const openAction = (receivable: EmployeeReceivable, type: ModalLoanAction) => {
    setActiveReceivable(receivable);
    setActiveActionType(type);
    setActionModalOpen(true);
  };

  const statusOptions = [
    { id: "all", label: t("common.allStatuses") },
    { id: "Draft", label: t("statuses.draft") },
    { id: "Approved", label: t("statuses.approved") },
    { id: "Disbursed", label: t("statuses.disbursed") },
    { id: "Paused", label: t("statuses.paused") },
    { id: "Settled", label: t("statuses.settled") },
    { id: "WrittenOff", label: t("statuses.writtenOff") }
  ];

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
      {/* Header Bar */}
      <div
        style={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          flexWrap: "wrap",
          gap: "1rem",
          padding: "1.25rem 1.5rem",
          background: "var(--color-surface-card)",
          borderRadius: "var(--radius-lg)",
          border: "1px solid var(--color-border-default)",
          boxShadow: "var(--shadow-sm)"
        }}
      >
        <div>
          <h2 style={{ margin: 0, fontSize: "1.125rem", fontWeight: 700, color: "var(--color-content-primary)" }}>
            {t("loans.tabTitle")}
          </h2>
          <p style={{ margin: "0.25rem 0 0 0", fontSize: "0.875rem", color: "var(--color-content-muted)" }}>
            {t("loans.tabSubtitle")}
          </p>
        </div>

        <Button
          variant="primary"
          onPress={() => setIsNewModalOpen(true)}
          style={{ display: "inline-flex", alignItems: "center", gap: "0.5rem" }}
        >
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
            <line x1="12" y1="5" x2="12" y2="19" />
            <line x1="5" y1="12" x2="19" y2="12" />
          </svg>
          {t("loans.newLoanAction")}
        </Button>
      </div>

      {actionError && (
        <div
          role="alert"
          style={{
            padding: "0.875rem 1rem",
            borderRadius: "var(--radius-md)",
            background: "var(--color-status-danger-subtle, #fee2e2)",
            color: "var(--color-status-danger, #b91c1c)",
            border: "1px solid var(--color-status-danger-border, #fecaca)",
            fontSize: "0.875rem"
          }}
        >
          {actionError}
        </div>
      )}

      {/* Filter and Search Bar */}
      <div
        style={{
          display: "flex",
          gap: "1rem",
          alignItems: "center",
          flexWrap: "wrap",
          padding: "1rem 1.25rem",
          background: "var(--color-surface-card)",
          borderRadius: "var(--radius-md)",
          border: "1px solid var(--color-border-default)"
        }}
      >
        <div style={{ flex: 1, minWidth: "240px" }}>
          <TextField
            label={t("common.search")}
            value={searchTerm}
            onChange={setSearchTerm}
            placeholder={t("loans.searchPlaceholder")}
          />
        </div>

        <div style={{ width: "200px" }}>
          <Select
            label={t("common.status")}
            selectedKey={statusFilter}
            onSelectionChange={(val) => {
              if (val) setStatusFilter(String(val));
            }}
            options={statusOptions}
          />
        </div>
      </div>

      {/* Receivables Table */}
      <div
        style={{
          background: "var(--color-surface-card)",
          borderRadius: "var(--radius-lg)",
          border: "1px solid var(--color-border-default)",
          boxShadow: "var(--shadow-sm)",
          overflow: "hidden"
        }}
      >
        <div style={{ overflowX: "auto" }}>
          <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "0.875rem" }}>
            <thead>
              <tr
                style={{
                  background: "var(--color-surface-subtle)",
                  borderBottom: "1px solid var(--color-border-default)",
                  textAlign: "start"
                }}
              >
                <th style={{ padding: "0.75rem 1rem" }}>{t("loans.employee")}</th>
                <th style={{ padding: "0.75rem 1rem" }}>{t("loans.type")}</th>
                <th style={{ padding: "0.75rem 1rem" }}>{t("loans.principal")}</th>
                <th style={{ padding: "0.75rem 1rem" }}>{t("loans.outstanding")}</th>
                <th style={{ padding: "0.75rem 1rem" }}>{t("loans.status")}</th>
                <th style={{ padding: "0.75rem 1rem" }}>{t("loans.actionsTitle")}</th>
              </tr>
            </thead>
            <tbody>
              {isLoading ? (
                <tr>
                  <td colSpan={6} style={{ padding: "2rem", textAlign: "center", color: "var(--color-content-muted)" }}>
                    {t("common.loading")}
                  </td>
                </tr>
              ) : filtered.length === 0 ? (
                <tr>
                  <td colSpan={6} style={{ padding: "3rem 2rem", textAlign: "center" }}>
                    <p style={{ margin: 0, fontWeight: 600, color: "var(--color-content-primary)" }}>
                      {t("loans.emptyTitle")}
                    </p>
                    <p style={{ margin: "0.25rem 0 0 0", fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
                      {t("loans.emptyDesc")}
                    </p>
                  </td>
                </tr>
              ) : (
                filtered.map((item: EmployeeReceivable) => (
                  <tr
                    key={item.id}
                    style={{
                      borderBottom: "1px solid var(--color-border-subtle)",
                      transition: "background-color 0.1s ease"
                    }}
                  >
                    <td style={{ padding: "0.75rem 1rem" }}>
                      <div style={{ fontWeight: 600, color: "var(--color-content-primary)" }}>
                        {item.employmentId}
                      </div>
                    </td>
                    <td style={{ padding: "0.75rem 1rem" }}>
                      <span
                        style={{
                          fontSize: "0.75rem",
                          fontWeight: 600,
                          padding: "0.2rem 0.5rem",
                          borderRadius: "var(--radius-sm)",
                          background: item.type === "Loan" ? "var(--color-surface-subtle)" : "var(--color-brand-primary-subtle)",
                          color: "var(--color-content-primary)"
                        }}
                      >
                        {item.type === "Loan" ? t("loans.types.loan") : t("loans.types.advance")}
                      </span>
                    </td>
                    <td style={{ padding: "0.75rem 1rem", fontWeight: 600 }}>
                      <bdi>{item.principal.toLocaleString()} {item.currencyCode}</bdi>
                    </td>
                    <td style={{ padding: "0.75rem 1rem", fontWeight: 700, color: item.outstandingPrincipal > 0 ? "var(--color-status-warning-content, #92400e)" : "var(--color-status-success)" }}>
                      <bdi>{item.outstandingPrincipal.toLocaleString()} {item.currencyCode}</bdi>
                    </td>
                    <td style={{ padding: "0.75rem 1rem" }}>
                      <PayrollStatusBadge status={item.status} />
                    </td>
                    <td style={{ padding: "0.75rem 1rem" }}>
                      <div style={{ display: "flex", gap: "0.375rem", flexWrap: "wrap" }}>
                        {item.status === "Draft" && (
                          <Button
                            variant="primary"
                            size="compact"
                            onPress={() => handleApprove(item.id)}
                            isDisabled={approveMutation.isPending}
                          >
                            {t("loans.approveAction")}
                          </Button>
                        )}

                        {item.status === "Approved" && (
                          <Button
                            variant="primary"
                            size="compact"
                            onPress={() => handleDisburse(item.id)}
                            isDisabled={disburseMutation.isPending}
                            style={{ background: "var(--color-status-success)", borderColor: "var(--color-status-success)" }}
                          >
                            {t("loans.disburseAction")}
                          </Button>
                        )}

                        {item.status === "Disbursed" && (
                          <>
                            <Button
                              variant="secondary"
                              size="compact"
                              onPress={() => openAction(item, "pause")}
                            >
                              {t("loans.pauseAction")}
                            </Button>
                            <Button
                              variant="secondary"
                              size="compact"
                              onPress={() => openAction(item, "reschedule")}
                            >
                              {t("loans.rescheduleAction")}
                            </Button>
                            <Button
                              variant="secondary"
                              size="compact"
                              onPress={() => openAction(item, "repay")}
                            >
                              {t("loans.repayAction")}
                            </Button>
                            <Button
                              variant="secondary"
                              size="compact"
                              onPress={() => openAction(item, "settle")}
                            >
                              {t("loans.settleAction")}
                            </Button>
                          </>
                        )}

                        {item.status === "Paused" && (
                          <>
                            <Button
                              variant="primary"
                              size="compact"
                              onPress={() => openAction(item, "resume")}
                            >
                              {t("loans.resumeAction")}
                            </Button>
                            <Button
                              variant="secondary"
                              size="compact"
                              onPress={() => openAction(item, "repay")}
                            >
                              {t("loans.repayAction")}
                            </Button>
                            <Button
                              variant="danger"
                              size="compact"
                              onPress={() => openAction(item, "writeOff")}
                            >
                              {t("loans.writeOffAction")}
                            </Button>
                          </>
                        )}
                      </div>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* Modals */}
      <NewLoanModal
        isOpen={isNewModalOpen}
        onClose={() => setIsNewModalOpen(false)}
        legalEntityId={legalEntityId}
      />

      <LoanActionModal
        isOpen={actionModalOpen}
        onClose={() => {
          setActionModalOpen(false);
          setActiveReceivable(null);
        }}
        legalEntityId={legalEntityId}
        receivable={activeReceivable}
        actionType={activeActionType}
      />
    </div>
  );
}

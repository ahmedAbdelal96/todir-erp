import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Table } from "../../../../components/primitives/table.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Pagination } from "../../../../components/primitives/pagination.tsx";
import { ExpensesKpiCard } from "../../components/expenses-kpi-card.tsx";
import {
  ClockIcon,
  AlertCircleIcon,
  BanknoteIcon,
  ReceiptIcon,
  ArrowRightLeftIcon
} from "../../components/expenses-icons.tsx";
import {
  ExpenseStateBadge,
  ApprovalStateBadge,
  AccountingStateBadge,
  SettlementStateBadge,
  PaymentSourceBadge
} from "../../components/expense-state-badge.tsx";
import {
  useExpensesListQuery,
  useExpenseExceptionSummaryQuery
} from "../../hooks/use-expenses-queries.ts";
import { ExpenseDetailDrawer } from "../drawers/expense-detail-drawer.tsx";
import { NewExpenseModal } from "../modals/new-expense-modal.tsx";

export interface ExpensesReviewTabProps {
  readonly legalEntityId: string;
}

export function ExpensesReviewTab({ legalEntityId }: ExpensesReviewTabProps): React.JSX.Element {
  const { t } = useTranslation("expenses");

  const [stateFilter, setStateFilter] = useState<string | null>(null);
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [selectedExpenseId, setSelectedExpenseId] = useState<string | null>(null);
  const [isNewExpenseOpen, setIsNewExpenseOpen] = useState(false);

  const summaryQuery = useExpenseExceptionSummaryQuery(legalEntityId);
  const expensesQuery = useExpensesListQuery(legalEntityId, {
    state: stateFilter,
    search: search.trim() || null,
    page,
    pageSize: 15
  });

  const summary = summaryQuery.data;

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
      {/* KPI Ribbon */}
      <div style={{ display: "flex", gap: "1rem", flexWrap: "wrap" }}>
        <ExpensesKpiCard
          title={t("kpi.pendingApproval")}
          value={summary ? summary.pendingApproval : "—"}
          tone="warning"
          icon={<ClockIcon size={18} />}
          onClick={() => {
            setStateFilter("Submitted");
            setPage(1);
          }}
        />
        <ExpensesKpiCard
          title={t("kpi.missingEvidence")}
          value={summary ? summary.missingEvidence : "—"}
          tone="danger"
          icon={<AlertCircleIcon size={18} />}
        />
        <ExpensesKpiCard
          title={t("kpi.reimbursementPending")}
          value={
            summary ? (
              <bdi>
                {(summary.reimbursementAmount ?? 0).toFixed(0)} ({summary.reimbursementPending ?? 0})
              </bdi>
            ) : (
              "—"
            )
          }
          tone="primary"
          icon={<BanknoteIcon size={18} />}
        />
        <ExpensesKpiCard
          title={t("kpi.personalRepaymentDue")}
          value={
            summary ? (
              <bdi>
                {(summary.personalRepaymentAmount ?? 0).toFixed(0)} ({summary.personalRepaymentDue ?? 0})
              </bdi>
            ) : (
              "—"
            )
          }
          tone="warning"
          icon={<ArrowRightLeftIcon size={18} />}
        />
        <ExpensesKpiCard
          title={t("kpi.outstandingAdvances")}
          value={
            summary ? (
              <bdi>
                {(summary.outstandingAdvanceAmount ?? 0).toFixed(0)} ({summary.outstandingAdvances ?? 0})
              </bdi>
            ) : (
              "—"
            )
          }
          tone="default"
          icon={<ReceiptIcon size={18} />}
        />
      </div>

      {/* Main Review Workplace */}
      <Surface style={{ padding: "1.25rem", display: "flex", flexDirection: "column", gap: "1.25rem" }}>
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: "1rem" }}>
          <div>
            <h2 style={{ margin: 0, fontSize: "1.125rem", color: "var(--color-content-primary, #0f172a)" }}>
              {t("review.title")}
            </h2>
            <p style={{ margin: "0.25rem 0 0", fontSize: "0.8125rem", color: "var(--color-content-muted, #64748b)" }}>
              {t("review.description")}
            </p>
          </div>

          <Button variant="primary" onClick={() => setIsNewExpenseOpen(true)}>
            + {t("selfService.newExpense")}
          </Button>
        </div>

        {/* Filter bar & Search */}
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: "1rem" }}>
          <div style={{ display: "flex", gap: "0.5rem", flexWrap: "wrap" }}>
            {[
              { id: null, label: t("review.filterAll") },
              { id: "Submitted", label: t("review.filterSubmitted") },
              { id: "Approved", label: t("review.filterApproved") },
              { id: "Finalized", label: t("review.filterFinalized") },
              { id: "Reversed", label: t("review.filterReversed") }
            ].map((pill) => (
              <button
                key={String(pill.id)}
                type="button"
                onClick={() => {
                  setStateFilter(pill.id);
                  setPage(1);
                }}
                style={{
                  padding: "0.375rem 0.75rem",
                  borderRadius: "var(--radius-full, 9999px)",
                  border: `1px solid ${stateFilter === pill.id ? "var(--color-brand-primary, #102a43)" : "var(--color-border-default, #cbd5e1)"}`,
                  background: stateFilter === pill.id ? "var(--color-brand-primary, #102a43)" : "#ffffff",
                  color: stateFilter === pill.id ? "#ffffff" : "var(--color-content-primary, #0f172a)",
                  fontSize: "0.8125rem",
                  fontWeight: 600,
                  cursor: "pointer"
                }}
              >
                {pill.label}
              </button>
            ))}
          </div>

          <div style={{ minWidth: "260px" }}>
            <TextField
              placeholder={t("review.searchPlaceholder")}
              value={search}
              onChange={(val) => {
                setSearch(val);
                setPage(1);
              }}
            />
          </div>
        </div>

        {/* Expense Table */}
        {expensesQuery.isLoading ? (
          <div style={{ padding: "3rem", textAlign: "center" }}>{t("common.loading")}</div>
        ) : !expensesQuery.data?.items || expensesQuery.data.items.length === 0 ? (
          <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted, #64748b)" }}>
            {t("review.empty")}
          </div>
        ) : (
          <>
            <Table>
              <thead>
                <tr>
                  <th>{t("common.merchant")}</th>
                  <th>{t("common.date")}</th>
                  <th>{t("common.amount")}</th>
                  <th>{t("common.source")}</th>
                  <th>{t("common.status")}</th>
                  <th>{t("review.approvalCol")}</th>
                  <th>{t("review.postingCol")}</th>
                  <th>{t("review.settlementCol")}</th>
                  <th>{t("common.actions")}</th>
                </tr>
              </thead>
              <tbody>
                {expensesQuery.data.items.map((exp) => (
                  <tr
                    key={exp.id}
                    style={{ cursor: "pointer" }}
                    onClick={() => setSelectedExpenseId(exp.id)}
                  >
                    <td style={{ fontWeight: 600 }}>{exp.merchantName}</td>
                    <td>{exp.expenseDate}</td>
                    <td style={{ fontWeight: 600 }}>
                      <bdi>
                        {exp.amount.toFixed(2)} {exp.currencyCode}
                      </bdi>
                    </td>
                    <td>
                      <PaymentSourceBadge source={exp.paymentSource} />
                    </td>
                    <td>
                      <ExpenseStateBadge state={exp.expenseState} />
                    </td>
                    <td>
                      <ApprovalStateBadge state={exp.approvalState} />
                    </td>
                    <td>
                      <AccountingStateBadge state={exp.accountingState} />
                    </td>
                    <td>
                      <SettlementStateBadge state={exp.settlementState} />
                    </td>
                    <td>
                      <Button
                        variant="secondary"
                        size="compact"
                        onClick={(e) => {
                          e.stopPropagation();
                          setSelectedExpenseId(exp.id);
                        }}
                      >
                        {t("review.inspectAction")}
                      </Button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </Table>

            {expensesQuery.data.totalCount > 15 && (
              <div style={{ display: "flex", justifyContent: "center", marginTop: "1rem" }}>
                <Pagination
                  page={page}
                  totalPages={Math.ceil(expensesQuery.data.totalCount / 15)}
                  onPageChange={setPage}
                />
              </div>
            )}
          </>
        )}
      </Surface>

      {/* Expense Detail Drawer */}
      <ExpenseDetailDrawer
        isOpen={Boolean(selectedExpenseId)}
        onClose={() => setSelectedExpenseId(null)}
        legalEntityId={legalEntityId}
        expenseId={selectedExpenseId}
        canApprove={true}
        canFinalize={true}
      />

      <NewExpenseModal
        isOpen={isNewExpenseOpen}
        onClose={() => setIsNewExpenseOpen(false)}
        legalEntityId={legalEntityId}
        isSelfService={false}
      />
    </div>
  );
}

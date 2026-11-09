import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Table } from "../../../../components/primitives/table.tsx";
import { Pagination } from "../../../../components/primitives/pagination.tsx";
import {
  useMyExpensesQuery,
  useMyAdvancesQuery,
  useMyCardsQuery,
  useMyReimbursementsQuery,
  useMyRepaymentsQuery,
  useMyReportsQuery
} from "../../hooks/use-expenses-queries.ts";
import {
  ExpenseStateBadge,
  ApprovalStateBadge,
  SettlementStateBadge,
  PaymentSourceBadge
} from "../../components/expense-state-badge.tsx";
import { NewExpenseModal } from "../modals/new-expense-modal.tsx";
import { RequestAdvanceModal } from "../modals/request-advance-modal.tsx";
import { ExpenseDetailDrawer } from "../drawers/expense-detail-drawer.tsx";
import { CreateExpenseFromCardModal } from "../modals/card-modals.tsx";
import type { ExpenseCardTransactionDto } from "../../types.ts";

export interface ExpensesSelfServiceTabProps {
  readonly legalEntityId: string;
}

export function ExpensesSelfServiceTab({
  legalEntityId
}: ExpensesSelfServiceTabProps): React.JSX.Element {
  const { t } = useTranslation("expenses");

  const [subtab, setSubtab] = useState<
    "myExpenses" | "myAdvances" | "myCards" | "myReimbursements" | "myRepayments" | "myReports"
  >("myExpenses");

  const [page, setPage] = useState(1);
  const [search, setSearch] = useState("");
  const [selectedExpenseId, setSelectedExpenseId] = useState<string | null>(null);
  const [isNewExpenseOpen, setIsNewExpenseOpen] = useState(false);
  const [isRequestAdvanceOpen, setIsRequestAdvanceOpen] = useState(false);
  const [selectedCard, setSelectedCard] = useState<ExpenseCardTransactionDto | null>(null);

  const myExpensesQuery = useMyExpensesQuery(legalEntityId, {
    search: search.trim() || null,
    page,
    pageSize: 15
  });

  const myAdvancesQuery = useMyAdvancesQuery(legalEntityId, { page, pageSize: 15 });
  const myCardsQuery = useMyCardsQuery(legalEntityId, { page, pageSize: 15 });
  const myReimbursementsQuery = useMyReimbursementsQuery(legalEntityId, { page, pageSize: 15 });
  const myRepaymentsQuery = useMyRepaymentsQuery(legalEntityId, { page, pageSize: 15 });
  const myReportsQuery = useMyReportsQuery(legalEntityId, { page, pageSize: 15 });

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
      {/* Header card with action buttons */}
      <Surface
        style={{
          padding: "1.25rem 1.5rem",
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          flexWrap: "wrap",
          gap: "1rem"
        }}
      >
        <div>
          <h2 style={{ margin: 0, fontSize: "1.25rem", color: "var(--color-content-primary, #0f172a)" }}>
            {t("selfService.title")}
          </h2>
          <p style={{ margin: "0.25rem 0 0", fontSize: "0.875rem", color: "var(--color-content-muted, #64748b)" }}>
            {t("selfService.description")}
          </p>
        </div>

        <div style={{ display: "flex", gap: "0.75rem", flexWrap: "wrap" }}>
          <Button variant="secondary" onClick={() => setIsRequestAdvanceOpen(true)}>
            + {t("selfService.requestAdvance")}
          </Button>
          <Button variant="primary" onClick={() => setIsNewExpenseOpen(true)}>
            + {t("selfService.newExpense")}
          </Button>
        </div>
      </Surface>

      {/* Subtabs selector */}
      <div
        style={{
          display: "flex",
          gap: "0.5rem",
          borderBottom: "2px solid var(--color-border-default, #cbd5e1)",
          paddingBottom: "0.5rem",
          overflowX: "auto"
        }}
      >
        {[
          { id: "myExpenses", label: t("selfService.subtabs.myExpenses") },
          { id: "myAdvances", label: t("selfService.subtabs.myAdvances") },
          { id: "myCards", label: t("selfService.subtabs.myCards") },
          { id: "myReimbursements", label: t("selfService.subtabs.myReimbursements") },
          { id: "myRepayments", label: t("selfService.subtabs.myRepayments") },
          { id: "myReports", label: t("selfService.subtabs.myReports") }
        ].map((item) => (
          <button
            key={item.id}
            type="button"
            onClick={() => {
              setSubtab(item.id as any);
              setPage(1);
            }}
            style={{
              padding: "0.5rem 1rem",
              borderRadius: "var(--radius-md, 0.5rem)",
              border: "none",
              background: subtab === item.id ? "var(--color-brand-primary, #102a43)" : "transparent",
              color: subtab === item.id ? "#ffffff" : "var(--color-content-primary, #0f172a)",
              fontWeight: 600,
              fontSize: "0.875rem",
              cursor: "pointer",
              whiteSpace: "nowrap"
            }}
          >
            {item.label}
          </button>
        ))}
      </div>

      {/* View: My Expenses */}
      {subtab === "myExpenses" && (
        <Surface style={{ padding: "1.25rem", display: "flex", flexDirection: "column", gap: "1rem" }}>
          <div style={{ maxWidth: "300px" }}>
            <TextField
              placeholder={t("review.searchPlaceholder")}
              value={search}
              onChange={(val) => {
                setSearch(val);
                setPage(1);
              }}
            />
          </div>

          {myExpensesQuery.isLoading ? (
            <div style={{ padding: "2rem", textAlign: "center" }}>{t("common.loading")}</div>
          ) : !myExpensesQuery.data?.items || myExpensesQuery.data.items.length === 0 ? (
            <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted, #64748b)" }}>
              {t("selfService.emptyExpenses")}
            </div>
          ) : (
            <>
              <div style={{ width: "100%", overflowX: "auto" }}>
                <Table style={{ minWidth: "750px" }}>
                  <thead>
                    <tr>
                      <th>{t("common.merchant")}</th>
                      <th>{t("common.date")}</th>
                      <th>{t("common.amount")}</th>
                      <th>{t("common.source")}</th>
                      <th>{t("common.status")}</th>
                      <th>{t("review.approvalCol")}</th>
                      <th>{t("review.settlementCol")}</th>
                      <th>{t("common.actions")}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {myExpensesQuery.data.items.map((exp) => (
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
              </div>

              {myExpensesQuery.data.totalCount > 15 && (
                <div style={{ display: "flex", justifyContent: "center", marginTop: "1rem" }}>
                  <Pagination
                    page={page}
                    totalPages={Math.ceil(myExpensesQuery.data.totalCount / 15)}
                    onPageChange={setPage}
                  />
                </div>
              )}
            </>
          )}
        </Surface>
      )}

      {/* View: My Advances */}
      {subtab === "myAdvances" && (
        <Surface style={{ padding: "1.25rem", display: "flex", flexDirection: "column", gap: "1rem" }}>
          {myAdvancesQuery.isLoading ? (
            <div style={{ padding: "2rem", textAlign: "center" }}>{t("common.loading")}</div>
          ) : !myAdvancesQuery.data?.items || myAdvancesQuery.data.items.length === 0 ? (
            <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted, #64748b)" }}>
              {t("selfService.emptyAdvances")}
            </div>
          ) : (
            <div style={{ width: "100%", overflowX: "auto" }}>
              <Table style={{ minWidth: "650px" }}>
                <thead>
                  <tr>
                    <th>{t("common.purpose")}</th>
                    <th>{t("common.amount")}</th>
                    <th>{t("advances.disbursed")}</th>
                    <th>{t("advances.applied")}</th>
                    <th>{t("advances.returned")}</th>
                    <th>{t("advances.outstanding")}</th>
                    <th>{t("common.status")}</th>
                  </tr>
                </thead>
                <tbody>
                  {myAdvancesQuery.data.items.map((adv) => {
                    const outstanding = adv.issuedAmount - adv.appliedAmount - adv.returnedAmount - adv.writtenOffAmount;
                    return (
                      <tr key={adv.id}>
                        <td style={{ fontWeight: 600 }}>{adv.purpose}</td>
                        <td>
                          <bdi>
                            {adv.amount.toFixed(2)} {adv.currencyCode}
                          </bdi>
                        </td>
                        <td>
                          <bdi>
                            {adv.issuedAmount.toFixed(2)} {adv.currencyCode}
                          </bdi>
                        </td>
                        <td>
                          <bdi>
                            {adv.appliedAmount.toFixed(2)} {adv.currencyCode}
                          </bdi>
                        </td>
                        <td>
                          <bdi>
                            {adv.returnedAmount.toFixed(2)} {adv.currencyCode}
                          </bdi>
                        </td>
                        <td style={{ fontWeight: 700, color: outstanding > 0 ? "var(--color-status-warning)" : "inherit" }}>
                          <bdi>
                            {outstanding.toFixed(2)} {adv.currencyCode}
                          </bdi>
                        </td>
                        <td>
                          <span
                            style={{
                              padding: "0.25rem 0.625rem",
                              borderRadius: "var(--radius-full, 9999px)",
                              fontSize: "0.75rem",
                              fontWeight: 600,
                              backgroundColor: adv.state === "Settled" ? "#ecfdf5" : "#fffbeb",
                              color: adv.state === "Settled" ? "#059669" : "#d97706"
                            }}
                          >
                            {t(`states.advance.${adv.state}` as any, { defaultValue: adv.state })}
                          </span>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </Table>
            </div>
          )}
        </Surface>
      )}

      {/* View: My Corporate Cards */}
      {subtab === "myCards" && (
        <Surface style={{ padding: "1.25rem", display: "flex", flexDirection: "column", gap: "1rem" }}>
          {myCardsQuery.isLoading ? (
            <div style={{ padding: "2rem", textAlign: "center" }}>{t("common.loading")}</div>
          ) : !myCardsQuery.data?.items || myCardsQuery.data.items.length === 0 ? (
            <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted, #64748b)" }}>
              {t("selfService.emptyCards")}
            </div>
          ) : (
            <div style={{ width: "100%", overflowX: "auto" }}>
              <Table style={{ minWidth: "600px" }}>
                <thead>
                  <tr>
                    <th>{t("common.merchant")}</th>
                    <th>{t("common.date")}</th>
                    <th>{t("common.amount")}</th>
                    <th>{t("common.status")}</th>
                    <th>{t("common.actions")}</th>
                  </tr>
                </thead>
                <tbody>
                  {myCardsQuery.data.items.map((card) => (
                    <tr key={card.id}>
                      <td style={{ fontWeight: 600 }}>{card.merchantName}</td>
                      <td>{card.transactionDate}</td>
                      <td style={{ fontWeight: 600 }}>
                        <bdi>
                          {card.amount.toFixed(2)} {card.currencyCode}
                        </bdi>
                      </td>
                      <td>
                        <span
                          style={{
                            padding: "0.25rem 0.625rem",
                            borderRadius: "var(--radius-full, 9999px)",
                            fontSize: "0.75rem",
                            fontWeight: 600,
                            backgroundColor: card.state === "Matched" ? "#ecfdf5" : "#f1f5f9",
                            color: card.state === "Matched" ? "#059669" : "#475569"
                          }}
                        >
                          {t(`states.card.${card.state}` as any, { defaultValue: card.state })}
                        </span>
                      </td>
                      <td>
                        {card.state === "Unmatched" && (
                          <Button variant="secondary" size="compact" onClick={() => setSelectedCard(card)}>
                            {t("corporateCards.createExpenseAction")}
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

      {/* View: My Reimbursements */}
      {subtab === "myReimbursements" && (
        <Surface style={{ padding: "1.25rem", display: "flex", flexDirection: "column", gap: "1rem" }}>
          {myReimbursementsQuery.isLoading ? (
            <div style={{ padding: "2rem", textAlign: "center" }}>{t("common.loading")}</div>
          ) : !myReimbursementsQuery.data?.items || myReimbursementsQuery.data.items.length === 0 ? (
            <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted, #64748b)" }}>
              {t("selfService.emptyReimbursements")}
            </div>
          ) : (
            <div style={{ width: "100%", overflowX: "auto" }}>
              <Table style={{ minWidth: "550px" }}>
                <thead>
                  <tr>
                    <th>{t("reimbursements.claimDate")}</th>
                    <th>{t("reimbursements.dueAmount")}</th>
                    <th>{t("reimbursements.paidAmount")}</th>
                    <th>{t("common.status")}</th>
                  </tr>
                </thead>
                <tbody>
                  {myReimbursementsQuery.data.items.map((reimb) => (
                    <tr key={reimb.id}>
                      <td>{reimb.createdAt.slice(0, 10)}</td>
                      <td style={{ fontWeight: 600 }}>
                        <bdi>
                          {reimb.amount.toFixed(2)} {reimb.currencyCode}
                        </bdi>
                      </td>
                      <td>
                        <bdi>
                          {reimb.settledAmount.toFixed(2)} {reimb.currencyCode}
                        </bdi>
                      </td>
                      <td>
                        <SettlementStateBadge
                          state={reimb.status === "Paid" ? "ReimbursementPaid" : "ReimbursementPending"}
                        />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </Table>
            </div>
          )}
        </Surface>
      )}

      {/* View: My Repayments */}
      {subtab === "myRepayments" && (
        <Surface style={{ padding: "1.25rem", display: "flex", flexDirection: "column", gap: "1rem" }}>
          {myRepaymentsQuery.isLoading ? (
            <div style={{ padding: "2rem", textAlign: "center" }}>{t("common.loading")}</div>
          ) : !myRepaymentsQuery.data?.items || myRepaymentsQuery.data.items.length === 0 ? (
            <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted, #64748b)" }}>
              {t("selfService.emptyRepayments")}
            </div>
          ) : (
            <div style={{ width: "100%", overflowX: "auto" }}>
              <Table style={{ minWidth: "600px" }}>
                <thead>
                  <tr>
                    <th>{t("reimbursements.reference")}</th>
                    <th>{t("reimbursements.entryDate")}</th>
                    <th>{t("reimbursements.totalAmount")}</th>
                    <th>{t("reimbursements.collectedAmount")}</th>
                    <th>{t("common.status")}</th>
                  </tr>
                </thead>
                <tbody>
                  {myRepaymentsQuery.data.items.map((rep) => (
                    <tr key={rep.id}>
                      <td>{rep.sourceReference}</td>
                      <td>{rep.createdAt.slice(0, 10)}</td>
                      <td style={{ fontWeight: 600 }}>
                        <bdi>
                          {rep.amount.toFixed(2)} {rep.currencyCode}
                        </bdi>
                      </td>
                      <td>
                        <bdi>
                          {rep.settledAmount.toFixed(2)} {rep.currencyCode}
                        </bdi>
                      </td>
                      <td>
                        <SettlementStateBadge
                          state={rep.state === "Settled" ? "RepaymentSettled" : "RepaymentPending"}
                        />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </Table>
            </div>
          )}
        </Surface>
      )}

      {/* View: My Reports */}
      {subtab === "myReports" && (
        <Surface style={{ padding: "1.25rem", display: "flex", flexDirection: "column", gap: "1rem" }}>
          {myReportsQuery.isLoading ? (
            <div style={{ padding: "2rem", textAlign: "center" }}>{t("common.loading")}</div>
          ) : !myReportsQuery.data?.items || myReportsQuery.data.items.length === 0 ? (
            <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted, #64748b)" }}>
              {t("selfService.emptyReports")}
            </div>
          ) : (
            <div style={{ width: "100%", overflowX: "auto" }}>
              <Table style={{ minWidth: "500px" }}>
                <thead>
                  <tr>
                    <th>{t("reports.reportTitle")}</th>
                    <th>{t("reports.createdAt")}</th>
                    <th>{t("common.amount")}</th>
                    <th>{t("common.status")}</th>
                  </tr>
                </thead>
                <tbody>
                  {myReportsQuery.data.items.map((rpt) => (
                    <tr key={rpt.id}>
                      <td style={{ fontWeight: 600 }}>{rpt.title}</td>
                      <td>{rpt.createdAt.slice(0, 10)}</td>
                      <td style={{ fontWeight: 600 }}>
                        <bdi>
                          {rpt.totalAmount.toFixed(2)} {rpt.currencyCode}
                        </bdi>
                      </td>
                      <td>
                        <ExpenseStateBadge state={rpt.state} />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </Table>
            </div>
          )}
        </Surface>
      )}

      {/* Modals & Detail Drawers */}
      <NewExpenseModal
        isOpen={isNewExpenseOpen}
        onClose={() => setIsNewExpenseOpen(false)}
        legalEntityId={legalEntityId}
        isSelfService={true}
      />

      <RequestAdvanceModal
        isOpen={isRequestAdvanceOpen}
        onClose={() => setIsRequestAdvanceOpen(false)}
        legalEntityId={legalEntityId}
        isSelfService={true}
      />

      <ExpenseDetailDrawer
        isOpen={Boolean(selectedExpenseId)}
        onClose={() => setSelectedExpenseId(null)}
        legalEntityId={legalEntityId}
        expenseId={selectedExpenseId}
        canApprove={false}
        canFinalize={false}
      />

      <CreateExpenseFromCardModal
        isOpen={Boolean(selectedCard)}
        onClose={() => setSelectedCard(null)}
        legalEntityId={legalEntityId}
        cardTransaction={selectedCard}
      />
    </div>
  );
}

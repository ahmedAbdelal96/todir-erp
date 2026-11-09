import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Table } from "../../../../components/primitives/table.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Pagination } from "../../../../components/primitives/pagination.tsx";
import { ExpensesKpiCard } from "../../components/expenses-kpi-card.tsx";
import {
  CreditCardIcon,
  CheckCircleIcon,
  UserIcon,
  AlertCircleIcon
} from "../../components/expenses-icons.tsx";
import {
  useExpenseCardTransactionsQuery,
  useExpenseCardReconciliationQuery,
  useMarkCardPersonalMutation
} from "../../hooks/use-expenses-queries.ts";
import {
  MatchCardModal,
  CreateExpenseFromCardModal,
  RecordCardAdjustmentModal
} from "../modals/card-modals.tsx";
import type { ExpenseCardTransactionDto } from "../../types.ts";

export interface ExpensesCorporateCardsTabProps {
  readonly legalEntityId: string;
}

export function ExpensesCorporateCardsTab({ legalEntityId }: ExpensesCorporateCardsTabProps): React.JSX.Element {
  const { t } = useTranslation("expenses");

  const [stateFilter, setStateFilter] = useState<string | null>(null);
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);

  const [matchCard, setMatchCard] = useState<ExpenseCardTransactionDto | null>(null);
  const [createCard, setCreateCard] = useState<ExpenseCardTransactionDto | null>(null);
  const [adjustCard, setAdjustCard] = useState<ExpenseCardTransactionDto | null>(null);

  const reconQuery = useExpenseCardReconciliationQuery(legalEntityId);
  const cardsQuery = useExpenseCardTransactionsQuery(legalEntityId, {
    state: stateFilter,
    search: search.trim() || null,
    page,
    pageSize: 15
  });

  const markPersonalMutation = useMarkCardPersonalMutation(legalEntityId);
  const recon = reconQuery.data;

  const handleMarkPersonal = async (card: ExpenseCardTransactionDto) => {
    await markPersonalMutation.mutateAsync({
      cardId: card.id,
      request: { employeeId: card.employeeId || "00000000-0000-0000-0000-000000000001" }
    });
  };

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
      {/* Reconciliation KPI Ribbon */}
      <div style={{ display: "flex", gap: "1rem", flexWrap: "wrap" }}>
        <ExpensesKpiCard
          title={t("kpi.statementAmount")}
          value={
            recon ? (
              <bdi>
                {(recon.statementAmount ?? 0).toFixed(0)} ({recon.totalTransactions ?? 0})
              </bdi>
            ) : (
              "—"
            )
          }
          tone="default"
          icon={<CreditCardIcon size={18} />}
        />
        <ExpensesKpiCard
          title={t("kpi.matchedAmount")}
          value={recon ? <bdi>{(recon.matchedAmount ?? 0).toFixed(0)}</bdi> : "—"}
          tone="success"
          icon={<CheckCircleIcon size={18} />}
          onClick={() => {
            setStateFilter("Matched");
            setPage(1);
          }}
        />
        <ExpensesKpiCard
          title={t("kpi.personalAmount")}
          value={recon ? <bdi>{(recon.personalAmount ?? 0).toFixed(0)}</bdi> : "—"}
          tone="warning"
          icon={<UserIcon size={18} />}
          onClick={() => {
            setStateFilter("Personal");
            setPage(1);
          }}
        />
        <ExpensesKpiCard
          title={t("kpi.unclassifiedAmount")}
          value={recon ? <bdi>{(recon.unclassifiedAmount ?? 0).toFixed(0)}</bdi> : "—"}
          tone="danger"
          icon={<AlertCircleIcon size={18} />}
          onClick={() => {
            setStateFilter("Unmatched");
            setPage(1);
          }}
        />
      </div>

      {/* Main Inbox Surface */}
      <Surface style={{ padding: "1.25rem", display: "flex", flexDirection: "column", gap: "1.25rem" }}>
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: "1rem" }}>
          <div>
            <h2 style={{ margin: 0, fontSize: "1.125rem", color: "var(--color-content-primary, #0f172a)" }}>
              {t("corporateCards.title")}
            </h2>
            <p style={{ margin: "0.25rem 0 0", fontSize: "0.8125rem", color: "var(--color-content-muted, #64748b)" }}>
              {t("corporateCards.description")}
            </p>
          </div>
        </div>

        {/* Filter Bar */}
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: "1rem" }}>
          <div style={{ display: "flex", gap: "0.5rem", flexWrap: "wrap" }}>
            {[
              { id: null, label: t("corporateCards.filterAll") },
              { id: "Unmatched", label: t("corporateCards.filterUnmatched") },
              { id: "Matched", label: t("corporateCards.filterMatched") },
              { id: "Personal", label: t("corporateCards.filterPersonal") },
              { id: "Ignored", label: t("corporateCards.filterIgnored") }
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

        {/* Card Transactions Table */}
        {cardsQuery.isLoading ? (
          <div style={{ padding: "3rem", textAlign: "center" }}>{t("common.loading")}</div>
        ) : !cardsQuery.data?.items || cardsQuery.data.items.length === 0 ? (
          <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted, #64748b)" }}>
            {t("corporateCards.empty")}
          </div>
        ) : (
          <>
            <div style={{ width: "100%", overflowX: "auto" }}>
              <Table style={{ minWidth: "700px" }}>
                <thead>
                  <tr>
                    <th>{t("common.merchant")}</th>
                    <th>{t("corporateCards.bankReference")}</th>
                    <th>{t("common.date")}</th>
                    <th>{t("common.amount")}</th>
                    <th>{t("common.status")}</th>
                    <th>{t("common.actions")}</th>
                  </tr>
                </thead>
                <tbody>
                {cardsQuery.data.items.map((card) => (
                  <tr key={card.id}>
                    <td style={{ fontWeight: 600 }}>{card.merchantName}</td>
                    <td>{card.providerReference}</td>
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
                          backgroundColor:
                            card.state === "Matched"
                              ? "#ecfdf5"
                              : card.state === "Personal"
                                ? "#fffbeb"
                                : "#f1f5f9",
                          color:
                            card.state === "Matched"
                              ? "#059669"
                              : card.state === "Personal"
                                ? "#d97706"
                                : "#475569"
                        }}
                      >
                        {t(`states.card.${card.state}` as any, { defaultValue: card.state })}
                      </span>
                    </td>
                    <td>
                      <div style={{ display: "flex", gap: "0.375rem", flexWrap: "wrap" }}>
                        {card.state === "Unmatched" && (
                          <>
                            <Button variant="primary" size="compact" onClick={() => setCreateCard(card)}>
                              {t("corporateCards.createExpenseAction")}
                            </Button>
                            <Button variant="secondary" size="compact" onClick={() => setMatchCard(card)}>
                              {t("corporateCards.matchAction")}
                            </Button>
                            <Button
                              variant="secondary"
                              size="compact"
                              onClick={() => handleMarkPersonal(card)}
                              isDisabled={markPersonalMutation.isPending}
                            >
                              {t("corporateCards.personalAction")}
                            </Button>
                          </>
                        )}
                        <Button variant="secondary" size="compact" onClick={() => setAdjustCard(card)}>
                          {t("corporateCards.adjustAction")}
                        </Button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </Table>
          </div>

            {cardsQuery.data.totalCount > 15 && (
              <div style={{ display: "flex", justifyContent: "center", marginTop: "1rem" }}>
                <Pagination
                  page={page}
                  totalPages={Math.ceil(cardsQuery.data.totalCount / 15)}
                  onPageChange={setPage}
                />
              </div>
            )}
          </>
        )}
      </Surface>

      <MatchCardModal
        isOpen={Boolean(matchCard)}
        onClose={() => setMatchCard(null)}
        legalEntityId={legalEntityId}
        cardTransaction={matchCard}
      />

      <CreateExpenseFromCardModal
        isOpen={Boolean(createCard)}
        onClose={() => setCreateCard(null)}
        legalEntityId={legalEntityId}
        cardTransaction={createCard}
      />

      <RecordCardAdjustmentModal
        isOpen={Boolean(adjustCard)}
        onClose={() => setAdjustCard(null)}
        legalEntityId={legalEntityId}
        cardTransaction={adjustCard}
      />
    </div>
  );
}

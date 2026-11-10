import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Table } from "../../../../components/primitives/table.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import {
  useExpenseRepaymentsQuery,
  useExpenseReimbursementBatchesQuery
} from "../../hooks/use-expenses-queries.ts";
import { useIsMobile } from "../../hooks/use-is-mobile.ts";
import {
  PrepareBatchModal,
  PayBatchModal,
  SettleRepaymentModal
} from "../modals/reimbursement-modals.tsx";
import type { ExpenseEmployeeRepaymentDto } from "../../types.ts";

export interface ExpensesReimbursementsTabProps {
  readonly legalEntityId: string;
}

export function ExpensesReimbursementsTab({ legalEntityId }: ExpensesReimbursementsTabProps): React.JSX.Element {
  const { t } = useTranslation("expenses");
  const isMobile = useIsMobile(768);

  const [subtab, setSubtab] = useState<"toEmployees" | "fromEmployees">("toEmployees");
  const [isPrepareOpen, setIsPrepareOpen] = useState(false);
  const [payBatch, setPayBatch] = useState<{ id: string; version: number } | null>(null);
  const [settleRepayment, setSettleRepayment] = useState<ExpenseEmployeeRepaymentDto | null>(null);

  const batchesQuery = useExpenseReimbursementBatchesQuery(legalEntityId);
  const repaymentsQuery = useExpenseRepaymentsQuery(legalEntityId);

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
      <Surface style={{ padding: "1.25rem", display: "flex", flexDirection: "column", gap: "1.25rem" }}>
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: "1rem" }}>
          <div>
            <h2 style={{ margin: 0, fontSize: "1.125rem", color: "var(--color-content-primary, #0f172a)" }}>
              {t("reimbursements.title")}
            </h2>
            <p style={{ margin: "0.25rem 0 0", fontSize: "0.8125rem", color: "var(--color-content-muted, #64748b)" }}>
              {t("reimbursements.description")}
            </p>
          </div>

          {subtab === "toEmployees" && (
            <Button variant="primary" onClick={() => setIsPrepareOpen(true)}>
              + {t("reimbursements.prepareBatch")}
            </Button>
          )}
        </div>

        {/* Subtabs selector */}
        <div style={{ display: "flex", gap: "0.5rem", borderBottom: "2px solid var(--color-border-default, #cbd5e1)", paddingBottom: "0.5rem" }}>
          <button
            type="button"
            onClick={() => setSubtab("toEmployees")}
            style={{
              padding: "0.5rem 1rem",
              borderRadius: "var(--radius-md, 0.5rem)",
              border: "none",
              background: subtab === "toEmployees" ? "var(--color-brand-primary, #102a43)" : "transparent",
              color: subtab === "toEmployees" ? "#ffffff" : "var(--color-content-primary, #0f172a)",
              fontWeight: 600,
              fontSize: "0.875rem",
              cursor: "pointer"
            }}
          >
            {t("reimbursements.subtabs.toEmployees")}
          </button>
          <button
            type="button"
            onClick={() => setSubtab("fromEmployees")}
            style={{
              padding: "0.5rem 1rem",
              borderRadius: "var(--radius-md, 0.5rem)",
              border: "none",
              background: subtab === "fromEmployees" ? "var(--color-brand-primary, #102a43)" : "transparent",
              color: subtab === "fromEmployees" ? "#ffffff" : "var(--color-content-primary, #0f172a)",
              fontWeight: 600,
              fontSize: "0.875rem",
              cursor: "pointer"
            }}
          >
            {t("reimbursements.subtabs.fromEmployees")}
          </button>
        </div>

        {/* View 1: To Employees (Prepared Batches) */}
        {subtab === "toEmployees" && (
          <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
            {batchesQuery.isLoading ? (
              <div style={{ padding: "3rem", textAlign: "center" }}>{t("common.loading")}</div>
            ) : !batchesQuery.data || batchesQuery.data.length === 0 ? (
              <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted, #64748b)" }}>
                {t("reimbursements.emptyBatches")}
              </div>
            ) : isMobile ? (
              <div style={{ display: "flex", flexDirection: "column", gap: "0.75rem" }}>
                {batchesQuery.data.map((batch) => (
                  <div
                    key={batch.id}
                    style={{
                      backgroundColor: "#ffffff",
                      border: "1px solid #cbd5e1",
                      borderRadius: "0.5rem",
                      padding: "0.875rem",
                      display: "flex",
                      flexDirection: "column",
                      gap: "0.5rem"
                    }}
                  >
                    <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", gap: "0.5rem" }}>
                      <div>
                        <div style={{ fontWeight: 600, fontSize: "0.875rem" }}>
                          {batch.id.slice(0, 8)}...
                        </div>
                        <div style={{ fontSize: "0.75rem", color: "var(--color-content-muted, #64748b)" }}>
                          {batch.createdAt.slice(0, 10)}
                        </div>
                      </div>
                      <span
                        style={{
                          padding: "0.25rem 0.625rem",
                          borderRadius: "var(--radius-full, 9999px)",
                          fontSize: "0.75rem",
                          fontWeight: 600,
                          backgroundColor: batch.state === "Paid" ? "#ecfdf5" : "#eff6ff",
                          color: batch.state === "Paid" ? "#059669" : "#2563eb",
                          flexShrink: 0
                        }}
                      >
                        {batch.state === "Paid" ? t("reimbursements.batchPaidStatus") : t("reimbursements.batchPreparedStatus")}
                      </span>
                    </div>

                    <div style={{ fontSize: "0.8125rem" }}>
                      <span style={{ color: "var(--color-content-muted, #64748b)" }}>{t("reimbursements.batchTotal")}: </span>
                      <bdi style={{ fontWeight: 700 }}>{batch.totalAmount.toFixed(2)} {batch.currencyCode}</bdi>
                    </div>

                    {batch.state === "Prepared" && (
                      <div style={{ marginTop: "0.25rem", paddingTop: "0.5rem", borderTop: "1px solid #f1f5f9" }}>
                        <Button variant="primary" size="compact" onClick={() => setPayBatch({ id: batch.id, version: batch.version })}>
                          {t("reimbursements.payBatchAction")}
                        </Button>
                      </div>
                    )}
                  </div>
                ))}
              </div>
            ) : (
              <div style={{ width: "100%", overflowX: "auto" }}>
                <Table style={{ minWidth: "650px" }}>
                  <thead>
                    <tr>
                      <th>{t("reimbursements.batchId")}</th>
                      <th>{t("reimbursements.preparedDate")}</th>
                      <th>{t("reimbursements.batchTotal")}</th>
                      <th>{t("common.status")}</th>
                      <th>{t("common.actions")}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {batchesQuery.data.map((batch) => (
                      <tr key={batch.id}>
                        <td style={{ fontWeight: 600 }}>{batch.id}</td>
                        <td>{batch.createdAt.slice(0, 10)}</td>
                        <td style={{ fontWeight: 600 }}>
                          <bdi>
                            {batch.totalAmount.toFixed(2)} {batch.currencyCode}
                          </bdi>
                        </td>
                        <td>
                          <span
                            style={{
                              padding: "0.25rem 0.625rem",
                              borderRadius: "var(--radius-full, 9999px)",
                              fontSize: "0.75rem",
                              fontWeight: 600,
                              backgroundColor: batch.state === "Paid" ? "#ecfdf5" : "#eff6ff",
                              color: batch.state === "Paid" ? "#059669" : "#2563eb"
                            }}
                          >
                            {batch.state === "Paid" ? t("reimbursements.batchPaidStatus") : t("reimbursements.batchPreparedStatus")}
                          </span>
                        </td>
                        <td>
                          {batch.state === "Prepared" && (
                            <Button variant="primary" size="compact" onClick={() => setPayBatch({ id: batch.id, version: batch.version })}>
                              {t("reimbursements.payBatchAction")}
                            </Button>
                          )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </Table>
              </div>
            )}
          </div>
        )}

        {/* View 2: From Employees (Repayments) */}
        {subtab === "fromEmployees" && (
          <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
            {repaymentsQuery.isLoading ? (
              <div style={{ padding: "3rem", textAlign: "center" }}>{t("common.loading")}</div>
            ) : !repaymentsQuery.data?.items || repaymentsQuery.data.items.length === 0 ? (
              <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted, #64748b)" }}>
                {t("reimbursements.emptyRepayments")}
              </div>
            ) : isMobile ? (
              <div style={{ display: "flex", flexDirection: "column", gap: "0.75rem" }}>
                {repaymentsQuery.data.items.map((rep) => {
                  const remaining = rep.amount - rep.settledAmount;
                  return (
                    <div
                      key={rep.id}
                      style={{
                        backgroundColor: "#ffffff",
                        border: "1px solid #cbd5e1",
                        borderRadius: "0.5rem",
                        padding: "0.875rem",
                        display: "flex",
                        flexDirection: "column",
                        gap: "0.5rem"
                      }}
                    >
                      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", gap: "0.5rem" }}>
                        <div>
                          <div style={{ fontWeight: 600, fontSize: "0.875rem" }}>{rep.sourceReference}</div>
                          <div style={{ fontSize: "0.75rem", color: "var(--color-content-muted, #64748b)" }}>
                            {rep.createdAt.slice(0, 10)}
                          </div>
                        </div>
                        <span
                          style={{
                            padding: "0.25rem 0.625rem",
                            borderRadius: "var(--radius-full, 9999px)",
                            fontSize: "0.75rem",
                            fontWeight: 600,
                            backgroundColor: rep.state === "Settled" ? "#ecfdf5" : "#fffbeb",
                            color: rep.state === "Settled" ? "#059669" : "#d97706",
                            flexShrink: 0
                          }}
                        >
                          {rep.state === "Settled" ? t("reimbursements.collectedStatus") : t("reimbursements.dueStatus")}
                        </span>
                      </div>

                      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.375rem", fontSize: "0.8125rem" }}>
                        <div>
                          <span style={{ color: "var(--color-content-muted, #64748b)" }}>{t("reimbursements.totalAmount")}: </span>
                          <bdi style={{ fontWeight: 600 }}>{rep.amount.toFixed(2)} {rep.currencyCode}</bdi>
                        </div>
                        <div>
                          <span style={{ color: "var(--color-content-muted, #64748b)" }}>{t("reimbursements.collectedAmount")}: </span>
                          <bdi style={{ fontWeight: 600 }}>{rep.settledAmount.toFixed(2)} {rep.currencyCode}</bdi>
                        </div>
                        <div style={{ gridColumn: "span 2" }}>
                          <span style={{ color: "var(--color-content-muted, #64748b)" }}>{t("reimbursements.remainingAmount")}: </span>
                          <bdi style={{ fontWeight: 700, color: remaining > 0 ? "var(--color-status-danger)" : "inherit" }}>
                            {remaining.toFixed(2)} {rep.currencyCode}
                          </bdi>
                        </div>
                      </div>

                      {rep.state !== "Settled" && (
                        <div style={{ marginTop: "0.25rem", paddingTop: "0.5rem", borderTop: "1px solid #f1f5f9" }}>
                          <Button variant="primary" size="compact" onClick={() => setSettleRepayment(rep)}>
                            {t("reimbursements.settleRepaymentAction")}
                          </Button>
                        </div>
                      )}
                    </div>
                  );
                })}
              </div>
            ) : (
              <div style={{ width: "100%", overflowX: "auto" }}>
                <Table style={{ minWidth: "650px" }}>
                  <thead>
                    <tr>
                      <th>{t("reimbursements.reference")}</th>
                      <th>{t("reimbursements.entryDate")}</th>
                      <th>{t("reimbursements.totalAmount")}</th>
                      <th>{t("reimbursements.collectedAmount")}</th>
                      <th>{t("reimbursements.remainingAmount")}</th>
                      <th>{t("common.status")}</th>
                      <th>{t("common.actions")}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {repaymentsQuery.data.items.map((rep) => {
                      const remaining = rep.amount - rep.settledAmount;
                      return (
                        <tr key={rep.id}>
                          <td style={{ fontWeight: 600 }}>{rep.sourceReference}</td>
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
                          <td style={{ fontWeight: 700, color: remaining > 0 ? "var(--color-status-danger)" : "inherit" }}>
                            <bdi>
                              {remaining.toFixed(2)} {rep.currencyCode}
                            </bdi>
                          </td>
                          <td>
                            <span
                              style={{
                                padding: "0.25rem 0.625rem",
                                borderRadius: "var(--radius-full, 9999px)",
                                fontSize: "0.75rem",
                                fontWeight: 600,
                                backgroundColor: rep.state === "Settled" ? "#ecfdf5" : "#fffbeb",
                                color: rep.state === "Settled" ? "#059669" : "#d97706"
                              }}
                            >
                              {rep.state === "Settled" ? t("reimbursements.collectedStatus") : t("reimbursements.dueStatus")}
                            </span>
                          </td>
                          <td>
                            {rep.state !== "Settled" && (
                              <Button variant="primary" size="compact" onClick={() => setSettleRepayment(rep)}>
                                {t("reimbursements.settleRepaymentAction")}
                              </Button>
                            )}
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </Table>
              </div>
            )}
          </div>
        )}
      </Surface>

      <PrepareBatchModal
        isOpen={isPrepareOpen}
        onClose={() => setIsPrepareOpen(false)}
        legalEntityId={legalEntityId}
      />

      <PayBatchModal
        isOpen={Boolean(payBatch)}
        onClose={() => setPayBatch(null)}
        legalEntityId={legalEntityId}
        batchId={payBatch?.id ?? ""}
        version={payBatch?.version ?? 1}
      />

      <SettleRepaymentModal
        isOpen={Boolean(settleRepayment)}
        onClose={() => setSettleRepayment(null)}
        legalEntityId={legalEntityId}
        repayment={settleRepayment}
      />
    </div>
  );
}

import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Table } from "../../../../components/primitives/table.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import {
  useExpenseRepaymentsQuery
} from "../../hooks/use-expenses-queries.ts";
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

  const [subtab, setSubtab] = useState<"toEmployees" | "fromEmployees">("toEmployees");
  const [isPrepareOpen, setIsPrepareOpen] = useState(false);
  const [payBatch, setPayBatch] = useState<{ id: string; version: number } | null>(null);
  const [settleRepayment, setSettleRepayment] = useState<ExpenseEmployeeRepaymentDto | null>(null);

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
            <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted, #64748b)" }}>
              {t("reimbursements.emptyBatches")}
            </div>
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
            ) : (
              <Table>
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

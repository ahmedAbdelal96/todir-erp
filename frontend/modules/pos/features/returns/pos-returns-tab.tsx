import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "../../../../components/primitives/button.tsx";
import { BidiText } from "../../../../components/primitives/bidi-text.tsx";
import { ReturnModal } from "../../components/return-modals.tsx";
import { ReturnIcon } from "../../components/pos-icons.tsx";
import {
  useReturnsReportQuery,
  useSalesReportQuery,
  usePaymentAccountsQuery,
  useCreateReturnMutation,
  useRefundReturnMutation,
  useReturnCreditNoteMutation,
  useCompleteReturnMutation,
} from "../../hooks/use-pos-queries.ts";
import type { CreateReturnRequest, SessionResponse } from "../../types.ts";

export interface PosReturnsTabProps {
  readonly legalEntityId: string;
  readonly activeSession: SessionResponse | null;
}

export function PosReturnsTab({
  legalEntityId,
  activeSession,
}: PosReturnsTabProps): React.JSX.Element {
  const { t } = useTranslation("pos");
  const [isReturnModalOpen, setIsReturnModalOpen] = useState(false);
  const [statusMessage, setStatusMessage] = useState<string | null>(null);

  const { data: returnsData, refetch } = useReturnsReportQuery(legalEntityId);
  const { data: salesData } = useSalesReportQuery(legalEntityId, undefined, undefined, 1);
  const { data: paymentAccountsData } = usePaymentAccountsQuery(legalEntityId);

  const createReturnMutation = useCreateReturnMutation();
  const refundMutation = useRefundReturnMutation();
  const creditNoteMutation = useReturnCreditNoteMutation();
  const completeMutation = useCompleteReturnMutation();

  const returnsList = (returnsData as any)?.items || [];
  const completedSales = (salesData as any)?.items || [];
  const paymentAccounts = paymentAccountsData || [];
  const defaultCashAccount =
    paymentAccounts.find(
      (a) =>
        (a.type?.toLowerCase() === "cashbox" || a.type === "Cashbox") &&
        (a.status === "Active" || !a.status)
    ) || paymentAccounts[0];

  const handleCreateReturn = async (request: CreateReturnRequest) => {
    try {
      await createReturnMutation.mutateAsync(request);
      refetch();
      setStatusMessage(t("returns.createdSuccess"));
    } catch (err: unknown) {
      throw err;
    }
  };

  const handleRefund = async (returnId: string) => {
    if (!defaultCashAccount?.id) {
      setStatusMessage(t("tender.noCashAccountAvailable", "لا يوجد حساب خزينة متاح لصرف الاسترداد النقدي"));
      return;
    }

    const returnItem = returnsList.find((r: any) => r.id === returnId);
    const refundAmount = returnItem?.total || returnItem?.totalAmount || 0;

    try {
      await refundMutation.mutateAsync({
        returnId,
        request: {
          paymentAccountId: defaultCashAccount.id,
          amount: refundAmount,
          reasonCode: "CustomerReturn",
        },
      });
      refetch();
      setStatusMessage(t("returns.refundProcessed"));
    } catch (err: unknown) {
      setStatusMessage(err instanceof Error ? err.message : "Refund failed");
    }
  };

  const handleCreditNote = async (returnId: string) => {
    try {
      await creditNoteMutation.mutateAsync(returnId);
      refetch();
      setStatusMessage(t("returns.creditNoteIssued"));
    } catch (err: unknown) {
      setStatusMessage(err instanceof Error ? err.message : "Credit note failed");
    }
  };

  const handleCompleteReturn = async (returnId: string) => {
    try {
      await completeMutation.mutateAsync(returnId);
      refetch();
      setStatusMessage(t("returns.completedSuccess"));
    } catch (err: unknown) {
      setStatusMessage(err instanceof Error ? err.message : "Complete return failed");
    }
  };

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
      {/* Header Bar */}
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: "1rem" }}>
        <div>
          <h2 style={{ fontSize: "1.25rem", fontWeight: 700, margin: 0, color: "var(--color-content-default)" }}>
            {t("returns.tabTitle")}
          </h2>
          <p style={{ margin: "0.25rem 0 0", fontSize: "0.875rem", color: "var(--color-content-muted)" }}>
            {t("returns.tabSubtitle")}
          </p>
        </div>

        <Button
          variant="primary"
          onClick={() => setIsReturnModalOpen(true)}
          isDisabled={!activeSession}
        >
          + {t("returns.newReturnAction")}
        </Button>
      </div>

      {statusMessage && (
        <div
          role="status"
          style={{
            padding: "0.6rem 1rem",
            borderRadius: "var(--radius-md)",
            backgroundColor: "rgba(16, 185, 129, 0.1)",
            border: "1px solid rgba(16, 185, 129, 0.3)",
            color: "var(--color-status-success)",
            fontSize: "0.875rem",
          }}
        >
          {statusMessage}
        </div>
      )}

      {/* Returns Table */}
      <div
        style={{
          backgroundColor: "#ffffff",
          borderRadius: "var(--radius-lg)",
          border: "1px solid #cbd5e1",
          overflowX: "auto",
          boxShadow: "0 1px 3px 0 rgba(0, 0, 0, 0.05)",
        }}
      >
        <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "0.875rem", textAlign: "start" }}>
          <thead>
            <tr style={{ backgroundColor: "#f8fafc", borderBottom: "1px solid #e2e8f0" }}>
              <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "#475569" }}>{t("returns.returnId")}</th>
              <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "#475569" }}>{t("returns.originalSale")}</th>
              <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "#475569" }}>{t("returns.reason")}</th>
              <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "#475569" }}>{t("returns.disposition")}</th>
              <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "#475569" }}>{t("returns.totalAmount")}</th>
              <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "#475569" }}>{t("returns.status")}</th>
              <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "#475569" }}>{t("returns.refundState")}</th>
              <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "#475569", textAlign: "end" }}>{t("returns.actions")}</th>
            </tr>
          </thead>
          <tbody>
            {returnsList.length === 0 ? (
              <tr>
                <td colSpan={8} style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted)" }}>
                  <ReturnIcon size={36} style={{ margin: "0 auto 0.5rem", opacity: 0.4 }} />
                  <div>{t("returns.noReturnsYet")}</div>
                </td>
              </tr>
            ) : (
              returnsList.map((ret: any) => (
                <tr key={ret.id} style={{ borderBottom: "1px solid #f1f5f9" }}>
                  <td style={{ padding: "0.75rem 1rem", fontWeight: 600 }}>
                    <BidiText>{ret.id.slice(0, 8)}</BidiText>
                  </td>
                  <td style={{ padding: "0.75rem 1rem" }}>
                    <BidiText>{ret.saleId ? ret.saleId.slice(0, 8) : ret.originalSaleId ? ret.originalSaleId.slice(0, 8) : "—"}</BidiText>
                  </td>
                  <td style={{ padding: "0.75rem 1rem" }}>{ret.reason}</td>
                  <td style={{ padding: "0.75rem 1rem" }}>{ret.disposition}</td>
                  <td style={{ padding: "0.75rem 1rem", fontWeight: 700, color: "var(--color-brand-primary)" }}>
                    <BidiText>{(ret.total ?? ret.totalAmount ?? 0).toFixed(2)}</BidiText>
                  </td>
                  <td style={{ padding: "0.75rem 1rem" }}>
                    <span
                      style={{
                        padding: "0.2rem 0.5rem",
                        borderRadius: "var(--radius-full)",
                        fontSize: "0.75rem",
                        fontWeight: 600,
                        backgroundColor: ret.state === "Completed" ? "#dcfce7" : "#fef3c7",
                        color: ret.state === "Completed" ? "#15803d" : "#b45309",
                      }}
                    >
                      {ret.state}
                    </span>
                  </td>
                  <td style={{ padding: "0.75rem 1rem" }}>
                    <span
                      style={{
                        padding: "0.2rem 0.5rem",
                        borderRadius: "var(--radius-full)",
                        fontSize: "0.75rem",
                        fontWeight: 600,
                        backgroundColor: ret.refundState === "Completed" ? "#dcfce7" : "#f1f5f9",
                        color: ret.refundState === "Completed" ? "#15803d" : "#475569",
                      }}
                    >
                      {ret.refundState === "Completed"
                        ? t("returns.refundStateCompleted")
                        : t("returns.refundStateNotRequested")}
                    </span>
                  </td>
                  <td style={{ padding: "0.75rem 1rem", textAlign: "end" }}>
                    <div style={{ display: "inline-flex", gap: "0.35rem" }}>
                      {ret.refundState !== "Completed" && (
                        <Button
                          size="compact"
                          variant="secondary"
                          onClick={() => handleRefund(ret.id)}
                          isDisabled={refundMutation.isPending}
                        >
                          {t("returns.processRefund")}
                        </Button>
                      )}

                      <Button
                        size="compact"
                        variant="secondary"
                        onClick={() => handleCreditNote(ret.id)}
                        isDisabled={creditNoteMutation.isPending}
                      >
                        {t("returns.issueCreditNote")}
                      </Button>

                      {ret.state !== "Completed" && (
                        <Button
                          size="compact"
                          variant="primary"
                          onClick={() => handleCompleteReturn(ret.id)}
                          isDisabled={completeMutation.isPending}
                        >
                          {t("returns.completeAction")}
                        </Button>
                      )}
                    </div>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      <ReturnModal
        isOpen={isReturnModalOpen}
        onClose={() => setIsReturnModalOpen(false)}
        sessionId={activeSession?.id || ""}
        sales={completedSales}
        onCreateReturn={handleCreateReturn}
        isSubmitting={createReturnMutation.isPending}
      />
    </div>
  );
}

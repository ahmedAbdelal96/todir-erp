import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "../../../../components/primitives/button.tsx";
import { BidiText } from "../../../../components/primitives/bidi-text.tsx";
import { ReturnModal } from "../../components/return-modals.tsx";
import { ReturnIcon } from "../../components/pos-icons.tsx";
import {
  useReturnsReportQuery,
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
  const createReturnMutation = useCreateReturnMutation();
  const refundMutation = useRefundReturnMutation();
  const creditNoteMutation = useReturnCreditNoteMutation();
  const completeMutation = useCompleteReturnMutation();

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
    try {
      await refundMutation.mutateAsync({
        returnId,
        request: {
          paymentAccountId: "00000000-0000-0000-0000-000000000000",
          amount: 50,
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

  const returnsList = (returnsData as any)?.items || [];

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
          style={{ display: "flex", gap: "0.5rem", alignItems: "center" }}
        >
          <ReturnIcon size={18} />
          <span>+ {t("returns.createReturnAction")}</span>
        </Button>
      </div>

      {statusMessage && (
        <div
          role="status"
          style={{
            padding: "0.75rem 1rem",
            borderRadius: "var(--radius-md)",
            backgroundColor: "rgba(16, 185, 129, 0.1)",
            border: "1px solid rgba(16, 185, 129, 0.3)",
            color: "var(--color-status-success)",
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
          }}
        >
          <span>{statusMessage}</span>
          <button
            type="button"
            onClick={() => setStatusMessage(null)}
            style={{ background: "none", border: "none", cursor: "pointer", fontSize: "1rem" }}
          >
            ×
          </button>
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
            <tr
              style={{
                backgroundColor: "var(--color-surface-hover)",
                borderBottom: "1px solid #cbd5e1",
                color: "var(--color-content-muted)",
                fontWeight: 600,
              }}
            >
              <th style={{ padding: "0.75rem 1rem" }}>{t("returns.colReturnId")}</th>
              <th style={{ padding: "0.75rem 1rem" }}>{t("returns.colSaleId")}</th>
              <th style={{ padding: "0.75rem 1rem" }}>{t("returns.colReason")}</th>
              <th style={{ padding: "0.75rem 1rem" }}>{t("returns.colDisposition")}</th>
              <th style={{ padding: "0.75rem 1rem" }}>{t("returns.colStatus")}</th>
              <th style={{ padding: "0.75rem 1rem", textAlign: "end" }}>{t("common.actions")}</th>
            </tr>
          </thead>
          <tbody>
            {returnsList.length > 0 ? (
              returnsList.map((item: any, idx: number) => (
                <tr key={item.id || idx} style={{ borderBottom: idx === returnsList.length - 1 ? "none" : "1px solid #e2e8f0" }}>
                  <td style={{ padding: "0.75rem 1rem", fontWeight: 700 }}>
                    <BidiText>{(item.id || "").slice(0, 8)}</BidiText>
                  </td>
                  <td style={{ padding: "0.75rem 1rem" }}>
                    <BidiText>{(item.originalSaleId || "").slice(0, 8)}</BidiText>
                  </td>
                  <td style={{ padding: "0.75rem 1rem" }}>
                    {item.reason}
                  </td>
                  <td style={{ padding: "0.75rem 1rem" }}>
                    {item.disposition}
                  </td>
                  <td style={{ padding: "0.75rem 1rem" }}>
                    <span
                      style={{
                        padding: "0.2rem 0.5rem",
                        borderRadius: "var(--radius-full)",
                        fontSize: "0.75rem",
                        fontWeight: 600,
                        backgroundColor: "rgba(59, 130, 246, 0.1)",
                        color: "var(--color-brand-primary)",
                      }}
                    >
                      {item.state || item.status || "—"}
                    </span>
                  </td>
                  <td style={{ padding: "0.75rem 1rem", textAlign: "end" }}>
                    <div style={{ display: "flex", gap: "0.35rem", justifyContent: "flex-end" }}>
                      <Button size="compact" variant="secondary" onClick={() => handleRefund(item.id)}>
                        {t("returns.actionRefund")}
                      </Button>
                      <Button size="compact" variant="secondary" onClick={() => handleCreditNote(item.id)}>
                        {t("returns.actionCreditNote")}
                      </Button>
                      <Button size="compact" variant="primary" onClick={() => handleCompleteReturn(item.id)}>
                        {t("returns.actionComplete")}
                      </Button>
                    </div>
                  </td>
                </tr>
              ))
            ) : (
              <tr>
                <td colSpan={6} style={{ padding: "2rem", textAlign: "center", color: "var(--color-content-muted)" }}>
                  {t("returns.empty")}
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      {activeSession && (
        <ReturnModal
          isOpen={isReturnModalOpen}
          onClose={() => setIsReturnModalOpen(false)}
          sessionId={activeSession.id}
          onCreateReturn={handleCreateReturn}
          isSubmitting={createReturnMutation.isPending}
        />
      )}
    </div>
  );
}

import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "../../../../components/primitives/button.tsx";
import { BidiText } from "../../../../components/primitives/bidi-text.tsx";
import { OpenSessionModal, CloseSessionModal } from "../../components/session-modals.tsx";
import { CashRegisterIcon } from "../../components/pos-icons.tsx";
import {
  useOpenSessionMutation,
  useCloseSessionMutation,
  useApproveVarianceMutation,
} from "../../hooks/use-pos-queries.ts";
import type { RegisterResponse, SessionResponse } from "../../types.ts";

export interface PosSessionsTabProps {
  readonly legalEntityId: string;
  readonly registers: readonly RegisterResponse[];
  readonly currentRegister?: RegisterResponse | undefined;
  readonly activeSession: SessionResponse | null;
  readonly onSessionChanged: () => void;
}

export function PosSessionsTab({
  legalEntityId,
  registers,
  currentRegister,
  activeSession,
  onSessionChanged,
}: PosSessionsTabProps): React.JSX.Element {
  const { t } = useTranslation("pos");
  const [isOpenModalActive, setIsOpenModalActive] = useState(false);
  const [isCloseModalActive, setIsCloseModalActive] = useState(false);
  const [statusMessage, setStatusMessage] = useState<string | null>(null);

  const openSessionMutation = useOpenSessionMutation(legalEntityId);
  const closeSessionMutation = useCloseSessionMutation();
  const approveVarianceMutation = useApproveVarianceMutation();
  const currency = currentRegister?.currencyCode || "";

  const handleOpenSession = async (registerId: string, openingFloat: number) => {
    try {
      const activeCashierId =
        (typeof window !== "undefined" ? window.sessionStorage.getItem("erp.current.userId") : null) ||
        currentRegister?.branchId ||
        registerId;

      await openSessionMutation.mutateAsync({
        registerId,
        cashierUserId: activeCashierId,
        businessDate: new Date().toISOString().slice(0, 10),
        openingFloat,
        currencyCode: currentRegister?.currencyCode || "EGP",
      });
      onSessionChanged();
      setStatusMessage(t("session.openedSuccess"));
    } catch (err: unknown) {
      throw err;
    }
  };

  const handleCloseSession = async (countedCash: number, varianceReason?: string) => {
    if (!activeSession) return;
    try {
      await closeSessionMutation.mutateAsync({
        sessionId: activeSession.id,
        req: {
          sessionId: activeSession.id,
          countedCash,
          varianceReason: varianceReason ?? null,
        },
      });
      onSessionChanged();
      setStatusMessage(t("session.closedSuccess"));
    } catch (err: unknown) {
      throw err;
    }
  };

  const handleApproveVariance = async () => {
    if (!activeSession) return;
    try {
      await approveVarianceMutation.mutateAsync(activeSession.id);
      onSessionChanged();
      setStatusMessage(t("session.varianceApproved"));
    } catch (err: unknown) {
      setStatusMessage(err instanceof Error ? err.message : "Approval failed");
    }
  };

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
      {/* Top Banner / Controls */}
      <div
        style={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          flexWrap: "wrap",
          gap: "1rem",
        }}
      >
        <div>
          <h2 style={{ fontSize: "1.25rem", fontWeight: 700, margin: 0, color: "var(--color-content-default)" }}>
            {t("session.tabTitle")}
          </h2>
          <p style={{ margin: "0.25rem 0 0", fontSize: "0.875rem", color: "var(--color-content-muted)" }}>
            {t("session.tabSubtitle")}
          </p>
        </div>

        <div>
          {!activeSession ? (
            <Button
              variant="primary"
              onClick={() => setIsOpenModalActive(true)}
              style={{ display: "flex", gap: "0.5rem", alignItems: "center" }}
            >
              <CashRegisterIcon size={18} />
              <span>{t("session.openNewSession")}</span>
            </Button>
          ) : (
            <Button
              variant="danger"
              onClick={() => setIsCloseModalActive(true)}
            >
              {t("session.closeActiveSession")}
            </Button>
          )}
        </div>
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

      {/* Active Session Card */}
      {activeSession ? (
        <div
          style={{
            backgroundColor: "#ffffff",
            borderRadius: "var(--radius-lg)",
            border: "1px solid #cbd5e1",
            boxShadow: "0 2px 4px 0 rgba(0, 0, 0, 0.05)",
            padding: "1.5rem",
          }}
        >
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "1rem" }}>
            <div style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
              <span
                style={{
                  display: "inline-block",
                  width: "0.75rem",
                  height: "0.75rem",
                  borderRadius: "50%",
                  backgroundColor: "var(--color-status-success)",
                }}
              />
              <strong style={{ fontSize: "1.125rem", color: "var(--color-content-default)" }}>
                {t("session.activeSession")}
              </strong>
            </div>
            <span
              style={{
                fontSize: "0.75rem",
                fontWeight: 600,
                padding: "0.25rem 0.6rem",
                borderRadius: "var(--radius-full)",
                backgroundColor: "rgba(16, 185, 129, 0.1)",
                color: "var(--color-status-success)",
              }}
            >
              {activeSession.status}
            </span>
          </div>

          <div
            style={{
              display: "grid",
              gridTemplateColumns: "repeat(auto-fit, minmax(10rem, 1fr))",
              gap: "1.25rem",
            }}
          >
            <div>
              <div style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
                {t("session.sessionId")}
              </div>
              <div style={{ fontWeight: 600, color: "var(--color-content-default)" }}>
                <BidiText>{activeSession.id.slice(0, 8)}</BidiText>
              </div>
            </div>

            <div>
              <div style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
                {t("session.businessDate")}
              </div>
              <div style={{ fontWeight: 600, color: "var(--color-content-default)" }}>
                <BidiText>{activeSession.businessDate}</BidiText>
              </div>
            </div>

            <div>
              <div style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
                {t("session.openingFloat")}
              </div>
              <div style={{ fontWeight: 700, color: "var(--color-brand-primary)" }}>
                <BidiText>{activeSession.openingFloat.toFixed(2)} {currency}</BidiText>
              </div>
            </div>

            <div>
              <div style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
                {t("session.countedCash")}
              </div>
              <div style={{ fontWeight: 600, color: "var(--color-content-default)" }}>
                {activeSession.countedCash !== null ? (
                  <BidiText>{activeSession.countedCash.toFixed(2)} {currency}</BidiText>
                ) : (
                  "—"
                )}
              </div>
            </div>

            <div>
              <div style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
                {t("session.variance")}
              </div>
              <div
                style={{
                  fontWeight: 700,
                  color:
                    activeSession.variance === null || activeSession.variance === 0
                      ? "var(--color-content-default)"
                      : "var(--color-status-danger)",
                }}
              >
                {activeSession.variance !== null ? (
                  <BidiText>{activeSession.variance.toFixed(2)} {currency}</BidiText>
                ) : (
                  "—"
                )}
              </div>
            </div>
          </div>

          {activeSession.variance !== null && activeSession.variance !== 0 && (
            <div style={{ marginTop: "1rem", display: "flex", justifyContent: "flex-end" }}>
              <Button size="compact" variant="secondary" onClick={handleApproveVariance}>
                {t("session.approveVariance")}
              </Button>
            </div>
          )}
        </div>
      ) : (
        <div
          style={{
            backgroundColor: "#ffffff",
            borderRadius: "var(--radius-lg)",
            border: "1px dashed #cbd5e1",
            padding: "3rem",
            textAlign: "center",
          }}
        >
          <CashRegisterIcon size={40} style={{ color: "var(--color-content-muted)", margin: "0 auto 0.75rem" }} />
          <h3 style={{ margin: "0 0 0.5rem", color: "var(--color-content-default)" }}>
            {t("session.noActiveSession")}
          </h3>
          <p style={{ margin: "0 0 1.25rem", color: "var(--color-content-muted)", fontSize: "0.875rem" }}>
            {t("session.noActiveSessionPrompt")}
          </p>
          <Button variant="primary" onClick={() => setIsOpenModalActive(true)}>
            {t("session.openNewSession")}
          </Button>
        </div>
      )}

      {/* Modals */}
      <OpenSessionModal
        isOpen={isOpenModalActive}
        onClose={() => setIsOpenModalActive(false)}
        registers={registers}
        selectedRegisterId={currentRegister?.id}
        onOpenSession={handleOpenSession}
        isSubmitting={openSessionMutation.isPending}
      />

      {activeSession && (
        <CloseSessionModal
          isOpen={isCloseModalActive}
          onClose={() => setIsCloseModalActive(false)}
          expectedCash={activeSession.openingFloat}
          currencyCode={currentRegister?.currencyCode}
          onCloseSession={handleCloseSession}
          isSubmitting={closeSessionMutation.isPending}
        />
      )}
    </div>
  );
}

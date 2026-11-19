import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import {
  useReconciliations,
  useOpeningReconciliations,
  useFinalizeReconciliation,
  useReopenReconciliation,
  useFinalizeOpeningReconciliation
} from "../../hooks/use-banking-queries.ts";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "../../../../components/primitives/table.tsx";
import { BankingMoney } from "../../components/banking-money.tsx";
import { StatusBadge } from "../../../../components/fields/status-badge.tsx";
import { BdiText } from "../../../../components/formatting/bidi-text.tsx";
import { formatDate } from "../../../../components/formatting/date-formatter.ts";
import { CreateReconciliationModal } from "./create-reconciliation-modal.tsx";
import { ReconciliationWorkbenchModal } from "./reconciliation-workbench-modal.tsx";
import { OpeningReconciliationModal } from "./opening-reconciliation-modal.tsx";
import type { ReconciliationDto } from "../../types.ts";

interface Props {
  readonly legalEntityId: string;
}

export function ReconciliationTab({ legalEntityId }: Props): React.JSX.Element {
  const { t } = useTranslation("banking");
  const [activeSubTab, setActiveSubTab] = useState<"sessions" | "opening">("sessions");

  const [isCreateOpen, setIsCreateOpen] = useState(false);
  const [isOpeningOpen, setIsOpeningOpen] = useState(false);
  const [selectedSession, setSelectedSession] = useState<ReconciliationDto | null>(null);

  const { data: sessionsData, isLoading: isSessionsLoading, refetch: refetchSessions } = useReconciliations(legalEntityId);
  const { data: openingData, isLoading: isOpeningLoading, refetch: refetchOpening } = useOpeningReconciliations(legalEntityId);

  const finalizeMutation = useFinalizeReconciliation(legalEntityId, selectedSession?.id ?? "");
  const reopenMutation = useReopenReconciliation(legalEntityId, selectedSession?.id ?? "");
  const finalizeOpeningMutation = useFinalizeOpeningReconciliation(legalEntityId);

  const handleFinalize = async (session: ReconciliationDto) => {
    try {
      await finalizeMutation.mutateAsync(session.version);
      void refetchSessions();
    } catch {
      // ignore
    }
  };

  const handleReopen = async (session: ReconciliationDto) => {
    try {
      await reopenMutation.mutateAsync(session.version);
      void refetchSessions();
    } catch {
      // ignore
    }
  };

  const handleFinalizeOpening = async (id: string, version: number) => {
    try {
      await finalizeOpeningMutation.mutateAsync({ id, version });
      void refetchOpening();
    } catch {
      // ignore
    }
  };

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
      {/* Header bar */}
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: "1rem" }}>
        <div>
          <h2 style={{ margin: 0, fontSize: "1.25rem", color: "var(--color-brand-primary)" }}>{t("reconciliation.title")}</h2>
          <p style={{ margin: "0.25rem 0 0", fontSize: "0.875rem", color: "var(--color-content-muted)" }}>{t("reconciliation.subtitle")}</p>
        </div>
        <div style={{ display: "flex", gap: "0.5rem" }}>
          {activeSubTab === "sessions" ? (
            <Button variant="primary" onClick={() => setIsCreateOpen(true)}>
              {t("reconciliation.newSessionButton")}
            </Button>
          ) : (
            <Button variant="primary" onClick={() => setIsOpeningOpen(true)}>
              {t("reconciliation.newOpeningButton")}
            </Button>
          )}
        </div>
      </div>

      {/* Sub-tab navigation */}
      <div style={{ display: "flex", gap: "0.5rem" }}>
        <Button
          variant={activeSubTab === "sessions" ? "primary" : "ghost"}
          size="compact"
          onClick={() => setActiveSubTab("sessions")}
        >
          {t("reconciliation.title")}
        </Button>
        <Button
          variant={activeSubTab === "opening" ? "primary" : "ghost"}
          size="compact"
          onClick={() => setActiveSubTab("opening")}
        >
          {t("reconciliation.openingTab")}
        </Button>
      </div>

      {/* Main Surface */}
      <Surface style={{ padding: "1.25rem" }}>
        {activeSubTab === "sessions" ? (
          isSessionsLoading ? (
            <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted)" }}>
              {t("common.loading")}
            </div>
          ) : (sessionsData?.items ?? []).length === 0 ? (
            <div style={{ padding: "3rem", textAlign: "center" }}>
              <h3 style={{ margin: "0 0 0.5rem", color: "var(--color-brand-primary)" }}>{t("reconciliation.emptyTitle")}</h3>
              <p style={{ color: "var(--color-content-muted)", margin: "0 0 1.5rem" }}>{t("reconciliation.emptyDescription")}</p>
              <Button variant="primary" onClick={() => setIsCreateOpen(true)}>
                {t("reconciliation.newSessionButton")}
              </Button>
            </div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>{t("reconciliation.asOfDate")}</TableHead>
                  <TableHead align="end">{t("reconciliation.externalBalance")}</TableHead>
                  <TableHead align="end">{t("reconciliation.bookBalance")}</TableHead>
                  <TableHead align="end">{t("reconciliation.difference")}</TableHead>
                  <TableHead>{t("reconciliation.matchesCount")}</TableHead>
                  <TableHead>{t("reconciliation.state")}</TableHead>
                  <TableHead align="end">{t("accounts.actions")}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {sessionsData?.items.map((session) => (
                  <TableRow key={session.id}>
                    <TableCell style={{ fontWeight: 600 }}>
                      <BdiText>{formatDate(session.asOfDate)}</BdiText>
                    </TableCell>
                    <TableCell align="end">
                      <BankingMoney amount={session.externalBalance} currency="EGP" />
                    </TableCell>
                    <TableCell align="end">
                      <BankingMoney amount={session.bookBalance} currency="EGP" />
                    </TableCell>
                    <TableCell align="end">
                      <span style={{ fontWeight: 600, color: session.difference === 0 ? "#16a34a" : "var(--color-status-danger)" }}>
                        <BankingMoney amount={session.difference} currency="EGP" />
                      </span>
                    </TableCell>
                    <TableCell>
                      <BdiText>{session.matchCount}</BdiText>
                    </TableCell>
                    <TableCell>
                      <StatusBadge
                        intent={session.state === "Finalized" ? "success" : session.state === "Open" ? "neutral" : "warning"}
                        label={session.state}
                      />
                    </TableCell>
                    <TableCell align="end">
                      <div style={{ display: "flex", gap: "0.375rem", justifyContent: "flex-end" }}>
                        <Button
                          size="compact"
                          variant="secondary"
                          data-testid={`open-workbench-${session.id}`}
                          onClick={() => setSelectedSession(session)}
                        >
                          {t("reconciliation.openWorkbench")}
                        </Button>
                        {session.state === "Open" && (
                          <Button
                            size="compact"
                            variant="primary"
                            onClick={() => handleFinalize(session)}
                            isDisabled={session.difference !== 0}
                          >
                            {t("reconciliation.finalizeAction")}
                          </Button>
                        )}
                        {session.state === "Finalized" && (
                          <Button
                            size="compact"
                            variant="ghost"
                            onClick={() => handleReopen(session)}
                          >
                            {t("reconciliation.reopenAction")}
                          </Button>
                        )}
                      </div>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )
        ) : (
          isOpeningLoading ? (
            <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted)" }}>
              {t("common.loading")}
            </div>
          ) : (openingData?.items ?? []).length === 0 ? (
            <div style={{ padding: "3rem", textAlign: "center" }}>
              <p style={{ color: "var(--color-content-muted)" }}>{t("common.noData")}</p>
            </div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>{t("reconciliation.asOfDate")}</TableHead>
                  <TableHead>{t("reconciliation.ledgerBatch")}</TableHead>
                  <TableHead align="end">{t("reconciliation.bookBalance")}</TableHead>
                  <TableHead align="end">{t("reconciliation.externalBalance")}</TableHead>
                  <TableHead align="end">{t("reconciliation.openingDifference")}</TableHead>
                  <TableHead>{t("accounts.status")}</TableHead>
                  <TableHead align="end">{t("accounts.actions")}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {openingData?.items.map((item) => (
                  <TableRow key={item.id}>
                    <TableCell>
                      <BdiText>{formatDate(item.openingAsOfDate)}</BdiText>
                    </TableCell>
                    <TableCell>
                      <BdiText>{item.ledgerOpeningBatchId}</BdiText>
                    </TableCell>
                    <TableCell align="end">
                      <BankingMoney amount={item.bookOpeningBalance} currency={item.currencyCode} />
                    </TableCell>
                    <TableCell align="end">
                      <BankingMoney amount={item.externalOpeningBalance} currency={item.currencyCode} />
                    </TableCell>
                    <TableCell align="end">
                      <BankingMoney amount={item.difference} currency={item.currencyCode} />
                    </TableCell>
                    <TableCell>
                      <StatusBadge
                        intent={item.status === "Finalized" ? "success" : "neutral"}
                        label={item.status}
                      />
                    </TableCell>
                    <TableCell align="end">
                      {item.status !== "Finalized" && (
                        <Button
                          size="compact"
                          variant="primary"
                          onClick={() => handleFinalizeOpening(item.id, item.version)}
                          isDisabled={finalizeOpeningMutation.isPending}
                        >
                          {t("reconciliation.finalizeOpening")}
                        </Button>
                      )}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )
        )}
      </Surface>

      {/* Modals */}
      <CreateReconciliationModal
        legalEntityId={legalEntityId}
        isOpen={isCreateOpen}
        onClose={() => setIsCreateOpen(false)}
      />

      <ReconciliationWorkbenchModal
        legalEntityId={legalEntityId}
        reconciliation={selectedSession}
        isOpen={Boolean(selectedSession)}
        onClose={() => setSelectedSession(null)}
      />

      <OpeningReconciliationModal
        legalEntityId={legalEntityId}
        isOpen={isOpeningOpen}
        onClose={() => setIsOpeningOpen(false)}
      />
    </div>
  );
}

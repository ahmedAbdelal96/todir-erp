import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { useOutletContext } from "react-router-dom";
import type { WorkspaceOutletContext } from "../../../../app/router/route-contract.ts";
import { canPerformAction } from "../../../../core/runtime/effective-access.ts";
import {
  useTransferReconciliations,
  useProviderSettlements,
  useChequesInTransit
} from "../../hooks/use-banking-queries.ts";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "../../../../components/primitives/table.tsx";
import { BankingMoney } from "../../components/banking-money.tsx";
import { StatusBadge } from "../../../../components/fields/status-badge.tsx";
import { BdiText } from "../../../../components/formatting/bidi-text.tsx";
import { formatDate } from "../../../../components/formatting/date-formatter.ts";
import { NewTransferModal } from "./new-transfer-modal.tsx";
import { ReconcileTransferModal } from "./reconcile-transfer-modal.tsx";
import { MatchProviderModal } from "./match-provider-modal.tsx";
import type { TransferReconciliationDto, ProviderSettlementDto } from "../../types.ts";

interface Props {
  readonly legalEntityId: string;
}

export function TransfersTab({ legalEntityId }: Props): React.JSX.Element {
  const { t } = useTranslation("banking");
  const outletCtx = useOutletContext<WorkspaceOutletContext | undefined>();
  const effectiveAccess = outletCtx?.effectiveAccess;
  const canInitiate = effectiveAccess ? canPerformAction(effectiveAccess, "banking.transfers.initiate", "banking") : true;
  const canReconcile = effectiveAccess ? canPerformAction(effectiveAccess, "banking.reconciliation.match", "banking") : true;

  const [activeSubTab, setActiveSubTab] = useState<"transfers" | "providers" | "cheques">("transfers");

  // Modals state
  const [isNewTransferOpen, setIsNewTransferOpen] = useState(false);
  const [selectedTransferForReconcile, setSelectedTransferForReconcile] = useState<TransferReconciliationDto | null>(null);
  const [selectedSettlementForMatch, setSelectedSettlementForMatch] = useState<ProviderSettlementDto | null>(null);

  // Queries
  const { data: transfersData, isLoading: isTransfersLoading } = useTransferReconciliations(legalEntityId);
  const { data: providerData, isLoading: isProviderLoading, refetch: refetchProvider } = useProviderSettlements(legalEntityId);
  const { data: chequesData, isLoading: isChequesLoading } = useChequesInTransit(legalEntityId);

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
      {/* Header bar */}
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: "1rem" }}>
        <div>
          <h2 style={{ margin: 0, fontSize: "1.25rem", color: "var(--color-brand-primary)" }}>{t("transfers.title")}</h2>
          <p style={{ margin: "0.25rem 0 0", fontSize: "0.875rem", color: "var(--color-content-muted)" }}>{t("transfers.subtitle")}</p>
        </div>
        {activeSubTab === "transfers" && canInitiate && (
          <Button variant="primary" data-testid="new-internal-transfer" onClick={() => setIsNewTransferOpen(true)}>
            {t("transfers.newTransferButton")}
          </Button>
        )}
      </div>

      {/* Sub-tab navigation */}
      <div style={{ display: "flex", gap: "0.5rem" }}>
        <Button
          variant={activeSubTab === "transfers" ? "primary" : "ghost"}
          size="compact"
          onClick={() => setActiveSubTab("transfers")}
        >
          {t("transfers.title")}
        </Button>
        <Button
          variant={activeSubTab === "providers" ? "primary" : "ghost"}
          size="compact"
          onClick={() => setActiveSubTab("providers")}
        >
          {t("transfers.providerSettlementsTab")}
        </Button>
        <Button
          variant={activeSubTab === "cheques" ? "primary" : "ghost"}
          size="compact"
          onClick={() => setActiveSubTab("cheques")}
        >
          {t("transfers.chequesTab")}
        </Button>
      </div>

      {/* Main Table Surface */}
      <Surface style={{ padding: "1.25rem" }}>
        {activeSubTab === "transfers" ? (
          isTransfersLoading ? (
            <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted)" }}>
              {t("common.loading")}
            </div>
          ) : (transfersData?.items ?? []).length === 0 ? (
            <div style={{ padding: "3rem", textAlign: "center" }}>
              <h3 style={{ margin: "0 0 0.5rem", color: "var(--color-brand-primary)" }}>{t("transfers.emptyTransfersTitle")}</h3>
              <p style={{ color: "var(--color-content-muted)", margin: "0 0 1.5rem" }}>{t("transfers.emptyTransfersDesc")}</p>
              <Button variant="primary" onClick={() => setIsNewTransferOpen(true)}>
                {t("transfers.newTransferButton")}
              </Button>
            </div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>{t("transfers.transferRef")}</TableHead>
                  <TableHead>{t("transfers.sourceAccount")}</TableHead>
                  <TableHead>{t("transfers.destinationAccount")}</TableHead>
                  <TableHead align="end">{t("transfers.sourceAmount")}</TableHead>
                  <TableHead align="end">{t("transfers.destinationAmount")}</TableHead>
                  <TableHead align="end">{t("transfers.inTransitAmount")}</TableHead>
                  <TableHead>{t("transfers.transferState")}</TableHead>
                  <TableHead align="end">{t("accounts.actions")}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {transfersData?.items.map((item) => (
                  <TableRow key={item.id} data-testid={`transfer-row-${item.transferReference}`}>
                    <TableCell style={{ fontWeight: 600 }}>
                      <BdiText>{item.transferReference}</BdiText>
                    </TableCell>
                    <TableCell>
                      <BdiText>{item.sourceAccountId.slice(0, 8)}…</BdiText>
                    </TableCell>
                    <TableCell>
                      <BdiText>{item.destinationAccountId.slice(0, 8)}…</BdiText>
                    </TableCell>
                    <TableCell align="end">
                      <BankingMoney amount={item.sourceAmount} currency={item.sourceCurrencyCode || "EGP"} />
                    </TableCell>
                    <TableCell align="end">
                      <BankingMoney amount={item.destinationAmount} currency={item.destinationCurrencyCode || "EGP"} />
                    </TableCell>
                    <TableCell align="end">
                      <BankingMoney amount={item.inTransitAmount} currency={item.sourceCurrencyCode || "EGP"} />
                    </TableCell>
                    <TableCell>
                      <StatusBadge
                        intent={item.state === "Matched" ? "success" : "warning"}
                        label={<BdiText>{item.state}</BdiText>}
                      />
                    </TableCell>
                    <TableCell align="end">
                      {item.state !== "Matched" && canReconcile && (
                        <Button
                          size="compact"
                          variant="secondary"
                          onClick={() => setSelectedTransferForReconcile(item)}
                        >
                          {t("transfers.reconcileTransferAction")}
                        </Button>
                      )}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )
        ) : activeSubTab === "providers" ? (
          isProviderLoading ? (
            <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted)" }}>
              {t("common.loading")}
            </div>
          ) : (providerData?.items ?? []).length === 0 ? (
            <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted)" }}>
              {t("common.noData")}
            </div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>{t("transfers.providerCode")}</TableHead>
                  <TableHead>{t("transfers.providerRef")}</TableHead>
                  <TableHead>{t("statements.openingDate")}</TableHead>
                  <TableHead>{t("statements.closingDate")}</TableHead>
                  <TableHead align="end">{t("transfers.grossAmount")}</TableHead>
                  <TableHead align="end">{t("transfers.feesAmount")}</TableHead>
                  <TableHead align="end">{t("transfers.netPayout")}</TableHead>
                  <TableHead>{t("accounts.status")}</TableHead>
                  <TableHead align="end">{t("accounts.actions")}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {providerData?.items.map((settlement) => (
                  <TableRow key={settlement.id}>
                    <TableCell style={{ fontWeight: 600 }}>{settlement.providerCode}</TableCell>
                    <TableCell><BdiText>{settlement.providerReference}</BdiText></TableCell>
                    <TableCell><BdiText>{formatDate(settlement.periodFrom)}</BdiText></TableCell>
                    <TableCell><BdiText>{formatDate(settlement.periodTo)}</BdiText></TableCell>
                    <TableCell align="end"><BankingMoney amount={settlement.grossAmount} currency={settlement.currencyCode} /></TableCell>
                    <TableCell align="end"><BankingMoney amount={settlement.feesAmount} currency={settlement.currencyCode} /></TableCell>
                    <TableCell align="end" style={{ fontWeight: 600 }}><BankingMoney amount={settlement.netPayoutAmount} currency={settlement.currencyCode} /></TableCell>
                    <TableCell>
                      <StatusBadge
                        intent={settlement.status === "Matched" ? "success" : "warning"}
                        label={<BdiText>{settlement.status}</BdiText>}
                      />
                    </TableCell>
                    <TableCell align="end">
                      {settlement.status !== "Matched" && canReconcile && (
                        <Button
                          size="compact"
                          variant="secondary"
                          onClick={() => setSelectedSettlementForMatch(settlement)}
                        >
                          {t("transfers.matchWithStatement")}
                        </Button>
                      )}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )
        ) : (
          isChequesLoading ? (
            <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted)" }}>
              {t("common.loading")}
            </div>
          ) : (chequesData?.items ?? []).length === 0 ? (
            <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted)" }}>
              {t("common.noData")}
            </div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>{t("transfers.paymentNumber")}</TableHead>
                  <TableHead>{t("transfers.direction")}</TableHead>
                  <TableHead align="end">{t("statements.amount")}</TableHead>
                  <TableHead>{t("transfers.paymentDate")}</TableHead>
                  <TableHead>{t("accounts.status")}</TableHead>
                  <TableHead>{t("transfers.bankRef")}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {chequesData?.items.map((chq) => (
                  <TableRow key={chq.paymentId}>
                    <TableCell style={{ fontWeight: 600 }}><BdiText>{chq.paymentNumber}</BdiText></TableCell>
                    <TableCell>
                      <StatusBadge
                        intent={chq.direction === "Incoming" ? "success" : "neutral"}
                        label={<BdiText>{chq.direction}</BdiText>}
                      />
                    </TableCell>
                    <TableCell align="end"><BankingMoney amount={chq.amount} currency={chq.currencyCode} /></TableCell>
                    <TableCell><BdiText>{formatDate(chq.paymentDate)}</BdiText></TableCell>
                    <TableCell>
                      <StatusBadge intent="warning" label={<BdiText>{chq.state}</BdiText>} />
                    </TableCell>
                    <TableCell><BdiText>{chq.bankReference || "—"}</BdiText></TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )
        )}
      </Surface>

      {/* Modals */}
      <NewTransferModal
        legalEntityId={legalEntityId}
        isOpen={isNewTransferOpen}
        onClose={() => setIsNewTransferOpen(false)}
      />

      <ReconcileTransferModal
        legalEntityId={legalEntityId}
        transfer={selectedTransferForReconcile}
        isOpen={Boolean(selectedTransferForReconcile)}
        onClose={() => setSelectedTransferForReconcile(null)}
      />

      <MatchProviderModal
        legalEntityId={legalEntityId}
        settlement={selectedSettlementForMatch}
        isOpen={Boolean(selectedSettlementForMatch)}
        onClose={() => setSelectedSettlementForMatch(null)}
        onMatched={() => {
          void refetchProvider();
          setSelectedSettlementForMatch(null);
        }}
      />
    </div>
  );
}

import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import {
  useTreasuryAccounts,
  useCashPosition,
  useCashCounts,
  useSubmitCashCount,
  useApproveCashCount,
  useFinalizeCashCount,
  usePosCashSettlements,
  useConfirmPosCashSettlement
} from "../../hooks/use-banking-queries.ts";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { Select } from "../../../../components/primitives/select.tsx";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "../../../../components/primitives/table.tsx";
import { BankingMoney } from "../../components/banking-money.tsx";
import { StatusBadge } from "../../../../components/fields/status-badge.tsx";
import { BdiText } from "../../../../components/formatting/bidi-text.tsx";
import { formatDate } from "../../../../components/formatting/date-formatter.ts";
import { CashCountModal } from "./cash-count-modal.tsx";
import { ResolveDifferenceModal } from "./resolve-difference-modal.tsx";
import { CustodyTransferModal } from "./custody-transfer-modal.tsx";
import type { CashCountDto } from "../../types.ts";

interface Props {
  readonly legalEntityId: string;
}

export function CashManagementTab({ legalEntityId }: Props): React.JSX.Element {
  const { t } = useTranslation("banking");
  const [activeSubTab, setActiveSubTab] = useState<"counts" | "pos">("counts");

  // Fetch only Cash and PettyCash accounts
  const { data: accountsData } = useTreasuryAccounts(legalEntityId);
  const cashAccounts = (accountsData?.items ?? []).filter(
    (a) => a.type === "Cash" || a.type === "PettyCash"
  );

  const [selectedAccountId, setSelectedAccountId] = useState<string>("");
  const activeAccountId = selectedAccountId || (cashAccounts.length > 0 ? (cashAccounts[0]?.id ?? "") : "");
  const selectedAccount = cashAccounts.find((a) => a.id === activeAccountId);

  // Modals state
  const [isCountOpen, setIsCountOpen] = useState(false);
  const [isCustodyOpen, setIsCustodyOpen] = useState(false);
  const [selectedCountForResolve, setSelectedCountForResolve] = useState<CashCountDto | null>(null);

  // Cash counts query & mutations
  const { data: positions } = useCashPosition(legalEntityId);
  const activePosition = positions?.find((p) => p.treasuryAccountId === activeAccountId);
  const expectedBalance = activePosition?.bookBalance ?? null;

  const { data: countsData, isLoading: isCountsLoading, refetch: refetchCounts } = useCashCounts(legalEntityId, activeAccountId);
  const submitMutation = useSubmitCashCount(legalEntityId, activeAccountId);
  const approveMutation = useApproveCashCount(legalEntityId, activeAccountId);
  const finalizeMutation = useFinalizeCashCount(legalEntityId, activeAccountId);

  // POS settlements query & mutation
  const { data: posData, isLoading: isPosLoading, refetch: refetchPos } = usePosCashSettlements(legalEntityId);
  const confirmPosMutation = useConfirmPosCashSettlement(legalEntityId);

  const handleSubmitCount = async (count: CashCountDto) => {
    try {
      await submitMutation.mutateAsync({ cashCountId: count.id, version: count.version });
      void refetchCounts();
    } catch {
      // ignore
    }
  };

  const handleApproveCount = async (count: CashCountDto) => {
    try {
      await approveMutation.mutateAsync({ cashCountId: count.id, version: count.version });
      void refetchCounts();
    } catch {
      // ignore
    }
  };

  const handleFinalizeCount = async (count: CashCountDto) => {
    try {
      await finalizeMutation.mutateAsync({ cashCountId: count.id, version: count.version });
      void refetchCounts();
    } catch {
      // ignore
    }
  };

  const handleConfirmPosSettlement = async (settlementId: string, version: number) => {
    if (!activeAccountId) return;
    try {
      await confirmPosMutation.mutateAsync({
        settlementId,
        request: { treasuryAccountId: activeAccountId, version }
      });
      void refetchPos();
    } catch {
      // ignore
    }
  };

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
      {/* Header bar */}
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: "1rem" }}>
        <div>
          <h2 style={{ margin: 0, fontSize: "1.25rem", color: "var(--color-brand-primary)" }}>{t("cash.title")}</h2>
          <p style={{ margin: "0.25rem 0 0", fontSize: "0.875rem", color: "var(--color-content-muted)" }}>{t("cash.subtitle")}</p>
        </div>
        <div style={{ display: "flex", gap: "0.5rem" }}>
          {activeSubTab === "counts" && (
            <>
              <Button variant="secondary" onClick={() => setIsCustodyOpen(true)} isDisabled={!activeAccountId}>
                {t("cash.transferCustodyButton")}
              </Button>
              <Button variant="primary" onClick={() => setIsCountOpen(true)} isDisabled={!activeAccountId}>
                {t("cash.newCountButton")}
              </Button>
            </>
          )}
        </div>
      </div>

      {/* Sub-tab navigation */}
      <div style={{ display: "flex", gap: "0.5rem" }}>
        <Button
          variant={activeSubTab === "counts" ? "primary" : "ghost"}
          size="compact"
          onClick={() => setActiveSubTab("counts")}
        >
          {t("cash.title")}
        </Button>
        <Button
          variant={activeSubTab === "pos" ? "primary" : "ghost"}
          size="compact"
          onClick={() => setActiveSubTab("pos")}
        >
          {t("cash.posSettlementsTab")}
        </Button>
      </div>

      {/* Account Selector Surface (shown in counts tab) */}
      {activeSubTab === "counts" && (
        <Surface style={{ padding: "1rem" }}>
          <div style={{ maxWidth: "360px" }}>
            <Select
              label={t("cash.selectCashAccount")}
              selectedKey={activeAccountId}
              onSelectionChange={(key) => setSelectedAccountId(key ? String(key) : "")}
              options={cashAccounts.map((acc) => ({
                id: acc.id,
                label: `${acc.name} (${acc.code}) - ${acc.currencyCode || "EGP"}`
              }))}
            />
          </div>
        </Surface>
      )}

      {/* Main Table Surface */}
      <Surface style={{ padding: "1.25rem" }}>
        {activeSubTab === "counts" ? (
          isCountsLoading ? (
            <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted)" }}>
              {t("common.loading")}
            </div>
          ) : (countsData?.items ?? []).length === 0 ? (
            <div style={{ padding: "3rem", textAlign: "center" }}>
              <h3 style={{ margin: "0 0 0.5rem", color: "var(--color-brand-primary)" }}>{t("cash.emptyTitle")}</h3>
              <p style={{ color: "var(--color-content-muted)", margin: "0 0 1.5rem" }}>{t("cash.emptyDescription")}</p>
              <Button variant="primary" onClick={() => setIsCountOpen(true)} isDisabled={!activeAccountId}>
                {t("cash.newCountButton")}
              </Button>
            </div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>{t("cash.cutoffDate")}</TableHead>
                  <TableHead align="end">{t("cash.expectedAmount")}</TableHead>
                  <TableHead align="end">{t("cash.countedAmount")}</TableHead>
                  <TableHead align="end">{t("cash.variance")}</TableHead>
                  <TableHead>{t("cash.custodian")}</TableHead>
                  <TableHead>{t("cash.status")}</TableHead>
                  <TableHead align="end">{t("accounts.actions")}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {countsData?.items.map((count) => {
                  const currency = selectedAccount?.currencyCode;
                  return (
                    <TableRow key={count.id} data-testid={`cash-count-row-${count.cutoffDate}`}>
                      <TableCell style={{ fontWeight: 600 }}>
                        <BdiText>{formatDate(count.cutoffDate)}</BdiText>
                      </TableCell>
                      <TableCell align="end">
                        <BankingMoney amount={count.expectedAmount} currency={currency} />
                      </TableCell>
                      <TableCell align="end">
                        <BankingMoney amount={count.countedAmount} currency={currency} />
                      </TableCell>
                      <TableCell align="end">
                        <span style={{ fontWeight: 600, color: count.difference === 0 ? "#16a34a" : count.difference < 0 ? "var(--color-status-danger)" : "#2563eb" }}>
                          <BankingMoney amount={count.difference} currency={currency} />
                        </span>
                      </TableCell>
                      <TableCell>
                        <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
                          <BdiText>{count.custodianId.slice(0, 8)}…</BdiText>
                        </span>
                      </TableCell>
                      <TableCell>
                        <StatusBadge
                          intent={count.state === "Finalized" ? "success" : count.state === "Approved" ? "info" : "neutral"}
                          label={<BdiText>{count.state}</BdiText>}
                        />
                      </TableCell>
                      <TableCell align="end">
                        <div style={{ display: "flex", gap: "0.375rem", justifyContent: "flex-end" }}>
                          {count.state === "Draft" && (
                            <Button
                              size="compact"
                              variant="secondary"
                              onClick={() => handleSubmitCount(count)}
                              isDisabled={submitMutation.isPending}
                            >
                              {t("cash.submitCount")}
                            </Button>
                          )}
                          {count.state === "Submitted" && (
                            <Button
                              size="compact"
                              variant="primary"
                              onClick={() => handleApproveCount(count)}
                              isDisabled={approveMutation.isPending}
                            >
                              {t("cash.approveCount")}
                            </Button>
                          )}
                          {count.state === "Approved" && (
                            <Button
                              size="compact"
                              variant="primary"
                              onClick={() => handleFinalizeCount(count)}
                              isDisabled={finalizeMutation.isPending}
                            >
                              {t("cash.finalizeCount")}
                            </Button>
                          )}
                          {count.difference !== 0 && count.state !== "Finalized" && (
                            <Button
                              size="compact"
                              variant="ghost"
                              onClick={() => setSelectedCountForResolve(count)}
                            >
                              {t("cash.resolveDiff")}
                            </Button>
                          )}
                        </div>
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          )
        ) : (
          isPosLoading ? (
            <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted)" }}>
              {t("common.loading")}
            </div>
          ) : (posData?.items ?? []).length === 0 ? (
            <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted)" }}>
              {t("common.noData")}
            </div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>{t("cash.posSessionId")}</TableHead>
                  <TableHead>{t("cash.registerId")}</TableHead>
                  <TableHead>{t("cash.businessDate")}</TableHead>
                  <TableHead align="end">{t("cash.expectedCash")}</TableHead>
                  <TableHead align="end">{t("cash.countedCash")}</TableHead>
                  <TableHead align="end">{t("cash.posVariance")}</TableHead>
                  <TableHead>{t("accounts.status")}</TableHead>
                  <TableHead align="end">{t("accounts.actions")}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {posData?.items.map((pos) => (
                  <TableRow key={pos.id}>
                    <TableCell style={{ fontWeight: 600 }}>
                      <BdiText>{pos.posSessionId.slice(0, 8)}…</BdiText>
                    </TableCell>
                    <TableCell>
                      <BdiText>{pos.registerId.slice(0, 8)}…</BdiText>
                    </TableCell>
                    <TableCell>
                      <BdiText>{formatDate(pos.businessDate)}</BdiText>
                    </TableCell>
                    <TableCell align="end">
                      <BankingMoney amount={pos.expectedCash} currency={pos.currencyCode} />
                    </TableCell>
                    <TableCell align="end">
                      <BankingMoney amount={pos.countedCash} currency={pos.currencyCode} />
                    </TableCell>
                    <TableCell align="end">
                      <BankingMoney amount={pos.variance} currency={pos.currencyCode} />
                    </TableCell>
                    <TableCell>
                      <StatusBadge
                        intent={pos.status === "Confirmed" ? "success" : "warning"}
                        label={<BdiText>{pos.status}</BdiText>}
                      />
                    </TableCell>
                    <TableCell align="end">
                      {pos.status !== "Confirmed" && (
                        <Button
                          size="compact"
                          variant="primary"
                          onClick={() => handleConfirmPosSettlement(pos.id, pos.version)}
                          isDisabled={confirmPosMutation.isPending || !activeAccountId}
                        >
                          {t("cash.confirmPosSettlement")}
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
      <CashCountModal
        legalEntityId={legalEntityId}
        accountId={activeAccountId}
        currencyCode={selectedAccount?.currencyCode || "EGP"}
        expectedBalance={expectedBalance}
        isOpen={isCountOpen}
        onClose={() => setIsCountOpen(false)}
      />

      <ResolveDifferenceModal
        legalEntityId={legalEntityId}
        accountId={activeAccountId}
        cashCount={selectedCountForResolve}
        isOpen={Boolean(selectedCountForResolve)}
        onClose={() => setSelectedCountForResolve(null)}
      />

      <CustodyTransferModal
        legalEntityId={legalEntityId}
        accountId={activeAccountId}
        isOpen={isCustodyOpen}
        onClose={() => setIsCustodyOpen(false)}
      />
    </div>
  );
}

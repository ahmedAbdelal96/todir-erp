import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { useOutletContext } from "react-router-dom";
import type { WorkspaceOutletContext } from "../../../app/router/route-contract.ts";
import { AccountingHeader } from "../features/accounting-header.tsx";
import { AccountingReadiness } from "../features/accounting-readiness.tsx";
import { ChartOfAccountsView } from "../features/chart-of-accounts/chart-of-accounts-view.tsx";
import { JournalsView } from "../features/journals/journals-view.tsx";
import { PeriodsView } from "../features/periods/periods-view.tsx";
import { FinancialLocksView } from "../features/periods/financial-locks-view.tsx";
import { GeneralJournalView } from "../features/general-journal/general-journal-view.tsx";
import { EntriesExplorerView } from "../features/entries/entries-explorer-view.tsx";
import { OpeningBalancesView } from "../features/opening-balances/opening-balances-view.tsx";
import { TrialBalanceView } from "../features/reports/trial-balance-view.tsx";
import { GeneralLedgerView } from "../features/reports/general-ledger-view.tsx";
import { ReconciliationView } from "../features/reports/reconciliation-view.tsx";
import { DimensionsView } from "../features/dimensions/dimensions-view.tsx";
import { AccountingTabNav } from "../components/accounting-tab-nav.tsx";
import {
  useLedgerScope,
  useLedgerPermissions,
  useLedgerReadiness,
  useLedgerMode,
  useAccounts,
  useJournals,
  useFiscalYears,
  usePeriods,
  useLocks,
  useJournalEntries
} from "../hooks/use-ledger-queries.ts";

export function AccountingPage(): React.JSX.Element {
  const { t } = useTranslation("accounting");
  const outletCtx = useOutletContext<WorkspaceOutletContext | undefined>();
  const { legalEntityId, baseCurrency, today } = useLedgerScope({ legalEntityId: outletCtx?.legalEntityId });
  const permissions = useLedgerPermissions();

  const [activeTab, setActiveTab] = useState<string>("overview");
  const [selectedGlAccountCode, setSelectedGlAccountCode] = useState<string | undefined>(undefined);

  // Queries
  const { data: readiness, isLoading: isReadinessLoading } = useLedgerReadiness(legalEntityId);
  const { data: modeData } = useLedgerMode(legalEntityId);
  const { data: accounts = [], isLoading: isAccountsLoading } = useAccounts(legalEntityId);
  const { data: journals = [], isLoading: isJournalsLoading } = useJournals(legalEntityId);
  const { data: fiscalYears = [] } = useFiscalYears(legalEntityId);
  const { data: periods = [], isLoading: isPeriodsLoading } = usePeriods(legalEntityId);
  const { data: locks = [], isLoading: isLocksLoading } = useLocks(legalEntityId);
  const { data: entries = [], isLoading: isEntriesLoading } = useJournalEntries(legalEntityId);

  const currentMode = modeData?.mode ?? "Managed";
  const modeVersion = modeData?.version ?? 1;

  // Tabs based on mode
  const tabs = currentMode === "Managed"
    ? [
        { id: "overview", label: t("tabs.overview") },
        { id: "entries", label: t("tabs.entries") },
        { id: "generalJournal", label: t("tabs.generalJournal") },
        { id: "trialBalance", label: t("tabs.trialBalance") },
        { id: "generalLedger", label: t("tabs.generalLedger") }
      ]
    : [
        { id: "overview", label: t("tabs.overview") },
        { id: "chartOfAccounts", label: t("tabs.chartOfAccounts") },
        { id: "journals", label: t("tabs.journals") },
        { id: "periods", label: t("tabs.periods") },
        { id: "locks", label: t("tabs.locks") },
        { id: "entries", label: t("tabs.entries") },
        { id: "generalJournal", label: t("tabs.generalJournal") },
        { id: "openingBalances", label: t("tabs.openingBalances") },
        { id: "dimensions", label: t("tabs.dimensions") },
        { id: "trialBalance", label: t("tabs.trialBalance") },
        { id: "generalLedger", label: t("tabs.generalLedger") },
        { id: "reconciliation", label: t("tabs.reconciliation") }
      ];

  const handleOpenGeneralLedger = (accountCode: string) => {
    setSelectedGlAccountCode(accountCode);
    setActiveTab("generalLedger");
  };

  return (
    <div style={{ padding: "1.25rem 1.5rem", width: "100%", maxWidth: "1600px", margin: "0 auto", boxSizing: "border-box", minWidth: 0 }}>
      {/* Accounting Header */}
      <AccountingHeader
        legalEntityId={legalEntityId}
        mode={currentMode}
        modeVersion={modeVersion}
        canManageConfig={permissions.canManageConfig}
      />

      {/* Tabs Navigation */}
      <AccountingTabNav
        tabs={tabs}
        activeTab={activeTab}
        onSelectTab={setActiveTab}
      />

      {/* Tab Contents */}
      {activeTab === "overview" && (
        <div>
          <AccountingReadiness
            legalEntityId={legalEntityId}
            readiness={readiness}
            isLoading={isReadinessLoading}
            canManageConfig={permissions.canManageConfig}
          />
          <EntriesExplorerView
            legalEntityId={legalEntityId}
            entries={entries.slice(0, 10)}
            isLoading={isEntriesLoading}
            canReverse={permissions.canReverseEntries}
            canExplain={permissions.canExplainEntries}
            today={today}
          />
        </div>
      )}

      {activeTab === "chartOfAccounts" && (
        <ChartOfAccountsView
          legalEntityId={legalEntityId}
          accounts={accounts}
          isLoading={isAccountsLoading}
          canManageAccounts={permissions.canManageAccounts}
          onOpenGeneralLedger={handleOpenGeneralLedger}
        />
      )}

      {activeTab === "journals" && (
        <JournalsView
          legalEntityId={legalEntityId}
          journals={journals}
          isLoading={isJournalsLoading}
          canManageJournals={permissions.canManageJournals}
        />
      )}

      {activeTab === "periods" && (
        <PeriodsView
          legalEntityId={legalEntityId}
          fiscalYears={fiscalYears}
          periods={periods}
          isLoading={isPeriodsLoading}
          canManagePeriods={permissions.canManagePeriods}
          canSoftClose={permissions.canSoftClosePeriod}
          canClose={permissions.canClosePeriod}
          canReopen={permissions.canReopenPeriod}
        />
      )}

      {activeTab === "locks" && (
        <FinancialLocksView
          legalEntityId={legalEntityId}
          locks={locks}
          isLoading={isLocksLoading}
          canManageLocks={permissions.canManageLocks}
        />
      )}

      {activeTab === "entries" && (
        <EntriesExplorerView
          legalEntityId={legalEntityId}
          entries={entries}
          isLoading={isEntriesLoading}
          canReverse={permissions.canReverseEntries}
          canExplain={permissions.canExplainEntries}
          today={today}
        />
      )}

      {activeTab === "generalJournal" && (
        <GeneralJournalView
          legalEntityId={legalEntityId}
          baseCurrency={baseCurrency}
          today={today}
          canCreate={permissions.canCreateGeneralJournal}
          canPost={permissions.canPostGeneralJournal}
          onEntryPosted={() => setActiveTab("entries")}
        />
      )}

      {activeTab === "openingBalances" && (
        <OpeningBalancesView
          legalEntityId={legalEntityId}
          baseCurrency={baseCurrency}
          today={today}
          canManageOpening={permissions.canManageOpening}
          canPostOpening={permissions.canPostOpening}
        />
      )}

      {activeTab === "dimensions" && (
        <DimensionsView
          legalEntityId={legalEntityId}
          canManageDimensions={permissions.canManageDimensions}
        />
      )}

      {activeTab === "trialBalance" && (
        <TrialBalanceView
          legalEntityId={legalEntityId}
          baseCurrency={baseCurrency}
          onOpenGeneralLedger={handleOpenGeneralLedger}
        />
      )}

      {activeTab === "generalLedger" && (
        <GeneralLedgerView
          legalEntityId={legalEntityId}
          baseCurrency={baseCurrency}
          initialAccountCode={selectedGlAccountCode}
        />
      )}

      {activeTab === "reconciliation" && (
        <ReconciliationView
          legalEntityId={legalEntityId}
          baseCurrency={baseCurrency}
        />
      )}
    </div>
  );
}

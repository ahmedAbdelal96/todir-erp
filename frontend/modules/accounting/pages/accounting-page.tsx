import React, { useState, useEffect } from "react";
import { useTranslation } from "react-i18next";
import { useOutletContext, useSearchParams } from "react-router-dom";
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
import { AccountingHealthViewComponent } from "../features/health/accounting-health-view.tsx";
import { OwnerFinancialView } from "../features/owner/owner-financial-view.tsx";
import { FinancialStatementsView } from "../features/statements/financial-statements-view.tsx";
import { ControlReconciliationsView } from "../features/reconciliations/control-reconciliations-view.tsx";
import { FxRevaluationView } from "../features/fx/fx-revaluation-view.tsx";
import { AccountingTabNav } from "../components/accounting-tab-nav.tsx";
import { useAccountingPermissions } from "../hooks/use-accounting-queries.ts";
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
} from "../hooks/use-ledger-queries.ts";

export function AccountingPage(): React.JSX.Element {
  const { t } = useTranslation("accounting");
  const outletCtx = useOutletContext<WorkspaceOutletContext | undefined>();
  const { legalEntityId, baseCurrency, today } = useLedgerScope({ legalEntityId: outletCtx?.legalEntityId });
  const permissions = useLedgerPermissions();
  const accountingPermissions = useAccountingPermissions();

  const [searchParams, setSearchParams] = useSearchParams();
  const tabFromUrl = searchParams.get("tab");
  const [activeTab, setActiveTab] = useState<string>(tabFromUrl || "overview");
  const [selectedGlAccountCode, setSelectedGlAccountCode] = useState<string | undefined>(undefined);

  useEffect(() => {
    if (tabFromUrl && tabFromUrl !== activeTab) {
      setActiveTab(tabFromUrl);
    }
  }, [tabFromUrl]);

  const handleSelectTab = (tabId: string) => {
    setActiveTab(tabId);
    setSearchParams((prev) => {
      const next = new URLSearchParams(prev);
      next.set("tab", tabId);
      return next;
    }, { replace: true });
  };

  // Queries
  const { data: readiness, isLoading: isReadinessLoading } = useLedgerReadiness(legalEntityId);
  const { data: modeData } = useLedgerMode(legalEntityId);
  const { data: accounts = [], isLoading: isAccountsLoading } = useAccounts(legalEntityId);
  const { data: journals = [], isLoading: isJournalsLoading } = useJournals(legalEntityId);
  const { data: fiscalYears = [] } = useFiscalYears(legalEntityId);
  const { data: periods = [], isLoading: isPeriodsLoading } = usePeriods(legalEntityId);
  const { data: locks = [], isLoading: isLocksLoading } = useLocks(legalEntityId);

  const currentMode = modeData?.mode ?? "Managed";
  const modeVersion = modeData?.version ?? 1;

  // Tabs based on mode
  const tabs = currentMode === "Managed"
    ? [
        { id: "overview", label: t("tabs.overview") },
        { id: "ownerSummary", label: t("tabs.ownerSummary") },
        { id: "health", label: t("tabs.health") },
        { id: "financialStatements", label: t("tabs.financialStatements") },
        { id: "controlReconciliation", label: t("tabs.controlReconciliation") },
        { id: "fxRevaluation", label: t("tabs.fxRevaluation") },
        { id: "periods", label: t("tabs.periods") },
        { id: "entries", label: t("tabs.entries") },
        { id: "generalJournal", label: t("tabs.generalJournal") },
        { id: "trialBalance", label: t("tabs.trialBalance") },
        { id: "generalLedger", label: t("tabs.generalLedger") }
      ]
    : [
        { id: "overview", label: t("tabs.overview") },
        { id: "ownerSummary", label: t("tabs.ownerSummary") },
        { id: "health", label: t("tabs.health") },
        { id: "financialStatements", label: t("tabs.financialStatements") },
        { id: "controlReconciliation", label: t("tabs.controlReconciliation") },
        { id: "fxRevaluation", label: t("tabs.fxRevaluation") },
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
    handleSelectTab("generalLedger");
  };

  return (
    <div data-testid="accounting-page" style={{ padding: "1.25rem 1.5rem", width: "100%", maxWidth: "1600px", margin: "0 auto", boxSizing: "border-box", minWidth: 0 }}>
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
        onSelectTab={handleSelectTab}
      />

      {/* Tab Contents */}
      {activeTab === "overview" && (
        <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
          <AccountingReadiness
            legalEntityId={legalEntityId}
            readiness={readiness}
            isLoading={isReadinessLoading}
            canManageConfig={permissions.canManageConfig}
          />
          <EntriesExplorerView
            legalEntityId={legalEntityId}
            canReverse={permissions.canReverseEntries}
            canExplain={permissions.canExplainEntries}
            today={today}
            pageSize={10}
          />
        </div>
      )}

      {activeTab === "ownerSummary" && (
        <OwnerFinancialView legalEntityId={legalEntityId} onNavigateTab={handleSelectTab} />
      )}

      {activeTab === "health" && (
        <AccountingHealthViewComponent legalEntityId={legalEntityId} onNavigateTab={handleSelectTab} />
      )}

      {activeTab === "financialStatements" && (
        <FinancialStatementsView legalEntityId={legalEntityId} />
      )}

      {activeTab === "controlReconciliation" && (
        <ControlReconciliationsView legalEntityId={legalEntityId} />
      )}

      {activeTab === "fxRevaluation" && (
        <FxRevaluationView legalEntityId={legalEntityId} />
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
          onEntryPosted={() => handleSelectTab("entries")}
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

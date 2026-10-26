import React from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router-dom";
import { PageHeader } from "../../../components/pages/page-header.tsx";
import { Surface } from "../../../components/pages/surface.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { MoneyDisplay } from "../../../components/fields/money-display.tsx";
import {
  useSubledgerScope,
  useOpenItemsQuery,
  useReconciliationControlAccountsQuery
} from "../hooks/subledger-queries.ts";

export function SubledgerLandingPage(): React.JSX.Element {
  const { t } = useTranslation("subledger");
  const { legalEntityId } = useSubledgerScope();
  const today = new Date().toISOString().slice(0, 10);
  const now = new Date();

  // Queries for high-level numbers
  const receivablesQuery = useOpenItemsQuery({
    legalEntityId,
    direction: "Receivable",
    openOnly: true,
    limit: 100
  });

  const payablesQuery = useOpenItemsQuery({
    legalEntityId,
    direction: "Payable",
    openOnly: true,
    limit: 100
  });

  const reconciliationQuery = useReconciliationControlAccountsQuery(
    legalEntityId,
    today,
    Boolean(legalEntityId)
  );

  const recItems = receivablesQuery.data?.items ?? [];
  const payItems = payablesQuery.data?.items ?? [];

  // Compute receivables due and available credits
  const receivablesDue = recItems
    .filter((i) => i.polarity === "Debit")
    .reduce((sum, i) => sum + parseFloat(i.openBaseCarryingAmount || "0"), 0);

  const customerCredits = recItems
    .filter((i) => i.polarity === "Credit")
    .reduce((sum, i) => sum + parseFloat(i.openBaseCarryingAmount || "0"), 0);

  const overdueReceivables = recItems
    .filter((i) => i.polarity === "Debit" && new Date(i.dueDate) < now && parseFloat(i.openTransactionAmount) > 0)
    .reduce((sum, i) => sum + parseFloat(i.openBaseCarryingAmount || "0"), 0);

  // Compute payables due and available credits
  const payablesDue = payItems
    .filter((i) => i.polarity === "Debit")
    .reduce((sum, i) => sum + parseFloat(i.openBaseCarryingAmount || "0"), 0);

  const supplierCredits = payItems
    .filter((i) => i.polarity === "Credit")
    .reduce((sum, i) => sum + parseFloat(i.openBaseCarryingAmount || "0"), 0);

  const overduePayables = payItems
    .filter((i) => i.polarity === "Debit" && new Date(i.dueDate) < now && parseFloat(i.openTransactionAmount) > 0)
    .reduce((sum, i) => sum + parseFloat(i.openBaseCarryingAmount || "0"), 0);

  const controlAccounts = reconciliationQuery.data ?? [];

  return (
    <div className="space-y-6">
      <PageHeader
        title={t("overview.title")}
        description={t("overview.subtitle")}
        secondaryActions={
          <div className="flex items-center gap-2">
            <Link to="/subledger/open-items">
              <Button variant="primary" size="compact" className="bg-slate-900 hover:bg-slate-800 text-white">
                {t("navigation.openItems")}
              </Button>
            </Link>
            <Link to="/subledger/applications">
              <Button variant="secondary" size="compact" className="border-slate-300 text-slate-800 hover:bg-slate-50">
                {t("overview.applyPayment")}
              </Button>
            </Link>
          </div>
        }
      />

      {/* Primary Financial Metric Cards */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
        {/* Receivables Due */}
        <Surface variant="card" padding="normal" className="bg-white border-2 border-slate-300 rounded-lg shadow-sm">
          <span className="text-xs font-bold text-slate-600 uppercase tracking-wider block">
            {t("overview.receivablesDue")}
          </span>
          <p className="mt-2 text-2xl font-black text-slate-900">
            <MoneyDisplay value={{ amount: receivablesDue.toString(), currency: "EGP" }} />
          </p>
          <div className="mt-3 flex items-center justify-between text-xs pt-2 border-t border-slate-100">
            <span className="text-rose-700 font-semibold">
              {t("overview.overdueReceivables")}: <MoneyDisplay value={{ amount: overdueReceivables.toString(), currency: "EGP" }} />
            </span>
            <span className="text-emerald-700 font-semibold">
              {t("overview.availableCustomerCredits")}: <MoneyDisplay value={{ amount: customerCredits.toString(), currency: "EGP" }} />
            </span>
          </div>
        </Surface>

        {/* Payables Due */}
        <Surface variant="card" padding="normal" className="bg-white border-2 border-slate-300 rounded-lg shadow-sm">
          <span className="text-xs font-bold text-slate-600 uppercase tracking-wider block">
            {t("overview.payablesDue")}
          </span>
          <p className="mt-2 text-2xl font-black text-slate-900">
            <MoneyDisplay value={{ amount: payablesDue.toString(), currency: "EGP" }} />
          </p>
          <div className="mt-3 flex items-center justify-between text-xs pt-2 border-t border-slate-100">
            <span className="text-rose-700 font-semibold">
              {t("overview.overduePayables")}: <MoneyDisplay value={{ amount: overduePayables.toString(), currency: "EGP" }} />
            </span>
            <span className="text-emerald-700 font-semibold">
              {t("overview.availableSupplierCredits")}: <MoneyDisplay value={{ amount: supplierCredits.toString(), currency: "EGP" }} />
            </span>
          </div>
        </Surface>

        {/* GL Control Reconciliation Status */}
        <Surface variant="card" padding="normal" className="bg-white border-2 border-slate-300 rounded-lg shadow-sm flex flex-col justify-between">
          <div>
            <span className="text-xs font-bold text-slate-600 uppercase tracking-wider block">
              {t("overview.reconciliationHealth")}
            </span>
            <div className="mt-2 flex items-center gap-2">
              <span className="inline-block w-3 h-3 rounded-full bg-emerald-500 animate-pulse" />
              <span className="text-lg font-bold text-slate-900">
                {controlAccounts.length > 0 ? t("overview.reconciled") : t("landing.readyToCheck")}
              </span>
            </div>
            <p className="mt-1 text-xs text-slate-500">
              {t("landing.activeControlAccounts")} {controlAccounts.length} {t("landing.accountUnit")}
            </p>
          </div>
          <div className="pt-3 border-t border-slate-100">
            <Link to="/subledger/reconciliation">
              <Button variant="secondary" size="compact" className="w-full text-xs font-bold border-slate-300">
                {t("overview.runReconciliation")}
              </Button>
            </Link>
          </div>
        </Surface>
      </div>

      {/* Navigation Quick Cards */}
      <div>
        <h3 className="text-sm font-bold text-slate-900 mb-3">{t("landing.navigationTitle")}</h3>
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
          <Link to="/subledger/open-items" className="block group">
            <Surface variant="card" padding="normal" className="bg-white border border-slate-200 rounded-lg shadow-sm group-hover:border-slate-800 transition-colors h-full flex flex-col justify-between">
              <div>
                <h4 className="text-sm font-bold text-slate-900 group-hover:text-slate-800">{t("navigation.openItems")}</h4>
                <p className="text-xs text-slate-500 mt-1">{t("landing.openItemsDesc")}</p>
              </div>
              <span className="text-xs font-bold text-slate-700 mt-4 block">{t("landing.viewItemsLink")}</span>
            </Surface>
          </Link>

          <Link to="/subledger/applications" className="block group">
            <Surface variant="card" padding="normal" className="bg-white border border-slate-200 rounded-lg shadow-sm group-hover:border-slate-800 transition-colors h-full flex flex-col justify-between">
              <div>
                <h4 className="text-sm font-bold text-slate-900 group-hover:text-slate-800">{t("navigation.applications")}</h4>
                <p className="text-xs text-slate-500 mt-1">{t("landing.applicationsDesc")}</p>
              </div>
              <span className="text-xs font-bold text-slate-700 mt-4 block">{t("landing.startSettlementLink")}</span>
            </Surface>
          </Link>

          <Link to="/subledger/parties" className="block group">
            <Surface variant="card" padding="normal" className="bg-white border border-slate-200 rounded-lg shadow-sm group-hover:border-slate-800 transition-colors h-full flex flex-col justify-between">
              <div>
                <h4 className="text-sm font-bold text-slate-900 group-hover:text-slate-800">{t("navigation.partyFinancial")}</h4>
                <p className="text-xs text-slate-500 mt-1">{t("landing.partyFinancialDesc")}</p>
              </div>
              <span className="text-xs font-bold text-slate-700 mt-4 block">{t("landing.openCockpitLink")}</span>
            </Surface>
          </Link>

          <Link to="/subledger/aging" className="block group">
            <Surface variant="card" padding="normal" className="bg-white border border-slate-200 rounded-lg shadow-sm group-hover:border-slate-800 transition-colors h-full flex flex-col justify-between">
              <div>
                <h4 className="text-sm font-bold text-slate-900 group-hover:text-slate-800">{t("navigation.aging")}</h4>
                <p className="text-xs text-slate-500 mt-1">{t("landing.agingDesc")}</p>
              </div>
              <span className="text-xs font-bold text-slate-700 mt-4 block">{t("landing.viewAgingLink")}</span>
            </Surface>
          </Link>

          <Link to="/subledger/statement" className="block group">
            <Surface variant="card" padding="normal" className="bg-white border border-slate-200 rounded-lg shadow-sm group-hover:border-slate-800 transition-colors h-full flex flex-col justify-between">
              <div>
                <h4 className="text-sm font-bold text-slate-900 group-hover:text-slate-800">{t("navigation.statement")}</h4>
                <p className="text-xs text-slate-500 mt-1">{t("landing.statementDesc")}</p>
              </div>
              <span className="text-xs font-bold text-slate-700 mt-4 block">{t("landing.viewStatementLink")}</span>
            </Surface>
          </Link>

          <Link to="/subledger/reconciliation" className="block group">
            <Surface variant="card" padding="normal" className="bg-white border border-slate-200 rounded-lg shadow-sm group-hover:border-slate-800 transition-colors h-full flex flex-col justify-between">
              <div>
                <h4 className="text-sm font-bold text-slate-900 group-hover:text-slate-800">{t("navigation.reconciliation")}</h4>
                <p className="text-xs text-slate-500 mt-1">{t("landing.reconciliationDesc")}</p>
              </div>
              <span className="text-xs font-bold text-slate-700 mt-4 block">{t("landing.viewReconciliationLink")}</span>
            </Surface>
          </Link>

          <Link to="/subledger/opening" className="block group">
            <Surface variant="card" padding="normal" className="bg-white border border-slate-200 rounded-lg shadow-sm group-hover:border-slate-800 transition-colors h-full flex flex-col justify-between">
              <div>
                <h4 className="text-sm font-bold text-slate-900 group-hover:text-slate-800">{t("navigation.opening")}</h4>
                <p className="text-xs text-slate-500 mt-1">{t("landing.openingDesc")}</p>
              </div>
              <span className="text-xs font-bold text-slate-700 mt-4 block">{t("landing.viewOpeningLink")}</span>
            </Surface>
          </Link>
        </div>
      </div>
    </div>
  );
}

import React, { useState } from "react";
import { useOwnerFinancialSummary } from "../../hooks/use-accounting-queries.ts";
import { formatCurrency } from "../../utils/currency.ts";
import { useAccountingI18n } from "../../i18n";

export interface OwnerFinancialViewProps {
  readonly onNavigateTab?: (tab: string) => void;
}

export const OwnerFinancialView: React.FC<OwnerFinancialViewProps> = ({ onNavigateTab }) => {
  const { t } = useAccountingI18n();
  const [asOfDate, setAsOfDate] = useState<string>(() => new Date().toISOString().slice(0, 10));

  const { data: summary, isLoading, isError, refetch } = useOwnerFinancialSummary(asOfDate);

  const getHealthBadge = (health?: string) => {
    switch (health) {
      case "Healthy":
        return (
          <span className="inline-flex items-center px-3 py-1 rounded-full text-xs font-semibold bg-emerald-100 text-emerald-800 border border-emerald-300">
            <span className="w-2 h-2 rounded-full bg-emerald-500 me-2" />
            {t("owner.healthHealthy")}
          </span>
        );
      case "Attention":
        return (
          <span className="inline-flex items-center px-3 py-1 rounded-full text-xs font-semibold bg-amber-100 text-amber-800 border border-amber-300">
            <span className="w-2 h-2 rounded-full bg-amber-500 me-2" />
            {t("owner.healthAttention")}
          </span>
        );
      case "Blocking":
      default:
        return (
          <span className="inline-flex items-center px-3 py-1 rounded-full text-xs font-semibold bg-rose-100 text-rose-800 border border-rose-300">
            <span className="w-2 h-2 rounded-full bg-rose-500 me-2" />
            {t("owner.healthBlocking")}
          </span>
        );
    }
  };

  return (
    <div className="space-y-6" data-testid="owner-financial-view">
      {/* Control / Filter Bar */}
      <div className="bg-white p-4 rounded-lg border border-slate-200 shadow-sm flex flex-wrap items-center justify-between gap-4">
        <div>
          <h2 className="text-lg font-bold text-slate-800">{t("owner.title")}</h2>
          <p className="text-sm text-slate-500 mt-1">{t("owner.subtitle")}</p>
        </div>

        <div className="flex items-center gap-3">
          <label htmlFor="owner-as-of-date" className="text-sm font-medium text-slate-700">
            {t("owner.asOfDateLabel")}:
          </label>
          <input
            id="owner-as-of-date"
            type="date"
            value={asOfDate}
            onChange={e => setAsOfDate(e.target.value)}
            className="px-3 py-1.5 border border-slate-300 rounded-md text-sm text-slate-800 focus:outline-none focus:ring-2 focus:ring-blue-500"
          />
          <button
            type="button"
            onClick={() => void refetch()}
            className="px-4 py-1.5 bg-slate-100 hover:bg-slate-200 text-slate-700 rounded-md text-sm font-medium transition"
          >
            {t("owner.refresh")}
          </button>
        </div>
      </div>

      {isLoading && (
        <div className="bg-white p-8 rounded-lg border border-slate-200 text-center text-slate-500">
          <p className="animate-pulse">{t("owner.loading")}</p>
        </div>
      )}

      {isError && (
        <div className="bg-rose-50 border border-rose-200 p-4 rounded-lg text-rose-700 text-sm">
          {t("owner.errorLoading")}
        </div>
      )}

      {summary && (
        <>
          {/* Executive Integrity Banner */}
          <div className="bg-white p-5 rounded-lg border border-slate-200 shadow-sm flex flex-wrap items-center justify-between gap-4">
            <div className="flex items-center gap-3">
              <span className="text-sm font-medium text-slate-600">{t("owner.integrityStatus")}:</span>
              {getHealthBadge(summary.health)}
            </div>
            <div className="text-xs text-slate-500">
              {t("owner.lastUpdated")}: {summary.asOfDate} ({summary.currencyCode})
            </div>
          </div>

          {/* 5 KPI Metric Cards */}
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-5 gap-4">
            {/* 1. Cash Book Value */}
            <div className="bg-white p-5 rounded-lg border border-slate-200 shadow-sm hover:border-slate-300 transition">
              <div className="text-xs font-semibold text-slate-500 uppercase tracking-wider">
                {t("owner.cashBook")}
              </div>
              <div
                className={`text-2xl font-bold mt-2 ${
                  summary.cashBookValue >= 0 ? "text-slate-800" : "text-rose-600"
                }`}
                data-testid="owner-kpi-cash"
              >
                {formatCurrency(summary.cashBookValue, summary.currencyCode)}
              </div>
              <div className="text-xs text-slate-400 mt-2">{t("owner.cashBookDesc")}</div>
            </div>

            {/* 2. Receivables */}
            <div className="bg-white p-5 rounded-lg border border-slate-200 shadow-sm hover:border-slate-300 transition">
              <div className="text-xs font-semibold text-slate-500 uppercase tracking-wider">
                {t("owner.receivables")}
              </div>
              <div className="text-2xl font-bold mt-2 text-slate-800" data-testid="owner-kpi-receivables">
                {formatCurrency(summary.receivables, summary.currencyCode)}
              </div>
              <div className="text-xs text-slate-400 mt-2">{t("owner.receivablesDesc")}</div>
            </div>

            {/* 3. Payables */}
            <div className="bg-white p-5 rounded-lg border border-slate-200 shadow-sm hover:border-slate-300 transition">
              <div className="text-xs font-semibold text-slate-500 uppercase tracking-wider">
                {t("owner.payables")}
              </div>
              <div className="text-2xl font-bold mt-2 text-slate-800" data-testid="owner-kpi-payables">
                {formatCurrency(summary.payables, summary.currencyCode)}
              </div>
              <div className="text-xs text-slate-400 mt-2">{t("owner.payablesDesc")}</div>
            </div>

            {/* 4. Inventory Value */}
            <div className="bg-white p-5 rounded-lg border border-slate-200 shadow-sm hover:border-slate-300 transition">
              <div className="text-xs font-semibold text-slate-500 uppercase tracking-wider">
                {t("owner.inventoryValue")}
              </div>
              <div className="text-2xl font-bold mt-2 text-slate-800" data-testid="owner-kpi-inventory">
                {summary.inventoryValue !== null && summary.inventoryValue !== undefined
                  ? formatCurrency(summary.inventoryValue, summary.currencyCode)
                  : t("owner.notAvailable")}
              </div>
              <div className="text-xs text-slate-400 mt-2">{t("owner.inventoryValueDesc")}</div>
            </div>

            {/* 5. Net Profit */}
            <div className="bg-white p-5 rounded-lg border border-slate-200 shadow-sm hover:border-slate-300 transition">
              <div className="text-xs font-semibold text-slate-500 uppercase tracking-wider">
                {t("owner.profit")}
              </div>
              <div
                className={`text-2xl font-bold mt-2 ${
                  summary.profit >= 0 ? "text-emerald-600" : "text-rose-600"
                }`}
                data-testid="owner-kpi-profit"
              >
                {formatCurrency(summary.profit, summary.currencyCode)}
              </div>
              <div className="text-xs text-slate-400 mt-2">{t("owner.profitDesc")}</div>
            </div>
          </div>

          {/* Quick Shortcuts */}
          <div className="bg-white p-6 rounded-lg border border-slate-200 shadow-sm">
            <h3 className="text-sm font-bold text-slate-800 mb-4">{t("owner.quickLinksTitle")}</h3>
            <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-4 gap-4">
              <button
                type="button"
                onClick={() => onNavigateTab?.("financialStatements")}
                className="p-4 text-start border border-slate-200 rounded-lg hover:border-blue-400 hover:bg-blue-50/50 transition group"
              >
                <div className="text-sm font-bold text-slate-800 group-hover:text-blue-700">
                  {t("owner.linkIncomeStatement")}
                </div>
                <div className="text-xs text-slate-500 mt-1">{t("owner.linkIncomeStatementDesc")}</div>
              </button>

              <button
                type="button"
                onClick={() => onNavigateTab?.("financialStatements")}
                className="p-4 text-start border border-slate-200 rounded-lg hover:border-blue-400 hover:bg-blue-50/50 transition group"
              >
                <div className="text-sm font-bold text-slate-800 group-hover:text-blue-700">
                  {t("owner.linkBalanceSheet")}
                </div>
                <div className="text-xs text-slate-500 mt-1">{t("owner.linkBalanceSheetDesc")}</div>
              </button>

              <button
                type="button"
                onClick={() => onNavigateTab?.("controlReconciliation")}
                className="p-4 text-start border border-slate-200 rounded-lg hover:border-blue-400 hover:bg-blue-50/50 transition group"
              >
                <div className="text-sm font-bold text-slate-800 group-hover:text-blue-700">
                  {t("owner.linkReconciliation")}
                </div>
                <div className="text-xs text-slate-500 mt-1">{t("owner.linkReconciliationDesc")}</div>
              </button>

              <button
                type="button"
                onClick={() => onNavigateTab?.("entries")}
                className="p-4 text-start border border-slate-200 rounded-lg hover:border-blue-400 hover:bg-blue-50/50 transition group"
              >
                <div className="text-sm font-bold text-slate-800 group-hover:text-blue-700">
                  {t("owner.linkEntries")}
                </div>
                <div className="text-xs text-slate-500 mt-1">{t("owner.linkEntriesDesc")}</div>
              </button>
            </div>
          </div>
        </>
      )}
    </div>
  );
};

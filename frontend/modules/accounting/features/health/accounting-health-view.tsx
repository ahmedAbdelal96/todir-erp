import React, { useState } from "react";
import { useAccountingHealth } from "../../hooks/use-accounting-queries.ts";
import { useAccountingI18n } from "../../i18n";
import type { AccountingHealthItem } from "../../types.ts";

export interface AccountingHealthViewProps {
  readonly onNavigateTab?: (tab: string) => void;
}

export const AccountingHealthViewComponent: React.FC<AccountingHealthViewProps> = ({ onNavigateTab }) => {
  const { t } = useAccountingI18n();
  const [filterSeverity, setFilterSeverity] = useState<string>("ALL");

  const { data: health, isLoading, isError, refetch } = useAccountingHealth();

  const getSeverityBadge = (severity: string) => {
    switch (severity) {
      case "Blocking":
        return (
          <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold bg-rose-100 text-rose-800 border border-rose-300">
            {t("health.severityBlocking")}
          </span>
        );
      case "Attention":
        return (
          <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold bg-amber-100 text-amber-800 border border-amber-300">
            {t("health.severityAttention")}
          </span>
        );
      case "Info":
      default:
        return (
          <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold bg-emerald-100 text-emerald-800 border border-emerald-300">
            {t("health.severityInfo")}
          </span>
        );
    }
  };

  const getOverallStatusBadge = (status?: string) => {
    switch (status) {
      case "Healthy":
        return (
          <div className="flex items-center gap-2 px-3 py-1.5 rounded-lg bg-emerald-50 border border-emerald-200 text-emerald-800 font-bold text-sm">
            <span className="w-2.5 h-2.5 rounded-full bg-emerald-500" />
            {t("health.overallHealthy")}
          </div>
        );
      case "Attention":
        return (
          <div className="flex items-center gap-2 px-3 py-1.5 rounded-lg bg-amber-50 border border-amber-200 text-amber-800 font-bold text-sm">
            <span className="w-2.5 h-2.5 rounded-full bg-amber-500" />
            {t("health.overallAttention")}
          </div>
        );
      case "Blocking":
      default:
        return (
          <div className="flex items-center gap-2 px-3 py-1.5 rounded-lg bg-rose-50 border border-rose-200 text-rose-800 font-bold text-sm">
            <span className="w-2.5 h-2.5 rounded-full bg-rose-500" />
            {t("health.overallBlocking")}
          </div>
        );
    }
  };

  const filteredItems = (health?.items ?? []).filter(item => {
    if (filterSeverity === "ALL") return true;
    return item.severity === filterSeverity;
  });

  const blockingCount = health?.items.filter(i => i.severity === "Blocking").length ?? 0;
  const attentionCount = health?.items.filter(i => i.severity === "Attention").length ?? 0;
  const readyCount = health?.items.filter(i => i.severity === "Info").length ?? 0;

  return (
    <div className="space-y-6" data-testid="accounting-health-view">
      {/* Top Banner & Overall Status */}
      <div className="bg-white p-6 rounded-lg border border-slate-200 shadow-sm flex flex-wrap items-center justify-between gap-4">
        <div>
          <h2 className="text-lg font-bold text-slate-800">{t("health.title")}</h2>
          <p className="text-sm text-slate-500 mt-1">{t("health.subtitle")}</p>
        </div>

        <div className="flex items-center gap-4">
          {getOverallStatusBadge(health?.status)}
          <button
            type="button"
            onClick={() => void refetch()}
            className="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-md text-sm font-semibold transition shadow-sm"
          >
            {t("health.recheck")}
          </button>
        </div>
      </div>

      {/* Summary KPI Cards */}
      <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
        <button
          type="button"
          onClick={() => setFilterSeverity("Blocking")}
          className={`p-4 rounded-lg border text-start transition ${
            filterSeverity === "Blocking"
              ? "bg-rose-50 border-rose-400 ring-2 ring-rose-200"
              : "bg-white border-slate-200 hover:border-rose-200"
          }`}
        >
          <div className="text-xs font-semibold text-rose-600 uppercase tracking-wider">
            {t("health.countBlocking")}
          </div>
          <div className="text-2xl font-bold mt-1 text-rose-700">{blockingCount}</div>
          <div className="text-xs text-slate-400 mt-1">{t("health.blockingDesc")}</div>
        </button>

        <button
          type="button"
          onClick={() => setFilterSeverity("Attention")}
          className={`p-4 rounded-lg border text-start transition ${
            filterSeverity === "Attention"
              ? "bg-amber-50 border-amber-400 ring-2 ring-amber-200"
              : "bg-white border-slate-200 hover:border-amber-200"
          }`}
        >
          <div className="text-xs font-semibold text-amber-600 uppercase tracking-wider">
            {t("health.countAttention")}
          </div>
          <div className="text-2xl font-bold mt-1 text-amber-700">{attentionCount}</div>
          <div className="text-xs text-slate-400 mt-1">{t("health.attentionDesc")}</div>
        </button>

        <button
          type="button"
          onClick={() => setFilterSeverity("Info")}
          className={`p-4 rounded-lg border text-start transition ${
            filterSeverity === "Info"
              ? "bg-emerald-50 border-emerald-400 ring-2 ring-emerald-200"
              : "bg-white border-slate-200 hover:border-emerald-200"
          }`}
        >
          <div className="text-xs font-semibold text-emerald-600 uppercase tracking-wider">
            {t("health.countReady")}
          </div>
          <div className="text-2xl font-bold mt-1 text-emerald-700">{readyCount}</div>
          <div className="text-xs text-slate-400 mt-1">{t("health.readyDesc")}</div>
        </button>
      </div>

      {isLoading && (
        <div className="bg-white p-8 rounded-lg border border-slate-200 text-center text-slate-500">
          <p className="animate-pulse">{t("health.loading")}</p>
        </div>
      )}

      {isError && (
        <div className="bg-rose-50 border border-rose-200 p-4 rounded-lg text-rose-700 text-sm">
          {t("health.errorLoading")}
        </div>
      )}

      {/* Health Diagnostic Items Table / Cards */}
      {health && (
        <div className="bg-white rounded-lg border border-slate-200 shadow-sm overflow-hidden">
          <div className="p-4 border-b border-slate-200 flex items-center justify-between">
            <h3 className="text-sm font-bold text-slate-800">{t("health.diagnosticItemsTitle")}</h3>
            <div className="flex items-center gap-2">
              <span className="text-xs text-slate-500">{t("health.filterLabel")}:</span>
              <select
                value={filterSeverity}
                onChange={e => setFilterSeverity(e.target.value)}
                className="text-xs border border-slate-300 rounded px-2 py-1 bg-white text-slate-700"
              >
                <option value="ALL">{t("health.filterAll")}</option>
                <option value="Blocking">{t("health.filterBlocking")}</option>
                <option value="Attention">{t("health.filterAttention")}</option>
                <option value="Info">{t("health.filterInfo")}</option>
              </select>
            </div>
          </div>

          <div className="divide-y divide-slate-200">
            {filteredItems.length === 0 ? (
              <div className="p-8 text-center text-slate-400 text-sm">
                {t("health.noItemsFound")}
              </div>
            ) : (
              filteredItems.map((item: AccountingHealthItem) => (
                <div
                  key={item.code}
                  className="p-4 hover:bg-slate-50/70 transition flex flex-col sm:flex-row sm:items-center justify-between gap-3"
                  data-testid={`health-item-${item.code}`}
                >
                  <div className="space-y-1">
                    <div className="flex items-center gap-2">
                      <span className="text-sm font-bold text-slate-800">{item.code}</span>
                      {getSeverityBadge(item.severity)}
                      {item.source && (
                        <span className="px-2 py-0.5 rounded text-[10px] font-medium bg-slate-100 text-slate-600 border border-slate-200">
                          {item.source}
                        </span>
                      )}
                    </div>
                    <p className="text-sm text-slate-600">{item.message}</p>
                  </div>

                  <div className="flex items-center gap-2 self-end sm:self-center">
                    <span className="text-xs font-semibold px-2.5 py-1 rounded bg-slate-100 text-slate-700">
                      {item.status}
                    </span>
                    {item.code.startsWith("reconciliation.") && (
                      <button
                        type="button"
                        onClick={() => onNavigateTab?.("controlReconciliation")}
                        className="text-xs text-blue-600 hover:text-blue-800 font-semibold px-2 py-1 rounded hover:bg-blue-50 transition"
                      >
                        {t("health.actionReconcile")}
                      </button>
                    )}
                    {item.code.startsWith("statement.") && (
                      <button
                        type="button"
                        onClick={() => onNavigateTab?.("financialStatements")}
                        className="text-xs text-blue-600 hover:text-blue-800 font-semibold px-2 py-1 rounded hover:bg-blue-50 transition"
                      >
                        {t("health.actionViewStatements")}
                      </button>
                    )}
                  </div>
                </div>
              ))
            )}
          </div>
        </div>
      )}
    </div>
  );
};

import React, { useState } from "react";
import {
  useReconciliationsList,
  useReconciliationIssues,
  useRunReconciliationMutation
} from "../../hooks/use-accounting-queries.ts";
import { formatCurrency } from "../../utils/currency.ts";
import { useAccountingI18n } from "../../i18n";
import type { SubledgerReconciliationView, ReconciliationIssueView } from "../../types.ts";

export const ControlReconciliationsView: React.FC = () => {
  const { t } = useAccountingI18n();

  // Modal state
  const [isRunModalOpen, setIsRunModalOpen] = useState(false);
  const [selectedType, setSelectedType] = useState<"AR" | "AP" | "Inventory">("AR");
  const [asOfDate, setAsOfDate] = useState<string>(() => new Date().toISOString().slice(0, 10));

  // Queries
  const {
    data: reconciliations = [],
    isLoading: isListLoading,
    refetch: refetchList
  } = useReconciliationsList({ limit: 50 });

  const {
    data: issues = [],
    isLoading: isIssuesLoading,
    refetch: refetchIssues
  } = useReconciliationIssues({ limit: 50 });

  // Mutation
  const runMutation = useRunReconciliationMutation();

  const handleOpenRunModal = (type: "AR" | "AP" | "Inventory") => {
    setSelectedType(type);
    setIsRunModalOpen(true);
  };

  const handleExecuteRun = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      await runMutation.mutateAsync({ type: selectedType, asOf: asOfDate });
      setIsRunModalOpen(false);
      void refetchList();
      void refetchIssues();
    } catch {
      // Handled by query mutation error state
    }
  };

  const getStatusBadge = (status: string) => {
    switch (status) {
      case "Reconciled":
        return (
          <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold bg-emerald-100 text-emerald-800 border border-emerald-300">
            {t("reconciliation.statusReconciled")}
          </span>
        );
      case "Difference":
        return (
          <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold bg-rose-100 text-rose-800 border border-rose-300">
            {t("reconciliation.statusDifference")}
          </span>
        );
      case "NotReady":
      default:
        return (
          <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold bg-slate-100 text-slate-700 border border-slate-300">
            {status || t("reconciliation.statusNotReady")}
          </span>
        );
    }
  };

  return (
    <div className="space-y-6" data-testid="control-reconciliations-view">
      {/* Header and Action Triggers */}
      <div className="bg-white p-5 rounded-lg border border-slate-200 shadow-sm flex flex-wrap items-center justify-between gap-4">
        <div>
          <h2 className="text-lg font-bold text-slate-800">{t("reconciliation.title")}</h2>
          <p className="text-sm text-slate-500 mt-1">{t("reconciliation.subtitle")}</p>
        </div>

        <div className="flex flex-wrap items-center gap-3">
          <button
            type="button"
            onClick={() => handleOpenRunModal("AR")}
            className="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-md text-sm font-semibold transition shadow-sm"
            data-testid="btn-run-ar"
          >
            + {t("reconciliation.runAr")}
          </button>
          <button
            type="button"
            onClick={() => handleOpenRunModal("AP")}
            className="px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white rounded-md text-sm font-semibold transition shadow-sm"
            data-testid="btn-run-ap"
          >
            + {t("reconciliation.runAp")}
          </button>
          <button
            type="button"
            onClick={() => handleOpenRunModal("Inventory")}
            className="px-4 py-2 bg-emerald-600 hover:bg-emerald-700 text-white rounded-md text-sm font-semibold transition shadow-sm"
            data-testid="btn-run-inventory"
          >
            + {t("reconciliation.runInventory")}
          </button>
        </div>
      </div>

      {/* Reconciliations Run History */}
      <div className="bg-white rounded-lg border border-slate-200 shadow-sm overflow-hidden">
        <div className="p-4 border-b border-slate-200 flex items-center justify-between bg-slate-50/50">
          <h3 className="text-sm font-bold text-slate-800">{t("reconciliation.runsHistoryTitle")}</h3>
          <button
            type="button"
            onClick={() => void refetchList()}
            className="text-xs text-slate-600 hover:text-slate-900 font-semibold"
          >
            {t("reconciliation.refresh")}
          </button>
        </div>

        {isListLoading ? (
          <div className="p-8 text-center text-slate-400 text-sm animate-pulse">
            {t("reconciliation.loading")}
          </div>
        ) : reconciliations.length === 0 ? (
          <div className="p-8 text-center text-slate-400 text-sm">
            {t("reconciliation.noRunsYet")}
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-start text-sm">
              <thead>
                <tr className="bg-slate-50 text-slate-600 font-semibold border-b border-slate-200">
                  <th className="py-3 px-4 text-start">{t("reconciliation.colType")}</th>
                  <th className="py-3 px-4 text-start">{t("reconciliation.colAsOfDate")}</th>
                  <th className="py-3 px-4 text-end">{t("reconciliation.colSourceBalance")}</th>
                  <th className="py-3 px-4 text-end">{t("reconciliation.colLedgerBalance")}</th>
                  <th className="py-3 px-4 text-end">{t("reconciliation.colDifference")}</th>
                  <th className="py-3 px-4 text-center">{t("reconciliation.colStatus")}</th>
                  <th className="py-3 px-4 text-start">{t("reconciliation.colDiagnostics")}</th>
                  <th className="py-3 px-4 text-start">{t("reconciliation.colCreatedAt")}</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-200">
                {reconciliations.map((run: SubledgerReconciliationView) => (
                  <tr key={run.id} className="hover:bg-slate-50/60 transition" data-testid={`reconciliation-row-${run.id}`}>
                    <td className="py-3 px-4 font-bold text-slate-800">
                      <span className="px-2 py-0.5 rounded bg-blue-50 text-blue-800 text-xs font-bold border border-blue-200">
                        {run.type}
                      </span>
                    </td>
                    <td className="py-3 px-4 text-slate-700 text-xs font-mono">{run.asOfDate}</td>
                    <td className="py-3 px-4 text-end font-semibold text-slate-800">
                      {run.sourceBalance !== null && run.sourceBalance !== undefined
                        ? formatCurrency(run.sourceBalance, run.currencyCode ?? "EGP")
                        : "—"}
                    </td>
                    <td className="py-3 px-4 text-end font-semibold text-slate-800">
                      {run.ledgerBalance !== null && run.ledgerBalance !== undefined
                        ? formatCurrency(run.ledgerBalance, run.currencyCode ?? "EGP")
                        : "—"}
                    </td>
                    <td
                      className={`py-3 px-4 text-end font-bold ${
                        (run.difference ?? 0) === 0 ? "text-emerald-600" : "text-rose-600"
                      }`}
                    >
                      {run.difference !== null && run.difference !== undefined
                        ? formatCurrency(run.difference, run.currencyCode ?? "EGP")
                        : "—"}
                    </td>
                    <td className="py-3 px-4 text-center">{getStatusBadge(run.status)}</td>
                    <td className="py-3 px-4 text-slate-500 text-xs">
                      {run.diagnostics && run.diagnostics.length > 0 ? (
                        <div className="flex flex-wrap gap-1">
                          {run.diagnostics.map((diag: string, i: number) => (
                            <span
                              key={i}
                              className="px-1.5 py-0.5 rounded bg-amber-50 text-amber-700 text-[11px] border border-amber-200"
                            >
                              {diag}
                            </span>
                          ))}
                        </div>
                      ) : (
                        "—"
                      )}
                    </td>
                    <td className="py-3 px-4 text-slate-400 text-xs">
                      {new Date(run.createdAt).toLocaleString()}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* Reconciliation Issues Table */}
      <div className="bg-white rounded-lg border border-slate-200 shadow-sm overflow-hidden">
        <div className="p-4 border-b border-slate-200 flex items-center justify-between bg-slate-50/50">
          <div>
            <h3 className="text-sm font-bold text-slate-800">{t("reconciliation.issuesTitle")}</h3>
            <p className="text-xs text-slate-500 mt-0.5">{t("reconciliation.issuesSubtitle")}</p>
          </div>
          <button
            type="button"
            onClick={() => void refetchIssues()}
            className="text-xs text-slate-600 hover:text-slate-900 font-semibold"
          >
            {t("reconciliation.refresh")}
          </button>
        </div>

        {isIssuesLoading ? (
          <div className="p-8 text-center text-slate-400 text-sm animate-pulse">
            {t("reconciliation.loading")}
          </div>
        ) : issues.length === 0 ? (
          <div className="p-6 text-center text-emerald-600 text-sm font-medium">
            ✓ {t("reconciliation.noIssuesFound")}
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-start text-sm">
              <thead>
                <tr className="bg-slate-50 text-slate-600 font-semibold border-b border-slate-200">
                  <th className="py-3 px-4 text-start">{t("reconciliation.issueColCode")}</th>
                  <th className="py-3 px-4 text-start">{t("reconciliation.issueColSeverity")}</th>
                  <th className="py-3 px-4 text-start">{t("reconciliation.issueColStatus")}</th>
                  <th className="py-3 px-4 text-start">{t("reconciliation.issueColCreatedAt")}</th>
                  <th className="py-3 px-4 text-start">{t("reconciliation.issueColResolvedAt")}</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-200">
                {issues.map((issue: ReconciliationIssueView) => (
                  <tr key={issue.id} className="hover:bg-slate-50/60 transition">
                    <td className="py-3 px-4 font-mono text-xs font-bold text-slate-800">
                      {issue.code}
                    </td>
                    <td className="py-3 px-4">
                      <span className="px-2 py-0.5 rounded text-xs font-semibold bg-rose-100 text-rose-800 border border-rose-200">
                        {issue.severity}
                      </span>
                    </td>
                    <td className="py-3 px-4 text-slate-700 text-xs font-semibold">{issue.status}</td>
                    <td className="py-3 px-4 text-slate-400 text-xs">
                      {new Date(issue.createdAt).toLocaleString()}
                    </td>
                    <td className="py-3 px-4 text-slate-400 text-xs">
                      {issue.resolvedAt ? new Date(issue.resolvedAt).toLocaleString() : "—"}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* Execute Reconciliation Modal Dialog */}
      {isRunModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
          <div className="bg-white rounded-lg border border-slate-200 shadow-xl max-w-md w-full overflow-hidden animate-in fade-in zoom-in-95 duration-150">
            <div className="p-5 border-b border-slate-200 flex items-center justify-between bg-slate-50">
              <h3 className="font-bold text-slate-800">
                {t("reconciliation.modalRunTitle")} ({selectedType})
              </h3>
              <button
                type="button"
                onClick={() => setIsRunModalOpen(false)}
                className="text-slate-400 hover:text-slate-600 text-lg leading-none"
              >
                &times;
              </button>
            </div>

            <form onSubmit={handleExecuteRun} className="p-5 space-y-4">
              <div>
                <label htmlFor="reconciliation-as-of-date" className="block text-xs font-bold text-slate-700 mb-1">
                  {t("reconciliation.modalAsOfLabel")}
                </label>
                <input
                  id="reconciliation-as-of-date"
                  type="date"
                  required
                  value={asOfDate}
                  onChange={e => setAsOfDate(e.target.value)}
                  className="w-full px-3 py-2 border border-slate-300 rounded-md text-sm text-slate-800 focus:outline-none focus:ring-2 focus:ring-blue-500"
                />
                <p className="text-xs text-slate-400 mt-1">{t("reconciliation.modalAsOfHelp")}</p>
              </div>

              {runMutation.isError && (
                <div className="p-3 bg-rose-50 border border-rose-200 text-rose-700 text-xs rounded">
                  {t("reconciliation.modalRunError")}
                </div>
              )}

              <div className="pt-2 flex items-center justify-end gap-3">
                <button
                  type="button"
                  onClick={() => setIsRunModalOpen(false)}
                  className="px-4 py-2 border border-slate-300 hover:bg-slate-50 text-slate-700 rounded-md text-sm font-medium transition"
                >
                  {t("reconciliation.modalCancel")}
                </button>
                <button
                  type="submit"
                  disabled={runMutation.isPending}
                  className="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-md text-sm font-semibold transition shadow-sm disabled:opacity-50"
                  data-testid="btn-confirm-reconcile"
                >
                  {runMutation.isPending ? t("reconciliation.modalRunning") : t("reconciliation.modalSubmit")}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};

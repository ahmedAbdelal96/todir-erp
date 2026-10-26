import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../components/pages/surface.tsx";
import { MoneyDisplay } from "../../../components/fields/money-display.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { Alert } from "../../../components/primitives/alert.tsx";
import {
  useReconciliationControlAccountsQuery,
  useRunReconciliationMutation
} from "../hooks/subledger-queries.ts";
import type { ReconciliationResult } from "../types.ts";

export interface ControlReconciliationViewProps {
  readonly legalEntityId: string;
}

export function ControlReconciliationView({
  legalEntityId
}: ControlReconciliationViewProps): React.JSX.Element {
  const { t } = useTranslation("subledger");
  const [asOfDate, setAsOfDate] = useState<string>(() => new Date().toISOString().slice(0, 10));
  const [lastResult, setLastResult] = useState<ReconciliationResult | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const controlAccountsQuery = useReconciliationControlAccountsQuery(
    legalEntityId,
    asOfDate,
    Boolean(legalEntityId && asOfDate)
  );

  const runReconciliationMutation = useRunReconciliationMutation();

  const handleRunReconciliation = async () => {
    setErrorMessage(null);
    try {
      const res = await runReconciliationMutation.mutateAsync({
        legalEntityId,
        asOfDate
      });
      setLastResult(res);
    } catch (err: unknown) {
      setErrorMessage(
        err instanceof Error
          ? err.message
          : t("reconciliation.unbalancedNotice")
      );
    }
  };

  const contexts = controlAccountsQuery.data ?? [];
  const isRunning = runReconciliationMutation.isPending;

  return (
    <div className="space-y-6">
      {/* Control Bar */}
      <Surface variant="card" padding="normal" className="bg-white border border-slate-200 rounded-lg shadow-sm">
        <div className="flex flex-wrap items-center justify-between gap-4">
          <div className="flex flex-wrap items-center gap-4">
            <div className="flex items-center gap-2">
              <label htmlFor="recon-as-of-date" className="text-xs font-semibold text-slate-700">
                {t("reconciliation.asOfDate")}:
              </label>
              <input
                id="recon-as-of-date"
                type="date"
                value={asOfDate}
                onChange={(e) => {
                  setAsOfDate(e.target.value);
                  setLastResult(null);
                }}
                className="px-3 py-1.5 text-xs font-medium border border-slate-300 rounded bg-white text-slate-900 shadow-sm focus:border-slate-800 focus:outline-none"
              />
            </div>
            <span className="text-xs text-slate-500">
              {t("reconciliation.description")}
            </span>
          </div>

          <Button
            variant="primary"
            size="compact"
            onClick={() => void handleRunReconciliation()}
            isDisabled={isRunning || !legalEntityId}
            className="bg-slate-900 hover:bg-slate-800 text-white shadow-sm"
          >
            {isRunning ? t("reconciliation.checking") : t("reconciliation.runCheck")}
          </Button>
        </div>
      </Surface>

      {/* Error Alert */}
      {errorMessage && (
        <Alert variant="danger" title={t("reconciliation.errorTitle")} onDismiss={() => setErrorMessage(null)}>
          {errorMessage}
        </Alert>
      )}

      {/* Reconciliation Result Banner */}
      {lastResult && (
        <div
          className={`p-4 rounded-lg border-2 shadow-sm ${
            lastResult.isBalanced
              ? "bg-emerald-50 border-emerald-400 text-emerald-950"
              : "bg-rose-50 border-rose-400 text-rose-950"
          }`}
        >
          <div className="flex items-center justify-between">
            <div>
              <h4 className="text-base font-bold">
                {lastResult.isBalanced ? t("reconciliation.balanced") : t("reconciliation.unbalanced")}
              </h4>
              <p className="text-xs mt-1">
                {lastResult.isBalanced
                  ? t("reconciliation.noVariance")
                  : t("reconciliation.unbalancedNotice")}
              </p>
            </div>
            <div className="text-end">
              <span className="text-xs font-semibold uppercase tracking-wider block">
                {t("reconciliation.variance")}
              </span>
              <span className="text-xl font-black">
                <MoneyDisplay value={{ amount: lastResult.variance, currency: lastResult.baseCurrencyCode }} />
              </span>
            </div>
          </div>
        </div>
      )}

      {/* Subledger Control Sources (Before Run) or Detailed Comparison (After Run) */}
      <Surface variant="card" padding="none" className="bg-white border border-slate-200 rounded-lg shadow-sm overflow-hidden">
        <div className="px-5 py-4 border-b border-slate-200 bg-slate-50 flex items-center justify-between">
          <div>
            <h3 className="text-sm font-bold text-slate-900">
              {lastResult ? t("reconciliation.diagnosticsTitle") : t("reconciliation.approvedControlAccounts")}
            </h3>
            <p className="text-xs text-slate-500 mt-0.5">
              {lastResult
                ? `${t("reconciliation.liveComparison")} ${lastResult.asOfDate}`
                : t("reconciliation.currentSubledgerBalances")}
            </p>
          </div>
          {lastResult && (
            <span
              className={`text-xs px-2.5 py-1 rounded-full font-bold ${
                lastResult.isBalanced ? "bg-emerald-100 text-emerald-800" : "bg-rose-100 text-rose-800"
              }`}
            >
              {lastResult.isBalanced ? t("reconciliation.balancedStatus") : t("reconciliation.unbalancedStatus")}
            </span>
          )}
        </div>

        {lastResult ? (
          <div className="overflow-x-auto">
            <table className="w-full text-xs text-start">
              <thead className="bg-slate-100 text-slate-700 font-semibold border-b border-slate-200">
                <tr>
                  <th className="px-4 py-3 text-start">{t("reconciliation.controlRole")}</th>
                  <th className="px-4 py-3 text-start">{t("reconciliation.direction")}</th>
                  <th className="px-4 py-3 text-start">{t("reconciliation.glAccount")}</th>
                  <th className="px-4 py-3 text-end">{t("reconciliation.subledgerBalance")}</th>
                  <th className="px-4 py-3 text-end">{t("reconciliation.ledgerBalance")}</th>
                  <th className="px-4 py-3 text-end">{t("reconciliation.variance")}</th>
                  <th className="px-4 py-3 text-center">{t("reconciliation.status")}</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-200 text-slate-900 font-medium">
                {(lastResult.details ?? []).map((d) => {
                  const varNum = parseFloat(d.variance || "0");
                  const isZeroVar = Math.abs(varNum) < 0.0001;
                  return (
                    <tr key={d.id} className="hover:bg-slate-50">
                      <td className="px-4 py-3 font-bold text-slate-800">
                        {d.controlRole}
                      </td>
                      <td className="px-4 py-3 text-slate-700">
                        {d.direction === "Receivable" ? t("openItems.receivableOnly") : t("openItems.payableOnly")}
                      </td>
                      <td className="px-4 py-3 font-mono text-slate-600">
                        {d.ledgerAccountId ? d.ledgerAccountId.slice(0, 8) + "..." : t("reconciliation.unmapped")}
                      </td>
                      <td className="px-4 py-3 text-end font-semibold text-slate-900">
                        <MoneyDisplay value={{ amount: d.subledgerBaseBalance, currency: lastResult.baseCurrencyCode }} />
                      </td>
                      <td className="px-4 py-3 text-end font-semibold text-slate-900">
                        <MoneyDisplay value={{ amount: d.ledgerBaseBalance, currency: lastResult.baseCurrencyCode }} />
                      </td>
                      <td
                        className={`px-4 py-3 text-end font-bold ${
                          isZeroVar ? "text-emerald-700" : "text-rose-700"
                        }`}
                      >
                        <MoneyDisplay value={{ amount: d.variance, currency: lastResult.baseCurrencyCode }} />
                      </td>
                      <td className="px-4 py-3 text-center">
                        <span
                          className={`inline-block px-2 py-0.5 text-[10px] font-bold rounded-full ${
                            isZeroVar
                              ? "bg-emerald-100 text-emerald-800"
                              : "bg-rose-100 text-rose-800"
                          }`}
                        >
                          {isZeroVar ? t("reconciliation.ok") : t("reconciliation.discrepancy")}
                        </span>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        ) : contexts.length === 0 ? (
          <div className="p-8 text-center text-slate-500 text-sm">
            {t("reconciliation.noAccountsFound")}
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-xs text-start">
              <thead className="bg-slate-100 text-slate-700 font-semibold border-b border-slate-200">
                <tr>
                  <th className="px-4 py-3 text-start">{t("reconciliation.controlRole")}</th>
                  <th className="px-4 py-3 text-start">{t("reconciliation.direction")}</th>
                  <th className="px-4 py-3 text-start">{t("reconciliation.reference")}</th>
                  <th className="px-4 py-3 text-end">{t("reconciliation.subledgerBalance")}</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-200 text-slate-900 font-medium">
                {contexts.map((ctx, idx) => (
                  <tr key={`${ctx.controlRole}-${idx}`} className="hover:bg-slate-50">
                    <td className="px-4 py-3 font-bold text-slate-800">
                      {ctx.controlRole}
                    </td>
                    <td className="px-4 py-3 text-slate-700">
                      {ctx.direction === "Receivable" ? t("openItems.receivableOnly") : t("openItems.payableOnly")}
                    </td>
                    <td className="px-4 py-3 font-mono text-slate-600">
                      {ctx.controlAccountReference || ctx.controlConfigurationReference}
                    </td>
                    <td className="px-4 py-3 text-end font-semibold text-slate-900">
                      <MoneyDisplay value={{ amount: String(ctx.subledgerBaseBalance ?? 0), currency: ctx.baseCurrencyCode || "EGP" }} />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Surface>
    </div>
  );
}

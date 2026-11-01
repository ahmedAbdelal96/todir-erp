import React, { useState } from "react";
import {
  usePreviewFxRevaluationMutation,
  usePostFxRevaluationMutation,
  useReverseFxRevaluationMutation
} from "../../hooks/use-accounting-queries.ts";
import { formatCurrency } from "../../utils/currency.ts";
import { useAccountingI18n } from "../../i18n";
import type { FxRevaluationRunView, FxRevaluationLineView } from "../../types.ts";

export const FxRevaluationView: React.FC = () => {
  const { t } = useAccountingI18n();

  const [asOfDate, setAsOfDate] = useState<string>(() => new Date().toISOString().slice(0, 10));
  const [autoReversalDate, setAutoReversalDate] = useState<string>("");
  const [currentRun, setCurrentRun] = useState<FxRevaluationRunView | null>(null);

  // Reverse Modal State
  const [isReverseModalOpen, setIsReverseModalOpen] = useState(false);
  const [reversalDate, setReversalDate] = useState<string>(() => new Date().toISOString().slice(0, 10));
  const [reversalReason, setReversalReason] = useState<string>("");

  // Mutations
  const previewMutation = usePreviewFxRevaluationMutation();
  const postMutation = usePostFxRevaluationMutation();
  const reverseMutation = useReverseFxRevaluationMutation();

  const handlePreview = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      const res = await previewMutation.mutateAsync({
        asOfDate,
        idempotencyKey: `fx-prev-${Date.now()}`,
        autoReversalDate: autoReversalDate || null
      });
      setCurrentRun(res);
    } catch {
      // Handled by mutation error
    }
  };

  const handlePost = async () => {
    if (!currentRun) return;
    try {
      const res = await postMutation.mutateAsync(currentRun.id);
      setCurrentRun(res);
    } catch {
      // Handled by mutation error
    }
  };

  const handleReverse = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!currentRun) return;
    try {
      const res = await reverseMutation.mutateAsync({
        runId: currentRun.id,
        request: {
          reversalDate,
          reason: reversalReason
        }
      });
      setCurrentRun(res);
      setIsReverseModalOpen(false);
    } catch {
      // Handled by mutation error
    }
  };

  const totalGainLoss = (currentRun?.lines ?? []).reduce(
    (sum, l) => sum + (l.differenceAmount ?? 0),
    0
  );

  return (
    <div className="space-y-6" data-testid="fx-revaluation-view">
      {/* Top Header */}
      <div className="bg-white p-5 rounded-lg border border-slate-200 shadow-sm flex flex-wrap items-center justify-between gap-4">
        <div>
          <h2 className="text-lg font-bold text-slate-800">{t("fx.title")}</h2>
          <p className="text-sm text-slate-500 mt-1">{t("fx.subtitle")}</p>
        </div>

        {currentRun && (
          <div className="flex items-center gap-2">
            <span className="text-xs text-slate-500">{t("fx.runStatus")}:</span>
            <span
              className={`px-3 py-1 rounded-full text-xs font-bold border ${
                currentRun.status === "Posted"
                  ? "bg-emerald-100 text-emerald-800 border-emerald-300"
                  : currentRun.status === "Reversed"
                  ? "bg-slate-100 text-slate-700 border-slate-300"
                  : "bg-blue-100 text-blue-800 border-blue-300"
              }`}
              data-testid="fx-run-status-badge"
            >
              {currentRun.status}
            </span>
          </div>
        )}
      </div>

      {/* Revaluation Parameters & Form */}
      <div className="bg-white p-5 rounded-lg border border-slate-200 shadow-sm">
        <form onSubmit={handlePreview} className="flex flex-wrap items-end gap-4">
          <div>
            <label htmlFor="fx-as-of-date" className="block text-xs font-bold text-slate-700 mb-1">
              {t("fx.asOfDateLabel")}
            </label>
            <input
              id="fx-as-of-date"
              type="date"
              required
              value={asOfDate}
              onChange={e => setAsOfDate(e.target.value)}
              className="px-3 py-2 border border-slate-300 rounded-md text-sm text-slate-800 focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>

          <div>
            <label htmlFor="fx-auto-reversal-date" className="block text-xs font-bold text-slate-700 mb-1">
              {t("fx.autoReversalDateLabel")} ({t("fx.optional")})
            </label>
            <input
              id="fx-auto-reversal-date"
              type="date"
              value={autoReversalDate}
              onChange={e => setAutoReversalDate(e.target.value)}
              className="px-3 py-2 border border-slate-300 rounded-md text-sm text-slate-800 focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>

          <button
            type="submit"
            disabled={previewMutation.isPending}
            className="px-5 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-md text-sm font-semibold transition shadow-sm disabled:opacity-50"
            data-testid="btn-fx-preview"
          >
            {previewMutation.isPending ? t("fx.previewing") : t("fx.btnPreview")}
          </button>
        </form>

        {previewMutation.isError && (
          <div className="mt-4 p-3 bg-rose-50 border border-rose-200 text-rose-700 text-xs rounded">
            {t("fx.previewError")}
          </div>
        )}
      </div>

      {/* Preview Lines Results Table */}
      {currentRun && (
        <div className="bg-white rounded-lg border border-slate-200 shadow-sm overflow-hidden space-y-0">
          <div className="p-4 border-b border-slate-200 flex flex-wrap items-center justify-between gap-4 bg-slate-50/50">
            <div>
              <h3 className="text-sm font-bold text-slate-800">{t("fx.previewLinesTitle")}</h3>
              <p className="text-xs text-slate-500 mt-0.5">
                {t("fx.totalItems")}: {currentRun.lines.length} | {t("fx.totalGainLoss")}:{" "}
                <span
                  className={`font-bold ${
                    totalGainLoss >= 0 ? "text-emerald-600" : "text-rose-600"
                  }`}
                >
                  {formatCurrency(totalGainLoss, "EGP")}
                </span>
              </p>
            </div>

            {/* Post & Reverse Triggers */}
            <div className="flex items-center gap-3">
              {currentRun.status !== "Posted" && currentRun.status !== "Reversed" && (
                <button
                  type="button"
                  onClick={handlePost}
                  disabled={postMutation.isPending || currentRun.lines.length === 0}
                  className="px-4 py-2 bg-emerald-600 hover:bg-emerald-700 text-white rounded-md text-sm font-semibold transition shadow-sm disabled:opacity-50"
                  data-testid="btn-post-fx"
                >
                  {postMutation.isPending ? t("fx.posting") : t("fx.btnPost")}
                </button>
              )}

              {currentRun.status === "Posted" && (
                <button
                  type="button"
                  onClick={() => setIsReverseModalOpen(true)}
                  className="px-4 py-2 bg-rose-600 hover:bg-rose-700 text-white rounded-md text-sm font-semibold transition shadow-sm"
                  data-testid="btn-open-reverse-fx"
                >
                  {t("fx.btnReverse")}
                </button>
              )}
            </div>
          </div>

          {postMutation.isError && (
            <div className="p-3 bg-rose-50 border-b border-rose-200 text-rose-700 text-xs">
              {t("fx.postError")}
            </div>
          )}

          {currentRun.lines.length === 0 ? (
            <div className="p-8 text-center text-slate-400 text-sm">
              {t("fx.noOpenItemsToRevalue")}
            </div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-start text-sm">
                <thead>
                  <tr className="bg-slate-50 text-slate-600 font-semibold border-b border-slate-200">
                    <th className="py-3 px-4 text-start">{t("fx.colDirection")}</th>
                    <th className="py-3 px-4 text-end">{t("fx.colTxAmount")}</th>
                    <th className="py-3 px-4 text-end">{t("fx.colCarryingAmount")}</th>
                    <th className="py-3 px-4 text-end">{t("fx.colRate")}</th>
                    <th className="py-3 px-4 text-end">{t("fx.colRevaluedAmount")}</th>
                    <th className="py-3 px-4 text-end">{t("fx.colDifference")}</th>
                    <th className="py-3 px-4 text-center">{t("fx.colRole")}</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-200">
                  {currentRun.lines.map((line: FxRevaluationLineView) => (
                    <tr key={line.id} className="hover:bg-slate-50/60 transition">
                      <td className="py-3 px-4">
                        <span
                          className={`px-2 py-0.5 rounded text-xs font-semibold ${
                            line.direction === "Receivable"
                              ? "bg-blue-100 text-blue-800"
                              : "bg-indigo-100 text-indigo-800"
                          }`}
                        >
                          {line.direction}
                        </span>
                      </td>
                      <td className="py-3 px-4 text-end font-mono text-xs text-slate-700">
                        {formatCurrency(line.openTransactionAmount, line.transactionCurrency)}
                      </td>
                      <td className="py-3 px-4 text-end font-semibold text-slate-800">
                        {formatCurrency(line.carryingBaseAmount, line.baseCurrency)}
                      </td>
                      <td className="py-3 px-4 text-end font-mono text-xs text-slate-600">
                        {line.exchangeRate.toFixed(4)}
                      </td>
                      <td className="py-3 px-4 text-end font-semibold text-slate-800">
                        {formatCurrency(line.revaluedBaseAmount, line.baseCurrency)}
                      </td>
                      <td
                        className={`py-3 px-4 text-end font-bold ${
                          line.differenceAmount >= 0 ? "text-emerald-600" : "text-rose-600"
                        }`}
                      >
                        {formatCurrency(line.differenceAmount, line.baseCurrency)}
                      </td>
                      <td className="py-3 px-4 text-center text-xs text-slate-500">
                        {line.differenceRole}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      )}

      {/* Reversal Modal Dialog */}
      {isReverseModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
          <div className="bg-white rounded-lg border border-slate-200 shadow-xl max-w-md w-full overflow-hidden">
            <div className="p-5 border-b border-slate-200 flex items-center justify-between bg-slate-50">
              <h3 className="font-bold text-slate-800">{t("fx.modalReverseTitle")}</h3>
              <button
                type="button"
                onClick={() => setIsReverseModalOpen(false)}
                className="text-slate-400 hover:text-slate-600 text-lg leading-none"
              >
                &times;
              </button>
            </div>

            <form onSubmit={handleReverse} className="p-5 space-y-4">
              <div>
                <label htmlFor="fx-reversal-date" className="block text-xs font-bold text-slate-700 mb-1">
                  {t("fx.modalReversalDateLabel")}
                </label>
                <input
                  id="fx-reversal-date"
                  type="date"
                  required
                  value={reversalDate}
                  onChange={e => setReversalDate(e.target.value)}
                  className="w-full px-3 py-2 border border-slate-300 rounded-md text-sm text-slate-800 focus:outline-none focus:ring-2 focus:ring-blue-500"
                />
              </div>

              <div>
                <label htmlFor="fx-reversal-reason" className="block text-xs font-bold text-slate-700 mb-1">
                  {t("fx.modalReversalReasonLabel")}
                </label>
                <textarea
                  id="fx-reversal-reason"
                  required
                  rows={3}
                  value={reversalReason}
                  onChange={e => setReversalReason(e.target.value)}
                  placeholder={t("fx.modalReversalReasonPlaceholder")}
                  className="w-full px-3 py-2 border border-slate-300 rounded-md text-sm text-slate-800 focus:outline-none focus:ring-2 focus:ring-blue-500"
                />
              </div>

              {reverseMutation.isError && (
                <div className="p-3 bg-rose-50 border border-rose-200 text-rose-700 text-xs rounded">
                  {t("fx.modalReverseError")}
                </div>
              )}

              <div className="pt-2 flex items-center justify-end gap-3">
                <button
                  type="button"
                  onClick={() => setIsReverseModalOpen(false)}
                  className="px-4 py-2 border border-slate-300 hover:bg-slate-50 text-slate-700 rounded-md text-sm font-medium transition"
                >
                  {t("fx.modalCancel")}
                </button>
                <button
                  type="submit"
                  disabled={reverseMutation.isPending}
                  className="px-4 py-2 bg-rose-600 hover:bg-rose-700 text-white rounded-md text-sm font-semibold transition shadow-sm disabled:opacity-50"
                  data-testid="btn-confirm-reverse-fx"
                >
                  {reverseMutation.isPending ? t("fx.modalReversing") : t("fx.modalConfirmReverse")}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};

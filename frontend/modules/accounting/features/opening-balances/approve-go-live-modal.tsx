import React, { useState } from "react";
import { useApproveGoLiveMutation } from "../../hooks/use-accounting-queries.ts";
import { useAccountingI18n } from "../../i18n";
import type { GoLiveApprovalView, CloseCheckView } from "../../types.ts";

export interface ApproveGoLiveModalProps {
  readonly openingBatchId: string;
  readonly goLiveDate?: string | null;
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly onSuccess?: () => void;
}

export const ApproveGoLiveModal: React.FC<ApproveGoLiveModalProps> = ({
  openingBatchId,
  goLiveDate,
  isOpen,
  onClose,
  onSuccess
}) => {
  const { t } = useAccountingI18n();
  const [reason, setReason] = useState("");
  const [subledgerBatchId, setSubledgerBatchId] = useState("");
  const [approvalResult, setApprovalResult] = useState<GoLiveApprovalView | null>(null);

  const approveMutation = useApproveGoLiveMutation();

  if (!isOpen) return null;

  const handleApprove = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!reason.trim()) return;
    try {
      const res = await approveMutation.mutateAsync({
        openingBatchId,
        subledgerOpeningBatchId: subledgerBatchId.trim() || null,
        financialGoLiveDate: goLiveDate || null,
        reason: reason.trim(),
        idempotencyKey: `go-live-approve-${openingBatchId}-${Date.now()}`
      });
      setApprovalResult(res);
      onSuccess?.();
    } catch {
      // Handled by mutation error
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4" data-testid="approve-go-live-modal">
      <div className="bg-white rounded-lg border border-slate-200 shadow-xl max-w-lg w-full overflow-hidden animate-in fade-in zoom-in-95 duration-150">
        <div className="p-5 border-b border-slate-200 flex items-center justify-between bg-slate-50">
          <div>
            <h3 className="font-bold text-slate-800">{t("goLive.modalTitle")}</h3>
            <p className="text-xs text-slate-500 mt-0.5">
              {t("goLive.batchLabel")}: {openingBatchId} | {t("goLive.dateLabel")}: {goLiveDate || "—"}
            </p>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="text-slate-400 hover:text-slate-600 text-lg leading-none"
          >
            &times;
          </button>
        </div>

        {approvalResult ? (
          <div className="p-5 space-y-4">
            <div className="p-4 bg-emerald-50 border border-emerald-200 rounded-lg text-emerald-800 flex items-center gap-3">
              <span className="text-2xl">✓</span>
              <div>
                <h4 className="font-bold text-sm">{t("goLive.successTitle")}</h4>
                <p className="text-xs text-emerald-700 mt-0.5">{t("goLive.successSubtitle")}</p>
              </div>
            </div>

            <div className="space-y-2">
              <span className="text-xs font-bold text-slate-700 uppercase tracking-wider">
                {t("goLive.executedChecksTitle")}
              </span>
              <div className="divide-y divide-slate-100 border border-slate-200 rounded-md max-h-48 overflow-y-auto">
                {approvalResult.checks.map((check: CloseCheckView, idx) => (
                  <div key={idx} className="p-2.5 flex items-center justify-between text-xs">
                    <div>
                      <span className="font-semibold text-slate-800">{check.code}</span>
                      <p className="text-slate-500 text-[11px]">{check.message}</p>
                    </div>
                    <span
                      className={`px-2 py-0.5 rounded text-[11px] font-semibold ${
                        check.status === "Pass"
                          ? "bg-emerald-100 text-emerald-800"
                          : "bg-slate-100 text-slate-600"
                      }`}
                    >
                      {check.status}
                    </span>
                  </div>
                ))}
              </div>
            </div>

            <div className="pt-2 flex justify-end">
              <button
                type="button"
                onClick={onClose}
                className="px-5 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-md text-sm font-semibold transition"
              >
                {t("goLive.closeDone")}
              </button>
            </div>
          </div>
        ) : (
          <form onSubmit={handleApprove} className="p-5 space-y-4">
            <div className="p-3 bg-blue-50 border border-blue-200 text-blue-800 text-xs rounded">
              ℹ️ {t("goLive.infoBanner")}
            </div>

            <div>
              <label htmlFor="go-live-subledger-batch" className="block text-xs font-bold text-slate-700 mb-1">
                {t("goLive.subledgerBatchLabel")} ({t("goLive.optional")})
              </label>
              <input
                id="go-live-subledger-batch"
                type="text"
                value={subledgerBatchId}
                onChange={e => setSubledgerBatchId(e.target.value)}
                placeholder="UUID"
                className="w-full px-3 py-2 border border-slate-300 rounded-md text-sm text-slate-800 focus:outline-none focus:ring-2 focus:ring-blue-500 font-mono text-xs"
              />
            </div>

            <div>
              <label htmlFor="go-live-approval-reason" className="block text-xs font-bold text-slate-700 mb-1">
                {t("goLive.reasonLabel")} *
              </label>
              <textarea
                id="go-live-approval-reason"
                required
                rows={3}
                value={reason}
                onChange={e => setReason(e.target.value)}
                placeholder={t("goLive.reasonPlaceholder")}
                className="w-full px-3 py-2 border border-slate-300 rounded-md text-sm text-slate-800 focus:outline-none focus:ring-2 focus:ring-blue-500"
              />
            </div>

            {approveMutation.isError && (
              <div className="p-3 bg-rose-50 border border-rose-200 text-rose-700 text-xs rounded">
                {t("goLive.errorApproving")}
              </div>
            )}

            <div className="pt-2 flex items-center justify-end gap-3">
              <button
                type="button"
                onClick={onClose}
                className="px-4 py-2 border border-slate-300 hover:bg-slate-50 text-slate-700 rounded-md text-sm font-medium transition"
              >
                {t("goLive.cancel")}
              </button>
              <button
                type="submit"
                disabled={approveMutation.isPending || !reason.trim()}
                className="px-5 py-2 bg-emerald-600 hover:bg-emerald-700 text-white rounded-md text-sm font-semibold transition shadow-sm disabled:opacity-50"
                data-testid="btn-confirm-go-live"
              >
                {approveMutation.isPending ? t("goLive.approving") : t("goLive.btnApprove")}
              </button>
            </div>
          </form>
        )}
      </div>
    </div>
  );
};

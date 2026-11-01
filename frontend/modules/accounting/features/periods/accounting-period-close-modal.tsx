import React, { useEffect, useState } from "react";
import {
  usePreviewCloseMutation,
  useClosePeriodMutation
} from "../../hooks/use-accounting-queries.ts";
import { useAccountingI18n } from "../../i18n";
import type { PeriodDto, CloseRunView, CloseCheckView } from "../../types.ts";

export interface AccountingPeriodCloseModalProps {
  readonly period: PeriodDto;
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly onSuccess?: () => void;
}

export const AccountingPeriodCloseModal: React.FC<AccountingPeriodCloseModalProps> = ({
  period,
  isOpen,
  onClose,
  onSuccess
}) => {
  const { t } = useAccountingI18n();
  const [closeReason, setCloseReason] = useState("");
  const [previewRun, setPreviewRun] = useState<CloseRunView | null>(null);

  const previewMutation = usePreviewCloseMutation();
  const closeMutation = useClosePeriodMutation();

  useEffect(() => {
    if (isOpen && period) {
      setCloseReason("");
      previewMutation
        .mutateAsync({
          periodId: period.id,
          idempotencyKey: `close-prev-${period.id}-${Date.now()}`
        })
        .then(res => setPreviewRun(res))
        .catch(() => {});
    }
  }, [isOpen, period.id]);

  if (!isOpen) return null;

  const hasBlockingErrors = (previewRun?.checks ?? []).some(
    check => check.severity === "Blocking" && check.status !== "Pass"
  );

  const handleConfirmClose = async (e: React.FormEvent) => {
    e.preventDefault();
    if (hasBlockingErrors) return;
    try {
      await closeMutation.mutateAsync({
        periodId: period.id,
        idempotencyKey: `close-exec-${period.id}-${Date.now()}`,
        reason: closeReason.trim() || undefined
      });
      onSuccess?.();
      onClose();
    } catch {
      // Handled by closeMutation
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4" data-testid="period-close-modal">
      <div className="bg-white rounded-lg border border-slate-200 shadow-xl max-w-lg w-full overflow-hidden animate-in fade-in zoom-in-95 duration-150">
        <div className="p-5 border-b border-slate-200 flex items-center justify-between bg-slate-50">
          <div>
            <h3 className="font-bold text-slate-800">{t("close.modalTitle")}</h3>
            <p className="text-xs text-slate-500 mt-0.5">
              {period.code} ({period.startDate} → {period.endDate})
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

        <form onSubmit={handleConfirmClose} className="p-5 space-y-4">
          {previewMutation.isPending && (
            <div className="p-6 text-center text-slate-500 text-sm animate-pulse">
              {t("close.previewingChecks")}
            </div>
          )}

          {previewMutation.isError && (
            <div className="p-3 bg-rose-50 border border-rose-200 text-rose-700 text-xs rounded">
              {t("close.previewError")}
            </div>
          )}

          {previewRun && (
            <div className="space-y-3">
              <div className="text-xs font-bold text-slate-700 uppercase tracking-wider">
                {t("close.checklistTitle")}
              </div>
              <div className="max-h-56 overflow-y-auto divide-y divide-slate-100 border border-slate-200 rounded-md">
                {previewRun.checks.map((check: CloseCheckView, idx) => (
                  <div key={idx} className="p-3 flex items-start justify-between gap-2 text-xs">
                    <div>
                      <span className="font-semibold text-slate-800">{check.code}</span>
                      <p className="text-slate-600 mt-0.5">{check.message}</p>
                    </div>
                    <span
                      className={`px-2 py-0.5 rounded font-semibold text-[11px] whitespace-nowrap ${
                        check.status === "Pass"
                          ? "bg-emerald-100 text-emerald-800"
                          : check.severity === "Blocking"
                          ? "bg-rose-100 text-rose-800"
                          : "bg-amber-100 text-amber-800"
                      }`}
                    >
                      {check.status}
                    </span>
                  </div>
                ))}
              </div>

              {hasBlockingErrors && (
                <div className="p-3 bg-rose-50 border border-rose-200 text-rose-800 text-xs rounded font-medium">
                  ⚠️ {t("close.blockingErrorWarning")}
                </div>
              )}
            </div>
          )}

          <div>
            <label htmlFor="period-close-note" className="block text-xs font-bold text-slate-700 mb-1">
              {t("close.optionalReasonLabel")}
            </label>
            <input
              id="period-close-note"
              type="text"
              value={closeReason}
              onChange={e => setCloseReason(e.target.value)}
              placeholder={t("close.reasonPlaceholder")}
              className="w-full px-3 py-2 border border-slate-300 rounded-md text-sm text-slate-800 focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>

          {closeMutation.isError && (
            <div className="p-3 bg-rose-50 border border-rose-200 text-rose-700 text-xs rounded">
              {t("close.executeError")}
            </div>
          )}

          <div className="pt-2 flex items-center justify-end gap-3">
            <button
              type="button"
              onClick={onClose}
              className="px-4 py-2 border border-slate-300 hover:bg-slate-50 text-slate-700 rounded-md text-sm font-medium transition"
            >
              {t("close.cancel")}
            </button>
            <button
              type="submit"
              disabled={closeMutation.isPending || previewMutation.isPending || hasBlockingErrors}
              className="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-md text-sm font-semibold transition shadow-sm disabled:opacity-50"
              data-testid="btn-confirm-period-close"
            >
              {closeMutation.isPending ? t("close.closing") : t("close.confirmClose")}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};

import React, { useState } from "react";
import { useReopenPeriodMutation } from "../../hooks/use-accounting-queries.ts";
import { useAccountingI18n } from "../../i18n";
import type { PeriodDto } from "../../types.ts";

export interface AccountingPeriodReopenModalProps {
  readonly period: PeriodDto;
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly onSuccess?: () => void;
}

export const AccountingPeriodReopenModal: React.FC<AccountingPeriodReopenModalProps> = ({
  period,
  isOpen,
  onClose,
  onSuccess
}) => {
  const { t } = useAccountingI18n();
  const [reopenReason, setReopenReason] = useState("");

  const reopenMutation = useReopenPeriodMutation();

  if (!isOpen) return null;

  const handleConfirmReopen = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!reopenReason.trim()) return;
    try {
      await reopenMutation.mutateAsync({
        periodId: period.id,
        request: {
          reason: reopenReason.trim(),
          idempotencyKey: `reopen-${period.id}-${Date.now()}`
        }
      });
      onSuccess?.();
      onClose();
    } catch {
      // Handled by reopenMutation error
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4" data-testid="period-reopen-modal">
      <div className="bg-white rounded-lg border border-slate-200 shadow-xl max-w-md w-full overflow-hidden animate-in fade-in zoom-in-95 duration-150">
        <div className="p-5 border-b border-slate-200 flex items-center justify-between bg-slate-50">
          <div>
            <h3 className="font-bold text-slate-800">{t("reopen.modalTitle")}</h3>
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

        <form onSubmit={handleConfirmReopen} className="p-5 space-y-4">
          <div className="p-3 bg-amber-50 border border-amber-200 text-amber-800 text-xs rounded">
            ⚠️ {t("reopen.warningMessage")}
          </div>

          <div>
            <label htmlFor="period-reopen-reason" className="block text-xs font-bold text-slate-700 mb-1">
              {t("reopen.requiredReasonLabel")} *
            </label>
            <textarea
              id="period-reopen-reason"
              required
              rows={3}
              value={reopenReason}
              onChange={e => setReopenReason(e.target.value)}
              placeholder={t("reopen.reasonPlaceholder")}
              className="w-full px-3 py-2 border border-slate-300 rounded-md text-sm text-slate-800 focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
            <p className="text-xs text-slate-400 mt-1">{t("reopen.reasonHelp")}</p>
          </div>

          {reopenMutation.isError && (
            <div className="p-3 bg-rose-50 border border-rose-200 text-rose-700 text-xs rounded">
              {t("reopen.errorReopening")}
            </div>
          )}

          <div className="pt-2 flex items-center justify-end gap-3">
            <button
              type="button"
              onClick={onClose}
              className="px-4 py-2 border border-slate-300 hover:bg-slate-50 text-slate-700 rounded-md text-sm font-medium transition"
            >
              {t("reopen.cancel")}
            </button>
            <button
              type="submit"
              disabled={reopenMutation.isPending || !reopenReason.trim()}
              className="px-4 py-2 bg-amber-600 hover:bg-amber-700 text-white rounded-md text-sm font-semibold transition shadow-sm disabled:opacity-50"
              data-testid="btn-confirm-period-reopen"
            >
              {reopenMutation.isPending ? t("reopen.reopening") : t("reopen.confirmReopen")}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};

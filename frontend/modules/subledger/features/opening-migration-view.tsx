import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../components/pages/surface.tsx";
import { MoneyDisplay } from "../../../components/fields/money-display.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { Alert } from "../../../components/primitives/alert.tsx";
import {
  useCreateOpeningBatchMutation,
  useValidateOpeningBatchMutation,
  useFinalizeOpeningBatchMutation
} from "../hooks/subledger-queries.ts";
import type { OpeningSubledgerBatchResult, OpeningSubledgerValidationResult } from "../types.ts";

export interface OpeningMigrationViewProps {
  readonly legalEntityId: string;
}

export function OpeningMigrationView({
  legalEntityId
}: OpeningMigrationViewProps): React.JSX.Element {
  const { t } = useTranslation("subledger");

  const [batchIdentity, setBatchIdentity] = useState<string>(() => `OPEN-BATCH-${new Date().getFullYear()}-01`);
  const [goLiveDate, setGoLiveDate] = useState<string>(() => new Date().toISOString().slice(0, 10));
  const [baseCurrency, setBaseCurrency] = useState<string>("EGP");
  const [controlRef, setControlRef] = useState<string>("DEFAULT-AR-AP-CTRL");

  const [activeBatch, setActiveBatch] = useState<OpeningSubledgerBatchResult | null>(null);
  const [validationResult, setValidationResult] = useState<OpeningSubledgerValidationResult | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);

  const createBatchMutation = useCreateOpeningBatchMutation();
  const validateBatchMutation = useValidateOpeningBatchMutation();
  const finalizeBatchMutation = useFinalizeOpeningBatchMutation();

  const handleCreateBatch = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMessage(null);
    setSuccessMessage(null);
    try {
      const rawRes = await createBatchMutation.mutateAsync({
        legalEntityId,
        batchIdentity,
        goLiveDate,
        baseCurrencyCode: baseCurrency,
        controlConfigurationReference: controlRef,
        idempotencyKey: `create-batch-${batchIdentity}-${Date.now()}`
      });
      const resolvedId =
        typeof rawRes === "string"
          ? rawRes
          : (rawRes as unknown as Record<string, string>)?.batchId ||
            (rawRes as unknown as Record<string, string>)?.id ||
            batchIdentity;
      setActiveBatch({
        id: resolvedId,
        batchIdentity,
        goLiveDate,
        status: "Draft",
        lineCount: 0,
        openItemIds: [],
        alreadyCompleted: false
      });
      setSuccessMessage(t("opening.activeBatch") + ": " + batchIdentity);
    } catch (err: unknown) {
      setErrorMessage(err instanceof Error ? err.message : t("opening.imbalanceWarning"));
    }
  };

  const handleValidateBatch = async () => {
    if (!activeBatch) return;
    setErrorMessage(null);
    try {
      const res = await validateBatchMutation.mutateAsync({
        batchId: activeBatch.id,
        legalEntityId
      });
      setValidationResult(res);
    } catch (err: unknown) {
      setErrorMessage(err instanceof Error ? err.message : t("opening.imbalanceWarning"));
    }
  };

  const handleFinalizeBatch = async () => {
    if (!activeBatch) return;
    setErrorMessage(null);
    try {
      const res = await finalizeBatchMutation.mutateAsync({
        batchId: activeBatch.id,
        legalEntityId
      });
      setActiveBatch(res);
      setSuccessMessage(t("opening.immutableNotice"));
    } catch (err: unknown) {
      setErrorMessage(err instanceof Error ? err.message : t("opening.imbalanceWarning"));
    }
  };

  const isCreating = createBatchMutation.isPending;
  const isValidating = validateBatchMutation.isPending;
  const isFinalizing = finalizeBatchMutation.isPending;

  return (
    <div className="space-y-6">
      {/* Header Info */}
      <Surface variant="card" padding="normal" className="bg-white border border-slate-200 rounded-lg shadow-sm">
        <div>
          <h2 className="text-base font-bold text-slate-900">{t("opening.title")}</h2>
          <p className="text-xs text-slate-500 mt-1">{t("opening.subtitle")}</p>
        </div>
      </Surface>

      {/* Messages */}
      {errorMessage && (
        <Alert variant="danger" title={t("profile.errorTitle")} onDismiss={() => setErrorMessage(null)}>
          {errorMessage}
        </Alert>
      )}

      {successMessage && (
        <Alert variant="success" title={t("profile.successTitle")} onDismiss={() => setSuccessMessage(null)}>
          {successMessage}
        </Alert>
      )}

      {/* Step 1: Batch Setup Form */}
      <Surface variant="card" padding="normal" className="bg-white border border-slate-200 rounded-lg shadow-sm">
        <h3 className="text-sm font-bold text-slate-900 mb-4 pb-2 border-b border-slate-100 flex items-center gap-2">
          <span className="w-6 h-6 rounded-full bg-slate-900 text-white text-xs flex items-center justify-center font-bold">{1}</span>
          {t("opening.createBatch")}
        </h3>

        <form onSubmit={(e) => void handleCreateBatch(e)} className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
          <div>
            <label htmlFor="open-batch-id" className="text-xs font-semibold text-slate-700 block mb-1">
              {t("opening.batchIdentity")} *
            </label>
            <input
              id="open-batch-id"
              type="text"
              required
              value={batchIdentity}
              disabled={Boolean(activeBatch)}
              onChange={(e) => setBatchIdentity(e.target.value)}
              className="w-full px-3 py-1.5 text-xs font-medium border border-slate-300 rounded bg-white text-slate-900 shadow-sm focus:border-slate-800 focus:outline-none disabled:bg-slate-100"
            />
          </div>

          <div>
            <label htmlFor="open-golive-date" className="text-xs font-semibold text-slate-700 block mb-1">
              {t("opening.goLiveDate")} *
            </label>
            <input
              id="open-golive-date"
              type="date"
              required
              value={goLiveDate}
              disabled={Boolean(activeBatch)}
              onChange={(e) => setGoLiveDate(e.target.value)}
              className="w-full px-3 py-1.5 text-xs font-medium border border-slate-300 rounded bg-white text-slate-900 shadow-sm focus:border-slate-800 focus:outline-none disabled:bg-slate-100"
            />
          </div>

          <div>
            <label htmlFor="open-base-currency" className="text-xs font-semibold text-slate-700 block mb-1">
              {t("opening.baseCurrency")} *
            </label>
            <input
              id="open-base-currency"
              type="text"
              required
              value={baseCurrency}
              disabled={Boolean(activeBatch)}
              onChange={(e) => setBaseCurrency(e.target.value.toUpperCase())}
              className="w-full px-3 py-1.5 text-xs font-medium border border-slate-300 rounded bg-white text-slate-900 shadow-sm focus:border-slate-800 focus:outline-none disabled:bg-slate-100"
            />
          </div>

          <div>
            <label htmlFor="open-ctrl-ref" className="text-xs font-semibold text-slate-700 block mb-1">
              {t("opening.controlConfig")}
            </label>
            <input
              id="open-ctrl-ref"
              type="text"
              value={controlRef}
              disabled={Boolean(activeBatch)}
              onChange={(e) => setControlRef(e.target.value)}
              className="w-full px-3 py-1.5 text-xs font-medium border border-slate-300 rounded bg-white text-slate-900 shadow-sm focus:border-slate-800 focus:outline-none disabled:bg-slate-100"
            />
          </div>

          {!activeBatch && (
            <div className="md:col-span-2 lg:col-span-4 flex justify-end">
              <Button
                variant="primary"
                type="submit"
                size="compact"
                isDisabled={isCreating || !batchIdentity}
                className="bg-slate-900 hover:bg-slate-800 text-white"
              >
                {isCreating ? t("opening.creating") : t("opening.createBatch")}
              </Button>
            </div>
          )}
        </form>
      </Surface>

      {/* Step 2 & 3: Batch Info, Validation, and Finalization */}
      {activeBatch && (
        <Surface variant="card" padding="normal" className="bg-white border border-slate-200 rounded-lg shadow-sm space-y-6">
          <div className="flex flex-wrap items-center justify-between pb-3 border-b border-slate-100">
            <div>
              <span className="text-xs font-bold text-slate-500 uppercase tracking-wider block">
                {t("opening.activeBatch")}
              </span>
              <p className="text-base font-extrabold text-slate-900 mt-0.5 font-mono">
                {activeBatch.batchIdentity}
              </p>
            </div>

            <div className="flex items-center gap-4">
              <div className="text-end">
                <span className="text-xs text-slate-500 block">{t("opening.lineCount")}</span>
                <span className="text-base font-bold text-slate-900">{activeBatch.lineCount}</span>
              </div>
              <div className="text-end">
                <span className="text-xs text-slate-500 block">{t("opening.batchStatus")}</span>
                <span className="inline-block px-2 py-0.5 text-xs font-bold rounded-full bg-slate-100 text-slate-800 border border-slate-300">
                  {activeBatch.status}
                </span>
              </div>
            </div>
          </div>

          {/* Validation section */}
          <div className="space-y-4">
            <h3 className="text-sm font-bold text-slate-900 flex items-center gap-2">
              <span className="w-6 h-6 rounded-full bg-slate-900 text-white text-xs flex items-center justify-center font-bold">{2}</span>
              {t("opening.validateGL")}
            </h3>

            <div className="flex items-center gap-3">
              <Button
                variant="secondary"
                size="compact"
                onClick={() => void handleValidateBatch()}
                isDisabled={isValidating || activeBatch.status === "Finalized"}
                className="border-slate-300 text-slate-800 hover:bg-slate-50"
              >
                {isValidating ? t("opening.validating") : t("opening.validateGL")}
              </Button>
              <span className="text-xs text-slate-500">
                {t("opening.validateExplain")}
              </span>
            </div>

            {/* Validation Feedback */}
            {validationResult && (
              <div
                className={`p-4 rounded-lg border-2 ${
                  validationResult.isBalanced
                    ? "bg-emerald-50 border-emerald-300 text-emerald-950"
                    : "bg-rose-50 border-rose-300 text-rose-950"
                }`}
              >
                <div className="flex items-center justify-between mb-3">
                  <h4 className="text-sm font-bold">
                    {validationResult.isBalanced ? t("opening.balancedSuccess") : t("opening.imbalanceWarning")}
                  </h4>
                  <span className="text-xs font-bold px-2 py-0.5 rounded bg-white/80 border">
                    {validationResult.lineCount} {t("common.lines")}
                  </span>
                </div>

                <div className="grid grid-cols-1 md:grid-cols-2 gap-3 text-xs">
                  {validationResult.directions.map((d) => (
                    <div key={d.direction} className="bg-white/80 p-3 rounded border border-slate-200">
                      <div className="flex justify-between font-bold mb-1">
                        <span>{d.direction === "Receivable" ? t("opening.customerAR") : t("opening.supplierAP")}</span>
                        <span className={d.isBalanced ? "text-emerald-700" : "text-rose-700"}>
                          {d.isBalanced ? t("opening.balanced") : t("opening.varianceExists")}
                        </span>
                      </div>
                      <div className="space-y-1 text-slate-700">
                        <div className="flex justify-between">
                          <span>{t("opening.subledger")}:</span>
                          <span className="font-semibold"><MoneyDisplay value={{ amount: d.subledgerBaseBalance, currency: baseCurrency }} /></span>
                        </div>
                        <div className="flex justify-between">
                          <span>{t("opening.generalLedger")}:</span>
                          <span className="font-semibold"><MoneyDisplay value={{ amount: d.ledgerBaseBalance, currency: baseCurrency }} /></span>
                        </div>
                        <div className="flex justify-between font-bold border-t pt-1">
                          <span>{t("opening.variance")}:</span>
                          <span className={d.isBalanced ? "text-emerald-700" : "text-rose-700"}>
                            <MoneyDisplay value={{ amount: d.variance, currency: baseCurrency }} />
                          </span>
                        </div>
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            )}
          </div>

          {/* Finalize step */}
          <div className="pt-4 border-t border-slate-200 space-y-4">
            <h3 className="text-sm font-bold text-slate-900 flex items-center gap-2">
              <span className="w-6 h-6 rounded-full bg-slate-900 text-white text-xs flex items-center justify-center font-bold">{3}</span>
              {t("opening.finalizeBatch")}
            </h3>

            <div className="p-3 bg-amber-50 border border-amber-300 rounded text-amber-900 text-xs">
              <strong>{t("opening.immutableNotice")}</strong>
            </div>

            <div className="flex items-center justify-end gap-3">
              <Button
                variant="primary"
                size="comfortable"
                onClick={() => void handleFinalizeBatch()}
                isDisabled={
                  isFinalizing ||
                  activeBatch.status === "Finalized" ||
                  (validationResult ? !validationResult.isBalanced : false)
                }
                className="bg-emerald-700 hover:bg-emerald-800 text-white shadow-sm font-bold"
              >
                {isFinalizing ? t("opening.finalizing") : t("opening.finalizeBatch")}
              </Button>
            </div>
          </div>
        </Surface>
      )}
    </div>
  );
}

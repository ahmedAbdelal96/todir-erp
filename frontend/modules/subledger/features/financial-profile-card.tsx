import React, { useState, useEffect } from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../components/pages/surface.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { Alert } from "../../../components/primitives/alert.tsx";
import { MoneyDisplay } from "../../../components/fields/money-display.tsx";
import {
  usePartyFinancialProfileQuery,
  useUpdateFinancialProfileMutation
} from "../hooks/subledger-queries.ts";

export interface FinancialProfileCardProps {
  readonly legalEntityId: string;
  readonly partyId?: string | undefined;
}

export function FinancialProfileCard({
  legalEntityId,
  partyId
}: FinancialProfileCardProps): React.JSX.Element {
  const { t } = useTranslation("subledger");

  const profileQuery = usePartyFinancialProfileQuery(legalEntityId, partyId);
  const updateMutation = useUpdateFinancialProfileMutation();

  const profile = profileQuery.data;

  const [creditLimit, setCreditLimit] = useState<string>("");
  const [currency, setCurrency] = useState<string>("EGP");
  const [receivableTerm, setReceivableTerm] = useState<string>("");
  const [payableTerm, setPayableTerm] = useState<string>("");
  const [receivablePolicy, setReceivablePolicy] = useState<string>("");
  const [payablePolicy, setPayablePolicy] = useState<string>("");

  const [isEditing, setIsEditing] = useState<boolean>(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);

  useEffect(() => {
    if (profile) {
      setCreditLimit(profile.creditLimitAmount || "");
      setCurrency(profile.creditLimitCurrency || "EGP");
      setReceivableTerm(profile.defaultReceivablePaymentTermId || "");
      setPayableTerm(profile.defaultPayablePaymentTermId || "");
      setReceivablePolicy(profile.receivablePolicyCode || "");
      setPayablePolicy(profile.payablePolicyCode || "");
    }
  }, [profile]);

  if (!partyId) {
    return (
      <Surface variant="card" padding="normal" className="bg-white border border-slate-200 rounded-lg shadow-sm text-center py-8">
        <p className="text-sm font-medium text-slate-600">{t("partyFinancial.selectPrompt")}</p>
      </Surface>
    );
  }

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!profile) return;
    setErrorMessage(null);
    setSuccessMessage(null);

    try {
      await updateMutation.mutateAsync({
        partyId,
        payload: {
          legalEntityId,
          creditLimitAmount: creditLimit || null,
          creditLimitCurrency: currency || null,
          defaultReceivablePaymentTermId: receivableTerm || null,
          defaultPayablePaymentTermId: payableTerm || null,
          receivablePolicyCode: receivablePolicy || null,
          payablePolicyCode: payablePolicy || null,
          expectedVersion: profile.version
        }
      });
      setSuccessMessage(t("profile.savedSuccess"));
      setIsEditing(false);
    } catch (err: unknown) {
      setErrorMessage(err instanceof Error ? err.message : t("profile.errorTitle"));
    }
  };

  const isSaving = updateMutation.isPending;

  return (
    <Surface variant="card" padding="normal" className="bg-white border border-slate-200 rounded-lg shadow-sm space-y-4">
      <div className="flex items-center justify-between pb-3 border-b border-slate-200">
        <div>
          <h3 className="text-sm font-bold text-slate-900">{t("profile.title")}</h3>
          <p className="text-xs text-slate-500 mt-0.5">
            {t("profile.subtitle")}
          </p>
        </div>
        {!isEditing && (
          <Button
            variant="secondary"
            size="compact"
            onClick={() => setIsEditing(true)}
            className="border-slate-300 text-slate-800 hover:bg-slate-50"
          >
            {t("profile.editPolicies")}
          </Button>
        )}
      </div>

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

      {isEditing ? (
        <form onSubmit={(e) => void handleSave(e)} className="space-y-4">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div>
              <label htmlFor="prof-credit-limit" className="text-xs font-semibold text-slate-700 block mb-1">
                {t("profile.creditLimit")}
              </label>
              <input
                id="prof-credit-limit"
                type="number"
                step="0.01"
                min="0"
                placeholder="0.00"
                value={creditLimit}
                onChange={(e) => setCreditLimit(e.target.value)}
                className="w-full px-3 py-1.5 text-xs font-medium border border-slate-300 rounded bg-white text-slate-900 shadow-sm focus:border-slate-800 focus:outline-none"
              />
            </div>

            <div>
              <label htmlFor="prof-currency" className="text-xs font-semibold text-slate-700 block mb-1">
                {t("profile.currency")}
              </label>
              <input
                id="prof-currency"
                type="text"
                placeholder={t("common.currencyPlaceholder")}
                value={currency}
                onChange={(e) => setCurrency(e.target.value.toUpperCase())}
                className="w-full px-3 py-1.5 text-xs font-medium border border-slate-300 rounded bg-white text-slate-900 shadow-sm focus:border-slate-800 focus:outline-none"
              />
            </div>

            <div>
              <label htmlFor="prof-rec-policy" className="text-xs font-semibold text-slate-700 block mb-1">
                {t("profile.receivablePolicy")}
              </label>
              <input
                id="prof-rec-policy"
                type="text"
                value={receivablePolicy}
                onChange={(e) => setReceivablePolicy(e.target.value)}
                className="w-full px-3 py-1.5 text-xs font-medium border border-slate-300 rounded bg-white text-slate-900 shadow-sm focus:border-slate-800 focus:outline-none"
              />
            </div>

            <div>
              <label htmlFor="prof-pay-policy" className="text-xs font-semibold text-slate-700 block mb-1">
                {t("profile.payablePolicy")}
              </label>
              <input
                id="prof-pay-policy"
                type="text"
                value={payablePolicy}
                onChange={(e) => setPayablePolicy(e.target.value)}
                className="w-full px-3 py-1.5 text-xs font-medium border border-slate-300 rounded bg-white text-slate-900 shadow-sm focus:border-slate-800 focus:outline-none"
              />
            </div>
          </div>

          <div className="flex items-center justify-end gap-3 pt-3 border-t border-slate-200">
            <Button
              variant="secondary"
              size="compact"
              type="button"
              onClick={() => setIsEditing(false)}
              isDisabled={isSaving}
              className="border-slate-300"
            >
              {t("common.cancel")}
            </Button>
            <Button
              variant="primary"
              size="compact"
              type="submit"
              isDisabled={isSaving}
              className="bg-slate-900 hover:bg-slate-800 text-white"
            >
              {isSaving ? t("profile.saving") : t("profile.saveProfile")}
            </Button>
          </div>
        </form>
      ) : (
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
          <div className="p-3 bg-slate-50 border border-slate-200 rounded-lg">
            <span className="text-[11px] font-bold text-slate-500 uppercase tracking-wider block">
              {t("profile.creditLimit")}
            </span>
            <p className="mt-1 text-base font-extrabold text-slate-900">
              {profile?.creditLimitAmount ? (
                <MoneyDisplay value={{ amount: profile.creditLimitAmount, currency: profile.creditLimitCurrency || "EGP" }} />
              ) : (
                t("common.unlimited")
              )}
            </p>
          </div>

          <div className="p-3 bg-slate-50 border border-slate-200 rounded-lg">
            <span className="text-[11px] font-bold text-slate-500 uppercase tracking-wider block">
              {t("profile.receivablePolicy")}
            </span>
            <p className="mt-1 text-base font-bold text-slate-800">
              {profile?.receivablePolicyCode || t("common.standard")}
            </p>
          </div>

          <div className="p-3 bg-slate-50 border border-slate-200 rounded-lg">
            <span className="text-[11px] font-bold text-slate-500 uppercase tracking-wider block">
              {t("profile.payablePolicy")}
            </span>
            <p className="mt-1 text-base font-bold text-slate-800">
              {profile?.payablePolicyCode || t("common.standard")}
            </p>
          </div>

          <div className="p-3 bg-slate-50 border border-slate-200 rounded-lg">
            <span className="text-[11px] font-bold text-slate-500 uppercase tracking-wider block">
              {t("profile.profileVersion")}
            </span>
            <p className="mt-1 text-base font-mono font-bold text-slate-800">
              #{profile?.version ?? 1}
            </p>
          </div>
        </div>
      )}
    </Surface>
  );
}

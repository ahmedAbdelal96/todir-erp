import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../components/pages/surface.tsx";
import { MoneyDisplay } from "../../../components/fields/money-display.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { usePartyAgingQuery } from "../hooks/subledger-queries.ts";
import type { SubledgerDirection } from "../types.ts";

export interface AgingReportViewProps {
  readonly legalEntityId: string;
  readonly partyId?: string | undefined;
  readonly defaultDirection?: SubledgerDirection | undefined;
}

export function AgingReportView({
  legalEntityId,
  partyId,
  defaultDirection = "Receivable"
}: AgingReportViewProps): React.JSX.Element {
  const { t } = useTranslation("subledger");
  const [direction, setDirection] = useState<SubledgerDirection>(defaultDirection);
  const [asOfDate, setAsOfDate] = useState<string>(() => new Date().toISOString().slice(0, 10));
  const [currencyCode, setCurrencyCode] = useState<string>("");

  const agingQuery = usePartyAgingQuery(
    legalEntityId,
    partyId,
    direction,
    asOfDate,
    currencyCode || undefined,
    Boolean(legalEntityId && partyId && asOfDate)
  );

  const buckets = agingQuery.data ?? [];
  const isLoading = agingQuery.isLoading;

  // Primary bucket (first currency or matched currency)
  const primaryBucket = buckets.length > 0 ? buckets[0] : null;

  const current = parseFloat(primaryBucket?.current || "0");
  const days1To30 = parseFloat(primaryBucket?.days1To30 || "0");
  const days31To60 = parseFloat(primaryBucket?.days31To60 || "0");
  const days61To90 = parseFloat(primaryBucket?.days61To90 || "0");
  const days91Plus = parseFloat(primaryBucket?.days91Plus || "0");
  const total = current + days1To30 + days31To60 + days61To90 + days91Plus;

  const overdueTotal = days1To30 + days31To60 + days61To90 + days91Plus;
  const overduePercent = total > 0 ? Math.min(100, Math.round((overdueTotal / total) * 100)) : 0;
  const primaryCurrency = primaryBucket?.currencyCode || "EGP";

  return (
    <div className="space-y-6">
      {/* Filter / Control Header */}
      <Surface variant="card" padding="normal" className="bg-white border border-slate-200 rounded-lg shadow-sm">
        <div className="flex flex-wrap items-center justify-between gap-4">
          <div className="flex flex-wrap items-center gap-4">
            {/* Direction Selector */}
            <div className="flex items-center space-x-1 rtl:space-x-reverse bg-slate-100 p-1 rounded-md border border-slate-300">
              <button
                type="button"
                onClick={() => setDirection("Receivable")}
                className={`px-3 py-1.5 text-xs font-semibold rounded ${
                  direction === "Receivable"
                    ? "bg-slate-900 text-white shadow-sm"
                    : "text-slate-700 hover:text-slate-900"
                }`}
              >
                {t("openItems.receivableOnly")}
              </button>
              <button
                type="button"
                onClick={() => setDirection("Payable")}
                className={`px-3 py-1.5 text-xs font-semibold rounded ${
                  direction === "Payable"
                    ? "bg-slate-900 text-white shadow-sm"
                    : "text-slate-700 hover:text-slate-900"
                }`}
              >
                {t("openItems.payableOnly")}
              </button>
            </div>

            {/* As Of Date Picker */}
            <div className="flex items-center gap-2">
              <label htmlFor="aging-as-of-date" className="text-xs font-semibold text-slate-700">
                {t("aging.asOfDate")}:
              </label>
              <input
                id="aging-as-of-date"
                type="date"
                value={asOfDate}
                onChange={(e) => setAsOfDate(e.target.value)}
                className="px-3 py-1.5 text-xs font-medium border border-slate-300 rounded bg-white text-slate-900 shadow-sm focus:border-slate-800 focus:outline-none"
              />
            </div>

            {/* Currency Filter */}
            <div className="flex items-center gap-2">
              <label htmlFor="aging-currency-filter" className="text-xs font-semibold text-slate-700">
                {t("aging.currencyBasis")}:
              </label>
              <input
                id="aging-currency-filter"
                type="text"
                placeholder={t("common.currencyPlaceholder")}
                value={currencyCode}
                onChange={(e) => setCurrencyCode(e.target.value.toUpperCase())}
                className="w-24 px-3 py-1.5 text-xs font-medium border border-slate-300 rounded bg-white text-slate-900 shadow-sm focus:border-slate-800 focus:outline-none"
              />
            </div>
          </div>

          <Button
            variant="secondary"
            size="compact"
            onClick={() => void agingQuery.refetch()}
            isDisabled={isLoading}
            className="border-slate-300 text-slate-700 hover:bg-slate-50"
          >
            {t("common.refresh")}
          </Button>
        </div>
      </Surface>

      {/* High-Level Overview Metrics */}
      {!partyId ? (
        <Surface variant="card" padding="normal" className="bg-white border border-slate-200 rounded-lg shadow-sm text-center py-10">
          <p className="text-sm font-medium text-slate-600">{t("partyFinancial.selectPrompt")}</p>
        </Surface>
      ) : isLoading ? (
        <Surface variant="card" padding="normal" className="bg-white border border-slate-200 rounded-lg shadow-sm text-center py-10">
          <p className="text-sm font-medium text-slate-500">{t("aging.loadingText")}</p>
        </Surface>
      ) : (
        <>
          {/* Visual Overdue Bar */}
          <Surface variant="card" padding="normal" className="bg-white border border-slate-200 rounded-lg shadow-sm space-y-3">
            <div className="flex items-center justify-between text-xs font-semibold text-slate-700">
              <span>{t("aging.ratio")}: {overduePercent}%</span>
              <span>
                {t("aging.totalBalance")}: <MoneyDisplay value={{ amount: total.toString(), currency: primaryCurrency }} />
              </span>
            </div>
            <div className="h-3 w-full bg-slate-100 rounded-full overflow-hidden flex border border-slate-200">
              <div
                style={{ width: `${total > 0 ? (current / total) * 100 : 100}%` }}
                className="bg-emerald-500 h-full transition-all"
                title={`${t("aging.current")}: ${current}`}
              />
              <div
                style={{ width: `${total > 0 ? (days1To30 / total) * 100 : 0}%` }}
                className="bg-amber-400 h-full transition-all"
                title={`${t("aging.days1To30")}: ${days1To30}`}
              />
              <div
                style={{ width: `${total > 0 ? (days31To60 / total) * 100 : 0}%` }}
                className="bg-orange-500 h-full transition-all"
                title={`${t("aging.days31To60")}: ${days31To60}`}
              />
              <div
                style={{ width: `${total > 0 ? (days61To90 / total) * 100 : 0}%` }}
                className="bg-rose-500 h-full transition-all"
                title={`${t("aging.days61To90")}: ${days61To90}`}
              />
              <div
                style={{ width: `${total > 0 ? (days91Plus / total) * 100 : 0}%` }}
                className="bg-rose-800 h-full transition-all"
                title={`${t("aging.days91Plus")}: ${days91Plus}`}
              />
            </div>
          </Surface>

          {/* Aging Buckets Cards Grid */}
          <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-3">
            {/* Current */}
            <div className="bg-white border-2 border-emerald-300 rounded-lg p-3 shadow-sm hover:border-emerald-500 transition-colors">
              <span className="text-[11px] font-bold uppercase tracking-wider text-emerald-800 block">
                {t("aging.current")}
              </span>
              <p className="mt-2 text-base font-extrabold text-slate-900">
                <MoneyDisplay value={{ amount: primaryBucket?.current || "0", currency: primaryCurrency }} />
              </p>
              <span className="text-[10px] text-slate-500 mt-1 block">{t("aging.notOverdue")}</span>
            </div>

            {/* 1 - 30 Days */}
            <div className="bg-white border border-amber-300 rounded-lg p-3 shadow-sm hover:border-amber-500 transition-colors">
              <span className="text-[11px] font-bold uppercase tracking-wider text-amber-800 block">
                {t("aging.days1To30")}
              </span>
              <p className="mt-2 text-base font-extrabold text-slate-900">
                <MoneyDisplay value={{ amount: primaryBucket?.days1To30 || "0", currency: primaryCurrency }} />
              </p>
              <span className="text-[10px] text-slate-500 mt-1 block">{t("aging.days1To30")}</span>
            </div>

            {/* 31 - 60 Days */}
            <div className="bg-white border border-orange-300 rounded-lg p-3 shadow-sm hover:border-orange-500 transition-colors">
              <span className="text-[11px] font-bold uppercase tracking-wider text-orange-800 block">
                {t("aging.days31To60")}
              </span>
              <p className="mt-2 text-base font-extrabold text-slate-900">
                <MoneyDisplay value={{ amount: primaryBucket?.days31To60 || "0", currency: primaryCurrency }} />
              </p>
              <span className="text-[10px] text-slate-500 mt-1 block">{t("aging.days31To60")}</span>
            </div>

            {/* 61 - 90 Days */}
            <div className="bg-white border border-rose-300 rounded-lg p-3 shadow-sm hover:border-rose-500 transition-colors">
              <span className="text-[11px] font-bold uppercase tracking-wider text-rose-800 block">
                {t("aging.days61To90")}
              </span>
              <p className="mt-2 text-base font-extrabold text-slate-900">
                <MoneyDisplay value={{ amount: primaryBucket?.days61To90 || "0", currency: primaryCurrency }} />
              </p>
              <span className="text-[10px] text-slate-500 mt-1 block">{t("aging.days61To90")}</span>
            </div>

            {/* 91+ Days */}
            <div className="bg-white border-2 border-rose-400 bg-rose-50/20 rounded-lg p-3 shadow-sm hover:border-rose-600 transition-colors">
              <span className="text-[11px] font-bold uppercase tracking-wider text-rose-900 block">
                {t("aging.days91Plus")}
              </span>
              <p className="mt-2 text-base font-extrabold text-rose-900">
                <MoneyDisplay value={{ amount: primaryBucket?.days91Plus || "0", currency: primaryCurrency }} />
              </p>
              <span className="text-[10px] text-rose-600 font-semibold mt-1 block">{t("aging.days91Plus")}</span>
            </div>

            {/* Total Due */}
            <div className="bg-slate-900 text-white rounded-lg p-3 shadow-md border border-slate-950">
              <span className="text-[11px] font-bold uppercase tracking-wider text-slate-300 block">
                {t("aging.totalDue")}
              </span>
              <p className="mt-2 text-base font-extrabold text-white">
                <MoneyDisplay value={{ amount: total.toString(), currency: primaryCurrency }} />
              </p>
              <span className="text-[10px] text-slate-400 mt-1 block">{t("aging.totalDue")}</span>
            </div>
          </div>

          {/* Breakdown Table */}
          <Surface variant="card" padding="none" className="bg-white border border-slate-200 rounded-lg shadow-sm overflow-hidden">
            <div className="px-5 py-4 border-b border-slate-200 bg-slate-50 flex items-center justify-between">
              <h3 className="text-sm font-bold text-slate-900">{t("aging.tableTitle")}</h3>
              <span className="text-xs text-slate-500">{t("aging.asOf")}: {asOfDate}</span>
            </div>
            <div className="overflow-x-auto">
              <table className="w-full text-xs text-start">
                <thead className="bg-slate-100 text-slate-700 font-semibold border-b border-slate-200">
                  <tr>
                    <th className="px-4 py-3 text-start">{t("openItems.currency")}</th>
                    <th className="px-4 py-3 text-start">{t("aging.direction")}</th>
                    <th className="px-4 py-3 text-end">{t("aging.current")}</th>
                    <th className="px-4 py-3 text-end">{t("aging.days1To30")}</th>
                    <th className="px-4 py-3 text-end">{t("aging.days31To60")}</th>
                    <th className="px-4 py-3 text-end">{t("aging.days61To90")}</th>
                    <th className="px-4 py-3 text-end">{t("aging.days91Plus")}</th>
                    <th className="px-4 py-3 text-end font-bold">{t("aging.totalDue")}</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-200 text-slate-900 font-medium">
                  {buckets.map((b) => {
                    const rowCurrent = parseFloat(b.current || "0");
                    const row1to30 = parseFloat(b.days1To30 || "0");
                    const row31to60 = parseFloat(b.days31To60 || "0");
                    const row61to90 = parseFloat(b.days61To90 || "0");
                    const row91plus = parseFloat(b.days91Plus || "0");
                    const rowTotal = rowCurrent + row1to30 + row31to60 + row61to90 + row91plus;
                    return (
                      <tr key={b.currencyCode} className="hover:bg-slate-50">
                        <td className="px-4 py-3 font-bold font-mono text-slate-800">
                          {b.currencyCode}
                        </td>
                        <td className="px-4 py-3 font-semibold text-slate-800">
                          {direction === "Receivable" ? t("openItems.receivableOnly") : t("openItems.payableOnly")}
                        </td>
                        <td className="px-4 py-3 text-end text-emerald-700 font-semibold">
                          <MoneyDisplay value={{ amount: b.current, currency: b.currencyCode }} />
                        </td>
                        <td className="px-4 py-3 text-end text-amber-700">
                          <MoneyDisplay value={{ amount: b.days1To30, currency: b.currencyCode }} />
                        </td>
                        <td className="px-4 py-3 text-end text-orange-700">
                          <MoneyDisplay value={{ amount: b.days31To60, currency: b.currencyCode }} />
                        </td>
                        <td className="px-4 py-3 text-end text-rose-700">
                          <MoneyDisplay value={{ amount: b.days61To90, currency: b.currencyCode }} />
                        </td>
                        <td className="px-4 py-3 text-end text-rose-900 font-bold">
                          <MoneyDisplay value={{ amount: b.days91Plus, currency: b.currencyCode }} />
                        </td>
                        <td className="px-4 py-3 text-end font-bold text-slate-900 bg-slate-50">
                          <MoneyDisplay value={{ amount: rowTotal.toString(), currency: b.currencyCode }} />
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          </Surface>
        </>
      )}
    </div>
  );
}

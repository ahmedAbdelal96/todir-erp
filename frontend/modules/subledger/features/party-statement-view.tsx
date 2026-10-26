import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../components/pages/surface.tsx";
import { MoneyDisplay } from "../../../components/fields/money-display.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { usePartyStatementQuery } from "../hooks/subledger-queries.ts";
import type { SubledgerDirection } from "../types.ts";

export interface PartyStatementViewProps {
  readonly legalEntityId: string;
  readonly partyId?: string | undefined;
  readonly defaultDirection?: SubledgerDirection | undefined;
}

export function PartyStatementView({
  legalEntityId,
  partyId,
  defaultDirection = "Receivable"
}: PartyStatementViewProps): React.JSX.Element {
  const { t } = useTranslation("subledger");
  const [direction, setDirection] = useState<SubledgerDirection>(defaultDirection);

  const [fromDate, setFromDate] = useState<string>(() => {
    const d = new Date();
    d.setMonth(d.getMonth() - 1);
    return d.toISOString().slice(0, 10);
  });
  const [toDate, setToDate] = useState<string>(() => new Date().toISOString().slice(0, 10));
  const [currencyCode, setCurrencyCode] = useState<string>("");
  const [page, setPage] = useState<number>(1);

  const statementQuery = usePartyStatementQuery(
    legalEntityId,
    partyId,
    direction,
    fromDate,
    toDate,
    currencyCode || undefined,
    page,
    200,
    Boolean(legalEntityId && partyId && fromDate && toDate)
  );

  const statement = statementQuery.data;
  const isLoading = statementQuery.isLoading;
  const lines = statement?.lines ?? [];

  // Primary closing balance
  const primaryCurrency = currencyCode || (lines.length > 0 && lines[0] ? lines[0].currencyCode : "EGP");
  const closingBalance = statement?.closingBalances?.[primaryCurrency] || "0";

  return (
    <div className="space-y-6">
      {/* Filters & Date Selection */}
      <Surface variant="card" padding="normal" className="bg-white border border-slate-200 rounded-lg shadow-sm">
        <div className="flex flex-wrap items-center justify-between gap-4">
          <div className="flex flex-wrap items-center gap-4">
            {/* Direction Selector */}
            <div className="flex items-center space-x-1 rtl:space-x-reverse bg-slate-100 p-1 rounded-md border border-slate-300">
              <button
                type="button"
                onClick={() => {
                  setDirection("Receivable");
                  setPage(1);
                }}
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
                onClick={() => {
                  setDirection("Payable");
                  setPage(1);
                }}
                className={`px-3 py-1.5 text-xs font-semibold rounded ${
                  direction === "Payable"
                    ? "bg-slate-900 text-white shadow-sm"
                    : "text-slate-700 hover:text-slate-900"
                }`}
              >
                {t("openItems.payableOnly")}
              </button>
            </div>

            {/* Date Range */}
            <div className="flex items-center gap-2">
              <label htmlFor="stmt-from-date" className="text-xs font-semibold text-slate-700">
                {t("statement.fromDate")}:
              </label>
              <input
                id="stmt-from-date"
                type="date"
                value={fromDate}
                onChange={(e) => {
                  setFromDate(e.target.value);
                  setPage(1);
                }}
                className="px-3 py-1.5 text-xs font-medium border border-slate-300 rounded bg-white text-slate-900 shadow-sm focus:border-slate-800 focus:outline-none"
              />
            </div>

            <div className="flex items-center gap-2">
              <label htmlFor="stmt-to-date" className="text-xs font-semibold text-slate-700">
                {t("statement.toDate")}:
              </label>
              <input
                id="stmt-to-date"
                type="date"
                value={toDate}
                onChange={(e) => {
                  setToDate(e.target.value);
                  setPage(1);
                }}
                className="px-3 py-1.5 text-xs font-medium border border-slate-300 rounded bg-white text-slate-900 shadow-sm focus:border-slate-800 focus:outline-none"
              />
            </div>

            {/* Currency Filter */}
            <div className="flex items-center gap-2">
              <label htmlFor="stmt-curr-filter" className="text-xs font-semibold text-slate-700">
                {t("openItems.currency")}:
              </label>
              <input
                id="stmt-curr-filter"
                type="text"
                placeholder={t("common.currencyPlaceholder")}
                value={currencyCode}
                onChange={(e) => {
                  setCurrencyCode(e.target.value.toUpperCase());
                  setPage(1);
                }}
                className="w-24 px-3 py-1.5 text-xs font-medium border border-slate-300 rounded bg-white text-slate-900 shadow-sm focus:border-slate-800 focus:outline-none"
              />
            </div>
          </div>

          <Button
            variant="secondary"
            size="compact"
            onClick={() => void statementQuery.refetch()}
            isDisabled={isLoading}
            className="border-slate-300 text-slate-700 hover:bg-slate-50"
          >
            {t("common.refresh")}
          </Button>
        </div>
      </Surface>

      {/* State checks */}
      {!partyId ? (
        <Surface variant="card" padding="normal" className="bg-white border border-slate-200 rounded-lg shadow-sm text-center py-10">
          <p className="text-sm font-medium text-slate-600">{t("partyFinancial.selectPrompt")}</p>
        </Surface>
      ) : isLoading ? (
        <Surface variant="card" padding="normal" className="bg-white border border-slate-200 rounded-lg shadow-sm text-center py-10">
          <p className="text-sm font-medium text-slate-500">{t("statement.loading")}</p>
        </Surface>
      ) : (
        <>
          {/* Closing Balances Card */}
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <Surface variant="card" padding="normal" className="bg-white border border-slate-200 rounded-lg shadow-sm">
              <span className="text-xs font-bold text-slate-500 uppercase tracking-wider block">
                {t("statement.closingBalance")}
              </span>
              <p className="mt-2 text-2xl font-black text-slate-900">
                <MoneyDisplay value={{ amount: closingBalance, currency: primaryCurrency }} />
              </p>
              <p className="mt-1 text-xs text-slate-500">
                {t("statement.closingBalanceOn")} {toDate}
              </p>
            </Surface>

            <Surface variant="card" padding="normal" className="bg-white border border-slate-200 rounded-lg shadow-sm">
              <span className="text-xs font-bold text-slate-500 uppercase tracking-wider block">
                {t("statement.transactionsCount")}
              </span>
              <p className="mt-2 text-2xl font-black text-slate-900">
                {lines.length}
              </p>
              <p className="mt-1 text-xs text-slate-500">
                {t("statement.from")} {fromDate} {t("statement.to")} {toDate}
              </p>
            </Surface>
          </div>

          {/* Statement Lines Table */}
          <Surface variant="card" padding="none" className="bg-white border border-slate-200 rounded-lg shadow-sm overflow-hidden">
            <div className="px-5 py-4 border-b border-slate-200 bg-slate-50 flex items-center justify-between">
              <div>
                <h3 className="text-sm font-bold text-slate-900">{t("statement.title")}</h3>
                <p className="text-xs text-slate-500 mt-0.5">{t("statement.subtitle")}</p>
              </div>
            </div>

            {lines.length === 0 ? (
              <div className="p-8 text-center text-slate-500 text-sm">
                {t("statement.empty")}
              </div>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full text-xs text-start">
                  <thead className="bg-slate-100 text-slate-700 font-semibold border-b border-slate-200">
                    <tr>
                      <th className="px-4 py-3 text-start">{t("statement.date")}</th>
                      <th className="px-4 py-3 text-start">{t("statement.sourceType")}</th>
                      <th className="px-4 py-3 text-start">{t("statement.reference")}</th>
                      <th className="px-4 py-3 text-end">{t("statement.amount")}</th>
                      <th className="px-4 py-3 text-end font-bold">{t("statement.runningBalance")}</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-200 text-slate-900 font-medium">
                    {lines.map((line, idx) => {
                      const amtNum = parseFloat(line.amount || "0");
                      const isNegative = amtNum < 0;
                      return (
                        <tr key={`${line.sourceId}-${idx}`} className="hover:bg-slate-50">
                          <td className="px-4 py-3 text-slate-700 whitespace-nowrap">
                            {line.postingDate || line.effectiveDate}
                          </td>
                          <td className="px-4 py-3 text-slate-800 font-semibold">
                            {line.sourceType}
                          </td>
                          <td className="px-4 py-3 font-mono text-slate-700">
                            {line.sourceReference || "—"}
                          </td>
                          <td className={`px-4 py-3 text-end font-semibold ${isNegative ? "text-emerald-700" : "text-slate-900"}`}>
                            <MoneyDisplay value={{ amount: line.amount, currency: line.currencyCode }} />
                          </td>
                          <td className="px-4 py-3 text-end font-black text-slate-900 bg-slate-50/50">
                            <MoneyDisplay value={{ amount: line.runningBalance, currency: line.currencyCode }} />
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            )}
          </Surface>
        </>
      )}
    </div>
  );
}

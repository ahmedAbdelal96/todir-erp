import React, { useState } from "react";
import {
  useFinancialStatement,
  useStatementDefinition,
  useSaveStatementDefinitionMutation
} from "../../hooks/use-accounting-queries.ts";
import { formatCurrency } from "../../utils/currency.ts";
import { useAccountingI18n } from "../../i18n";
import type { StatementKind, StatementLineView } from "../../types.ts";

export const FinancialStatementsView: React.FC = () => {
  const { t, language } = useAccountingI18n();

  const [statementKind, setStatementKind] = useState<StatementKind>("ProfitAndLoss");
  const [activeTab, setActiveTab] = useState<"statement" | "definition">("statement");

  const [fromDate, setFromDate] = useState<string>(() => `${new Date().getFullYear()}-01-01`);
  const [toDate, setToDate] = useState<string>(() => new Date().toISOString().slice(0, 10));

  // Query Statement
  const {
    data: statement,
    isLoading: isStatementLoading,
    isError: isStatementError,
    refetch: refetchStatement
  } = useFinancialStatement(statementKind, fromDate, toDate);

  // Query Definition
  const {
    data: definition,
    isLoading: isDefLoading,
    refetch: refetchDefinition
  } = useStatementDefinition(statementKind);

  return (
    <div className="space-y-6" data-testid="financial-statements-view">
      {/* Top Header & Statement Selector */}
      <div className="bg-white p-5 rounded-lg border border-slate-200 shadow-sm flex flex-wrap items-center justify-between gap-4">
        <div>
          <h2 className="text-lg font-bold text-slate-800">{t("statements.title")}</h2>
          <p className="text-sm text-slate-500 mt-1">{t("statements.subtitle")}</p>
        </div>

        {/* Kind Buttons */}
        <div className="flex items-center gap-2 bg-slate-100 p-1 rounded-lg">
          <button
            type="button"
            onClick={() => setStatementKind("ProfitAndLoss")}
            className={`px-4 py-1.5 rounded-md text-sm font-semibold transition ${
              statementKind === "ProfitAndLoss"
                ? "bg-white text-blue-700 shadow-sm"
                : "text-slate-600 hover:text-slate-900"
            }`}
            data-testid="tab-pnl"
          >
            {t("statements.tabPnl")}
          </button>
          <button
            type="button"
            onClick={() => setStatementKind("BalanceSheet")}
            className={`px-4 py-1.5 rounded-md text-sm font-semibold transition ${
              statementKind === "BalanceSheet"
                ? "bg-white text-blue-700 shadow-sm"
                : "text-slate-600 hover:text-slate-900"
            }`}
            data-testid="tab-balance-sheet"
          >
            {t("statements.tabBalanceSheet")}
          </button>
        </div>
      </div>

      {/* Sub-Tabs: Statement View vs. Definitions */}
      <div className="flex items-center justify-between border-b border-slate-200">
        <div className="flex gap-4">
          <button
            type="button"
            onClick={() => setActiveTab("statement")}
            className={`pb-3 text-sm font-bold border-b-2 transition ${
              activeTab === "statement"
                ? "border-blue-600 text-blue-600"
                : "border-transparent text-slate-500 hover:text-slate-800"
            }`}
          >
            {t("statements.tabReport")}
          </button>
          <button
            type="button"
            onClick={() => setActiveTab("definition")}
            className={`pb-3 text-sm font-bold border-b-2 transition ${
              activeTab === "definition"
                ? "border-blue-600 text-blue-600"
                : "border-transparent text-slate-500 hover:text-slate-800"
            }`}
          >
            {t("statements.tabDefinitions")}
          </button>
        </div>

        {/* Date Filter (for report view) */}
        {activeTab === "statement" && (
          <div className="flex items-center gap-3 pb-3">
            <label htmlFor="statement-from-date" className="text-xs font-semibold text-slate-600">
              {t("statements.from")}:
            </label>
            <input
              id="statement-from-date"
              type="date"
              value={fromDate}
              onChange={e => setFromDate(e.target.value)}
              className="text-xs px-2.5 py-1.5 border border-slate-300 rounded focus:ring-1 focus:ring-blue-500"
            />
            <label htmlFor="statement-to-date" className="text-xs font-semibold text-slate-600">
              {t("statements.to")}:
            </label>
            <input
              id="statement-to-date"
              type="date"
              value={toDate}
              onChange={e => setToDate(e.target.value)}
              className="text-xs px-2.5 py-1.5 border border-slate-300 rounded focus:ring-1 focus:ring-blue-500"
            />
            <button
              type="button"
              onClick={() => void refetchStatement()}
              className="px-3 py-1.5 bg-slate-100 hover:bg-slate-200 text-slate-700 text-xs font-semibold rounded transition"
            >
              {t("statements.refresh")}
            </button>
          </div>
        )}
      </div>

      {/* REPORT VIEW */}
      {activeTab === "statement" && (
        <div className="space-y-4">
          {isStatementLoading && (
            <div className="bg-white p-8 rounded-lg border border-slate-200 text-center text-slate-500">
              <p className="animate-pulse">{t("statements.loading")}</p>
            </div>
          )}

          {isStatementError && (
            <div className="bg-rose-50 border border-rose-200 p-4 rounded-lg text-rose-700 text-sm">
              {t("statements.errorLoading")}
            </div>
          )}

          {statement && (
            <>
              {/* Balance Sheet Balanced Indicator */}
              {statementKind === "BalanceSheet" && (
                <div
                  className={`p-4 rounded-lg border flex items-center justify-between ${
                    statement.isBalanced
                      ? "bg-emerald-50 border-emerald-300 text-emerald-800"
                      : "bg-rose-50 border-rose-300 text-rose-800"
                  }`}
                  data-testid="balance-sheet-integrity-badge"
                >
                  <div className="flex items-center gap-2">
                    <span
                      className={`w-3 h-3 rounded-full ${
                        statement.isBalanced ? "bg-emerald-500" : "bg-rose-500"
                      }`}
                    />
                    <span className="font-bold text-sm">
                      {statement.isBalanced
                        ? t("statements.balancedMessage")
                        : t("statements.unbalancedMessage")}
                    </span>
                  </div>
                  {statement.diagnostic && (
                    <div className="text-xs text-rose-700">{statement.diagnostic}</div>
                  )}
                </div>
              )}

              {/* Statement Statement Card & Lines Table */}
              <div className="bg-white rounded-lg border border-slate-200 shadow-sm overflow-hidden">
                <div className="p-5 border-b border-slate-200 flex items-center justify-between bg-slate-50/50">
                  <div>
                    <h3 className="text-base font-bold text-slate-800">
                      {statementKind === "ProfitAndLoss"
                        ? t("statements.pnlHeader")
                        : t("statements.balanceSheetHeader")}
                    </h3>
                    <p className="text-xs text-slate-500 mt-0.5">
                      {statement.fromDate} → {statement.toDate} ({statement.currencyCode})
                    </p>
                  </div>
                  <div className="text-end">
                    <span className="text-xs text-slate-500 uppercase tracking-wider block">
                      {statementKind === "ProfitAndLoss"
                        ? t("statements.netProfitTotal")
                        : t("statements.balanceTotal")}
                    </span>
                    <span
                      className={`text-xl font-bold ${
                        statement.total >= 0 ? "text-slate-800" : "text-rose-600"
                      }`}
                    >
                      {formatCurrency(statement.total, statement.currencyCode)}
                    </span>
                  </div>
                </div>

                <div className="overflow-x-auto">
                  <table className="w-full text-start text-sm">
                    <thead>
                      <tr className="bg-slate-50 text-slate-600 font-semibold border-b border-slate-200">
                        <th className="py-3 px-4 text-start">{t("statements.colCode")}</th>
                        <th className="py-3 px-4 text-start">{t("statements.colLineName")}</th>
                        <th className="py-3 px-4 text-start">{t("statements.colAccountType")}</th>
                        <th className="py-3 px-4 text-start">{t("statements.colLineKind")}</th>
                        <th className="py-3 px-4 text-end">{t("statements.colAmount")}</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-slate-200">
                      {statement.lines.map((line: StatementLineView) => (
                        <tr
                          key={line.code}
                          className={`hover:bg-slate-50/60 transition ${
                            line.lineKind === "Arithmetic" || line.code === "net-profit"
                              ? "bg-slate-50/80 font-bold"
                              : ""
                          }`}
                          data-testid={`statement-line-${line.code}`}
                        >
                          <td className="py-3 px-4 text-slate-600 font-mono text-xs">{line.code}</td>
                          <td className="py-3 px-4 text-slate-800">
                            {language === "ar"
                              ? line.nameAr || line.name
                              : line.nameEn || line.name}
                          </td>
                          <td className="py-3 px-4 text-slate-500 text-xs">{line.accountType}</td>
                          <td className="py-3 px-4 text-slate-500 text-xs">
                            <span className="px-2 py-0.5 rounded bg-slate-100 text-slate-600 border border-slate-200">
                              {line.lineKind}
                            </span>
                          </td>
                          <td
                            className={`py-3 px-4 text-end font-semibold ${
                              line.amount >= 0 ? "text-slate-800" : "text-rose-600"
                            }`}
                          >
                            {formatCurrency(line.amount, statement.currencyCode)}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
            </>
          )}
        </div>
      )}

      {/* DEFINITION VIEW */}
      {activeTab === "definition" && (
        <div className="bg-white rounded-lg border border-slate-200 shadow-sm p-6 space-y-4">
          <div className="flex items-center justify-between border-b border-slate-200 pb-4">
            <div>
              <h3 className="text-base font-bold text-slate-800">{t("statements.defTitle")}</h3>
              <p className="text-xs text-slate-500 mt-1">{t("statements.defSubtitle")}</p>
            </div>
            {definition && (
              <div className="text-xs text-slate-600">
                <span className="font-semibold">{t("statements.defVersion")}:</span> {definition.version} (
                <span className="text-emerald-600 font-semibold">{definition.status}</span>)
              </div>
            )}
          </div>

          {isDefLoading && (
            <div className="p-8 text-center text-slate-400 text-sm animate-pulse">
              {t("statements.loading")}
            </div>
          )}

          {definition && (
            <div className="overflow-x-auto">
              <table className="w-full text-start text-sm">
                <thead>
                  <tr className="bg-slate-50 text-slate-600 font-semibold border-b border-slate-200">
                    <th className="py-3 px-4 text-start">{t("statements.defColSeq")}</th>
                    <th className="py-3 px-4 text-start">{t("statements.defColCode")}</th>
                    <th className="py-3 px-4 text-start">{t("statements.defColNameAr")}</th>
                    <th className="py-3 px-4 text-start">{t("statements.defColNameEn")}</th>
                    <th className="py-3 px-4 text-start">{t("statements.defColAccountType")}</th>
                    <th className="py-3 px-4 text-start">{t("statements.defColKind")}</th>
                    <th className="py-3 px-4 text-start">{t("statements.defColFormula")}</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-200">
                  {definition.lines.map(line => (
                    <tr key={line.code} className="hover:bg-slate-50/50">
                      <td className="py-3 px-4 text-slate-500 text-xs font-mono">{line.sequence}</td>
                      <td className="py-3 px-4 text-slate-700 font-mono text-xs">{line.code}</td>
                      <td className="py-3 px-4 text-slate-800">{line.nameAr}</td>
                      <td className="py-3 px-4 text-slate-800">{line.nameEn}</td>
                      <td className="py-3 px-4 text-slate-500 text-xs">{line.accountType || "—"}</td>
                      <td className="py-3 px-4 text-slate-500 text-xs">{line.lineKind}</td>
                      <td className="py-3 px-4 text-slate-500 text-xs">
                        {line.formulaLineCodes && line.formulaLineCodes.length > 0
                          ? `${line.arithmeticOperator ?? "Op"}(${line.formulaLineCodes.join(", ")})`
                          : "—"}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      )}
    </div>
  );
};

import React, { useState } from "react";
import type { TFunction } from "i18next";
import {
  useFinancialStatement,
  useStatementDefinition,
  useSaveStatementDefinitionMutation
} from "../../hooks/use-accounting-queries.ts";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { StatusBadge } from "../../../../components/fields/status-badge.tsx";
import { MoneyDisplay } from "../../../../components/fields/money-display.tsx";
import { useAccountingI18n } from "../../i18n/index.ts";
import type { StatementKind, StatementLineView } from "../../types.ts";
import { formatAccountType } from "../../utils/diagnostic-translations.ts";

export interface FinancialStatementsViewProps {
  readonly legalEntityId?: string;
}

function getLineLabel(line: StatementLineView, lang: string, translate: TFunction<"accounting">): string {
  const raw = lang === "ar" ? line.nameAr || line.name : line.nameEn || line.name;
  if (!raw || raw.includes("?")) {
    switch (line.code) {
      case "asset":
        return translate("statements.lineAsset");
      case "liability":
        return translate("statements.lineLiability");
      case "equity":
        return translate("statements.lineEquity");
      case "revenue":
        return translate("statements.lineRevenue");
      case "expense":
        return translate("statements.lineExpense");
      case "net-profit":
        return translate("statements.lineNetProfit");
      default:
        return line.code;
    }
  }
  return raw;
}

export const FinancialStatementsView: React.FC<FinancialStatementsViewProps> = ({ legalEntityId }) => {
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
  } = useFinancialStatement(statementKind, fromDate, toDate, legalEntityId);

  // Query Definition
  const {
    data: definition,
    isLoading: isDefLoading,
    refetch: refetchDefinition
  } = useStatementDefinition(statementKind, legalEntityId);

  const navTabBtnStyle = (active: boolean): React.CSSProperties => ({
    padding: "0.5rem 1rem",
    borderRadius: "var(--radius-md, 0.5rem)",
    border: "1px solid var(--color-border-default, #cbd5e1)",
    fontSize: "0.875rem",
    fontWeight: 600,
    cursor: "pointer",
    backgroundColor: active ? "var(--color-brand-primary, #102a43)" : "var(--color-surface-card, #ffffff)",
    color: active ? "#ffffff" : "var(--color-content-primary, #0f172a)",
    transition: "background-color 150ms ease, color 150ms ease"
  });

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem", width: "100%", minWidth: 0 }} data-testid="financial-statements-view">
      {/* Top Header Card & Selectors */}
      <Surface variant="card" style={{ padding: "1.25rem 1.5rem" }}>
        <div
          style={{
            display: "flex",
            alignItems: "center",
            justifyContent: "space-between",
            flexWrap: "wrap",
            gap: "1rem",
            marginBottom: "1rem"
          }}
        >
          <div>
            <h2 style={{ margin: 0, fontSize: "1.25rem", fontWeight: 700, color: "var(--color-content-primary, #0f172a)" }}>
              {t("statements.title")}
            </h2>
            <p style={{ margin: "0.25rem 0 0", fontSize: "0.875rem", color: "var(--color-content-muted, #64748b)" }}>
              {t("statements.subtitle")}
            </p>
          </div>

          {/* Statement Kind Toggle Buttons */}
          <div style={{ display: "flex", gap: "0.5rem", flexWrap: "wrap" }}>
            <button
              type="button"
              style={navTabBtnStyle(statementKind === "ProfitAndLoss")}
              onClick={() => setStatementKind("ProfitAndLoss")}
            >
              {t("statements.tabPnl")}
            </button>
            <button
              type="button"
              style={navTabBtnStyle(statementKind === "BalanceSheet")}
              onClick={() => setStatementKind("BalanceSheet")}
            >
              {t("statements.tabBalanceSheet")}
            </button>
          </div>
        </div>

        {/* View Mode Toggle & Date Filters */}
        <div
          style={{
            display: "flex",
            alignItems: "center",
            justifyContent: "space-between",
            flexWrap: "wrap",
            gap: "1rem",
            paddingTop: "0.75rem",
            borderTop: "1px solid var(--color-border-subtle, #e2e8f0)"
          }}
        >
          <div style={{ display: "flex", gap: "0.5rem" }}>
            <button
              type="button"
              style={{
                padding: "0.375rem 0.75rem",
                fontSize: "0.8125rem",
                fontWeight: 600,
                border: "none",
                borderRadius: "var(--radius-md, 0.5rem)",
                cursor: "pointer",
                backgroundColor: activeTab === "statement" ? "var(--color-surface-subtle, #f0f4f8)" : "transparent",
                color: activeTab === "statement" ? "var(--color-brand-primary, #102a43)" : "var(--color-content-muted, #64748b)"
              }}
              onClick={() => setActiveTab("statement")}
            >
              {t("statements.tabReport")}
            </button>
            <button
              type="button"
              style={{
                padding: "0.375rem 0.75rem",
                fontSize: "0.8125rem",
                fontWeight: 600,
                border: "none",
                borderRadius: "var(--radius-md, 0.5rem)",
                cursor: "pointer",
                backgroundColor: activeTab === "definition" ? "var(--color-surface-subtle, #f0f4f8)" : "transparent",
                color: activeTab === "definition" ? "var(--color-brand-primary, #102a43)" : "var(--color-content-muted, #64748b)"
              }}
              onClick={() => setActiveTab("definition")}
            >
              {t("statements.tabDefinitions")}
            </button>
          </div>

          {activeTab === "statement" && (
            <div style={{ display: "flex", alignItems: "center", gap: "0.75rem", flexWrap: "wrap" }}>
              <label htmlFor="statement-from-date" style={{ fontSize: "0.8125rem", fontWeight: 600, color: "var(--color-content-secondary, #334155)" }}>
                {t("statements.from")}:
              </label>
              <input
                id="statement-from-date"
                type="date"
                value={fromDate}
                onChange={(e) => setFromDate(e.target.value)}
                style={{
                  fontSize: "0.8125rem",
                  padding: "0.375rem 0.5rem",
                  border: "1px solid var(--color-border-default, #cbd5e1)",
                  borderRadius: "var(--radius-md, 0.5rem)",
                  backgroundColor: "var(--color-surface-card, #ffffff)",
                  color: "var(--color-content-primary, #0f172a)"
                }}
              />

              <label htmlFor="statement-to-date" style={{ fontSize: "0.8125rem", fontWeight: 600, color: "var(--color-content-secondary, #334155)" }}>
                {t("statements.to")}:
              </label>
              <input
                id="statement-to-date"
                type="date"
                value={toDate}
                onChange={(e) => setToDate(e.target.value)}
                style={{
                  fontSize: "0.8125rem",
                  padding: "0.375rem 0.5rem",
                  border: "1px solid var(--color-border-default, #cbd5e1)",
                  borderRadius: "var(--radius-md, 0.5rem)",
                  backgroundColor: "var(--color-surface-card, #ffffff)",
                  color: "var(--color-content-primary, #0f172a)"
                }}
              />

              <Button variant="secondary" size="compact" onPress={() => void refetchStatement()}>
                {t("statements.refresh")}
              </Button>
            </div>
          )}
        </div>
      </Surface>

      {/* REPORT VIEW */}
      {activeTab === "statement" && (
        <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
          {isStatementLoading && (
            <Surface variant="card" style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted, #64748b)" }}>
              <p>{t("statements.loading")}</p>
            </Surface>
          )}

          {isStatementError && (
            <Surface variant="card" style={{ padding: "1.5rem", border: "1px solid var(--color-status-danger, #dc2626)", backgroundColor: "var(--color-status-danger-subtle, #fef2f2)" }}>
              <p style={{ margin: 0, color: "var(--color-status-danger, #dc2626)", fontWeight: 600 }}>{t("statements.errorLoading")}</p>
            </Surface>
          )}

          {statement && (
            <>
              {/* Balance Sheet Integrity Indicator */}
              {statementKind === "BalanceSheet" && (
                <Surface
                  variant="card"
                  style={{
                    padding: "1rem 1.5rem",
                    backgroundColor: statement.isBalanced ? "var(--color-status-success-subtle, #ecfdf5)" : "var(--color-status-danger-subtle, #fef2f2)",
                    border: `1px solid ${statement.isBalanced ? "var(--color-status-success, #059669)" : "var(--color-status-danger, #dc2626)"}`
                  }}
                  data-testid="balance-sheet-integrity-badge"
                >
                  <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", flexWrap: "wrap", gap: "1rem" }}>
                    <div style={{ display: "flex", alignItems: "center", gap: "0.75rem" }}>
                      <StatusBadge
                        intent={statement.isBalanced ? "success" : "danger"}
                        label={statement.isBalanced ? t("statements.balancedMessage") : t("statements.unbalancedMessage")}
                        size="medium"
                      />
                    </div>
                    {statement.diagnostic && (
                      <span style={{ fontSize: "0.8125rem", color: "var(--color-status-danger, #dc2626)" }}>
                        {statement.diagnostic}
                      </span>
                    )}
                  </div>
                </Surface>
              )}

              {/* Statement Summary Card & Lines Table */}
              <Surface
                variant="card"
                title={statementKind === "ProfitAndLoss" ? t("statements.pnlHeader") : t("statements.balanceSheetHeader")}
                description={t("statements.dateRange", { fromDate: statement.fromDate, toDate: statement.toDate, currencyCode: statement.currencyCode })}
                headerActions={
                  <div style={{ textAlign: "end" }}>
                    <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted, #64748b)", display: "block" }}>
                      {statementKind === "ProfitAndLoss" ? t("statements.netProfitTotal") : t("statements.balanceTotal")}
                    </span>
                    <MoneyDisplay
                      value={{ amount: statement.total.toFixed(2), currency: statement.currencyCode }}
                      size="large"
                      coloredSign={statementKind === "ProfitAndLoss"}
                    />
                  </div>
                }
                style={{ padding: "0" }}
              >
                <div style={{ width: "100%", boxSizing: "border-box" }}>
                  {/* Header Row */}
                  <div
                    style={{
                      display: "flex",
                      justifyContent: "space-between",
                      alignItems: "center",
                      padding: "0.75rem 1rem",
                      backgroundColor: "var(--color-surface-subtle, #f8fafc)",
                      borderBottom: "1px solid var(--color-border-default, #cbd5e1)",
                      color: "var(--color-content-secondary, #334155)",
                      fontSize: "0.8125rem",
                      fontWeight: 700
                    }}
                  >
                    <span>{t("statements.colLineName")}</span>
                    <span>{t("statements.colAmount")}</span>
                  </div>

                  {/* Lines Rows */}
                  <div style={{ display: "flex", flexDirection: "column" }}>
                    {statement.lines.map((line: StatementLineView, idx: number) => {
                      const isHighlight = line.lineKind === "Subtotal" || line.lineKind === "Arithmetic" || line.code === "net-profit";
                      return (
                        <div
                          key={line.code}
                          style={{
                            display: "flex",
                            justifyContent: "space-between",
                            alignItems: "center",
                            padding: "0.75rem 1rem",
                            borderBottom: idx < statement.lines.length - 1 ? "1px solid var(--color-border-subtle, #e2e8f0)" : "none",
                            backgroundColor: isHighlight
                              ? "var(--color-surface-subtle, #f8fafc)"
                              : idx % 2 === 0 ? "var(--color-surface-card, #ffffff)" : "var(--color-surface-subtle, #f8fafc)",
                            gap: "0.75rem"
                          }}
                          data-testid={`statement-line-${line.code}`}
                        >
                          <div style={{ minWidth: 0, flex: "1 1 auto" }}>
                            <div style={{ display: "flex", alignItems: "center", gap: "0.5rem", flexWrap: "wrap" }}>
                              <span style={{ fontWeight: isHighlight ? 700 : 600, fontSize: "0.875rem", color: "var(--color-content-primary, #0f172a)" }}>
                                {getLineLabel(line, language, t)}
                              </span>
                              <span
                                style={{
                                  fontSize: "0.6875rem",
                                  padding: "0.125rem 0.375rem",
                                  borderRadius: "var(--radius-sm, 0.25rem)",
                                  backgroundColor: "var(--color-surface-sunken, #e2e8f0)",
                                  color: "var(--color-content-secondary, #475569)",
                                  fontWeight: 500
                                }}
                              >
                                {formatAccountType(line.lineKind)}
                              </span>
                              {statementKind === "ProfitAndLoss" && (line.code === "expense" || line.accountType === "Expense") && line.amount < 0 && (
                                <span
                                  style={{
                                    fontSize: "0.6875rem",
                                    padding: "0.125rem 0.375rem",
                                    borderRadius: "var(--radius-sm, 0.25rem)",
                                    backgroundColor: "var(--color-status-info-subtle, #f0f9ff)",
                                    color: "var(--color-status-info, #0284c7)",
                                    fontWeight: 700
                                  }}
                                >
                                  {t("statements.creditBalanceNote")}
                                </span>
                              )}
                            </div>
                            {line.accountType && (
                              <div style={{ fontSize: "0.6875rem", color: "var(--color-content-muted, #64748b)", marginTop: "0.125rem" }}>
                                {formatAccountType(line.accountType)}
                              </div>
                            )}
                            {statementKind === "ProfitAndLoss" && (line.code === "expense" || line.accountType === "Expense") && line.amount < 0 && (
                              <p style={{ margin: "0.25rem 0 0", fontSize: "0.75rem", color: "var(--color-status-info, #0284c7)", lineHeight: 1.4 }}>
                                {t("statements.creditExpenseExplanation")}
                              </p>
                            )}
                          </div>

                          <div style={{ flexShrink: 0, textAlign: "start", whiteSpace: "nowrap" }}>
                            <MoneyDisplay
                              value={{ amount: line.amount.toFixed(2), currency: statement.currencyCode }}
                              size={isHighlight ? "large" : "medium"}
                              coloredSign
                            />
                          </div>
                        </div>
                      );
                    })}
                  </div>
                </div>
              </Surface>
            </>
          )}
        </div>
      )}

      {/* DEFINITION VIEW */}
      {activeTab === "definition" && (
        <Surface
          variant="card"
          title={`${t("statements.defTitle")} (${statementKind})`}
          description={t("statements.defSubtitle")}
          style={{ padding: "0" }}
        >
          {isDefLoading ? (
            <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted, #64748b)" }}>
              <p>{t("common.loading")}</p>
            </div>
          ) : !definition ? (
            <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted, #64748b)" }}>
              <p>{t("statements.noStatementStructure")}</p>
            </div>
          ) : (
            <div style={{ overflowX: "auto" }}>
              <table style={{ width: "100%", borderCollapse: "collapse", textAlign: "start" }}>
                <thead>
                  <tr
                    style={{
                      backgroundColor: "var(--color-surface-subtle, #f8fafc)",
                      borderBottom: "1px solid var(--color-border-default, #cbd5e1)",
                      color: "var(--color-content-secondary, #334155)",
                      fontSize: "0.8125rem",
                      fontWeight: 700
                    }}
                  >
                    <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("statements.defColSeq")}</th>
                    <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("statements.defColCode")}</th>
                    <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("statements.defColNameAr")}</th>
                    <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("statements.defColNameEn")}</th>
                    <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("statements.defColAccountType")}</th>
                    <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("statements.defColKind")}</th>
                  </tr>
                </thead>
                <tbody>
                  {definition.lines.map((line, idx: number) => (
                    <tr
                      key={line.code}
                      style={{
                        borderBottom: idx < definition.lines.length - 1 ? "1px solid var(--color-border-subtle, #e2e8f0)" : "none",
                        backgroundColor: idx % 2 === 0 ? "var(--color-surface-card, #ffffff)" : "var(--color-surface-subtle, #f8fafc)"
                      }}
                    >
                      <td style={{ padding: "0.75rem 1rem", fontFamily: "var(--font-mono, monospace)", fontSize: "0.8125rem" }}>
                        {line.sequence}
                      </td>
                      <td style={{ padding: "0.75rem 1rem", fontFamily: "var(--font-mono, monospace)", fontSize: "0.75rem", fontWeight: 700 }}>
                        <bdi>{line.code}</bdi>
                      </td>
                      <td style={{ padding: "0.75rem 1rem", fontSize: "0.875rem", fontWeight: 600 }}>{line.nameAr}</td>
                      <td style={{ padding: "0.75rem 1rem", fontSize: "0.8125rem", color: "var(--color-content-muted, #64748b)" }}>{line.nameEn}</td>
                      <td style={{ padding: "0.75rem 1rem", fontSize: "0.8125rem" }}>{line.accountType || "—"}</td>
                      <td style={{ padding: "0.75rem 1rem" }}>
                        <span
                          style={{
                            fontSize: "0.6875rem",
                            padding: "0.1875rem 0.5rem",
                            borderRadius: "var(--radius-md, 0.5rem)",
                            backgroundColor: "var(--color-surface-subtle, #f1f5f9)",
                            border: "1px solid var(--color-border-subtle, #e2e8f0)",
                            color: "var(--color-content-secondary, #334155)"
                          }}
                        >
                          {line.lineKind}
                        </span>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </Surface>
      )}
    </div>
  );
};

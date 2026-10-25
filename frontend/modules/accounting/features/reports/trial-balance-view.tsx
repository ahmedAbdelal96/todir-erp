import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { MoneyDisplay } from "../../../../components/fields/money-display.tsx";
import { AccountingDataTable, type AccountingTableColumn } from "../../components/accounting-data-table.tsx";
import { useTrialBalance } from "../../hooks/use-ledger-queries.ts";
import type { TrialBalanceRowDto } from "../../types.ts";

export interface TrialBalanceViewProps {
  readonly legalEntityId: string;
  readonly baseCurrency: string;
  readonly onOpenGeneralLedger?: (accountCode: string) => void;
}

export function TrialBalanceView({
  legalEntityId,
  baseCurrency,
  onOpenGeneralLedger
}: TrialBalanceViewProps): React.JSX.Element {
  const { t } = useTranslation("accounting");
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");

  const { data: rows = [], isLoading } = useTrialBalance(
    legalEntityId,
    from || undefined,
    to || undefined
  );

  const totalDebit = rows.reduce((s, r) => s + r.debit, 0);
  const totalCredit = rows.reduce((s, r) => s + r.credit, 0);
  const difference = Math.abs(totalDebit - totalCredit);
  const isBalanced = difference < 0.0001;

  const columns: readonly AccountingTableColumn<TrialBalanceRowDto>[] = [
    {
      key: "code",
      header: t("account.code"),
      width: "160px",
      render: (item) => (
        <span style={{ fontWeight: 600, fontFamily: "var(--font-mono, monospace)" }}>
          <bdi>{item.accountCode}</bdi>
        </span>
      )
    },
    {
      key: "name",
      header: t("account.name"),
      render: (item) => <span>{item.accountName}</span>
    },
    {
      key: "debit",
      header: t("journal.debit"),
      width: "160px",
      align: "end",
      render: (item) => <MoneyDisplay value={{ amount: item.debit.toFixed(2), currency: item.currencyCode }} />
    },
    {
      key: "credit",
      header: t("journal.credit"),
      width: "160px",
      align: "end",
      render: (item) => <MoneyDisplay value={{ amount: item.credit.toFixed(2), currency: item.currencyCode }} />
    },
    {
      key: "balance",
      header: t("account.balance"),
      width: "160px",
      align: "end",
      render: (item) => (
        <span style={{ fontWeight: 700 }}>
          <MoneyDisplay value={{ amount: item.balance.toFixed(2), currency: item.currencyCode }} />
        </span>
      )
    },
    {
      key: "actions",
      header: t("common.actions"),
      width: "120px",
      align: "end",
      render: (item) => (
        onOpenGeneralLedger ? (
          <Button
            size="compact"
            variant="secondary"
            onPress={() => onOpenGeneralLedger(item.accountCode)}
          >
            {t("reports.drillDown")}
          </Button>
        ) : null
      )
    }
  ];

  return (
    <Surface style={{ padding: "1.25rem 1.5rem", width: "100%", maxWidth: "100%", boxSizing: "border-box", minWidth: 0 }}>
      <div
        style={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          flexWrap: "wrap",
          gap: "1rem",
          marginBottom: "1.25rem"
        }}
      >
        <div>
          <h2 style={{ margin: 0, fontSize: "1.25rem", fontWeight: 700 }}>
            {t("reports.trialBalanceTitle")}
          </h2>
          <p style={{ margin: "0.25rem 0 0", color: "var(--color-content-muted)", fontSize: "0.875rem" }}>
            {t("reports.trialBalanceDescription")}
          </p>
        </div>

        {/* Date Filter */}
        <div style={{ display: "flex", gap: "0.75rem", alignItems: "flex-end", flexWrap: "wrap" }}>
          <div style={{ width: "150px" }}>
            <TextField label={t("reports.from")} value={from} onChange={setFrom} placeholder="2026-01-01" />
          </div>
          <div style={{ width: "150px" }}>
            <TextField label={t("reports.to")} value={to} onChange={setTo} placeholder="2026-12-31" />
          </div>
        </div>
      </div>

      {isLoading ? (
        <div style={{ padding: "3rem", textAlign: "center" }}>{t("common.loading")}</div>
      ) : (
        <>
          <AccountingDataTable
            data={rows}
            columns={columns}
            rowKey={(item) => item.accountId}
            emptyMessage={t("reports.noDataFound")}
          />

          {/* Totals Footer Card */}
          <div
            style={{
              marginTop: "1.5rem",
              padding: "1rem 1.5rem",
              borderRadius: "var(--radius-md, 0.5rem)",
              backgroundColor: "var(--color-surface-sunken)",
              border: `1px solid ${isBalanced ? "var(--color-status-success)" : "var(--color-status-danger)"}`,
              display: "flex",
              justifyContent: "space-between",
              alignItems: "center",
              flexWrap: "wrap",
              gap: "1rem"
            }}
          >
            <div style={{ display: "flex", alignItems: "center", gap: "1rem" }}>
              <span style={{ fontWeight: 700, fontSize: "1rem" }}>{t("reports.trialBalanceTotals")}</span>
              <span style={{ fontWeight: 700, color: isBalanced ? "var(--color-status-success)" : "var(--color-status-danger)" }}>
                <span>{isBalanced ? "✓" : "✕"}</span>{" "}
                <span>{isBalanced ? t("reports.trialBalanceBalanced") : t("reports.trialBalanceUnbalanced")}</span>
              </span>
            </div>

            <div style={{ display: "flex", gap: "2rem", fontSize: "1rem", fontWeight: 700 }}>
              <div>
                <span style={{ color: "var(--color-content-muted)", fontSize: "0.875rem", display: "block" }}>
                  {t("journal.totalDebit")}
                </span>
                <MoneyDisplay value={{ amount: totalDebit.toFixed(2), currency: baseCurrency }} />
              </div>
              <div>
                <span style={{ color: "var(--color-content-muted)", fontSize: "0.875rem", display: "block" }}>
                  {t("journal.totalCredit")}
                </span>
                <MoneyDisplay value={{ amount: totalCredit.toFixed(2), currency: baseCurrency }} />
              </div>
            </div>
          </div>
        </>
      )}
    </Surface>
  );
}

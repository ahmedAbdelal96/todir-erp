import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { MoneyDisplay } from "../../../../components/fields/money-display.tsx";
import { LocalDateDisplay } from "../../../../components/fields/local-date-display.tsx";
import { AccountingDataTable, type AccountingTableColumn } from "../../components/accounting-data-table.tsx";
import { EntryExplanationModal } from "../entries/entry-explanation-modal.tsx";
import { useGeneralLedger, useAccounts } from "../../hooks/use-ledger-queries.ts";
import type { GeneralLedgerRowDto } from "../../types.ts";

export interface GeneralLedgerViewProps {
  readonly legalEntityId: string;
  readonly baseCurrency: string;
  readonly initialAccountCode?: string | undefined;
}

export function GeneralLedgerView({
  legalEntityId,
  baseCurrency,
  initialAccountCode
}: GeneralLedgerViewProps): React.JSX.Element {
  const { t } = useTranslation("accounting");

  const { data: accounts = [] } = useAccounts(legalEntityId);

  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [selectedAccountId, setSelectedAccountId] = useState<string>(() => {
    if (initialAccountCode) {
      const match = accounts.find((a) => a.code === initialAccountCode);
      return match ? match.id : "";
    }
    return "";
  });
  const [entryNumber, setEntryNumber] = useState("");
  const [cursor, setCursor] = useState<string | undefined>(undefined);

  const [explainEntryId, setExplainEntryId] = useState<string | null>(null);

  const { data: pageData, isLoading } = useGeneralLedger(legalEntityId, {
    from: from || undefined,
    to: to || undefined,
    accountId: selectedAccountId || undefined,
    entryNumber: entryNumber || undefined,
    cursor,
    limit: 50
  });

  const items = pageData?.items ?? [];

  // Running balance calculation
  let running = 0;
  const itemsWithRunningBalance = items.map((row) => {
    running += row.debit - row.credit;
    return {
      ...row,
      runningBalance: running
    };
  });

  const columns: readonly AccountingTableColumn<GeneralLedgerRowDto & { runningBalance: number }>[] = [
    {
      key: "postingDate",
      header: t("journal.entryDate"),
      width: "120px",
      render: (item) => <LocalDateDisplay value={item.postingDate} />
    },
    {
      key: "entryNumber",
      header: t("journal.entryNumber"),
      width: "160px",
      render: (item) => (
        <span style={{ fontWeight: 600, fontFamily: "var(--font-mono, monospace)" }}>
          <bdi>{item.entryNumber}</bdi>
        </span>
      )
    },
    {
      key: "account",
      header: t("journal.accountCodeName"),
      width: "200px",
      render: (item) => (
        <span>
          <bdi>{item.accountCode}</bdi> — {item.accountName}
        </span>
      )
    },
    {
      key: "source",
      header: t("explanation.sourceModule"),
      width: "140px",
      render: (item) => (
        <span>
          {item.sourceModule} ({item.sourceReference})
        </span>
      )
    },
    {
      key: "debit",
      header: t("journal.debit"),
      width: "130px",
      align: "end",
      render: (item) => <MoneyDisplay value={{ amount: item.debit.toFixed(2), currency: item.currencyCode }} />
    },
    {
      key: "credit",
      header: t("journal.credit"),
      width: "130px",
      align: "end",
      render: (item) => <MoneyDisplay value={{ amount: item.credit.toFixed(2), currency: item.currencyCode }} />
    },
    {
      key: "runningBalance",
      header: t("reports.runningBalance"),
      width: "140px",
      align: "end",
      render: (item) => (
        <span style={{ fontWeight: 600 }}>
          <MoneyDisplay value={{ amount: item.runningBalance.toFixed(2), currency: baseCurrency }} />
        </span>
      )
    },
    {
      key: "actions",
      header: t("common.actions"),
      width: "120px",
      align: "end",
      render: (item) => (
        <Button
          size="compact"
          variant="secondary"
          onPress={() => setExplainEntryId(item.entryId)}
        >
          {t("explanation.whyThisAccount")}
        </Button>
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
            {t("reports.generalLedgerTitle")}
          </h2>
          <p style={{ margin: "0.25rem 0 0", color: "var(--color-content-muted)", fontSize: "0.875rem" }}>
            {t("reports.generalLedgerDescription")}
          </p>
        </div>
      </div>

      {/* Filter toolbar */}
      <div
        style={{
          display: "grid",
          gridTemplateColumns: "repeat(auto-fit, minmax(180px, 1fr))",
          gap: "1rem",
          marginBottom: "1.25rem",
          alignItems: "flex-end"
        }}
      >
        <div>
          <label style={{ display: "block", marginBottom: "0.25rem", fontSize: "0.875rem", fontWeight: 600 }}>
            {t("account.filterByAccount")}
          </label>
          <select
            value={selectedAccountId}
            onChange={(e) => setSelectedAccountId(e.target.value)}
            style={{
              width: "100%",
              padding: "0.5rem 0.75rem",
              borderRadius: "var(--radius-md, 0.5rem)",
              border: "1px solid var(--color-border-default)",
              backgroundColor: "var(--color-surface-card)",
              fontSize: "0.875rem"
            }}
          >
            <option value="">{t("account.allAccounts")}</option>
            {accounts.map((a) => (
              <option key={a.id} value={a.id}>
                {a.code} — {a.nameAr}
              </option>
            ))}
          </select>
        </div>

        <TextField
          label={t("reports.from")}
          value={from}
          onChange={setFrom}
          placeholder="2026-01-01"
        />

        <TextField
          label={t("reports.to")}
          value={to}
          onChange={setTo}
          placeholder="2026-12-31"
        />

        <TextField
          label={t("journal.entryNumber")}
          value={entryNumber}
          onChange={setEntryNumber}
        />
      </div>

      {isLoading ? (
        <div style={{ padding: "3rem", textAlign: "center" }}>{t("common.loading")}</div>
      ) : (
        <>
          <AccountingDataTable
            data={itemsWithRunningBalance}
            columns={columns}
            rowKey={(item) => `${item.entryId}-${item.lineId}`}
            emptyMessage={t("reports.noTransactionsFound")}
          />

          {/* Cursor pagination controls */}
          {pageData?.nextCursor && (
            <div style={{ display: "flex", justifyContent: "center", marginTop: "1.5rem" }}>
              <Button
                variant="secondary"
                onPress={() => setCursor(pageData.nextCursor ?? undefined)}
              >
                {t("reports.loadMore")}
              </Button>
            </div>
          )}
        </>
      )}

      {/* Entry Explanation Modal */}
      <EntryExplanationModal
        isOpen={Boolean(explainEntryId)}
        onClose={() => setExplainEntryId(null)}
        legalEntityId={legalEntityId}
        entryId={explainEntryId}
      />
    </Surface>
  );
}

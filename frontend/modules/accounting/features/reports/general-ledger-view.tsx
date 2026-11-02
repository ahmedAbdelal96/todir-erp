import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { FilterBar } from "../../../../components/primitives/filter-bar.tsx";
import { SearchField } from "../../../../components/primitives/search-field.tsx";
import { Pagination } from "../../../../components/primitives/pagination.tsx";
import { DataGrid, type DataGridColumn } from "../../../../components/grid/index.ts";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { MoneyDisplay } from "../../../../components/fields/money-display.tsx";
import { LocalDateDisplay } from "../../../../components/fields/local-date-display.tsx";
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
  const [cursorHistory, setCursorHistory] = useState<readonly (string | undefined)[]>([]);
  const [pageSize, setPageSize] = useState(50);

  const [explainEntryId, setExplainEntryId] = useState<string | null>(null);

  const { data: pageData, isLoading } = useGeneralLedger(legalEntityId, {
    from: from || undefined,
    to: to || undefined,
    accountId: selectedAccountId || undefined,
    entryNumber: entryNumber || undefined,
    cursor,
    limit: pageSize
  });

  const items = pageData?.items ?? [];

  const resetCursor = () => {
    setCursor(undefined);
    setCursorHistory([]);
  };

  const columns: readonly DataGridColumn<GeneralLedgerRowDto>[] = [
    {
      id: "postingDate",
      header: t("journal.entryDate"),
      width: "120px",
      sortable: false, filterable: false, cellRenderer: ({ row: item }) => <LocalDateDisplay value={item.postingDate} />
    },
    {
      id: "entryNumber",
      header: t("journal.entryNumber"),
      width: "160px",
      sortable: false, filterable: false, cellRenderer: ({ row: item }) => (
        <span style={{ fontWeight: 600, fontFamily: "var(--font-mono, monospace)" }}>
          <bdi>{item.entryNumber}</bdi>
        </span>
      )
    },
    {
      id: "account",
      header: t("journal.accountCodeName"),
      width: "200px",
      sortable: false, filterable: false, cellRenderer: ({ row: item }) => (
        <span>
          <bdi>{item.accountCode}</bdi> — {item.accountName}
        </span>
      )
    },
    {
      id: "source",
      header: t("explanation.sourceModule"),
      width: "140px",
      sortable: false, filterable: false, cellRenderer: ({ row: item }) => (
        <span>
          {item.sourceModule} ({item.sourceReference})
        </span>
      )
    },
    {
      id: "debit",
      header: t("journal.debit"),
      width: "130px",
      align: "end",
      sortable: false, filterable: false, cellRenderer: ({ row: item }) => <MoneyDisplay value={{ amount: item.debit.toFixed(2), currency: item.currencyCode }} />
    },
    {
      id: "credit",
      header: t("journal.credit"),
      width: "130px",
      align: "end",
      sortable: false, filterable: false, cellRenderer: ({ row: item }) => <MoneyDisplay value={{ amount: item.credit.toFixed(2), currency: item.currencyCode }} />
    },
    {
      id: "runningBalance",
      header: t("reports.runningBalance"),
      width: "140px",
      align: "end",
      sortable: false, filterable: false, cellRenderer: ({ row: item }) => (
        <span style={{ fontWeight: 600 }}>
          {item.runningBalance === null || item.runningBalance === undefined ? "—" : <MoneyDisplay value={{ amount: item.runningBalance.toFixed(2), currency: baseCurrency }} />}
        </span>
      )
    },
    {
      id: "actions",
      header: t("common.actions"),
      width: "120px",
      align: "end",
      sortable: false, filterable: false, cellRenderer: ({ row: item }) => (
        <Button
          size="compact"
          variant="secondary"
          onClick={() => setExplainEntryId(item.entryId)}
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

      {/* Shared server-side filter toolbar */}
      <FilterBar
        searchInput={
          <SearchField
            value={entryNumber}
            onChange={(value) => { setEntryNumber(value); resetCursor(); }}
            placeholder={t("journal.entryNumber")}
            isPending={isLoading}
            ariaLabel={t("journal.entryNumber")}
          />
        }
        activeFilterCount={(from ? 1 : 0) + (to ? 1 : 0) + (selectedAccountId ? 1 : 0) + (entryNumber ? 1 : 0)}
        onResetFilters={() => { setFrom(""); setTo(""); setSelectedAccountId(""); setEntryNumber(""); resetCursor(); }}
        isPending={isLoading}
      >
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
            onChange={(e) => { setSelectedAccountId(e.target.value); resetCursor(); }}
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
          onChange={(value) => { setFrom(value); resetCursor(); }}
          placeholder="2026-01-01"
        />

        <TextField
          label={t("reports.to")}
          value={to}
          onChange={(value) => { setTo(value); resetCursor(); }}
          placeholder="2026-12-31"
        />

      </div>
      </FilterBar>

      {isLoading ? (
        <div style={{ padding: "3rem", textAlign: "center" }}>{t("common.loading")}</div>
      ) : (
        <>
          <DataGrid columns={columns} staticRows={items} loading={isLoading} emptyMessage={t("reports.noTransactionsFound")} hasActiveFilters={Boolean(from || to || selectedAccountId || entryNumber)} density="compact" />

          <Pagination
            mode="cursor"
            page={cursorHistory.length + 1}
            itemCount={items.length}
            pageSize={pageSize}
            hasPrevious={cursorHistory.length > 0}
            hasNext={Boolean(pageData?.nextCursor)}
            onPrevious={() => {
              if (cursorHistory.length === 0) return;
              const nextHistory = cursorHistory.slice(0, -1);
              setCursorHistory(nextHistory);
              setCursor(nextHistory[nextHistory.length - 1]);
            }}
            onNext={() => {
              if (!pageData?.nextCursor) return;
              setCursorHistory((history) => [...history, cursor]);
              setCursor(pageData.nextCursor ?? undefined);
            }}
            onPageSizeChange={(size) => { setPageSize(size); resetCursor(); }}
            isDisabled={isLoading}
          />
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

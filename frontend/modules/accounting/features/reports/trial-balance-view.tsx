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
import { useTrialBalancePage } from "../../hooks/use-ledger-queries.ts";
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
  const [search, setSearch] = useState("");
  const [sort, setSort] = useState("accountCodeAsc");
  const [cursor, setCursor] = useState<string | undefined>();
  const [cursorHistory, setCursorHistory] = useState<readonly (string | undefined)[]>([]);
  const [pageSize, setPageSize] = useState(50);

  const { data: pageData, isLoading, isError, refetch } = useTrialBalancePage(legalEntityId, {
    from: from || undefined,
    to: to || undefined,
    search: search || undefined,
    sort,
    cursor,
    limit: pageSize
  });
  const rows = pageData?.items ?? [];

  const totalDebit = pageData?.totalDebit ?? 0;
  const totalCredit = pageData?.totalCredit ?? 0;
  const difference = Math.abs(totalDebit - totalCredit);
  const isBalanced = difference < 0.0001;
  const resetCursor = () => { setCursor(undefined); setCursorHistory([]); };

  const columns: readonly DataGridColumn<TrialBalanceRowDto>[] = [
    {
      id: "code",
      header: t("account.code"),
      width: "160px",
      sortable: false, filterable: false, cellRenderer: ({ row: item }) => (
        <span style={{ fontWeight: 600, fontFamily: "var(--font-mono, monospace)" }}>
          <bdi>{item.accountCode}</bdi>
        </span>
      )
    },
    {
      id: "name",
      header: t("account.name"),
      sortable: false, filterable: false, cellRenderer: ({ row: item }) => <span>{item.accountName}</span>
    },
    {
      id: "debit",
      header: t("journal.debit"),
      width: "160px",
      align: "end",
      sortable: false, filterable: false, cellRenderer: ({ row: item }) => <MoneyDisplay value={{ amount: item.debit.toFixed(2), currency: item.currencyCode }} />
    },
    {
      id: "credit",
      header: t("journal.credit"),
      width: "160px",
      align: "end",
      sortable: false, filterable: false, cellRenderer: ({ row: item }) => <MoneyDisplay value={{ amount: item.credit.toFixed(2), currency: item.currencyCode }} />
    },
    {
      id: "balance",
      header: t("account.balance"),
      width: "160px",
      align: "end",
      sortable: false, filterable: false, cellRenderer: ({ row: item }) => (
        <span style={{ fontWeight: 700 }}>
          <MoneyDisplay value={{ amount: item.balance.toFixed(2), currency: item.currencyCode }} />
        </span>
      )
    },
    {
      id: "actions",
      header: t("common.actions"),
      width: "120px",
      align: "end",
      sortable: false, filterable: false, cellRenderer: ({ row: item }) => (
        onOpenGeneralLedger ? (
          <Button
            size="compact"
            variant="secondary"
            onClick={() => onOpenGeneralLedger(item.accountCode)}
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

        {/* Shared server-side filter bar */}
        <FilterBar
          searchInput={
            <SearchField
              value={search}
              onChange={(value) => { setSearch(value); resetCursor(); }}
              placeholder={t("reports.accountSearchPlaceholder")}
              isPending={isLoading}
              ariaLabel={t("reports.accountSearch")}
            />
          }
          activeFilterCount={(from ? 1 : 0) + (to ? 1 : 0) + (search ? 1 : 0) + (sort !== "accountCodeAsc" ? 1 : 0)}
          onResetFilters={() => { setFrom(""); setTo(""); setSearch(""); setSort("accountCodeAsc"); resetCursor(); }}
          isPending={isLoading}
        >
          <div style={{ width: "150px" }}>
            <TextField label={t("reports.from")} value={from} onChange={(value) => { setFrom(value); resetCursor(); }} placeholder="2026-01-01" />
          </div>
          <div style={{ width: "150px" }}>
            <TextField label={t("reports.to")} value={to} onChange={(value) => { setTo(value); resetCursor(); }} placeholder="2026-12-31" />
          </div>
          <label style={{ width: "190px" }}><span style={{ display: "block", marginBottom: "0.25rem", fontSize: "0.8125rem", fontWeight: 600 }}>{t("reports.sortBy")}</span><select value={sort} onChange={(event) => { setSort(event.target.value); resetCursor(); }} style={{ width: "100%", padding: "0.5rem 0.75rem", border: "1px solid var(--color-border-default)", borderRadius: "var(--radius-md)", background: "var(--color-surface-card)" }}><option value="accountCodeAsc">{t("reports.accountCodeOrder")}</option><option value="accountNameAsc">{t("reports.accountNameOrder")}</option></select></label>
        </FilterBar>
      </div>

      {isLoading ? (
        <div style={{ padding: "3rem", textAlign: "center" }}>{t("common.loading")}</div>
      ) : isError ? (
        <div style={{ padding: "3rem", textAlign: "center" }}>
          <p>{t("reports.loadError")}</p>
          <Button variant="secondary" onPress={() => void refetch()}>{t("reports.retry")}</Button>
        </div>
      ) : (
        <>
          <DataGrid columns={columns} staticRows={rows} loading={isLoading} emptyMessage={t("reports.noDataFound")} hasActiveFilters={Boolean(from || to || search || sort !== "accountCodeAsc")} density="compact" />

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
          <Pagination
            mode="cursor"
            page={cursorHistory.length + 1}
            itemCount={rows.length}
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
    </Surface>
  );
}

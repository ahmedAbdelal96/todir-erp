import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { FilterBar } from "../../../../components/primitives/filter-bar.tsx";
import { SearchField } from "../../../../components/primitives/search-field.tsx";
import { Pagination } from "../../../../components/primitives/pagination.tsx";
import { DataGrid, type DataGridColumn } from "../../../../components/grid/index.ts";
import { LocalDateDisplay } from "../../../../components/fields/local-date-display.tsx";
import { MoneyDisplay } from "../../../../components/fields/money-display.tsx";
import { LedgerEntryStatusBadge } from "../../components/ledger-status-badges.tsx";
import { EntryDetailModal } from "./entry-detail-modal.tsx";
import { EntryExplanationModal } from "./entry-explanation-modal.tsx";
import { ReversalModal } from "./reversal-modal.tsx";
import { useJournalEntriesPage } from "../../hooks/use-ledger-queries.ts";
import type { JournalEntryDto } from "../../types.ts";

export interface EntriesExplorerViewProps {
  readonly legalEntityId: string;
  readonly canReverse: boolean;
  readonly canExplain: boolean;
  readonly today: string;
  readonly pageSize?: number;
}

export function EntriesExplorerView({
  legalEntityId,
  canReverse,
  canExplain,
  today,
  pageSize: initialPageSize = 50
}: EntriesExplorerViewProps): React.JSX.Element {
  const { t } = useTranslation("accounting");
  const [searchTerm, setSearchTerm] = useState("");
  const [status, setStatus] = useState("");
  const [sort, setSort] = useState("postingDateDesc");
  const [pageSize, setPageSize] = useState(initialPageSize);
  const [cursor, setCursor] = useState<string | undefined>();
  const [cursorHistory, setCursorHistory] = useState<readonly (string | undefined)[]>([]);

  const { data: pageData, isLoading, isError, refetch } = useJournalEntriesPage(legalEntityId, {
    search: searchTerm || undefined,
    status: status || undefined,
    sort,
    cursor,
    limit: pageSize
  });
  const entries = pageData?.items ?? [];

  const [selectedEntry, setSelectedEntry] = useState<JournalEntryDto | null>(null);
  const [explainEntryId, setExplainEntryId] = useState<string | null>(null);
  const [reversalEntry, setReversalEntry] = useState<JournalEntryDto | null>(null);

  const resetCursor = () => {
    setCursor(undefined);
    setCursorHistory([]);
  };

  const columns: readonly DataGridColumn<JournalEntryDto>[] = [
    {
      id: "entryNumber",
      header: t("journal.entryNumber"),
      width: "180px",
      sortable: false, filterable: false, cellRenderer: ({ row: item }) => (
        <span style={{ fontWeight: 600, fontFamily: "var(--font-mono, monospace)" }}>
          <bdi>{item.entryNumber}</bdi>
        </span>
      )
    },
    {
      id: "postingDate",
      header: t("journal.entryDate"),
      width: "130px",
      sortable: false, filterable: false, cellRenderer: ({ row: item }) => <LocalDateDisplay value={item.postingDate} />
    },
    {
      id: "debit",
      header: t("journal.totalDebit"),
      width: "140px",
      align: "end",
      sortable: false, filterable: false, cellRenderer: ({ row: item }) => {
        const totalDebit = item.lines.reduce((s, l) => s + l.debit, 0);
        return <MoneyDisplay value={{ amount: totalDebit.toFixed(2), currency: item.currencyCode }} />;
      }
    },
    {
      id: "credit",
      header: t("journal.totalCredit"),
      width: "140px",
      align: "end",
      sortable: false, filterable: false, cellRenderer: ({ row: item }) => {
        const totalCredit = item.lines.reduce((s, l) => s + l.credit, 0);
        return <MoneyDisplay value={{ amount: totalCredit.toFixed(2), currency: item.currencyCode }} />;
      }
    },
    {
      id: "status",
      header: t("account.status"),
      width: "130px",
      sortable: false, filterable: false, cellRenderer: ({ row: item }) => <LedgerEntryStatusBadge status={item.status} />
    },
    {
      id: "actions",
      header: t("common.actions"),
      width: "260px",
      align: "end",
      sortable: false, filterable: false, cellRenderer: ({ row: item }) => (
        <div style={{ display: "flex", gap: "0.5rem", justifyContent: "flex-end", flexWrap: "wrap" }}>
          <Button
            size="compact"
            variant="secondary"
            onClick={() => setSelectedEntry(item)}
          >
            {t("journal.viewDetails")}
          </Button>

          {canExplain && (
            <Button
              size="compact"
              variant="secondary"
              onClick={() => setExplainEntryId(item.id)}
            >
              {t("explanation.whyThisAccount")}
            </Button>
          )}

          {canReverse && item.status === "Posted" && (
            <Button
              size="compact"
              variant="secondary"
              onClick={() => setReversalEntry(item)}
            >
              {t("reversal.button")}
            </Button>
          )}
        </div>
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
            {t("journal.entriesExplorerTitle")}
          </h2>
          <p style={{ margin: "0.25rem 0 0", color: "var(--color-content-muted)", fontSize: "0.875rem" }}>
            {t("journal.entriesExplorerDescription")}
          </p>
        </div>

        <FilterBar
          searchInput={
            <SearchField
              value={searchTerm}
              onChange={(value) => { setSearchTerm(value); resetCursor(); }}
              placeholder={t("journal.searchEntriesPlaceholder")}
              isPending={isLoading}
              ariaLabel={t("journal.searchEntriesPlaceholder")}
            />
          }
          activeFilterCount={(searchTerm ? 1 : 0) + (status ? 1 : 0) + (sort !== "postingDateDesc" ? 1 : 0)}
          onResetFilters={() => { setSearchTerm(""); setStatus(""); setSort("postingDateDesc"); resetCursor(); }}
          isPending={isLoading}
        >
        <div style={{ width: "180px" }}>
          <label style={{ display: "block", marginBottom: "0.25rem", fontSize: "0.8125rem", fontWeight: 600 }}>{t("account.status")}</label>
          <select value={status} onChange={(event) => { setStatus(event.target.value); resetCursor(); }} style={{ width: "100%", padding: "0.5rem 0.75rem", border: "1px solid var(--color-border-default)", borderRadius: "var(--radius-md)", background: "var(--color-surface-card)" }}>
            <option value="">{t("reports.allStatuses")}</option>
            <option value="Posted">{t("reports.posted")}</option>
            <option value="Draft">{t("reports.draft")}</option>
          </select>
        </div>
        <div style={{ width: "180px" }}>
          <label style={{ display: "block", marginBottom: "0.25rem", fontSize: "0.8125rem", fontWeight: 600 }}>{t("reports.sortBy")}</label>
          <select value={sort} onChange={(event) => { setSort(event.target.value); resetCursor(); }} style={{ width: "100%", padding: "0.5rem 0.75rem", border: "1px solid var(--color-border-default)", borderRadius: "var(--radius-md)", background: "var(--color-surface-card)" }}>
            <option value="postingDateDesc">{t("reports.newestFirst")}</option>
            <option value="postingDateAsc">{t("reports.oldestFirst")}</option>
          </select>
        </div>
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
          <DataGrid columns={columns} staticRows={entries} loading={isLoading} emptyMessage={t("journal.noEntriesFound")} hasActiveFilters={Boolean(searchTerm || status || sort !== "postingDateDesc")} density="compact" />
          <Pagination
            mode="cursor"
            page={cursorHistory.length + 1}
            itemCount={entries.length}
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

      {/* Entry Details Modal */}
      <EntryDetailModal
        isOpen={Boolean(selectedEntry)}
        onClose={() => setSelectedEntry(null)}
        entry={selectedEntry}
        onExplain={() => {
          if (selectedEntry) {
            setExplainEntryId(selectedEntry.id);
            setSelectedEntry(null);
          }
        }}
      />

      {/* Entry Explanation Modal ("Why This Account?") */}
      <EntryExplanationModal
        isOpen={Boolean(explainEntryId)}
        onClose={() => setExplainEntryId(null)}
        legalEntityId={legalEntityId}
        entryId={explainEntryId}
      />

      {/* Reversal Modal */}
      <ReversalModal
        isOpen={Boolean(reversalEntry)}
        onClose={() => setReversalEntry(null)}
        legalEntityId={legalEntityId}
        entry={reversalEntry}
        today={today}
      />
    </Surface>
  );
}

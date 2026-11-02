import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { CursorPagination } from "../../../../components/primitives/cursor-pagination.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { LocalDateDisplay } from "../../../../components/fields/local-date-display.tsx";
import { MoneyDisplay } from "../../../../components/fields/money-display.tsx";
import { AccountingDataTable, type AccountingTableColumn } from "../../components/accounting-data-table.tsx";
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
  const [pageSize, setPageSize] = useState(initialPageSize);
  const [cursor, setCursor] = useState<string | undefined>();
  const [cursorHistory, setCursorHistory] = useState<readonly (string | undefined)[]>([]);

  const { data: pageData, isLoading, isError, refetch } = useJournalEntriesPage(legalEntityId, {
    search: searchTerm || undefined,
    status: status || undefined,
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

  const columns: readonly AccountingTableColumn<JournalEntryDto>[] = [
    {
      key: "entryNumber",
      header: t("journal.entryNumber"),
      width: "180px",
      render: (item) => (
        <span style={{ fontWeight: 600, fontFamily: "var(--font-mono, monospace)" }}>
          <bdi>{item.entryNumber}</bdi>
        </span>
      )
    },
    {
      key: "postingDate",
      header: t("journal.entryDate"),
      width: "130px",
      render: (item) => <LocalDateDisplay value={item.postingDate} />
    },
    {
      key: "debit",
      header: t("journal.totalDebit"),
      width: "140px",
      align: "end",
      render: (item) => {
        const totalDebit = item.lines.reduce((s, l) => s + l.debit, 0);
        return <MoneyDisplay value={{ amount: totalDebit.toFixed(2), currency: item.currencyCode }} />;
      }
    },
    {
      key: "credit",
      header: t("journal.totalCredit"),
      width: "140px",
      align: "end",
      render: (item) => {
        const totalCredit = item.lines.reduce((s, l) => s + l.credit, 0);
        return <MoneyDisplay value={{ amount: totalCredit.toFixed(2), currency: item.currencyCode }} />;
      }
    },
    {
      key: "status",
      header: t("account.status"),
      width: "130px",
      render: (item) => <LedgerEntryStatusBadge status={item.status} />
    },
    {
      key: "actions",
      header: t("common.actions"),
      width: "260px",
      align: "end",
      render: (item) => (
        <div style={{ display: "flex", gap: "0.5rem", justifyContent: "flex-end", flexWrap: "wrap" }}>
          <Button
            size="compact"
            variant="secondary"
            onPress={() => setSelectedEntry(item)}
          >
            {t("journal.viewDetails")}
          </Button>

          {canExplain && (
            <Button
              size="compact"
              variant="secondary"
              onPress={() => setExplainEntryId(item.id)}
            >
              {t("explanation.whyThisAccount")}
            </Button>
          )}

          {canReverse && item.status === "Posted" && (
            <Button
              size="compact"
              variant="secondary"
              onPress={() => setReversalEntry(item)}
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

        <div style={{ width: "260px" }}>
          <TextField
            value={searchTerm}
            onChange={(value) => { setSearchTerm(value); resetCursor(); }}
            placeholder={t("journal.searchEntriesPlaceholder")}
          />
        </div>
        <div style={{ width: "180px" }}>
          <label style={{ display: "block", marginBottom: "0.25rem", fontSize: "0.8125rem", fontWeight: 600 }}>{t("account.status")}</label>
          <select value={status} onChange={(event) => { setStatus(event.target.value); resetCursor(); }} style={{ width: "100%", padding: "0.5rem 0.75rem", border: "1px solid var(--color-border-default)", borderRadius: "var(--radius-md)", background: "var(--color-surface-card)" }}>
            <option value="">{t("reports.allStatuses")}</option>
            <option value="Posted">{t("reports.posted")}</option>
            <option value="Draft">{t("reports.draft")}</option>
          </select>
        </div>
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
          <AccountingDataTable
          data={entries}
          columns={columns}
          rowKey={(item) => item.id}
          emptyMessage={t("journal.noEntriesFound")}
          />
          <CursorPagination
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

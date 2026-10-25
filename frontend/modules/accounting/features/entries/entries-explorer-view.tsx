import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { LocalDateDisplay } from "../../../../components/fields/local-date-display.tsx";
import { MoneyDisplay } from "../../../../components/fields/money-display.tsx";
import { AccountingDataTable, type AccountingTableColumn } from "../../components/accounting-data-table.tsx";
import { LedgerEntryStatusBadge } from "../../components/ledger-status-badges.tsx";
import { EntryDetailModal } from "./entry-detail-modal.tsx";
import { EntryExplanationModal } from "./entry-explanation-modal.tsx";
import { ReversalModal } from "./reversal-modal.tsx";
import type { JournalEntryDto } from "../../types.ts";

export interface EntriesExplorerViewProps {
  readonly legalEntityId: string;
  readonly entries: readonly JournalEntryDto[];
  readonly isLoading: boolean;
  readonly canReverse: boolean;
  readonly canExplain: boolean;
  readonly today: string;
}

export function EntriesExplorerView({
  legalEntityId,
  entries,
  isLoading,
  canReverse,
  canExplain,
  today
}: EntriesExplorerViewProps): React.JSX.Element {
  const { t } = useTranslation("accounting");
  const [searchTerm, setSearchTerm] = useState("");

  const [selectedEntry, setSelectedEntry] = useState<JournalEntryDto | null>(null);
  const [explainEntryId, setExplainEntryId] = useState<string | null>(null);
  const [reversalEntry, setReversalEntry] = useState<JournalEntryDto | null>(null);

  const filteredEntries = entries.filter((e) =>
    e.entryNumber.toLowerCase().includes(searchTerm.toLowerCase())
  );

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
            onChange={setSearchTerm}
            placeholder={t("journal.searchEntriesPlaceholder")}
          />
        </div>
      </div>

      {isLoading ? (
        <div style={{ padding: "3rem", textAlign: "center" }}>{t("common.loading")}</div>
      ) : (
        <AccountingDataTable
          data={filteredEntries}
          columns={columns}
          rowKey={(item) => item.id}
          emptyMessage={t("journal.noEntriesFound")}
        />
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

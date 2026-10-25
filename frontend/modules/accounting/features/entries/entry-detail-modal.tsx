import React from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { MoneyDisplay } from "../../../../components/fields/money-display.tsx";
import { LocalDateDisplay } from "../../../../components/fields/local-date-display.tsx";
import { LedgerEntryStatusBadge } from "../../components/ledger-status-badges.tsx";
import { AccountingDataTable, type AccountingTableColumn } from "../../components/accounting-data-table.tsx";
import type { EntryLineDto, JournalEntryDto } from "../../types.ts";

export interface EntryDetailModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly entry: JournalEntryDto | null;
  readonly onExplain?: () => void;
}

export function EntryDetailModal({
  isOpen,
  onClose,
  entry,
  onExplain
}: EntryDetailModalProps): React.JSX.Element | null {
  const { t } = useTranslation("accounting");

  if (!entry || !isOpen) return null;

  const totalDebit = entry.lines.reduce((s, l) => s + l.debit, 0);
  const totalCredit = entry.lines.reduce((s, l) => s + l.credit, 0);

  const columns: readonly AccountingTableColumn<EntryLineDto>[] = [
    {
      key: "seq",
      header: "#",
      width: "50px",
      render: (item) => <span>{item.sequence}</span>
    },
    {
      key: "role",
      header: t("explanation.accountRole"),
      width: "160px",
      render: (item) => <span style={{ fontWeight: 600 }}>{item.role}</span>
    },
    {
      key: "debit",
      header: t("journal.debit"),
      width: "140px",
      align: "end",
      render: (item) => <MoneyDisplay value={{ amount: item.debit.toFixed(2), currency: item.currencyCode }} />
    },
    {
      key: "credit",
      header: t("journal.credit"),
      width: "140px",
      align: "end",
      render: (item) => <MoneyDisplay value={{ amount: item.credit.toFixed(2), currency: item.currencyCode }} />
    },
    {
      key: "description",
      header: t("journal.memo"),
      render: (item) => <span>{item.description || "—"}</span>
    }
  ];

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open) => !open && onClose()}
      title={`${t("journal.entryDetails")}: ${entry.entryNumber}`}
      size="large"
      footer={
        <div style={{ display: "flex", justifyContent: "space-between", width: "100%" }}>
          <div>
            {onExplain && (
              <Button variant="secondary" onPress={onExplain}>
                {t("explanation.whyThisAccount")}
              </Button>
            )}
          </div>
          <Button variant="primary" onPress={onClose}>
            {t("common.close")}
          </Button>
        </div>
      }
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {/* Header summary */}
        <div
          style={{
            display: "grid",
            gridTemplateColumns: "repeat(auto-fit, minmax(180px, 1fr))",
            gap: "0.75rem",
            padding: "0.875rem 1rem",
            backgroundColor: "var(--color-surface-sunken)",
            borderRadius: "var(--radius-md, 0.5rem)",
            fontSize: "0.875rem"
          }}
        >
          <div>
            <span style={{ color: "var(--color-content-muted)" }}>{t("journal.entryDate")}: </span>
            <strong>
              <LocalDateDisplay value={entry.postingDate} />
            </strong>
          </div>
          <div>
            <span style={{ color: "var(--color-content-muted)" }}>{t("account.status")}: </span>
            <div style={{ marginTop: "0.25rem" }}>
              <LedgerEntryStatusBadge status={entry.status} />
            </div>
          </div>
          <div>
            <span style={{ color: "var(--color-content-muted)" }}>{t("journal.totalDebit")}: </span>
            <strong>
              <MoneyDisplay value={{ amount: totalDebit.toFixed(2), currency: entry.currencyCode }} />
            </strong>
          </div>
          <div>
            <span style={{ color: "var(--color-content-muted)" }}>{t("journal.totalCredit")}: </span>
            <strong>
              <MoneyDisplay value={{ amount: totalCredit.toFixed(2), currency: entry.currencyCode }} />
            </strong>
          </div>
        </div>

        {/* Lines */}
        <AccountingDataTable
          data={entry.lines}
          columns={columns}
          rowKey={(item) => `${entry.id}-${item.sequence}`}
          emptyMessage={t("journal.noLines")}
        />
      </div>
    </Dialog>
  );
}

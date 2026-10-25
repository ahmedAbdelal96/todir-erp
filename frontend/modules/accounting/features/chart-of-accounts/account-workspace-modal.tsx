import React from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { MoneyDisplay } from "../../../../components/fields/money-display.tsx";
import { LocalDateDisplay } from "../../../../components/fields/local-date-display.tsx";
import { useAccountWorkspace } from "../../hooks/use-ledger-queries.ts";
import { AccountStatusBadge } from "../../components/ledger-status-badges.tsx";
import { AccountingDataTable, type AccountingTableColumn } from "../../components/accounting-data-table.tsx";
import type { GeneralLedgerRowDto } from "../../types.ts";

export interface AccountWorkspaceModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly accountId: string | null;
  readonly onOpenGeneralLedger?: ((accountCode: string) => void) | undefined;
}

export function AccountWorkspaceModal({
  isOpen,
  onClose,
  legalEntityId,
  accountId,
  onOpenGeneralLedger
}: AccountWorkspaceModalProps): React.JSX.Element | null {
  const { t } = useTranslation("accounting");
  const { data: workspace, isLoading } = useAccountWorkspace(legalEntityId, accountId ?? "");

  if (!accountId || !isOpen) return null;

  const activityColumns: readonly AccountingTableColumn<GeneralLedgerRowDto>[] = [
    {
      key: "postingDate",
      header: t("journal.entryDate"),
      width: "120px",
      render: (item) => <LocalDateDisplay value={item.postingDate} />
    },
    {
      key: "entryNumber",
      header: t("journal.entryNumber"),
      width: "140px",
      render: (item) => <span style={{ fontWeight: 600 }}>{item.entryNumber}</span>
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
      render: (item) => (
        <MoneyDisplay value={{ amount: item.debit.toFixed(2), currency: item.currencyCode }} />
      )
    },
    {
      key: "credit",
      header: t("journal.credit"),
      width: "130px",
      align: "end",
      render: (item) => (
        <MoneyDisplay value={{ amount: item.credit.toFixed(2), currency: item.currencyCode }} />
      )
    }
  ];

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open) => !open && onClose()}
      title={workspace ? `${workspace.account.code} — ${workspace.account.nameAr} (${workspace.account.nameEn})` : t("account.workspaceTitle")}
      size="large"
      footer={
        <div style={{ display: "flex", gap: "0.75rem", justifyContent: "space-between", width: "100%" }}>
          <div>
            {workspace && onOpenGeneralLedger && (
              <Button
                variant="secondary"
                onPress={() => {
                  onClose();
                  onOpenGeneralLedger(workspace.account.code);
                }}
              >
                {t("account.viewInGeneralLedger")}
              </Button>
            )}
          </div>
          <Button variant="primary" onPress={onClose}>
            {t("common.close")}
          </Button>
        </div>
      }
    >
      {isLoading || !workspace ? (
        <div style={{ padding: "2rem", textAlign: "center" }}>{t("common.loading")}</div>
      ) : (
        <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
          {/* Account Overview Cards */}
          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", gap: "1rem" }}>
            <div
              style={{
                padding: "1rem",
                borderRadius: "var(--radius-md, 0.5rem)",
                backgroundColor: "var(--color-surface-sunken)",
                border: "1px solid var(--color-border-default)"
              }}
            >
              <div style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>{t("account.balance")}</div>
              <div style={{ fontSize: "1.25rem", fontWeight: 700, marginTop: "0.25rem" }}>
                <MoneyDisplay
                  value={{
                    amount: workspace.balance.balance.toFixed(2),
                    currency: workspace.balance.currencyCode
                  }}
                />
              </div>
            </div>

            <div
              style={{
                padding: "1rem",
                borderRadius: "var(--radius-md, 0.5rem)",
                backgroundColor: "var(--color-surface-sunken)",
                border: "1px solid var(--color-border-default)"
              }}
            >
              <div style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>{t("journal.totalDebit")}</div>
              <div style={{ fontSize: "1.125rem", fontWeight: 600, marginTop: "0.25rem" }}>
                <MoneyDisplay
                  value={{
                    amount: workspace.balance.debit.toFixed(2),
                    currency: workspace.balance.currencyCode
                  }}
                />
              </div>
            </div>

            <div
              style={{
                padding: "1rem",
                borderRadius: "var(--radius-md, 0.5rem)",
                backgroundColor: "var(--color-surface-sunken)",
                border: "1px solid var(--color-border-default)"
              }}
            >
              <div style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>{t("journal.totalCredit")}</div>
              <div style={{ fontSize: "1.125rem", fontWeight: 600, marginTop: "0.25rem" }}>
                <MoneyDisplay
                  value={{
                    amount: workspace.balance.credit.toFixed(2),
                    currency: workspace.balance.currencyCode
                  }}
                />
              </div>
            </div>

            <div
              style={{
                padding: "1rem",
                borderRadius: "var(--radius-md, 0.5rem)",
                backgroundColor: "var(--color-surface-sunken)",
                border: "1px solid var(--color-border-default)"
              }}
            >
              <div style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>{t("account.status")}</div>
              <div style={{ marginTop: "0.5rem" }}>
                <AccountStatusBadge status={workspace.account.status} />
              </div>
            </div>
          </div>

          {/* Account Attributes */}
          <div
            style={{
              padding: "1rem",
              borderRadius: "var(--radius-md, 0.5rem)",
              border: "1px solid var(--color-border-default)",
              display: "grid",
              gridTemplateColumns: "repeat(auto-fit, minmax(160px, 1fr))",
              gap: "0.75rem",
              fontSize: "0.875rem"
            }}
          >
            <div>
              <span style={{ color: "var(--color-content-muted)" }}>{t("account.type")}: </span>
              <strong>{t(`accountTypes.${workspace.account.type}`)}</strong>
            </div>
            <div>
              <span style={{ color: "var(--color-content-muted)" }}>{t("account.subtype")}: </span>
              <strong>{t(`accountSubtypes.${workspace.account.subtype}` as any, { defaultValue: workspace.account.subtype })}</strong>
            </div>
            <div>
              <span style={{ color: "var(--color-content-muted)" }}>{t("account.normalBalance")}: </span>
              <strong>{t(`normalBalances.${workspace.account.normalBalance}`)}</strong>
            </div>
            <div>
              <span style={{ color: "var(--color-content-muted)" }}>{t("account.directPosting")}: </span>
              <strong>{workspace.account.allowDirectPosting ? t("common.yes") : t("common.no")}</strong>
            </div>
          </div>

          {/* Recent Activity Table */}
          <div>
            <h4 style={{ margin: "0 0 0.5rem", fontSize: "1rem", fontWeight: 600 }}>
              {t("account.recentActivity")}
            </h4>
            <AccountingDataTable
              data={workspace.recentActivity}
              columns={activityColumns}
              rowKey={(item) => `${item.entryId}-${item.lineId}`}
              emptyMessage={t("account.noRecentActivity")}
            />
          </div>
        </div>
      )}
    </Dialog>
  );
}

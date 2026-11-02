import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { StatusBadge } from "../../../../components/fields/status-badge.tsx";
import { MoneyDisplay } from "../../../../components/fields/money-display.tsx";
import { AccountingDataTable, type AccountingTableColumn } from "../../components/accounting-data-table.tsx";
import { useReconcileBalances, useRebuildBalances } from "../../hooks/use-ledger-queries.ts";
import type { ProjectionReconciliationDifferenceDto } from "../../types.ts";

export interface ReconciliationViewProps {
  readonly legalEntityId: string;
  readonly baseCurrency: string;
}

export function ReconciliationView({
  legalEntityId,
  baseCurrency
}: ReconciliationViewProps): React.JSX.Element {
  const { t } = useTranslation("accounting");

  const { data: reconciliation, isLoading } = useReconcileBalances(legalEntityId);
  const rebuildMutation = useRebuildBalances();

  const [rebuildSuccess, setRebuildSuccess] = useState<boolean>(false);

  const handleRebuild = async () => {
    setRebuildSuccess(false);
    await rebuildMutation.mutateAsync({ legalEntityId });
    setRebuildSuccess(true);
  };

  const isHealthy = reconciliation?.healthy ?? true;
  const differences = reconciliation?.differences ?? [];

  const columns: readonly AccountingTableColumn<ProjectionReconciliationDifferenceDto>[] = [
    {
      key: "accountId",
      header: t("reconciliation.accountId"),
      width: "160px",
      render: (item) => <span style={{ fontFamily: "var(--font-mono, monospace)" }}>{item.accountId.slice(0, 8)}...</span>
    },
    {
      key: "canonicalBalance",
      header: t("reconciliation.canonicalBalance"),
      width: "160px",
      align: "end",
      render: (item) => <MoneyDisplay value={{ amount: item.canonicalBalance.toFixed(2), currency: item.currencyCode }} />
    },
    {
      key: "projectionBalance",
      header: t("reconciliation.projectionBalance"),
      width: "160px",
      align: "end",
      render: (item) => <MoneyDisplay value={{ amount: item.projectionBalance.toFixed(2), currency: item.currencyCode }} />
    },
    {
      key: "balanceDifference",
      header: t("reconciliation.difference"),
      width: "160px",
      align: "end",
      render: (item) => (
        <span style={{ fontWeight: 700, color: "var(--color-status-danger)" }}>
          <MoneyDisplay value={{ amount: item.balanceDifference.toFixed(2), currency: item.currencyCode }} />
        </span>
      )
    },
    {
      key: "status",
      header: t("account.status"),
      width: "120px",
      render: (item) => (
        <StatusBadge
          status={item.balanceDifference === 0 ? "success" : "danger"}
          label={item.balanceDifference === 0 ? t("reconciliation.statusMatched") : t("reconciliation.statusDiscrepancy")}
        />
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
          marginBottom: "1.5rem"
        }}
      >
        <div>
          <div style={{ display: "flex", alignItems: "center", gap: "0.75rem", marginBottom: "0.25rem" }}>
            <h2 style={{ margin: 0, fontSize: "1.25rem", fontWeight: 700 }}>
              {t("reconciliation.projectionTitle")}
            </h2>
            <StatusBadge
              status={isHealthy ? "success" : "danger"}
              label={isHealthy ? t("reconciliation.healthy") : t("reconciliation.unreconciled")}
            />
          </div>
          <p style={{ margin: 0, color: "var(--color-content-muted)", fontSize: "0.875rem" }}>
            {t("reconciliation.description")}
          </p>
        </div>

        <Button
          variant="secondary"
          isLoading={rebuildMutation.isPending}
          onPress={handleRebuild}
        >
          {t("reconciliation.rebuildBalances")}
        </Button>
      </div>

      {rebuildSuccess && (
        <div
          style={{
            padding: "0.875rem 1rem",
            marginBottom: "1.25rem",
            backgroundColor: "var(--color-status-success-subtle, #ecfdf5)",
            border: "1px solid var(--color-status-success, #10b981)",
            color: "var(--color-status-success, #065f46)",
            borderRadius: "var(--radius-md, 0.5rem)",
            fontSize: "0.875rem"
          }}
        >
          ✓ {t("reconciliation.rebuildComplete")}
        </div>
      )}

      {isLoading ? (
        <div style={{ padding: "3rem", textAlign: "center" }}>{t("common.loading")}</div>
      ) : isHealthy ? (
        <div
          style={{
            padding: "2.5rem",
            textAlign: "center",
            backgroundColor: "var(--color-surface-sunken)",
            borderRadius: "var(--radius-lg, 0.75rem)",
            border: "1px solid var(--color-border-default)"
          }}
        >
          <div style={{ fontSize: "1.5rem", marginBottom: "0.5rem" }}>✓</div>
          <h3 style={{ margin: "0 0 0.5rem", color: "var(--color-status-success)" }}>
            {t("reconciliation.allBalancesMatch")}
          </h3>
          <p style={{ margin: 0, color: "var(--color-content-muted)", fontSize: "0.875rem" }}>
            {t("reconciliation.projectionsTruthVerified")}
          </p>
        </div>
      ) : (
        <div>
          <h4 style={{ margin: "0 0 0.75rem", color: "var(--color-status-danger)" }}>
            {t("reconciliation.differencesDetected")} ({differences.length})
          </h4>
          <AccountingDataTable
            data={differences}
            columns={columns}
            rowKey={(item) => `${item.accountId}-${item.periodId}`}
          />
        </div>
      )}
    </Surface>
  );
}

import React from "react";
import { useTranslation } from "react-i18next";
import { useCashPosition, useCashToLedgerReport } from "../../hooks/use-banking-queries.ts";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "../../../../components/primitives/table.tsx";
import { BankingMoney } from "../../components/banking-money.tsx";
import { StatusBadge } from "../../../../components/fields/status-badge.tsx";
import { BdiText } from "../../../../components/formatting/bidi-text.tsx";
import { formatDate } from "../../../../components/formatting/date-formatter.ts";
import { LandmarkIcon, AlertTriangleIcon, CheckCircleIcon, RefreshCwIcon } from "../../components/banking-icons.tsx";

interface Props {
  readonly legalEntityId: string;
}

export function BankingOverviewTab({ legalEntityId }: Props): React.JSX.Element {
  const { t } = useTranslation("banking");
  const { data: positions, isLoading: isPositionsLoading, error: positionsError, refetch: refetchPositions } = useCashPosition(legalEntityId);
  const { data: ledgerReport, isLoading: isLedgerLoading, error: ledgerError, refetch: refetchLedger } = useCashToLedgerReport(legalEntityId);

  const handleRefreshAll = () => {
    void refetchPositions();
    void refetchLedger();
  };

  if (isPositionsLoading) {
    return (
      <div style={{ padding: "2rem", textAlign: "center", color: "var(--color-content-muted)" }}>
        <p>{t("common.loading")}</p>
      </div>
    );
  }

  if (positionsError) {
    return (
      <Surface style={{ padding: "2rem", textAlign: "center", border: "1px solid var(--color-border-danger)" }}>
        <p style={{ color: "var(--color-status-danger)", fontWeight: 600 }}>{t("common.error")}</p>
        <Button variant="secondary" onClick={handleRefreshAll} style={{ marginTop: "1rem" }}>
          {t("common.retry")}
        </Button>
      </Surface>
    );
  }

  const items = positions ?? [];
  const hasStale = items.some((p) => p.isStale);

  const currencyTotals = items.reduce<Record<string, { liquid: number; book: number; inTransit: number }>>((acc, curr) => {
    const c = curr.currencyCode || "EGP";
    if (!acc[c]) {
      acc[c] = { liquid: 0, book: 0, inTransit: 0 };
    }
    acc[c].liquid += curr.availableBalance ?? 0;
    acc[c].book += curr.bookBalance ?? 0;
    acc[c].inTransit += curr.inTransitAmount ?? 0;
    return acc;
  }, {});

  const currencies = Object.keys(currencyTotals);

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
      {/* Header ribbon */}
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: "1rem" }}>
        <div>
          <h2 style={{ margin: 0, fontSize: "1.25rem", color: "var(--color-brand-primary)", display: "flex", alignItems: "center", gap: "0.5rem" }}>
            <LandmarkIcon />
            {t("overview.title")}
          </h2>
        </div>
        <Button variant="secondary" onClick={handleRefreshAll} startIcon={<RefreshCwIcon />}>
          {t("overview.refresh")}
        </Button>
      </div>

      {/* Stale Warning Banner */}
      {hasStale && (
        <div
          role="alert"
          style={{
            padding: "0.875rem 1.25rem",
            backgroundColor: "#fffbeb",
            border: "1px solid #fde68a",
            borderRadius: "var(--radius-md)",
            color: "#92400e",
            display: "flex",
            alignItems: "center",
            gap: "0.75rem",
            fontWeight: 500
          }}
        >
          <AlertTriangleIcon />
          <span>{t("overview.staleWarning")}</span>
        </div>
      )}

      {/* KPI Ribbon */}
      <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(240px, 1fr))", gap: "1rem" }}>
        {currencies.length > 0 ? (
          currencies.map((curr) => (
            <Surface key={curr} style={{ padding: "1.25rem" }}>
              <div style={{ fontSize: "0.875rem", color: "var(--color-content-muted)", marginBottom: "0.25rem" }}>
                {t("overview.totalLiquid")} ({curr})
              </div>
              <div style={{ fontSize: "1.5rem", fontWeight: 700, color: "var(--color-brand-primary)" }}>
                <BankingMoney amount={currencyTotals[curr]?.liquid ?? 0} currency={curr} size="large" />
              </div>
              <div style={{ display: "flex", justifyContent: "space-between", fontSize: "0.8125rem", color: "var(--color-content-muted)", marginTop: "0.75rem", borderTop: "1px solid var(--color-border-subtle)", paddingTop: "0.5rem" }}>
                <span>{t("overview.totalBook")}:</span>
                <BankingMoney amount={currencyTotals[curr]?.book ?? 0} currency={curr} />
              </div>
              <div style={{ display: "flex", justifyContent: "space-between", fontSize: "0.8125rem", color: "var(--color-content-muted)", marginTop: "0.25rem" }}>
                <span>{t("overview.totalInTransit")}:</span>
                <BankingMoney amount={currencyTotals[curr]?.inTransit ?? 0} currency={curr} />
              </div>
            </Surface>
          ))
        ) : (
          <Surface style={{ padding: "1.25rem" }}>
            <div style={{ fontSize: "0.875rem", color: "var(--color-content-muted)" }}>{t("overview.totalLiquid")}</div>
            <div style={{ fontSize: "1.5rem", fontWeight: 700, color: "var(--color-brand-primary)" }}>—</div>
          </Surface>
        )}
      </div>

      {/* Cash-to-Ledger Control Card */}
      <Surface style={{ padding: "1.5rem" }}>
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "1rem", flexWrap: "wrap", gap: "0.75rem" }}>
          <h3 style={{ margin: 0, fontSize: "1.0625rem", color: "var(--color-brand-primary)", display: "flex", alignItems: "center", gap: "0.5rem" }}>
            {ledgerReport?.isBalanced ? <CheckCircleIcon style={{ color: "#16a34a" }} /> : <AlertTriangleIcon style={{ color: "#d97706" }} />}
            {t("overview.cashToLedgerTitle")}
          </h3>
          <StatusBadge
            intent={ledgerReport?.isBalanced ? "success" : "info"}
            label={ledgerReport ? (ledgerReport.isBalanced ? t("overview.balanced") : t("overview.unbalanced")) : t("overview.balanced")}
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", gap: "1rem" }}>
          <div style={{ padding: "0.875rem", background: "var(--color-surface-page)", borderRadius: "var(--radius-sm)", border: "1px solid var(--color-border-subtle)" }}>
            <div style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", marginBottom: "0.25rem" }}>{t("overview.ledgerCashBalance")}</div>
            <div style={{ fontSize: "1.125rem", fontWeight: 600 }}>
              <BankingMoney amount={ledgerReport?.ledgerCashBalance ?? 0} currency={ledgerReport?.baseCurrencyCode ?? "EGP"} />
            </div>
          </div>
          <div style={{ padding: "0.875rem", background: "var(--color-surface-page)", borderRadius: "var(--radius-sm)", border: "1px solid var(--color-border-subtle)" }}>
            <div style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", marginBottom: "0.25rem" }}>{t("overview.bankingBookBalance")}</div>
            <div style={{ fontSize: "1.125rem", fontWeight: 600 }}>
              <BankingMoney amount={ledgerReport?.bankingBookBalance ?? 0} currency={ledgerReport?.baseCurrencyCode ?? "EGP"} />
            </div>
          </div>
          <div style={{ padding: "0.875rem", background: "var(--color-surface-page)", borderRadius: "var(--radius-sm)", border: "1px solid var(--color-border-subtle)" }}>
            <div style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", marginBottom: "0.25rem" }}>{t("overview.difference")}</div>
            <div style={{ fontSize: "1.125rem", fontWeight: 600, color: (ledgerReport?.difference ?? 0) === 0 ? "inherit" : "var(--color-status-danger)" }}>
              <BankingMoney amount={ledgerReport?.difference ?? 0} currency={ledgerReport?.baseCurrencyCode ?? "EGP"} />
            </div>
          </div>
          <div style={{ padding: "0.875rem", background: "var(--color-surface-page)", borderRadius: "var(--radius-sm)", border: "1px solid var(--color-border-subtle)" }}>
            <div style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", marginBottom: "0.25rem" }}>{t("overview.ledgerBasis")}</div>
            <div style={{ fontSize: "0.875rem", fontWeight: 500, color: "var(--color-content-default)" }}>
              <BdiText>{ledgerReport?.ledgerBasis ?? t("overview.ledgerBasisDefault")}</BdiText>
            </div>
          </div>
        </div>
      </Surface>

      {/* Cash Position Details Table */}
      <Surface style={{ padding: "1.25rem" }}>
        <h3 style={{ margin: "0 0 1rem", fontSize: "1rem", color: "var(--color-brand-primary)" }}>
          {t("overview.cashPositionTable")}
        </h3>
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t("overview.account")}</TableHead>
              <TableHead>{t("overview.type")}</TableHead>
              <TableHead>{t("overview.currency")}</TableHead>
              <TableHead align="end">{t("overview.bookBalance")}</TableHead>
              <TableHead align="end">{t("overview.externalBalance")}</TableHead>
              <TableHead align="end">{t("overview.availableBalance")}</TableHead>
              <TableHead align="end">{t("overview.inTransit")}</TableHead>
              <TableHead>{t("overview.basis")}</TableHead>
              <TableHead>{t("overview.asOf")}</TableHead>
              <TableHead>{t("overview.stale")}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {items.length === 0 ? (
              <TableRow>
                <TableCell colSpan={10} style={{ textAlign: "center", color: "var(--color-content-muted)", padding: "2rem" }}>
                  {t("common.noData")}
                </TableCell>
              </TableRow>
            ) : (
              items.map((pos) => (
                <TableRow key={`${pos.treasuryAccountId}-${pos.currencyCode}`}>
                  <TableCell>
                    <div style={{ fontWeight: 600 }}>{pos.accountName}</div>
                    <div style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
                      <BdiText>{pos.accountCode}</BdiText>
                    </div>
                  </TableCell>
                  <TableCell>
                    <span style={{ fontSize: "0.8125rem" }}>{t(`accountTypes.${pos.accountType}` as const)}</span>
                  </TableCell>
                  <TableCell>
                    <BdiText>{pos.currencyCode}</BdiText>
                  </TableCell>
                  <TableCell align="end">
                    <BankingMoney amount={pos.bookBalance} currency={pos.currencyCode} />
                  </TableCell>
                  <TableCell align="end">
                    <BankingMoney amount={pos.externalBalance} currency={pos.currencyCode} />
                  </TableCell>
                  <TableCell align="end">
                    <span style={{ fontWeight: 600 }}>
                      <BankingMoney amount={pos.availableBalance} currency={pos.currencyCode} />
                    </span>
                  </TableCell>
                  <TableCell align="end">
                    <BankingMoney amount={pos.inTransitAmount} currency={pos.currencyCode} />
                  </TableCell>
                  <TableCell style={{ fontSize: "0.8125rem" }}>
                    <BdiText>{pos.balanceBasis}</BdiText>
                  </TableCell>
                  <TableCell style={{ fontSize: "0.8125rem" }}>
                    {pos.asOf ? <BdiText>{formatDate(pos.asOf)}</BdiText> : "—"}
                  </TableCell>
                  <TableCell>
                    <StatusBadge
                      intent={pos.isStale ? "warning" : "success"}
                      label={pos.isStale ? t("overview.staleYes") : t("overview.staleNo")}
                    />
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </Surface>
    </div>
  );
}

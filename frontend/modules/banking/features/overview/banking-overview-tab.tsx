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

  interface CurrencyAggregation {
    currency: string;
    totalAccounts: number;
    syncedAccounts: number;
    hasPending: boolean;
    hasStale: boolean;
    liquidTotal: number | null;
    bookTotal: number | null;
    inTransitTotal: number;
  }

  const currencyTotals = items.reduce<Record<string, CurrencyAggregation>>((acc, curr) => {
    const c = curr.currencyCode || "EGP";
    if (!acc[c]) {
      acc[c] = {
        currency: c,
        totalAccounts: 0,
        syncedAccounts: 0,
        hasPending: false,
        hasStale: false,
        liquidTotal: 0,
        bookTotal: 0,
        inTransitTotal: 0
      };
    }
    const agg = acc[c]!;
    agg.totalAccounts += 1;
    if (curr.isStale) agg.hasStale = true;

    if (curr.availableBalance === null || curr.availableBalance === undefined) {
      agg.hasPending = true;
      agg.liquidTotal = null;
    } else if (agg.liquidTotal !== null) {
      agg.liquidTotal += curr.availableBalance;
    }

    if (curr.bookBalance === null || curr.bookBalance === undefined) {
      agg.hasPending = true;
      agg.bookTotal = null;
    } else if (agg.bookTotal !== null) {
      agg.bookTotal += curr.bookBalance;
    }

    if (curr.availableBalance !== null && curr.availableBalance !== undefined &&
        curr.bookBalance !== null && curr.bookBalance !== undefined) {
      agg.syncedAccounts += 1;
    }

    agg.inTransitTotal += curr.inTransitAmount ?? 0;
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
      <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(260px, 1fr))", gap: "1rem" }}>
        {currencies.length > 0 ? (
          currencies.map((curr) => {
            const agg = currencyTotals[curr]!;
            return (
              <Surface key={curr} style={{ padding: "1.25rem", display: "flex", flexDirection: "column", justifyContent: "space-between" }}>
                <div>
                  <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "0.5rem" }}>
                    <span style={{ fontSize: "0.875rem", color: "var(--color-content-muted)", fontWeight: 500 }}>
                      {t("overview.totalLiquid")} ({curr})
                    </span>
                    <span style={{
                      fontSize: "0.75rem",
                      padding: "0.125rem 0.375rem",
                      borderRadius: "var(--radius-xs)",
                      backgroundColor: agg.hasStale ? "#fef3c7" : "var(--color-surface-subtle)",
                      color: agg.hasStale ? "#92400e" : "var(--color-content-muted)",
                      fontWeight: 600
                    }}>
                      {agg.hasStale ? t("overview.sourceStale") : t("overview.sourceFreshness")}
                    </span>
                  </div>

                  <div style={{ fontSize: "1.5rem", fontWeight: 700, color: "var(--color-brand-primary)", minHeight: "2.25rem", display: "flex", alignItems: "center" }}>
                    {agg.liquidTotal !== null ? (
                      <BankingMoney amount={agg.liquidTotal} currency={curr} size="large" />
                    ) : (
                      <span style={{ fontSize: "1rem", color: "var(--color-status-warning)", fontWeight: 600 }}>
                        {t("overview.balancePending")}
                      </span>
                    )}
                  </div>

                  <div style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", marginTop: "0.25rem" }}>
                    {agg.hasPending
                      ? t("overview.coverageIndicator", { synced: agg.syncedAccounts, total: agg.totalAccounts })
                      : t("overview.coverageComplete", { count: agg.totalAccounts })}
                  </div>
                </div>

                <div style={{ marginTop: "0.875rem", borderTop: "1px solid var(--color-border-subtle)", paddingTop: "0.625rem", display: "flex", flexDirection: "column", gap: "0.375rem" }}>
                  <div style={{ display: "flex", justifyContent: "space-between", fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
                    <span>{t("overview.totalBook")}:</span>
                    {agg.bookTotal !== null ? (
                      <BankingMoney amount={agg.bookTotal} currency={curr} />
                    ) : (
                      <span style={{ color: "var(--color-status-warning)" }}>{t("overview.balancePending")}</span>
                    )}
                  </div>
                  <div style={{ display: "flex", justifyContent: "space-between", fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
                    <span>{t("overview.totalInTransit")}:</span>
                    <BankingMoney amount={agg.inTransitTotal} currency={curr} />
                  </div>
                </div>
              </Surface>
            );
          })
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
            {ledgerError ? (
              <AlertTriangleIcon style={{ color: "#d97706" }} />
            ) : ledgerReport?.isBalanced ? (
              <CheckCircleIcon style={{ color: "#16a34a" }} />
            ) : (
              <AlertTriangleIcon style={{ color: "#dc2626" }} />
            )}
            {t("overview.cashToLedgerTitle")}
          </h3>
          {ledgerError ? (
            <StatusBadge intent="warning" label={t("overview.configurationRequired")} />
          ) : isLedgerLoading ? (
            <StatusBadge intent="neutral" label={t("common.loading")} />
          ) : (
            <StatusBadge
              intent={ledgerReport?.isBalanced ? "success" : "danger"}
              label={ledgerReport?.isBalanced ? t("overview.balanced") : t("overview.unbalanced")}
            />
          )}
        </div>

        {ledgerError ? (
          <div
            role="alert"
            style={{
              padding: "1rem",
              backgroundColor: "var(--color-surface-subtle, #f8fafc)",
              border: "1px dashed var(--color-border-warning, #f59e0b)",
              borderRadius: "var(--radius-md)",
              color: "var(--color-content-muted)",
              display: "flex",
              alignItems: "flex-start",
              gap: "0.75rem"
            }}
          >
            <AlertTriangleIcon style={{ color: "#d97706", marginTop: "0.125rem", flexShrink: 0 }} />
            <div>
              <div style={{ fontWeight: 600, color: "var(--color-content-default)", marginBottom: "0.25rem" }}>
                {t("overview.configurationRequired")}
              </div>
              <div style={{ fontSize: "0.875rem", lineHeight: 1.5 }}>
                {t("overview.ledgerControlNotConfigured")}
              </div>
            </div>
          </div>
        ) : isLedgerLoading ? (
          <div style={{ padding: "1rem", textAlign: "center", color: "var(--color-content-muted)" }}>
            {t("common.loading")}
          </div>
        ) : (
          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", gap: "1rem" }}>
            <div style={{ padding: "0.875rem", background: "var(--color-surface-page)", borderRadius: "var(--radius-sm)", border: "1px solid var(--color-border-subtle)" }}>
              <div style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", marginBottom: "0.25rem" }}>{t("overview.ledgerCashBalance")}</div>
              <div style={{ fontSize: "1.125rem", fontWeight: 600 }}>
                <BankingMoney amount={ledgerReport?.ledgerCashBalance} currency={ledgerReport?.baseCurrencyCode} />
              </div>
            </div>
            <div style={{ padding: "0.875rem", background: "var(--color-surface-page)", borderRadius: "var(--radius-sm)", border: "1px solid var(--color-border-subtle)" }}>
              <div style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", marginBottom: "0.25rem" }}>{t("overview.bankingBookBalance")}</div>
              <div style={{ fontSize: "1.125rem", fontWeight: 600 }}>
                <BankingMoney amount={ledgerReport?.bankingBookBalance} currency={ledgerReport?.baseCurrencyCode} />
              </div>
            </div>
            <div style={{ padding: "0.875rem", background: "var(--color-surface-page)", borderRadius: "var(--radius-sm)", border: "1px solid var(--color-border-subtle)" }}>
              <div style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", marginBottom: "0.25rem" }}>{t("overview.difference")}</div>
              <div style={{ fontSize: "1.125rem", fontWeight: 600, color: (ledgerReport?.difference ?? 0) === 0 ? "inherit" : "var(--color-status-danger)" }}>
                <BankingMoney amount={ledgerReport?.difference} currency={ledgerReport?.baseCurrencyCode} />
              </div>
            </div>
            <div style={{ padding: "0.875rem", background: "var(--color-surface-page)", borderRadius: "var(--radius-sm)", border: "1px solid var(--color-border-subtle)" }}>
              <div style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", marginBottom: "0.25rem" }}>{t("overview.ledgerBasis")}</div>
              <div style={{ fontSize: "0.875rem", fontWeight: 500, color: "var(--color-content-default)" }}>
                <BdiText>{ledgerReport?.ledgerBasis ?? t("overview.ledgerBasisDefault")}</BdiText>
              </div>
            </div>
          </div>
        )}
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
                      {pos.availableBalance != null ? (
                        <BankingMoney amount={pos.availableBalance} currency={pos.currencyCode} />
                      ) : (
                        <span style={{ color: "var(--color-content-muted)", fontSize: "0.8125rem" }}>
                          {t("overview.balancePending")}
                        </span>
                      )}
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

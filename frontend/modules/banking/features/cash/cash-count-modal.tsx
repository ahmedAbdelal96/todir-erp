import React, { useState, useEffect } from "react";
import { useTranslation } from "react-i18next";
import { useOutletContext } from "react-router-dom";
import { useCreateCashCount, useCashPosition } from "../../hooks/use-banking-queries.ts";
import { useOptionalUserContext } from "../../../../core/runtime/runtime-provider.tsx";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "../../../../components/primitives/table.tsx";
import { BankingMoney } from "../../components/banking-money.tsx";
import { BdiText } from "../../../../components/formatting/bidi-text.tsx";

interface Props {
  readonly legalEntityId: string;
  readonly accountId: string;
  readonly currencyCode?: string;
  readonly expectedBalance?: number | null;
  readonly isOpen: boolean;
  readonly onClose: () => void;
}

interface DenominationRow {
  denomination: string;
  unitValue: number;
  quantity: number;
}

const DEFAULT_DENOMINATIONS: readonly { denomination: string; unitValue: number }[] = [
  { denomination: "200", unitValue: 200 },
  { denomination: "100", unitValue: 100 },
  { denomination: "50", unitValue: 50 },
  { denomination: "20", unitValue: 20 },
  { denomination: "10", unitValue: 10 },
  { denomination: "5", unitValue: 5 },
  { denomination: "1", unitValue: 1 },
  { denomination: "0.50", unitValue: 0.5 }
];

export function CashCountModal({
  legalEntityId,
  accountId,
  currencyCode = "EGP",
  expectedBalance,
  isOpen,
  onClose
}: Props): React.JSX.Element | null {
  const { t } = useTranslation("banking");
  const createMutation = useCreateCashCount(legalEntityId, accountId);
  const user = useOptionalUserContext();
  const outlet = useOutletContext<{ userId?: string } | undefined>();
  const effectiveUserId = outlet?.userId || user?.userId || null;

  const { data: positions, isLoading: isPositionLoading } = useCashPosition(legalEntityId);
  const currentPosition = positions?.find((p) => p.treasuryAccountId === accountId);
  const resolvedExpected =
    expectedBalance != null
      ? expectedBalance
      : currentPosition?.bookBalance != null
      ? currentPosition.bookBalance
      : null;

  const today = new Date().toISOString().slice(0, 10);
  const [cutoffDate, setCutoffDate] = useState(today);
  const [denominations, setDenominations] = useState<DenominationRow[]>(
    DEFAULT_DENOMINATIONS.map((d) => ({ ...d, quantity: 0 }))
  );
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  if (!isOpen) return null;

  const totalCounted = denominations.reduce((acc, d) => acc + d.unitValue * d.quantity, 0);
  const variance =
    resolvedExpected != null
      ? Math.round((totalCounted - resolvedExpected) * 100) / 100
      : null;
  const isSubmissionDisabled =
    !effectiveUserId || resolvedExpected == null || createMutation.isPending;

  const handleQuantityChange = (index: number, val: string) => {
    const qty = parseInt(val, 10) || 0;
    setDenominations((prev) =>
      prev.map((item, i) => (i === index ? { ...item, quantity: Math.max(0, qty) } : item))
    );
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMsg(null);

    if (!effectiveUserId || resolvedExpected == null) {
      setErrorMsg(t("cash.cannotSubmitWithoutBalanceOrCustodian"));
      return;
    }

    try {
      await createMutation.mutateAsync({
        cutoffDate,
        custodianId: effectiveUserId,
        expectedAmount: resolvedExpected,
        denominations: denominations
          .filter((d) => d.quantity > 0)
          .map((d) => ({
            denomination: d.denomination,
            unitValue: d.unitValue,
            quantity: d.quantity
          }))
      });
      onClose();
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : t("common.error");
      setErrorMsg(msg);
    }
  };

  return (
    <Dialog isOpen={isOpen} onClose={onClose} title={t("cash.countModalTitle")}>
      <form
        onSubmit={handleSubmit}
        style={{
          display: "flex",
          flexDirection: "column",
          gap: "1rem",
          minWidth: "320px",
          maxWidth: "min(96vw, 680px)"
        }}
      >
        <p style={{ margin: 0, fontSize: "0.875rem", color: "var(--color-content-muted)" }}>
          {t("cash.countModalSubtitle")}
        </p>

        {errorMsg && (
          <div
            role="alert"
            style={{
              padding: "0.75rem",
              background: "var(--color-surface-danger)",
              color: "var(--color-status-danger)",
              borderRadius: "var(--radius-sm)",
              fontSize: "0.875rem"
            }}
          >
            {errorMsg}
          </div>
        )}

        {!effectiveUserId && (
          <div
            role="alert"
            style={{
              padding: "0.75rem",
              background: "var(--color-surface-danger)",
              color: "var(--color-status-danger)",
              borderRadius: "var(--radius-sm)",
              fontSize: "0.875rem"
            }}
          >
            {t("cash.custodianUnavailable")}
          </div>
        )}

        {resolvedExpected == null && !isPositionLoading && (
          <div
            role="alert"
            style={{
              padding: "0.75rem",
              background: "var(--color-surface-danger)",
              color: "var(--color-status-danger)",
              borderRadius: "var(--radius-sm)",
              fontSize: "0.875rem"
            }}
          >
            {t("cash.expectedBalanceUnavailable")}
          </div>
        )}

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
          <TextField
            label={t("cash.cutoffDate")}
            type="date"
            value={cutoffDate}
            onChange={(val) => setCutoffDate(val)}
            isRequired
          />
          <div style={{ display: "flex", flexDirection: "column", gap: "0.25rem" }}>
            <label style={{ fontSize: "0.8125rem", fontWeight: 500, color: "var(--color-content-default)" }}>
              {t("cash.expectedAmountLabel")}
            </label>
            <div
              data-testid="expected-cash-balance"
              style={{
                padding: "0.625rem 0.875rem",
                background: "var(--color-surface-page)",
                border: `1px solid ${resolvedExpected == null ? "var(--color-border-danger, #ef4444)" : "var(--color-border-subtle)"}`,
                borderRadius: "var(--radius-sm)",
                display: "flex",
                alignItems: "center",
                justifyContent: "space-between",
                minHeight: "42px"
              }}
            >
              {resolvedExpected != null ? (
                <span style={{ fontWeight: 600, fontSize: "0.875rem", color: "var(--color-content-default)" }}>
                  <BankingMoney amount={resolvedExpected} currency={currencyCode} />
                </span>
              ) : (
                <span style={{ color: "var(--color-status-danger)", fontSize: "0.8125rem" }}>
                  {isPositionLoading ? t("common.loading") : t("cash.expectedBalanceUnavailable")}
                </span>
              )}
            </div>
          </div>
        </div>

        {/* Authenticated Custodian Badge (Zero raw UUID entry) */}
        <div style={{ display: "flex", flexDirection: "column", gap: "0.25rem" }}>
          <label style={{ fontSize: "0.8125rem", fontWeight: 500, color: "var(--color-content-default)" }}>
            {t("cash.custodian")}
          </label>
          <div
            data-testid="cash-custodian-badge"
            style={{
              padding: "0.625rem 0.875rem",
              background: "var(--color-surface-page)",
              border: `1px solid ${!effectiveUserId ? "var(--color-border-danger, #ef4444)" : "var(--color-border-subtle)"}`,
              borderRadius: "var(--radius-sm)",
              display: "flex",
              alignItems: "center",
              justifyContent: "space-between",
              minHeight: "42px"
            }}
          >
            {effectiveUserId ? (
              <>
                <span style={{ fontWeight: 600, fontSize: "0.875rem", color: "var(--color-content-default)" }}>
                  {user?.displayName || effectiveUserId}
                </span>
                {user?.email && (
                  <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
                    {user.email}
                  </span>
                )}
              </>
            ) : (
              <span style={{ color: "var(--color-status-danger)", fontSize: "0.8125rem" }}>
                {t("cash.custodianUnavailable")}
              </span>
            )}
          </div>
        </div>

        {/* Denomination Grid */}
        <div
          style={{
            maxHeight: "280px",
            overflowY: "auto",
            overflowX: "auto",
            border: "1px solid var(--color-border-subtle)",
            borderRadius: "var(--radius-sm)"
          }}
        >
          <Table style={{ minWidth: "480px" }}>
            <TableHeader>
              <TableRow>
                <TableHead style={{ minWidth: "120px", whiteSpace: "nowrap" }}>{t("cash.denomination")}</TableHead>
                <TableHead style={{ minWidth: "90px", whiteSpace: "nowrap" }} align="end">{t("cash.unitValue")}</TableHead>
                <TableHead style={{ width: "120px", minWidth: "100px", whiteSpace: "nowrap" }}>{t("cash.quantity")}</TableHead>
                <TableHead style={{ minWidth: "110px", whiteSpace: "nowrap" }} align="end">{t("cash.lineTotal")}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {denominations.map((row, index) => {
                const lineTotal = row.unitValue * row.quantity;
                return (
                  <TableRow key={row.denomination}>
                    <TableCell style={{ fontWeight: 600, whiteSpace: "nowrap" }}>
                      <BdiText>{row.denomination} {currencyCode}</BdiText>
                    </TableCell>
                    <TableCell align="end" style={{ whiteSpace: "nowrap" }}>
                      <BankingMoney amount={row.unitValue} currency={currencyCode} />
                    </TableCell>
                    <TableCell>
                      <input
                        type="number"
                        min="0"
                        value={row.quantity || ""}
                        placeholder="0"
                        onChange={(e) => handleQuantityChange(index, e.target.value)}
                        style={{
                          width: "100%",
                          padding: "0.375rem 0.5rem",
                          border: "1px solid var(--color-border-default)",
                          borderRadius: "var(--radius-sm)",
                          textAlign: "center"
                        }}
                        aria-label={t("cash.countForDenom", { denom: row.denomination })}
                      />
                    </TableCell>
                    <TableCell align="end" style={{ fontWeight: 600, whiteSpace: "nowrap" }}>
                      <BankingMoney amount={lineTotal} currency={currencyCode} />
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        </div>

        {/* Summary Footer */}
        <div
          style={{
            display: "grid",
            gridTemplateColumns: "1fr 1fr",
            gap: "1rem",
            background: "var(--color-surface-page)",
            padding: "1rem",
            borderRadius: "var(--radius-sm)",
            border: "1px solid var(--color-border-subtle)"
          }}
        >
          <div>
            <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", display: "block" }}>
              {t("cash.totalCountedLabel")}
            </span>
            <span style={{ fontSize: "1.25rem", fontWeight: 700, color: "var(--color-brand-primary)" }}>
              <BankingMoney amount={totalCounted} currency={currencyCode} />
            </span>
          </div>
          <div>
            <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", display: "block" }}>
              {t("cash.differenceLabel")}
            </span>
            <span
              style={{
                fontSize: "1.25rem",
                fontWeight: 700,
                color:
                  variance == null
                    ? "var(--color-content-muted)"
                    : variance === 0
                    ? "#16a34a"
                    : variance < 0
                    ? "var(--color-status-danger)"
                    : "#2563eb"
              }}
            >
              {variance != null ? (
                <BankingMoney amount={variance} currency={currencyCode} />
              ) : (
                "—"
              )}
            </span>
          </div>
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "0.5rem" }}>
          <Button type="button" variant="secondary" onClick={onClose} isDisabled={createMutation.isPending}>
            {t("accounts.cancel")}
          </Button>
          <Button type="submit" data-testid="submit-cash-count" variant="primary" isDisabled={isSubmissionDisabled}>
            {createMutation.isPending ? t("common.loading") : t("cash.countSubmit")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

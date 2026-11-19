import React, { useState, useEffect, useCallback } from "react";
import { useTranslation } from "react-i18next";
import {
  useAddMatch,
  useFinalizeReconciliation,
  useReconciliationSuggestions,
  useReconciliationClaims,
  useStatementLines,
  useTreasuryAccount,
  useReconciliation
} from "../../hooks/use-banking-queries.ts";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "../../../../components/primitives/table.tsx";
import { BankingMoney } from "../../components/banking-money.tsx";
import { StatusBadge } from "../../../../components/fields/status-badge.tsx";
import { BdiText } from "../../../../components/formatting/bidi-text.tsx";
import { formatDate } from "../../../../components/formatting/date-formatter.ts";
import { CheckCircleIcon, AlertTriangleIcon } from "../../components/banking-icons.tsx";
import { paymentApi } from "../../../payments/public.ts";
import type { PaymentRecord } from "../../../payments/public.ts";
import type { ReconciliationDto, StatementLineDto } from "../../types.ts";

interface Props {
  readonly legalEntityId: string;
  readonly reconciliation: ReconciliationDto | null;
  readonly isOpen: boolean;
  readonly onClose: () => void;
}

export function ReconciliationWorkbenchModal({
  legalEntityId,
  reconciliation,
  isOpen,
  onClose
}: Props): React.JSX.Element | null {
  const { t } = useTranslation("banking");

  const [selectedLineIds, setSelectedLineIds] = useState<string[]>([]);
  const [selectedLinesMap, setSelectedLinesMap] = useState<Map<string, StatementLineDto>>(new Map());
  const [selectedPaymentsMap, setSelectedPaymentsMap] = useState<
    Map<string, { payment: PaymentRecord; matchedAmount: number }>
  >(new Map());
  const [isDirectAdjustment, setIsDirectAdjustment] = useState<boolean>(false);
  const [systemAmountInput, setSystemAmountInput] = useState<string>("");
  const [explanation, setExplanation] = useState("");
  const [availablePayments, setAvailablePayments] = useState<readonly PaymentRecord[]>([]);
  const [paymentSearch, setPaymentSearch] = useState("");
  const [paymentPage, setPaymentPage] = useState(1);
  const [linePage, setLinePage] = useState(1);
  const [hasMorePayments, setHasMorePayments] = useState(false);
  const [isLoadingPayments, setIsLoadingPayments] = useState(false);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  // Queries
  const { data: liveSession, refetch: refetchSession } = useReconciliation(
    legalEntityId,
    reconciliation?.id ?? ""
  );
  const currentSession = liveSession ?? reconciliation;

  const { data: treasuryAccount } = useTreasuryAccount(
    legalEntityId,
    currentSession?.treasuryAccountId ?? ""
  );
  const activeCurrency = treasuryAccount?.currencyCode ?? null;
  const displayCurrency = activeCurrency ?? "—";

  const statementId = currentSession?.statementId ?? "";

  const { data: linesData, refetch: refetchLines } = useStatementLines(
    legalEntityId,
    statementId,
    linePage,
    50
  );
  const { data: suggestions, refetch: refetchSuggestions } = useReconciliationSuggestions(
    legalEntityId,
    currentSession?.id ?? ""
  );
  const { data: claimsData, refetch: refetchClaims } = useReconciliationClaims(
    legalEntityId,
    currentSession?.id ?? ""
  );
  const claims = claimsData ?? {};

  const addMatchMutation = useAddMatch(legalEntityId, currentSession?.id ?? "");
  const finalizeMutation = useFinalizeReconciliation(legalEntityId, currentSession?.id ?? "");

  const lines = linesData?.items ?? [];
  const selectedLines = Array.from(selectedLinesMap.values());
  const lineDirection = selectedLines[0]?.direction;
  const expectedPaymentDirection =
    lineDirection === "Credit" ? "Incoming" : lineDirection === "Debit" ? "Outgoing" : undefined;

  // Load eligible completed payments for this treasury account, currency & direction
  const fetchPayments = useCallback(async () => {
    if (!isOpen || !legalEntityId || !activeCurrency) {
      setAvailablePayments([]);
      setHasMorePayments(false);
      return;
    }
    setIsLoadingPayments(true);
    try {
      const res = await paymentApi.listPayments({
        legalEntityId,
        paymentAccountId: currentSession?.treasuryAccountId || undefined,
        direction: expectedPaymentDirection,
        page: paymentPage,
        pageSize: 20,
        state: "Completed",
        search: paymentSearch.trim() || undefined,
        currencyCode: activeCurrency
      });
      setAvailablePayments(res.items ?? []);
      setHasMorePayments(res.hasMore);
    } catch {
      setAvailablePayments([]);
    } finally {
      setIsLoadingPayments(false);
    }
  }, [isOpen, legalEntityId, currentSession?.treasuryAccountId, expectedPaymentDirection, paymentPage, paymentSearch, activeCurrency]);

  useEffect(() => {
    void fetchPayments();
  }, [fetchPayments]);

  useEffect(() => {
    if (!isOpen) return;
    setLinePage(1);
    setPaymentPage(1);
    setSelectedLineIds([]);
    setSelectedLinesMap(new Map());
    setSelectedPaymentsMap(new Map());
  }, [isOpen, reconciliation?.id]);

  if (!isOpen || !currentSession) return null;

  const externalSum = selectedLines.reduce(
    (acc, l) => acc + (l.amount - l.claimedAmount),
    0
  );

  const selectedPaymentsList = Array.from(selectedPaymentsMap.values());

  const calculatedSystemSum = isDirectAdjustment
    ? 0
    : selectedPaymentsList.length > 0
    ? selectedPaymentsList.reduce((acc, item) => acc + item.matchedAmount, 0)
    : systemAmountInput
    ? parseFloat(systemAmountInput) || 0
    : externalSum;

  const matchDiff = Math.round((externalSum - calculatedSystemSum) * 100) / 100;

  const handleToggleLine = (line: { id: string; currencyCode?: string; direction?: string }) => {
    if (!activeCurrency) {
      setErrorMsg(t("reconciliation.accountCurrencyUnavailable"));
      return;
    }
    if (line.currencyCode && line.currencyCode !== activeCurrency) {
      setErrorMsg(t("reconciliation.crossCurrencyForbidden"));
      return;
    }
    if (selectedLines.length > 0 && !selectedLineIds.includes(line.id) && line.direction && line.direction !== lineDirection) {
      setErrorMsg(t("reconciliation.directionMismatch"));
      return;
    }
    setErrorMsg(null);
    setSelectedLineIds((prev) => prev.includes(line.id) ? prev.filter((id) => id !== line.id) : [...prev, line.id]);
    setSelectedLinesMap((prev) => {
      const next = new Map(prev);
      if (next.has(line.id)) next.delete(line.id);
      else {
        const selectedLine = lines.find((item) => item.id === line.id);
        if (selectedLine) next.set(line.id, selectedLine);
      }
      return next;
    });
  };

  const handleTogglePayment = (p: PaymentRecord) => {
    if (!activeCurrency) {
      setErrorMsg(t("reconciliation.accountCurrencyUnavailable"));
      return;
    }
    if (p.currencyCode && p.currencyCode !== activeCurrency) {
      setErrorMsg(t("reconciliation.crossCurrencyForbidden"));
      return;
    }
    if (p.paymentAccountId && currentSession?.treasuryAccountId && p.paymentAccountId !== currentSession.treasuryAccountId) {
      setErrorMsg(t("reconciliation.paymentAccountMismatch"));
      return;
    }
    if (expectedPaymentDirection && p.direction !== expectedPaymentDirection) {
      setErrorMsg(t("reconciliation.directionMismatch"));
      return;
    }
    const rawAmount = parseFloat(p.amount) || 0;
    const claimed = claims[p.id] || 0;
    const remainingAmount = Math.max(0, Math.round((rawAmount - claimed) * 100) / 100);
    if (remainingAmount <= 0) {
      setErrorMsg(t("reconciliation.paymentAlreadyClaimed"));
      return;
    }

    setErrorMsg(null);
    setSelectedPaymentsMap((prev) => {
      const next = new Map(prev);
      if (next.has(p.id)) {
        next.delete(p.id);
      } else {
        next.set(p.id, { payment: p, matchedAmount: remainingAmount });
      }
      return next;
    });
  };

  const handleConfirmMatch = async () => {
    setErrorMsg(null);
    if (selectedLineIds.length === 0) {
      setErrorMsg(t("common.validationError"));
      return;
    }

    if (!isDirectAdjustment && calculatedSystemSum > 0 && selectedPaymentsList.length === 0) {
      setErrorMsg(t("reconciliation.selectPaymentRequired"));
      return;
    }

    const paymentReferences = isDirectAdjustment
      ? []
      : selectedPaymentsList.map((item) => ({
          paymentId: item.payment.id,
          amount: item.matchedAmount
        }));

    try {
      await addMatchMutation.mutateAsync({
        statementLineIds: selectedLineIds,
        payments: paymentReferences,
        externalAmount: externalSum,
        systemAmount: calculatedSystemSum,
        difference: matchDiff,
        source: isDirectAdjustment ? "BankFeeAdjustment" : "ManualWorkbench",
        explanation:
          explanation.trim() ||
          (isDirectAdjustment ? t("reconciliation.directAdjustmentNotice") : null)
      });
      setSelectedLineIds([]);
      setSelectedLinesMap(new Map());
      setSelectedPaymentsMap(new Map());
      setSystemAmountInput("");
      setExplanation("");
      setIsDirectAdjustment(false);
      void refetchLines();
      void refetchSuggestions();
      void refetchClaims();
      void refetchSession();
    } catch (err: unknown) {
      const errStr = err instanceof Error ? err.message : String(err);
      if (errStr.includes("409") || errStr.toLowerCase().includes("version") || errStr.toLowerCase().includes("conflict")) {
        setErrorMsg(t("reconciliation.versionConflict"));
      } else {
        setErrorMsg(errStr || t("common.error"));
      }
    }
  };

  const handleFinalize = async () => {
    setErrorMsg(null);
    try {
      await finalizeMutation.mutateAsync(currentSession.version);
      onClose();
    } catch (err: unknown) {
      const errStr = err instanceof Error ? err.message : String(err);
      if (errStr.includes("409") || errStr.toLowerCase().includes("version") || errStr.toLowerCase().includes("conflict")) {
        setErrorMsg(t("reconciliation.versionConflict"));
      } else {
        setErrorMsg(errStr || t("common.error"));
      }
    }
  };

  const canFinalize =
    currentSession.state === "Open" &&
    currentSession.difference === 0 &&
    currentSession.matchCount > 0;

  return (
    <Dialog isOpen={isOpen} onClose={onClose} title={t("reconciliation.workbenchTitle")}>
      <div style={{ minWidth: "360px", maxWidth: "min(96vw, 1250px)", display: "flex", flexDirection: "column", gap: "1rem" }}>
        {/* Session Status Banner */}
        <div
          style={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            background: "var(--color-surface-page)",
            padding: "0.875rem 1.25rem",
            borderRadius: "var(--radius-sm)",
            border: "1px solid var(--color-border-subtle)",
            flexWrap: "wrap",
            gap: "0.75rem"
          }}
        >
          <div>
            <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
              {t("reconciliation.asOfDate")}:
            </span>{" "}
            <span style={{ fontWeight: 600 }}>
              <BdiText>{formatDate(currentSession.asOfDate)}</BdiText>
            </span>
          </div>
          <div>
            <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
              {t("reconciliation.matchingCurrency")}:
            </span>{" "}
            <span style={{ fontWeight: 700, color: "var(--color-brand-primary)" }}>
              <BdiText>{displayCurrency}</BdiText>
            </span>
          </div>
          <div>
            <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
              {t("reconciliation.externalBalance")}:
            </span>{" "}
            <span style={{ fontWeight: 600 }}>
              <BankingMoney amount={currentSession.externalBalance} currency={displayCurrency} />
            </span>
          </div>
          <div>
            <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
              {t("reconciliation.bookBalance")}:
            </span>{" "}
            <span style={{ fontWeight: 600 }}>
              {currentSession.bookBalance != null ? (
                <BankingMoney amount={currentSession.bookBalance} currency={displayCurrency} />
              ) : (
                "—"
              )}
            </span>
          </div>
          <div>
            <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
              {t("reconciliation.difference")}:
            </span>{" "}
            <span
              style={{
                fontWeight: 600,
                color: currentSession.difference === 0 ? "#16a34a" : "var(--color-status-danger)"
              }}
            >
              {currentSession.difference != null ? (
                <BankingMoney amount={currentSession.difference} currency={displayCurrency} />
              ) : (
                "—"
              )}
            </span>
          </div>
          <StatusBadge
            intent={currentSession.state === "Finalized" ? "success" : "neutral"}
            label={currentSession.state}
          />
        </div>

        {errorMsg && (
          <div
            role="alert"
            style={{
              padding: "0.75rem 1rem",
              background: "var(--color-surface-danger)",
              color: "var(--color-status-danger)",
              borderRadius: "var(--radius-sm)",
              fontSize: "0.875rem",
              display: "flex",
              justifyContent: "space-between",
              alignItems: "center",
              gap: "0.5rem"
            }}
          >
            <span>{errorMsg}</span>
            {errorMsg === t("reconciliation.versionConflict") && (
              <Button size="compact" variant="secondary" onClick={() => { void refetchSession(); setErrorMsg(null); }}>
                {t("reconciliation.reloadSession")}
              </Button>
            )}
          </div>
        )}

        {!activeCurrency && (
          <div role="alert" style={{ padding: "0.75rem 1rem", background: "#fffbeb", color: "#b45309", border: "1px solid #fde68a", borderRadius: "var(--radius-sm)", fontSize: "0.875rem" }}>
            {t("reconciliation.accountCurrencyUnavailable")}
          </div>
        )}

        {!statementId && (
          <div
            role="alert"
            style={{
              padding: "0.75rem 1rem",
              background: "#fffbeb",
              color: "#b45309",
              border: "1px solid #fde68a",
              borderRadius: "var(--radius-sm)",
              fontSize: "0.875rem"
            }}
          >
            {t("reconciliation.noLinkedStatement")}
          </div>
        )}

        {/* Smart Suggestions Ribbon (if available) */}
        {(suggestions ?? []).length > 0 && (
          <div
            style={{
              background: "#f0fdf4",
              border: "1px solid #bbf7d0",
              padding: "0.875rem",
              borderRadius: "var(--radius-sm)"
            }}
          >
            <div
              style={{
                fontWeight: 600,
                fontSize: "0.875rem",
                color: "#166534",
                marginBottom: "0.5rem",
                display: "flex",
                alignItems: "center",
                gap: "0.5rem"
              }}
            >
              <CheckCircleIcon />
              {t("reconciliation.suggestionsTitle")}
            </div>
            <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem" }}>
              {suggestions?.map((s) => (
                <div
                  key={s.statementLineId}
                  style={{
                    display: "flex",
                    justifyContent: "space-between",
                    alignItems: "center",
                    fontSize: "0.8125rem"
                  }}
                >
                  <span>
                    {s.reason} &bull;{" "}
                    <BankingMoney amount={s.amount} currency={s.currencyCode} />
                  </span>
                  <Button
                    size="compact"
                    variant="secondary"
                    onClick={() => {
                      const suggestedLine = lines.find((line) => line.id === s.statementLineId);
                      if (!suggestedLine) return;
                      setSelectedLineIds([s.statementLineId]);
                      setSelectedLinesMap(new Map([[suggestedLine.id, suggestedLine]]));
                      setSystemAmountInput(String(s.amount));
                    }}
                  >
                    {t("reconciliation.applySuggestion")}
                  </Button>
                </div>
              ))}
            </div>
          </div>
        )}

        {/* Grid: Statement Lines (Left) vs Ledger Payments (Right) */}
        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          {/* External Statement Lines */}
          <div style={{ display: "flex", flexDirection: "column" }}>
            <div
              style={{
                fontSize: "0.875rem",
                fontWeight: 600,
                marginBottom: "0.5rem",
                color: "var(--color-brand-primary)"
              }}
            >
                {t("reconciliation.externalLinesTitle")} ({linesData?.totalCount ?? lines.length})
            </div>
            {(linePage > 1 || Boolean(linesData?.hasMore)) && (
              <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginTop: "0.375rem", fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
                <Button size="compact" variant="secondary" isDisabled={linePage <= 1} onClick={() => setLinePage((p) => Math.max(1, p - 1))}>&larr;</Button>
                <span>{linePage}</span>
                <Button size="compact" variant="secondary" isDisabled={!linesData?.hasMore} onClick={() => setLinePage((p) => p + 1)}>&rarr;</Button>
              </div>
            )}
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
                    <TableHead style={{ width: "36px", minWidth: "36px" }}></TableHead>
                    <TableHead style={{ minWidth: "120px", whiteSpace: "nowrap" }}>
                      {t("statements.lineExternalId")}
                    </TableHead>
                    <TableHead style={{ minWidth: "90px", whiteSpace: "nowrap" }}>
                      {t("statements.direction")}
                    </TableHead>
                    <TableHead style={{ minWidth: "120px", whiteSpace: "nowrap" }} align="end">
                      {t("statements.amount")}
                    </TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {lines.length === 0 ? (
                    <TableRow>
                      <TableCell
                        colSpan={4}
                        style={{
                          textAlign: "center",
                          color: "var(--color-content-muted)",
                          padding: "1.5rem"
                        }}
                      >
                        {t("common.noData")}
                      </TableCell>
                    </TableRow>
                  ) : (
                    lines.map((line) => {
                      const isSelected = selectedLineIds.includes(line.id);
                      const remaining = line.amount - line.claimedAmount;
                      return (
                        <TableRow
                          key={line.id}
                          onClick={() => handleToggleLine(line)}
                          style={{
                            cursor: "pointer",
                            background: isSelected ? "var(--color-surface-hover)" : "transparent"
                          }}
                        >
                          <TableCell style={{ width: "36px" }}>
                            <input
                              type="checkbox"
                              data-testid={`statement-line-${line.id}`}
                              checked={isSelected}
                              onClick={(event) => event.stopPropagation()}
                              onChange={() => handleToggleLine(line)}
                              aria-label={line.externalLineId}
                            />
                          </TableCell>
                          <TableCell style={{ fontWeight: 600, whiteSpace: "nowrap" }}>
                            <BdiText>{line.externalLineId}</BdiText>
                          </TableCell>
                          <TableCell style={{ whiteSpace: "nowrap" }}>
                            <StatusBadge
                              intent={line.direction === "Credit" ? "success" : "neutral"}
                              label={line.direction === "Credit" ? t("reports.credit") : t("reports.debit")}
                            />
                          </TableCell>
                          <TableCell align="end" style={{ whiteSpace: "nowrap" }}>
                            <BankingMoney amount={remaining} currency={line.currencyCode} />
                          </TableCell>
                        </TableRow>
                      );
                    })
                  )}
                </TableBody>
              </Table>
            </div>
          </div>

          {/* System Ledger Payments */}
          <div style={{ display: "flex", flexDirection: "column" }}>
            <div
              style={{
                display: "flex",
                justifyContent: "space-between",
                alignItems: "center",
                marginBottom: "0.5rem"
              }}
            >
              <span
                style={{
                  fontSize: "0.875rem",
                  fontWeight: 600,
                  color: "var(--color-brand-primary)"
                }}
              >
                {t("reconciliation.ledgerPaymentsTitle")} ({availablePayments.length})
              </span>
              <label
                style={{
                  fontSize: "0.75rem",
                  display: "flex",
                  alignItems: "center",
                  gap: "0.25rem",
                  cursor: "pointer"
                }}
              >
                <input
                  type="checkbox"
                  checked={isDirectAdjustment}
                  onChange={(e) => setIsDirectAdjustment(e.target.checked)}
                />
                {t("reconciliation.directAdjustmentCheckbox")}
              </label>
            </div>

            {!isDirectAdjustment && selectedPaymentsList.length > 0 && (
              <div
                style={{
                  padding: "0.5rem 0.75rem",
                  background: "var(--color-surface-page)",
                  border: "1px solid var(--color-border-subtle)",
                  borderRadius: "var(--radius-sm)",
                  fontSize: "0.8125rem",
                  display: "flex",
                  justifyContent: "space-between",
                  alignItems: "center",
                  marginBottom: "0.5rem"
                }}
              >
                <span style={{ fontWeight: 600, color: "var(--color-brand-primary)" }}>
                  {t("reconciliation.selectedCountLabel", { count: selectedPaymentsList.length })}
                </span>
                <span style={{ fontWeight: 600 }}>
                  <BankingMoney
                    amount={selectedPaymentsList.reduce((acc, item) => acc + item.matchedAmount, 0)}
                    currency={displayCurrency}
                  />
                </span>
              </div>
            )}

            {!isDirectAdjustment && (
              <div style={{ marginBottom: "0.5rem" }}>
                <TextField
                  data-testid="reconciliation-payment-search"
                  value={paymentSearch}
                  onChange={(val) => {
                    setPaymentSearch(val);
                    setPaymentPage(1);
                  }}
                  placeholder={t("reconciliation.searchPaymentsPlaceholder")}
                />
              </div>
            )}

            <div
              style={{
                maxHeight: "240px",
                overflowY: "auto",
                overflowX: "auto",
                border: "1px solid var(--color-border-subtle)",
                borderRadius: "var(--radius-sm)"
              }}
            >
              {isDirectAdjustment ? (
                <div
                  style={{
                    padding: "2rem",
                    textAlign: "center",
                    color: "var(--color-content-muted)",
                    fontSize: "0.8125rem"
                  }}
                >
                  {t("reconciliation.directAdjustmentNotice")}
                </div>
              ) : (
                <Table style={{ minWidth: "480px" }}>
                  <TableHeader>
                    <TableRow>
                      <TableHead style={{ width: "36px", minWidth: "36px" }}></TableHead>
                      <TableHead style={{ minWidth: "120px", whiteSpace: "nowrap" }}>
                        {t("reconciliation.paymentNumber")}
                      </TableHead>
                      <TableHead style={{ minWidth: "90px", whiteSpace: "nowrap" }}>
                        {t("statements.direction")}
                      </TableHead>
                      <TableHead style={{ minWidth: "120px", whiteSpace: "nowrap" }} align="end">
                        {t("reconciliation.remainingMatchable")}
                      </TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {isLoadingPayments ? (
                      <TableRow>
                        <TableCell colSpan={4} style={{ textAlign: "center", padding: "1.5rem" }}>
                          {t("common.loading")}
                        </TableCell>
                      </TableRow>
                    ) : availablePayments.length === 0 ? (
                      <TableRow>
                        <TableCell
                          colSpan={4}
                          style={{
                            textAlign: "center",
                            color: "var(--color-content-muted)",
                            padding: "1.5rem"
                          }}
                        >
                          {t("reconciliation.noPaymentsAvailable")}
                        </TableCell>
                      </TableRow>
                    ) : (
                      availablePayments.map((p) => {
                        const isSelected = selectedPaymentsMap.has(p.id);
                        const rawAmount = parseFloat(p.amount) || 0;
                        const claimed = claims[p.id] || 0;
                        const remainingAmount = Math.max(0, Math.round((rawAmount - claimed) * 100) / 100);
                        const isFullyClaimed = remainingAmount <= 0;

                        return (
                          <TableRow
                            key={p.id}
                            onClick={() => {
                              if (!isFullyClaimed) handleTogglePayment(p);
                            }}
                            style={{
                              cursor: isFullyClaimed ? "not-allowed" : "pointer",
                              background: isSelected ? "var(--color-surface-hover)" : "transparent",
                              opacity: isFullyClaimed ? 0.6 : 1
                            }}
                          >
                            <TableCell style={{ width: "36px" }}>
                              <input
                                type="checkbox"
                                data-testid={`reconciliation-payment-${p.id}`}
                                checked={isSelected}
                                disabled={isFullyClaimed}
                                onClick={(event) => event.stopPropagation()}
                                onChange={() => handleTogglePayment(p)}
                                aria-label={p.number}
                              />
                            </TableCell>
                            <TableCell style={{ fontWeight: 600, whiteSpace: "nowrap" }}>
                              <BdiText>{p.number}</BdiText>
                            </TableCell>
                            <TableCell style={{ whiteSpace: "nowrap" }}>
                              <StatusBadge
                                intent={p.direction === "Incoming" ? "success" : "neutral"}
                                label={p.direction === "Incoming" ? t("reports.credit") : t("reports.debit")}
                              />
                            </TableCell>
                            <TableCell align="end" style={{ whiteSpace: "nowrap" }}>
                              <BankingMoney
                                amount={remainingAmount}
                                currency={p.currencyCode || displayCurrency}
                              />
                              {isFullyClaimed && (
                                <span style={{ fontSize: "0.75rem", color: "var(--color-status-danger)", display: "block" }}>
                                  {t("reconciliation.fullyClaimedBadge")}
                                </span>
                              )}
                            </TableCell>
                          </TableRow>
                        );
                      })
                    )}
                  </TableBody>
                </Table>
              )}
            </div>

            {!isDirectAdjustment && (paymentPage > 1 || hasMorePayments) && (
              <div
                style={{
                  display: "flex",
                  justifyContent: "space-between",
                  alignItems: "center",
                  marginTop: "0.375rem",
                  fontSize: "0.75rem",
                  color: "var(--color-content-muted)"
                }}
              >
                <Button
                  size="compact"
                  variant="secondary"
                  isDisabled={paymentPage <= 1}
                  onClick={() => setPaymentPage((p) => Math.max(1, p - 1))}
                >
                  &larr;
                </Button>
                <span>
                  {paymentPage}
                </span>
                <Button
                  size="compact"
                  variant="secondary"
                  isDisabled={!hasMorePayments}
                  onClick={() => setPaymentPage((p) => p + 1)}
                >
                  &rarr;
                </Button>
              </div>
            )}
          </div>
        </div>

        {/* Selected Match Arithmetic Bar */}
        <div
          style={{
            padding: "1rem",
            background: "var(--color-surface-page)",
            borderRadius: "var(--radius-sm)",
            border: "1px solid var(--color-border-subtle)"
          }}
        >
          <div
            style={{
              display: "grid",
              gridTemplateColumns: "repeat(auto-fit, minmax(180px, 1fr))",
              gap: "0.75rem",
              alignItems: "center"
            }}
          >
            <div>
              <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", display: "block" }}>
                {t("reconciliation.selectedExternal")}
              </span>
              <span style={{ fontSize: "1.125rem", fontWeight: 700 }}>
                <BankingMoney amount={externalSum} currency={displayCurrency} />
              </span>
            </div>
            <div>
              <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", display: "block" }}>
                {t("reconciliation.selectedSystem")}
              </span>
              <span style={{ fontSize: "1.125rem", fontWeight: 700 }}>
                <BankingMoney amount={calculatedSystemSum} currency={displayCurrency} />
              </span>
            </div>
            <div>
              <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", display: "block" }}>
                {t("reconciliation.workbenchDiff")}
              </span>
              <span
                style={{
                  fontSize: "1.125rem",
                  fontWeight: 700,
                  color: matchDiff === 0 ? "#16a34a" : "var(--color-status-danger)"
                }}
              >
                <BankingMoney amount={matchDiff} currency={displayCurrency} />
              </span>
            </div>
          </div>

          <div style={{ marginTop: "0.75rem" }}>
            <TextField
              label={t("reconciliation.explanationLabel")}
              value={explanation}
              onChange={(val) => setExplanation(val)}
              placeholder={t("reconciliation.explanationPlaceholder")}
            />
          </div>

          <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "1rem" }}>
            <Button
              variant="primary"
              onClick={handleConfirmMatch}
              data-testid="confirm-reconciliation-match"
              isDisabled={!activeCurrency || selectedLineIds.length === 0 || addMatchMutation.isPending}
            >
              {addMatchMutation.isPending ? t("common.loading") : t("reconciliation.confirmMatchButton")}
            </Button>
          </div>
        </div>

        {/* Finalize Notice & Actions Footer */}
        <div
          style={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            borderTop: "1px solid var(--color-border-subtle)",
            paddingTop: "1rem",
            flexWrap: "wrap",
            gap: "0.75rem"
          }}
        >
          <div
            style={{
              fontSize: "0.8125rem",
              color: "var(--color-content-muted)",
              display: "flex",
              alignItems: "center",
              gap: "0.5rem"
            }}
          >
            <AlertTriangleIcon />
            <span>
              {!canFinalize && currentSession.state === "Open"
                ? t("reconciliation.cannotFinalizeNotice")
                : t("reconciliation.finalizeNotice")}
            </span>
          </div>
          <div style={{ display: "flex", gap: "0.75rem" }}>
            <Button variant="secondary" onClick={onClose}>
              {t("common.close")}
            </Button>
            {currentSession.state === "Open" && (
              <Button
                variant="primary"
                onClick={handleFinalize}
                data-testid="finalize-reconciliation"
                isDisabled={!canFinalize || finalizeMutation.isPending}
              >
                {finalizeMutation.isPending ? t("common.loading") : t("reconciliation.finalizeAction")}
              </Button>
            )}
          </div>
        </div>
      </div>
    </Dialog>
  );
}

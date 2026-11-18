import React, { useState, useEffect, useCallback } from "react";
import { useTranslation } from "react-i18next";
import {
  useAddMatch,
  useFinalizeReconciliation,
  useReconciliationSuggestions,
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
import type { ReconciliationDto } from "../../types.ts";

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
  const [selectedPaymentIds, setSelectedPaymentIds] = useState<string[]>([]);
  const [isDirectAdjustment, setIsDirectAdjustment] = useState<boolean>(false);
  const [systemAmountInput, setSystemAmountInput] = useState<string>("");
  const [explanation, setExplanation] = useState("");
  const [availablePayments, setAvailablePayments] = useState<readonly PaymentRecord[]>([]);
  const [paymentSearch, setPaymentSearch] = useState("");
  const [paymentPage, setPaymentPage] = useState(1);
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
  const activeCurrency = treasuryAccount?.currencyCode || "EGP";

  const statementId = currentSession?.statementId ?? "";

  const { data: linesData, refetch: refetchLines } = useStatementLines(
    legalEntityId,
    statementId,
    1,
    50
  );
  const { data: suggestions, refetch: refetchSuggestions } = useReconciliationSuggestions(
    legalEntityId,
    currentSession?.id ?? ""
  );

  const addMatchMutation = useAddMatch(legalEntityId, currentSession?.id ?? "");
  const finalizeMutation = useFinalizeReconciliation(legalEntityId, currentSession?.id ?? "");

  // Load eligible completed payments for this legal entity & currency
  const fetchPayments = useCallback(async () => {
    if (!isOpen || !legalEntityId) return;
    setIsLoadingPayments(true);
    try {
      const res = await paymentApi.listPayments({
        legalEntityId,
        page: paymentPage,
        pageSize: 20,
        state: "Completed",
        search: paymentSearch.trim() || undefined
      });
      // Authoritatively filter by account currency to prevent cross-currency mismatch
      const currencyFiltered = (res.items ?? []).filter(
        (p) => !p.currencyCode || p.currencyCode === activeCurrency
      );
      setAvailablePayments(currencyFiltered);
      setHasMorePayments(res.hasMore);
    } catch {
      setAvailablePayments([]);
    } finally {
      setIsLoadingPayments(false);
    }
  }, [isOpen, legalEntityId, paymentPage, paymentSearch, activeCurrency]);

  useEffect(() => {
    void fetchPayments();
  }, [fetchPayments]);

  if (!isOpen || !currentSession) return null;

  const lines = linesData?.items ?? [];
  const selectedLines = lines.filter((l) => selectedLineIds.includes(l.id));

  const externalSum = selectedLines.reduce(
    (acc, l) => acc + (l.amount - l.claimedAmount),
    0
  );

  const selectedPayments = availablePayments.filter((p) =>
    selectedPaymentIds.includes(p.id)
  );

  const calculatedSystemSum = isDirectAdjustment
    ? 0
    : selectedPayments.length > 0
    ? selectedPayments.reduce((acc, p) => acc + (parseFloat(p.amount) || 0), 0)
    : systemAmountInput
    ? parseFloat(systemAmountInput) || 0
    : externalSum;

  const matchDiff = Math.round((externalSum - calculatedSystemSum) * 100) / 100;

  const handleToggleLine = (line: { id: string; currencyCode?: string }) => {
    if (line.currencyCode && line.currencyCode !== activeCurrency) {
      setErrorMsg(t("reconciliation.crossCurrencyForbidden"));
      return;
    }
    setErrorMsg(null);
    setSelectedLineIds((prev) =>
      prev.includes(line.id) ? prev.filter((id) => id !== line.id) : [...prev, line.id]
    );
  };

  const handleTogglePayment = (p: PaymentRecord) => {
    if (p.currencyCode && p.currencyCode !== activeCurrency) {
      setErrorMsg(t("reconciliation.crossCurrencyForbidden"));
      return;
    }
    setErrorMsg(null);
    setSelectedPaymentIds((prev) =>
      prev.includes(p.id) ? prev.filter((id) => id !== p.id) : [...prev, p.id]
    );
  };

  const handleConfirmMatch = async () => {
    setErrorMsg(null);
    if (selectedLineIds.length === 0) {
      setErrorMsg(t("common.validationError"));
      return;
    }

    if (!isDirectAdjustment && calculatedSystemSum > 0 && selectedPayments.length === 0) {
      setErrorMsg(t("reconciliation.selectPaymentRequired"));
      return;
    }

    const paymentReferences = isDirectAdjustment
      ? []
      : selectedPayments.map((p) => ({
          paymentId: p.id,
          amount: parseFloat(p.amount) || 0
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
      setSelectedPaymentIds([]);
      setSystemAmountInput("");
      setExplanation("");
      setIsDirectAdjustment(false);
      void refetchLines();
      void refetchSuggestions();
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
              <BdiText>{activeCurrency}</BdiText>
            </span>
          </div>
          <div>
            <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
              {t("reconciliation.externalBalance")}:
            </span>{" "}
            <span style={{ fontWeight: 600 }}>
              <BankingMoney amount={currentSession.externalBalance} currency={activeCurrency} />
            </span>
          </div>
          <div>
            <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
              {t("reconciliation.bookBalance")}:
            </span>{" "}
            <span style={{ fontWeight: 600 }}>
              {currentSession.bookBalance != null ? (
                <BankingMoney amount={currentSession.bookBalance} currency={activeCurrency} />
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
                <BankingMoney amount={currentSession.difference} currency={activeCurrency} />
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
                      setSelectedLineIds([s.statementLineId]);
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
              {t("reconciliation.externalLinesTitle")} ({lines.length})
            </div>
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
                              checked={isSelected}
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

            {!isDirectAdjustment && (
              <div style={{ marginBottom: "0.5rem" }}>
                <TextField
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
                        {t("statements.amount")}
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
                        const isSelected = selectedPaymentIds.includes(p.id);
                        return (
                          <TableRow
                            key={p.id}
                            onClick={() => handleTogglePayment(p)}
                            style={{
                              cursor: "pointer",
                              background: isSelected ? "var(--color-surface-hover)" : "transparent"
                            }}
                          >
                            <TableCell style={{ width: "36px" }}>
                              <input
                                type="checkbox"
                                checked={isSelected}
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
                                amount={parseFloat(p.amount) || 0}
                                currency={p.currencyCode || activeCurrency}
                              />
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
                <BankingMoney amount={externalSum} currency={activeCurrency} />
              </span>
            </div>
            <div>
              <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", display: "block" }}>
                {t("reconciliation.selectedSystem")}
              </span>
              <span style={{ fontSize: "1.125rem", fontWeight: 700 }}>
                <BankingMoney amount={calculatedSystemSum} currency={activeCurrency} />
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
                <BankingMoney amount={matchDiff} currency={activeCurrency} />
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
              isDisabled={selectedLineIds.length === 0 || addMatchMutation.isPending}
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

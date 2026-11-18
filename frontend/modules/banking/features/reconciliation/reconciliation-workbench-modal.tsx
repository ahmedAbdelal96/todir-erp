import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import {
  useAddMatch,
  useFinalizeReconciliation,
  useReconciliationSuggestions,
  useStatementLines
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
import type { ReconciliationDto } from "../../types.ts";

interface Props {
  readonly legalEntityId: string;
  readonly reconciliation: ReconciliationDto | null;
  readonly isOpen: boolean;
  readonly onClose: () => void;
}

export function ReconciliationWorkbenchModal({ legalEntityId, reconciliation, isOpen, onClose }: Props): React.JSX.Element | null {
  const { t } = useTranslation("banking");

  const [selectedLineIds, setSelectedLineIds] = useState<string[]>([]);
  const [systemAmountInput, setSystemAmountInput] = useState<string>("");
  const [explanation, setExplanation] = useState("");
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  // Queries
  const { data: linesData, refetch: refetchLines } = useStatementLines(legalEntityId, reconciliation?.id ?? "", 1, 50);
  const { data: suggestions, refetch: refetchSuggestions } = useReconciliationSuggestions(legalEntityId, reconciliation?.id ?? "");

  const addMatchMutation = useAddMatch(legalEntityId, reconciliation?.id ?? "");
  const finalizeMutation = useFinalizeReconciliation(legalEntityId, reconciliation?.id ?? "");

  if (!isOpen || !reconciliation) return null;

  const lines = linesData?.items ?? [];
  const selectedLines = lines.filter((l) => selectedLineIds.includes(l.id));
  const externalSum = selectedLines.reduce((acc, l) => acc + (l.amount - l.claimedAmount), 0);
  const systemAmount = systemAmountInput ? parseFloat(systemAmountInput) : externalSum;
  const matchDiff = Math.round((externalSum - systemAmount) * 100) / 100;

  const handleToggleLine = (lineId: string) => {
    setSelectedLineIds((prev) =>
      prev.includes(lineId) ? prev.filter((id) => id !== lineId) : [...prev, lineId]
    );
  };

  const handleConfirmMatch = async () => {
    setErrorMsg(null);
    if (selectedLineIds.length === 0) {
      setErrorMsg(t("common.validationError"));
      return;
    }

    try {
      await addMatchMutation.mutateAsync({
        statementLineIds: selectedLineIds,
        payments: [],
        externalAmount: externalSum,
        systemAmount,
        difference: matchDiff,
        source: "ManualWorkbench",
        explanation: explanation.trim() || null
      });
      setSelectedLineIds([]);
      setSystemAmountInput("");
      setExplanation("");
      void refetchLines();
      void refetchSuggestions();
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : t("common.error");
      setErrorMsg(msg);
    }
  };

  const handleFinalize = async () => {
    setErrorMsg(null);
    try {
      await finalizeMutation.mutateAsync(reconciliation.version);
      onClose();
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : t("common.error");
      setErrorMsg(msg);
    }
  };

  const canFinalize = reconciliation.state === "Open" && (reconciliation.difference === 0 || reconciliation.difference == null);

  return (
    <Dialog isOpen={isOpen} onClose={onClose} title={t("reconciliation.workbenchTitle")}>
      <div style={{ minWidth: "360px", maxWidth: "860px", display: "flex", flexDirection: "column", gap: "1rem" }}>
        {/* Session Status Banner */}
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", background: "var(--color-surface-page)", padding: "0.875rem 1.25rem", borderRadius: "var(--radius-sm)", border: "1px solid var(--color-border-subtle)", flexWrap: "wrap", gap: "0.5rem" }}>
          <div>
            <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>{t("reconciliation.asOfDate")}:</span>{" "}
            <span style={{ fontWeight: 600 }}><BdiText>{formatDate(reconciliation.asOfDate)}</BdiText></span>
          </div>
          <div>
            <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>{t("reconciliation.externalBalance")}:</span>{" "}
            <span style={{ fontWeight: 600 }}><BankingMoney amount={reconciliation.externalBalance} currency="EGP" /></span>
          </div>
          <div>
            <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>{t("reconciliation.bookBalance")}:</span>{" "}
            <span style={{ fontWeight: 600 }}>{reconciliation.bookBalance != null ? <BankingMoney amount={reconciliation.bookBalance} currency="EGP" /> : "—"}</span>
          </div>
          <div>
            <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>{t("reconciliation.difference")}:</span>{" "}
            <span style={{ fontWeight: 600, color: reconciliation.difference === 0 ? "#16a34a" : "var(--color-status-danger)" }}>
              {reconciliation.difference != null ? <BankingMoney amount={reconciliation.difference} currency="EGP" /> : "—"}
            </span>
          </div>
          <StatusBadge
            intent={reconciliation.state === "Finalized" ? "success" : "neutral"}
            label={reconciliation.state}
          />
        </div>

        {errorMsg && (
          <div role="alert" style={{ padding: "0.75rem", background: "var(--color-surface-danger)", color: "var(--color-status-danger)", borderRadius: "var(--radius-sm)", fontSize: "0.875rem" }}>
            {errorMsg}
          </div>
        )}

        {/* Smart Suggestions Ribbon (if available) */}
        {(suggestions ?? []).length > 0 && (
          <div style={{ background: "#f0fdf4", border: "1px solid #bbf7d0", padding: "0.875rem", borderRadius: "var(--radius-sm)" }}>
            <div style={{ fontWeight: 600, fontSize: "0.875rem", color: "#166534", marginBottom: "0.5rem", display: "flex", alignItems: "center", gap: "0.5rem" }}>
              <CheckCircleIcon />
              {t("reconciliation.suggestionsTitle")}
            </div>
            <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem" }}>
              {suggestions?.map((s) => (
                <div key={s.statementLineId} style={{ display: "flex", justifyContent: "space-between", alignItems: "center", fontSize: "0.8125rem" }}>
                  <span>{s.reason} &bull; <BankingMoney amount={s.amount} currency={s.currencyCode} /></span>
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

        {/* External Statement Lines Available for Matching */}
        <div>
          <div style={{ fontSize: "0.9375rem", fontWeight: 600, marginBottom: "0.5rem", color: "var(--color-brand-primary)" }}>
            {t("reconciliation.externalLinesTitle")}
          </div>
          <div style={{ maxHeight: "240px", overflowY: "auto", border: "1px solid var(--color-border-subtle)", borderRadius: "var(--radius-sm)" }}>
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead style={{ width: "40px" }}></TableHead>
                  <TableHead>{t("statements.lineExternalId")}</TableHead>
                  <TableHead>{t("statements.bookingDate")}</TableHead>
                  <TableHead>{t("statements.direction")}</TableHead>
                  <TableHead align="end">{t("statements.amount")}</TableHead>
                  <TableHead align="end">{t("statements.remainingAmount")}</TableHead>
                  <TableHead>{t("statements.reference")}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {lines.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={7} style={{ textAlign: "center", color: "var(--color-content-muted)", padding: "1.5rem" }}>
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
                        onClick={() => handleToggleLine(line.id)}
                        style={{ cursor: "pointer", background: isSelected ? "var(--color-surface-hover)" : "transparent" }}
                      >
                        <TableCell>
                          <input
                            type="checkbox"
                            checked={isSelected}
                            onChange={() => handleToggleLine(line.id)}
                            aria-label={t("reconciliation.selectLineAria", { id: line.externalLineId })}
                          />
                        </TableCell>
                        <TableCell style={{ fontWeight: 600 }}>
                          <BdiText>{line.externalLineId}</BdiText>
                        </TableCell>
                        <TableCell>
                          <BdiText>{formatDate(line.bookingDate)}</BdiText>
                        </TableCell>
                        <TableCell>
                          <StatusBadge
                            intent={line.direction === "Credit" ? "success" : "neutral"}
                            label={line.direction === "Credit" ? t("reports.credit") : t("reports.debit")}
                          />
                        </TableCell>
                        <TableCell align="end">
                          <BankingMoney amount={line.amount} currency={line.currencyCode} />
                        </TableCell>
                        <TableCell align="end">
                          <BankingMoney amount={remaining} currency={line.currencyCode} />
                        </TableCell>
                        <TableCell style={{ fontSize: "0.8125rem" }}>{line.reference || "—"}</TableCell>
                      </TableRow>
                    );
                  })
                )}
              </TableBody>
            </Table>
          </div>
        </div>

        {/* Selected Match Arithmetic Bar */}
        <div style={{ padding: "1rem", background: "var(--color-surface-page)", borderRadius: "var(--radius-sm)", border: "1px solid var(--color-border-subtle)" }}>
          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(180px, 1fr))", gap: "0.75rem", alignItems: "center" }}>
            <div>
              <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", display: "block" }}>{t("reconciliation.selectedExternal")}</span>
              <span style={{ fontSize: "1.125rem", fontWeight: 700 }}><BankingMoney amount={externalSum} currency="EGP" /></span>
            </div>
            <div>
              <TextField
                label={t("reconciliation.selectedSystem")}
                type="number"
                value={systemAmountInput}
                onChange={(val) => setSystemAmountInput(val)}
                placeholder={String(externalSum)}
              />
            </div>
            <div>
              <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", display: "block" }}>{t("reconciliation.workbenchDiff")}</span>
              <span style={{ fontSize: "1.125rem", fontWeight: 700, color: matchDiff === 0 ? "#16a34a" : "var(--color-status-danger)" }}>
                <BankingMoney amount={matchDiff} currency="EGP" />
              </span>
            </div>
          </div>

          <div style={{ marginTop: "0.75rem" }}>
            <TextField
              label={t("reconciliation.explanationLabel")}
              value={explanation}
              onChange={(val) => setExplanation(val)}
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
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", borderTop: "1px solid var(--color-border-subtle)", paddingTop: "1rem", flexWrap: "wrap", gap: "0.75rem" }}>
          <div style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)", display: "flex", alignItems: "center", gap: "0.5rem" }}>
            <AlertTriangleIcon />
            <span>{t("reconciliation.finalizeNotice")}</span>
          </div>
          <div style={{ display: "flex", gap: "0.75rem" }}>
            <Button variant="secondary" onClick={onClose}>
              {t("common.close")}
            </Button>
            {reconciliation.state === "Open" && (
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

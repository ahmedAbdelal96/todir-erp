import React, { useState, useMemo } from "react";
import { useTranslation } from "react-i18next";
import { ApiError } from "../../../core/api/http-client.ts";
import { Surface } from "../../../components/pages/surface.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { Alert } from "../../../components/primitives/alert.tsx";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { MoneyDisplay } from "../../../components/fields/money-display.tsx";
import { FinancialStatusBadge } from "../components/financial-status-badge.tsx";
import {
  useAutoApplySuggestionsMutation,
  useApplicationPreviewMutation,
  useApplyOpenItemsMutation,
  useOpenItemsQuery
} from "../hooks/subledger-queries.ts";
import type {
  PartyOpenItem,
  ApplicationPreviewResult,
  SubledgerDirection,
  AutoApplySuggestionLine
} from "../types.ts";

export interface ApplicationWorkspaceProps {
  readonly legalEntityId: string;
  readonly partyId: string;
  readonly direction: SubledgerDirection;
  readonly preselectedSourceId?: string | undefined;
  readonly onCompleted?: () => void;
}

export function ApplicationWorkspace({
  legalEntityId,
  partyId,
  direction,
  preselectedSourceId,
  onCompleted
}: ApplicationWorkspaceProps): React.JSX.Element {
  const { t } = useTranslation("subledger");

  // Fetch all open items for this party & direction
  const openItemsQuery = useOpenItemsQuery({
    legalEntityId,
    partyId,
    direction,
    openOnly: true,
    limit: 100
  });

  const allItems = openItemsQuery.data?.items ?? [];

  // Source credit items (Credit polarity: Payments, Advances, CreditNotes)
  const creditSources = useMemo(
    () => allItems.filter((i) => i.polarity === "Credit" && parseFloat(i.openTransactionAmount) > 0),
    [allItems]
  );

  // Target debit items (Debit polarity: Invoices, DebitNotes)
  const debitTargets = useMemo(
    () => allItems.filter((i) => i.polarity === "Debit" && parseFloat(i.openTransactionAmount) > 0),
    [allItems]
  );

  const [selectedSourceId, setSelectedSourceId] = useState<string>(() => {
    if (preselectedSourceId && creditSources.some((c) => c.id === preselectedSourceId)) {
      return preselectedSourceId;
    }
    return creditSources[0]?.id || "";
  });

  const selectedSource = useMemo(
    () => creditSources.find((c) => c.id === selectedSourceId),
    [creditSources, selectedSourceId]
  );

  React.useEffect(() => {
    if (!selectedSourceId && creditSources.length > 0 && creditSources[0]) {
      setSelectedSourceId(creditSources[0].id);
    }
  }, [selectedSourceId, creditSources]);

  // Allocation state: map of debitItemId -> applied amount string
  const [allocations, setAllocations] = useState<Record<string, string>>({});
  // Auto-apply reasons: map of debitItemId -> AutoApplySuggestionLine
  const [suggestionReasons, setSuggestionReasons] = useState<Record<string, AutoApplySuggestionLine>>({});

  // Preview dialog state
  const [isPreviewOpen, setIsPreviewOpen] = useState(false);
  const [previewResult, setPreviewResult] = useState<ApplicationPreviewResult | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);

  const autoApplyMutation = useAutoApplySuggestionsMutation();
  const previewMutation = useApplicationPreviewMutation();
  const applyMutation = useApplyOpenItemsMutation();

  // Calculations
  const sourceAvailable = selectedSource ? parseFloat(selectedSource.openTransactionAmount) : 0;
  const totalApplied = useMemo(() => {
    return Object.values(allocations).reduce((sum, val) => {
      const num = parseFloat(val);
      return sum + (isNaN(num) ? 0 : num);
    }, 0);
  }, [allocations]);

  const remainingSource = Math.max(0, sourceAvailable - totalApplied);

  const handleApplyFull = (target: PartyOpenItem) => {
    const targetRem = parseFloat(target.openTransactionAmount);
    const currAlloc = parseFloat(allocations[target.id] || "0");
    const currentTotalOther = totalApplied - currAlloc;
    const availableFromSource = Math.max(0, sourceAvailable - currentTotalOther);

    const amountToApply = Math.min(targetRem, availableFromSource);
    setAllocations((prev) => ({
      ...prev,
      [target.id]: amountToApply.toFixed(2)
    }));
  };

  const handleClear = (targetId: string) => {
    setAllocations((prev) => {
      const next = { ...prev };
      delete next[targetId];
      return next;
    });
    setSuggestionReasons((prev) => {
      const next = { ...prev };
      delete next[targetId];
      return next;
    });
  };

  const handleAutoApply = async () => {
    if (!selectedSource) return;
    setErrorMessage(null);
    try {
      const result = await autoApplyMutation.mutateAsync({
        legalEntityId,
        sourceOpenItemId: selectedSource.id,
        policy: "ExactReferenceThenOldestDue",
        maximumLines: 50
      });

      const nextAlloc: Record<string, string> = {};
      const nextReasons: Record<string, AutoApplySuggestionLine> = {};

      for (const line of result.suggestions) {
        const amt = line.debitAmount || (line as unknown as Record<string, string>).suggestedAmount || "0.00";
        const code = line.reasonCode || (line as unknown as Record<string, string>).policyRuleTriggered || "OldestDue";
        nextAlloc[line.debitItemId] = amt;
        nextReasons[line.debitItemId] = {
          ...line,
          debitAmount: amt,
          reasonCode: code
        };
      }

      setAllocations(nextAlloc);
      setSuggestionReasons(nextReasons);
    } catch (err: unknown) {
      setErrorMessage(err instanceof Error ? err.message : String(err));
    }
  };

  const handleOpenPreview = async () => {
    if (!selectedSource) return;
    setErrorMessage(null);

    const lines = Object.entries(allocations)
      .filter(([_, amt]) => parseFloat(amt) > 0)
      .map(([debitId, amt]) => {
        const target = debitTargets.find((d) => d.id === debitId);
        return {
          debitItemId: debitId,
          creditItemId: selectedSource.id,
          amount: amt,
          creditAmount: amt,
          expectedDebitVersion: target?.rowVersion ?? 1,
          expectedCreditVersion: selectedSource.rowVersion
        };
      });

    if (lines.length === 0) {
      setErrorMessage(t("application.selectSource"));
      return;
    }

    try {
      const preview = await previewMutation.mutateAsync({
        legalEntityId,
        partyId,
        direction,
        idempotencyKey: crypto.randomUUID(),
        applicationDate: new Date().toISOString().slice(0, 10),
        settlementDate: new Date().toISOString().slice(0, 10),
        lines
      });
      setPreviewResult(preview);
      setIsPreviewOpen(true);
    } catch (err: unknown) {
      setErrorMessage(err instanceof Error ? err.message : String(err));
    }
  };

  const handleConfirmCommit = async () => {
    if (!selectedSource || !previewResult) return;
    setErrorMessage(null);

    const lines = previewResult.lines.map((l) => ({
      debitItemId: l.debitItemId,
      creditItemId: l.creditItemId,
      amount: l.debitAppliedAmount,
      creditAmount: l.creditAppliedAmount,
      expectedDebitVersion: l.expectedDebitVersion,
      expectedCreditVersion: l.expectedCreditVersion
    }));

    try {
      await applyMutation.mutateAsync({
        legalEntityId,
        partyId,
        direction,
        idempotencyKey: crypto.randomUUID(),
        applicationDate: new Date().toISOString().slice(0, 10),
        settlementDate: new Date().toISOString().slice(0, 10),
        lines
      });

      setIsPreviewOpen(false);
      setAllocations({});
      setSuggestionReasons({});
      setSuccessMessage(t("application.successMessage"));
      if (onCompleted) onCompleted();
    } catch (err: unknown) {
      setIsPreviewOpen(false);
      const isConflict =
        (err instanceof ApiError &&
          (err.status === 409 ||
           err.problemDetails?.title?.includes("item_changed") ||
           err.message?.includes("item_changed"))) ||
        (err instanceof Error &&
          (err.message.includes("item_changed") ||
           err.message.includes("conflict") ||
           err.message.includes("concurrency") ||
           err.message.includes("409")));
      if (isConflict) {
        setErrorMessage(t("application.concurrencyError"));
      } else {
        const msg = err instanceof Error ? err.message : String(err);
        setErrorMessage(msg);
      }
    }
  };

  if (!partyId) {
    return (
      <Surface variant="card" padding="normal" className="bg-white border border-slate-200 rounded-lg shadow-sm text-center py-12">
        <p className="text-sm font-semibold text-slate-700">{t("partyFinancial.selectPrompt")}</p>
      </Surface>
    );
  }

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem", width: "100%" }}>
      {errorMessage && (
        <Alert variant="danger">
          {errorMessage}
        </Alert>
      )}

      {successMessage && (
        <Alert variant="success">
          {successMessage}
        </Alert>
      )}

      {/* 1. Source Credit Selector Card */}
      <Surface variant="card" padding="normal">
        <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: "1rem" }}>
            <div>
              <h3 style={{ margin: 0, fontSize: "1.125rem", fontWeight: 800, color: "var(--color-brand-primary, #102a43)" }}>
                {t("application.selectSource")}
              </h3>
              <p style={{ margin: "0.25rem 0 0", fontSize: "0.8125rem", color: "var(--color-content-muted, #64748b)" }}>
                {t("application.subtitle")}
              </p>
            </div>

            {selectedSource && (
              <div
                style={{
                  display: "flex",
                  alignItems: "center",
                  gap: "1.5rem",
                  padding: "0.625rem 1rem",
                  backgroundColor: "var(--color-surface-subtle, #f8fafc)",
                  borderRadius: "var(--radius-md, 0.5rem)",
                  border: "1px solid var(--color-border-default, #cbd5e1)"
                }}
              >
                <div>
                  <div style={{ fontSize: "0.75rem", fontWeight: 700, color: "var(--color-content-muted)" }}>
                    {t("application.sourceAvailable")}
                  </div>
                  <div style={{ fontSize: "1.125rem", fontWeight: 800, color: "var(--color-status-info, #0284c7)" }}>
                    <MoneyDisplay
                      value={{ amount: selectedSource.openTransactionAmount, currency: selectedSource.transactionCurrency }}
                      displayCurrencyCode
                    />
                  </div>
                </div>

                <div style={{ borderInlineStart: "1px solid var(--color-border-default, #cbd5e1)", paddingInlineStart: "1rem" }}>
                  <div style={{ fontSize: "0.75rem", fontWeight: 700, color: "var(--color-content-muted)" }}>
                    {t("application.appliedTotal")}
                  </div>
                  <div style={{ fontSize: "1.125rem", fontWeight: 800, color: "var(--color-status-success, #059669)" }}>
                    <MoneyDisplay
                      value={{ amount: totalApplied.toFixed(2), currency: selectedSource.transactionCurrency }}
                      displayCurrencyCode
                    />
                  </div>
                </div>

                <div style={{ borderInlineStart: "1px solid var(--color-border-default, #cbd5e1)", paddingInlineStart: "1rem" }}>
                  <div style={{ fontSize: "0.75rem", fontWeight: 700, color: "var(--color-content-muted)" }}>
                    {t("application.remainingSource")}
                  </div>
                  <div style={{ fontSize: "1.125rem", fontWeight: 800, color: "var(--color-brand-primary, #102a43)" }}>
                    <MoneyDisplay
                      value={{ amount: remainingSource.toFixed(2), currency: selectedSource.transactionCurrency }}
                      displayCurrencyCode
                    />
                  </div>
                </div>
              </div>
            )}
          </div>

          {/* Credit Sources Dropdown or Grid */}
          <div style={{ display: "flex", gap: "0.75rem", flexWrap: "wrap", alignItems: "center" }}>
            <label style={{ fontSize: "0.875rem", fontWeight: 700, color: "var(--color-content-secondary, #334155)" }}>
              {t("application.selectSource")}:
            </label>
            <select
              value={selectedSourceId}
              onChange={(e) => {
                setSelectedSourceId(e.target.value);
                setAllocations({});
                setSuggestionReasons({});
              }}
              style={{
                padding: "0.5rem 0.875rem",
                borderRadius: "var(--radius-md, 0.375rem)",
                border: "1px solid var(--color-border-default, #cbd5e1)",
                backgroundColor: "var(--color-surface-card, #ffffff)",
                fontSize: "0.875rem",
                fontWeight: 600,
                minWidth: "320px"
              }}
            >
              {creditSources.length === 0 ? (
                <option value="">{t("partyFinancial.availableCredits")}</option>
              ) : (
                creditSources.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.sourceType} - {c.sourceDisplayNumber || c.sourceReference} ({c.openTransactionAmount} {c.transactionCurrency})
                  </option>
                ))
              )}
            </select>

            <Button
              variant="secondary"
              onClick={() => void handleAutoApply()}
              isLoading={autoApplyMutation.isPending}
              isDisabled={!selectedSource || debitTargets.length === 0}
            >
              {t("application.autoApplyButton")}
            </Button>
          </div>
        </div>
      </Surface>

      {/* 2. Target Invoices Table */}
      <Surface variant="card" padding="normal">
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "1rem" }}>
          <div>
            <h4 style={{ margin: 0, fontSize: "1rem", fontWeight: 800, color: "var(--color-content-primary, #0f172a)" }}>
              {t("application.eligibleTargets")} ({debitTargets.length})
            </h4>
          </div>

          <div style={{ display: "flex", gap: "0.75rem" }}>
            <Button
              variant="primary"
              onClick={() => void handleOpenPreview()}
              isLoading={previewMutation.isPending}
              isDisabled={totalApplied <= 0}
            >
              {t("application.previewButton")}
            </Button>
          </div>
        </div>

        {debitTargets.length === 0 ? (
          <div style={{ padding: "2rem", textAlign: "center", color: "var(--color-content-muted)" }}>
            {t("openItems.emptyTitle")}
          </div>
        ) : (
          <div className="ui-table-container">
            <table className="ui-table">
              <thead className="ui-table-header">
                <tr>
                  <th className="ui-table-head">{t("openItems.documentRef")}</th>
                  <th className="ui-table-head">{t("openItems.dueDate")}</th>
                  <th className="ui-table-head">{t("openItems.originalAmount")}</th>
                  <th className="ui-table-head">{t("openItems.remainingAmount")}</th>
                  <th className="ui-table-head">{t("application.appliedTotal")}</th>
                  <th className="ui-table-head">{t("openItems.actions")}</th>
                </tr>
              </thead>
              <tbody>
                {debitTargets.map((target) => {
                  const applied = allocations[target.id] || "";
                  const reason = suggestionReasons[target.id];

                  return (
                    <tr key={target.id} className="ui-table-row">
                      <td className="ui-table-cell">
                        <div style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
                          <strong>{target.sourceDisplayNumber || target.sourceReference}</strong>
                          {target.sourceScheduleReference && (
                            <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
                              (#{target.sourceScheduleReference})
                            </span>
                          )}
                          {reason && (
                            <span
                              style={{
                                fontSize: "0.6875rem",
                                padding: "0.125rem 0.375rem",
                                borderRadius: "var(--radius-sm)",
                                backgroundColor: "rgba(79, 70, 229, 0.1)",
                                color: "var(--color-brand-primary, #102a43)",
                                fontWeight: 700
                              }}
                            >
                              {t(`application.reasons.${reason.reasonCode}`, { defaultValue: reason.reasonCode })}
                            </span>
                          )}
                        </div>
                      </td>

                      <td className="ui-table-cell">
                        <bdi>{target.dueDate}</bdi>
                      </td>

                      <td className="ui-table-cell">
                        <MoneyDisplay
                          value={{ amount: target.originalTransactionAmount, currency: target.transactionCurrency }}
                          displayCurrencyCode
                        />
                      </td>

                      <td className="ui-table-cell">
                        <strong style={{ color: "var(--color-status-danger, #dc2626)" }}>
                          <MoneyDisplay
                            value={{ amount: target.openTransactionAmount, currency: target.transactionCurrency }}
                            displayCurrencyCode
                          />
                        </strong>
                      </td>

                      <td className="ui-table-cell" style={{ width: "160px" }}>
                        <input
                          type="number"
                          step="0.01"
                          min="0"
                          max={target.openTransactionAmount}
                          value={applied}
                          placeholder="0.00"
                          onChange={(e) => {
                            const val = e.target.value;
                            setAllocations((prev) => ({
                              ...prev,
                              [target.id]: val
                            }));
                          }}
                          style={{
                            width: "100%",
                            padding: "0.375rem 0.5rem",
                            borderRadius: "var(--radius-md, 0.375rem)",
                            border: "1px solid var(--color-border-default, #cbd5e1)",
                            fontSize: "0.875rem",
                            fontWeight: 700,
                            color: "var(--color-content-primary, #0f172a)"
                          }}
                        />
                      </td>

                      <td className="ui-table-cell">
                        <div style={{ display: "flex", gap: "0.375rem" }}>
                          <Button
                            variant="secondary"
                            size="compact"
                            onClick={() => handleApplyFull(target)}
                          >
                            {t("application.applyFull")}
                          </Button>
                          {applied && (
                            <Button
                              variant="ghost"
                              size="compact"
                              onClick={() => handleClear(target.id)}
                            >
                              {t("application.clearAllocation")}
                            </Button>
                          )}
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </Surface>

      {/* 3. Preview Modal */}
      {previewResult && (
        <Dialog
          isOpen={isPreviewOpen}
          onOpenChange={setIsPreviewOpen}
          size="medium"
          title={t("application.previewTitle")}
          description={t("application.previewSubtitle")}
          footer={
            <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", width: "100%" }}>
              <Button variant="secondary" onClick={() => setIsPreviewOpen(false)}>
                {t("reversal.cancelButton")}
              </Button>
              <Button
                variant="primary"
                onClick={() => void handleConfirmCommit()}
                isLoading={applyMutation.isPending}
              >
                {t("application.confirmButton")}
              </Button>
            </div>
          }
        >
          <div style={{ display: "flex", flexDirection: "column", gap: "1rem", padding: "0.5rem 0" }}>
            <div
              style={{
                display: "grid",
                gridTemplateColumns: "1fr 1fr",
                gap: "1rem",
                padding: "1rem",
                backgroundColor: "var(--color-surface-subtle, #f8fafc)",
                borderRadius: "var(--radius-md, 0.5rem)",
                border: "1px solid var(--color-border-subtle, #e2e8f0)"
              }}
            >
              <div>
                <span style={{ fontSize: "0.75rem", fontWeight: 700, color: "var(--color-content-muted)" }}>
                  {t("application.debitApplied")}:
                </span>
                <div style={{ fontSize: "1.125rem", fontWeight: 800, color: "var(--color-status-success)" }}>
                  {previewResult.totalDebitApplied} {selectedSource?.transactionCurrency}
                </div>
              </div>

              <div>
                <span style={{ fontSize: "0.75rem", fontWeight: 700, color: "var(--color-content-muted)" }}>
                  {t("application.creditApplied")}:
                </span>
                <div style={{ fontSize: "1.125rem", fontWeight: 800, color: "var(--color-brand-primary, #102a43)" }}>
                  {previewResult.totalCreditApplied} {selectedSource?.transactionCurrency}
                </div>
              </div>

              {previewResult.totalFxImpactBase && parseFloat(previewResult.totalFxImpactBase) !== 0 && (
                <div style={{ gridColumn: "1 / -1" }}>
                  <span style={{ fontSize: "0.75rem", fontWeight: 700, color: "var(--color-content-muted)" }}>
                    {t("application.fxImpact")}:
                  </span>
                  <div style={{ fontSize: "1rem", fontWeight: 700, color: "var(--color-status-warning)" }}>
                    {previewResult.totalFxImpactBase}
                  </div>
                </div>
              )}
            </div>

            {/* Preview Breakdown Table */}
            <div className="ui-table-container">
              <table className="ui-table">
                <thead className="ui-table-header">
                  <tr>
                    <th className="ui-table-head">{t("openItems.documentRef")}</th>
                    <th className="ui-table-head">{t("application.appliedTotal")}</th>
                    <th className="ui-table-head">{t("openItems.remainingAmount")}</th>
                  </tr>
                </thead>
                <tbody>
                  {previewResult.lines.map((l) => {
                    const target = debitTargets.find((d) => d.id === l.debitItemId);
                    return (
                      <tr key={l.debitItemId} className="ui-table-row">
                        <td className="ui-table-cell">
                          <strong>{target?.sourceDisplayNumber || target?.sourceReference || l.debitItemId}</strong>
                        </td>
                        <td className="ui-table-cell">
                          <strong style={{ color: "var(--color-status-success)" }}>
                            {l.debitAppliedAmount} {selectedSource?.transactionCurrency}
                          </strong>
                        </td>
                        <td className="ui-table-cell">
                          {l.debitRemainingAmount} {selectedSource?.transactionCurrency}
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          </div>
        </Dialog>
      )}
    </div>
  );
}

import React from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { MoneyDisplay } from "../../../../components/fields/money-display.tsx";
import { LocalDateDisplay } from "../../../../components/fields/local-date-display.tsx";
import { useExplainEntry } from "../../hooks/use-ledger-queries.ts";

export interface EntryExplanationModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly entryId: string | null;
}

export function EntryExplanationModal({
  isOpen,
  onClose,
  legalEntityId,
  entryId
}: EntryExplanationModalProps): React.JSX.Element | null {
  const { t } = useTranslation("accounting");
  const { data: explanation, isLoading } = useExplainEntry(legalEntityId, entryId ?? "");

  if (!entryId || !isOpen) return null;

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open) => !open && onClose()}
      title={explanation ? `${t("explanation.title")}: ${explanation.entryNumber}` : t("explanation.title")}
      size="large"
      footer={
        <div style={{ display: "flex", justifyContent: "flex-end" }}>
          <Button variant="primary" onPress={onClose}>
            {t("common.close")}
          </Button>
        </div>
      }
    >
      {isLoading || !explanation ? (
        <div style={{ padding: "2rem", textAlign: "center" }}>{t("common.loading")}</div>
      ) : (
        <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
          {/* Metadata Card */}
          <div
            style={{
              padding: "1rem",
              borderRadius: "var(--radius-md, 0.5rem)",
              backgroundColor: "var(--color-surface-sunken)",
              border: "1px solid var(--color-border-default)",
              display: "grid",
              gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))",
              gap: "0.75rem",
              fontSize: "0.875rem"
            }}
          >
            <div>
              <span style={{ color: "var(--color-content-muted)" }}>{t("explanation.sourceModule")}: </span>
              <strong>{explanation.sourceModule}</strong> ({explanation.sourceReference})
            </div>
            <div>
              <span style={{ color: "var(--color-content-muted)" }}>{t("explanation.factType")}: </span>
              <strong>{explanation.factType}</strong> <span>({t("explanation.version")} {explanation.factVersion})</span>
            </div>
            <div>
              <span style={{ color: "var(--color-content-muted)" }}>{t("journal.entryDate")}: </span>
              <LocalDateDisplay value={explanation.postingDate} />
            </div>
            <div>
              <span style={{ color: "var(--color-content-muted)" }}>{t("explanation.period")}: </span>
              <strong>{explanation.accountingPeriod}</strong>
            </div>
            <div>
              <span style={{ color: "var(--color-content-muted)" }}>{t("explanation.baseCurrency")}: </span>
              <strong>{explanation.baseCurrencyCode}</strong>
            </div>
            <div>
              <span style={{ color: "var(--color-content-muted)" }}>{t("explanation.postingRule")}: </span>
              <span style={{ fontFamily: "var(--font-mono, monospace)", fontSize: "0.75rem" }}>
                {explanation.postingRuleVersionId.slice(0, 8)}...
              </span>
            </div>
          </div>

          {/* Explanation Lines */}
          <div>
            <h4 style={{ margin: "0 0 0.75rem", fontSize: "1rem", fontWeight: 600 }}>
              {t("explanation.accountDeterminationBreakdown")}
            </h4>
            <div style={{ display: "flex", flexDirection: "column", gap: "0.75rem" }}>
              {explanation.lines.map((line) => (
                <div
                  key={line.lineId}
                  style={{
                    padding: "0.875rem 1rem",
                    borderRadius: "var(--radius-md, 0.5rem)",
                    border: "1px solid var(--color-border-default)",
                    backgroundColor: "var(--color-surface-card)"
                  }}
                >
                  <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "0.5rem", flexWrap: "wrap", gap: "0.5rem" }}>
                    <div style={{ display: "flex", alignItems: "center", gap: "0.5rem", flexWrap: "wrap" }}>
                      <span
                        style={{
                          padding: "0.25rem 0.5rem",
                          borderRadius: "var(--radius-sm, 0.25rem)",
                          backgroundColor: "var(--color-brand-primary-subtle, #e0e7ff)",
                          color: "var(--color-brand-primary, #3730a3)",
                          fontSize: "0.75rem",
                          fontWeight: 700
                        }}
                      >
                        {t(`explanation.roles.${line.accountRole}`, { defaultValue: line.accountRole })}
                      </span>
                      <strong style={{ fontSize: "0.9375rem" }}>
                        <bdi>{line.accountCode}</bdi> — {line.accountName}
                      </strong>
                    </div>

                    <div style={{ display: "flex", gap: "1rem", fontSize: "0.875rem", fontWeight: 600 }}>
                      {line.debit > 0 && (
                        <span>
                          {t("journal.debit")}:{" "}
                          <MoneyDisplay value={{ amount: line.baseDebit.toFixed(2), currency: line.currencyCode }} />
                        </span>
                      )}
                      {line.credit > 0 && (
                        <span>
                          {t("journal.credit")}:{" "}
                          <MoneyDisplay value={{ amount: line.baseCredit.toFixed(2), currency: line.currencyCode }} />
                        </span>
                      )}
                    </div>
                  </div>

                  {/* Clean Business Explanation */}
                  <div
                    style={{
                      padding: "0.625rem 0.875rem",
                      borderRadius: "var(--radius-sm, 0.25rem)",
                      backgroundColor: "var(--color-surface-sunken)",
                      fontSize: "0.875rem",
                      color: "var(--color-content-secondary)",
                      lineHeight: 1.5
                    }}
                  >
                    <div style={{ fontWeight: 600, color: "var(--color-content-primary)", marginBottom: "0.25rem" }}>
                      {t("explanation.roleLabel")}: {t(`explanation.roles.${line.accountRole}`, { defaultValue: line.accountRole })}
                    </div>
                    <div>
                      {t("explanation.determinedAccount")}
                    </div>
                  </div>

                  {/* Expandable Technical Details & Audit Evidence */}
                  <details
                    style={{
                      marginTop: "0.5rem",
                      padding: "0.5rem 0.75rem",
                      borderRadius: "var(--radius-sm, 0.25rem)",
                      border: "1px solid var(--color-border-subtle, #e2e8f0)",
                      backgroundColor: "var(--color-surface-card)",
                      fontSize: "0.75rem",
                      color: "var(--color-content-muted)"
                    }}
                  >
                    <summary style={{ cursor: "pointer", fontWeight: 600, color: "var(--color-brand-primary)" }}>
                      {t("explanation.technicalDetails")}
                    </summary>
                    <div style={{ marginTop: "0.5rem", display: "flex", flexDirection: "column", gap: "0.25rem", fontFamily: "var(--font-mono, monospace)" }}>
                      <div>
                        <span>{t("explanation.rawRole")}: </span>
                        <code>{line.accountRole}</code>
                      </div>
                      <div>
                        <span>{t("explanation.systemExplanation")}: </span>
                        <span>{line.whyThisAccount}</span>
                      </div>
                      {line.mappingId && (
                        <div>
                          <span>{t("explanation.mappingId")}: </span>
                          <code>{line.mappingId}</code>
                          <span> ({t("explanation.versionPrefix")} {line.mappingVersion ?? 1})</span>
                        </div>
                      )}
                      <div>
                        <span>{t("explanation.postingRuleId")}: </span>
                        <code>{explanation.postingRuleVersionId}</code>
                      </div>
                    </div>
                  </details>
                </div>
              ))}
            </div>
          </div>
        </div>
      )}
    </Dialog>
  );
}

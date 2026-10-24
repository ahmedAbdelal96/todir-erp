import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import type { TaxExplanationContract, TaxResultContract } from "../types.ts";
import { Button } from "../../../components/primitives/button.tsx";
import { TaxStatusBadge } from "./tax-status-badge.tsx";

export interface WhyThisTaxPanelProps {
  readonly result: TaxResultContract;
  readonly isTestOnly?: boolean;
}

export function WhyThisTaxPanel({ result, isTestOnly = true }: WhyThisTaxPanelProps): React.JSX.Element {
  const { t } = useTranslation("tax");
  const [showTechnicalDetails, setShowTechnicalDetails] = useState(false);
  const explanation: TaxExplanationContract = result.explanation;

  return (
    <section
      style={{
        padding: "1.25rem",
        borderRadius: "var(--radius-lg)",
        border: "1px solid var(--color-brand-primary-subtle, #bfdbfe)",
        backgroundColor: "var(--color-surface-subtle, #f8fafc)",
        display: "flex",
        flexDirection: "column",
        gap: "1rem"
      }}
    >
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
        <h3
          style={{ margin: 0, fontSize: "1rem", fontWeight: 600, color: "var(--color-content-primary)" }}
        >
          {t("whyThisTax.title")}
        </h3>
        <span
          style={{
            fontSize: "0.75rem",
            padding: "0.25rem 0.5rem",
            borderRadius: "var(--radius-full)",
            backgroundColor: "var(--color-brand-primary-subtle, #e0f2fe)",
            color: "var(--color-brand-primary, #0369a1)",
            fontWeight: 600
          }}
        >
          {explanation.countryPackCode} {t("registrations.versionTag", { version: explanation.countryPackVersion })}
        </span>
      </div>

      {isTestOnly && (
        <div
          role="note"
          style={{
            padding: "0.5rem 0.75rem",
            borderRadius: "var(--radius-sm)",
            backgroundColor: "var(--color-status-warning-subtle, #fffbeb)",
            border: "1px solid var(--color-status-warning-border, #fcd34d)",
            color: "var(--color-status-warning-text, #92400e)",
            fontSize: "0.8125rem",
            fontWeight: 600,
            display: "flex",
            alignItems: "center",
            gap: "0.5rem"
          }}
        >
          <span style={{ width: "8px", height: "8px", borderRadius: "50%", backgroundColor: "var(--color-status-warning-text, #92400e)" }} />
          <span>{t("whyThisTax.testOnlyNotice")}</span>
        </div>
      )}

      {explanation.summary && (
        <p style={{ margin: 0, fontSize: "0.875rem", color: "var(--color-content-secondary)", lineHeight: 1.5 }}>
          {isTestOnly ? `[${t("whyThisTax.testDataOnlyTag")}] ${explanation.summary}` : explanation.summary}
        </p>
      )}

      {/* Human terms grid */}
      <div
        style={{
          display: "grid",
          gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))",
          gap: "0.875rem",
          fontSize: "0.8125rem",
          padding: "0.75rem",
          borderRadius: "var(--radius-md)",
          backgroundColor: "var(--color-surface-card, #ffffff)",
          border: "1px solid var(--color-border-subtle, #f1f5f9)"
        }}
      >
        <div>
          <span style={{ color: "var(--color-content-muted)", display: "block" }}>{t("whyThisTax.taxDate")}</span>
          <span style={{ fontWeight: 600 }}>{explanation.taxDate}</span>
        </div>
        <div>
          <span style={{ color: "var(--color-content-muted)", display: "block" }}>{t("whyThisTax.jurisdiction")}</span>
          <span style={{ fontWeight: 600 }}>{explanation.jurisdiction || t("whyThisTax.none")}</span>
        </div>
        <div>
          <span style={{ color: "var(--color-content-muted)", display: "block" }}>{t("whyThisTax.registrationStatus")}</span>
          <TaxStatusBadge status={explanation.registrationStatus} type="registration" />
        </div>
        <div>
          <span style={{ color: "var(--color-content-muted)", display: "block" }}>{t("whyThisTax.partyTreatment")}</span>
          <span style={{ fontWeight: 600 }}>{explanation.partyTreatment || t("treatment.standardRated")}</span>
        </div>
        <div>
          <span style={{ color: "var(--color-content-muted)", display: "block" }}>{t("whyThisTax.productCategory")}</span>
          <span style={{ fontWeight: 600 }}>{explanation.productCategory || t("whyThisTax.none")}</span>
        </div>
        <div>
          <span style={{ color: "var(--color-content-muted)", display: "block" }}>{t("whyThisTax.rounding")}</span>
          <span style={{ fontWeight: 600 }}>{explanation.rounding}</span>
        </div>
      </div>

      {/* Components list */}
      {explanation.componentCodes.length > 0 && (
        <div style={{ fontSize: "0.8125rem" }}>
          <span style={{ color: "var(--color-content-muted)", fontWeight: 500 }}>
            {t("whyThisTax.components")}:
          </span>{" "}
          <span style={{ fontWeight: 600 }}>{explanation.componentCodes.join(", ")}</span>
        </div>
      )}

      {/* Legal reason if present */}
      {(explanation.legalReasonText || explanation.legalReasonCode) && (
        <div
          style={{
            fontSize: "0.8125rem",
            padding: "0.625rem 0.75rem",
            borderRadius: "var(--radius-sm)",
            backgroundColor: "var(--color-status-warning-subtle, #fffbeb)",
            color: "var(--color-status-warning-text, #92400e)",
            border: isTestOnly ? "1px dashed var(--color-status-warning-border, #fcd34d)" : undefined
          }}
        >
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "0.25rem", flexWrap: "wrap", gap: "0.5rem" }}>
            <strong>
              {isTestOnly ? t("whyThisTax.testFixtureLegalBasis") : t("whyThisTax.legalBasis")}:
            </strong>
            {isTestOnly && (
              <span
                style={{
                  fontSize: "0.6875rem",
                  padding: "0.125rem 0.375rem",
                  borderRadius: "var(--radius-sm)",
                  backgroundColor: "var(--color-status-warning-border, #fcd34d)",
                  color: "var(--color-status-warning-text, #92400e)",
                  fontWeight: 700
                }}
              >
                {t("whyThisTax.testDataOnlyTag")}
              </span>
            )}
          </div>
          <div>{explanation.legalReasonText || explanation.legalReasonCode}</div>
        </div>
      )}

      {/* Technical details toggle */}
      <div>
        <Button
          variant="secondary"
          size="compact"
          onClick={() => setShowTechnicalDetails((prev) => !prev)}
        >
          {showTechnicalDetails ? t("whyThisTax.hideTechnical") : t("whyThisTax.showTechnical")}
        </Button>

        {showTechnicalDetails && (
          <div
            style={{
              marginTop: "0.75rem",
              padding: "0.75rem",
              borderRadius: "var(--radius-md)",
              backgroundColor: "var(--color-surface-sunken, #f1f5f9)",
              fontSize: "0.75rem",
              fontFamily: "var(--font-mono, monospace)",
              display: "flex",
              flexDirection: "column",
              gap: "0.375rem"
            }}
          >
            <div><strong>{t("whyThisTax.ruleIdLabel")}: </strong> {explanation.matchedRuleId || t("whyThisTax.none")}</div>
            <div><strong>{t("whyThisTax.ruleSetVersionLabel")}: </strong> {result.ruleSetVersion || t("whyThisTax.none")}</div>
            <div><strong>{t("whyThisTax.calculationVersionLabel")}: </strong> {result.calculationVersion}</div>
            <div><strong>{t("whyThisTax.jurisdictionSnapshotLabel")}: </strong> {result.jurisdictionSnapshot}</div>
            <div><strong>{t("whyThisTax.registrationSnapshotLabel")}: </strong> {result.registrationSnapshot}</div>
            {result.roundingEvidence && (
              <div>
                <strong>{t("whyThisTax.roundingEvidenceLabel")}: </strong> {result.roundingEvidence.strategy} ({result.roundingEvidence.mode}),
                {" "}{t("whyThisTax.scaleLabel")}: {result.roundingEvidence.moneyScale},
                {" "}{t("whyThisTax.residualLabel")}: {result.roundingEvidence.residual}
              </div>
            )}
          </div>
        )}
      </div>
    </section>
  );
}

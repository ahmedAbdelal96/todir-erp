import React from "react";
import { useTranslation } from "react-i18next";
import { StatusBadge, type StatusIntent } from "../../../components/fields/status-badge.tsx";

export interface TaxTreatmentDisplayProps {
  readonly treatment: string | null | undefined;
  readonly rate?: string | null | undefined;
  readonly isReverseCharge?: boolean | undefined;
  readonly legalReasonCode?: string | null | undefined;
  readonly showBadge?: boolean | undefined;
}

export function TaxTreatmentDisplay({
  treatment,
  rate,
  isReverseCharge,
  legalReasonCode,
  showBadge = true
}: TaxTreatmentDisplayProps): React.JSX.Element {
  const { t } = useTranslation("tax");

  const normalized = (treatment ?? "").trim();
  let intent: StatusIntent = "neutral";
  let labelKey = "treatment.standardRated";

  switch (normalized) {
    case "StandardRated":
      intent = "success";
      labelKey = "treatment.standardRated";
      break;
    case "ZeroRated":
      intent = "info";
      labelKey = "treatment.zeroRated";
      break;
    case "Exempt":
      intent = "warning";
      labelKey = "treatment.exempt";
      break;
    case "OutOfScope":
      intent = "neutral";
      labelKey = "treatment.outOfScope";
      break;
    case "ReverseCharge":
      intent = "warning";
      labelKey = "treatment.reverseCharge";
      break;
    case "TaxDisabled":
      intent = "neutral";
      labelKey = "treatment.taxDisabled";
      break;
    default:
      labelKey = normalized ? `treatment.${normalized}` : "treatment.unknown";
  }

  const baseLabel = t(labelKey as any, { defaultValue: normalized || t("treatment.unknown") });
  const displayLabel = rate && normalized === "StandardRated" ? `${baseLabel} (${rate}%)` : baseLabel;

  return (
    <div style={{ display: "inline-flex", alignItems: "center", gap: "0.5rem", flexWrap: "wrap" }}>
      {showBadge ? (
        <StatusBadge status={normalized || "Unknown"} intent={intent} label={displayLabel} />
      ) : (
        <span style={{ fontWeight: 500 }}>{displayLabel}</span>
      )}

      {isReverseCharge && (
        <span
          style={{
            fontSize: "0.75rem",
            padding: "0.125rem 0.375rem",
            borderRadius: "var(--radius-sm)",
            backgroundColor: "var(--color-status-warning-subtle, #fef3c7)",
            color: "var(--color-status-warning-text, #92400e)",
            fontWeight: 600
          }}
        >
          {t("treatment.reverseChargeBadge")}
        </span>
      )}

      {legalReasonCode && (
        <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
          ({legalReasonCode})
        </span>
      )}
    </div>
  );
}

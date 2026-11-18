import React from "react";
import { useTranslation } from "react-i18next";
import { QuantityDisplay } from "../../../components/fields/quantity-display.tsx";
import { toQty, type StockAvailabilitySummary } from "../utils.ts";

export interface InventoryAvailabilityPanelProps {
  readonly summary: StockAvailabilitySummary;
  readonly title: string;
  readonly isLoading?: boolean;
  readonly hasSelection: boolean;
  readonly isInsufficient?: boolean;
}

export function InventoryAvailabilityPanel({
  summary,
  title,
  isLoading = false,
  hasSelection,
  isInsufficient = false
}: InventoryAvailabilityPanelProps): React.JSX.Element {
  const { t } = useTranslation("inventory");

  return (
    <section
      aria-live="polite"
      style={{
        padding: "0.75rem 0.875rem",
        backgroundColor: isInsufficient ? "#fff7ed" : "var(--color-surface-subtle, #f8fafc)",
        border: `1px solid ${isInsufficient ? "#fdba74" : "var(--color-border-default)"}`,
        borderRadius: "var(--radius-sm)",
        display: "flex",
        flexDirection: "column",
        gap: "0.625rem"
      }}
    >
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", gap: "0.75rem", flexWrap: "wrap" }}>
        <strong style={{ fontSize: "0.875rem", color: "var(--color-content-primary)" }}>{title}</strong>
        {isInsufficient && (
          <span style={{ color: "var(--color-status-warning, #c2410c)", fontSize: "0.75rem", fontWeight: 700 }}>
            {t("operations.insufficientWarning")}
          </span>
        )}
      </div>

      {!hasSelection ? (
        <span style={{ color: "var(--color-content-muted)", fontSize: "0.8125rem" }}>
          {t("operations.selectProductForAvailability")}
        </span>
      ) : isLoading ? (
        <span style={{ color: "var(--color-content-muted)", fontSize: "0.8125rem" }}>
          {t("operations.loadingAvailability")}
        </span>
      ) : (
        <div style={{ display: "grid", gridTemplateColumns: "repeat(3, minmax(0, 1fr))", gap: "0.625rem" }}>
          <AvailabilityMetric label={t("operations.currentOnHand")} value={summary.onHandBaseQuantity} />
          <AvailabilityMetric label={t("operations.currentReserved")} value={summary.reservedBaseQuantity} tone="warning" />
          <AvailabilityMetric label={t("operations.currentAvailable")} value={summary.availableBaseQuantity} tone={isInsufficient ? "danger" : "success"} />
        </div>
      )}
    </section>
  );
}

function AvailabilityMetric({
  label,
  value,
  tone = "default"
}: {
  readonly label: string;
  readonly value: string;
  readonly tone?: "default" | "warning" | "success" | "danger";
}): React.JSX.Element {
  const colorByTone = {
    default: "var(--color-content-primary)",
    warning: "var(--color-status-warning, #c2410c)",
    success: "var(--color-status-success, #15803d)",
    danger: "var(--color-status-danger, #b91c1c)"
  } as const;

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "0.125rem", minWidth: 0 }}>
      <span style={{ color: "var(--color-content-muted)", fontSize: "0.6875rem" }}>{label}</span>
      <span style={{ color: colorByTone[tone], fontSize: "0.9375rem", fontWeight: 700 }}>
        <QuantityDisplay value={toQty(value)} />
      </span>
    </div>
  );
}

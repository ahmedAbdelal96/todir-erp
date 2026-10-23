import React from "react";
import { useTranslation } from "react-i18next";
import type { TaxRecoverabilityContract } from "../types.ts";

export interface TaxRecoverabilityPanelProps {
  readonly recoverability: TaxRecoverabilityContract;
  readonly taxTotal: string;
  readonly currencyCode?: string | undefined;
}

export function TaxRecoverabilityPanel({
  recoverability,
  taxTotal,
  currencyCode = ""
}: TaxRecoverabilityPanelProps): React.JSX.Element {
  const { t } = useTranslation("tax");

  return (
    <div
      style={{
        padding: "0.875rem 1rem",
        borderRadius: "var(--radius-md)",
        border: "1px solid var(--color-border-subtle, #e5e7eb)",
        backgroundColor: "var(--color-surface-subtle, #f9fafb)",
        display: "flex",
        flexDirection: "column",
        gap: "0.5rem"
      }}
    >
      <div style={{ fontSize: "0.8125rem", fontWeight: 600, color: "var(--color-content-primary)" }}>
        {t("recoverability.title")}
      </div>
      <div
        style={{
          display: "grid",
          gridTemplateColumns: "repeat(auto-fit, minmax(130px, 1fr))",
          gap: "0.75rem",
          fontSize: "0.8125rem"
        }}
      >
        <div>
          <span style={{ color: "var(--color-content-muted)", display: "block" }}>{t("recoverability.taxTotal")}</span>
          <span style={{ fontWeight: 600 }}>{taxTotal} {currencyCode}</span>
        </div>
        <div>
          <span style={{ color: "var(--color-content-muted)", display: "block" }}>{t("recoverability.percentage")}</span>
          <span style={{ fontWeight: 600 }}>{recoverability.recoverablePercentage}%</span>
        </div>
        <div>
          <span style={{ color: "var(--color-content-muted)", display: "block" }}>{t("recoverability.recoverableAmount")}</span>
          <span style={{ fontWeight: 600, color: "var(--color-status-success-text, #166534)" }}>
            {recoverability.recoverableAmount} {currencyCode}
          </span>
        </div>
        <div>
          <span style={{ color: "var(--color-content-muted)", display: "block" }}>{t("recoverability.nonRecoverableAmount")}</span>
          <span style={{ fontWeight: 600, color: "var(--color-status-danger-text, #991b1b)" }}>
            {recoverability.nonRecoverableAmount} {currencyCode}
          </span>
        </div>
      </div>
    </div>
  );
}

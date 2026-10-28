import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { InventoryDataTable } from "../../components/inventory-data-table.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { LocalDateField } from "../../../../components/fields/local-date-field.tsx";
import { MoneyDisplay } from "../../../../components/fields/money-display.tsx";
import { QuantityDisplay } from "../../../../components/fields/quantity-display.tsx";
import { EmptyState } from "../../../../components/primitives/empty-state.tsx";
import { InventoryProductDisplay } from "../../components/inventory-product-display.tsx";
import { InventoryProductPicker } from "../../components/inventory-product-picker.tsx";
import { useValuation, useReconciliation } from "../../hooks/use-inventory-queries.ts";
import { toQty, toMoneyVal } from "../../utils.ts";
import type { Warehouse } from "../../types.ts";

export interface ValuationTabProps {
  readonly legalEntityId: string;
  readonly canReadCost: boolean;
  readonly warehouses: readonly Warehouse[];
}

export function ValuationTab({
  legalEntityId,
  canReadCost,
  warehouses
}: ValuationTabProps): React.JSX.Element {
  const { t } = useTranslation("inventory");
  const today = new Date().toISOString().slice(0, 10);

  const [asOfDate, setAsOfDate] = useState<string>(today);
  const [productVariantId, setProductVariantId] = useState<string>("a6315416-cffe-bf52-bcf0-d5e590d3bf94");

  // Cost queries are ONLY enabled if canReadCost is TRUE!
  const { data: valuation } = useValuation(
    legalEntityId,
    productVariantId,
    asOfDate,
    canReadCost
  );

  const { data: reconciliation } = useReconciliation(
    legalEntityId,
    asOfDate,
    canReadCost
  );

  // If user lacks permission: Strict privacy notice!
  if (!canReadCost) {
    return (
      <div
        role="alert"
        style={{
          padding: "2rem",
          textAlign: "center",
          backgroundColor: "var(--color-surface-card)",
          border: "1px solid var(--color-border-default)",
          borderRadius: "var(--radius-md)",
          boxShadow: "0 1px 3px rgba(0, 0, 0, 0.05)"
        }}
      >
        <span style={{ fontSize: "2rem", display: "block", marginBottom: "0.5rem" }}>🔒</span>
        <h3 style={{ margin: "0 0 0.5rem", color: "var(--color-brand-primary)" }}>
          {t("valuation.title")}
        </h3>
        <p style={{ margin: 0, color: "var(--color-content-muted)", fontSize: "0.9375rem" }}>
          {t("valuation.costRestrictedNotice")}
        </p>
      </div>
    );
  }

  const isMatched = reconciliation ? (reconciliation.status === "Matched" || reconciliation.difference === 0) : false;

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
      {/* Top Filter and Date Bar */}
      <div
        style={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          flexWrap: "wrap",
          gap: "1rem",
          padding: "1rem",
          backgroundColor: "var(--color-surface-card)",
          border: "1px solid var(--color-border-default)",
          borderRadius: "var(--radius-md)"
        }}
      >
        <div>
          <h3 style={{ margin: "0 0 0.25rem", fontSize: "1.125rem", color: "var(--color-brand-primary)" }}>
            {t("valuation.title")}
          </h3>
          <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
            {t("valuation.subtitle")}
          </span>
        </div>

        <div style={{ display: "flex", gap: "0.75rem", alignItems: "center", flexWrap: "wrap" }}>
          <div style={{ minWidth: "260px" }}>
            <InventoryProductPicker
              value={productVariantId}
              onChange={setProductVariantId}
            />
          </div>
          <div style={{ width: "160px", alignSelf: "flex-end" }}>
            <LocalDateField
              value={asOfDate}
              onChange={(val) => setAsOfDate(val ?? today)}
            />
          </div>
        </div>
      </div>

      {/* Inventory-to-GL Reconciliation Card */}
      <div
        style={{
          padding: "1.25rem",
          backgroundColor: "var(--color-surface-card)",
          border: `1px solid ${isMatched ? "var(--color-border-default)" : "#fca5a5"}`,
          borderTop: `3px solid ${isMatched ? "var(--color-status-success)" : "var(--color-status-danger)"}`,
          borderRadius: "var(--radius-md)",
          boxShadow: "0 1px 3px rgba(0, 0, 0, 0.05)"
        }}
      >
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "1rem", flexWrap: "wrap", gap: "0.5rem" }}>
          <h4 style={{ margin: 0, fontSize: "1rem", color: "var(--color-brand-primary)" }}>
            {t("valuation.reconciliationCard.title")}
          </h4>
          <span
            style={{
              padding: "0.25rem 0.625rem",
              borderRadius: "var(--radius-sm)",
              fontSize: "0.75rem",
              fontWeight: 600,
              backgroundColor: isMatched ? "#dcfce7" : "#fee2e2",
              color: isMatched ? "#15803d" : "#b91c1c"
            }}
          >
            {isMatched
              ? t("valuation.reconciliationCard.statusMatched")
              : t("valuation.reconciliationCard.statusDiscrepancy")}
          </span>
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", gap: "1rem", marginBottom: "1rem" }}>
          <div style={{ padding: "0.75rem", backgroundColor: "var(--color-surface-subtle)", borderRadius: "var(--radius-sm)" }}>
            <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", display: "block" }}>
              {t("valuation.reconciliationCard.inventoryValue")}
            </span>
            <span style={{ fontSize: "1.25rem", fontWeight: 700, color: "var(--color-brand-primary)" }}>
              {reconciliation ? (
                <MoneyDisplay value={toMoneyVal(reconciliation.inventoryValue, reconciliation.currencyCode)} />
              ) : "—"}
            </span>
          </div>

          <div style={{ padding: "0.75rem", backgroundColor: "var(--color-surface-subtle)", borderRadius: "var(--radius-sm)" }}>
            <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", display: "block" }}>
              {t("valuation.reconciliationCard.ledgerValue")}
            </span>
            <span style={{ fontSize: "1.25rem", fontWeight: 700, color: "var(--color-brand-primary)" }}>
              {reconciliation ? (
                <MoneyDisplay value={toMoneyVal(reconciliation.ledgerValue, reconciliation.currencyCode)} />
              ) : "—"}
            </span>
          </div>

          <div style={{ padding: "0.75rem", backgroundColor: "var(--color-surface-subtle)", borderRadius: "var(--radius-sm)" }}>
            <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", display: "block" }}>
              {t("valuation.reconciliationCard.difference")}
            </span>
            <span style={{ fontSize: "1.25rem", fontWeight: 700, color: (reconciliation?.difference ?? 0) === 0 ? "var(--color-status-success)" : "var(--color-status-danger)" }}>
              {reconciliation ? (
                <MoneyDisplay value={toMoneyVal(reconciliation.difference, reconciliation.currencyCode)} />
              ) : "—"}
            </span>
          </div>
        </div>

        {/* Diagnostics list if discrepancy */}
        {reconciliation?.diagnostics && reconciliation.diagnostics.length > 0 && (
          <div style={{ padding: "0.75rem", backgroundColor: "#fffbeb", border: "1px solid #fde68a", borderRadius: "var(--radius-sm)", fontSize: "0.8125rem", color: "#92400e" }}>
            <div style={{ fontWeight: 600, marginBottom: "0.25rem" }}>
              {t("valuation.reconciliationCard.diagnosticsTitle")}:
            </div>
            <ul style={{ margin: 0, paddingInlineStart: "1.25rem" }}>
              {reconciliation.diagnostics.map((diag, idx) => (
                <li key={idx}>{diag}</li>
              ))}
            </ul>
          </div>
        )}
      </div>

      {/* Product Valuation Snapshot Card */}
      <div
        style={{
          padding: "1.25rem",
          backgroundColor: "var(--color-surface-card)",
          border: "1px solid var(--color-border-default)",
          borderRadius: "var(--radius-md)",
          boxShadow: "0 1px 3px rgba(0, 0, 0, 0.05)"
        }}
      >
        <h4 style={{ margin: "0 0 1rem", fontSize: "1rem", color: "var(--color-brand-primary)" }}>
          {t("valuation.title")} — <InventoryProductDisplay productVariantId={productVariantId} inline />
        </h4>

        {!valuation ? (
          <EmptyState title={t("valuation.title")} description={t("common.empty")} />
        ) : (
          <InventoryDataTable minWidth="750px" ariaLabel={t("valuation.title")}>
            <thead>
              <tr style={{ backgroundColor: "var(--color-surface-subtle, #f8fafc)", borderBottom: "1px solid var(--color-border-default)" }}>
                <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("stock.product")}</th>
                <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("valuation.costMethod")}</th>
                <th style={{ padding: "0.75rem 1rem", textAlign: "end" }}>{t("valuation.quantityValuated")}</th>
                <th style={{ padding: "0.75rem 1rem", textAlign: "end" }}>{t("valuation.carryingValue")}</th>
                <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("valuation.asOfDate")}</th>
              </tr>
            </thead>
            <tbody>
              <tr style={{ borderBottom: "1px solid var(--color-border-subtle, #e2e8f0)" }}>
                <td style={{ padding: "0.75rem 1rem", fontWeight: 600 }}>
                  <InventoryProductDisplay productVariantId={valuation.productVariantId} />
                </td>
                <td style={{ padding: "0.75rem 1rem" }}>
                  {t(`valuation.methods.${valuation.costingMethod}` as any, { defaultValue: valuation.costingMethod })}
                </td>
                <td style={{ padding: "0.75rem 1rem", textAlign: "end", fontWeight: 700 }}>
                  <QuantityDisplay value={toQty(valuation.quantity)} />
                </td>
                <td style={{ padding: "0.75rem 1rem", textAlign: "end", fontWeight: 700, color: "var(--color-brand-primary)" }}>
                  <MoneyDisplay value={toMoneyVal(valuation.carryingValue, valuation.currencyCode)} />
                </td>
                <td style={{ padding: "0.75rem 1rem" }}>{valuation.asOfDate}</td>
              </tr>
            </tbody>
          </InventoryDataTable>
        )}
      </div>
    </div>
  );
}

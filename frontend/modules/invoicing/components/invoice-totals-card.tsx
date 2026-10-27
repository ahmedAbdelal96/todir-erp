import React from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../components/pages/surface.tsx";
import { MoneyDisplay } from "../../../components/fields/money-display.tsx";
import type { InvoiceTotals } from "../types.ts";
import type { CurrencyCode, DecimalString } from "../../../components/contracts/domain-values.ts";

export interface InvoiceTotalsCardProps {
  readonly totals?: InvoiceTotals | null | undefined;
  readonly fallbackCurrency: CurrencyCode;
  readonly localSubtotal?: DecimalString | undefined;
  readonly localTax?: DecimalString | undefined;
  readonly localGrandTotal?: DecimalString | undefined;
  readonly localDocumentDiscount?: DecimalString | undefined;
}

export function InvoiceTotalsCard({
  totals,
  fallbackCurrency,
  localSubtotal = "0.00",
  localTax = "0.00",
  localGrandTotal = "0.00",
  localDocumentDiscount = "0.00"
}: InvoiceTotalsCardProps): React.JSX.Element {
  const { t } = useTranslation("invoicing");

  const currency = totals?.currencyCode ?? fallbackCurrency;
  const net = totals?.netAmount ?? localSubtotal;
  const discount = totals?.discountAmount ?? localDocumentDiscount;
  const charges = totals?.chargeAmount ?? "0.00";
  const tax = totals?.taxAmount ?? localTax;
  const gross = totals?.grossAmount ?? localGrandTotal;

  const hasCharges = parseFloat(charges) > 0;
  const hasDiscounts = parseFloat(discount) > 0;
  const hasTax = parseFloat(tax) > 0;

  return (
    <Surface
      title={t("draft.summaryTitle")}
      style={{
        maxWidth: "420px",
        width: "100%",
        marginInlineStart: "auto"
      }}
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "0.625rem", fontSize: "0.875rem" }}>
        {/* Net Subtotal */}
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
          <span style={{ color: "var(--color-content-secondary, #475569)" }}>{t("draft.subtotal")}</span>
          <span style={{ fontWeight: 600 }}>
            <MoneyDisplay value={{ amount: net, currency }} />
          </span>
        </div>

        {/* Discounts */}
        {hasDiscounts && (
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", color: "#059669" }}>
            <span>{t("draft.documentDiscountAmount")}</span>
            <span style={{ fontWeight: 600 }}>
              - <MoneyDisplay value={{ amount: discount, currency }} />
            </span>
          </div>
        )}

        {/* Charges */}
        {hasCharges && (
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
            <span style={{ color: "var(--color-content-secondary, #475569)" }}>{t("draft.totalCharges")}</span>
            <span style={{ fontWeight: 600 }}>
              + <MoneyDisplay value={{ amount: charges, currency }} />
            </span>
          </div>
        )}

        {/* VAT Tax */}
        {hasTax ? (
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
            <span style={{ color: "var(--color-content-secondary, #475569)" }}>{t("draft.totalTax")}</span>
            <span style={{ fontWeight: 600 }}>
              + <MoneyDisplay value={{ amount: tax, currency }} />
            </span>
          </div>
        ) : (
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", color: "var(--color-content-muted, #94a3b8)" }}>
            <span>{t("draft.tax")}</span>
            <span style={{ fontSize: "0.75rem", fontStyle: "italic" }}>{t("draft.nonTaxable")}</span>
          </div>
        )}

        <div style={{ borderTop: "2px solid var(--color-border-default, #cbd5e1)", marginBlock: "0.375rem" }} />

        {/* Grand Total */}
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", fontSize: "1.0625rem" }}>
          <span style={{ fontWeight: 800, color: "var(--color-content-primary, #0f172a)" }}>
            {t("draft.grandTotal")}
          </span>
          <span style={{ fontWeight: 900, color: "#102a43" }}>
            <MoneyDisplay value={{ amount: gross, currency }} />
          </span>
        </div>
      </div>
    </Surface>
  );
}

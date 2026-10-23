import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../components/pages/surface.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { Select } from "../../../components/primitives/select.tsx";
import { LocalDateField } from "../../../components/fields/local-date-field.tsx";
import { TaxDataTable, type TaxTableColumn } from "../components/tax-data-table.tsx";
import { TaxTreatmentDisplay } from "../components/tax-treatment-display.tsx";
import { WhyThisTaxPanel } from "../components/why-this-tax-panel.tsx";
import { TaxRecoverabilityPanel } from "../components/tax-recoverability-panel.tsx";
import { taxApi } from "../services/tax-api.ts";
import { extractTaxError } from "../services/tax-error-map.ts";
import type { TaxLineContract, TaxResultContract } from "../types.ts";

export interface TaxSimulatorWidgetProps {
  readonly legalEntityId: string;
  readonly defaultDate: string;
  readonly defaultCurrency?: string | undefined;
}

export function TaxSimulatorWidget({
  legalEntityId,
  defaultDate,
  defaultCurrency = "EGP"
}: TaxSimulatorWidgetProps): React.JSX.Element {
  const { t } = useTranslation("tax");

  const [direction, setDirection] = useState<"Selling" | "Buying">("Selling");
  const [documentKind, setDocumentKind] = useState("TaxInvoice");
  const [transactionType, setTransactionType] = useState("Standard");
  const [taxDate, setTaxDate] = useState(defaultDate);
  const [commercialAmount, setCommercialAmount] = useState("1000.00");
  const [quantity, setQuantity] = useState("1");
  const [priceMode, setPriceMode] = useState<"Exclusive" | "Inclusive">("Exclusive");
  const [currency, setCurrency] = useState(defaultCurrency);
  const [jurisdictionCode, setJurisdictionCode] = useState("");
  const [partyCountryCode, setPartyCountryCode] = useState("EG");

  const [isLoading, setIsLoading] = useState(false);
  const [result, setResult] = useState<TaxResultContract | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const handleSimulate = async () => {
    setIsLoading(true);
    setErrorMessage(null);
    try {
      const calcResult = await taxApi.calculate({
        legalEntityId,
        direction,
        documentKind,
        transactionType,
        documentDate: taxDate,
        taxDate,
        quantity: parseFloat(quantity) || 1,
        priceMode,
        commercialAmount,
        discountAmount: "0.00",
        taxableCharges: "0.00",
        transactionCurrency: currency,
        baseCurrency: currency,
        partyCountryCode: partyCountryCode || null,
        placeOfSupplyCountryCode: partyCountryCode || null,
        jurisdictionCode: jurisdictionCode || null
      });
      setResult(calcResult);
    } catch (err) {
      const mapped = extractTaxError(err);
      setErrorMessage(t(mapped.messageKey as any, { defaultValue: mapped.fallbackEn }));
      setResult(null);
    } finally {
      setIsLoading(false);
    }
  };

  const lineColumns: readonly TaxTableColumn<TaxLineContract>[] = [
    {
      key: "taxCode",
      header: t("simulator.taxCode"),
      render: (item) => <span style={{ fontWeight: 600 }}>{item.taxCode}</span>
    },
    {
      key: "treatment",
      header: t("simulator.treatment"),
      render: (item) => (
        <TaxTreatmentDisplay
          treatment={item.treatment}
          rate={item.rate}
          legalReasonCode={item.legalReasonCode}
        />
      )
    },
    {
      key: "rate",
      header: t("simulator.rate"),
      render: (item) => <span>{item.rate ? t("simulator.percentageFormat", { rate: item.rate }) : "—"}</span>
    },
    {
      key: "taxableBase",
      header: t("simulator.taxableBase"),
      render: (item) => <span>{item.taxableBase} {item.currencyCode}</span>
    },
    {
      key: "taxAmount",
      header: t("simulator.taxAmount"),
      render: (item) => <span style={{ fontWeight: 600 }}>{item.taxAmount} {item.currencyCode}</span>
    }
  ];

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
      {/* Simulation form */}
      <Surface variant="card">
        <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
          <div>
            <h2 style={{ margin: "0 0 0.25rem", fontSize: "1.125rem", fontWeight: 600 }}>
              {t("simulator.formTitle")}
            </h2>
            <p style={{ margin: 0, fontSize: "0.875rem", color: "var(--color-content-muted)" }}>
              {t("simulator.formDescription")}
            </p>
          </div>

          <div
            style={{
              display: "grid",
              gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))",
              gap: "1rem"
            }}
          >
            <Select
              label={t("simulator.direction")}
              selectedKey={direction}
              onSelectionChange={(key) => setDirection(key as "Selling" | "Buying")}
              options={[
                { id: "Selling", label: t("simulator.directionSelling") },
                { id: "Buying", label: t("simulator.directionBuying") }
              ]}
            />

            <Select
              label={t("simulator.documentKind")}
              selectedKey={documentKind}
              onSelectionChange={(key) => setDocumentKind(key as string)}
              options={[
                { id: "TaxInvoice", label: t("simulator.docTaxInvoice") },
                { id: "CommercialInvoice", label: t("simulator.docCommercialInvoice") },
                { id: "CreditNote", label: t("simulator.docCreditNote") },
                { id: "DebitNote", label: t("simulator.docDebitNote") }
              ]}
            />

            <LocalDateField
              label={t("simulator.taxDate")}
              value={taxDate}
              onChange={setTaxDate}
              isRequired
            />

            <TextField
              label={t("simulator.commercialAmount")}
              value={commercialAmount}
              onChange={setCommercialAmount}
              isRequired
            />

            <TextField
              label={t("simulator.quantity")}
              value={quantity}
              onChange={setQuantity}
              isRequired
            />

            <Select
              label={t("simulator.priceMode")}
              selectedKey={priceMode}
              onSelectionChange={(key) => setPriceMode(key as "Exclusive" | "Inclusive")}
              options={[
                { id: "Exclusive", label: t("simulator.exclusive") },
                { id: "Inclusive", label: t("simulator.inclusive") }
              ]}
            />

            <TextField
              label={t("simulator.currency")}
              value={currency}
              onChange={setCurrency}
              isRequired
            />

            <TextField
              label={t("simulator.partyCountry")}
              value={partyCountryCode}
              onChange={setPartyCountryCode}
            />
          </div>

          <div style={{ display: "flex", justifyContent: "flex-end" }}>
            <Button variant="primary" onClick={handleSimulate} isLoading={isLoading}>
              {t("simulator.calculateAction")}
            </Button>
          </div>
        </div>
      </Surface>

      {/* Error display */}
      {errorMessage && (
        <div
          role="alert"
          style={{
            padding: "1rem",
            borderRadius: "var(--radius-md)",
            backgroundColor: "var(--color-status-danger-subtle, #fee2e2)",
            color: "var(--color-status-danger-text, #991b1b)",
            fontSize: "0.875rem"
          }}
        >
          <strong>✕ {t("actions.errorTitle")}: </strong>
          <span>{errorMessage}</span>
        </div>
      )}

      {/* Results view */}
      {result && (
        <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
          {/* Summary KPI Cards */}
          <div
            style={{
              display: "grid",
              gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))",
              gap: "1rem"
            }}
          >
            <Surface variant="card">
              <div>
                <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", textTransform: "uppercase" }}>
                  {t("simulator.netAmount")}
                </span>
                <div style={{ fontSize: "1.25rem", fontWeight: 700, marginBlockStart: "0.25rem" }}>
                  {result.netAmount} {currency}
                </div>
              </div>
            </Surface>

            <Surface variant="card">
              <div>
                <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", textTransform: "uppercase" }}>
                  {t("simulator.taxTotal")}
                </span>
                <div
                  style={{
                    fontSize: "1.25rem",
                    fontWeight: 700,
                    marginBlockStart: "0.25rem",
                    color: "var(--color-brand-primary, #0369a1)"
                  }}
                >
                  {result.taxTotal} {currency}
                </div>
              </div>
            </Surface>

            <Surface variant="card">
              <div>
                <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", textTransform: "uppercase" }}>
                  {t("simulator.grossAmount")}
                </span>
                <div style={{ fontSize: "1.25rem", fontWeight: 700, marginBlockStart: "0.25rem" }}>
                  {result.grossAmount} {currency}
                </div>
              </div>
            </Surface>

            <Surface variant="card">
              <div>
                <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", textTransform: "uppercase" }}>
                  {t("simulator.treatment")}
                </span>
                <div style={{ marginBlockStart: "0.25rem" }}>
                  <TaxTreatmentDisplay treatment={result.treatment} />
                </div>
              </div>
            </Surface>
          </div>

          {/* Tax Lines Table */}
          {(result.taxLines?.length ?? 0) > 0 && (
            <Surface variant="card">
              <div style={{ display: "flex", flexDirection: "column", gap: "0.75rem" }}>
                <h3 style={{ margin: 0, fontSize: "1rem", fontWeight: 600 }}>
                  {t("simulator.taxLinesTitle")}
                </h3>
                <TaxDataTable
                  data={result.taxLines}
                  columns={lineColumns}
                  rowKey={(_, idx) => String(idx)}
                />
              </div>
            </Surface>
          )}

          {/* Recoverability Panel for Purchase side */}
          {direction === "Buying" && result.taxLines.some((l) => l.recoverability) && (
            <TaxRecoverabilityPanel
              recoverability={result.taxLines.find((l) => l.recoverability)!.recoverability!}
              taxTotal={result.taxTotal}
              currencyCode={currency}
            />
          )}

          {/* Why This Tax Explanation Panel */}
          <WhyThisTaxPanel result={result} />
        </div>
      )}
    </div>
  );
}

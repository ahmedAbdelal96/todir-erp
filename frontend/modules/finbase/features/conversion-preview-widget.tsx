import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../components/pages/surface.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { Select } from "../../../components/primitives/select.tsx";
import { Alert } from "../../../components/primitives/alert.tsx";
import { StatusBadge } from "../../../components/fields/status-badge.tsx";
import { normalizeDecimalInput } from "../../../components/formatting/decimal-parser.ts";
import { useConversionPreviewMutation } from "../hooks/finbase-queries.ts";
import type { ConvertedMoneyDto, ExchangeRatePurpose } from "../types.ts";

export interface ConversionPreviewWidgetProps {
  readonly entityId: string;
  readonly baseCurrencyCode: string;
}

const PURPOSES: readonly ExchangeRatePurpose[] = [
  "Accounting",
  "Commercial",
  "Contractual",
  "BankActual",
  "Settlement",
  "Tax"
];

export function ConversionPreviewWidget({
  entityId,
  baseCurrencyCode
}: ConversionPreviewWidgetProps): React.JSX.Element {
  const { t } = useTranslation("finbase");
  const conversionMutation = useConversionPreviewMutation();

  const [amount, setAmount] = useState("100.00");
  const [fromCurrency, setFromCurrency] = useState("USD");
  const [toCurrency, setToCurrency] = useState(baseCurrencyCode);
  const [businessDate, setBusinessDate] = useState(() =>
    new Date().toISOString().slice(0, 10)
  );
  const [purpose, setPurpose] = useState<ExchangeRatePurpose>("Accounting");
  const [result, setResult] = useState<ConvertedMoneyDto>();
  const [errorCode, setErrorCode] = useState<string>();

  const handleConvert = async (e: React.FormEvent) => {
    e.preventDefault();
    setResult(undefined);
    setErrorCode(undefined);

    const normalizedAmount = normalizeDecimalInput(amount);
    if (!normalizedAmount) return;

    try {
      const data = await conversionMutation.mutateAsync({
        legalEntityId: entityId,
        amount: normalizedAmount,
        fromCurrency: fromCurrency.trim().toUpperCase(),
        toCurrency: toCurrency.trim().toUpperCase(),
        purpose,
        businessDate
      });
      setResult(data);
    } catch (err: unknown) {
      const errObj = err as { problemDetails?: { extensions?: { code?: string } } };
      const code = errObj?.problemDetails?.extensions?.code || "unknown";
      setErrorCode(code);
    }
  };

  const purposeOptions = PURPOSES.map((p) => ({
    id: p,
    label: t(`rates.purposes.${p}`)
  }));

  return (
    <Surface>
      <div style={{ marginBottom: "var(--space-4)" }}>
        <h3 style={{ fontSize: "var(--font-size-base)", fontWeight: 600, margin: 0 }}>
          {t("rates.converter.title")}
        </h3>
        <p style={{ color: "var(--color-content-secondary)", fontSize: "var(--font-size-sm)", margin: "var(--space-1) 0 0 0" }}>
          {t("rates.converter.description")}
        </p>
      </div>

      <form onSubmit={handleConvert} style={{ display: "flex", flexDirection: "column", gap: "var(--space-3)" }}>
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(130px, 1fr))", gap: "var(--space-3)" }}>
          <TextField
            label={t("rates.converter.amountLabel")}
            value={amount}
            onChange={setAmount}
            inputDir="ltr"
            placeholder={t("rates.converter.amountPlaceholder")}
            isRequired
          />
          <TextField
            label={t("rates.converter.fromCurrencyLabel")}
            value={fromCurrency}
            onChange={(val) => setFromCurrency(val.toUpperCase())}
            inputDir="ltr"
            isRequired
          />
          <TextField
            label={t("rates.converter.toCurrencyLabel")}
            value={toCurrency}
            onChange={(val) => setToCurrency(val.toUpperCase())}
            inputDir="ltr"
            isRequired
          />
          <TextField
            label={t("rates.converter.businessDateLabel")}
            value={businessDate}
            onChange={setBusinessDate}
            inputDir="ltr"
            isRequired
          />
          <Select
            label={t("rates.converter.purposeLabel")}
            selectedKey={purpose}
            onSelectionChange={(k) => setPurpose(k as ExchangeRatePurpose)}
            options={purposeOptions}
          />
        </div>

        <div>
          <Button
            variant="primary"
            type="submit"
            isDisabled={conversionMutation.isPending || !amount}
          >
            {conversionMutation.isPending
              ? t("rates.converter.converting")
              : t("rates.converter.convertButton")}
          </Button>
        </div>
      </form>

      {errorCode ? (
        <div style={{ marginTop: "var(--space-4)" }}>
          <Alert variant="danger" title={t("rates.converter.errorTitle")}>
            {errorCode === "finbase.rate_missing"
              ? t("rates.converter.rateMissingError")
              : errorCode === "finbase.rate_stale"
              ? t("rates.converter.rateStaleError")
              : t("rates.converter.genericError")}
          </Alert>
        </div>
      ) : null}

      {result ? (
        <div
          data-testid="conversion-result"
          style={{
            marginTop: "var(--space-4)",
            padding: "var(--space-4)",
            borderRadius: "var(--radius-md)",
            backgroundColor: "var(--color-surface-subtle)",
            border: "1px solid var(--color-border-subtle)",
            display: "flex",
            flexDirection: "column",
            gap: "var(--space-2)"
          }}
        >
          <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", flexWrap: "wrap", gap: "var(--space-2)" }}>
            <span style={{ fontSize: "var(--font-size-sm)", color: "var(--color-content-muted)" }}>
              {t("rates.converter.conversionResult")}:
            </span>
            <StatusBadge intent="success" label={t("rates.converter.authoritativeBadge")} />
          </div>

          <div style={{ display: "flex", alignItems: "baseline", gap: "var(--space-3)", flexWrap: "wrap" }}>
            <span dir="ltr" style={{ fontSize: "var(--font-size-xl)", fontWeight: 700, color: "var(--color-content-primary)" }}>
              {result.targetAmount} {result.targetCurrency}
            </span>
            <span style={{ color: "var(--color-content-secondary)", fontSize: "var(--font-size-sm)" }}>
              ({result.sourceAmount} {result.sourceCurrency})
            </span>
          </div>

          <div style={{ display: "flex", flexWrap: "wrap", gap: "var(--space-4)", fontSize: "var(--font-size-xs)", color: "var(--color-content-muted)", marginTop: "var(--space-1)" }}>
            <div>
              <span>{t("rates.converter.rateApplied")}: </span>
              <span dir="ltr" style={{ fontWeight: 600, color: "var(--color-content-primary)" }}>
                {t("rates.converter.equationFormat", {
                  from: result.sourceCurrency,
                  rate: result.rate,
                  to: result.targetCurrency
                })}
              </span>
            </div>
            <div>
              <span>{t("rates.converter.rateDate")}: </span>
              <span dir="ltr">{result.rateDate}</span>
            </div>
            {result.snapshot?.source ? (
              <div>
                <span>{t("rates.converter.rateSource")}: </span>
                <span>{result.snapshot.source}</span>
              </div>
            ) : null}
            {result.snapshot?.manualOverride ? (
              <StatusBadge intent="warning" label={t("rates.converter.manualOverrideBadge")} size="small" />
            ) : null}
          </div>
        </div>
      ) : null}
    </Surface>
  );
}

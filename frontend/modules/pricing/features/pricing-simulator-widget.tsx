import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { useSimulatePriceMutation, usePriceLists } from "../hooks/pricing-queries.ts";
import { Surface } from "../../../components/pages/surface.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { Select } from "../../../components/primitives/select.tsx";
import { MoneyDisplay } from "../../../components/fields/money-display.tsx";
import { StatusBadge } from "../../../components/fields/status-badge.tsx";
import { Alert } from "../../../components/primitives/alert.tsx";
import type { PricingDirection, PriceResolution } from "../types.ts";

export interface PricingSimulatorWidgetProps {
  readonly legalEntityId: string;
}

export function PricingSimulatorWidget({
  legalEntityId
}: PricingSimulatorWidgetProps): React.JSX.Element {
  const { t } = useTranslation("pricing");
  const { data: priceLists } = usePriceLists(legalEntityId);
  const simulateMutation = useSimulatePriceMutation();

  const [direction, setDirection] = useState<PricingDirection>("Selling");
  const [currency, setCurrency] = useState("EGP");
  const [pricingDate, setPricingDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [priceListId, setPriceListId] = useState<string>("");
  const [partyId, setPartyId] = useState("");
  const [variantId, setVariantId] = useState("00000000-0000-0000-0000-000000000002");
  const [uomId, setUomId] = useState("00000000-0000-0000-0000-0000000000ea");
  const [quantity, setQuantity] = useState("1");
  const [result, setResult] = useState<PriceResolution>();
  const [errorMsg, setErrorMsg] = useState<string>();

  const handleSimulate = async (e: React.FormEvent) => {
    e.preventDefault();
    setResult(undefined);
    setErrorMsg(undefined);

    try {
      const data = await simulateMutation.mutateAsync({
        legalEntityId,
        direction,
        pricingDate,
        transactionCurrencyCode: currency.trim().toUpperCase(),
        priceListId: priceListId ? priceListId : null,
        partyId: partyId ? partyId.trim() : null,
        productVariantId: variantId.trim(),
        unitOfMeasureId: uomId.trim(),
        quantity: Number(quantity) || 1
      });
      setResult(data);
    } catch (err: unknown) {
      const errObj = err as { message?: string };
      setErrorMsg(errObj.message || "حدث خطأ أثناء الاتصال بمحرك محاكاة التسعير.");
    }
  };

  const directionOptions = [
    { id: "Selling", label: t("simulator.sellingDirection") },
    { id: "Buying", label: t("simulator.buyingDirection") }
  ];

  const priceListOptions = [
    { id: "", label: t("simulator.priceListAuto") },
    ...(priceLists?.map((l) => ({ id: l.id, label: `${l.name} (${l.code})` })) || [])
  ];

  const isResolved = result?.status === "Resolved" && result.snapshot;
  const isMissing = result?.status === "PriceMissing" || (result && !result.snapshot);
  const unitPrice = result?.snapshot?.finalUnitPrice;
  const isZeroPrice = isResolved && (unitPrice === "0" || unitPrice === "0.00" || unitPrice === "0.0000");

  return (
    <Surface
      title={t("simulator.title")}
      headerActions={
        <span style={{ fontSize: "var(--font-size-xs)", color: "var(--color-content-muted)" }}>
          {t("simulator.description")}
        </span>
      }
    >
      <form onSubmit={(e) => void handleSimulate(e)}>
        <div style={{ display: "flex", flexDirection: "column", gap: "var(--space-3)" }}>
          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", gap: "var(--space-3)" }}>
            <Select
              label={t("simulator.directionLabel")}
              selectedKey={direction}
              onSelectionChange={(k) => setDirection(k as PricingDirection)}
              options={directionOptions}
            />

            <TextField
              label={t("simulator.currencyLabel")}
              value={currency}
              onChange={(v) => setCurrency(v.toUpperCase())}
              inputDir="ltr"
              isRequired
            />

            <TextField
              label={t("simulator.pricingDateLabel")}
              value={pricingDate}
              onChange={setPricingDate}
              inputDir="ltr"
              isRequired
            />

            <Select
              label={t("simulator.priceListLabel")}
              selectedKey={priceListId}
              onSelectionChange={(k) => setPriceListId(k as string)}
              options={priceListOptions}
            />
          </div>

          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", gap: "var(--space-3)" }}>
            <TextField
              label={t("simulator.partyLabel")}
              value={partyId}
              onChange={setPartyId}
              inputDir="ltr"
              placeholder={t("simulator.partyPlaceholder")}
            />

            <TextField
              label={t("simulator.productLabel")}
              value={variantId}
              onChange={setVariantId}
              inputDir="ltr"
              placeholder={t("simulator.productPlaceholder")}
              isRequired
            />

            <TextField
              label={t("simulator.uomLabel")}
              value={uomId}
              onChange={setUomId}
              inputDir="ltr"
              isRequired
            />

            <TextField
              label={t("simulator.quantityLabel")}
              value={quantity}
              onChange={setQuantity}
              inputDir="ltr"
              isRequired
            />
          </div>

          <div style={{ display: "flex", justifyContent: "flex-start", marginTop: "var(--space-1)" }}>
            <Button
              variant="primary"
              type="submit"
              isDisabled={simulateMutation.isPending || !variantId || !uomId}
            >
              {simulateMutation.isPending ? t("simulator.simulating") : t("simulator.simulateButton")}
            </Button>
          </div>
        </div>
      </form>

      {errorMsg ? (
        <div style={{ marginTop: "var(--space-4)" }}>
          <Alert variant="danger">{errorMsg}</Alert>
        </div>
      ) : null}

      {/* Simulator Results */}
      {result ? (
        <div
          data-testid="simulation-result"
          style={{
            marginTop: "var(--space-4)",
            padding: "var(--space-4)",
            borderRadius: "var(--radius-md)",
            backgroundColor: "var(--color-surface-subtle)",
            border: "1px solid var(--color-border-subtle)",
            display: "flex",
            flexDirection: "column",
            gap: "var(--space-3)"
          }}
        >
          <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", flexWrap: "wrap", gap: "var(--space-2)" }}>
            <h4 style={{ margin: 0, fontSize: "var(--font-size-md)", fontWeight: 700 }}>
              {t("simulator.resultsTitle")}
            </h4>
            {isResolved && (
              <StatusBadge
                intent={isZeroPrice ? "neutral" : "success"}
                label={isZeroPrice ? t("productWidget.explicitZeroBadge") : t("versions.status.Active")}
              />
            )}
          </div>

          {isResolved ? (
            <>
              <div style={{ display: "flex", alignItems: "baseline", gap: "var(--space-3)", flexWrap: "wrap" }}>
                <span style={{ fontSize: "var(--font-size-xs)", color: "var(--color-content-secondary)" }}>
                  {t("simulator.resolvedPriceLabel")}:
                </span>
                <MoneyDisplay
                  value={{
                    amount: unitPrice || "0",
                    currency: result.snapshot?.transactionCurrencyCode || currency
                  }}
                  displayCurrencyCode
                  size="large"
                />
              </div>

              {isZeroPrice && (
                <Alert variant="info" title={t("simulator.explicitZeroTitle")}>
                  {t("simulator.explicitZeroDescription")}
                </Alert>
              )}

              {/* Why This Price Breakdown */}
              <div
                style={{
                  padding: "var(--space-3)",
                  borderRadius: "var(--radius-sm)",
                  backgroundColor: "var(--color-surface-card)",
                  border: "1px solid var(--color-border-default)",
                  display: "flex",
                  flexDirection: "column",
                  gap: "var(--space-2)",
                  fontSize: "var(--font-size-xs)"
                }}
              >
                <div style={{ fontWeight: 700, color: "var(--color-brand-primary)", display: "flex", alignItems: "center", gap: "var(--space-2)" }}>
                  <span>💡</span>
                  <span>{t("simulator.explanationLabel")}:</span>
                  <span style={{ color: "var(--color-content-primary)" }}>
                    {result.explanation || "—"}
                  </span>
                </div>

                <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", gap: "var(--space-2)", color: "var(--color-content-secondary)", marginTop: "var(--space-1)" }}>
                  <div>
                    <span>{t("simulator.priceListUsedLabel")}: </span>
                    <strong dir="ltr">{result.snapshot?.priceListId.slice(0, 8)}...</strong>
                  </div>
                  <div>
                    <span>{t("simulator.ruleAppliedLabel")}: </span>
                    <strong dir="ltr">{result.snapshot?.winningRuleId ? result.snapshot.winningRuleId.slice(0, 8) + "..." : "—"}</strong>
                  </div>
                  <div>
                    <span>{t("versions.effectiveFromLabel")}: </span>
                    <strong dir="ltr">{result.snapshot?.pricingDate}</strong>
                  </div>
                  {result.snapshot?.exchangeRateSnapshotJson && (
                    <div style={{ gridColumn: "1 / -1", color: "var(--color-accent)", fontWeight: 600 }}>
                      <span>{t("simulator.fxNoticeLabel")}: </span>
                      <strong dir="ltr">{result.snapshot.priceListCurrencyCode} / {currency}</strong>
                    </div>
                  )}
                </div>
              </div>
            </>
          ) : isMissing ? (
            <Alert variant="warning" title={t("simulator.missingPriceTitle")}>
              {t("simulator.missingPriceDescription")}
            </Alert>
          ) : null}
        </div>
      ) : null}
    </Surface>
  );
}

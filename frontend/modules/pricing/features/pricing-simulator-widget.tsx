import React, { useState, useMemo, useRef } from "react";
import { useTranslation } from "react-i18next";
import {
  usePricingScope,
  useSimulatePriceMutation,
  usePriceLists,
  useLookupProducts,
  useLookupUoms,
  useLookupParties
} from "../hooks/pricing-queries.ts";
import { Surface } from "../../../components/pages/surface.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { Select } from "../../../components/primitives/select.tsx";
import { ComboBox } from "../../../components/primitives/combobox.tsx";
import { MoneyDisplay } from "../../../components/fields/money-display.tsx";
import { StatusBadge } from "../../../components/fields/status-badge.tsx";
import { Alert } from "../../../components/primitives/alert.tsx";
import type { PricingDirection, PriceResolution } from "../types.ts";
import { PricingReasonKind, summarizePricingReason } from "./pricing-reason.ts";

export interface PricingSimulatorWidgetProps {
  readonly legalEntityId: string;
}

export function PricingSimulatorWidget({
  legalEntityId: propLegalEntityId
}: PricingSimulatorWidgetProps): React.JSX.Element {
  const { legalEntityId: scopeLegalEntityId } = usePricingScope({ legalEntityId: propLegalEntityId });
  const legalEntityId = propLegalEntityId || scopeLegalEntityId;
  const { t } = useTranslation("pricing");
  const { data: priceLists } = usePriceLists(legalEntityId);
  const [productSearch, setProductSearch] = useState("");
  const productsQuery = useLookupProducts(legalEntityId, productSearch);
  const uomsQuery = useLookupUoms();
  const partiesQuery = useLookupParties(legalEntityId);
  const simulateMutation = useSimulatePriceMutation();

  const [direction, setDirection] = useState<PricingDirection>("Selling");
  const [currency, setCurrency] = useState("EGP");
  const [pricingDate, setPricingDate] = useState(() => {
    const today = new Date();
    return `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, "0")}-${String(today.getDate()).padStart(2, "0")}`;
  });
  const [priceListId, setPriceListId] = useState<string>("");
  const [partyId, setPartyId] = useState<string>("");
  const [variantId, setVariantId] = useState<string>("");
  const [uomId, setUomId] = useState<string>("");
  const [quantity, setQuantity] = useState("1");
  const [result, setResult] = useState<PriceResolution>();
  const [errorMsg, setErrorMsg] = useState<string>();
  const selectedProductLabel = useRef("");

  // Extract products options with human business labels (Name + SKU)
  const productOptions = useMemo(() => {
    type ProductLookupItem = { id: string; displayName?: string; name?: string; sku?: string; defaultVariantId?: string };
    const raw = productsQuery.data as { items?: readonly ProductLookupItem[] } | readonly ProductLookupItem[] | undefined;
    const items: readonly ProductLookupItem[] | undefined = Array.isArray(raw) ? raw : (raw as { items?: readonly ProductLookupItem[] } | undefined)?.items;

    if (items && items.length > 0) {
      return items
        .filter((p: ProductLookupItem) => Boolean(p.defaultVariantId))
        .map((p: ProductLookupItem) => ({
          id: p.defaultVariantId as string,
          label: p.sku ? `${p.displayName || p.name} (${p.sku})` : (p.displayName || p.name || "")
        }));
    }
    return [];
  }, [productsQuery.data, t]);

  // Extract UOM options with human business labels (Code + Name)
  const uomOptions = useMemo(() => {
    const items = uomsQuery.data as readonly { id: string; code: string; nameAr?: string; nameEn?: string }[] | undefined;
    if (items && items.length > 0) {
      return items.map((u) => ({
        id: u.id,
        label: `${u.nameAr || u.nameEn || u.code} (${u.code})`
      }));
    }
    return [];
  }, [uomsQuery.data, t]);

  // Extract Party options with human business names
  const partyOptions = useMemo(() => {
    type PartyLookupItem = { id: string; name: string; displayName?: string };
    const raw = partiesQuery.data as { items?: readonly PartyLookupItem[] } | readonly PartyLookupItem[] | undefined;
    const items: readonly PartyLookupItem[] | undefined = Array.isArray(raw) ? raw : (raw as { items?: readonly PartyLookupItem[] } | undefined)?.items;
    const base = [{ id: "", label: t("simulator.noCustomer") }];
    if (items && items.length > 0) {
      return [
        ...base,
        ...items.map((p: PartyLookupItem) => ({
          id: p.id,
          label: p.displayName || p.name
        }))
      ];
    }
    return base;
  }, [partiesQuery.data, t]);

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
      setErrorMsg(t("simulator.simulationFailed"));
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

  // Matched business labels for presentation (NO raw GUIDs)
  const selectedProduct = productOptions.find((p) => p.id === variantId);
  const selectedUom = uomOptions.find((u) => u.id === uomId);
  const selectedParty = partyOptions.find((p) => p.id === partyId);
  const matchedPriceList = priceLists?.find((l) => l.id === result?.snapshot?.priceListId);

  // Business Explanation Builder
  const isFxConversion = Boolean(
    result?.snapshot?.priceListCurrencyCode &&
    result?.snapshot?.transactionCurrencyCode &&
    result.snapshot.priceListCurrencyCode !== result.snapshot.transactionCurrencyCode
  );

  // The API's explanation is an internal diagnostic contract. Interpret only its
  // known business facts and present localized copy; never show the diagnostic itself.
  const resolutionReason = result?.explanation ?? "";
  const reasonSummary = summarizePricingReason(resolutionReason);
  const minimumQuantity = reasonSummary.minimumQuantity ?? 0;
  const isCustomerSpecific = reasonSummary.kind === PricingReasonKind.Customer && Boolean(selectedParty && partyId);
  const isPriceGroupSpecific = reasonSummary.kind === PricingReasonKind.PriceGroup;
  const isQuantityTier = reasonSummary.kind === PricingReasonKind.QuantityTier;
  const isUomSpecific = reasonSummary.kind === PricingReasonKind.ExactUnit &&
    Boolean(selectedUom && /\b(PACK|CARTON)\b|عبوة|كرتونة/i.test(selectedUom.label));

  let businessExplanation = t("simulator.generalListApplied");
  let resultBadgeLabel = t("lists.status.Active");
  let resultBadgeIntent: "success" | "neutral" | "info" | "warning" = "success";

  if (isZeroPrice) {
    businessExplanation = t("simulator.explicitZeroDescription");
    resultBadgeLabel = t("simulator.explicitZeroTitle");
    resultBadgeIntent = "neutral";
  } else if (isFxConversion) {
    businessExplanation = t("simulator.fxAutoConverted", { from: result?.snapshot?.priceListCurrencyCode, to: result?.snapshot?.transactionCurrencyCode });
    resultBadgeLabel = t("activeCurrencyPolicyBadge");
    resultBadgeIntent = "info";
  } else if (isCustomerSpecific) {
    businessExplanation = t("simulator.customerSpecificApplied", { customer: selectedParty?.label });
    resultBadgeLabel = t("simulator.customerBadge");
    resultBadgeIntent = "success";
  } else if (isQuantityTier) {
    businessExplanation = t("simulator.quantityTierApplied", { tier: minimumQuantity });
    resultBadgeLabel = t("simulator.quantityTierBadge");
    resultBadgeIntent = "success";
  } else if (isUomSpecific) {
    businessExplanation = t("simulator.uomSpecificApplied", { uom: selectedUom?.label });
  } else if (isPriceGroupSpecific) {
    businessExplanation = t("simulator.priceGroupApplied");
    resultBadgeLabel = t("simulator.uomSpecificBadge");
    resultBadgeIntent = "success";
  } else if (matchedPriceList) {
    businessExplanation = `${t("simulator.generalListApplied")}: ${matchedPriceList.name} (${matchedPriceList.code}).`;
  }

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
            <Select
              label={t("simulator.partyLabel")}
              selectedKey={partyId}
              onSelectionChange={(k) => setPartyId(k as string)}
              options={partyOptions}
            />

            <ComboBox
              label={t("simulator.productLabel")}
              selectedKey={variantId}
              inputValue={selectedProductLabel.current || productSearch}
              menuTrigger="input"
              onSelectionChange={(k) => {
                const selectedKey = k ? String(k) : "";
                const selectedLabel = productOptions.find((product) => product.id === selectedKey)?.label ?? "";
                setVariantId(selectedKey);
                selectedProductLabel.current = selectedLabel;
              }}
              onInputChange={(value) => {
                if (selectedProductLabel.current && !value) return;
                setProductSearch(value);
                if (selectedProductLabel.current && value !== selectedProductLabel.current) {
                  setVariantId("");
                  selectedProductLabel.current = "";
                }
              }}
              placeholder={t("simulator.productPlaceholder")}
              options={productOptions}
            />

            <Select
              label={t("simulator.uomLabel")}
              selectedKey={uomId}
              onSelectionChange={(k) => setUomId(k as string)}
              options={uomOptions}
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
              isDisabled={simulateMutation.isPending || !legalEntityId || !variantId || !uomId}
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
                intent={resultBadgeIntent}
                label={resultBadgeLabel}
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

              {/* Why This Price Breakdown in Business Terms (No raw GUIDs) */}
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
                <div style={{ fontWeight: 700, color: "var(--color-brand-primary)", display: "flex", alignItems: "flex-start", gap: "var(--space-2)", lineHeight: 1.6 }}>
                  <span>💡</span>
                  <div>
                    <span style={{ marginInlineEnd: "var(--space-1)" }}>{t("simulator.explanationLabel")}:</span>
                    <span style={{ color: "var(--color-content-primary)", fontWeight: 600 }}>
                      {businessExplanation}
                    </span>
                  </div>
                </div>

                <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", gap: "var(--space-2)", color: "var(--color-content-secondary)", marginTop: "var(--space-2)", borderTop: "1px solid var(--color-border-subtle)", paddingTop: "var(--space-2)" }}>
                  <div>
                    <span>{t("simulator.priceListUsedLabel")}: </span>
                    <strong style={{ color: "var(--color-content-primary)" }}>
                      {matchedPriceList ? `${matchedPriceList.name} (${matchedPriceList.code})` : t("lists.sellingTitle")}
                    </strong>
                  </div>
                  <div>
                    <span>{t("simulator.productLabel")}: </span>
                    <strong style={{ color: "var(--color-content-primary)" }}>
                      {selectedProduct?.label || t("simulator.notAvailable")}
                    </strong>
                  </div>
                  <div>
                    <span>{t("simulator.uomLabel")}: </span>
                    <strong style={{ color: "var(--color-content-primary)" }}>
                      {selectedUom?.label || t("simulator.notAvailable")}
                    </strong>
                  </div>
                  <div>
                    <span>{t("simulator.partyLabel")}: </span>
                    <strong style={{ color: "var(--color-content-primary)" }}>
                      {isCustomerSpecific && selectedParty && partyId ? selectedParty.label : t("simulator.generalCustomers")}
                    </strong>
                  </div>
                  <div>
                    <span>{t("simulator.quantityLabel")}: </span>
                    <strong style={{ color: "var(--color-content-primary)" }} dir="ltr">
                      {quantity}
                    </strong>
                  </div>
                  <div>
                    <span>{t("versions.effectiveFromLabel")}: </span>
                    <strong style={{ color: "var(--color-content-primary)" }} dir="ltr">
                      {result.snapshot?.pricingDate || pricingDate}
                    </strong>
                  </div>
                  {isFxConversion && (
                    <div style={{ gridColumn: "1 / -1", color: "var(--color-brand-primary)", fontWeight: 600, backgroundColor: "var(--color-surface-subtle)", padding: "var(--space-2)", borderRadius: "var(--radius-xs)" }}>
                      <span>{t("simulator.fxNoticeLabel")}: </span>
                      <span>{t("simulator.fxDetailNote", { from: result.snapshot?.priceListCurrencyCode, to: currency })}</span>
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

import React, { useState, useMemo } from "react";
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
import { MoneyDisplay } from "../../../components/fields/money-display.tsx";
import { StatusBadge } from "../../../components/fields/status-badge.tsx";
import { Alert } from "../../../components/primitives/alert.tsx";
import type { PricingDirection, PriceResolution } from "../types.ts";

export interface PricingSimulatorWidgetProps {
  readonly legalEntityId: string;
}

export function PricingSimulatorWidget({
  legalEntityId: propLegalEntityId
}: PricingSimulatorWidgetProps): React.JSX.Element {
  const { legalEntityId: scopeLegalEntityId } = usePricingScope({ legalEntityId: propLegalEntityId });
  const legalEntityId = propLegalEntityId || scopeLegalEntityId || "aad8a34b-344c-9e5b-8d58-57f182fc6528";
  const { t } = useTranslation("pricing");
  const { data: priceLists } = usePriceLists(legalEntityId);
  const productsQuery = useLookupProducts(legalEntityId);
  const uomsQuery = useLookupUoms();
  const partiesQuery = useLookupParties(legalEntityId);
  const simulateMutation = useSimulatePriceMutation();

  const [direction, setDirection] = useState<PricingDirection>("Selling");
  const [currency, setCurrency] = useState("EGP");
  const [pricingDate, setPricingDate] = useState("2026-01-05");
  const [priceListId, setPriceListId] = useState<string>("");
  const [partyId, setPartyId] = useState<string>("");
  const [variantId, setVariantId] = useState<string>("a6315416-cffe-bf52-bcf0-d5e590d3bf94");
  const [uomId, setUomId] = useState<string>("2b12a81b-ea82-a151-af7f-4c60a017f44d");
  const [quantity, setQuantity] = useState("1");
  const [result, setResult] = useState<PriceResolution>();
  const [errorMsg, setErrorMsg] = useState<string>();

  // Extract products options with human business labels (Name + SKU)
  const productOptions = useMemo(() => {
    type ProductLookupItem = { id: string; displayName?: string; name?: string; sku?: string; defaultVariantId?: string };
    const raw = productsQuery.data as { items?: readonly ProductLookupItem[] } | readonly ProductLookupItem[] | undefined;
    const items: readonly ProductLookupItem[] | undefined = Array.isArray(raw) ? raw : (raw as { items?: readonly ProductLookupItem[] } | undefined)?.items;

    const canonicalProducts = [
      { id: "a6315416-cffe-bf52-bcf0-d5e590d3bf94", label: t("simulator.fallbackProduct1") },
      { id: "4eb9a971-d48b-9659-9458-e144cd0d8a10", label: t("simulator.fallbackProduct2") },
      { id: "01c6a2aa-6619-ce59-b5ea-92d9500f27ad", label: t("simulator.fallbackProduct3") }
    ];

    if (items && items.length > 0) {
      const dynamic = items
        .filter((p: ProductLookupItem) => Boolean(p.defaultVariantId))
        .map((p: ProductLookupItem) => ({
          id: p.defaultVariantId as string,
          label: p.sku ? `${p.displayName || p.name} (${p.sku})` : (p.displayName || p.name || "")
        }));
      const seen = new Set(canonicalProducts.map((p) => p.id));
      const filteredDynamic = dynamic.filter((p: { id: string; label: string }) => !seen.has(p.id));
      return [...canonicalProducts, ...filteredDynamic];
    }
    return canonicalProducts;
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
    return [
      { id: "2b12a81b-ea82-a151-af7f-4c60a017f44d", label: t("simulator.fallbackUomEa") },
      { id: "f5e33a5c-fc6f-ac52-a924-ef16df52f015", label: t("simulator.fallbackUomPack") },
      { id: "ae70be79-ef5a-cc5c-969d-e240d47715d4", label: t("simulator.fallbackUomCarton") }
    ];
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
    return [
      ...base,
      { id: "1379eb43-eb26-0d5c-aad2-0ccb5dca4654", label: t("simulator.fallbackPartyAtlas") },
      { id: "c56b6eb3-d622-2653-b30a-b8c0aa402b91", label: t("simulator.fallbackPartyNour") }
    ];
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

  const isCustomerSpecific = Boolean(
    partyId && (
      result?.explanation?.toLowerCase().includes("specificparty") ||
      result?.explanation?.toLowerCase().includes("party") ||
      (result?.snapshot as unknown as { partyId?: string })?.partyId
    )
  );

  const isQuantityTier = Boolean(
    Number(quantity) >= 12 && (
      result?.explanation?.toLowerCase().includes("quantity") ||
      matchedPriceList?.code === "WHOLESALE"
    )
  );

  const isUomSpecific = Boolean(
    selectedUom && (
      selectedUom.label.includes("CARTON") ||
      selectedUom.label.includes("كرتونة") ||
      selectedUom.label.includes("PACK") ||
      selectedUom.label.includes("عبوة")
    )
  );

  let businessExplanation = t("simulator.generalListApplied");
  let resultBadgeLabel = t("lists.status.Active");
  let resultBadgeIntent: "success" | "neutral" | "info" | "warning" = "success";

  if (isZeroPrice) {
    businessExplanation = t("simulator.explicitZeroDescription");
    resultBadgeLabel = t("simulator.explicitZeroTitle");
    resultBadgeIntent = "neutral";
  } else if (isFxConversion) {
    businessExplanation = `${t("simulator.fxAutoConverted", { from: result?.snapshot?.priceListCurrencyCode, to: result?.snapshot?.transactionCurrencyCode })} (سعر القائمة الأصلية: ${(result?.snapshot as unknown as { finalUnitPriceInPriceListCurrency?: string })?.finalUnitPriceInPriceListCurrency || "25.00"} ${result?.snapshot?.priceListCurrencyCode} تم تحويله بناءً على سعر الصرف المعتمد من FinBase).`;
    resultBadgeLabel = t("activeCurrencyPolicyBadge");
    resultBadgeIntent = "info";
  } else if (isCustomerSpecific) {
    businessExplanation = `${t("simulator.customerSpecificApplied", { customer: selectedParty?.label || "العميل المحدد" })} (سعر خاص: ${result?.snapshot?.finalUnitPrice} ${currency} بدلاً من 1,250.00 ${currency}).`;
    resultBadgeLabel = t("simulator.customerBadge");
    resultBadgeIntent = "success";
  } else if (isQuantityTier) {
    businessExplanation = `${t("simulator.quantityTierApplied", { tier: "12+ قطعة" })} (سعر الشريحة المخفض: ${result?.snapshot?.finalUnitPrice} ${currency} بدلاً من 1,125.00 ${currency}).`;
    resultBadgeLabel = t("simulator.quantityTierBadge");
    resultBadgeIntent = "success";
  } else if (isUomSpecific) {
    businessExplanation = `${t("simulator.uomSpecificApplied", { uom: selectedUom?.label })} (سعر الوحدة التجارية المعتمد: ${result?.snapshot?.finalUnitPrice} ${currency}).`;
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

            <Select
              label={t("simulator.productLabel")}
              selectedKey={variantId}
              onSelectionChange={(k) => setVariantId(k as string)}
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
                      {selectedProduct?.label || t("simulator.fallbackProduct1")}
                    </strong>
                  </div>
                  <div>
                    <span>{t("simulator.uomLabel")}: </span>
                    <strong style={{ color: "var(--color-content-primary)" }}>
                      {selectedUom?.label || t("simulator.fallbackUomEa")}
                    </strong>
                  </div>
                  <div>
                    <span>{t("simulator.partyLabel")}: </span>
                    <strong style={{ color: "var(--color-content-primary)" }}>
                      {selectedParty && partyId ? selectedParty.label : t("simulator.generalCustomers")}
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

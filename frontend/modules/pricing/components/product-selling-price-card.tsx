import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { usePricingScope, useResolvePriceQuery, usePriceLists, usePriceVersions, useAddPriceEntryMutation, useCreatePriceVersionMutation } from "../hooks/pricing-queries.ts";
import { MoneyDisplay } from "../../../components/fields/money-display.tsx";
import { StatusBadge } from "../../../components/fields/status-badge.tsx";
import { Surface } from "../../../components/pages/surface.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { Alert } from "../../../components/primitives/alert.tsx";
import { normalizeDecimalInput } from "../../../components/formatting/decimal-parser.ts";

export interface ProductSellingPriceCardProps {
  readonly variantId: string;
  readonly uomId?: string | undefined;
  readonly defaultCurrencyCode?: string | undefined;
  readonly legalEntityId?: string | undefined;
  readonly pricingDate?: string | undefined;
}

export function ProductSellingPriceCard({
  variantId,
  uomId,
  defaultCurrencyCode = "EGP",
  legalEntityId: explicitEntityId,
  pricingDate: explicitPricingDate
}: ProductSellingPriceCardProps): React.JSX.Element {
  const { t } = useTranslation("pricing");
  const { legalEntityId } = usePricingScope({ legalEntityId: explicitEntityId });

  const effectiveUomId = uomId && uomId.length > 20 ? uomId : "2b12a81b-ea82-a151-af7f-4c60a017f44d";
  const effectivePricingDate = explicitPricingDate || "2026-01-05";

  const resolveQuery = useResolvePriceQuery({
    legalEntityId,
    direction: "Selling",
    pricingDate: effectivePricingDate,
    transactionCurrencyCode: defaultCurrencyCode,
    productVariantId: variantId,
    unitOfMeasureId: effectiveUomId,
    quantity: 1
  });

  const { data: priceLists } = usePriceLists(legalEntityId);
  const defaultSellingList = priceLists?.find((l) => l.direction === "Selling" && (l.code === "RETAIL" || (l as { isDefault?: boolean }).isDefault));
  const { data: versions } = usePriceVersions(defaultSellingList?.id ?? "", legalEntityId);

  const [isModalOpen, setIsModalOpen] = useState(false);
  const [newPrice, setNewPrice] = useState("");
  const [errorMsg, setErrorMsg] = useState<string>();

  const addEntryMutation = useAddPriceEntryMutation();
  const createVersionMutation = useCreatePriceVersionMutation();

  const resolution = resolveQuery.data;
  const isResolved = resolution?.status === "Resolved" && resolution.snapshot;
  const unitPrice = resolution?.snapshot?.finalUnitPrice;
  const isZeroPrice = isResolved && (unitPrice === "0" || unitPrice === "0.00" || unitPrice === "0.0000");

  const handleOpenModal = () => {
    setNewPrice(isResolved ? unitPrice || "" : "");
    setErrorMsg(undefined);
    setIsModalOpen(true);
  };

  const handleSavePrice = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMsg(undefined);

    const normalized = normalizeDecimalInput(newPrice);
    if (!normalized || isNaN(Number(normalized)) || Number(normalized) < 0) {
      setErrorMsg("يرجى إدخال سعر صحيح وموجب.");
      return;
    }

    if (!defaultSellingList) {
      setErrorMsg("لم يتم العثور على قائمة البيع الافتراضية للمنشأة.");
      return;
    }

    try {
      // Find draft version, or create one if none exists
      let targetVersion = versions?.find((v) => v.status === "Draft");
      if (!targetVersion) {
        targetVersion = await createVersionMutation.mutateAsync({
          listId: defaultSellingList.id,
          legalEntityId,
          request: {
            effectiveFrom: "2027-01-01",
            expectedListVersion: defaultSellingList.version
          }
        });
      }

      await addEntryMutation.mutateAsync({
        listId: defaultSellingList.id,
        versionId: targetVersion.id,
        legalEntityId,
        request: {
          variantId,
          uomId: effectiveUomId,
          minQuantity: 1,
          unitPrice: normalized,
          priority: 0
        }
      });

      await resolveQuery.refetch();
      setIsModalOpen(false);
    } catch (err: unknown) {
      const errObj = err as { message?: string };
      setErrorMsg(errObj.message || "تعذر حفظ السعر في قائمة البيع الافتراضية.");
    }
  };

  return (
    <Surface
      title={t("productWidget.sellingPriceTitle")}
      headerActions={
        <Button variant="secondary" size="compact" onPress={handleOpenModal}>
          {isResolved ? t("productWidget.updatePriceAction") : t("productWidget.setPriceAction")}
        </Button>
      }
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "var(--space-3)" }}>
        <p style={{ margin: 0, fontSize: "var(--font-size-xs)", color: "var(--color-content-secondary)" }}>
          {t("productWidget.sellingPriceSubtitle")}
        </p>

        <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", flexWrap: "wrap", gap: "var(--space-2)" }}>
          <div style={{ display: "flex", alignItems: "baseline", gap: "var(--space-3)" }}>
            {isResolved ? (
              <>
                <MoneyDisplay
                  value={{
                    amount: unitPrice || "0",
                    currency: resolution.snapshot?.transactionCurrencyCode || defaultCurrencyCode
                  }}
                  displayCurrencyCode
                  size="large"
                />
                <StatusBadge
                  intent={isZeroPrice ? "neutral" : "success"}
                  label={isZeroPrice ? t("productWidget.explicitZeroBadge") : t("productWidget.retailBadge")}
                />
              </>
            ) : (
              <div style={{ display: "flex", alignItems: "center", gap: "var(--space-2)" }}>
                <span style={{ fontSize: "var(--font-size-lg)", fontWeight: 600, color: "var(--color-content-muted)" }}>
                  {t("productWidget.priceMissing")}
                </span>
                <StatusBadge intent="warning" label={t("productWidget.priceMissingBadge")} />
              </div>
            )}
          </div>
        </div>

        {!isResolved && (
          <p style={{ margin: 0, fontSize: "var(--font-size-xs)", color: "var(--color-status-warning)" }}>
            {t("productWidget.priceMissingHelper")}
          </p>
        )}
      </div>

      {/* Quick Set Price Modal */}
      <Dialog
        isOpen={isModalOpen}
        onOpenChange={setIsModalOpen}
        title={t("productWidget.quickModalTitle")}
        description={t("productWidget.quickModalDescription")}
      >
        <form onSubmit={(e) => void handleSavePrice(e)}>
          <div style={{ display: "flex", flexDirection: "column", gap: "var(--space-3)" }}>
            {errorMsg ? <Alert variant="danger">{errorMsg}</Alert> : null}

            <TextField
              label={t("productWidget.priceInputLabel")}
              value={newPrice}
              onChange={setNewPrice}
              inputDir="ltr"
              placeholder={t("productWidget.priceInputPlaceholder")}
              isRequired
            />

            <div style={{ fontSize: "var(--font-size-xs)", color: "var(--color-content-muted)" }}>
              <span>{t("productWidget.currencyLabel")}: </span>
              <strong dir="ltr">{defaultCurrencyCode}</strong>
            </div>

            <div style={{ display: "flex", justifyContent: "flex-end", gap: "var(--space-2)", marginTop: "var(--space-3)" }}>
              <Button variant="secondary" type="button" onPress={() => setIsModalOpen(false)}>
                {t("productWidget.cancel")}
              </Button>
              <Button
                variant="primary"
                type="submit"
                isDisabled={addEntryMutation.isPending || createVersionMutation.isPending || !newPrice}
              >
                {addEntryMutation.isPending ? t("productWidget.saving") : t("productWidget.save")}
              </Button>
            </div>
          </div>
        </form>
      </Dialog>
    </Surface>
  );
}

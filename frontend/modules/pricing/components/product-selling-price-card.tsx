import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { usePricingScope, useResolvePriceQuery, usePriceLists, usePriceVersions, useAddPriceEntryMutation, useCreatePriceVersionMutation, usePricingManagePermission } from "../hooks/pricing-queries.ts";
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
  defaultCurrencyCode,
  legalEntityId: explicitEntityId,
  pricingDate: explicitPricingDate
}: ProductSellingPriceCardProps): React.JSX.Element {
  const { t } = useTranslation("pricing");
  const { legalEntityId, baseCurrencyCode } = usePricingScope({ legalEntityId: explicitEntityId });
  const canManagePricing = usePricingManagePermission();

  const effectiveUomId = uomId ?? "";
  const effectiveCurrencyCode = defaultCurrencyCode ?? baseCurrencyCode;
  const today = new Date();
  const effectivePricingDate = explicitPricingDate || `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, "0")}-${String(today.getDate()).padStart(2, "0")}`;

  const resolveQuery = useResolvePriceQuery({
    legalEntityId,
    direction: "Selling",
    pricingDate: effectivePricingDate,
    transactionCurrencyCode: effectiveCurrencyCode,
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
  const [successMsg, setSuccessMsg] = useState<string>();

  const addEntryMutation = useAddPriceEntryMutation();
  const createVersionMutation = useCreatePriceVersionMutation();

  const resolution = resolveQuery.data;
  const isResolved = resolution?.status === "Resolved" && resolution.snapshot;
  const isPriceLoading = resolveQuery.isLoading || !effectiveUomId;
  const hasPriceLoadError = resolveQuery.isError;
  const unitPrice = resolution?.snapshot?.finalUnitPrice;
  const isZeroPrice = isResolved && (unitPrice === "0" || unitPrice === "0.00" || unitPrice === "0.0000");

  const handleOpenModal = () => {
    setNewPrice(isResolved ? unitPrice || "" : "");
    setErrorMsg(undefined);
    setSuccessMsg(undefined);
    setIsModalOpen(true);
  };

  const handleSavePrice = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMsg(undefined);

    const normalized = normalizeDecimalInput(newPrice);
    if (!normalized || isNaN(Number(normalized)) || Number(normalized) < 0) {
      setErrorMsg(t("productWidget.invalidPrice"));
      return;
    }

    if (!defaultSellingList) {
      setErrorMsg(t("productWidget.defaultListMissing"));
      return;
    }
    if (!effectiveUomId) {
      setErrorMsg(t("productWidget.unitUnavailable"));
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
            effectiveFrom: effectivePricingDate,
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
      setSuccessMsg(t("productWidget.savedToDraft", { date: targetVersion.effectiveFrom }));
      setIsModalOpen(false);
    } catch (err: unknown) {
      setErrorMsg(t("productWidget.saveFailed"));
    }
  };

  return (
    <Surface
      title={t("productWidget.sellingPriceTitle")}
      headerActions={
        canManagePricing ? <Button variant="secondary" size="compact" onPress={handleOpenModal} isDisabled={!effectiveUomId || !defaultSellingList}>
          {isResolved ? t("productWidget.updatePriceAction") : t("productWidget.setPriceAction")}
        </Button> : null
      }
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "var(--space-3)" }}>
        {successMsg ? <Alert variant="info">{successMsg}</Alert> : null}
        <p style={{ margin: 0, fontSize: "var(--font-size-xs)", color: "var(--color-content-secondary)" }}>
          {t("productWidget.sellingPriceSubtitle")}
        </p>

        <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", flexWrap: "wrap", gap: "var(--space-2)" }}>
          <div style={{ display: "flex", alignItems: "baseline", gap: "var(--space-3)" }}>
            {isPriceLoading ? (
              <span role="status" style={{ color: "var(--color-content-secondary)" }}>{t("productWidget.loadingPrice")}</span>
            ) : hasPriceLoadError ? (
              <span role="alert" style={{ color: "var(--color-status-danger)" }}>{t("productWidget.priceUnavailable")}</span>
            ) : isResolved ? (
              <>
                <MoneyDisplay
                  value={{
                    amount: unitPrice || "0",
                    currency: resolution.snapshot?.transactionCurrencyCode || effectiveCurrencyCode
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

        {!isPriceLoading && !hasPriceLoadError && !isResolved && (
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
              <strong dir="ltr">{effectiveCurrencyCode}</strong>
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

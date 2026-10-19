import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { Select, SelectItem } from "../../../components/primitives/select.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { Checkbox } from "../../../components/primitives/checkbox.tsx";
import { Alert } from "../../../components/primitives/alert.tsx";
import { useAddProductUom, useCatalogUoms } from "../hooks/catalog-queries.ts";
import type { AddProductUomInput, ProductDetails, Uom } from "../types.ts";

export interface ProductUomDialogProps {
  readonly isOpen: boolean;
  readonly onOpenChange: (open: boolean) => void;
  readonly product: ProductDetails;
  readonly onAdded?: () => void;
}

export function ProductUomDialog({
  isOpen,
  onOpenChange,
  product,
  onAdded
}: ProductUomDialogProps): React.JSX.Element {
  const { t } = useTranslation("catalog");
  const [uomId, setUomId] = useState("");
  const [factorToBase, setFactorToBase] = useState("1");
  const [effectiveFrom, setEffectiveFrom] = useState(() => new Date().toISOString().slice(0, 10));
  const [effectiveTo, setEffectiveTo] = useState("");
  const [usageSales, setUsageSales] = useState(true);
  const [usagePurchase, setUsagePurchase] = useState(true);
  const [usageInventory, setUsageInventory] = useState(true);
  const [usagePos, setUsagePos] = useState(false);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  const uoms = useCatalogUoms().data ?? [];
  const addUom = useAddProductUom();

  const selectedUom = uoms.find((u) => u.id === uomId);
  const baseUom = uoms.find((u) => u.code === product.baseUomCode);

  const factorNum = parseFloat(factorToBase) || 0;

  const resetForm = () => {
    setUomId("");
    setFactorToBase("1");
    setEffectiveFrom(new Date().toISOString().slice(0, 10));
    setEffectiveTo("");
    setUsageSales(true);
    setUsagePurchase(true);
    setUsageInventory(true);
    setUsagePos(false);
    setErrorMsg(null);
  };

  const handleClose = (open: boolean) => {
    if (!open) resetForm();
    onOpenChange(open);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!uomId) {
      setErrorMsg(t("productUomDialog.errorUomRequired"));
      return;
    }
    if (factorNum <= 0) {
      setErrorMsg(t("productUomDialog.errorFactorPositive"));
      return;
    }

    const usageFlags: string[] = [];
    if (usageSales) usageFlags.push("Sales");
    if (usagePurchase) usageFlags.push("Purchase");
    if (usageInventory) usageFlags.push("Inventory");
    if (usagePos) usageFlags.push("POS");

    const input: AddProductUomInput = {
      uomId,
      factorToBase: factorNum,
      effectiveFrom: new Date(effectiveFrom).toISOString(),
      effectiveTo: effectiveTo ? new Date(effectiveTo).toISOString() : null,
      usageFlags: usageFlags.length > 0 ? usageFlags : undefined
    };

    try {
      await addUom.mutateAsync({
        productId: product.id,
        variantId: product.defaultVariantId,
        input
      });
      handleClose(false);
      onAdded?.();
    } catch (err: unknown) {
      const problem = (err as { problemDetails?: { extensions?: { code?: string } } })?.problemDetails;
      const code = problem?.extensions?.code;
      if (code === "catalog.conversion_overlap") {
        setErrorMsg(t("errors.conversionOverlap"));
      } else if (code === "catalog.uom_dimension_mismatch") {
        setErrorMsg(t("errors.dimensionMismatch"));
      } else {
        setErrorMsg(t("productUomDialog.genericError"));
      }
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={handleClose}
      title={t("productUomDialog.title")}
      size="medium"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {errorMsg ? (
          <Alert variant="danger" title={t("common.error")}>{errorMsg}</Alert>
        ) : null}

        <p style={{ color: "var(--color-content-secondary)", fontSize: "0.875rem", margin: 0 }}>
          {t("productUomDialog.baseUnitNotice", { unit: product.baseUomCode || "—" })}
        </p>

        <Select
          label={t("productUomDialog.alternateUnitLabel")}
          selectedKey={uomId}
          onSelectionChange={(key) => setUomId(String(key))}
          placeholder={t("productUomDialog.selectUnitPlaceholder")}
          isRequired
        >
          {uoms
            .filter((u) => u.code !== product.baseUomCode)
            .map((uom) => (
              <SelectItem key={uom.id} id={uom.id}>
                {uom.code} — {uom.nameAr}
              </SelectItem>
            ))}
        </Select>

        <TextField
          label={t("productUomDialog.factorLabel")}
          value={factorToBase}
          onChange={setFactorToBase}
          placeholder={t("productUomDialog.factorPlaceholder")}
          description={t("productUomDialog.factorHint")}
          isRequired
        />

        {/* Human-readable conversion explanation */}
        {selectedUom && factorNum > 0 ? (
          <div style={{ padding: "0.75rem", background: "var(--color-surface-subtle)", borderRadius: "var(--radius-sm)", border: "1px dashed var(--color-border-default)", fontSize: "0.875rem", fontWeight: 600 }}>
            <bdi dir="ltr" style={{ display: "inline-block", unicodeBidi: "isolate" }}>
              {t("productRecord.conversionRate", {
                target: selectedUom.code,
                factor: factorNum,
                base: product.baseUomCode || baseUom?.code || ""
              })}
            </bdi>
            <span style={{ marginInlineStart: "0.5rem", color: "var(--color-content-secondary)", fontWeight: 400 }}>
              ({selectedUom.nameAr || selectedUom.code} ← {baseUom?.nameAr || product.baseUomCode})
            </span>
          </div>
        ) : null}

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <TextField
            label={t("productUomDialog.effectiveFromLabel")}
            type="date"
            value={effectiveFrom}
            onChange={setEffectiveFrom}
            isRequired
          />
          <TextField
            label={t("productUomDialog.effectiveToLabel")}
            type="date"
            value={effectiveTo}
            onChange={setEffectiveTo}
            description={t("productUomDialog.effectiveToHint")}
          />
        </div>

        <div>
          <label style={{ fontSize: "0.8125rem", fontWeight: 600, display: "block", marginBottom: "0.5rem" }}>
            {t("productUomDialog.usageFlagsLabel")}
          </label>
          <div style={{ display: "flex", gap: "1.25rem", flexWrap: "wrap" }}>
            <Checkbox isSelected={usageSales} onChange={setUsageSales}>
              {t("productUomDialog.flagSales")}
            </Checkbox>
            <Checkbox isSelected={usagePurchase} onChange={setUsagePurchase}>
              {t("productUomDialog.flagPurchase")}
            </Checkbox>
            <Checkbox isSelected={usageInventory} onChange={setUsageInventory}>
              {t("productUomDialog.flagInventory")}
            </Checkbox>
            <Checkbox isSelected={usagePos} onChange={setUsagePos}>
              {t("productUomDialog.flagPos")}
            </Checkbox>
          </div>
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "0.5rem" }}>
          <Button variant="secondary" type="button" onPress={() => handleClose(false)}>
            {t("common.cancel")}
          </Button>
          <Button variant="primary" type="submit" isLoading={addUom.isPending}>
            {t("productUomDialog.submit")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

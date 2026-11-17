import React from "react";
import { useTranslation } from "react-i18next";
import { ProductPicker } from "../../catalog/public.ts";

export interface PurchasingProductPickerProps {
  readonly value?: string | null | undefined;
  readonly onChange: (variantId: string, product: { name: string; sku: string; uom: string; defaultBuyPrice: number; isStocked: boolean }) => void;
  readonly isRequired?: boolean | undefined;
  readonly label?: string | undefined;
  readonly disabled?: boolean | undefined;
}

/** Purchase lines select an active Catalog product variant, never a local fixture. */
export function PurchasingProductPicker({
  value,
  onChange,
  isRequired = false,
  label,
  disabled = false
}: PurchasingProductPickerProps): React.JSX.Element {
  const { t } = useTranslation("purchasing");

  return (
    <ProductPicker
      label={label}
      value={value ?? undefined}
      onChange={(selection) => {
        onChange(selection.variantId, {
          name: selection.displayName,
          sku: selection.sku ?? "",
          uom: selection.baseUomCode ?? "",
          // Catalog does not own purchasing prices; keep the line's existing price.
          defaultBuyPrice: 0,
          isStocked: selection.isStocked ?? true
        });
      }}
      isRequired={isRequired}
      isDisabled={disabled}
      enableQuickCreate={false}
      placeholder={t("products.searchPlaceholder")}
    />
  );
}

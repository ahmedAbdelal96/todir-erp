import React from "react";
import { useTranslation } from "react-i18next";
import { ProductPicker } from "../../catalog/public.ts";

export interface SalesProductPickerProps {
  readonly value?: string | null | undefined;
  readonly onChange: (variantId: string, product: { name: string; sku: string; uom: string; listPrice?: number | undefined }) => void;
  readonly isRequired?: boolean | undefined;
  readonly label?: string | undefined;
  readonly disabled?: boolean | undefined;
}

/** Sales lines select an active Catalog product variant, never a local fixture. */
export function SalesProductPicker({
  value,
  onChange,
  isRequired = false,
  label,
  disabled = false
}: SalesProductPickerProps): React.JSX.Element {
  const { t } = useTranslation("sales");

  return (
    <ProductPicker
      label={label}
      value={value ?? undefined}
      onChange={(selection) => {
        onChange(selection.variantId, {
          name: selection.displayName,
          sku: selection.sku ?? "",
          uom: selection.baseUomCode ?? "",
          // Pricing stays an explicit commercial input until the pricing capability provides a value.
          listPrice: undefined
        });
      }}
      isRequired={isRequired}
      isDisabled={disabled}
      enableQuickCreate={false}
      placeholder={t("quotations.searchProductPlaceholder")}
    />
  );
}

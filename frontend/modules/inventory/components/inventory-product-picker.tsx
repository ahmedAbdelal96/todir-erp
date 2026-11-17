import React from "react";
import { ProductPicker } from "../../catalog/public.ts";

export interface InventoryProductPickerProps {
  readonly value?: string | null;
  readonly onChange: (variantId: string) => void;
  readonly isRequired?: boolean;
  readonly label?: string;
  readonly disabled?: boolean;
}

/** Inventory operations select mastered Catalog variants, including barcode resolution. */
export function InventoryProductPicker({
  value,
  onChange,
  isRequired = false,
  label,
  disabled = false
}: InventoryProductPickerProps): React.JSX.Element {
  return (
    <ProductPicker
      label={label}
      value={value ?? undefined}
      onChange={(selection) => onChange(selection.variantId)}
      isRequired={isRequired}
      isDisabled={disabled}
      enableQuickCreate={false}
    />
  );
}

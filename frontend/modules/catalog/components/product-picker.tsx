import React from "react";
import { useTranslation } from "react-i18next";
import { ComboBox, type ComboBoxOption } from "../../../components/primitives/combobox.tsx";
import { useProductList } from "../hooks/catalog-queries.ts";

export function ProductPicker({ value, onChange, label }: { readonly value?: string; readonly onChange?: (id: string) => void; readonly label?: string }): React.JSX.Element {
  const { t } = useTranslation("catalog");
  const { data, isLoading } = useProductList({ pageSize: 50, status: "Active" });
  const options: ComboBoxOption[] = (data?.items ?? []).map((item) => ({
    id: item.defaultVariantId,
    label: item.displayName,
    description: item.sku ?? item.baseUomCode ?? undefined
  }));
  return (
    <ComboBox
      label={label ?? t("productPicker.label")}
      selectedKey={value ?? null}
      onSelectionChange={(key) => onChange?.(String(key ?? ""))}
      options={options}
      placeholder={isLoading ? t("productPicker.loading") : t("productPicker.placeholder")}
    />
  );
}

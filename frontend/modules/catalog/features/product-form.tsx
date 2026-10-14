import React from "react";
import { useTranslation } from "react-i18next";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { FormActions } from "../../../components/forms/form-actions.tsx";
import { FormSection } from "../../../components/forms/form-section.tsx";
import { createProductSchema, type ProductFormData } from "../validation/product-schema.ts";
import type { Brand, Category, Uom } from "../types.ts";

export interface ProductFormProps {
  readonly initialData?: Partial<ProductFormData>;
  readonly categories: readonly Category[];
  readonly brands: readonly Brand[];
  readonly uoms: readonly Uom[];
  readonly onSubmit: (data: ProductFormData) => Promise<void>;
  readonly onCancel?: () => void;
  readonly isSubmitting?: boolean;
  readonly isLocked?: boolean;
}

const inputStyle: React.CSSProperties = {
  width: "100%", boxSizing: "border-box", padding: "0.6rem 0.7rem",
  border: "1px solid var(--color-border-default)", borderRadius: "var(--radius-sm)",
  background: "var(--color-surface-card)", color: "var(--color-content-primary)"
};

export function ProductForm({ initialData, categories, brands, uoms, onSubmit, onCancel, isSubmitting = false, isLocked = false }: ProductFormProps): React.JSX.Element {
  const { t } = useTranslation("catalog");
  const schema = createProductSchema((key) => t(key));
  const { register, handleSubmit, watch, formState: { isDirty, errors } } = useForm<ProductFormData>({
    resolver: zodResolver(schema),
    defaultValues: {
      displayName: initialData?.displayName ?? "", nature: initialData?.nature ?? "Goods", sku: initialData?.sku ?? "",
      nameAr: initialData?.nameAr ?? "", nameEn: initialData?.nameEn ?? "", categoryId: initialData?.categoryId ?? "",
      brandId: initialData?.brandId ?? "", baseUomId: initialData?.baseUomId ?? ""
    }
  });
  const nature = watch("nature");
  const field = (name: keyof ProductFormData, label: string, required = false) => (
    <label style={{ display: "flex", flexDirection: "column", gap: "0.3rem" }}>
      <span>{label}{required ? " *" : ""}</span>
      <input disabled={isLocked} style={inputStyle} {...register(name)} />
      {errors[name] ? <small role="alert" style={{ color: "var(--color-status-danger)" }}>{String(errors[name]?.message ?? "")}</small> : null}
    </label>
  );

  return (
    <form onSubmit={handleSubmit(onSubmit)} style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
      <FormSection title={t("productForm.identity")} description={t("productForm.identityDescription")}>
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit,minmax(220px,1fr))", gap: "1rem" }}>
          {field("displayName", t("productForm.name"), true)}
          <label style={{ display: "flex", flexDirection: "column", gap: "0.3rem" }}>
            <span>{t("productForm.nature")} *</span>
            <select disabled={isLocked} style={inputStyle} {...register("nature")}>
              <option value="Goods">{t("productForm.goods")}</option>
              <option value="Service">{t("productForm.service")}</option>
            </select>
          </label>
          {field("sku", t("productForm.sku"))}
          {field("nameAr", t("productForm.nameAr"))}
          {field("nameEn", t("productForm.nameEn"))}
        </div>
      </FormSection>
      <FormSection title={t("productForm.classification")} description={nature === "Service" ? t("productForm.serviceUnitDescription") : t("productForm.baseUnitDescription")}>
        <label style={{ display: "flex", flexDirection: "column", gap: "0.3rem" }}>
          <span>{t("productForm.category")}</span>
          <select disabled={isLocked} style={inputStyle} {...register("categoryId")}>
            <option value="">{t("productForm.noCategory")}</option>
            {categories.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
          </select>
        </label>
        <label style={{ display: "flex", flexDirection: "column", gap: "0.3rem" }}>
          <span>{t("productForm.brand")}</span>
          <select disabled={isLocked} style={inputStyle} {...register("brandId")}>
            <option value="">{t("productForm.noBrand")}</option>
            {brands.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
          </select>
        </label>
        <label style={{ display: "flex", flexDirection: "column", gap: "0.3rem" }}>
          <span>{t("productForm.baseUnit")} *</span>
          <select disabled={isLocked} style={inputStyle} {...register("baseUomId")}>
            <option value="">{t("productForm.chooseUnit")}</option>
            {uoms.map((item) => <option key={item.id} value={item.id}>{item.code} — {item.nameAr}</option>)}
          </select>
          {errors.baseUomId ? <small role="alert" style={{ color: "var(--color-status-danger)" }}>{String(errors.baseUomId.message ?? "")}</small> : null}
        </label>
      </FormSection>
      <FormActions submitLabel={t("productForm.save")} cancelLabel={t("productForm.cancel")} isSubmitting={isSubmitting} isDirty={isDirty} isLocked={isLocked} onCancel={onCancel} />
    </form>
  );
}

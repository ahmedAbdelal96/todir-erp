import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { Select, SelectItem } from "../../../components/primitives/select.tsx";
import { Alert } from "../../../components/primitives/alert.tsx";
import { useCatalogBrands, useCatalogCategories, useUpdateProduct } from "../hooks/catalog-queries.ts";
import type { ProductDetails, UpdateProductInput } from "../types.ts";

export interface EditProductDialogProps {
  readonly isOpen: boolean;
  readonly onOpenChange: (open: boolean) => void;
  readonly product: ProductDetails;
  readonly onUpdated?: () => void;
}

export function EditProductDialog({
  isOpen,
  onOpenChange,
  product,
  onUpdated
}: EditProductDialogProps): React.JSX.Element {
  const { t } = useTranslation("catalog");
  const [displayName, setDisplayName] = useState(product.displayName);
  const [sku, setSku] = useState(product.sku ?? "");
  const [nameAr, setNameAr] = useState(product.nameAr ?? "");
  const [nameEn, setNameEn] = useState(product.nameEn ?? "");
  const [categoryId, setCategoryId] = useState(product.categoryId ?? "");
  const [brandId, setBrandId] = useState(product.brandId ?? "");
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  const categories = useCatalogCategories().data ?? [];
  const brands = useCatalogBrands().data ?? [];
  const updateProduct = useUpdateProduct();

  const handleClose = (open: boolean) => {
    if (!open) {
      setDisplayName(product.displayName);
      setSku(product.sku ?? "");
      setNameAr(product.nameAr ?? "");
      setNameEn(product.nameEn ?? "");
      setCategoryId(product.categoryId ?? "");
      setBrandId(product.brandId ?? "");
      setErrorMsg(null);
    }
    onOpenChange(open);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!displayName.trim()) {
      setErrorMsg(t("quickCreate.errorNameRequired"));
      return;
    }

    const input: UpdateProductInput = {
      displayName: displayName.trim(),
      sku: sku.trim() || undefined,
      nameAr: nameAr.trim() || undefined,
      nameEn: nameEn.trim() || undefined,
      categoryId: categoryId || undefined,
      brandId: brandId || undefined,
      version: product.version
    };

    try {
      await updateProduct.mutateAsync({ id: product.id, input });
      handleClose(false);
      onUpdated?.();
    } catch (err: unknown) {
      const problem = (err as { problemDetails?: { extensions?: { code?: string } } })?.problemDetails;
      const code = problem?.extensions?.code;
      if (code === "catalog.concurrency_conflict") {
        setErrorMsg(t("errors.concurrencyConflict"));
      } else if (code === "catalog.sku_duplicate") {
        setErrorMsg(t("errors.skuExists"));
      } else {
        setErrorMsg(t("editProduct.genericError"));
      }
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={handleClose}
      title={t("editProduct.title")}
      size="medium"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {errorMsg ? (
          <Alert variant="danger" title={t("common.error")}>{errorMsg}</Alert>
        ) : null}

        <TextField
          label={t("productForm.name")}
          value={displayName}
          onChange={setDisplayName}
          isRequired
          autoFocus
        />

        {product.nature === "Goods" ? (
          <TextField
            label={t("productForm.sku")}
            value={sku}
            onChange={setSku}
          />
        ) : null}

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <TextField
            label={t("productForm.nameAr")}
            value={nameAr}
            onChange={setNameAr}
          />
          <TextField
            label={t("productForm.nameEn")}
            value={nameEn}
            onChange={setNameEn}
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <Select
            label={t("productForm.category")}
            selectedKey={categoryId}
            onSelectionChange={(key) => setCategoryId(String(key))}
            placeholder={t("productForm.noCategory")}
          >
            <SelectItem id="">{t("productForm.noCategory")}</SelectItem>
            {categories.map((c) => (
              <SelectItem key={c.id} id={c.id}>
                {c.name}
              </SelectItem>
            ))}
          </Select>

          <Select
            label={t("productForm.brand")}
            selectedKey={brandId}
            onSelectionChange={(key) => setBrandId(String(key))}
            placeholder={t("productForm.noBrand")}
          >
            <SelectItem id="">{t("productForm.noBrand")}</SelectItem>
            {brands.map((b) => (
              <SelectItem key={b.id} id={b.id}>
                {b.name}
              </SelectItem>
            ))}
          </Select>
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "0.5rem" }}>
          <Button variant="secondary" type="button" onPress={() => handleClose(false)}>
            {t("common.cancel")}
          </Button>
          <Button variant="primary" type="submit" isLoading={updateProduct.isPending}>
            {t("common.save")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

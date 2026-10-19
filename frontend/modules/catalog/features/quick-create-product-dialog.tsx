import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import type { CreateProductInput, ProductDetails, ProductNature } from "../types.ts";
import {
  useCatalogBrands,
  useCatalogCategories,
  useCatalogUoms,
  useCreateProduct
} from "../hooks/catalog-queries.ts";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { Select, SelectItem } from "../../../components/primitives/select.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { useToast } from "../../../components/primitives/toast.tsx";
import { Alert } from "../../../components/primitives/alert.tsx";

export interface QuickCreateProductDialogProps {
  readonly isOpen: boolean;
  readonly onOpenChange: (open: boolean) => void;
  readonly defaultNature?: ProductNature | undefined;
  readonly onCreated?: ((product: ProductDetails) => void) | undefined;
}

export function QuickCreateProductDialog({
  isOpen,
  onOpenChange,
  defaultNature = "Goods",
  onCreated
}: QuickCreateProductDialogProps): React.JSX.Element {
  const { t } = useTranslation("catalog");
  const { showToast } = useToast();

  const [displayName, setDisplayName] = useState("");
  const [nature, setNature] = useState<ProductNature>(defaultNature);
  const [baseUomId, setBaseUomId] = useState("");
  const [sku, setSku] = useState("");
  const [nameAr, setNameAr] = useState("");
  const [nameEn, setNameEn] = useState("");
  const [categoryId, setCategoryId] = useState("");
  const [brandId, setBrandId] = useState("");
  const [showAdvanced, setShowAdvanced] = useState(false);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  const categories = useCatalogCategories().data ?? [];
  const brands = useCatalogBrands().data ?? [];
  const uoms = useCatalogUoms().data ?? [];
  const createProduct = useCreateProduct();

  // Reset form
  const resetForm = () => {
    setDisplayName("");
    setNature(defaultNature);
    setBaseUomId("");
    setSku("");
    setNameAr("");
    setNameEn("");
    setCategoryId("");
    setBrandId("");
    setShowAdvanced(false);
    setErrorMsg(null);
  };

  const handleClose = (open: boolean) => {
    if (!open) resetForm();
    onOpenChange(open);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!displayName.trim()) {
      setErrorMsg(t("quickCreate.errorNameRequired"));
      return;
    }
    if (!baseUomId) {
      setErrorMsg(t("quickCreate.errorUomRequired"));
      return;
    }

    setErrorMsg(null);

    const input: CreateProductInput = {
      displayName: displayName.trim(),
      nature,
      baseUomId,
      sku: sku.trim() || undefined,
      nameAr: nameAr.trim() || undefined,
      nameEn: nameEn.trim() || undefined,
      categoryId: categoryId || undefined,
      brandId: brandId || undefined
    };

    try {
      const created = await createProduct.mutateAsync(input);
      showToast({
        title: t("quickCreate.successTitle"),
        description: t("quickCreate.successMessage", { name: created.displayName }),
        type: "success"
      });
      handleClose(false);
      onCreated?.(created);
    } catch (err: unknown) {
      const problem = (err as { problemDetails?: { extensions?: { code?: string } } })?.problemDetails;
      const code = problem?.extensions?.code;
      if (code === "catalog.sku_duplicate") {
        setErrorMsg(t("errors.skuExists"));
      } else {
        setErrorMsg(t("quickCreate.genericError"));
      }
    }
  };

  const isService = nature === "Service";

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={handleClose}
      title={isService ? t("quickCreate.titleService") : t("quickCreate.titleGood")}
      size="medium"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {errorMsg ? (
          <Alert variant="danger" title={t("quickCreate.validationError")}>{errorMsg}</Alert>
        ) : null}

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <Select
            label={t("quickCreate.natureLabel")}
            selectedKey={nature}
            onSelectionChange={(key) => {
              const val = String(key) as ProductNature;
              setNature(val);
            }}
          >
            <SelectItem id="Goods">{t("common.goods")}</SelectItem>
            <SelectItem id="Service">{t("common.service")}</SelectItem>
          </Select>

          <Select
            label={t("quickCreate.baseUomLabel")}
            selectedKey={baseUomId}
            onSelectionChange={(key) => setBaseUomId(String(key))}
            placeholder={t("quickCreate.selectUomPlaceholder")}
            isRequired
          >
            {uoms.map((uom) => (
              <SelectItem key={uom.id} id={uom.id}>
                {uom.code} — {uom.nameAr}
              </SelectItem>
            ))}
          </Select>
        </div>

        <TextField
          label={t("quickCreate.nameLabel")}
          value={displayName}
          onChange={setDisplayName}
          placeholder={isService ? t("quickCreate.namePlaceholderService") : t("quickCreate.namePlaceholderGood")}
          isRequired
          autoFocus
        />

        {!isService ? (
          <TextField
            label={t("quickCreate.skuLabel")}
            value={sku}
            onChange={setSku}
            placeholder={t("quickCreate.skuPlaceholder")}
            description={t("quickCreate.skuHint")}
          />
        ) : null}

        <div>
          <Button
            variant="ghost"
            size="compact"
            type="button"
            onPress={() => setShowAdvanced(!showAdvanced)}
          >
            {showAdvanced ? t("quickCreate.hideAdvanced") : t("quickCreate.showAdvanced")}
          </Button>
        </div>

        {showAdvanced ? (
          <div style={{ display: "flex", flexDirection: "column", gap: "0.75rem", borderTop: "1px solid var(--color-border-subtle)", paddingTop: "0.75rem" }}>
            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
              <TextField
                label={t("quickCreate.nameArLabel")}
                value={nameAr}
                onChange={setNameAr}
                placeholder={t("quickCreate.nameArPlaceholder")}
              />
              <TextField
                label={t("quickCreate.nameEnLabel")}
                value={nameEn}
                onChange={setNameEn}
                placeholder={t("quickCreate.nameEnPlaceholder")}
                inputDir="ltr"
              />
            </div>

            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
              <Select
                label={t("quickCreate.categoryLabel")}
                selectedKey={categoryId}
                onSelectionChange={(key) => setCategoryId(String(key))}
                placeholder={t("quickCreate.noCategoryPlaceholder")}
              >
                {categories.map((cat) => (
                  <SelectItem key={cat.id} id={cat.id}>
                    {cat.name}
                  </SelectItem>
                ))}
              </Select>

              <Select
                label={t("quickCreate.brandLabel")}
                selectedKey={brandId}
                onSelectionChange={(key) => setBrandId(String(key))}
                placeholder={t("quickCreate.noBrandPlaceholder")}
              >
                {brands.map((brand) => (
                  <SelectItem key={brand.id} id={brand.id}>
                    {brand.name}
                  </SelectItem>
                ))}
              </Select>
            </div>
          </div>
        ) : null}

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "0.5rem" }}>
          <Button variant="secondary" type="button" onPress={() => handleClose(false)}>
            {t("common.cancel")}
          </Button>
          <Button variant="primary" type="submit" isLoading={createProduct.isPending}>
            {t("quickCreate.submit")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

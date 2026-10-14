import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { RecordPage } from "../../../components/pages/record-page.tsx";
import { DateTimeDisplay, LocalDateDisplay } from "../../../components/fields/index.ts";
import { Surface } from "../../../components/pages/surface.tsx";
import { PropertyGrid, PropertyItem, StatCard } from "../../../components/pages/property-grid.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { StatusBadge } from "../../../components/fields/status-badge.tsx";
import { Spinner } from "../../../components/primitives/spinner.tsx";
import {
  CatalogIcon,
  TagIcon,
  CheckCircleIcon,
  BuildingIcon,
  FolderIcon
} from "../../../components/primitives/icons.tsx";
import {
  useCatalogBrands,
  useCatalogCategories,
  useCatalogUoms,
  useChangeProductStatus,
  useProduct,
  useUpdateProduct
} from "../hooks/catalog-queries.ts";
import { ProductForm } from "../features/product-form.tsx";
import type { ProductFormData } from "../validation/product-schema.ts";

export interface ProductRecordPageProps {
  readonly productId: string;
  readonly onBack: () => void;
}

export function ProductRecordPage({
  productId,
  onBack
}: ProductRecordPageProps): React.JSX.Element {
  const { t } = useTranslation("catalog");
  const { data, error, isLoading } = useProduct(productId);
  const [editing, setEditing] = useState(false);

  const categories = useCatalogCategories().data ?? [];
  const brands = useCatalogBrands().data ?? [];
  const uoms = useCatalogUoms().data ?? [];
  const update = useUpdateProduct();
  const status = useChangeProductStatus();

  if (isLoading) {
    return (
      <div style={{ display: "flex", justifyContent: "center", alignItems: "center", minHeight: "360px" }}>
        <Spinner size="large" aria-label={t("productRecord.loading")} />
      </div>
    );
  }

  if (error || !data) {
    return (
      <div style={{ padding: "3rem", textAlign: "center" }}>
        <p style={{ color: "var(--color-status-danger, #ef4444)", fontWeight: 600, marginBottom: "1rem" }}>
          {t("productRecord.notFound")}
        </p>
        <Button variant="secondary" onPress={onBack}>
          {t("productRecord.backToCatalog")}
        </Button>
      </div>
    );
  }

  const isActive = data.status === "Active";
  const isService = data.nature === "Service";

  const form: ProductFormData = {
    displayName: data.displayName,
    nature: data.nature,
    sku: data.sku ?? "",
    nameAr: data.nameAr ?? "",
    nameEn: data.nameEn ?? "",
    categoryId: data.categoryId ?? "",
    brandId: data.brandId ?? "",
    baseUomId: uoms.find((item) => item.code === data.baseUomCode)?.id ?? ""
  };

  const kpiCards = (
    <>
      <StatCard
        title={t("productRecord.nature")}
        value={isService ? t("productRecord.serviceNonStock") : t("productRecord.goodsInventory")}
        intent="info"
        icon={<CatalogIcon size={20} />}
        subtitle={isService ? t("productRecord.noInventoryTracking") : t("productRecord.inventoryMovements")}
      />
      <StatCard
        title={t("productRecord.productStatus")}
        value={isActive ? t("productRecord.activeForSale") : t("productRecord.archived")}
        intent={isActive ? "success" : "neutral"}
        icon={<CheckCircleIcon size={20} />}
        subtitle={isActive ? t("productRecord.availableForTransactions") : t("productRecord.stoppedForNewTransactions")}
      />
      <StatCard
        title={t("productRecord.baseUnit")}
        value={data.baseUomCode ?? "—"}
        intent="primary"
        icon={<TagIcon size={20} />}
        subtitle={t("productRecord.tradingUnit")}
      />
      <StatCard
        title={t("productRecord.barcodes")}
        value={data.barcodes.length > 0 ? t("productRecord.barcodeCount", { count: data.barcodes.length }) : t("productRecord.none")}
        intent="neutral"
        icon={<TagIcon size={20} />}
        subtitle={t("productRecord.scanCodes")}
      />
    </>
  );

  return (
    <RecordPage
      title={data.displayName}
      recordCode={data.sku ? `#${data.sku}` : undefined}
      status={isActive ? "success" : "neutral"}
      kpiCards={kpiCards}
      breadcrumbs={[
        { label: t("productList.pageTitle"), href: "/catalog", onClick: onBack },
        { label: data.displayName }
      ]}
      headerActions={
        <div style={{ display: "flex", gap: "0.75rem", flexWrap: "wrap" }}>
          <Button
            variant="primary"
            onPress={() => setEditing(true)}
            isDisabled={!isActive}
          >
            {t("productRecord.edit")}
          </Button>
          <Button
            variant={isActive ? "danger" : "secondary"}
            onPress={() =>
              void status.mutateAsync({
                id: data.id,
                archive: isActive,
                version: data.version
              })
            }
          >
            {isActive ? t("productRecord.archive") : t("productRecord.unarchive")}
          </Button>
          <Button variant="secondary" onPress={onBack}>
            {t("productRecord.back")}
          </Button>
        </div>
      }
      auditInfo={{
        createdAt: <LocalDateDisplay value={data.createdAt} dateStyle="long" />,
        updatedAt: <DateTimeDisplay value={data.updatedAt} type="datetime" />
      }}
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
        {/* Basic Identification */}
        <Surface
          title={t("productRecord.identity")}
          description={t("productRecord.identityDescription")}
        >
          <PropertyGrid columns={3}>
            <PropertyItem
              label={t("productRecord.displayName")}
              value={data.displayName}
              icon={<CatalogIcon size={16} />}
            />
            <PropertyItem
              label={t("productForm.nameAr")}
              value={data.nameAr || "—"}
            />
            <PropertyItem
              label={t("productForm.nameEn")}
              value={
                data.nameEn ? (
                  <bdi dir="ltr">{data.nameEn}</bdi>
                ) : (
                  "—"
                )
              }
            />
            <PropertyItem
              label={t("productRecord.sku")}
              value={data.sku ? <bdi dir="ltr">{data.sku}</bdi> : "—"}
              isCopyable={Boolean(data.sku)}
              rawCopyValue={data.sku ?? undefined}
            />
            <PropertyItem
              label={t("productRecord.itemType")}
              value={
                <StatusBadge
                  intent="info"
                  label={isService ? t("productForm.service") : t("productRecord.stockGoods")}
                  size="small"
                />
              }
            />
            <PropertyItem
              label={t("productRecord.itemStatus")}
              value={
                <StatusBadge
                  intent={isActive ? "success" : "neutral"}
                  label={isActive ? t("productRecord.active") : t("productRecord.archived")}
                  size="small"
                />
              }
            />
          </PropertyGrid>
        </Surface>

        {/* Classification & Structure */}
        <Surface
          title={t("productRecord.classification")}
          description={t("productRecord.classificationDescription")}
        >
          <PropertyGrid columns={3}>
            <PropertyItem
              label={t("productForm.category")}
              value={data.categoryName || t("productRecord.uncategorized")}
              icon={<FolderIcon size={16} />}
            />
            <PropertyItem
              label={t("productForm.brand")}
              value={data.brandName || t("productForm.noBrand")}
              icon={<BuildingIcon size={16} />}
            />
            <PropertyItem
              label={t("productRecord.unitOfMeasure")}
              value={data.baseUomCode || "—"}
              icon={<TagIcon size={16} />}
            />
          </PropertyGrid>
        </Surface>

        {/* Barcodes */}
        <Surface
          title={t("productRecord.linkedBarcodes")}
          description={t("productRecord.barcodeDescription")}
        >
          {data.barcodes.length > 0 ? (
            <div style={{ display: "flex", flexWrap: "wrap", gap: "0.75rem", padding: "0.5rem 0" }}>
              {data.barcodes.map((barcode) => (
                <div
                  key={barcode.id}
                  style={{
                    display: "inline-flex",
                    alignItems: "center",
                    gap: "0.5rem",
                    padding: "0.5rem 0.875rem",
                    borderRadius: "var(--radius-md)",
                    backgroundColor: "var(--color-surface-subtle)",
                    border: "1px solid var(--color-border-default)",
                    fontFamily: "var(--font-mono)",
                    fontSize: "0.875rem",
                    fontWeight: 600,
                    color: "var(--color-content-primary)"
                  }}
                >
                  <TagIcon size={14} style={{ color: "var(--color-content-muted)" }} />
                  <bdi dir="ltr">{barcode.value}</bdi>
                </div>
              ))}
            </div>
          ) : (
            <p style={{ margin: 0, fontSize: "0.875rem", color: "var(--color-content-muted)" }}>
              {t("productRecord.noBarcodes")}
            </p>
          )}
        </Surface>
      </div>

      {editing ? (
        <ProductForm
          initialData={form}
          categories={categories}
          brands={brands}
          uoms={uoms}
          onSubmit={async (value) => {
            await update.mutateAsync({
              id: data.id,
              input: { ...value, version: data.version }
            });
            setEditing(false);
          }}
          onCancel={() => setEditing(false)}
          isSubmitting={update.isPending}
        />
      ) : null}
    </RecordPage>
  );
}

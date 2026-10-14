import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { ListPage } from "../../../components/pages/list-page.tsx";
import { DataGrid, type DataGridColumn } from "../../../components/grid/index.ts";
import { StatusBadge } from "../../../components/fields/status-badge.tsx";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { SearchField } from "../../../components/primitives/search-field.tsx";
import { FilterBar } from "../../../components/primitives/filter-bar.tsx";
import { Select, SelectItem } from "../../../components/primitives/select.tsx";
import { ErrorState } from "../../../components/primitives/error-state.tsx";
import { EmptyState } from "../../../components/primitives/empty-state.tsx";
import { CatalogIcon } from "../../../components/primitives/icons.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import {
  useCatalogBrands,
  useCatalogCategories,
  useCatalogUoms,
  useCreateProduct,
  useProductList
} from "../hooks/catalog-queries.ts";
import { ProductForm } from "../features/product-form.tsx";
import type { ProductFormData } from "../validation/product-schema.ts";
import type { ProductSummary } from "../types.ts";

export interface ProductListPageProps {
  readonly onSelectProduct?: ((product: ProductSummary) => void) | undefined;
}

export function ProductListPage({ onSelectProduct }: ProductListPageProps): React.JSX.Element {
  const { t } = useTranslation("catalog");
  const { t: commonT } = useTranslation("common");
  const [search, setSearch] = useState("");
  const [nature, setNature] = useState<"" | "Goods" | "Service">("");
  const [open, setOpen] = useState(false);

  const { data, isLoading, error, refetch } = useProductList({
    search: search || undefined,
    nature: nature || undefined,
    pageSize: 25
  });

  const categories = useCatalogCategories().data ?? [];
  const brands = useCatalogBrands().data ?? [];
  const uoms = useCatalogUoms().data ?? [];
  const create = useCreateProduct();
  const products = data?.items ?? [];

  const columns: DataGridColumn<ProductSummary>[] = [
    {
      id: "displayName",
      header: t("productList.productService"),
      width: "1fr",
      cellRenderer: ({ row }) => (
        <button
          type="button"
          onClick={() => onSelectProduct?.(row)}
          style={{
            border: 0,
            background: "none",
            color: "var(--color-brand-primary, #4f46e5)",
            cursor: "pointer",
            fontWeight: 600,
            fontSize: "0.875rem",
            padding: 0,
            textAlign: "start"
          }}
        >
          {row.displayName}
        </button>
      )
    },
    {
      id: "sku",
      header: "SKU",
      width: 150,
      cellRenderer: ({ row }) => (
        <bdi dir="ltr" style={{ fontFamily: "var(--font-mono, monospace)", fontSize: "0.8125rem", color: "var(--color-content-secondary)" }}>
          {row.sku || "—"}
        </bdi>
      )
    },
    {
      id: "nature",
      header: t("productList.type"),
      width: 120,
      cellRenderer: ({ row }) => (row.nature === "Service" ? t("productList.service") : t("productList.goods"))
    },
    {
      id: "categoryName",
      header: t("productList.category"),
      width: 150,
      cellRenderer: ({ row }) => row.categoryName || "—"
    },
    {
      id: "brandName",
      header: t("productList.brand"),
      width: 150,
      cellRenderer: ({ row }) => row.brandName || "—"
    },
    {
      id: "baseUomCode",
      header: t("productList.unit"),
      width: 110,
      cellRenderer: ({ row }) => row.baseUomCode || "—"
    },
    {
      id: "status",
      header: t("productList.status"),
      width: 110,
      cellRenderer: ({ row }) => (
        <StatusBadge
          status={row.status === "Active" ? "success" : "neutral"}
          label={row.status === "Active" ? t("productList.active") : t("productList.archived")}
          size="small"
        />
      )
    }
  ];

  return (
    <ListPage
      title={t("productList.pageTitle")}
      subtitle={t("productList.subtitle")}
      breadcrumbs={[{ label: t("productList.breadcrumbCatalog") }, { label: t("productList.breadcrumbProducts") }]}
      primaryAction={{
        label: t("productList.add"),
        onClick: () => setOpen(true)
      }}
      filterBar={
        <FilterBar
          searchInput={
            <SearchField
              ariaLabel={t("productList.searchLabel")}
              placeholder={t("productList.searchPlaceholder")}
              value={search}
              onChange={setSearch}
            />
          }
          activeFilterCount={(nature ? 1 : 0) + (search ? 1 : 0)}
          onResetFilters={() => {
            setSearch("");
            setNature("");
          }}
        >
          <div style={{ minWidth: "160px" }}>
            <Select
              label={t("productList.typeFilter")}
              selectedKey={nature}
              onSelectionChange={(k) => setNature(String(k) as typeof nature)}
            >
              <SelectItem id="" textValue={t("productList.allTypes")}>{t("productList.allTypes")}</SelectItem>
              <SelectItem id="Goods" textValue={t("productList.goodsFilter")}>{t("productList.goodsFilter")}</SelectItem>
              <SelectItem id="Service" textValue={t("productList.servicesFilter")}>{t("productList.servicesFilter")}</SelectItem>
            </Select>
          </div>
        </FilterBar>
      }
    >
      {error ? (
        <ErrorState
          title={t("productList.loadError")}
          message={t("productList.loadErrorDescription")}
          onRetry={() => void refetch()}
        />
      ) : !isLoading && products.length === 0 ? (
        <EmptyState
          icon={<CatalogIcon size={28} />}
          title={t("productList.emptyTitle")}
          description={
            search || nature
              ? t("productList.noResults")
              : t("productList.emptyDescription")
          }
          action={
            <Button variant="primary" onPress={() => setOpen(true)}>
              {t("productList.add")}
            </Button>
          }
        />
      ) : (
        <DataGrid<ProductSummary>
          columns={columns}
          staticRows={products}
          loading={isLoading}
          loadingMessage={isLoading ? commonT("states.loading") : undefined}
          onSelectionChange={(rows) => rows[0] && onSelectProduct?.(rows[0])}
        />
      )}

      <Dialog
        isOpen={open}
        onOpenChange={setOpen}
        title={t("productList.add")}
        description={t("productList.addDescription")}
        size="large"
      >
        <ProductForm
          categories={categories}
          brands={brands}
          uoms={uoms}
          onSubmit={async (form: ProductFormData) => {
            await create.mutateAsync(form);
            setOpen(false);
          }}
          onCancel={() => setOpen(false)}
          isSubmitting={create.isPending}
        />
      </Dialog>
    </ListPage>
  );
}

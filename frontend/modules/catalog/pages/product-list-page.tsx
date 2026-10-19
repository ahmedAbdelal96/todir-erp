import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { ListPage } from "../../../components/pages/list-page.tsx";
import { DataGrid, type DataGridColumn } from "../../../components/grid/index.ts";
import { StatusBadge } from "../../../components/fields/status-badge.tsx";
import { SearchField } from "../../../components/primitives/search-field.tsx";
import { FilterBar } from "../../../components/primitives/filter-bar.tsx";
import { Select, SelectItem } from "../../../components/primitives/select.tsx";
import { ErrorState } from "../../../components/primitives/error-state.tsx";
import { EmptyState } from "../../../components/primitives/empty-state.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { Pagination } from "../../../components/primitives/pagination.tsx";
import { CatalogIcon, RefreshIcon } from "../../../components/primitives/icons.tsx";
import {
  useCatalogBrands,
  useCatalogCategories,
  useProductList
} from "../hooks/catalog-queries.ts";
import { catalogApi } from "../services/catalog-api.ts";
import { QuickCreateProductDialog } from "../features/quick-create-product-dialog.tsx";
import { CatalogImportDialog } from "../features/catalog-import-dialog.tsx";
import type { CatalogStatus, ProductDetails, ProductNature, ProductSummary } from "../types.ts";

export interface ProductListPageProps {
  readonly onSelectProduct?: ((product: ProductSummary) => void) | undefined;
}

export function ProductListPage({ onSelectProduct }: ProductListPageProps): React.JSX.Element {
  const { t } = useTranslation("catalog");
  const [search, setSearch] = useState("");
  const [nature, setNature] = useState<ProductNature | "">("");
  const [status, setStatus] = useState<CatalogStatus | "">("");
  const [categoryId, setCategoryId] = useState("");
  const [brandId, setBrandId] = useState("");
  const [page, setPage] = useState(1);
  const pageSize = 25;

  const [isQuickCreateOpen, setIsQuickCreateOpen] = useState(false);
  const [isImportOpen, setIsImportOpen] = useState(false);

  const { data, isLoading, error, refetch } = useProductList({
    search: search || undefined,
    nature: nature || undefined,
    status: status || undefined,
    categoryId: categoryId || undefined,
    brandId: brandId || undefined,
    page,
    pageSize
  });

  const categories = useCatalogCategories().data ?? [];
  const brands = useCatalogBrands().data ?? [];

  const products = data?.items ?? [];
  const totalCount = data?.totalCount ?? 0;
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));

  const handleExportCsv = async () => {
    try {
      const csvText = await catalogApi.exportCsv();
      const blob = new Blob([csvText], { type: "text/csv;charset=utf-8;" });
      const url = URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.href = url;
      link.setAttribute("download", `catalog-${new Date().toISOString().slice(0, 10)}.csv`);
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
    } catch {
      // ignore
    }
  };

  const columns: DataGridColumn<ProductSummary>[] = [
    {
      id: "displayName",
      header: t("productList.productService"),
      width: "1.5fr",
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
      header: t("productList.skuHeader"),
      width: 140,
      cellRenderer: ({ row }) => (
        <bdi dir="ltr" style={{ fontFamily: "var(--font-mono, monospace)", fontSize: "0.8125rem", color: "var(--color-content-secondary)" }}>
          {row.sku || "—"}
        </bdi>
      )
    },
    {
      id: "nature",
      header: t("productList.type"),
      width: 110,
      cellRenderer: ({ row }) => (
        <span
          style={{
            display: "inline-block",
            padding: "0.15rem 0.5rem",
            borderRadius: "var(--radius-sm)",
            fontSize: "0.75rem",
            fontWeight: 600,
            background: row.nature === "Service" ? "var(--color-surface-subtle)" : "rgba(79, 70, 229, 0.08)",
            color: row.nature === "Service" ? "var(--color-content-secondary)" : "var(--color-brand-primary)"
          }}
        >
          {row.nature === "Service" ? t("common.service") : t("common.goods")}
        </span>
      )
    },
    {
      id: "categoryName",
      header: t("productList.category"),
      width: 130,
      cellRenderer: ({ row }) => row.categoryName || "—"
    },
    {
      id: "brandName",
      header: t("productList.brand"),
      width: 130,
      cellRenderer: ({ row }) => row.brandName || "—"
    },
    {
      id: "baseUomCode",
      header: t("productList.unit"),
      width: 90,
      cellRenderer: ({ row }) => (
        <span style={{ fontFamily: "var(--font-mono, monospace)", fontSize: "0.8125rem" }}>
          {row.baseUomCode || "—"}
        </span>
      )
    },
    {
      id: "status",
      header: t("productList.status"),
      width: 100,
      cellRenderer: ({ row }) => (
        <StatusBadge
          status={row.status}
          label={row.status === "Active" ? t("common.active") : t("common.archived")}
        />
      )
    }
  ];

  const filterBar = (
    <FilterBar
      searchInput={
        <SearchField
          value={search}
          onChange={(val) => {
            setSearch(val);
            setPage(1);
          }}
          ariaLabel={t("productList.searchLabel")}
          placeholder={t("productList.searchPlaceholder")}
        />
      }
      onResetFilters={() => {
        setSearch("");
        setNature("");
        setStatus("");
        setCategoryId("");
        setBrandId("");
        setPage(1);
      }}
    >
      <Select
        aria-label={t("productList.typeFilter")}
        selectedKey={nature}
        onSelectionChange={(key) => {
          setNature(String(key) as ProductNature | "");
          setPage(1);
        }}
        placeholder={t("productList.allTypes")}
      >
        <SelectItem id="">{t("productList.allTypes")}</SelectItem>
        <SelectItem id="Goods">{t("common.goods")}</SelectItem>
        <SelectItem id="Service">{t("common.service")}</SelectItem>
      </Select>

      <Select
        aria-label={t("productList.statusFilter")}
        selectedKey={status}
        onSelectionChange={(key) => {
          setStatus(String(key) as CatalogStatus | "");
          setPage(1);
        }}
        placeholder={t("productList.allStatuses")}
      >
        <SelectItem id="">{t("productList.allStatuses")}</SelectItem>
        <SelectItem id="Active">{t("common.active")}</SelectItem>
        <SelectItem id="Archived">{t("common.archived")}</SelectItem>
      </Select>

      <Select
        aria-label={t("productList.categoryFilter")}
        selectedKey={categoryId}
        onSelectionChange={(key) => {
          setCategoryId(String(key));
          setPage(1);
        }}
        placeholder={t("productList.allCategories")}
      >
        <SelectItem id="">{t("productList.allCategories")}</SelectItem>
        {categories.map((c) => (
          <SelectItem key={c.id} id={c.id}>
            {c.name}
          </SelectItem>
        ))}
      </Select>

      <Select
        aria-label={t("productList.brandFilter")}
        selectedKey={brandId}
        onSelectionChange={(key) => {
          setBrandId(String(key));
          setPage(1);
        }}
        placeholder={t("productList.allBrands")}
      >
        <SelectItem id="">{t("productList.allBrands")}</SelectItem>
        {brands.map((b) => (
          <SelectItem key={b.id} id={b.id}>
            {b.name}
          </SelectItem>
        ))}
      </Select>
    </FilterBar>
  );

  return (
    <>
      <ListPage
        title={t("productList.pageTitle")}
        subtitle={t("productList.subtitle")}
        breadcrumbs={[
          { label: t("productList.breadcrumbCatalog"), href: "/catalog" },
          { label: t("productList.breadcrumbProducts") }
        ]}
        primaryAction={{
          label: t("productList.add"),
          onClick: () => setIsQuickCreateOpen(true)
        }}
        secondaryActions={
          <div style={{ display: "flex", gap: "0.5rem", alignItems: "center" }}>
            <Button variant="secondary" size="compact" onPress={() => setIsImportOpen(true)}>
              {t("productList.importAction")}
            </Button>
            <Button variant="secondary" size="compact" onPress={handleExportCsv}>
              {t("productList.exportAction")}
            </Button>
            <Button variant="ghost" size="compact" onPress={() => void refetch()} aria-label={t("productList.refresh")}>
              <RefreshIcon size={16} />
            </Button>
          </div>
        }
        filterBar={filterBar}
        pagination={
          totalCount > 0 ? (
            <Pagination
              page={page}
              totalPages={totalPages}
              onPageChange={setPage}
              totalItems={totalCount}
              pageSize={pageSize}
            />
          ) : undefined
        }
      >
        {isLoading ? (
          <div style={{ display: "flex", justifyContent: "center", padding: "3rem" }}>
            <span style={{ color: "var(--color-content-secondary)" }}>{t("common.loading")}</span>
          </div>
        ) : error ? (
          <ErrorState
            title={t("productList.loadError")}
            message={t("productList.loadErrorDescription")}
            onRetry={() => void refetch()}
          />
        ) : products.length === 0 ? (
          <EmptyState
            icon={<CatalogIcon size={40} />}
            title={search || nature || status || categoryId || brandId ? t("productList.noResults") : t("productList.emptyTitle")}
            description={t("productList.emptyDescription")}
            action={
              <Button variant="primary" size="compact" onPress={() => setIsQuickCreateOpen(true)}>
                {t("productList.add")}
              </Button>
            }
          />
        ) : (
          <DataGrid
            staticRows={products}
            columns={columns}
          />
        )}
      </ListPage>

      <QuickCreateProductDialog
        isOpen={isQuickCreateOpen}
        onOpenChange={setIsQuickCreateOpen}
        onCreated={(created: ProductDetails) => {
          onSelectProduct?.(created);
        }}
      />

      <CatalogImportDialog
        isOpen={isImportOpen}
        onOpenChange={setIsImportOpen}
        onImportCompleted={() => {
          void refetch();
        }}
      />
    </>
  );
}

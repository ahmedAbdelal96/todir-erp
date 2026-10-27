import React, { useState, useEffect } from "react";
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

function useIsMobile(breakpoint = 768): boolean {
  const [isMobile, setIsMobile] = useState(() =>
    typeof window !== "undefined" ? window.innerWidth <= breakpoint : false
  );
  useEffect(() => {
    if (typeof window === "undefined") return;
    const mql = window.matchMedia(`(max-width: ${breakpoint}px)`);
    const onChange = (e: MediaQueryListEvent) => setIsMobile(e.matches);
    setIsMobile(mql.matches);
    mql.addEventListener("change", onChange);
    return () => mql.removeEventListener("change", onChange);
  }, [breakpoint]);
  return isMobile;
}

export function ProductListPage({ onSelectProduct }: ProductListPageProps): React.JSX.Element {
  const { t } = useTranslation("catalog");
  const isMobile = useIsMobile();
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
      width: "2fr",
      minWidth: 260,
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
      width: 180,
      minWidth: 160,
      cellRenderer: ({ row }) => (
        <bdi dir="ltr" style={{ fontFamily: "var(--font-mono, monospace)", fontSize: "0.8125rem", color: "var(--color-content-secondary)" }}>
          {row.sku || "—"}
        </bdi>
      )
    },
    {
      id: "nature",
      header: t("productList.type"),
      width: 140,
      minWidth: 130,
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
      width: 170,
      minWidth: 150,
      cellRenderer: ({ row }) => row.categoryName || "—"
    },
    {
      id: "brandName",
      header: t("productList.brand"),
      width: 160,
      minWidth: 140,
      cellRenderer: ({ row }) => row.brandName || "—"
    },
    {
      id: "baseUomCode",
      header: t("productList.unit"),
      width: 140,
      minWidth: 130,
      cellRenderer: ({ row }) => (
        <bdi dir="ltr" style={{ fontFamily: "var(--font-mono, monospace)", fontSize: "0.8125rem" }}>
          {row.baseUomCode || "—"}
        </bdi>
      )
    },
    {
      id: "status",
      header: t("productList.status"),
      width: 140,
      minWidth: 120,
      cellRenderer: ({ row }) => (
        <StatusBadge
          status={row.status}
          label={row.status === "Active" ? t("common.active") : t("common.archived")}
        />
      )
    }
  ];

  const activeFilters = [
    Boolean(nature),
    Boolean(status),
    Boolean(categoryId),
    Boolean(brandId)
  ].filter(Boolean).length;

  const filterBar = (
    <FilterBar
      activeFilterCount={activeFilters}
      searchInput={
        <SearchField
          size="compact"
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
      <div className="ui-filter-item">
        <Select
          size="compact"
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
      </div>

      <div className="ui-filter-item">
        <Select
          size="compact"
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
      </div>

      <div className="ui-filter-item">
        <Select
          size="compact"
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
      </div>

      <div className="ui-filter-item">
        <Select
          size="compact"
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
      </div>
    </FilterBar>
  );

  const helpContent = {
    summary: t("productList.help.summary"),
    workflowSteps: [
      { step: 1, title: t("productList.help.step1Title"), description: t("productList.help.step1Desc") },
      { step: 2, title: t("productList.help.step2Title"), description: t("productList.help.step2Desc") },
      { step: 3, title: t("productList.help.step3Title"), description: t("productList.help.step3Desc") }
    ],
    keyConcepts: [
      { term: t("productList.help.concept1Term"), explanation: t("productList.help.concept1Desc") },
      { term: t("productList.help.concept2Term"), explanation: t("productList.help.concept2Desc") },
      { term: t("productList.help.concept3Term"), explanation: t("productList.help.concept3Desc") }
    ],
    proTips: [
      t("productList.help.tip1"),
      t("productList.help.tip2")
    ],
    faqs: [
      { question: t("productList.help.faq1Q"), answer: t("productList.help.faq1A") },
      { question: t("productList.help.faq2Q"), answer: t("productList.help.faq2A") }
    ]
  };

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
        help={helpContent}
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
        ) : isMobile ? (
          <div style={{ display: "flex", flexDirection: "column", gap: "0.75rem" }}>
            {products.map((product) => (
              <div
                key={product.id}
                style={{
                  backgroundColor: "var(--color-surface-card)",
                  border: "1px solid var(--color-border-default)",
                  borderRadius: "var(--radius-md)",
                  padding: "0.875rem 1rem",
                  display: "flex",
                  flexDirection: "column",
                  gap: "0.5rem",
                  boxShadow: "var(--shadow-sm)"
                }}
              >
                <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", gap: "0.5rem" }}>
                  <button
                    type="button"
                    onClick={() => onSelectProduct?.(product)}
                    style={{
                      border: 0,
                      background: "none",
                      color: "var(--color-brand-primary, #4f46e5)",
                      cursor: "pointer",
                      fontWeight: 700,
                      fontSize: "0.9375rem",
                      padding: 0,
                      textAlign: "start",
                      lineHeight: 1.3
                    }}
                  >
                    {product.displayName}
                  </button>
                  <StatusBadge
                    status={product.status}
                    label={product.status === "Active" ? t("common.active") : t("common.archived")}
                    size="small"
                  />
                </div>

                <div style={{ display: "flex", flexWrap: "wrap", alignItems: "center", gap: "0.5rem", fontSize: "0.8125rem" }}>
                  {product.sku && (
                    <bdi dir="ltr" style={{ fontFamily: "var(--font-mono, monospace)", color: "var(--color-content-secondary)", background: "var(--color-surface-subtle)", padding: "0.1rem 0.4rem", borderRadius: "var(--radius-sm)" }}>
                      {product.sku}
                    </bdi>
                  )}
                  <span
                    style={{
                      display: "inline-block",
                      padding: "0.1rem 0.4rem",
                      borderRadius: "var(--radius-sm)",
                      fontSize: "0.75rem",
                      fontWeight: 600,
                      background: product.nature === "Service" ? "var(--color-surface-subtle)" : "rgba(79, 70, 229, 0.08)",
                      color: product.nature === "Service" ? "var(--color-content-secondary)" : "var(--color-brand-primary)"
                    }}
                  >
                    {product.nature === "Service" ? t("common.service") : t("common.goods")}
                  </span>
                  {product.baseUomCode && (
                    <span style={{ color: "var(--color-content-muted)" }}>
                      {t("productList.unit")}: <bdi dir="ltr">{product.baseUomCode}</bdi>
                    </span>
                  )}
                </div>

                {(product.categoryName || product.brandName) && (
                  <div style={{ display: "flex", gap: "0.75rem", fontSize: "0.75rem", color: "var(--color-content-secondary)", borderTop: "1px dashed var(--color-border-subtle)", paddingTop: "0.375rem" }}>
                    {product.categoryName && <span>{product.categoryName}</span>}
                    {product.brandName && <span>• {product.brandName}</span>}
                  </div>
                )}
              </div>
            ))}
          </div>
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

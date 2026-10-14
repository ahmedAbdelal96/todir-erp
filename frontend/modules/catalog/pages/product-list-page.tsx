import React, { useState } from "react";
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
      header: "المنتج / الخدمة",
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
      header: "النوع",
      width: 120,
      cellRenderer: ({ row }) => (row.nature === "Service" ? "خدمة" : "بضاعة")
    },
    {
      id: "categoryName",
      header: "الفئة",
      width: 150,
      cellRenderer: ({ row }) => row.categoryName || "—"
    },
    {
      id: "brandName",
      header: "العلامة",
      width: 150,
      cellRenderer: ({ row }) => row.brandName || "—"
    },
    {
      id: "baseUomCode",
      header: "الوحدة",
      width: 110,
      cellRenderer: ({ row }) => row.baseUomCode || "—"
    },
    {
      id: "status",
      header: "الحالة",
      width: 110,
      cellRenderer: ({ row }) => (
        <StatusBadge
          status={row.status === "Active" ? "success" : "neutral"}
          label={row.status === "Active" ? "نشط" : "مؤرشف"}
          size="small"
        />
      )
    }
  ];

  return (
    <ListPage
      title="كتالوج المنتجات والخدمات"
      subtitle="هوية موحدة للمنتجات والخدمات المستخدمة في المبيعات والمشتريات والمخزون"
      breadcrumbs={[{ label: "Catalog" }, { label: "المنتجات والخدمات" }]}
      primaryAction={{
        label: "إضافة منتج أو خدمة",
        onClick: () => setOpen(true)
      }}
      filterBar={
        <FilterBar
          searchInput={
            <SearchField
              ariaLabel="البحث في المنتجات"
              placeholder="بحث بالاسم أو SKU أو باركود..."
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
              label="نوع المنتج"
              selectedKey={nature}
              onSelectionChange={(k) => setNature(String(k) as typeof nature)}
            >
              <SelectItem id="" textValue="كل الأنواع">كل الأنواع</SelectItem>
              <SelectItem id="Goods" textValue="البضائع">البضائع</SelectItem>
              <SelectItem id="Service" textValue="الخدمات">الخدمات</SelectItem>
            </Select>
          </div>
        </FilterBar>
      }
    >
      {error ? (
        <ErrorState
          title="تعذر تحميل كتالوج المنتجات"
          message="حدث خطأ أثناء جلب قائمة المنتجات والخدمات من الخادم."
          onRetry={() => void refetch()}
        />
      ) : !isLoading && products.length === 0 ? (
        <EmptyState
          icon={<CatalogIcon size={28} />}
          title="لا توجد منتجات أو خدمات حتى الآن"
          description={
            search || nature
              ? "لم يتم العثور على منتجات تطابق معايير البحث الحالية."
              : "ابدأ بإضافة أول منتج أو خدمة في كتالوج المؤسسة."
          }
          action={
            <Button variant="primary" onPress={() => setOpen(true)}>
              إضافة منتج أو خدمة
            </Button>
          }
        />
      ) : (
        <DataGrid<ProductSummary>
          columns={columns}
          staticRows={products}
          locale="ar"
          enableRtl={true}
          loading={isLoading}
          loadingMessage={isLoading ? "جارٍ التحميل..." : undefined}
          onSelectionChange={(rows) => rows[0] && onSelectProduct?.(rows[0])}
        />
      )}

      <Dialog
        isOpen={open}
        onOpenChange={setOpen}
        title="إضافة منتج أو خدمة"
        description="أنشئ الهوية ووحدة الأساس بسرعة؛ التفاصيل التجارية مملوكة لموديولات أخرى."
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

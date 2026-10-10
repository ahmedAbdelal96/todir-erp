import React, { useState } from "react";
import { RecordPage } from "../../../components/pages/record-page.tsx";
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
        <Spinner size="large" aria-label="جارٍ تحميل بيانات المنتج..." />
      </div>
    );
  }

  if (error || !data) {
    return (
      <div style={{ padding: "3rem", textAlign: "center" }}>
        <p style={{ color: "var(--color-status-danger, #ef4444)", fontWeight: 600, marginBottom: "1rem" }}>
          تعذر العثور على بيانات المنتج المطلوب.
        </p>
        <Button variant="secondary" onPress={onBack}>
          العودة للكتالوج
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
        title="طبيعة الصنف"
        value={isService ? "خدمة غير مخزنية" : "بضاعة ومخزون"}
        intent="info"
        icon={<CatalogIcon size={20} />}
        subtitle={isService ? "لا تتطلب تتبع رصيد مستودعي" : "تخضع لحركات وجرد المستودعات"}
      />
      <StatCard
        title="حالة المنتج"
        value={isActive ? "نشط ومتاح للبيع" : "مؤرشف"}
        intent={isActive ? "success" : "neutral"}
        icon={<CheckCircleIcon size={20} />}
        subtitle={isActive ? "يمكن إدراجه بالفواتير وأوامر الشراء" : "متوقف عن العمليات الجديدة"}
      />
      <StatCard
        title="وحدة القياس الأساسية"
        value={data.baseUomCode ?? "—"}
        intent="primary"
        icon={<TagIcon size={20} />}
        subtitle="وحدة التسعير والتداول المعتمدة"
      />
      <StatCard
        title="الباركودات المسجلة"
        value={data.barcodes.length > 0 ? `${data.barcodes.length} باركود` : "لا يوجد"}
        intent="neutral"
        icon={<TagIcon size={20} />}
        subtitle="أكواد المسح الضوئي المرتبطة"
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
        { label: "كتالوج المنتجات والخدمات", href: "/catalog", onClick: onBack },
        { label: data.displayName }
      ]}
      headerActions={
        <div style={{ display: "flex", gap: "0.75rem", flexWrap: "wrap" }}>
          <Button
            variant="primary"
            onPress={() => setEditing(true)}
            isDisabled={!isActive}
          >
            تعديل المنتج
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
            {isActive ? "أرشفة الصنف" : "إلغاء الأرشفة"}
          </Button>
          <Button variant="secondary" onPress={onBack}>
            رجوع
          </Button>
        </div>
      }
      auditInfo={{
        createdAt: data.createdAt,
        updatedAt: data.updatedAt
      }}
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
        {/* Basic Identification */}
        <Surface
          title="بيانات التعريف والتسمية"
          description="الأسماء التجارية والرموز المميزة للصنف"
        >
          <PropertyGrid columns={3}>
            <PropertyItem
              label="اسم العرض الأساسي"
              value={data.displayName}
              icon={<CatalogIcon size={16} />}
            />
            <PropertyItem
              label="الاسم بالعربية"
              value={data.nameAr || "—"}
            />
            <PropertyItem
              label="الاسم بالإنجليزية"
              value={
                data.nameEn ? (
                  <bdi dir="ltr">{data.nameEn}</bdi>
                ) : (
                  "—"
                )
              }
            />
            <PropertyItem
              label="رمز التخزين التعريفي (SKU)"
              value={data.sku ? <bdi dir="ltr">{data.sku}</bdi> : "—"}
              isCopyable={Boolean(data.sku)}
              rawCopyValue={data.sku ?? undefined}
            />
            <PropertyItem
              label="نوع الصنف"
              value={
                <StatusBadge
                  intent="info"
                  label={isService ? "خدمة" : "بضاعة مخزنية"}
                  size="small"
                />
              }
            />
            <PropertyItem
              label="حالة الصنف"
              value={
                <StatusBadge
                  intent={isActive ? "success" : "neutral"}
                  label={isActive ? "نشط ومتاح" : "مؤرشف"}
                  size="small"
                />
              }
            />
          </PropertyGrid>
        </Surface>

        {/* Classification & Structure */}
        <Surface
          title="التصنيف والعلامة التجارية"
          description="المجموعة السلعية والشركات المصنعة ووحدات القياس"
        >
          <PropertyGrid columns={3}>
            <PropertyItem
              label="الفئة / التصنيف"
              value={data.categoryName || "غير مصنف"}
              icon={<FolderIcon size={16} />}
            />
            <PropertyItem
              label="العلامة التجارية (Brand)"
              value={data.brandName || "بدون علامة"}
              icon={<BuildingIcon size={16} />}
            />
            <PropertyItem
              label="وحدة القياس المعتمدة (UoM)"
              value={data.baseUomCode || "—"}
              icon={<TagIcon size={16} />}
            />
          </PropertyGrid>
        </Surface>

        {/* Barcodes */}
        <Surface
          title="الباركودات المرتبطة"
          description="الأكواد المستخدمة في نقاط البيع وماسحات الباركود"
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
              لم يتم تعيين أي باركود لهذا الصنف حتى الآن.
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

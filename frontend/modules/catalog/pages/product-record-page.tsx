import React, { useState, useEffect } from "react";
import { useTranslation } from "react-i18next";
import { useQueryTab } from "../../../core/routing/use-query-tab.ts";
import { RecordPage } from "../../../components/pages/record-page.tsx";
import { Surface } from "../../../components/pages/surface.tsx";
import { PropertyGrid, PropertyItem, StatCard } from "../../../components/pages/property-grid.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { StatusBadge } from "../../../components/fields/status-badge.tsx";
import { Spinner } from "../../../components/primitives/spinner.tsx";
import { Tabs, TabList, Tab, TabPanel } from "../../../components/primitives/tabs.tsx";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "../../../components/primitives/table.tsx";
import { Select, SelectItem } from "../../../components/primitives/select.tsx";
import { ConfirmDialog } from "../../../components/primitives/confirm-dialog.tsx";
import { Alert } from "../../../components/primitives/alert.tsx";
import {
  CatalogIcon,
  CheckCircleIcon,
  TagIcon,
  BuildingIcon,
  AlertCircleIcon
} from "../../../components/primitives/icons.tsx";
import {
  useChangeProductStatus,
  useProduct,
  useProductAttributes,
  useProductReadiness,
  useProductReferences,
  useProductUoms
} from "../hooks/catalog-queries.ts";
import { EditProductDialog } from "../features/edit-product-dialog.tsx";
import { ProductUomDialog } from "../features/product-uom-dialog.tsx";
import { BarcodeDialog } from "../features/barcode-dialog.tsx";
import { VariantMatrixDialog } from "../features/variant-matrix-dialog.tsx";
import { ReferenceDialog } from "../features/reference-dialog.tsx";
import { VisibilityDialog } from "../features/visibility-dialog.tsx";
import { formatCatalogAuditEvent } from "../audit-format.ts";
import { ProductSellingPriceCard } from "../../pricing/public.ts";
import type { ProductDetails, VariantDto } from "../types.ts";

export interface ProductRecordPageProps {
  readonly productId: string;
  readonly onBack: () => void;
}

type ProductRecordTab = "overview" | "variants" | "unitsAndBarcodes" | "references" | "audit";

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

export function ProductRecordPage({
  productId,
  onBack
}: ProductRecordPageProps): React.JSX.Element {
  const { t } = useTranslation("catalog");
  const isMobile = useIsMobile();
  const { data: product, error, isLoading, refetch } = useProduct(productId);

  const [activeTab, setActiveTab] = useQueryTab<ProductRecordTab>({
    defaultTab: "overview",
    allowedTabs: ["overview", "variants", "unitsAndBarcodes", "references", "audit"] as const
  });

  // Dialog states
  const [isEditOpen, setIsEditOpen] = useState(false);
  const [isArchiveConfirmOpen, setIsArchiveConfirmOpen] = useState(false);
  const [isUomOpen, setIsUomOpen] = useState(false);
  const [isBarcodeOpen, setIsBarcodeOpen] = useState(false);
  const [isVariantOpen, setIsVariantOpen] = useState(false);
  const [isReferenceOpen, setIsReferenceOpen] = useState(false);
  const [isVisibilityOpen, setIsVisibilityOpen] = useState(false);

  // Lazy queries: only enabled when relevant tab is selected!
  const readinessQuery = useProductReadiness(productId);
  const uomsQuery = useProductUoms(product?.defaultVariantId ?? "");
  const attributesQuery = useProductAttributes(productId);
  const referencesQuery = useProductReferences(productId);

  const statusMutation = useChangeProductStatus();

  if (isLoading) {
    return (
      <div style={{ display: "flex", justifyContent: "center", alignItems: "center", minHeight: "360px" }}>
        <Spinner size="large" aria-label={t("productRecord.loading")} />
      </div>
    );
  }

  if (error || !product) {
    return (
      <div style={{ padding: "3rem", textAlign: "center" }}>
        <p style={{ color: "var(--color-status-danger)", fontWeight: 600, marginBottom: "1rem" }}>
          {t("productRecord.notFound")}
        </p>
        <Button variant="secondary" onPress={onBack}>
          {t("productRecord.backToCatalog")}
        </Button>
      </div>
    );
  }

  const isActive = product.status === "Active";
  const isService = product.nature === "Service";
  const readiness = readinessQuery.data;
  const alternateUoms = (uomsQuery.data ?? []).filter((u) => !u.isBase);
  const baseUom = (uomsQuery.data ?? []).find((u) => u.isBase);
  const templateAttributes = attributesQuery.data ?? [];
  const references = referencesQuery.data?.items ?? [];

  const handleToggleArchive = async () => {
    try {
      await statusMutation.mutateAsync({
        id: product.id,
        archive: isActive,
        version: product.version
      });
      setIsArchiveConfirmOpen(false);
      void refetch();
    } catch {
      // ignore
    }
  };

  const kpiCards = (
    <>
      <StatCard
        title={t("productRecord.nature")}
        value={isService ? t("common.service") : t("common.goods")}
        intent="info"
        icon={<CatalogIcon size={20} />}
        subtitle={isService ? t("productRecord.natureServiceDescription") : t("productRecord.natureGoodDescription")}
      />
      <StatCard
        title={t("productRecord.productStatus")}
        value={isActive ? t("common.active") : t("common.archived")}
        intent={isActive ? "success" : "neutral"}
        icon={<CheckCircleIcon size={20} />}
        subtitle={isActive ? t("productRecord.availableForTransactions") : t("productRecord.stoppedForNewTransactions")}
      />
      <StatCard
        title={t("productRecord.baseUnit")}
        value={product.baseUomCode ?? "—"}
        intent="neutral"
        icon={<TagIcon size={20} />}
        subtitle={t("productRecord.tradingUnit")}
      />
      <StatCard
        title={t("productRecord.readinessTitle")}
        value={readiness?.ready ? t("productRecord.ready") : t("productRecord.attentionRequired")}
        intent={readiness?.ready ? "success" : "warning"}
        icon={<BuildingIcon size={20} />}
        subtitle={
          readiness?.missing.length
            ? t("productRecord.missingCount", { count: readiness.missing.length })
            : t("productRecord.readyForOperations")
        }
      />
    </>
  );

  return (
    <>
      <RecordPage
        title={product.displayName}
        recordCode={product.sku || undefined}
        status={isActive ? "Active" : "Archived"}
        statusLabel={isActive ? t("common.active") : t("common.archived")}
        kpiCards={kpiCards}
        headerActions={
          <div style={{ display: "flex", gap: "0.5rem" }}>
            <Button variant="secondary" size="compact" onPress={() => setIsEditOpen(true)}>
              {t("common.edit")}
            </Button>
            <Button variant="secondary" size="compact" onPress={() => setIsVisibilityOpen(true)}>
              {t("productRecord.visibilityAction")}
            </Button>
            <Button
              variant={isActive ? "danger" : "secondary"}
              size="compact"
              onPress={() => setIsArchiveConfirmOpen(true)}
            >
              {isActive ? t("productRecord.archive") : t("productRecord.unarchive")}
            </Button>
            <Button variant="ghost" size="compact" onPress={onBack}>
              {t("common.back")}
            </Button>
          </div>
        }
      >
        <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
          {/* Mobile Tab Selector */}
          {isMobile && (
            <div style={{ marginBottom: "0.25rem" }}>
              <Select
                aria-label={t("productRecord.tabsAria")}
                selectedKey={activeTab}
                onSelectionChange={(k) => setActiveTab(String(k) as ProductRecordTab)}
              >
                <SelectItem id="overview">{t("productRecord.tabOverview")}</SelectItem>
                <SelectItem id="variants">{t("productRecord.tabVariants")}</SelectItem>
                <SelectItem id="unitsAndBarcodes">{t("productRecord.tabUnitsAndBarcodes")}</SelectItem>
                <SelectItem id="references">{t("productRecord.tabReferences")}</SelectItem>
                <SelectItem id="audit">{t("productRecord.tabAudit")}</SelectItem>
              </Select>
            </div>
          )}

          {/* Tabs Container */}
          <Tabs selectedKey={activeTab} onSelectionChange={(k) => setActiveTab(String(k) as ProductRecordTab)}>
            <div style={{ display: isMobile ? "none" : "block" }}>
              <TabList aria-label={t("productRecord.tabsAria")}>
                <Tab id="overview">{t("productRecord.tabOverview")}</Tab>
                <Tab id="variants">{t("productRecord.tabVariants")}</Tab>
                <Tab id="unitsAndBarcodes">{t("productRecord.tabUnitsAndBarcodes")}</Tab>
                <Tab id="references">{t("productRecord.tabReferences")}</Tab>
                <Tab id="audit">{t("productRecord.tabAudit")}</Tab>
              </TabList>
            </div>

            {/* TAB 1: OVERVIEW */}
            <TabPanel id="overview">
              <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem", marginTop: "1rem" }}>
                <Surface title={t("productRecord.identity")}>
                  <PropertyGrid columns={3}>
                    <PropertyItem label={t("productRecord.displayName")} value={product.displayName} />
                    <PropertyItem
                      label={t("productRecord.sku")}
                      value={
                        product.sku ? (
                          <bdi dir="ltr" style={{ fontFamily: "var(--font-mono, monospace)" }}>
                            {product.sku}
                          </bdi>
                        ) : (
                          "—"
                        )
                      }
                      isCopyable={Boolean(product.sku)}
                      rawCopyValue={product.sku ?? undefined}
                    />
                    <PropertyItem
                      label={t("productRecord.itemType")}
                      value={isService ? t("common.service") : t("common.goods")}
                    />
                    <PropertyItem label={t("productRecord.nameAr")} value={product.nameAr || "—"} />
                    <PropertyItem label={t("productRecord.nameEn")} value={product.nameEn || "—"} />
                    <PropertyItem label={t("productRecord.baseUnit")} value={product.baseUomCode || "—"} />
                  </PropertyGrid>
                </Surface>

                <Surface title={t("productRecord.classification")}>
                  <PropertyGrid columns={2}>
                    <PropertyItem label={t("productRecord.category")} value={product.categoryName || t("productRecord.uncategorized")} />
                    <PropertyItem label={t("productRecord.brand")} value={product.brandName || t("productRecord.noBrand")} />
                  </PropertyGrid>
                </Surface>

                {/* Centralized Commercial Selling Price (Pricing Module Integration) */}
                <ProductSellingPriceCard
                  variantId={product.defaultVariantId ?? product.id}
                  uomId={baseUom?.uomId}
                />

                {/* Readiness Breakdown */}
                {readiness ? (
                  <Surface title={t("productRecord.readinessBreakdown")}>
                    <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem" }}>
                      {readiness.ready ? (
                        <Alert variant="success" title={t("productRecord.readyTitle")}>{t("productRecord.readyDescription")}</Alert>
                      ) : null}
                      {readiness.missing.map((item) => (
                        <div key={item} style={{ display: "flex", alignItems: "center", gap: "0.5rem", color: "var(--color-status-danger)", fontSize: "0.875rem" }}>
                          <span>✕</span>
                          <span>{t(`readiness.missing.${item}`, { defaultValue: item })}</span>
                        </div>
                      ))}
                      {readiness.warnings.map((warn) => (
                        <div key={warn} style={{ display: "flex", alignItems: "center", gap: "0.5rem", color: "var(--color-status-warning)", fontSize: "0.875rem" }}>
                          <AlertCircleIcon size={14} />
                          <span>{t(`readiness.warning.${warn}`, { defaultValue: warn })}</span>
                        </div>
                      ))}
                    </div>
                  </Surface>
                ) : null}
              </div>
            </TabPanel>

            {/* TAB 2: VARIANTS */}
            <TabPanel id="variants">
              <div style={{ display: "flex", flexDirection: "column", gap: "1rem", marginTop: "1rem" }}>
                <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                  <div>
                    <h3 style={{ margin: "0 0 0.25rem 0", fontSize: "1rem" }}>{t("productRecord.variantsTitle")}</h3>
                    <p style={{ margin: 0, fontSize: "0.8125rem", color: "var(--color-content-secondary)" }}>
                      {t("productRecord.variantsDescription")}
                    </p>
                  </div>
                  <Button variant="primary" size="compact" onPress={() => setIsVariantOpen(true)}>
                    + {t("productRecord.addVariantAction")}
                  </Button>
                </div>

                {templateAttributes.length > 0 ? (
                  <div style={{ display: "flex", gap: "0.75rem", flexWrap: "wrap", padding: "0.75rem", background: "var(--color-surface-subtle)", borderRadius: "var(--radius-sm)" }}>
                    <span style={{ fontSize: "0.8125rem", fontWeight: 600 }}>{t("productRecord.assignedAttributes")}:</span>
                    {templateAttributes.map((ta, idx) => (
                      <span key={ta.attribute?.id ?? idx} style={{ fontSize: "0.8125rem", background: "var(--color-surface-card)", padding: "0.15rem 0.5rem", borderRadius: "var(--radius-sm)", border: "1px solid var(--color-border-subtle)" }}>
                        {ta.attribute?.nameAr ?? ta.attribute?.nameEn ?? ta.attribute?.code ?? "—"} ({ta.values?.map((v) => v.nameAr || v.nameEn || v.code).join(", ")})
                      </span>
                    ))}
                  </div>
                ) : null}

                <Surface>
                  <Table aria-label={t("productRecord.variantsTableAria")}>
                    <TableHeader>
                      <TableRow>
                        <TableHead>{t("common.variant")}</TableHead>
                        <TableHead>{t("common.sku")}</TableHead>
                        <TableHead>{t("common.status")}</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {/* Default Variant */}
                      <TableRow>
                        <TableCell>
                          <strong>{t("productRecord.defaultVariant")}</strong>
                        </TableCell>
                        <TableCell>
                          <bdi dir="ltr" style={{ fontFamily: "var(--font-mono, monospace)" }}>
                            {product.variants?.find((v: VariantDto) => Boolean(v.isDefault || v.isDefaultVariant))?.sku || product.sku || "—"}
                          </bdi>
                        </TableCell>
                        <TableCell>
                          <StatusBadge status={product.status} label={isActive ? t("common.active") : t("common.archived")} />
                        </TableCell>
                      </TableRow>

                      {/* Derived Variants */}
                      {product.variants
                        ?.filter((v: VariantDto) => !v.isDefault && !v.isDefaultVariant)
                        .map((variant: VariantDto) => {
                          const attrSummary = variant.attributes
                            ? Object.entries(variant.attributes)
                                .map(([k, val]) => `${k}: ${val}`)
                                .join(" / ")
                            : variant.id;
                          return (
                            <TableRow key={variant.id}>
                              <TableCell>{attrSummary || variant.id}</TableCell>
                              <TableCell>
                                <bdi dir="ltr" style={{ fontFamily: "var(--font-mono, monospace)" }}>
                                  {variant.sku || "—"}
                                </bdi>
                              </TableCell>
                              <TableCell>
                                <StatusBadge
                                  status={variant.status}
                                  label={variant.status === "Active" ? t("common.active") : t("common.archived")}
                                />
                              </TableCell>
                            </TableRow>
                          );
                        })}
                    </TableBody>
                  </Table>
                </Surface>
              </div>
            </TabPanel>

            {/* TAB 3: UNITS & BARCODES */}
            <TabPanel id="unitsAndBarcodes">
              <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem", marginTop: "1rem" }}>
                {/* Alternate UOMs Section */}
                <div style={{ display: "flex", flexDirection: "column", gap: "0.75rem" }}>
                  <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                    <div>
                      <h3 style={{ margin: "0 0 0.25rem 0", fontSize: "1rem" }}>{t("productRecord.packagingUomsTitle")}</h3>
                      <p style={{ margin: 0, fontSize: "0.8125rem", color: "var(--color-content-secondary)" }}>
                        {t("productRecord.packagingUomsDescription", { base: product.baseUomCode || "—" })}
                      </p>
                    </div>
                    <Button variant="secondary" size="compact" onPress={() => setIsUomOpen(true)}>
                      + {t("productRecord.addUomAction")}
                    </Button>
                  </div>

                  <Surface>
                    <Table aria-label={t("productRecord.uomsTableAria")}>
                      <TableHeader>
                        <TableRow>
                          <TableHead>{t("common.unit")}</TableHead>
                          <TableHead>{t("productRecord.factorToBase")}</TableHead>
                          <TableHead>{t("productRecord.conversionFormulaHeader")}</TableHead>
                          <TableHead>{t("productRecord.usageFlagsHeader")}</TableHead>
                          <TableHead>{t("common.status")}</TableHead>
                        </TableRow>
                      </TableHeader>
                      <TableBody>
                        {/* Base UOM Row */}
                        <TableRow>
                          <TableCell style={{ fontWeight: 600 }}>
                            {product.baseUomCode} ({t("productRecord.baseUnitBadge")})
                          </TableCell>
                          <TableCell>{t("productRecord.baseUnitFactor")}</TableCell>
                          <TableCell>
                            <bdi dir="ltr" style={{ display: "inline-block", unicodeBidi: "isolate" }}>
                              {t("productRecord.baseConversionRate", { base: product.baseUomCode || "—" })}
                            </bdi>
                          </TableCell>
                          <TableCell>{t("productRecord.allUsage")}</TableCell>
                          <TableCell>
                            <StatusBadge status="Active" label={t("common.active")} />
                          </TableCell>
                        </TableRow>

                        {/* Alternate UOMs */}
                        {alternateUoms.map((uom) => (
                          <TableRow key={uom.id}>
                            <TableCell style={{ fontWeight: 600 }}>{uom.uomCode}</TableCell>
                            <TableCell>{uom.factorToBase}</TableCell>
                            <TableCell style={{ fontStyle: "italic", color: "var(--color-brand-primary)" }}>
                              <bdi dir="ltr" style={{ display: "inline-block", unicodeBidi: "isolate", fontWeight: 600 }}>
                                {t("productRecord.conversionRate", { target: uom.uomCode, factor: uom.factorToBase, base: product.baseUomCode ?? "" })}
                              </bdi>
                            </TableCell>
                            <TableCell>{uom.usageFlags?.join(", ") || t("productRecord.allUsage")}</TableCell>
                            <TableCell>
                              <StatusBadge status={uom.status} label={uom.status === "Active" ? t("common.active") : t("common.archived")} />
                            </TableCell>
                          </TableRow>
                        ))}
                      </TableBody>
                    </Table>
                  </Surface>
                </div>

                {/* Barcodes Section */}
                <div style={{ display: "flex", flexDirection: "column", gap: "0.75rem" }}>
                  <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                    <div>
                      <h3 style={{ margin: "0 0 0.25rem 0", fontSize: "1rem" }}>{t("productRecord.barcodesTitle")}</h3>
                      <p style={{ margin: 0, fontSize: "0.8125rem", color: "var(--color-content-secondary)" }}>
                        {t("productRecord.barcodesDescription")}
                      </p>
                    </div>
                    <Button variant="secondary" size="compact" onPress={() => setIsBarcodeOpen(true)}>
                      + {t("productRecord.addBarcodeAction")}
                    </Button>
                  </div>

                  <Surface>
                    {product.barcodes.length === 0 ? (
                      <div style={{ padding: "1.5rem", textAlign: "center", color: "var(--color-content-secondary)", fontSize: "0.875rem" }}>
                        {t("productRecord.noBarcodesYet")}
                      </div>
                    ) : (
                      <Table aria-label={t("productRecord.barcodesTableAria")}>
                        <TableHeader>
                          <TableRow>
                            <TableHead>{t("productRecord.barcodeValue")}</TableHead>
                            <TableHead>{t("productRecord.barcodeTypeHeader")}</TableHead>
                            <TableHead>{t("productRecord.associatedUom")}</TableHead>
                            <TableHead>{t("productRecord.isPrimaryBadge")}</TableHead>
                          </TableRow>
                        </TableHeader>
                        <TableBody>
                          {product.barcodes.map((bc) => (
                            <TableRow key={bc.id}>
                              <TableCell>
                                <bdi dir="ltr" style={{ fontFamily: "var(--font-mono, monospace)", fontWeight: 600 }}>
                                  {bc.value}
                                </bdi>
                              </TableCell>
                              <TableCell>{bc.barcodeType}</TableCell>
                              <TableCell>{product.baseUomCode || "—"}</TableCell>
                              <TableCell>
                                {bc.isPrimary ? (
                                  <StatusBadge status="Active" label={t("productRecord.primaryBadge")} />
                                ) : (
                                  "—"
                                )}
                              </TableCell>
                            </TableRow>
                          ))}
                        </TableBody>
                      </Table>
                    )}
                  </Surface>
                </div>
              </div>
            </TabPanel>

            {/* TAB 4: REFERENCES */}
            <TabPanel id="references">
              <div style={{ display: "flex", flexDirection: "column", gap: "1rem", marginTop: "1rem" }}>
                <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                  <div>
                    <h3 style={{ margin: "0 0 0.25rem 0", fontSize: "1rem" }}>{t("productRecord.referencesTitle")}</h3>
                    <p style={{ margin: 0, fontSize: "0.8125rem", color: "var(--color-content-secondary)" }}>
                      {t("productRecord.referencesDescription")}
                    </p>
                  </div>
                  <Button variant="secondary" size="compact" onPress={() => setIsReferenceOpen(true)}>
                    + {t("productRecord.addReferenceAction")}
                  </Button>
                </div>

                <Surface>
                  {references.length === 0 ? (
                    <div style={{ padding: "1.5rem", textAlign: "center", color: "var(--color-content-secondary)", fontSize: "0.875rem" }}>
                      {t("productRecord.noReferencesYet")}
                    </div>
                  ) : (
                    <Table aria-label={t("productRecord.referencesTableAria")}>
                      <TableHeader>
                        <TableRow>
                          <TableHead>{t("productRecord.referenceType")}</TableHead>
                          <TableHead>{t("productRecord.referenceCode")}</TableHead>
                          <TableHead>{t("common.status")}</TableHead>
                        </TableRow>
                      </TableHeader>
                      <TableBody>
                        {references.map((ref) => {
                          const isCust = ref.referenceType === "Customer" || (ref as any).partyType === "Customer";
                          const isSupp = ref.referenceType === "Supplier" || (ref as any).partyType === "Supplier";
                          const refLabel = isCust
                            ? t("referenceDialog.typeCustomer")
                            : isSupp
                            ? t("referenceDialog.typeSupplier")
                            : t("referenceDialog.typeExternal");
                          return (
                            <TableRow key={ref.id}>
                              <TableCell>{refLabel}</TableCell>
                              <TableCell>
                                <bdi dir="ltr" style={{ fontFamily: "var(--font-mono, monospace)" }}>
                                  {ref.referenceCode}
                                </bdi>
                              </TableCell>
                              <TableCell>
                                <StatusBadge status={ref.status} label={ref.status === "Active" ? t("common.active") : t("common.archived")} />
                              </TableCell>
                            </TableRow>
                          );
                        })}
                      </TableBody>
                    </Table>
                  )}
                </Surface>
              </div>
            </TabPanel>

            {/* TAB 5: AUDIT */}
            <TabPanel id="audit">
              <div style={{ display: "flex", flexDirection: "column", gap: "1rem", marginTop: "1rem" }}>
                <h3 style={{ margin: 0, fontSize: "1rem" }}>{t("productRecord.auditHistoryTitle")}</h3>
                <Surface>
                  <div style={{ display: "flex", flexDirection: "column", gap: "0.75rem", padding: "0.5rem" }}>
                    {/* Simulated / formatted change event display */}
                    <div style={{ display: "flex", flexDirection: "column", gap: "0.25rem", borderBottom: "1px solid var(--color-border-subtle)", paddingBottom: "0.75rem" }}>
                      <span style={{ fontWeight: 600, fontSize: "0.875rem" }}>
                        {formatCatalogAuditEvent("product_created", JSON.stringify({ DisplayName: product.displayName, Nature: product.nature })).title}
                      </span>
                      <span style={{ fontSize: "0.75rem", color: "var(--color-content-secondary)" }}>
                        {t("productRecord.createdOnDate", { date: new Date(product.createdAt).toLocaleDateString() })}
                      </span>
                    </div>

                    <div style={{ display: "flex", flexDirection: "column", gap: "0.25rem" }}>
                      <span style={{ fontWeight: 600, fontSize: "0.875rem" }}>
                        {formatCatalogAuditEvent("product_identity_changed", JSON.stringify({ DisplayName: product.displayName, Sku: product.sku })).title}
                      </span>
                      <span style={{ fontSize: "0.75rem", color: "var(--color-content-secondary)" }}>
                        {t("productRecord.lastUpdatedDate", { date: new Date(product.updatedAt).toLocaleDateString() })}
                      </span>
                    </div>
                  </div>
                </Surface>
              </div>
            </TabPanel>
          </Tabs>
        </div>
      </RecordPage>

      {/* Dialogs */}
      <EditProductDialog
        isOpen={isEditOpen}
        onOpenChange={setIsEditOpen}
        product={product}
        onUpdated={() => void refetch()}
      />

      <ConfirmDialog
        isOpen={isArchiveConfirmOpen}
        onOpenChange={setIsArchiveConfirmOpen}
        title={isActive ? t("productRecord.archiveConfirmTitle") : t("productRecord.restoreConfirmTitle")}
        description={isActive ? t("productRecord.archiveConfirmMessage") : t("productRecord.restoreConfirmMessage")}
        confirmLabel={isActive ? t("productRecord.archiveConfirmButton") : t("productRecord.restoreConfirmButton")}
        onConfirm={handleToggleArchive}
        variant={isActive ? "danger" : "primary"}
        isPending={statusMutation.isPending}
      />

      <ProductUomDialog
        isOpen={isUomOpen}
        onOpenChange={setIsUomOpen}
        product={product}
        onAdded={() => {
          void uomsQuery.refetch();
          void refetch();
        }}
      />

      <BarcodeDialog
        isOpen={isBarcodeOpen}
        onOpenChange={setIsBarcodeOpen}
        product={product}
        onAdded={() => void refetch()}
      />

      <VariantMatrixDialog
        isOpen={isVariantOpen}
        onOpenChange={setIsVariantOpen}
        product={product}
        onCreated={() => {
          void attributesQuery.refetch();
          void refetch();
        }}
      />

      <ReferenceDialog
        isOpen={isReferenceOpen}
        onOpenChange={setIsReferenceOpen}
        product={product}
        onAdded={() => void referencesQuery.refetch()}
      />

      <VisibilityDialog
        isOpen={isVisibilityOpen}
        onOpenChange={setIsVisibilityOpen}
        product={product}
        onUpdated={() => void refetch()}
      />
    </>
  );
}

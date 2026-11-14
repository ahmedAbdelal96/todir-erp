import React, { useState, useMemo } from "react";
import { useTranslation } from "react-i18next";
import { Table } from "../../../../components/primitives/table.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Select, type SelectOption } from "../../../../components/primitives/select.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { Pagination } from "../../../../components/primitives/pagination.tsx";
import { AssetLifecycleBadge } from "../../components/asset-lifecycle-badge.tsx";
import { useIsMobile } from "../../hooks/use-is-mobile.ts";
import {
  useFixedAssetsQuery,
  useFixedAssetCategoriesQuery,
  useFixedAssetLocationsQuery
} from "../../hooks/use-fixed-assets-queries.ts";
import { fixedAssetsApi } from "../../services/fixed-assets-api.ts";
import type { FixedAssetDto, AssetLifecycleState, AssetNature } from "../../types.ts";
import {
  PlusIcon,
  DownloadIcon,
  EyeIcon,
  ArrowRightLeftIcon,
  CornerDownLeftIcon,
  TrashIcon,
  RotateCcwIcon
} from "../../components/fixed-assets-icons.tsx";

export interface AssetRegisterTabProps {
  readonly legalEntityId: string;
  readonly onOpenNewAssetModal: () => void;
  readonly onSelectAsset: (asset: FixedAssetDto) => void;
  readonly onTransferAsset: (asset: FixedAssetDto) => void;
  readonly onReturnAsset: (asset: FixedAssetDto) => void;
  readonly onDisposeAsset: (asset: FixedAssetDto) => void;
  readonly onReinstateAsset: (asset: FixedAssetDto) => void;
}

export function AssetRegisterTab({
  legalEntityId,
  onOpenNewAssetModal,
  onSelectAsset,
  onTransferAsset,
  onReturnAsset,
  onDisposeAsset,
  onReinstateAsset
}: AssetRegisterTabProps): React.JSX.Element {
  const { t } = useTranslation("fixedAssets");
  const isMobile = useIsMobile(768);

  const [search, setSearch] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const [selectedState, setSelectedState] = useState<string>("all");
  const [selectedNature, setSelectedNature] = useState<string>("all");
  const [selectedLocation, setSelectedLocation] = useState<string>("all");
  const [page, setPage] = useState(1);
  const [isExporting, setIsExporting] = useState(false);
  const [exportError, setExportError] = useState<string | null>(null);
  const pageSize = 15;

  // Debounce search
  React.useEffect(() => {
    const timer = setTimeout(() => {
      setDebouncedSearch(search);
      setPage(1);
    }, 300);
    return () => clearTimeout(timer);
  }, [search]);

  const categoriesQuery = useFixedAssetCategoriesQuery(legalEntityId);
  const locationsQuery = useFixedAssetLocationsQuery(legalEntityId);

  const assetsQuery = useFixedAssetsQuery(legalEntityId, {
    search: debouncedSearch || undefined,
    state: selectedState !== "all" ? selectedState : undefined,
    nature: selectedNature !== "all" ? selectedNature : undefined,
    locationId: selectedLocation !== "all" ? selectedLocation : undefined,
    pageNumber: page,
    pageSize
  });

  const stateOptions: SelectOption[] = [
    { id: "all", label: t("filters.allStates", "كافة الحالات التشغيلية") },
    { id: "Operational", label: t("states.Operational", "تشغيلي (Operational)") },
    { id: "Capitalized", label: t("states.Capitalized", "رسملة (Capitalized)") },
    { id: "Active", label: t("states.Active", "نشط (Active)") },
    { id: "UnderConstruction", label: t("states.UnderConstruction", "قيد التنفيذ (CIP)") },
    { id: "Draft", label: t("states.Draft", "مسودة (Draft)") },
    { id: "FullyDepreciated", label: t("states.FullyDepreciated", "مستهلك بالكامل (Fully Depreciated)") },
    { id: "Disposed", label: t("states.Disposed", "مستبعد / تم التخريد (Disposed)") }
  ];

  const natureOptions: SelectOption[] = [
    { id: "all", label: t("filters.allNatures", "كافة الطبيعات") },
    { id: "Equipment", label: t("nature.equipment", "آلات ومعدات") },
    { id: "Vehicle", label: t("nature.vehicle", "مركبات وسيارات") },
    { id: "Building", label: t("nature.building", "مباني ومنشآت") },
    { id: "Land", label: t("nature.land", "أراضي") },
    { id: "CIP", label: t("nature.cip", "مشروعات قيد التنفيذ") },
    { id: "Grouped", label: t("nature.grouped", "أصول مجمعة") },
    { id: "Intangible", label: t("nature.intangible", "أصول غير ملموسة") }
  ];

  const locationOptions: SelectOption[] = useMemo(() => {
    const list: SelectOption[] = [{ id: "all", label: t("filters.allLocations", "كافة المواقع") }];
    locationsQuery.data?.items?.forEach((loc) => {
      list.push({ id: loc.id, label: loc.locationName });
    });
    return list;
  }, [locationsQuery.data?.items, t]);

  const items = assetsQuery.data?.items ?? [];
  const totalCount = assetsQuery.data?.totalCount ?? 0;

  // Authoritative Backend CSV Export
  const handleExportCsv = async () => {
    try {
      setIsExporting(true);
      setExportError(null);
      const blob = await fixedAssetsApi.exportAssetRegister(legalEntityId);
      const url = URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.href = url;
      link.download = `Fixed_Asset_Register_${new Date().toISOString().slice(0, 10)}.csv`;
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      URL.revokeObjectURL(url);
    } catch (err: unknown) {
      setExportError(err instanceof Error ? err.message : t("reports.exportFailed", "فشل تصدير التقرير"));
    } finally {
      setIsExporting(false);
    }
  };

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }} data-testid="asset-register-tab">
      {exportError && (
        <div
          role="alert"
          style={{
            padding: "0.75rem 1rem",
            backgroundColor: "#fef2f2",
            border: "1px solid #f87171",
            borderRadius: "0.375rem",
            color: "#991b1b",
            fontSize: "0.875rem"
          }}
        >
          {exportError}
        </div>
      )}

      {/* Top Filter & Actions Bar */}
      <div
        style={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: isMobile ? "stretch" : "center",
          gap: "1rem",
          flexDirection: isMobile ? "column" : "row",
          padding: "1rem",
          backgroundColor: "#ffffff",
          border: "1px solid #cbd5e1",
          borderRadius: "0.5rem"
        }}
      >
        <div
          style={{
            display: "flex",
            gap: "0.75rem",
            alignItems: isMobile ? "stretch" : "center",
            flexDirection: isMobile ? "column" : "row",
            flexWrap: isMobile ? "nowrap" : "wrap",
            flex: 1
          }}
        >
          <div style={{ minWidth: isMobile ? "100%" : "220px", flex: 1 }}>
            <TextField
              value={search}
              onChange={setSearch}
              placeholder={t("register.searchPlaceholder", "بحث برقم الأصل، الاسم، السيريال، أو الباركود...")}
              data-testid="asset-search-input"
            />
          </div>

          <div style={{ minWidth: isMobile ? "100%" : "150px" }}>
            <Select
              options={stateOptions}
              value={selectedState}
              onChange={(val) => {
                setSelectedState(val ? String(val) : "all");
                setPage(1);
              }}
              data-testid="asset-state-filter"
            />
          </div>

          <div style={{ minWidth: isMobile ? "100%" : "150px" }}>
            <Select
              options={natureOptions}
              value={selectedNature}
              onChange={(val) => {
                setSelectedNature(val ? String(val) : "all");
                setPage(1);
              }}
              data-testid="asset-nature-filter"
            />
          </div>

          <div style={{ minWidth: isMobile ? "100%" : "150px" }}>
            <Select
              options={locationOptions}
              value={selectedLocation}
              onChange={(val) => {
                setSelectedLocation(val ? String(val) : "all");
                setPage(1);
              }}
              data-testid="asset-location-filter"
            />
          </div>
        </div>

        {/* Action Buttons */}
        <div
          style={{
            display: "flex",
            gap: "0.5rem",
            alignItems: "center",
            flexDirection: isMobile ? "column" : "row",
            width: isMobile ? "100%" : "auto"
          }}
        >
          <Button
            variant="secondary"
            onClick={handleExportCsv}
            isDisabled={isExporting}
            data-testid="export-csv-btn"
            style={{ width: isMobile ? "100%" : "auto", minHeight: "44px" }}
          >
            <DownloadIcon size={16} style={{ marginInlineEnd: "0.375rem" }} />
            {isExporting ? t("common.exporting", "جاري التصدير...") : t("register.exportCsv", "تصدير CSV")}
          </Button>
          <Button
            variant="primary"
            onClick={onOpenNewAssetModal}
            data-testid="new-asset-btn"
            style={{ width: isMobile ? "100%" : "auto", minHeight: "44px" }}
          >
            <PlusIcon size={16} style={{ marginInlineEnd: "0.375rem" }} />
            {t("register.newAsset", "إضافة أصل جديد")}
          </Button>
        </div>
      </div>

      {/* Main Content: Mobile Cards vs Desktop Table */}
      {isMobile ? (
        <div
          style={{ display: "flex", flexDirection: "column", gap: "0.875rem" }}
          data-testid="asset-register-mobile-cards"
        >
          {items.length === 0 ? (
            <div
              style={{
                backgroundColor: "#ffffff",
                border: "1px solid #cbd5e1",
                borderRadius: "0.5rem",
                padding: "2rem",
                textAlign: "center",
                color: "#64748b"
              }}
            >
              {t("register.noAssetsFound", "لا توجد أصول مطابقة لمعايير البحث")}
            </div>
          ) : (
            items.map((row) => (
              <div
                key={row.id}
                style={{
                  backgroundColor: "#ffffff",
                  border: "1px solid #cbd5e1",
                  borderRadius: "0.5rem",
                  padding: "1rem",
                  display: "flex",
                  flexDirection: "column",
                  gap: "0.75rem",
                  boxShadow: "0 1px 3px rgba(0,0,0,0.05)"
                }}
                data-testid={`asset-card-${row.id}`}
              >
                {/* Header: Asset Code & Lifecycle Badge */}
                <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                  <span style={{ fontWeight: 700, color: "#102a43", fontSize: "0.9375rem" }}>
                    <bdi>{row.assetCode}</bdi>
                  </span>
                  <AssetLifecycleBadge state={row.lifecycleState} size="small" />
                </div>

                {/* Asset Name & Category */}
                <div>
                  <div style={{ fontWeight: 600, color: "#0f172a", fontSize: "1rem", lineHeight: 1.35 }}>
                    {row.assetName}
                  </div>
                  {row.categoryName && (
                    <div style={{ fontSize: "0.8125rem", color: "#475569", marginTop: "0.125rem" }}>
                      {row.nature ? t(`natures.${row.nature}`, row.categoryName || row.nature) : (row.categoryName ? t(`natures.${row.categoryName}`, row.categoryName) : "")}
                    </div>
                  )}
                  {row.serialNumber && (
                    <div style={{ fontSize: "0.75rem", color: "#64748b", marginTop: "0.125rem" }}>
                      {t("fields.serialNumberShort", "ر.ت:")} {row.serialNumber}
                    </div>
                  )}
                </div>

                {/* Location & Custodian */}
                <div
                  style={{
                    display: "grid",
                    gridTemplateColumns: "1fr 1fr",
                    gap: "0.5rem",
                    backgroundColor: "#f8fafc",
                    padding: "0.5rem 0.75rem",
                    borderRadius: "0.375rem",
                    fontSize: "0.8125rem"
                  }}
                >
                  <div>
                    <span style={{ color: "#64748b", fontWeight: 500 }}>{t("fields.location", "الموقع")}: </span>
                    <span style={{ color: "#1e293b" }}>{row.locationName ?? "—"}</span>
                  </div>
                  <div>
                    <span style={{ color: "#64748b", fontWeight: 500 }}>{t("fields.custodian", "العهدة")}: </span>
                    <span style={{ color: "#1e293b" }}>{row.custodianId ?? "—"}</span>
                  </div>
                </div>

                {/* Financial Values (Cost & Net Book Value) */}
                <div
                  style={{
                    display: "flex",
                    justifyContent: "space-between",
                    alignItems: "center",
                    borderTop: "1px dashed #e2e8f0",
                    paddingTop: "0.5rem",
                    fontSize: "0.8125rem"
                  }}
                >
                  <div>
                    <span style={{ color: "#64748b" }}>{t("fields.acquisitionCost", "التكلفة")}: </span>
                    <span style={{ fontWeight: 600, color: "#1e293b" }}>
                      {row.acquisitionCost !== undefined && row.acquisitionCost !== null
                        ? `${row.acquisitionCost.toLocaleString("ar-EG", { minimumFractionDigits: 2 })} ${row.currency ?? ""}`
                        : "—"}
                    </span>
                  </div>
                  <div>
                    <span style={{ color: "#64748b" }}>{t("fields.netBookValue", "صافي الدفترية")}: </span>
                    <span style={{ fontWeight: 700, color: "#16a34a" }}>
                      {row.netBookValue !== undefined && row.netBookValue !== null
                        ? `${row.netBookValue.toLocaleString("ar-EG", { minimumFractionDigits: 2 })} ${row.currency ?? ""}`
                        : "—"}
                    </span>
                  </div>
                </div>

                {/* Action Buttons: Min 44px height for accessibility */}
                <div style={{ display: "flex", gap: "0.375rem", flexWrap: "wrap", marginTop: "0.25rem" }}>
                  <Button
                    variant="secondary"
                    onClick={() => onSelectAsset(row)}
                    data-testid={`view-asset-${row.id}`}
                    style={{ flex: 1, minHeight: "44px" }}
                  >
                    <EyeIcon size={14} style={{ marginInlineEnd: "0.25rem" }} />
                    {t("drawer.viewDetails", "عرض التفاصيل")}
                  </Button>
                  {row.lifecycleState === "Disposed" ? (
                    <Button
                      variant="secondary"
                      onClick={() => onReinstateAsset(row)}
                      data-testid={`reinstate-asset-${row.id}`}
                      style={{ minHeight: "44px" }}
                    >
                      <RotateCcwIcon size={14} style={{ marginInlineEnd: "0.25rem" }} />
                      {t("disposal.reinstate", "إعادة قيد")}
                    </Button>
                  ) : (
                    <>
                      <Button
                        variant="secondary"
                        onClick={() => onTransferAsset(row)}
                        data-testid={`transfer-asset-${row.id}`}
                        style={{ minHeight: "44px" }}
                        aria-label={t("custody.transfer", "نقل العهدة")}
                      >
                        <ArrowRightLeftIcon size={14} />
                      </Button>
                      <Button
                        variant="secondary"
                        onClick={() => onReturnAsset(row)}
                        data-testid={`return-asset-${row.id}`}
                        style={{ minHeight: "44px" }}
                        aria-label={t("custody.return", "إرجاع للمستودع")}
                      >
                        <CornerDownLeftIcon size={14} />
                      </Button>
                      <Button
                        variant="secondary"
                        onClick={() => onDisposeAsset(row)}
                        data-testid={`dispose-asset-${row.id}`}
                        style={{ minHeight: "44px" }}
                        aria-label={t("disposal.dispose", "استبعاد / بيع")}
                      >
                        <TrashIcon size={14} />
                      </Button>
                    </>
                  )}
                </div>
              </div>
            ))
          )}
        </div>
      ) : (
        <div
          style={{
            backgroundColor: "#ffffff",
            border: "1px solid #cbd5e1",
            borderRadius: "0.5rem",
            overflow: "hidden"
          }}
        >
          <div style={{ width: "100%", overflowX: "auto" }}>
            <Table isBordered={false} style={{ minWidth: "1200px", tableLayout: "auto" }} data-testid="asset-register-table">
              <thead>
                <tr style={{ backgroundColor: "#f8fafc", borderBottom: "1px solid #cbd5e1", textAlign: "start" }}>
                  <th style={{ padding: "0.75rem 1rem", minWidth: "130px", whiteSpace: "nowrap" }}>{t("fields.assetCode", "كود الأصل")}</th>
                  <th style={{ padding: "0.75rem 1rem", minWidth: "300px" }}>{t("fields.assetName", "اسم الأصل")}</th>
                  <th style={{ padding: "0.75rem 1rem", minWidth: "140px", whiteSpace: "nowrap" }}>{t("fields.category", "التصنيف")}</th>
                  <th style={{ padding: "0.75rem 1rem", minWidth: "120px", whiteSpace: "nowrap" }}>{t("fields.status", "الحالة")}</th>
                  <th style={{ padding: "0.75rem 1rem", minWidth: "130px", textAlign: "end", whiteSpace: "nowrap" }}>{t("fields.acquisitionCost", "التكلفة التاريخية")}</th>
                  <th style={{ padding: "0.75rem 1rem", minWidth: "130px", textAlign: "end", whiteSpace: "nowrap" }}>{t("fields.netBookValue", "صافي القيمة الدفترية")}</th>
                  <th style={{ padding: "0.75rem 1rem", minWidth: "120px", whiteSpace: "nowrap" }}>{t("fields.location", "الموقع")}</th>
                  <th style={{ padding: "0.75rem 1rem", minWidth: "120px", whiteSpace: "nowrap" }}>{t("fields.custodian", "العهدة")}</th>
                  <th style={{ padding: "0.75rem 1rem", minWidth: "140px", whiteSpace: "nowrap" }}>{t("common.actions", "الإجراءات")}</th>
                </tr>
              </thead>
              <tbody>
                {items.length === 0 ? (
                  <tr>
                    <td colSpan={9} style={{ textAlign: "center", padding: "2rem", color: "#64748b" }}>
                      {t("register.noAssetsFound", "لا توجد أصول مطابقة لمعايير البحث")}
                    </td>
                  </tr>
                ) : (
                  items.map((row) => (
                    <tr
                      key={row.id}
                      style={{ borderBottom: "1px solid #f1f5f9", cursor: "pointer" }}
                      onClick={() => onSelectAsset(row)}
                      data-testid={`asset-row-${row.id}`}
                    >
                      <td style={{ padding: "0.75rem 1rem", whiteSpace: "nowrap" }}>
                        <span style={{ fontWeight: 600, color: "#1e3a8a" }}><bdi>{row.assetCode}</bdi></span>
                      </td>
                      <td style={{ padding: "0.75rem 1rem", minWidth: "300px" }}>
                        <div style={{ fontWeight: 600, color: "#0f172a", whiteSpace: "normal", wordBreak: "normal", lineHeight: 1.45 }}>{row.assetName}</div>
                        {row.serialNumber && (
                          <div style={{ fontSize: "0.75rem", color: "#64748b", marginTop: "0.125rem" }}>{t("fields.serialNumberShort", "ر.ت:")} {row.serialNumber}</div>
                        )}
                      </td>
                      <td style={{ padding: "0.75rem 1rem", whiteSpace: "nowrap" }}>{row.nature ? t(`natures.${row.nature}`, row.categoryName || row.nature) : (row.categoryName ? t(`natures.${row.categoryName}`, row.categoryName) : "—")}</td>
                      <td style={{ padding: "0.75rem 1rem" }}>
                        <AssetLifecycleBadge state={row.lifecycleState} />
                      </td>
                      <td style={{ padding: "0.75rem 1rem", textAlign: "end" }}>
                        {row.acquisitionCost !== undefined && row.acquisitionCost !== null ? (
                          <span style={{ fontWeight: 500 }}>
                            {row.acquisitionCost.toLocaleString("ar-EG", { minimumFractionDigits: 2 })}{" "}
                            {row.currency ?? ""}
                          </span>
                        ) : (
                          <span style={{ color: "#94a3b8" }}>—</span>
                        )}
                      </td>
                      <td style={{ padding: "0.75rem 1rem", textAlign: "end" }}>
                        {row.netBookValue !== undefined && row.netBookValue !== null ? (
                          <span style={{ fontWeight: 600, color: "#16a34a" }}>
                            {row.netBookValue.toLocaleString("ar-EG", { minimumFractionDigits: 2 })}{" "}
                            {row.currency ?? ""}
                          </span>
                        ) : (
                          <span style={{ color: "#94a3b8" }}>—</span>
                        )}
                      </td>
                      <td style={{ padding: "0.75rem 1rem" }}>{row.locationName ?? "—"}</td>
                      <td style={{ padding: "0.75rem 1rem" }}>{row.custodianId ?? "—"}</td>
                      <td style={{ padding: "0.75rem 1rem" }} onClick={(e) => e.stopPropagation()}>
                        <div style={{ display: "flex", gap: "0.375rem" }}>
                          <Button
                            variant="secondary"
                            size="compact"
                            onClick={() => onSelectAsset(row)}
                            data-testid={`view-asset-${row.id}`}
                            aria-label={t("drawer.viewDetails", "عرض الملف الشامل")}
                          >
                            <EyeIcon size={14} />
                          </Button>
                          {row.lifecycleState === "Disposed" ? (
                            <Button
                              variant="secondary"
                              size="compact"
                              onClick={() => onReinstateAsset(row)}
                              data-testid={`reinstate-asset-${row.id}`}
                              aria-label={t("disposal.reinstate", "إعادة قيد")}
                            >
                              <RotateCcwIcon size={14} />
                            </Button>
                          ) : (
                            <>
                              <Button
                                variant="secondary"
                                size="compact"
                                onClick={() => onTransferAsset(row)}
                                data-testid={`transfer-asset-${row.id}`}
                                aria-label={t("custody.transfer", "نقل العهدة")}
                              >
                                <ArrowRightLeftIcon size={14} />
                              </Button>
                              <Button
                                variant="secondary"
                                size="compact"
                                onClick={() => onReturnAsset(row)}
                                data-testid={`return-asset-${row.id}`}
                                aria-label={t("custody.return", "إرجاع للمستودع")}
                              >
                                <CornerDownLeftIcon size={14} />
                              </Button>
                              <Button
                                variant="secondary"
                                size="compact"
                                onClick={() => onDisposeAsset(row)}
                                data-testid={`dispose-asset-${row.id}`}
                                aria-label={t("disposal.dispose", "استبعاد / بيع")}
                              >
                                <TrashIcon size={14} />
                              </Button>
                            </>
                          )}
                        </div>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </Table>
          </div>
        </div>
      )}

      {/* Pagination Bar */}
      {totalCount > pageSize && (
        <div
          style={{
            padding: "0.75rem 1rem",
            backgroundColor: "#ffffff",
            border: "1px solid #cbd5e1",
            borderRadius: "0.5rem",
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            flexDirection: isMobile ? "column" : "row",
            gap: isMobile ? "0.75rem" : "0"
          }}
        >
          <div style={{ fontSize: "0.8125rem", color: "#64748b" }}>
            {t("common.showingRecords", "عرض {{from}} إلى {{to}} من إجمالي {{total}} أصل", {
              from: (page - 1) * pageSize + 1,
              to: Math.min(page * pageSize, totalCount),
              total: totalCount
            })}
          </div>
          <Pagination
            page={page}
            totalPages={Math.ceil(totalCount / pageSize)}
            onPageChange={(p) => setPage(p)}
          />
        </div>
      )}
    </div>
  );
}

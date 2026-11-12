import React, { useState, useMemo } from "react";
import { useTranslation } from "react-i18next";
import { Table } from "../../../../components/primitives/table.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Select, type SelectOption } from "../../../../components/primitives/select.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { Pagination } from "../../../../components/primitives/pagination.tsx";
import { AssetLifecycleBadge } from "../../components/asset-lifecycle-badge.tsx";
import {
  useFixedAssetsQuery,
  useFixedAssetCategoriesQuery,
  useFixedAssetLocationsQuery
} from "../../hooks/use-fixed-assets-queries.ts";
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

  const [search, setSearch] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const [selectedState, setSelectedState] = useState<string>("all");
  const [selectedNature, setSelectedNature] = useState<string>("all");
  const [selectedLocation, setSelectedLocation] = useState<string>("all");
  const [page, setPage] = useState(1);
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
    { value: "all", label: t("filters.allStates", "كافة الحالات التشغيلية") },
    { value: "Active", label: t("states.active", "نشط (Active)") },
    { value: "UnderConstruction", label: t("states.underConstruction", "قيد التنفيذ (CIP)") },
    { value: "Draft", label: t("states.draft", "مسودة (Draft)") },
    { value: "FullyDepreciated", label: t("states.fullyDepreciated", "مستهلك بالكامل (Fully Depreciated)") },
    { value: "Disposed", label: t("states.disposed", "مستبعد / تم التخريد (Disposed)") }
  ];

  const natureOptions: SelectOption[] = [
    { value: "all", label: t("filters.allNatures", "كافة الطبيعات") },
    { value: "Equipment", label: t("nature.equipment", "آلات ومعدات") },
    { value: "Vehicle", label: t("nature.vehicle", "مركبات وسيارات") },
    { value: "Building", label: t("nature.building", "مباني ومنشآت") },
    { value: "Land", label: t("nature.land", "أراضي") },
    { value: "CIP", label: t("nature.cip", "مشروعات قيد التنفيذ") },
    { value: "Grouped", label: t("nature.grouped", "أصول مجمعة") },
    { value: "Intangible", label: t("nature.intangible", "أصول غير ملموسة") }
  ];

  const locationOptions: SelectOption[] = useMemo(() => {
    const list: SelectOption[] = [{ value: "all", label: t("filters.allLocations", "كافة المواقع") }];
    locationsQuery.data?.items?.forEach((loc) => {
      list.push({ value: loc.id, label: loc.locationName });
    });
    return list;
  }, [locationsQuery.data?.items, t]);

  const items = assetsQuery.data?.items ?? [];
  const totalCount = assetsQuery.data?.totalCount ?? 0;

  // Export CSV
  const handleExportCsv = () => {
    if (items.length === 0) return;
    const headers = [
      "Asset Code",
      "Asset Name",
      "Category",
      "Status",
      "Acquisition Cost",
      "Net Book Value",
      "Acquisition Date",
      "In Service Date",
      "Location",
      "Custodian"
    ];
    const rows = items.map((a) => [
      `"${a.assetCode}"`,
      `"${a.assetName}"`,
      `"${a.categoryName}"`,
      `"${a.lifecycleState}"`,
      a.acquisitionCost,
      a.netBookValue,
      `"${a.acquisitionDate}"`,
      `"${a.inServiceDate ?? ""}"`,
      `"${a.locationName ?? ""}"`,
      `"${a.custodianId ?? ""}"`
    ]);
    const csvContent = "data:text/csv;charset=utf-8,\uFEFF" + [headers.join(","), ...rows.map((r) => r.join(","))].join("\n");
    const encodedUri = encodeURI(csvContent);
    const link = document.createElement("a");
    link.setAttribute("href", encodedUri);
    link.setAttribute("download", `Fixed_Asset_Register_${new Date().toISOString().slice(0, 10)}.csv`);
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  };

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }} data-testid="asset-register-tab">
      {/* Top Filter & Actions Bar */}
      <div
        style={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          gap: "1rem",
          flexWrap: "wrap",
          padding: "1rem",
          backgroundColor: "#ffffff",
          border: "1px solid #cbd5e1",
          borderRadius: "0.5rem"
        }}
      >
        <div style={{ display: "flex", gap: "0.75rem", alignItems: "center", flexWrap: "wrap", flex: 1 }}>
          <div style={{ minWidth: "240px", flex: 1 }}>
            <TextField
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder={t("register.searchPlaceholder", "بحث برقم الأصل، الاسم، السيريال، أو الباركود...")}
              data-testid="asset-search-input"
            />
          </div>

          <div style={{ minWidth: "160px" }}>
            <Select
              options={stateOptions}
              value={selectedState}
              onChange={(val) => {
                setSelectedState(val);
                setPage(1);
              }}
              data-testid="asset-state-filter"
            />
          </div>

          <div style={{ minWidth: "160px" }}>
            <Select
              options={natureOptions}
              value={selectedNature}
              onChange={(val) => {
                setSelectedNature(val);
                setPage(1);
              }}
              data-testid="asset-nature-filter"
            />
          </div>

          <div style={{ minWidth: "160px" }}>
            <Select
              options={locationOptions}
              value={selectedLocation}
              onChange={(val) => {
                setSelectedLocation(val);
                setPage(1);
              }}
              data-testid="asset-location-filter"
            />
          </div>
        </div>

        {/* Action Buttons */}
        <div style={{ display: "flex", gap: "0.5rem", alignItems: "center" }}>
          <Button variant="secondary" onClick={handleExportCsv} data-testid="export-csv-btn">
            <DownloadIcon size={16} style={{ marginInlineEnd: "0.375rem" }} />
            {t("register.exportCsv", "تصدير CSV")}
          </Button>
          <Button variant="primary" onClick={onOpenNewAssetModal} data-testid="new-asset-btn">
            <PlusIcon size={16} style={{ marginInlineEnd: "0.375rem" }} />
            {t("register.newAsset", "إضافة أصل جديد")}
          </Button>
        </div>
      </div>

      {/* Main Table */}
      <div
        style={{
          backgroundColor: "#ffffff",
          border: "1px solid #cbd5e1",
          borderRadius: "0.5rem",
          overflow: "hidden"
        }}
      >
        <Table isBordered={false} data-testid="asset-register-table">
          <thead>
            <tr style={{ backgroundColor: "#f8fafc", borderBottom: "1px solid #cbd5e1", textAlign: "start" }}>
              <th style={{ padding: "0.75rem 1rem" }}>{t("fields.assetCode", "كود الأصل")}</th>
              <th style={{ padding: "0.75rem 1rem" }}>{t("fields.assetName", "اسم الأصل")}</th>
              <th style={{ padding: "0.75rem 1rem" }}>{t("fields.category", "التصنيف")}</th>
              <th style={{ padding: "0.75rem 1rem" }}>{t("fields.status", "الحالة")}</th>
              <th style={{ padding: "0.75rem 1rem" }}>{t("fields.acquisitionCost", "التكلفة التاريخية")}</th>
              <th style={{ padding: "0.75rem 1rem" }}>{t("fields.netBookValue", "صافي القيمة الدفترية")}</th>
              <th style={{ padding: "0.75rem 1rem" }}>{t("fields.location", "الموقع")}</th>
              <th style={{ padding: "0.75rem 1rem" }}>{t("fields.custodian", "العهدة")}</th>
              <th style={{ padding: "0.75rem 1rem" }}>{t("common.actions", "الإجراءات")}</th>
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
                <tr key={row.id} style={{ borderBottom: "1px solid #f1f5f9" }}>
                  <td style={{ padding: "0.75rem 1rem" }}>
                    <span style={{ fontWeight: 600, color: "#1e3a8a" }}>{row.assetCode}</span>
                  </td>
                  <td style={{ padding: "0.75rem 1rem" }}>
                    <div style={{ fontWeight: 600, color: "#0f172a" }}>{row.assetName}</div>
                    {row.serialNumber && (
                      <div style={{ fontSize: "0.75rem", color: "#64748b" }}>S/N: {row.serialNumber}</div>
                    )}
                  </td>
                  <td style={{ padding: "0.75rem 1rem" }}>{row.categoryName}</td>
                  <td style={{ padding: "0.75rem 1rem" }}>
                    <AssetLifecycleBadge state={row.lifecycleState} />
                  </td>
                  <td style={{ padding: "0.75rem 1rem" }}>
                    <span style={{ fontWeight: 500 }}>
                      {row.acquisitionCost.toLocaleString("ar-EG", { minimumFractionDigits: 2 })} {row.currency}
                    </span>
                  </td>
                  <td style={{ padding: "0.75rem 1rem" }}>
                    <span style={{ fontWeight: 600, color: "#16a34a" }}>
                      {row.netBookValue.toLocaleString("ar-EG", { minimumFractionDigits: 2 })} {row.currency}
                    </span>
                  </td>
                  <td style={{ padding: "0.75rem 1rem" }}>{row.locationName ?? "-"}</td>
                  <td style={{ padding: "0.75rem 1rem" }}>{row.custodianId ?? "-"}</td>
                  <td style={{ padding: "0.75rem 1rem" }}>
                    <div style={{ display: "flex", gap: "0.375rem" }}>
                      <Button
                        variant="secondary"
                        size="compact"
                        onClick={() => onSelectAsset(row)}
                        data-testid={`view-asset-${row.id}`}
                        title={t("drawer.viewDetails", "عرض الملف الشامل")}
                      >
                        <EyeIcon size={14} />
                      </Button>
                      {row.lifecycleState === "Disposed" ? (
                        <Button
                          variant="secondary"
                          size="compact"
                          onClick={() => onReinstateAsset(row)}
                          data-testid={`reinstate-asset-${row.id}`}
                          title={t("disposal.reinstate", "إعادة قيد")}
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
                            title={t("custody.transfer", "نقل العهدة")}
                          >
                            <ArrowRightLeftIcon size={14} />
                          </Button>
                          <Button
                            variant="secondary"
                            size="compact"
                            onClick={() => onReturnAsset(row)}
                            data-testid={`return-asset-${row.id}`}
                            title={t("custody.return", "إرجاع للمستودع")}
                          >
                            <CornerDownLeftIcon size={14} />
                          </Button>
                          <Button
                            variant="secondary"
                            size="compact"
                            onClick={() => onDisposeAsset(row)}
                            data-testid={`dispose-asset-${row.id}`}
                            title={t("disposal.dispose", "استبعاد / بيع")}
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

        {/* Pagination Bar */}
        {totalCount > pageSize && (
          <div
            style={{
              padding: "0.75rem 1rem",
              display: "flex",
              justifyContent: "space-between",
              alignItems: "center",
              borderTop: "1px solid #e2e8f0"
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
              currentPage={page}
              totalPages={Math.ceil(totalCount / pageSize)}
              onPageChange={(p) => setPage(p)}
            />
          </div>
        )}
      </div>
    </div>
  );
}

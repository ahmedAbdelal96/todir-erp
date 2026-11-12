import React, { useState, useEffect, useMemo } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Select, type SelectOption } from "../../../../components/primitives/select.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import {
  useFixedAssetCategoriesQuery,
  useFixedAssetLocationsQuery,
  useCreateAssetMutation
} from "../../hooks/use-fixed-assets-queries.ts";
import type { CreateFixedAssetRequest, AssetNature, DepreciationMethod } from "../../types.ts";

export interface NewAssetModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
}

export function NewAssetModal({
  isOpen,
  onClose,
  legalEntityId
}: NewAssetModalProps): React.JSX.Element {
  const { t } = useTranslation("fixedAssets");

  const categoriesQuery = useFixedAssetCategoriesQuery(legalEntityId);
  const locationsQuery = useFixedAssetLocationsQuery(legalEntityId);
  const createMutation = useCreateAssetMutation();

  const [assetName, setAssetName] = useState("");
  const [assetCode, setAssetCode] = useState("");
  const [categoryId, setCategoryId] = useState("");
  const [acquisitionDate, setAcquisitionDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [inServiceDate, setInServiceDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [acquisitionCost, setAcquisitionCost] = useState("");
  const [currency, setCurrency] = useState("EGP");
  const [salvageValue, setSalvageValue] = useState("0");
  const [usefulLifeMonths, setUsefulLifeMonths] = useState("60");
  const [depreciationMethod, setDepreciationMethod] = useState<DepreciationMethod>("StraightLine");
  const [serialNumber, setSerialNumber] = useState("");
  const [barcode, setBarcode] = useState("");
  const [modelNumber, setModelNumber] = useState("");
  const [manufacturer, setManufacturer] = useState("");
  const [locationId, setLocationId] = useState("");
  const [department, setDepartment] = useState("");
  const [custodianId, setCustodianId] = useState("");
  const [description, setDescription] = useState("");
  const [deedNumber, setDeedNumber] = useState("");
  const [plotNumber, setPlotNumber] = useState("");
  const [quantity, setQuantity] = useState("1");
  const [error, setError] = useState<string | null>(null);

  // Selected category data
  const selectedCategory = useMemo(() => {
    return categoriesQuery.data?.items?.find((c) => c.id === categoryId);
  }, [categoriesQuery.data?.items, categoryId]);

  // When category changes, auto-populate useful life & method
  useEffect(() => {
    if (selectedCategory) {
      if (selectedCategory.defaultUsefulLifeMonths) {
        setUsefulLifeMonths(String(selectedCategory.defaultUsefulLifeMonths));
      }
      if (selectedCategory.defaultDepreciationMethod) {
        setDepreciationMethod(selectedCategory.defaultDepreciationMethod);
      }
    }
  }, [selectedCategory]);

  const categoryOptions: SelectOption[] = useMemo(() => {
    return (
      categoriesQuery.data?.items?.map((cat) => ({
        value: cat.id,
        label: `${cat.categoryName} (${cat.nature})`
      })) ?? []
    );
  }, [categoriesQuery.data?.items]);

  const locationOptions: SelectOption[] = useMemo(() => {
    return (
      locationsQuery.data?.items?.map((loc) => ({
        value: loc.id,
        label: `${loc.locationName} - ${loc.building ?? ""} ${loc.floorNumber ? `(طابق ${loc.floorNumber})` : ""}`
      })) ?? []
    );
  }, [locationsQuery.data?.items]);

  const methodOptions: SelectOption[] = [
    { value: "StraightLine", label: t("depreciation.straightLine", "القسط الثابت (Straight Line)") },
    { value: "ReducingBalance", label: t("depreciation.reducingBalance", "الرصيد المتناقص (Reducing Balance)") },
    { value: "UnitsOfProduction", label: t("depreciation.unitsOfProduction", "وحدات الإنتاج (Units of Production)") },
    { value: "SumOfYearsDigits", label: t("depreciation.sumOfYearsDigits", "مجموع أرقام السنوات (Sum of Years Digits)") },
    { value: "None", label: t("depreciation.none", "بدون إهلاك (أراضي / CIP)") }
  ];

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    if (!assetName.trim()) {
      setError(t("validation.assetNameRequired", "اسم الأصل مطلوب"));
      return;
    }
    if (!categoryId) {
      setError(t("validation.categoryRequired", "يجب اختيار التصنيف المحاسبي"));
      return;
    }
    const cost = parseFloat(acquisitionCost);
    if (isNaN(cost) || cost < 0) {
      setError(t("validation.validCostRequired", "يجب إدخال تكلفة شراء صحيحة"));
      return;
    }

    try {
      const customAttributes: Record<string, unknown> = {};
      if (selectedCategory?.nature === "Land" || selectedCategory?.nature === "Building") {
        if (deedNumber) customAttributes.deedNumber = deedNumber;
        if (plotNumber) customAttributes.plotNumber = plotNumber;
      }
      if (selectedCategory?.nature === "Grouped") {
        customAttributes.quantity = parseInt(quantity, 10) || 1;
      }

      const payload: CreateFixedAssetRequest = {
        legalEntityId,
        categoryId,
        name: assetName.trim(),
        assetName: assetName.trim(),
        assetCode: assetCode.trim() || undefined,
        assetTag: barcode.trim() || undefined,
        description: description.trim() || undefined,
        acquisitionDate,
        inServiceDate: inServiceDate || acquisitionDate,
        placedInServiceDate: inServiceDate || acquisitionDate,
        acquisitionCost: cost,
        currency,
        salvageValue: parseFloat(salvageValue) || 0,
        usefulLifeMonths: parseInt(usefulLifeMonths, 10) || 0,
        depreciationMethod,
        serialNumber: serialNumber.trim() || undefined,
        barcode: barcode.trim() || undefined,
        modelNumber: modelNumber.trim() || undefined,
        manufacturer: manufacturer.trim() || undefined,
        locationId: locationId || undefined,
        department: department.trim() || undefined,
        custodianId: custodianId.trim() || undefined,
        customAttributes: Object.keys(customAttributes).length > 0 ? customAttributes : undefined
      };

      await createMutation.mutateAsync(payload as any);
      onClose();
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : t("common.unknownError", "فشل إنشاء الأصل");
      setError(msg);
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("register.newAssetModalTitle", "إضافة أصل رأسمالي جديد")}
      width="680px"
      data-testid="new-asset-modal"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {error && (
          <div
            data-testid="new-asset-error"
            style={{
              padding: "0.75rem",
              borderRadius: "0.375rem",
              backgroundColor: "#fef2f2",
              border: "1px solid #f87171",
              color: "#991b1b",
              fontSize: "0.875rem"
            }}
          >
            {error}
          </div>
        )}

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <TextField
            label={t("fields.assetName", "اسم الأصل")}
            value={assetName}
            onChange={(e) => setAssetName(e.target.value)}
            required
            data-testid="asset-name-input"
            placeholder={t("fields.assetNamePlaceholder", "مثال: حاسب محمول Dell Latitude 5540")}
          />
          <TextField
            label={t("fields.assetCode", "كود الأصل (اختياري / آلي)")}
            value={assetCode}
            onChange={(e) => setAssetCode(e.target.value)}
            data-testid="asset-code-input"
            placeholder={t("fields.assetCodePlaceholder", "FA-2026-0001")}
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <div>
            <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 500, marginBottom: "0.25rem", color: "#334155" }}>
              {t("fields.category", "التصنيف المحاسبي وطبيعة الأصل")} *
            </label>
            <Select
              options={categoryOptions}
              value={categoryId}
              onChange={(val) => setCategoryId(val)}
              placeholder={t("fields.selectCategory", "اختر التصنيف...")}
              data-testid="category-select"
            />
          </div>
          <div>
            <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 500, marginBottom: "0.25rem", color: "#334155" }}>
              {t("fields.location", "الموقع الجغرافي / المبنى")}
            </label>
            <Select
              options={locationOptions}
              value={locationId}
              onChange={(val) => setLocationId(val)}
              placeholder={t("fields.selectLocation", "اختر الموقع...")}
              data-testid="location-select"
            />
          </div>
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr", gap: "1rem" }}>
          <TextField
            label={t("fields.acquisitionCost", "تكلفة الاقتناء")}
            type="number"
            step="0.01"
            value={acquisitionCost}
            onChange={(e) => setAcquisitionCost(e.target.value)}
            required
            data-testid="cost-input"
            placeholder="0.00"
          />
          <TextField
            label={t("fields.currency", "العملة")}
            value={currency}
            onChange={(e) => setCurrency(e.target.value)}
            data-testid="currency-input"
          />
          <TextField
            label={t("fields.salvageValue", "قيمة الخردة المتبقية")}
            type="number"
            step="0.01"
            value={salvageValue}
            onChange={(e) => setSalvageValue(e.target.value)}
            data-testid="salvage-value-input"
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <TextField
            label={t("fields.acquisitionDate", "تاريخ الاقتناء")}
            type="date"
            value={acquisitionDate}
            onChange={(e) => setAcquisitionDate(e.target.value)}
            required
            data-testid="acquisition-date-input"
          />
          <TextField
            label={t("fields.inServiceDate", "تاريخ بدء التشغيل في الخدمة")}
            type="date"
            value={inServiceDate}
            onChange={(e) => setInServiceDate(e.target.value)}
            data-testid="in-service-date-input"
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <div>
            <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 500, marginBottom: "0.25rem", color: "#334155" }}>
              {t("fields.depreciationMethod", "طريقة الإهلاك المحاسبي")}
            </label>
            <Select
              options={methodOptions}
              value={depreciationMethod}
              onChange={(val) => setDepreciationMethod(val as DepreciationMethod)}
              data-testid="depreciation-method-select"
            />
          </div>
          <TextField
            label={t("fields.usefulLifeMonths", "العمر الإنتاجي (بالأشهر)")}
            type="number"
            value={usefulLifeMonths}
            onChange={(e) => setUsefulLifeMonths(e.target.value)}
            data-testid="useful-life-input"
          />
        </div>

        {/* Nature specific fields */}
        {(selectedCategory?.nature === "Land" || selectedCategory?.nature === "Building") && (
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem", backgroundColor: "#f8fafc", padding: "0.75rem", borderRadius: "0.375rem" }}>
            <TextField
              label={t("fields.deedNumber", "رقم صك الملكية / الوثيقة")}
              value={deedNumber}
              onChange={(e) => setDeedNumber(e.target.value)}
              data-testid="deed-number-input"
            />
            <TextField
              label={t("fields.plotNumber", "رقم القطعة / المخطط")}
              value={plotNumber}
              onChange={(e) => setPlotNumber(e.target.value)}
              data-testid="plot-number-input"
            />
          </div>
        )}

        {selectedCategory?.nature === "Grouped" && (
          <div style={{ backgroundColor: "#f8fafc", padding: "0.75rem", borderRadius: "0.375rem" }}>
            <TextField
              label={t("fields.groupedQuantity", "الكمية المجمعة للأصل")}
              type="number"
              value={quantity}
              onChange={(e) => setQuantity(e.target.value)}
              data-testid="grouped-quantity-input"
            />
          </div>
        )}

        {/* Tracking & Serial */}
        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <TextField
            label={t("fields.serialNumber", "الرقم التسلسلي (Serial Number)")}
            value={serialNumber}
            onChange={(e) => setSerialNumber(e.target.value)}
            data-testid="serial-number-input"
          />
          <TextField
            label={t("fields.barcode", "الباركود / رمز التتبع (Tag / Barcode)")}
            value={barcode}
            onChange={(e) => setBarcode(e.target.value)}
            data-testid="barcode-input"
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <TextField
            label={t("fields.department", "القسم / المركز المالي")}
            value={department}
            onChange={(e) => setDepartment(e.target.value)}
            data-testid="department-input"
          />
          <TextField
            label={t("fields.custodianId", "معرّف الموظف المسؤول (العهدة)")}
            value={custodianId}
            onChange={(e) => setCustodianId(e.target.value)}
            data-testid="custodian-id-input"
          />
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "0.5rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("common.cancel", "إلغاء")}
          </Button>
          <Button variant="primary" type="submit" isLoading={createMutation.isPending} data-testid="submit-asset-btn">
            {t("register.createButton", "حفظ وقيد الأصل")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

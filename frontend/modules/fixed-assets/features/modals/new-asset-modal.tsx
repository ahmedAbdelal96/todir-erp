import React, { useState, useEffect, useMemo } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Select, type SelectOption } from "../../../../components/primitives/select.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import {
  useFixedAssetCategoriesQuery,
  useFixedAssetLocationsQuery,
  useCreateAssetMutation,
  useAssetBooksQuery
} from "../../hooks/use-fixed-assets-queries.ts";
import { fixedAssetsApi } from "../../services/fixed-assets-api.ts";
import type { CreateAssetRequest, DepreciationMethod } from "../../types.ts";

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
  const booksQuery = useAssetBooksQuery(legalEntityId);
  const createMutation = useCreateAssetMutation();

  const [assetName, setAssetName] = useState("");
  const [assetCode, setAssetCode] = useState("");
  const [categoryId, setCategoryId] = useState("");
  const [acquisitionDate, setAcquisitionDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [inServiceDate, setInServiceDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [acquisitionCost, setAcquisitionCost] = useState("");
  const [currency, setCurrency] = useState("SAR");
  const [salvageValue, setSalvageValue] = useState("0");
  const [usefulLifeMonths, setUsefulLifeMonths] = useState("60");
  const [depreciationMethod, setDepreciationMethod] = useState<DepreciationMethod>("StraightLine");
  const [serialNumber, setSerialNumber] = useState("");
  const [barcode, setBarcode] = useState("");
  const [locationId, setLocationId] = useState("");
  const [department, setDepartment] = useState("");
  const [custodianId, setCustodianId] = useState("");
  const [description, setDescription] = useState("");
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
        id: cat.id,
        label: `${cat.categoryName} (${cat.nature})`
      })) ?? []
    );
  }, [categoriesQuery.data?.items]);

  const locationOptions: SelectOption[] = useMemo(() => {
    return (
      locationsQuery.data?.items?.map((loc) => ({
        id: loc.id,
        label: `${loc.locationName} (${loc.locationCode})`
      })) ?? []
    );
  }, [locationsQuery.data?.items]);

  const methodOptions: SelectOption[] = [
    { id: "StraightLine", label: t("depreciation.straightLine", "القسط الثابت (Straight Line)") },
    { id: "NoDepreciation", label: t("depreciation.none", "بدون إهلاك (أراضي / CIP)") }
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
      const payload: CreateAssetRequest = {
        legalEntityId,
        categoryId,
        name: assetName.trim(),
        description: description.trim() || undefined,
        assetTag: barcode.trim() || undefined,
        productVariantRef: assetCode.trim() || undefined,
        inventorySerialRef: serialNumber.trim() || undefined,
        acquisitionDate: acquisitionDate || undefined,
        capitalizationDate: inServiceDate || acquisitionDate || undefined,
        placedInServiceDate: inServiceDate || acquisitionDate || undefined,
        quantity: parseInt(quantity, 10) || 1
      };

      const asset = await createMutation.mutateAsync(payload);

      // If initial cost is provided, attach to primary book
      const books = booksQuery.data ?? [];
      const primaryBook = books[0];
      if (primaryBook && cost > 0) {
        try {
          await fixedAssetsApi.addAssetBook(asset.id, {
            legalEntityId,
            bookDefinitionId: primaryBook.id,
            originalCost: cost,
            residualValue: parseFloat(salvageValue) || 0,
            usefulLifeMonths: parseInt(usefulLifeMonths, 10) || 60,
            depreciationMethod: depreciationMethod,
            placedInServiceDate: inServiceDate || acquisitionDate
          });
        } catch {
          // Non-blocking if asset was created
        }
      }

      // If custody assignment is selected
      if (custodianId.trim() || locationId) {
        try {
          await fixedAssetsApi.assignAsset(asset.id, {
            assignmentType: custodianId.trim() ? "Employee" : "Location",
            employeeId: custodianId.trim() || undefined,
            departmentId: department.trim() || undefined,
            locationId: locationId || undefined,
            effectiveFrom: acquisitionDate || new Date().toISOString().slice(0, 10)
          });
        } catch {
          // Non-blocking
        }
      }

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
      size="large"
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
            onChange={setAssetName}
            isRequired
            data-testid="asset-name-input"
            placeholder={t("fields.assetNamePlaceholder", "مثال: حاسب محمول Dell Latitude 5540")}
          />
          <TextField
            label={t("fields.assetCode", "كود الأصل (اختياري / آلي)")}
            value={assetCode}
            onChange={setAssetCode}
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
              selectedKey={categoryId || null}
              onSelectionChange={(k) => setCategoryId(String(k ?? ""))}
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
              selectedKey={locationId || null}
              onSelectionChange={(k) => setLocationId(String(k ?? ""))}
              placeholder={t("fields.selectLocation", "اختر الموقع...")}
              data-testid="location-select"
            />
          </div>
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr", gap: "1rem" }}>
          <TextField
            label={t("fields.acquisitionCost", "تكلفة الاقتناء")}
            type="number"
            value={acquisitionCost}
            onChange={setAcquisitionCost}
            isRequired
            data-testid="cost-input"
            placeholder="0.00"
          />
          <TextField
            label={t("fields.currency", "العملة")}
            value={currency}
            onChange={setCurrency}
            data-testid="currency-input"
          />
          <TextField
            label={t("fields.salvageValue", "قيمة الخردة المتبقية")}
            type="number"
            value={salvageValue}
            onChange={setSalvageValue}
            data-testid="salvage-value-input"
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <TextField
            label={t("fields.acquisitionDate", "تاريخ الاقتناء")}
            type="date"
            value={acquisitionDate}
            onChange={setAcquisitionDate}
            isRequired
            data-testid="acquisition-date-input"
          />
          <TextField
            label={t("fields.inServiceDate", "تاريخ بدء التشغيل في الخدمة")}
            type="date"
            value={inServiceDate}
            onChange={setInServiceDate}
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
              selectedKey={depreciationMethod}
              onSelectionChange={(k) => setDepreciationMethod(String(k ?? "StraightLine") as DepreciationMethod)}
              data-testid="depreciation-method-select"
            />
          </div>
          <TextField
            label={t("fields.usefulLifeMonths", "العمر الإنتاجي (بالأشهر)")}
            type="number"
            value={usefulLifeMonths}
            onChange={setUsefulLifeMonths}
            data-testid="useful-life-input"
          />
        </div>

        {/* Tracking & Serial */}
        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <TextField
            label={t("fields.serialNumber", "الرقم التسلسلي (Serial Number)")}
            value={serialNumber}
            onChange={setSerialNumber}
            data-testid="serial-number-input"
          />
          <TextField
            label={t("fields.barcode", "الباركود / رمز التتبع (Tag / Barcode)")}
            value={barcode}
            onChange={setBarcode}
            data-testid="barcode-input"
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <TextField
            label={t("fields.department", "القسم / المركز المالي")}
            value={department}
            onChange={setDepartment}
            data-testid="department-input"
          />
          <TextField
            label={t("fields.custodianId", "معرّف الموظف المسؤول (العهدة)")}
            value={custodianId}
            onChange={setCustodianId}
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

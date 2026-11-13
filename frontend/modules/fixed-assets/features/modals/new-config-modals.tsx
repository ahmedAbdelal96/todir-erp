import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Select, type SelectOption } from "../../../../components/primitives/select.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import {
  useCreateLocationMutation,
  useCreateCategoryMutation,
  useCreateBookMutation
} from "../../hooks/use-fixed-assets-queries.ts";
import type {
  CreateAssetLocationRequest,
  CreateAssetCategoryRequest,
  CreateBookRequest,
  AssetNature,
  DepreciationMethod,
  TrackingGranularity,
  LocationType
} from "../../types.ts";

export interface NewLocationModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
}

export function NewLocationModal({
  isOpen,
  onClose,
  legalEntityId
}: NewLocationModalProps): React.JSX.Element {
  const { t } = useTranslation("fixedAssets");
  const createLocMutation = useCreateLocationMutation();

  const [locationName, setLocationName] = useState("");
  const [locationCode, setLocationCode] = useState("");
  const [locationType, setLocationType] = useState<LocationType>("Site");
  const [error, setError] = useState<string | null>(null);

  const locationTypeOptions: SelectOption[] = [
    { id: "Site", label: t("locations.typeSite", "موقع رئيسي / فرع (Site)") },
    { id: "Building", label: t("locations.typeBuilding", "مبنى (Building)") },
    { id: "Floor", label: t("locations.typeFloor", "طابق / دور (Floor)") },
    { id: "Room", label: t("locations.typeRoom", "غرفة / مكتب (Room)") },
    { id: "Vehicle", label: t("locations.typeVehicle", "مركبة / أصل متنقل (Vehicle)") },
    { id: "Other", label: t("locations.typeOther", "موقع آخر (Other)") }
  ];

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    if (!locationName.trim()) {
      setError(t("validation.locationNameRequired", "اسم الموقع الجغرافي مطلوب"));
      return;
    }

    try {
      const code = locationCode.trim() || `LOC-${Date.now().toString(36).toUpperCase()}`;
      const payload: CreateAssetLocationRequest = {
        legalEntityId,
        code,
        name: locationName.trim(),
        locationType
      };

      await createLocMutation.mutateAsync(payload);
      onClose();
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : t("common.unknownError", "فشل إضافة الموقع");
      setError(msg);
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("custody.newLocationModalTitle", "إضافة موقع ومبنى جديد")}
      size="medium"
      data-testid="new-location-modal"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {error && (
          <div data-testid="loc-error" style={{ padding: "0.75rem", backgroundColor: "#fef2f2", color: "#991b1b", borderRadius: "0.375rem" }}>
            {error}
          </div>
        )}

        <TextField
          label={t("fields.locationName", "اسم الموقع / المستودع")}
          value={locationName}
          onChange={setLocationName}
          isRequired
          placeholder={t("fields.locNamePlaceholder", "المبنى الإداري الرئيسي - التجمع")}
          data-testid="loc-name-input"
        />

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <TextField
            label={t("fields.locationCode", "كود الموقع")}
            value={locationCode}
            onChange={setLocationCode}
            placeholder={t("fields.locCodePlaceholder", "LOC-HQ-01")}
            data-testid="loc-code-input"
          />

          <div>
            <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 500, marginBottom: "0.25rem", color: "#334155" }}>
              {t("fields.locationType", "نوع الموقع")}
            </label>
            <Select
              options={locationTypeOptions}
              selectedKey={locationType}
              onSelectionChange={(k) => setLocationType(String(k ?? "Site") as LocationType)}
              data-testid="loc-type-select"
            />
          </div>
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "0.5rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("common.cancel", "إلغاء")}
          </Button>
          <Button variant="primary" type="submit" isLoading={createLocMutation.isPending} data-testid="submit-loc-btn">
            {t("common.save", "حفظ الموقع")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

export interface NewCategoryModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
}

export function NewCategoryModal({
  isOpen,
  onClose,
  legalEntityId: _legalEntityId
}: NewCategoryModalProps): React.JSX.Element {
  const { t } = useTranslation("fixedAssets");
  const createCatMutation = useCreateCategoryMutation();

  const [categoryName, setCategoryName] = useState("");
  const [categoryCode, setCategoryCode] = useState("");
  const [nature, setNature] = useState<AssetNature>("Equipment");
  const [granularity, setGranularity] = useState<TrackingGranularity>("Individual");
  const [defaultMethod, setDefaultMethod] = useState<DepreciationMethod>("StraightLine");
  const [defaultUsefulLifeMonths, setDefaultUsefulLifeMonths] = useState("60");
  const [error, setError] = useState<string | null>(null);

  const natureOptions: SelectOption[] = [
    { id: "Equipment", label: t("nature.equipment", "آلات ومعدات وأجهزة (Equipment)") },
    { id: "Vehicle", label: t("nature.vehicle", "مركبات وسيارات (Vehicle)") },
    { id: "Building", label: t("nature.building", "مباني ومنشآت (Building)") },
    { id: "Land", label: t("nature.land", "أراضي (Land - لا تهلك)") },
    { id: "CIP", label: t("nature.cip", "مشروعات قيد التنفيذ (CIP)") },
    { id: "Furniture", label: t("nature.furniture", "أثاث وتجهيزات مكتبية (Furniture)") },
    { id: "Intangible", label: t("nature.intangible", "أصول غير ملموسة / برمجيات (Intangible)") }
  ];

  const granularityOptions: SelectOption[] = [
    { id: "Individual", label: t("granularity.individual", "تتبع إفرادي مستقل (Individual)") },
    { id: "Grouped", label: t("granularity.grouped", "أصول مجمعة بكمية (Grouped)") },
    { id: "Bulk", label: t("granularity.bulk", "مجمّع كمي بدون ترقيم (Bulk)") }
  ];

  const methodOptions: SelectOption[] = [
    { id: "StraightLine", label: t("depreciation.straightLine", "القسط الثابت (Straight Line)") },
    { id: "NoDepreciation", label: t("depreciation.none", "بدون إهلاك (No Depreciation)") }
  ];

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    if (!categoryName.trim()) {
      setError(t("validation.categoryNameRequired", "اسم التصنيف المحاسبي مطلوب"));
      return;
    }

    try {
      const code = categoryCode.trim() || `CAT-${Date.now().toString(36).toUpperCase()}`;
      const payload: CreateAssetCategoryRequest = {
        code,
        name: categoryName.trim(),
        assetNature: nature,
        trackingGranularity: granularity,
        requiresLocation: true,
        requiresCustody: true,
        requiresDepreciation: defaultMethod !== "NoDepreciation",
        defaultDepreciationMethod: defaultMethod,
        defaultUsefulLifeMonths: defaultMethod === "NoDepreciation" ? null : (parseInt(defaultUsefulLifeMonths, 10) || 60),
        defaultResidualValue: 0
      };

      await createCatMutation.mutateAsync(payload);
      onClose();
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : t("common.unknownError", "فشل إضافة التصنيف");
      setError(msg);
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("categories.newCategoryModalTitle", "إضافة تصنيف أصول جديد وشجرة الحسابات")}
      size="medium"
      data-testid="new-category-modal"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {error && (
          <div data-testid="cat-error" style={{ padding: "0.75rem", backgroundColor: "#fef2f2", color: "#991b1b", borderRadius: "0.375rem" }}>
            {error}
          </div>
        )}

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <TextField
            label={t("fields.categoryName", "اسم التصنيف")}
            value={categoryName}
            onChange={setCategoryName}
            isRequired
            placeholder={t("fields.catNamePlaceholder", "أجهزة حاسب آلي ومحمول")}
            data-testid="cat-name-input"
          />
          <TextField
            label={t("fields.categoryCode", "كود التصنيف")}
            value={categoryCode}
            onChange={setCategoryCode}
            placeholder={t("fields.catCodePlaceholder", "CAT-IT-01")}
            data-testid="cat-code-input"
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <div>
            <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 500, marginBottom: "0.25rem", color: "#334155" }}>
              {t("fields.nature", "طبيعة الأصل المحاسبية")} *
            </label>
            <Select
              options={natureOptions}
              selectedKey={nature}
              onSelectionChange={(k) => setNature(String(k ?? "Equipment") as AssetNature)}
              data-testid="cat-nature-select"
            />
          </div>
          <div>
            <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 500, marginBottom: "0.25rem", color: "#334155" }}>
              {t("fields.granularity", "مستوى التتبع")} *
            </label>
            <Select
              options={granularityOptions}
              selectedKey={granularity}
              onSelectionChange={(k) => setGranularity(String(k ?? "Individual") as TrackingGranularity)}
              data-testid="cat-granularity-select"
            />
          </div>
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <div>
            <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 500, marginBottom: "0.25rem", color: "#334155" }}>
              {t("fields.defaultMethod", "طريقة الإهلاك الافتراضية")}
            </label>
            <Select
              options={methodOptions}
              selectedKey={defaultMethod}
              onSelectionChange={(k) => setDefaultMethod(String(k ?? "StraightLine") as DepreciationMethod)}
              data-testid="cat-method-select"
            />
          </div>

          <TextField
            label={t("fields.defaultUsefulLifeMonths", "العمر الإنتاجي الافتراضي (أشهر)")}
            type="number"
            value={defaultUsefulLifeMonths}
            onChange={setDefaultUsefulLifeMonths}
            data-testid="cat-life-input"
          />
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "0.5rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("common.cancel", "إلغاء")}
          </Button>
          <Button variant="primary" type="submit" isLoading={createCatMutation.isPending} data-testid="submit-cat-btn">
            {t("common.save", "حفظ التصنيف")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

export interface NewBookModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
}

export function NewBookModal({
  isOpen,
  onClose,
  legalEntityId
}: NewBookModalProps): React.JSX.Element {
  const { t } = useTranslation("fixedAssets");
  const createBookMutation = useCreateBookMutation();

  const [bookCode, setBookCode] = useState("");
  const [bookName, setBookName] = useState("");
  const [currencyCode, setCurrencyCode] = useState("SAR");
  const [postsToLedger, setPostsToLedger] = useState(false);
  const [defaultMethod, setDefaultMethod] = useState<DepreciationMethod>("StraightLine");
  const [error, setError] = useState<string | null>(null);

  const methodOptions: SelectOption[] = [
    { id: "StraightLine", label: t("depreciation.straightLine", "القسط الثابت (Straight Line)") },
    { id: "NoDepreciation", label: t("depreciation.none", "بدون إهلاك (No Depreciation)") }
  ];

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    if (!bookCode.trim() || !bookName.trim()) {
      setError(t("validation.bookCodeAndNameRequired", "كود واسم الدفتر مطلوبان"));
      return;
    }

    try {
      const payload: CreateBookRequest = {
        legalEntityId,
        code: bookCode.trim().toUpperCase(),
        name: bookName.trim(),
        postsToLedger,
        currencyCode: currencyCode.trim().toUpperCase() || "SAR",
        defaultDepreciationMethod: defaultMethod,
        defaultConvention: "FullMonth",
        defaultUsefulLifeMonths: 60
      };

      await createBookMutation.mutateAsync(payload);
      onClose();
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : t("common.unknownError", "فشل إضافة الدفتر");
      setError(msg);
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("books.newBookModalTitle", "إضافة دفتر أصول إضافي (مالي / ضريبي)")}
      size="medium"
      data-testid="new-book-modal"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {error && (
          <div data-testid="book-error" style={{ padding: "0.75rem", backgroundColor: "#fef2f2", color: "#991b1b", borderRadius: "0.375rem" }}>
            {error}
          </div>
        )}

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1.5fr", gap: "1rem" }}>
          <TextField
            label={t("fields.bookCode", "كود الدفتر")}
            value={bookCode}
            onChange={setBookCode}
            isRequired
            placeholder={t("fields.bookCodePlaceholder", "TAX-01")}
            data-testid="book-code-input"
          />
          <TextField
            label={t("fields.bookName", "اسم الدفتر")}
            value={bookName}
            onChange={setBookName}
            isRequired
            placeholder={t("fields.bookNamePlaceholder", "الدفتر الضريبي")}
            data-testid="book-name-input"
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <TextField
            label={t("fields.currency", "عملة الدفتر")}
            value={currencyCode}
            onChange={setCurrencyCode}
            placeholder={t("fields.currencyPlaceholder", "SAR")}
            data-testid="book-currency-input"
          />

          <div>
            <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 500, marginBottom: "0.25rem", color: "#334155" }}>
              {t("fields.defaultMethod", "طريقة الإهلاك")}
            </label>
            <Select
              options={methodOptions}
              selectedKey={defaultMethod}
              onSelectionChange={(k) => setDefaultMethod(String(k ?? "StraightLine") as DepreciationMethod)}
              data-testid="book-method-select"
            />
          </div>
        </div>

        <label style={{ display: "flex", alignItems: "center", gap: "0.5rem", fontSize: "0.875rem", cursor: "pointer" }}>
          <input
            type="checkbox"
            checked={postsToLedger}
            onChange={(e) => setPostsToLedger(e.target.checked)}
            data-testid="book-posts-ledger-checkbox"
          />
          <span>{t("books.postsToLedgerDesc", "يرحل آلياً إلى دفتر الأستاذ العام (Posting to GL)")}</span>
        </label>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "0.5rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("common.cancel", "إلغاء")}
          </Button>
          <Button variant="primary" type="submit" isLoading={createBookMutation.isPending} data-testid="submit-book-btn">
            {t("common.save", "حفظ الدفتر")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

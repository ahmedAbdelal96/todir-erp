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
  CreateAssetBookRequest,
  AssetNature,
  DepreciationMethod,
  BookType
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
  const [building, setBuilding] = useState("");
  const [floorNumber, setFloorNumber] = useState("");
  const [roomNumber, setRoomNumber] = useState("");
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    if (!locationName.trim()) {
      setError(t("validation.locationNameRequired", "اسم الموقع الجغرافي مطلوب"));
      return;
    }

    try {
      const payload: CreateAssetLocationRequest = {
        legalEntityId,
        locationName: locationName.trim(),
        locationCode: locationCode.trim() || undefined,
        building: building.trim() || undefined,
        floorNumber: floorNumber.trim() || undefined,
        roomNumber: roomNumber.trim() || undefined
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
      width="500px"
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
          onChange={(e) => setLocationName(e.target.value)}
          required
          placeholder={t("fields.locNamePlaceholder", "المبنى الإداري الرئيسي - التجمع")}
          data-testid="loc-name-input"
        />

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <TextField
            label={t("fields.locationCode", "كود الموقع")}
            value={locationCode}
            onChange={(e) => setLocationCode(e.target.value)}
            placeholder="LOC-HQ-01"
            data-testid="loc-code-input"
          />
          <TextField
            label={t("fields.building", "المبنى / الجناح")}
            value={building}
            onChange={(e) => setBuilding(e.target.value)}
            placeholder="مبنى B"
            data-testid="loc-building-input"
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <TextField
            label={t("fields.floorNumber", "الدور / الطابق")}
            value={floorNumber}
            onChange={(e) => setFloorNumber(e.target.value)}
            placeholder="الطابق 3"
            data-testid="loc-floor-input"
          />
          <TextField
            label={t("fields.roomNumber", "رقم الغرفة")}
            value={roomNumber}
            onChange={(e) => setRoomNumber(e.target.value)}
            placeholder="غرفة 304"
            data-testid="loc-room-input"
          />
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
  legalEntityId
}: NewCategoryModalProps): React.JSX.Element {
  const { t } = useTranslation("fixedAssets");
  const createCatMutation = useCreateCategoryMutation();

  const [categoryName, setCategoryName] = useState("");
  const [categoryCode, setCategoryCode] = useState("");
  const [nature, setNature] = useState<AssetNature>("Equipment");
  const [defaultMethod, setDefaultMethod] = useState<DepreciationMethod>("StraightLine");
  const [defaultUsefulLifeMonths, setDefaultUsefulLifeMonths] = useState("60");
  const [assetGlAccountId, setAssetGlAccountId] = useState("");
  const [accDepGlAccountId, setAccDepGlAccountId] = useState("");
  const [depExpGlAccountId, setDepExpGlAccountId] = useState("");
  const [error, setError] = useState<string | null>(null);

  const natureOptions: SelectOption[] = [
    { value: "Equipment", label: t("nature.equipment", "آلات ومعدات وأجهزة (Equipment)") },
    { value: "Vehicle", label: t("nature.vehicle", "مركبات وسيارات (Vehicle)") },
    { value: "Building", label: t("nature.building", "مباني ومنشآت (Building)") },
    { value: "Land", label: t("nature.land", "أراضي (Land - لا تهلك)") },
    { value: "CIP", label: t("nature.cip", "مشروعات قيد التنفيذ (CIP)") },
    { value: "Grouped", label: t("nature.grouped", "أصول مجمعة / أثاث (Grouped)") },
    { value: "Intangible", label: t("nature.intangible", "أصول غير ملموسة / برمجيات (Intangible)") }
  ];

  const methodOptions: SelectOption[] = [
    { value: "StraightLine", label: t("depreciation.straightLine", "القسط الثابت (Straight Line)") },
    { value: "ReducingBalance", label: t("depreciation.reducingBalance", "الرصيد المتناقص (Reducing Balance)") },
    { value: "None", label: t("depreciation.none", "بدون إهلاك") }
  ];

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    if (!categoryName.trim()) {
      setError(t("validation.categoryNameRequired", "اسم التصنيف المحاسبي مطلوب"));
      return;
    }

    try {
      const payload: CreateAssetCategoryRequest = {
        legalEntityId,
        categoryName: categoryName.trim(),
        categoryCode: categoryCode.trim() || undefined,
        nature,
        defaultDepreciationMethod: defaultMethod,
        defaultUsefulLifeMonths: parseInt(defaultUsefulLifeMonths, 10) || 60,
        assetGlAccountId: assetGlAccountId.trim() || undefined,
        accumulatedDepreciationGlAccountId: accDepGlAccountId.trim() || undefined,
        depreciationExpenseGlAccountId: depExpGlAccountId.trim() || undefined
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
      width="580px"
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
            onChange={(e) => setCategoryName(e.target.value)}
            required
            placeholder={t("fields.catNamePlaceholder", "أجهزة حاسب آلي ومحمول")}
            data-testid="cat-name-input"
          />
          <TextField
            label={t("fields.categoryCode", "كود التصنيف")}
            value={categoryCode}
            onChange={(e) => setCategoryCode(e.target.value)}
            placeholder="CAT-IT-01"
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
              value={nature}
              onChange={(val) => setNature(val as AssetNature)}
              data-testid="cat-nature-select"
            />
          </div>
          <div>
            <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 500, marginBottom: "0.25rem", color: "#334155" }}>
              {t("fields.defaultMethod", "طريقة الإهلاك الافتراضية")}
            </label>
            <Select
              options={methodOptions}
              value={defaultMethod}
              onChange={(val) => setDefaultMethod(val as DepreciationMethod)}
              data-testid="cat-method-select"
            />
          </div>
        </div>

        <TextField
          label={t("fields.defaultUsefulLifeMonths", "العمر الإنتاجي الافتراضي (أشهر)")}
          type="number"
          value={defaultUsefulLifeMonths}
          onChange={(e) => setDefaultUsefulLifeMonths(e.target.value)}
          data-testid="cat-life-input"
        />

        <div style={{ padding: "0.5rem 0", borderTop: "1px solid #e2e8f0" }}>
          <span style={{ fontSize: "0.8125rem", fontWeight: 600, color: "#475569" }}>
            {t("categories.glAccountsHeading", "ربط الحسابات بدفتر الأستاذ العام (Chart of Accounts):")}
          </span>
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr", gap: "0.75rem" }}>
          <TextField
            label={t("fields.assetGlAccount", "حساب الأصل (150xxx)")}
            value={assetGlAccountId}
            onChange={(e) => setAssetGlAccountId(e.target.value)}
            placeholder="150100"
            data-testid="cat-asset-gl-input"
          />
          <TextField
            label={t("fields.accDepGlAccount", "مجمع الإهلاك (159xxx)")}
            value={accDepGlAccountId}
            onChange={(e) => setAccDepGlAccountId(e.target.value)}
            placeholder="159100"
            data-testid="cat-accdep-gl-input"
          />
          <TextField
            label={t("fields.depExpGlAccount", "مصروف الإهلاك (520xxx)")}
            value={depExpGlAccountId}
            onChange={(e) => setDepExpGlAccountId(e.target.value)}
            placeholder="520100"
            data-testid="cat-depexp-gl-input"
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
  const [bookType, setBookType] = useState<BookType>("Tax");
  const [postsToLedger, setPostsToLedger] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const typeOptions: SelectOption[] = [
    { value: "Corporate", label: t("books.typeCorporate", "دفتر مالي وشركات (Corporate / IFRS)") },
    { value: "Tax", label: t("books.typeTax", "دفتر ضريبي (Tax Book)") },
    { value: "Management", label: t("books.typeManagement", "دفتر إداري / تكاليف (Management)") }
  ];

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    if (!bookCode.trim() || !bookName.trim()) {
      setError(t("validation.bookCodeAndNameRequired", "كود واسم الدفتر مطلوبان"));
      return;
    }

    try {
      const payload: CreateAssetBookRequest = {
        legalEntityId,
        bookCode: bookCode.trim(),
        bookName: bookName.trim(),
        bookType,
        currency: "EGP",
        postsToLedger
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
      width="500px"
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
            onChange={(e) => setBookCode(e.target.value)}
            required
            placeholder="TAX-EGY"
            data-testid="book-code-input"
          />
          <TextField
            label={t("fields.bookName", "اسم الدفتر")}
            value={bookName}
            onChange={(e) => setBookName(e.target.value)}
            required
            placeholder={t("fields.bookNamePlaceholder", "الدفتر الضريبي - مصر")}
            data-testid="book-name-input"
          />
        </div>

        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 500, marginBottom: "0.25rem", color: "#334155" }}>
            {t("fields.bookType", "نوع الدفتر")}
          </label>
          <Select
            options={typeOptions}
            value={bookType}
            onChange={(val) => setBookType(val as BookType)}
            data-testid="book-type-select"
          />
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

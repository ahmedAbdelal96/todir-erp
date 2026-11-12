import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Select, type SelectOption } from "../../../../components/primitives/select.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import {
  useFixedAssetLocationsQuery,
  useCreateVerificationCampaignMutation,
  useRecordAuditFindingMutation
} from "../../hooks/use-fixed-assets-queries.ts";
import type {
  CreateVerificationCampaignRequest,
  RecordAuditFindingRequest,
  AuditFindingStatus
} from "../../types.ts";

export interface NewVerificationCampaignModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
}

export function NewVerificationCampaignModal({
  isOpen,
  onClose,
  legalEntityId
}: NewVerificationCampaignModalProps): React.JSX.Element {
  const { t } = useTranslation("fixedAssets");
  const locationsQuery = useFixedAssetLocationsQuery(legalEntityId);
  const createCampaignMutation = useCreateVerificationCampaignMutation(legalEntityId);

  const [campaignName, setCampaignName] = useState("");
  const [scope, setScope] = useState("FullAudit");
  const [targetLocationId, setTargetLocationId] = useState("");
  const [plannedStartDate, setPlannedStartDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [plannedEndDate, setPlannedEndDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [error, setError] = useState<string | null>(null);

  const locationOptions: SelectOption[] =
    locationsQuery.data?.items?.map((loc) => ({
      value: loc.id,
      label: `${loc.locationName} (${loc.building ?? ""})`
    })) ?? [];

  const scopeOptions: SelectOption[] = [
    { value: "FullAudit", label: t("custody.scopeFull", "جرد شامل لكافة الأصول") },
    { value: "LocationSpecific", label: t("custody.scopeLocation", "جرد مخصص لموقع محدد") },
    { value: "CategorySpecific", label: t("custody.scopeCategory", "جرد تصنيف محدد (مثال: أجهزة وحواسيب)") },
    { value: "SampleAudit", label: t("custody.scopeSample", "جرد عشوائي للتحقق الدوري") }
  ];

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    if (!campaignName.trim()) {
      setError(t("validation.campaignNameRequired", "اسم حملة الجرد مطلوب"));
      return;
    }

    try {
      const payload: CreateVerificationCampaignRequest = {
        campaignName: campaignName.trim(),
        scope,
        targetLocationId: targetLocationId || undefined,
        plannedStartDate,
        plannedEndDate
      };

      await createCampaignMutation.mutateAsync(payload);
      onClose();
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : t("common.unknownError", "فشل إطلاق حملة الجرد");
      setError(msg);
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("custody.newCampaignModalTitle", "إنشاء حملة جرد فعلي وتدقيق ميداني")}
      width="540px"
      data-testid="new-campaign-modal"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {error && (
          <div data-testid="campaign-error" style={{ padding: "0.75rem", backgroundColor: "#fef2f2", color: "#991b1b", borderRadius: "0.375rem" }}>
            {error}
          </div>
        )}

        <TextField
          label={t("fields.campaignName", "اسم الحملة")}
          value={campaignName}
          onChange={(e) => setCampaignName(e.target.value)}
          required
          placeholder={t("fields.campaignNamePlaceholder", "جرد نهاية السنة المالية 2026 - المقر الرئيسي")}
          data-testid="campaign-name-input"
        />

        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 500, marginBottom: "0.25rem", color: "#334155" }}>
            {t("fields.auditScope", "نطاق الجرد")}
          </label>
          <Select
            options={scopeOptions}
            value={scope}
            onChange={(val) => setScope(val)}
            data-testid="campaign-scope-select"
          />
        </div>

        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 500, marginBottom: "0.25rem", color: "#334155" }}>
            {t("fields.targetLocation", "الموقع المستهدف للجرد (اختياري)")}
          </label>
          <Select
            options={locationOptions}
            value={targetLocationId}
            onChange={(val) => setTargetLocationId(val)}
            placeholder={t("fields.selectLocation", "كافة المواقع أو اختر موقع...")}
            data-testid="campaign-location-select"
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <TextField
            label={t("fields.plannedStartDate", "تاريخ البدء المخطط")}
            type="date"
            value={plannedStartDate}
            onChange={(e) => setPlannedStartDate(e.target.value)}
            required
            data-testid="planned-start-date-input"
          />
          <TextField
            label={t("fields.plannedEndDate", "تاريخ الانتهاء المخطط")}
            type="date"
            value={plannedEndDate}
            onChange={(e) => setPlannedEndDate(e.target.value)}
            required
            data-testid="planned-end-date-input"
          />
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "0.5rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("common.cancel", "إلغاء")}
          </Button>
          <Button variant="primary" type="submit" isLoading={createCampaignMutation.isPending} data-testid="submit-campaign-btn">
            {t("custody.launchCampaign", "بدء حملة الجرد")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

export interface RecordAuditFindingModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly campaignId: string;
  readonly defaultAssetId?: string;
}

export function RecordAuditFindingModal({
  isOpen,
  onClose,
  legalEntityId,
  campaignId,
  defaultAssetId
}: RecordAuditFindingModalProps): React.JSX.Element {
  const { t } = useTranslation("fixedAssets");
  const locationsQuery = useFixedAssetLocationsQuery(legalEntityId);
  const recordFindingMutation = useRecordAuditFindingMutation(legalEntityId);

  const [assetId, setAssetId] = useState(defaultAssetId ?? "");
  const [barcodeOrTag, setBarcodeOrTag] = useState("");
  const [findingStatus, setFindingStatus] = useState<AuditFindingStatus>("Found");
  const [actualLocationId, setActualLocationId] = useState("");
  const [notes, setNotes] = useState("");
  const [error, setError] = useState<string | null>(null);

  React.useEffect(() => {
    if (defaultAssetId) {
      setAssetId(defaultAssetId);
    }
  }, [defaultAssetId]);

  const locationOptions: SelectOption[] =
    locationsQuery.data?.items?.map((loc) => ({
      value: loc.id,
      label: `${loc.locationName} (${loc.building ?? ""})`
    })) ?? [];

  const statusOptions: SelectOption[] = [
    { value: "Found", label: t("custody.findingFound", "مطابق وموجود (Found)") },
    { value: "WrongLocation", label: t("custody.findingWrongLocation", "موجود في موقع غير مطابق (Wrong Location)") },
    { value: "Damaged", label: t("custody.findingDamaged", "تالف / بحاجة لصيانة (Damaged)") },
    { value: "Missing", label: t("custody.findingMissing", "مفقود وغير موجود (Missing)") }
  ];

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    if (!assetId.trim() && !barcodeOrTag.trim()) {
      setError(t("validation.assetOrBarcodeRequired", "يجب إدخال معرّف الأصل أو مسح الباركود"));
      return;
    }

    try {
      const payload: RecordAuditFindingRequest = {
        assetId: assetId.trim() || undefined,
        barcodeOrTag: barcodeOrTag.trim() || undefined,
        status: findingStatus,
        actualLocationId: actualLocationId || undefined,
        notes: notes.trim() || undefined
      };

      await recordFindingMutation.mutateAsync({ campaignId, payload });
      onClose();
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : t("common.unknownError", "فشل تسجيل نتيجة الفحص");
      setError(msg);
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("custody.recordFindingModalTitle", "تسجيل نتيجة فحص ميداني / مسح باركود")}
      width="520px"
      data-testid="record-finding-modal"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {error && (
          <div data-testid="finding-error" style={{ padding: "0.75rem", backgroundColor: "#fef2f2", color: "#991b1b", borderRadius: "0.375rem" }}>
            {error}
          </div>
        )}

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <TextField
            label={t("fields.assetId", "معرّف أو كود الأصل")}
            value={assetId}
            onChange={(e) => setAssetId(e.target.value)}
            placeholder="UUID / FA-CODE"
            data-testid="finding-asset-id-input"
          />
          <TextField
            label={t("fields.barcodeOrTag", "الباركود المقروء (Scan Tag)")}
            value={barcodeOrTag}
            onChange={(e) => setBarcodeOrTag(e.target.value)}
            placeholder="TAG-98242"
            data-testid="finding-barcode-input"
          />
        </div>

        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 500, marginBottom: "0.25rem", color: "#334155" }}>
            {t("fields.auditFindingStatus", "حالة الأصل في الموقع")} *
          </label>
          <Select
            options={statusOptions}
            value={findingStatus}
            onChange={(val) => setFindingStatus(val as AuditFindingStatus)}
            data-testid="finding-status-select"
          />
        </div>

        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 500, marginBottom: "0.25rem", color: "#334155" }}>
            {t("fields.actualLocation", "الموقع الفعلي الذي وجد فيه الأصل")}
          </label>
          <Select
            options={locationOptions}
            value={actualLocationId}
            onChange={(val) => setActualLocationId(val)}
            placeholder={t("fields.selectLocation", "اختر الموقع الفعلي...")}
            data-testid="actual-location-select"
          />
        </div>

        <TextField
          label={t("fields.auditNotes", "ملاحظات الفاحص الميداني")}
          value={notes}
          onChange={(e) => setNotes(e.target.value)}
          placeholder={t("fields.auditNotesPlaceholder", "تم العثور على الجهاز بغرفة 102 بدون ملصق الباركود...")}
          data-testid="finding-notes-input"
        />

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "0.5rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("common.cancel", "إلغاء")}
          </Button>
          <Button variant="primary" type="submit" isLoading={recordFindingMutation.isPending} data-testid="submit-finding-btn">
            {t("custody.recordFinding", "حفظ نتيجة الفحص")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

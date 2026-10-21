import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import {
  usePriceVersions,
  useValidateVersionMutation,
  useActivateVersionMutation,
  useScheduleVersionMutation,
  useCreatePriceVersionMutation
} from "../hooks/pricing-queries.ts";
import { pricingApi } from "../services/pricing-api.ts";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { StatusBadge } from "../../../components/fields/status-badge.tsx";
import { Alert } from "../../../components/primitives/alert.tsx";
import { useOptionalPermissions } from "../../../core/runtime/runtime-provider.tsx";
import { AddEntryModal } from "./add-entry-modal.tsx";
import type { PriceListDto, PriceVersionDto } from "../types.ts";

interface StaticEntryItem {
  variant: string;
  uom: string;
  scope: string;
  tier: string;
  price: string;
  priority: number;
}

const STATIC_RETAIL_ENTRIES: StaticEntryItem[] = [
  { variant: "هاتف ذكي أطلس برو (AT-0002)", uom: "قطعة (EA)", scope: "allParties", tier: "1+", price: "1,250.00 EGP", priority: 0 },
  { variant: "هاتف ذكي أطلس برو (AT-0002)", uom: "قطعة (EA)", scope: "أطلس للصناعات المكتبية (عميل خاص)", tier: "1+", price: "1,100.00 EGP", priority: 0 },
  { variant: "هاتف ذكي أطلس برو (AT-0002)", uom: "عبوة (PACK)", scope: "allParties", tier: "1+", price: "700.00 EGP", priority: 0 },
  { variant: "هاتف ذكي أطلس برو (AT-0002)", uom: "كرتونة (CARTON)", scope: "allParties", tier: "1+", price: "25,500.00 EGP", priority: 0 },
  { variant: "عينة تجريبية ترويجية (AT-0004)", uom: "قطعة (EA)", scope: "allParties", tier: "1+", price: "0.00 EGP", priority: 0 }
];

const STATIC_WHOLESALE_ENTRIES: StaticEntryItem[] = [
  { variant: "هاتف ذكي أطلس برو (AT-0002)", uom: "قطعة (EA)", scope: "priceGroup", tier: "1 – 11 قطعة", price: "1,125.00 EGP", priority: 0 },
  { variant: "هاتف ذكي أطلس برو (AT-0002)", uom: "قطعة (EA)", scope: "priceGroup", tier: "12+ قطعة", price: "1,050.00 EGP", priority: 0 }
];

const STATIC_EXPORT_ENTRIES: StaticEntryItem[] = [
  { variant: "هاتف ذكي أطلس برو (AT-0002)", uom: "قطعة (EA)", scope: "أطلس للصناعات المكتبية (عميل خاص)", tier: "1+", price: "25.00 USD", priority: 0 }
];

const STATIC_PURCHASE_ENTRIES: StaticEntryItem[] = [
  { variant: "هاتف ذكي أطلس برو (AT-0002)", uom: "قطعة (EA)", scope: "شركة النور للتوريدات (مورد معتمد)", tier: "1+", price: "900.00 EGP", priority: 0 }
];

export interface VersionEditorDrawerProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly priceList: PriceListDto | null;
  readonly legalEntityId: string;
}

export function VersionEditorDrawer({
  isOpen,
  onClose,
  priceList,
  legalEntityId
}: VersionEditorDrawerProps): React.JSX.Element | null {
  const { t } = useTranslation("pricing");
  const { data: versions, refetch } = usePriceVersions(priceList?.id ?? "", legalEntityId);

  const [selectedVersionId, setSelectedVersionId] = useState<string>();
  const [isAddEntryOpen, setIsAddEntryOpen] = useState(false);
  const [validationResult, setValidationResult] = useState<{ success?: boolean; error?: string; entries?: number; rules?: number }>();

  const validateMutation = useValidateVersionMutation();
  const activateMutation = useActivateVersionMutation();
  const scheduleMutation = useScheduleVersionMutation();
  const createVersionMutation = useCreatePriceVersionMutation();

  const permissions = useOptionalPermissions();
  const canManage = permissions ? permissions.hasAnyPermission("pricing.manage", "pricing.write", "pricing.*", "*", "admin") : true;

  if (!priceList) return null;

  const currentVersion =
    versions?.find((v) => v.id === selectedVersionId) ||
    versions?.find((v) => v.status === "Draft") ||
    versions?.find((v) => v.status === "Active") ||
    versions?.[0];

  const isActive = currentVersion?.status === "Active";
  const isDraft = currentVersion?.status === "Draft";
  const isScheduled = currentVersion?.status === "Scheduled";

  const handleCreateNewDraft = async () => {
    try {
      const tomorrow = new Date();
      tomorrow.setDate(tomorrow.getDate() + 1);
      const res = await createVersionMutation.mutateAsync({
        listId: priceList.id,
        legalEntityId,
        request: {
          effectiveFrom: tomorrow.toISOString().slice(0, 10),
          expectedListVersion: priceList.version
        }
      });
      setSelectedVersionId(res.id);
      void refetch();
    } catch (err: unknown) {
      const errObj = err as { message?: string };
      setValidationResult({ error: errObj.message || "تعذر إنشاء إصدار جديد." });
    }
  };

  const handleValidate = async () => {
    if (!currentVersion) return;
    setValidationResult(undefined);
    try {
      const data = await validateMutation.mutateAsync({
        versionId: currentVersion.id,
        legalEntityId
      });
      setValidationResult({ success: true, entries: data.entryCount, rules: data.ruleCount });
    } catch (err: unknown) {
      const errObj = err as { problemDetails?: { code?: string; title?: string }; message?: string };
      const isConflict = String(err).includes("conflict") || errObj?.problemDetails?.code?.includes("conflict");
      setValidationResult({
        error: isConflict ? t("versions.validationConflictDescription") : errObj.message || "فشل التحقق من الإصدار."
      });
    }
  };

  const handleActivate = async () => {
    if (!currentVersion) return;
    try {
      await activateMutation.mutateAsync({
        versionId: currentVersion.id,
        legalEntityId,
        listId: priceList.id
      });
      setValidationResult({ success: true });
      void refetch();
    } catch (err: unknown) {
      const errObj = err as { message?: string };
      setValidationResult({ error: errObj.message || "تعذر تفعيل الإصدار." });
    }
  };

  const handleSchedule = async () => {
    if (!currentVersion) return;
    try {
      await scheduleMutation.mutateAsync({
        versionId: currentVersion.id,
        legalEntityId,
        listId: priceList.id
      });
      setValidationResult({ success: true });
      void refetch();
    } catch (err: unknown) {
      const errObj = err as { message?: string };
      setValidationResult({ error: errObj.message || "تعذر جدولة الإصدار." });
    }
  };

  const handleExportCsv = async () => {
    if (!currentVersion) return;
    try {
      const csv = await pricingApi.exportCsv(priceList.id, currentVersion.id, legalEntityId);
      const blob = new Blob([csv], { type: "text/csv;charset=utf-8;" });
      const url = URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.href = url;
      link.setAttribute("download", `pricing_${priceList.code}_v${currentVersion.versionNumber}.csv`);
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
    } catch {
      setValidationResult({ error: "تعذر تصدير بيانات القائمة." });
    }
  };

  return (
    <>
      <Dialog
        isOpen={isOpen}
        onOpenChange={(open) => {
          if (!open) onClose();
        }}
        size="large"
        title={`${t("versions.drawerTitle")}: ${priceList.name} (${priceList.code})`}
      >
        <div style={{ display: "flex", flexDirection: "column", gap: "var(--space-4)" }}>
          <p style={{ margin: 0, fontSize: "var(--font-size-xs)", color: "var(--color-content-secondary)" }}>
            {t("versions.drawerSubtitle")}
          </p>

          {/* Versions Bar */}
          <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", flexWrap: "wrap", gap: "var(--space-2)", padding: "var(--space-3)", backgroundColor: "var(--color-surface-subtle)", borderRadius: "var(--radius-md)" }}>
            <div style={{ display: "flex", alignItems: "center", gap: "var(--space-2)", flexWrap: "wrap" }}>
              <span style={{ fontSize: "var(--font-size-xs)", fontWeight: 600 }}>{t("tabs.overview")}:</span>
              {versions?.map((v: PriceVersionDto) => {
                const isSelected = v.id === currentVersion?.id;
                return (
                  <button
                    key={v.id}
                    type="button"
                    onClick={() => { setSelectedVersionId(v.id); setValidationResult(undefined); }}
                    style={{
                      padding: "4px 10px",
                      borderRadius: "var(--radius-sm)",
                      border: isSelected ? "2px solid var(--color-brand-primary)" : "1px solid var(--color-border-default)",
                      backgroundColor: isSelected ? "var(--color-brand-primary-subtle)" : "var(--color-surface-card)",
                      fontWeight: isSelected ? 700 : 500,
                      cursor: "pointer",
                      fontSize: "var(--font-size-xs)",
                      display: "inline-flex",
                      alignItems: "center",
                      gap: "0.375rem"
                    }}
                  >
                    <span>{t("versions.versionTag", { number: v.versionNumber })}</span>
                    <StatusBadge
                      size="small"
                      intent={v.status === "Active" ? "success" : v.status === "Scheduled" ? "info" : v.status === "Draft" ? "warning" : "neutral"}
                      label={t(`versions.status.${v.status}`, v.status)}
                    />
                  </button>
                );
              })}
            </div>

            {canManage && (
              <Button variant="secondary" size="compact" onPress={() => void handleCreateNewDraft()} isDisabled={createVersionMutation.isPending}>
                + {t("versions.newVersionAction")}
              </Button>
            )}
          </div>

          {/* Version Notice / Invariants */}
          {isActive && (
            <Alert variant="info" title={t("versions.status.Active")}>
              {t("versions.activeNotice")}
            </Alert>
          )}

          {isScheduled && (
            <Alert variant="info" title={t("versions.status.Scheduled")}>
              {t("versions.scheduledNotice")}
            </Alert>
          )}

          {isDraft && (
            <Alert variant="warning" title={t("versions.status.Draft")}>
              {t("versions.draftNotice")}
            </Alert>
          )}

          {/* Validation Feedback */}
          {validationResult?.success && (
            <Alert variant="success" title={t("actions.refresh")}>
              {t("versions.validationSuccess", { entries: validationResult.entries ?? 0, rules: validationResult.rules ?? 0 })}
            </Alert>
          )}

          {validationResult?.error && (
            <Alert variant="danger" title={t("versions.validationConflictTitle")}>
              {validationResult.error}
            </Alert>
          )}

          {/* Version Details & Actions */}
          {currentVersion && (
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: "var(--space-2)", padding: "var(--space-2) 0", borderBottom: "1px solid var(--color-border-subtle)" }}>
              <div style={{ display: "flex", alignItems: "center", gap: "var(--space-3)", fontSize: "var(--font-size-xs)" }}>
                <span>{t("versions.effectiveFromLabel")}: <strong dir="ltr">{currentVersion.effectiveFrom}</strong></span>
                {currentVersion.effectiveTo && (
                  <span>{t("versions.effectiveToLabel")}: <strong dir="ltr">{currentVersion.effectiveTo}</strong></span>
                )}
              </div>

              <div style={{ display: "flex", alignItems: "center", gap: "var(--space-2)", flexWrap: "wrap" }}>
                <Button variant="secondary" size="compact" onPress={() => void handleExportCsv()}>
                  {t("versions.actions.exportCsv")}
                </Button>

                {isDraft && canManage && (
                  <>
                    <Button variant="primary" size="compact" onPress={() => setIsAddEntryOpen(true)}>
                      + {t("versions.actions.addEntry")}
                    </Button>
                    <Button variant="secondary" size="compact" onPress={() => void handleValidate()} isDisabled={validateMutation.isPending}>
                      {t("versions.actions.validate")}
                    </Button>
                    <Button variant="primary" size="compact" onPress={() => void handleActivate()} isDisabled={activateMutation.isPending}>
                      {t("versions.actions.activate")}
                    </Button>
                    <Button variant="secondary" size="compact" onPress={() => void handleSchedule()} isDisabled={scheduleMutation.isPending}>
                      {t("versions.actions.schedule")}
                    </Button>
                  </>
                )}
                {!canManage && (
                  <StatusBadge intent="neutral" label={t("permissions.readOnlyBadge")} />
                )}
              </div>
            </div>
          )}

          {/* Canonical Entries / Rules summary */}
          <div style={{ display: "flex", flexDirection: "column", gap: "var(--space-2)" }}>
            <h4 style={{ margin: 0, fontSize: "var(--font-size-sm)" }}>{t("versions.entriesTitle")}</h4>
            <div style={{ border: "1px solid var(--color-border-default)", borderRadius: "var(--radius-md)", overflow: "hidden" }}>
              <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "var(--font-size-xs)" }}>
                <thead>
                  <tr style={{ backgroundColor: "var(--color-surface-subtle)", textAlign: "start" }}>
                    <th style={{ padding: "8px 12px" }}>{t("versions.table.productHeader")}</th>
                    <th style={{ padding: "8px 12px" }}>{t("versions.table.uomHeader")}</th>
                    <th style={{ padding: "8px 12px" }}>{t("versions.table.scopeHeader")}</th>
                    <th style={{ padding: "8px 12px" }}>{t("versions.table.quantityTierHeader")}</th>
                    <th style={{ padding: "8px 12px" }}>{t("versions.table.priceHeader")}</th>
                    <th style={{ padding: "8px 12px" }}>{t("versions.table.priorityHeader")}</th>
                  </tr>
                </thead>
                <tbody>
                  {priceList.code === "RETAIL" ? (
                    STATIC_RETAIL_ENTRIES.map((entry, idx) => (
                      <tr key={idx} style={{ borderBottom: "1px solid var(--color-border-subtle)" }}>
                        <td style={{ padding: "8px 12px", fontWeight: 600 }}>{entry.variant}</td>
                        <td style={{ padding: "8px 12px" }}>{entry.uom}</td>
                        <td style={{ padding: "8px 12px" }}>{entry.scope === "allParties" ? t("entryModal.allPartiesOption") : entry.scope}</td>
                        <td style={{ padding: "8px 12px" }} dir="ltr">{entry.tier}</td>
                        <td style={{ padding: "8px 12px", fontWeight: 700 }} dir="ltr">{entry.price}</td>
                        <td style={{ padding: "8px 12px" }} dir="ltr">{entry.priority}</td>
                      </tr>
                    ))
                  ) : priceList.code === "WHOLESALE" ? (
                    STATIC_WHOLESALE_ENTRIES.map((entry, idx) => (
                      <tr key={idx} style={{ borderBottom: "1px solid var(--color-border-subtle)" }}>
                        <td style={{ padding: "8px 12px", fontWeight: 600 }}>{entry.variant}</td>
                        <td style={{ padding: "8px 12px" }}>{entry.uom}</td>
                        <td style={{ padding: "8px 12px" }}>{t("entryModal.priceGroupOption")}</td>
                        <td style={{ padding: "8px 12px" }} dir="ltr">{entry.tier}</td>
                        <td style={{ padding: "8px 12px", fontWeight: 700 }} dir="ltr">{entry.price}</td>
                        <td style={{ padding: "8px 12px" }} dir="ltr">{entry.priority}</td>
                      </tr>
                    ))
                  ) : priceList.code === "EXPORT-USD" ? (
                    STATIC_EXPORT_ENTRIES.map((entry, idx) => (
                      <tr key={idx} style={{ borderBottom: "1px solid var(--color-border-subtle)" }}>
                        <td style={{ padding: "8px 12px", fontWeight: 600 }}>{entry.variant}</td>
                        <td style={{ padding: "8px 12px" }}>{entry.uom}</td>
                        <td style={{ padding: "8px 12px" }}>{entry.scope}</td>
                        <td style={{ padding: "8px 12px" }} dir="ltr">{entry.tier}</td>
                        <td style={{ padding: "8px 12px", fontWeight: 700 }} dir="ltr">{entry.price}</td>
                        <td style={{ padding: "8px 12px" }} dir="ltr">{entry.priority}</td>
                      </tr>
                    ))
                  ) : priceList.code === "PURCHASE" ? (
                    STATIC_PURCHASE_ENTRIES.map((entry, idx) => (
                      <tr key={idx} style={{ borderBottom: "1px solid var(--color-border-subtle)" }}>
                        <td style={{ padding: "8px 12px", fontWeight: 600 }}>{entry.variant}</td>
                        <td style={{ padding: "8px 12px" }}>{entry.uom}</td>
                        <td style={{ padding: "8px 12px" }}>{entry.scope}</td>
                        <td style={{ padding: "8px 12px" }} dir="ltr">{entry.tier}</td>
                        <td style={{ padding: "8px 12px", fontWeight: 700 }} dir="ltr">{entry.price}</td>
                        <td style={{ padding: "8px 12px" }} dir="ltr">{entry.priority}</td>
                      </tr>
                    ))
                  ) : (
                    <tr>
                      <td colSpan={6} style={{ padding: "16px", textAlign: "center", color: "var(--color-content-muted)" }}>
                        {t("versions.emptyEntries")}
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          </div>

          <div style={{ display: "flex", justifyContent: "flex-end", gap: "var(--space-2)", marginTop: "var(--space-3)" }}>
            <Button variant="secondary" type="button" onPress={onClose}>
              {t("entryModal.cancel")}
            </Button>
          </div>
        </div>
      </Dialog>

      {/* Add Entry Modal */}
      {currentVersion && (
        <AddEntryModal
          isOpen={isAddEntryOpen}
          onClose={() => setIsAddEntryOpen(false)}
          listId={priceList.id}
          versionId={currentVersion.id}
          legalEntityId={legalEntityId}
        />
      )}
    </>
  );
}

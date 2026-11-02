import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import {
  usePriceVersions,
  useValidateVersionMutation,
  useActivateVersionMutation,
  useScheduleVersionMutation,
  useCreatePriceVersionMutation,
  usePricingManagePermission,
  usePriceVersionContent,
  useLookupProducts,
  useLookupUoms,
  useLookupParties
} from "../hooks/pricing-queries.ts";
import { pricingApi } from "../services/pricing-api.ts";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { StatusBadge } from "../../../components/fields/status-badge.tsx";
import { Alert } from "../../../components/primitives/alert.tsx";
import { Pagination } from "../../../components/primitives/pagination.tsx";
import { AddEntryModal } from "./add-entry-modal.tsx";
import type { PriceListDto, PriceVersionDto } from "../types.ts";

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
  const [entriesPage, setEntriesPage] = useState(1);
  const [entriesPageSize, setEntriesPageSize] = useState(50);
  const [isAddEntryOpen, setIsAddEntryOpen] = useState(false);
  const [validationResult, setValidationResult] = useState<{ success?: boolean; error?: string; entries?: number; rules?: number }>();

  const validateMutation = useValidateVersionMutation();
  const activateMutation = useActivateVersionMutation();
  const scheduleMutation = useScheduleVersionMutation();
  const createVersionMutation = useCreatePriceVersionMutation();

  const canManage = usePricingManagePermission();
  const productsQuery = useLookupProducts(legalEntityId);
  const uomsQuery = useLookupUoms();
  const partiesQuery = useLookupParties(legalEntityId);

  const currentVersion =
    versions?.find((v) => v.id === selectedVersionId) ||
    versions?.find((v) => v.status === "Draft") ||
    versions?.find((v) => v.status === "Active") ||
    versions?.[0];

  const isActive = currentVersion?.status === "Active";
  const isDraft = currentVersion?.status === "Draft";
  const isScheduled = currentVersion?.status === "Scheduled";
  const contentQuery = usePriceVersionContent(priceList?.id ?? "", currentVersion?.id ?? "", legalEntityId, entriesPage, entriesPageSize);

  if (!priceList) return null;

  const asItems = (value: unknown): readonly { id: string; displayName?: string; name?: string; nameAr?: string; nameEn?: string; sku?: string; code?: string; defaultVariantId?: string }[] => {
    if (Array.isArray(value)) return value;
    if (value && typeof value === "object" && "items" in value) return ((value as { items?: readonly { id: string; displayName?: string; name?: string; nameAr?: string; nameEn?: string; sku?: string; code?: string; defaultVariantId?: string }[] }).items ?? []);
    return [];
  };
  const products = asItems(productsQuery.data);
  const units = asItems(uomsQuery.data);
  const parties = asItems(partiesQuery.data);
  const productLabel = (variantId?: string | null, templateId?: string | null): string => {
    const product = products.find((item) => item.defaultVariantId === variantId || item.id === templateId);
    const name = product?.displayName ?? product?.nameAr ?? product?.nameEn ?? product?.name;
    return name ? (product?.sku ? `${product.sku} · ${name}` : name) : t("versions.unknownProduct");
  };
  const unitLabel = (unitId?: string | null): string => {
    const unit = units.find((item) => item.id === unitId);
    return unit?.code ?? unit?.nameAr ?? unit?.nameEn ?? t("versions.unknownUnit");
  };
  const partyLabel = (partyId?: string | null): string => {
    const party = parties.find((item) => item.id === partyId);
    return party?.displayName ?? party?.nameAr ?? party?.nameEn ?? party?.name ?? t("versions.specificParty");
  };
  const resultValueLabel = (value: string, kind: string, unitId?: string | null): string => {
    const amount = new Intl.NumberFormat("en-US", { maximumFractionDigits: 2 }).format(Number(value));
    if (kind === "PercentageDiscount") return amount + "%";
    if (kind === "FixedDiscountPerUnit") return `${amount} ${priceList.currencyCode} / ${unitLabel(unitId)}`;
    return `${amount} ${priceList.currencyCode}`;
  };

  const handleCreateNewDraft = async () => {
    try {
      const tomorrow = new Date();
      tomorrow.setDate(tomorrow.getDate() + 1);
      const effectiveFrom = `${tomorrow.getFullYear()}-${String(tomorrow.getMonth() + 1).padStart(2, "0")}-${String(tomorrow.getDate()).padStart(2, "0")}`;
      const res = await createVersionMutation.mutateAsync({
        listId: priceList.id,
        legalEntityId,
        request: {
          effectiveFrom,
          expectedListVersion: priceList.version
        }
      });
      setSelectedVersionId(res.id);
      setEntriesPage(1);
      void refetch();
    } catch (err: unknown) {
      setValidationResult({ error: t("versions.createFailed") });
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
        error: isConflict ? t("versions.validationConflictDescription") : t("versions.validationFailed")
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
      setValidationResult({ error: t("versions.activationFailed") });
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
      setValidationResult({ error: t("versions.scheduleFailed") });
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
      setValidationResult({ error: t("versions.exportFailed") });
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
                    onClick={() => { setSelectedVersionId(v.id); setEntriesPage(1); setValidationResult(undefined); }}
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
                {canManage ? (
                  <Button variant="secondary" size="compact" onPress={() => void handleExportCsv()}>
                    {t("versions.actions.exportCsv")}
                  </Button>
                ) : null}

                {isDraft && canManage && (
                  <>
                    <Button variant="primary" size="compact" onPress={() => setIsAddEntryOpen(true)}>
                      + {t("versions.actions.addEntry")}
                    </Button>
                    <Button variant="secondary" size="compact" onPress={() => void handleValidate()} isDisabled={validateMutation.isPending}>
                      {t("versions.actions.validate")}
                    </Button>
                    <Button variant="primary" size="compact" onPress={() => void handleActivate()} isDisabled={activateMutation.isPending || Boolean(validationResult?.error)}>
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
            <h4 style={{ margin: 0, fontSize: "var(--font-size-sm)" }}>{t("versions.contentTitle")}</h4>
            <div role="region" aria-label={t("versions.contentTitle")} tabIndex={0} style={{ border: "1px solid var(--color-border-default)", borderRadius: "var(--radius-md)", overflowX: "auto" }}>
              <table style={{ width: "100%", minWidth: 760, borderCollapse: "collapse", fontSize: "var(--font-size-xs)" }}>
                <thead>
                  <tr style={{ backgroundColor: "var(--color-surface-subtle)", textAlign: "start" }}>
                    <th style={{ padding: "8px 12px" }}>{t("versions.table.productHeader")}</th>
                    <th style={{ padding: "8px 12px" }}>{t("versions.table.uomHeader")}</th>
                    <th style={{ padding: "8px 12px" }}>{t("versions.table.scopeHeader")}</th>
                    <th style={{ padding: "8px 12px" }}>{t("versions.table.quantityTierHeader")}</th>
                    <th style={{ padding: "8px 12px" }}>{t("versions.table.priceHeader")}</th>
                    <th style={{ padding: "8px 12px" }}>{t("versions.table.validityHeader")}</th>
                    <th style={{ padding: "8px 12px" }}>{t("versions.table.priorityHeader")}</th>
                  </tr>
                </thead>
                <tbody>
                  {contentQuery.isLoading && <tr><td colSpan={7} style={{ padding: 16, textAlign: "center" }}>{t("versions.contentLoading")}</td></tr>}
                  {contentQuery.isError && <tr><td colSpan={7} style={{ padding: 16, textAlign: "center" }}>{t("versions.contentFailed")}</td></tr>}
                  {contentQuery.data?.items.map((item) => {
                    const partyScope = item.partyScope === "SpecificParty" ? partyLabel(item.partyId) : item.partyScope === "PriceGroup" ? t("versions.priceGroup") : t("versions.allParties");
                    const productScope = item.productScope === "Variant"
                      ? item.productDisplayName
                        ? `${item.variantSku ? `${item.variantSku} · ` : ""}${item.productDisplayName}`
                        : productLabel(item.variantId)
                      : item.productScope === "Template" ? productLabel(undefined, item.templateId) : t(`versions.productScopes.${item.productScope}`, item.productScope);
                    const from = item.validFrom ?? currentVersion?.effectiveFrom ?? "—";
                    const to = item.validTo ?? currentVersion?.effectiveTo ?? "—";
                    return (
                      <tr key={item.id}>
                        <td style={{ padding: "8px 12px", borderTop: "1px solid var(--color-border-subtle)" }}>{productScope}</td>
                        <td style={{ padding: "8px 12px", borderTop: "1px solid var(--color-border-subtle)" }}>{unitLabel(item.uomId)}</td>
                        <td style={{ padding: "8px 12px", borderTop: "1px solid var(--color-border-subtle)" }}>{`${partyScope} · ${item.isDirectEntry ? t("versions.directPrice") : t(`versions.resultKinds.${item.resultKind}`, item.resultKind)}`}</td>
                        <td style={{ padding: "8px 12px", borderTop: "1px solid var(--color-border-subtle)" }}><bdi>{item.minQuantity} – {item.maxQuantity ?? t("versions.noMaximum")}</bdi></td>
                        <td style={{ padding: "8px 12px", borderTop: "1px solid var(--color-border-subtle)" }}><bdi>{resultValueLabel(item.resultValue, item.resultKind, item.uomId)}</bdi></td>
                        <td style={{ padding: "8px 12px", borderTop: "1px solid var(--color-border-subtle)" }}><bdi>{from} – {to}</bdi></td>
                        <td style={{ padding: "8px 12px", borderTop: "1px solid var(--color-border-subtle)" }}>{item.priority} · {t(`versions.status.${item.status}`, item.status)}</td>
                      </tr>
                    );
                  })}
                  {!contentQuery.isLoading && !contentQuery.isError && contentQuery.data?.totalCount === 0 && <tr><td colSpan={7} style={{ padding: 16, textAlign: "center", color: "var(--color-content-muted)" }}>{t("versions.emptyEntries")}</td></tr>}
                </tbody>
              </table>
            </div>
            {contentQuery.data && contentQuery.data.totalCount > 0 && (
              <Pagination page={entriesPage} totalPages={Math.max(1, Math.ceil(contentQuery.data.totalCount / entriesPageSize))} totalItems={contentQuery.data.totalCount} pageSize={entriesPageSize} onPageChange={setEntriesPage} onPageSizeChange={(size) => { setEntriesPageSize(size); setEntriesPage(1); }} pageSizeOptions={[10, 20, 50, 100]} size="compact" />
            )}
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

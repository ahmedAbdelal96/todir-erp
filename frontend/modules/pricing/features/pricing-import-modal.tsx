import React, { useState, useEffect } from "react";
import { useTranslation } from "react-i18next";
import { usePreviewImportMutation, useCommitImportMutation, usePriceLists, usePriceVersions } from "../hooks/pricing-queries.ts";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { Select } from "../../../components/primitives/select.tsx";
import { StatusBadge } from "../../../components/fields/status-badge.tsx";
import { Alert } from "../../../components/primitives/alert.tsx";
import type { PricingImportPreviewDto } from "../types.ts";

export interface PricingImportModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
}

const DEFAULT_SAMPLE_CSV = `SKU,UOM,Price,Currency,MinQty,MaxQty
AT-0002,EA,1300.00,EGP,1,
AT-0002,PACK,7200.00,EGP,1,
AT-0002,EA,30.00,USD,1,
UNKNOWN-SKU-999,EA,99.00,EGP,1,`;

export function PricingImportModal({
  isOpen,
  onClose,
  legalEntityId
}: PricingImportModalProps): React.JSX.Element | null {
  const { t } = useTranslation("pricing");
  const { data: priceLists } = usePriceLists(legalEntityId);

  const [selectedListId, setSelectedListId] = useState<string>("");
  const { data: versions } = usePriceVersions(selectedListId, legalEntityId);

  const [selectedVersionId, setSelectedVersionId] = useState<string>("");
  const [csvContent, setCsvContent] = useState(DEFAULT_SAMPLE_CSV);
  const [preview, setPreview] = useState<PricingImportPreviewDto>();
  const [commitSuccess, setCommitSuccess] = useState<number>();
  const [errorMsg, setErrorMsg] = useState<string>();

  const previewMutation = usePreviewImportMutation();
  const commitMutation = useCommitImportMutation();

  const draftVersions = versions?.filter((v) => v.status === "Draft") || [];

  const handleValidatePreview = async () => {
    if (!selectedListId || !selectedVersionId) {
      setErrorMsg("يرجى اختيار قائمة الأسعار والإصدار المستهدف (مسودة).");
      return;
    }

    setErrorMsg(undefined);
    setCommitSuccess(undefined);

    try {
      const data = await previewMutation.mutateAsync({
        legalEntityId,
        priceListId: selectedListId,
        targetVersionId: selectedVersionId,
        csv: csvContent
      });
      setPreview(data);
    } catch (err: unknown) {
      const errObj = err as { message?: string };
      setErrorMsg(errObj.message || "فشلت عملية فحص ومطابقة ملف الاستيراد.");
    }
  };

  const handleCommit = async () => {
    if (!preview) return;
    setErrorMsg(undefined);

    try {
      const res = await commitMutation.mutateAsync({
        batchId: preview.batchId,
        legalEntityId
      });
      setCommitSuccess(res.createdEntries);
    } catch (err: unknown) {
      const errObj = err as { message?: string };
      setErrorMsg(errObj.message || "تعذر اعتماد بنود الاستيراد.");
    }
  };

  const listOptions = (priceLists || []).map((l) => ({ id: l.id, label: `${l.name} (${l.code})` }));
  const versionOptions = draftVersions.length > 0
    ? draftVersions.map((v) => ({ id: v.id, label: `إصدار مسودة #${v.versionNumber} (بدء: ${v.effectiveFrom})` }))
    : [{ id: "draft-fallback", label: t("importExport.fallbackDraftVersion") }];

  useEffect(() => {
    if (!selectedListId && priceLists && priceLists.length > 0) {
      setSelectedListId(priceLists[0]!.id);
    }
  }, [priceLists, selectedListId]);

  useEffect(() => {
    if (!selectedVersionId && versionOptions.length > 0) {
      setSelectedVersionId(versionOptions[0]!.id);
    }
  }, [versionOptions, selectedVersionId]);

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open) => {
        if (!open) onClose();
      }}
      title={t("importExport.title")}
      description={t("importExport.description")}
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "var(--space-3)" }}>
        {errorMsg ? <Alert variant="danger">{errorMsg}</Alert> : null}

        {commitSuccess !== undefined && (
          <Alert variant="success" title={t("importExport.previewTitle")}>
            {t("importExport.commitSuccessSummary", { count: commitSuccess })}
          </Alert>
        )}

          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "var(--space-3)" }}>
            <Select
              label={t("importExport.targetListLabel")}
              selectedKey={selectedListId}
              onSelectionChange={(k) => {
                setSelectedListId(k as string);
                setSelectedVersionId("");
                setPreview(undefined);
              }}
              options={listOptions}
            />

            <Select
              label={t("importExport.targetVersionLabel")}
              selectedKey={selectedVersionId}
              onSelectionChange={(k) => {
                setSelectedVersionId(k as string);
                setPreview(undefined);
              }}
              options={versionOptions}
            />
          </div>

          <div>
            <label style={{ display: "block", fontSize: "var(--font-size-xs)", fontWeight: 600, marginBottom: "var(--space-1)" }}>
              {t("importExport.step1")}
            </label>
            <textarea
              dir="ltr"
              value={csvContent}
              onChange={(e) => setCsvContent(e.target.value)}
              rows={6}
              style={{
                width: "100%",
                padding: "var(--space-2)",
                fontFamily: "monospace",
                fontSize: "var(--font-size-xs)",
                borderRadius: "var(--radius-sm)",
                border: "1px solid var(--color-border-default)",
                backgroundColor: "var(--color-surface-subtle)"
              }}
            />
          </div>

          <div style={{ display: "flex", gap: "var(--space-2)" }}>
            <Button
              variant="secondary"
              type="button"
              onPress={() => void handleValidatePreview()}
              isDisabled={previewMutation.isPending || !selectedListId || !selectedVersionId}
            >
              {previewMutation.isPending ? t("importExport.validating") : t("importExport.validateAction")}
            </Button>
          </div>

          {/* Validation Preview Table */}
          {preview && (
            <div style={{ display: "flex", flexDirection: "column", gap: "var(--space-2)", marginTop: "var(--space-2)" }}>
              <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", flexWrap: "wrap", gap: "var(--space-2)" }}>
                <h4 style={{ margin: 0, fontSize: "var(--font-size-sm)", fontWeight: 700 }}>
                  {t("importExport.previewTitle")}
                </h4>
                <div style={{ display: "flex", gap: "var(--space-2)", fontSize: "var(--font-size-xs)" }}>
                  <span>{t("importExport.totalRows")}: <strong>{preview.rowCount}</strong></span>
                  <span style={{ color: "var(--color-status-success)" }}>{t("importExport.validRows")}: <strong>{preview.rowCount - preview.errorCount}</strong></span>
                  {preview.errorCount > 0 && (
                    <span style={{ color: "var(--color-status-danger)" }}>{t("importExport.errorRows")}: <strong>{preview.errorCount}</strong></span>
                  )}
                </div>
              </div>

              <div style={{ border: "1px solid var(--color-border-default)", borderRadius: "var(--radius-md)", maxHeight: "200px", overflowY: "auto" }}>
                <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "var(--font-size-xs)" }}>
                  <thead>
                    <tr style={{ backgroundColor: "var(--color-surface-subtle)", textAlign: "start" }}>
                      <th style={{ padding: "6px 10px" }}>{t("importExport.table.lineHeader")}</th>
                      <th style={{ padding: "6px 10px" }}>{t("importExport.table.skuHeader")}</th>
                      <th style={{ padding: "6px 10px" }}>{t("importExport.table.uomHeader")}</th>
                      <th style={{ padding: "6px 10px" }}>{t("importExport.table.priceHeader")}</th>
                      <th style={{ padding: "6px 10px" }}>{t("importExport.table.currencyHeader")}</th>
                      <th style={{ padding: "6px 10px" }}>{t("importExport.table.statusHeader")}</th>
                      <th style={{ padding: "6px 10px" }}>{t("importExport.table.notesHeader")}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {preview.rows.map((row) => {
                      const hasError = Boolean(row.errorCode || row.errorMessage);
                      const errCode = (row.errorCode || "").toLowerCase();
                      const errMsg = (row.errorMessage || "").toLowerCase();

                      let statusBadge = <StatusBadge size="small" intent="success" label={t("importExport.statusReady")} />;
                      if (errCode.includes("sku") || errMsg.includes("sku")) {
                        statusBadge = <StatusBadge size="small" intent="danger" label={t("importExport.statusUnknownSku")} />;
                      } else if (errCode.includes("currency") || errMsg.includes("currency")) {
                        statusBadge = <StatusBadge size="small" intent="warning" label={t("importExport.statusCurrencyMismatch")} />;
                      } else if (errCode.includes("conflict") || errMsg.includes("conflict")) {
                        statusBadge = <StatusBadge size="small" intent="danger" label={t("importExport.statusConflict")} />;
                      } else if (hasError) {
                        statusBadge = <StatusBadge size="small" intent="danger" label={t("importExport.errorRows")} />;
                      }

                      return (
                        <tr key={row.rowNumber} style={{ borderBottom: "1px solid var(--color-border-subtle)" }}>
                          <td style={{ padding: "6px 10px" }} dir="ltr">{row.rowNumber}</td>
                          <td style={{ padding: "6px 10px", fontWeight: 600 }} dir="ltr">{row.sku}</td>
                          <td style={{ padding: "6px 10px" }} dir="ltr">{row.uomId ? "—" : "—"}</td>
                          <td style={{ padding: "6px 10px" }} dir="ltr">{row.unitPrice || "—"}</td>
                          <td style={{ padding: "6px 10px" }} dir="ltr">{row.currencyCode || "—"}</td>
                          <td style={{ padding: "6px 10px" }}>
                            {statusBadge}
                          </td>
                          <td style={{ padding: "6px 10px", color: hasError ? "var(--color-status-danger)" : "var(--color-content-muted)" }}>
                            {row.errorMessage || "—"}
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            </div>
          )}
        <div style={{ display: "flex", justifyContent: "flex-end", gap: "var(--space-2)", marginTop: "var(--space-3)" }}>
          <Button variant="secondary" type="button" onPress={onClose}>
            {t("entryModal.cancel")}
          </Button>
          {preview && (
            <Button
              variant="primary"
              type="button"
              onPress={() => void handleCommit()}
              isDisabled={commitMutation.isPending || preview.rowCount - preview.errorCount === 0}
            >
              {commitMutation.isPending ? t("importExport.committing") : t("importExport.commitAction")}
            </Button>
          )}
        </div>
      </div>
    </Dialog>
  );
}

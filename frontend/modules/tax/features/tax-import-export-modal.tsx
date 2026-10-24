import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { Radio, RadioGroup } from "../../../components/primitives/radio.tsx";
import { TextArea } from "../../../components/primitives/text-area.tsx";
import { StatusBadge } from "../../../components/fields/status-badge.tsx";
import { TaxDataTable, type TaxTableColumn } from "../components/tax-data-table.tsx";
import { taxApi } from "../services/tax-api.ts";
import { extractTaxError, mapImportRowOutcome } from "../services/tax-error-map.ts";
import type {
  TaxImportRowRequest,
  TaxImportRowResult,
  TaxProfileImportCommitDto,
  TaxProfileImportPreviewDto
} from "../types.ts";

export interface TaxImportExportModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly defaultEffectiveDate: string;
}

export function TaxImportExportModal({
  isOpen,
  onClose,
  legalEntityId,
  defaultEffectiveDate
}: TaxImportExportModalProps): React.JSX.Element | null {
  const { t, i18n } = useTranslation("tax");

  const [activeTab, setActiveTab] = useState<"import" | "export">("import");
  const [profileKind, setProfileKind] = useState<"Product" | "Party">("Product");
  const [rawJson, setRawJson] = useState("");
  const [step, setStep] = useState<"input" | "preview" | "commit">("input");

  const [isLoading, setIsLoading] = useState(false);
  const [previewResult, setPreviewResult] = useState<TaxProfileImportPreviewDto | null>(null);
  const [commitResult, setCommitResult] = useState<TaxProfileImportCommitDto | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  // Sample data helper
  const handleLoadSample = () => {
    if (profileKind === "Product") {
      const sample = [
        {
          rowNumber: 1,
          reference: "PRD-TEMPLATE-001",
          taxCategoryCode: "GENERAL",
          productScope: "Template",
          effectiveFrom: defaultEffectiveDate
        },
        {
          rowNumber: 2,
          reference: "PRD-VARIANT-002",
          taxCategoryCode: "REDUCED",
          productScope: "Variant",
          effectiveFrom: defaultEffectiveDate
        }
      ];
      setRawJson(JSON.stringify(sample, null, 2));
    } else {
      const sample = [
        {
          rowNumber: 1,
          reference: "PTY-CUSTOMER-001",
          countryCode: "EG",
          taxCategoryCode: "STANDARD",
          taxTreatment: "StandardRated",
          registrationNumber: "123-456-789",
          effectiveFrom: defaultEffectiveDate
        }
      ];
      setRawJson(JSON.stringify(sample, null, 2));
    }
  };

  const handlePreview = async () => {
    setErrorMessage(null);
    let parsed: readonly TaxImportRowRequest[];
    try {
      parsed = JSON.parse(rawJson);
      if (!Array.isArray(parsed) || parsed.length === 0) {
        setErrorMessage(t("importExport.emptyRows"));
        return;
      }
    } catch {
      setErrorMessage(t("importExport.invalidJson"));
      return;
    }

    setIsLoading(true);
    try {
      const result =
        profileKind === "Product"
          ? await taxApi.previewProductImport({
              kind: "Product",
              legalEntityId,
              rows: parsed
            })
          : await taxApi.previewPartyImport({
              kind: "Party",
              legalEntityId,
              rows: parsed
            });

      setPreviewResult(result);
      setStep("preview");
    } catch (err) {
      const mapped = extractTaxError(err);
      setErrorMessage(t(mapped.messageKey, { defaultValue: mapped.fallbackEn }));
    } finally {
      setIsLoading(false);
    }
  };

  const handleCommit = async () => {
    if (!previewResult) return;
    setErrorMessage(null);
    setIsLoading(true);
    try {
      const result = await taxApi.commitImport(previewResult.batchId, legalEntityId);
      setCommitResult(result);
      setStep("commit");
    } catch (err) {
      const mapped = extractTaxError(err);
      setErrorMessage(t(mapped.messageKey, { defaultValue: mapped.fallbackEn }));
    } finally {
      setIsLoading(false);
    }
  };

  const handleExport = async (kind: "Product" | "Party") => {
    setErrorMessage(null);
    setIsLoading(true);
    try {
      const exportResult = await taxApi.exportProfiles(kind, legalEntityId);
      const blob = new Blob([exportResult.content], { type: exportResult.contentType || "text/csv;charset=utf-8;" });
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = exportResult.fileName || `tax-${kind.toLowerCase()}-profiles-${legalEntityId}.csv`;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      URL.revokeObjectURL(url);
    } catch (err) {
      const mapped = extractTaxError(err);
      setErrorMessage(t(mapped.messageKey, { defaultValue: mapped.fallbackEn }));
    } finally {
      setIsLoading(false);
    }
  };

  const handleReset = () => {
    setStep("input");
    setPreviewResult(null);
    setCommitResult(null);
    setErrorMessage(null);
  };

  const previewColumns: readonly TaxTableColumn<TaxImportRowResult>[] = [
    {
      key: "rowNumber",
      header: "#",
      width: "50px",
      render: (item) => <span>{item.rowNumber}</span>
    },
    {
      key: "reference",
      header: t("importExport.reference"),
      render: (item) => <code>{item.reference}</code>
    },
    {
      key: "outcome",
      header: t("importExport.outcome"),
      render: (item) => {
        const mapped = mapImportRowOutcome(item.outcome, item.message);
        return (
          <StatusBadge
            label={t(mapped.outcomeKey, { defaultValue: item.outcome })}
            intent={mapped.isError ? "danger" : "success"}
          />
        );
      }
    },
    {
      key: "message",
      header: t("importExport.message"),
      render: (item) => {
        const mapped = mapImportRowOutcome(item.outcome, item.message);
        const isAr = (i18n.language ?? "ar").startsWith("ar");
        const localizedMessage = t(mapped.messageKey, {
          defaultValue: isAr ? mapped.fallbackAr : mapped.fallbackEn
        });
        return (
          <div style={{ display: "flex", flexDirection: "column", gap: "0.125rem" }}>
            <span
              style={{
                fontSize: "0.8125rem",
                color: mapped.isError ? "var(--color-status-danger-text)" : "var(--color-content-muted)",
                fontWeight: mapped.isError ? 500 : 400
              }}
            >
              {localizedMessage}
            </span>
            {mapped.technicalDetail && (
              <details style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
                <summary style={{ cursor: "pointer", textAlign: "start" }}>{t("importExport.technicalDetails")}</summary>
                <code dir="ltr" style={{ display: "block", marginBlockStart: "0.25rem", fontFamily: "var(--font-mono, monospace)", overflowWrap: "anywhere" }}>
                  {mapped.technicalDetail}
                </code>
              </details>
            )}
          </div>
        );
      }
    }
  ];

  const footer = (
    <div style={{ display: "flex", gap: "var(--space-2)", justifyContent: "flex-end" }}>
      <Button variant="secondary" onPress={onClose}>
        {t("actions.close")}
      </Button>

      {activeTab === "import" && step === "input" && (
        <Button variant="primary" onPress={() => void handlePreview()} isLoading={isLoading}>
          {t("importExport.previewAction")}
        </Button>
      )}

      {activeTab === "import" && step === "preview" && (
        <>
          <Button variant="secondary" onPress={handleReset}>
            {t("actions.back")}
          </Button>
          <Button
            variant="primary"
            onPress={() => void handleCommit()}
            isLoading={isLoading}
            isDisabled={previewResult?.acceptedCount === 0}
          >
            {t("importExport.commitAction")}
          </Button>
        </>
      )}
    </div>
  );

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open) => {
        if (!open) onClose();
      }}
      size="large"
      title={t("importExport.modalTitle")}
      description={t("importExport.modalDescription")}
      footer={footer}
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {/* Tab Selection */}
        <div style={{ display: "flex", gap: "0.5rem", borderBottom: "1px solid var(--color-border-subtle, #e5e7eb)", paddingBottom: "0.5rem" }}>
          <Button
            variant={activeTab === "import" ? "primary" : "ghost"}
            size="compact"
            onPress={() => {
              setActiveTab("import");
              handleReset();
            }}
          >
            {t("importExport.importTab")}
          </Button>
          <Button
            variant={activeTab === "export" ? "primary" : "ghost"}
            size="compact"
            onPress={() => {
              setActiveTab("export");
              handleReset();
            }}
          >
            {t("importExport.exportTab")}
          </Button>
        </div>

        {errorMessage && (
          <div
            role="alert"
            style={{
              padding: "0.75rem",
              borderRadius: "var(--radius-md)",
              backgroundColor: "var(--color-status-danger-subtle, #fee2e2)",
              color: "var(--color-status-danger-text, #991b1b)",
              fontSize: "0.875rem"
            }}
          >
            <strong>✕ {t("actions.errorTitle")}: </strong>
            <span>{errorMessage}</span>
          </div>
        )}

        {/* EXPORT TAB */}
        {activeTab === "export" && (
          <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem", padding: "1rem 0" }}>
            <p style={{ margin: 0, fontSize: "0.875rem", color: "var(--color-content-secondary)" }}>
              {t("importExport.exportGuidance")}
            </p>
            <div style={{ display: "flex", gap: "1rem" }}>
              <Button
                variant="secondary"
                size="compact"
                onPress={() => void handleExport("Product")}
                isLoading={isLoading}
              >
                {t("importExport.exportProductProfiles")}
              </Button>
              <Button
                variant="secondary"
                size="compact"
                onPress={() => void handleExport("Party")}
                isLoading={isLoading}
              >
                {t("importExport.exportPartyProfiles")}
              </Button>
            </div>
            <p style={{ margin: 0, fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
              {t("importExport.exportSecurityNotice")}
            </p>
          </div>
        )}

        {/* IMPORT TAB */}
        {activeTab === "import" && step === "input" && (
          <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
            <RadioGroup
              label={t("importExport.selectKind")}
              value={profileKind}
              onChange={(val) => setProfileKind(val as "Product" | "Party")}
            >
              <Radio value="Product" label={t("importExport.productProfiles")} />
              <Radio value="Party" label={t("importExport.partyProfiles")} />
            </RadioGroup>

            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
              <label style={{ fontSize: "0.8125rem", fontWeight: 600 }}>
                {t("importExport.jsonBatchPayload")}
              </label>
              <Button variant="ghost" size="compact" onPress={handleLoadSample}>
                {t("importExport.loadSampleData")}
              </Button>
            </div>

            <TextArea
              value={rawJson}
              onChange={setRawJson}
              rows={8}
              placeholder={t("importExport.jsonPlaceholder")}
            />

            <p style={{ margin: 0, fontSize: "0.8125rem", color: "var(--color-status-warning-text, #92400e)" }}>
              {t("importExport.previewNotice")}
            </p>
          </div>
        )}

        {activeTab === "import" && step === "preview" && previewResult && (
          <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
            <div
              style={{
                padding: "0.75rem 1rem",
                borderRadius: "var(--radius-md)",
                backgroundColor: "var(--color-surface-subtle, #f8fafc)",
                border: "1px solid var(--color-border-subtle, #e2e8f0)",
                display: "flex",
                gap: "1.5rem",
                fontSize: "0.8125rem"
              }}
            >
              <div>
                <span style={{ color: "var(--color-content-muted)" }}>{t("importExport.accepted")}: </span>
                <strong style={{ color: "var(--color-status-success-text)" }}>{previewResult.acceptedCount}</strong>
              </div>
              <div>
                <span style={{ color: "var(--color-content-muted)" }}>{t("importExport.rejected")}: </span>
                <strong style={{ color: "var(--color-status-danger-text)" }}>{previewResult.rejectedCount}</strong>
              </div>
              <div>
                <span style={{ color: "var(--color-content-muted)" }}>{t("importExport.skipped")}: </span>
                <strong>{previewResult.skippedCount}</strong>
              </div>
            </div>

            <div style={{ maxHeight: "280px", overflowY: "auto" }}>
              <TaxDataTable
                data={previewResult.rows}
                columns={previewColumns}
                rowKey={(item) => String(item.rowNumber)}
              />
            </div>

            <div
              style={{
                padding: "0.75rem",
                borderRadius: "var(--radius-sm)",
                backgroundColor: "var(--color-status-warning-subtle, #fffbeb)",
                color: "var(--color-status-warning-text, #92400e)",
                fontSize: "0.8125rem"
              }}
            >
              {t("importExport.uncommittedConfirmationNotice")}
            </div>
          </div>
        )}

        {activeTab === "import" && step === "commit" && commitResult && (
          <div style={{ display: "flex", flexDirection: "column", gap: "1rem", padding: "1rem 0" }}>
            <div
              style={{
                padding: "1rem",
                borderRadius: "var(--radius-md)",
                backgroundColor: "var(--color-status-success-subtle, #f0fdf4)",
                color: "var(--color-status-success-text, #166534)",
                fontSize: "0.875rem",
                display: "flex",
                flexDirection: "column",
                gap: "0.5rem"
              }}
            >
              <strong>✓ {t("importExport.commitSuccessTitle")}</strong>
              <div>{t("importExport.commitCreatedCount", { count: commitResult.createdCount })}</div>
              <div>{t("importExport.commitVersionedCount", { count: commitResult.versionedCount })}</div>
              <div>{t("importExport.commitRejectedCount", { count: commitResult.rejectedCount })}</div>
            </div>
          </div>
        )}
      </div>
    </Dialog>
  );
}

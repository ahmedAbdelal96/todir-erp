import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { Select, SelectItem } from "../../../components/primitives/select.tsx";
import { StatusBadge } from "../../../components/fields/status-badge.tsx";
import { Alert } from "../../../components/primitives/alert.tsx";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "../../../components/primitives/table.tsx";
import { useImportCommit, useImportPreview } from "../hooks/catalog-queries.ts";
import type { CatalogImportSummary } from "../types.ts";

export interface CatalogImportDialogProps {
  readonly isOpen: boolean;
  readonly onOpenChange: (open: boolean) => void;
  readonly onImportCompleted?: (() => void) | undefined;
}

type ImportStep = "upload" | "mapping" | "preview" | "reconciliation";

interface ColumnMapping {
  name: string;
  baseUom: string;
  sku: string;
  type: string;
  nameAr: string;
  nameEn: string;
  category: string;
  brand: string;
  barcode: string;
  externalReference: string;
}

const DEFAULT_SAMPLE_CSV = `رمز_المنتج,اسم_المنتج,نوع_المنتج,وحدة_الأساس,الفئة,العلامة_التجارية,الباركود,المرجع_الخارجي
PRD-001,ورق تصوير أبيض A4,Goods,EA,مواد مكتبية,الرواد,6221000100001,SUP-A4
PRD-002,كابل كهرباء نحاسي 2.5 مم,Goods,EA,أدوات كهربائية,دلتا,6221000100002,SUP-CAB
SRV-001,خدمة صيانة دورية,Service,HOUR,خدمات عامة,النخبة,,CLI-SRV`;

export function CatalogImportDialog({
  isOpen,
  onOpenChange,
  onImportCompleted
}: CatalogImportDialogProps): React.JSX.Element {
  const { t } = useTranslation("catalog");
  const [currentStep, setCurrentStep] = useState<ImportStep>("upload");
  const [fileName, setFileName] = useState("catalog-import-sample.csv");
  const [csvContent, setCsvContent] = useState(DEFAULT_SAMPLE_CSV);
  const [headers, setHeaders] = useState<string[]>([]);
  const [mapping, setMapping] = useState<ColumnMapping>({
    name: "اسم_المنتج",
    baseUom: "وحدة_الأساس",
    sku: "رمز_المنتج",
    type: "نوع_المنتج",
    nameAr: "",
    nameEn: "",
    category: "الفئة",
    brand: "العلامة_التجارية",
    barcode: "الباركود",
    externalReference: "المرجع_الخارجي"
  });

  const [previewSummary, setPreviewSummary] = useState<CatalogImportSummary | null>(null);
  const [reconciliation, setReconciliation] = useState<CatalogImportSummary | null>(null);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  const previewMutation = useImportPreview();
  const commitMutation = useImportCommit();

  const resetState = () => {
    setCurrentStep("upload");
    setPreviewSummary(null);
    setReconciliation(null);
    setErrorMsg(null);
  };

  const handleClose = (open: boolean) => {
    if (!open) resetState();
    onOpenChange(open);
  };

  const parseHeadersFromContent = (text: string) => {
    const lines = text.trim().split(/\r?\n/).filter((l) => l.trim().length > 0);
    if (lines.length < 2) {
      setErrorMsg(t("importDialog.errorNoRows"));
      return [];
    }
    const firstLine = lines[0];
    if (!firstLine) return [];
    const cols = firstLine.split(",").map((c) => c.trim().replace(/^"|"$/g, ""));
    setHeaders(cols);

    // Auto-map columns by keywords
    const findCol = (keywords: string[]) =>
      cols.find((c) => keywords.some((k) => c.toLowerCase().includes(k.toLowerCase()))) || "";

    setMapping({
      name: findCol(["اسم", "name", "display"]),
      baseUom: findCol(["وحدة", "uom", "unit"]),
      sku: findCol(["sku", "رمز", "code"]),
      type: findCol(["نوع", "type", "nature"]),
      nameAr: findCol(["عربي", "ar"]),
      nameEn: findCol(["إنجليزي", "en"]),
      category: findCol(["فئة", "category"]),
      brand: findCol(["علامة", "brand", "ماركة"]),
      barcode: findCol(["باركود", "barcode"]),
      externalReference: findCol(["مرجع", "ref", "external"])
    });
    return cols;
  };

  const handleFileUpload = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    setFileName(file.name);
    const reader = new FileReader();
    reader.onload = (event) => {
      const text = String(event.target?.result ?? "");
      setCsvContent(text);
      parseHeadersFromContent(text);
    };
    reader.readAsText(file);
  };

  const handleGoToMapping = () => {
    const cols = parseHeadersFromContent(csvContent);
    if (cols.length > 0) {
      setCurrentStep("mapping");
      setErrorMsg(null);
    }
  };

  const handleRunPreview = async () => {
    if (!mapping.name || !mapping.baseUom) {
      setErrorMsg(t("importDialog.mappingRequiredError"));
      return;
    }

    setErrorMsg(null);
    const columnMappingRecord: Record<string, string> = {
      name: mapping.name,
      baseUom: mapping.baseUom
    };
    if (mapping.sku) columnMappingRecord["sku"] = mapping.sku;
    if (mapping.type) columnMappingRecord["type"] = mapping.type;
    if (mapping.nameAr) columnMappingRecord["nameAr"] = mapping.nameAr;
    if (mapping.nameEn) columnMappingRecord["nameEn"] = mapping.nameEn;
    if (mapping.category) columnMappingRecord["category"] = mapping.category;
    if (mapping.brand) columnMappingRecord["brand"] = mapping.brand;
    if (mapping.barcode) columnMappingRecord["barcode"] = mapping.barcode;
    if (mapping.externalReference) columnMappingRecord["externalReference"] = mapping.externalReference;

    try {
      const summary = await previewMutation.mutateAsync({
        fileName,
        csvContent,
        columnMapping: columnMappingRecord
      });
      setPreviewSummary(summary);
      setCurrentStep("preview");
    } catch (err: unknown) {
      const problem = (err as { problemDetails?: { extensions?: { code?: string } } })?.problemDetails;
      setErrorMsg(problem?.extensions?.code ? t(`errors.${problem.extensions.code}`, { defaultValue: t("importDialog.previewFailed") }) : t("importDialog.previewFailed"));
    }
  };

  const handleCommit = async () => {
    if (!previewSummary?.batchId) return;
    try {
      const result = await commitMutation.mutateAsync(previewSummary.batchId);
      setReconciliation(result);
      setCurrentStep("reconciliation");
      onImportCompleted?.();
    } catch {
      setErrorMsg(t("importDialog.commitFailed"));
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={handleClose}
      title={t("importDialog.title")}
      size="large"
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
        {errorMsg ? (
          <Alert variant="danger" title={t("common.error")}>{errorMsg}</Alert>
        ) : null}

        {/* Step indicator */}
        <div style={{ display: "flex", gap: "0.5rem", borderBottom: "1px solid var(--color-border-subtle)", paddingBottom: "0.75rem" }}>
          <span style={{ fontWeight: currentStep === "upload" ? 700 : 400, color: currentStep === "upload" ? "var(--color-brand-primary)" : "var(--color-content-secondary)" }}>
            {t("importDialog.stepUpload")}
          </span>
          <span>→</span>
          <span style={{ fontWeight: currentStep === "mapping" ? 700 : 400, color: currentStep === "mapping" ? "var(--color-brand-primary)" : "var(--color-content-secondary)" }}>
            {t("importDialog.stepMapping")}
          </span>
          <span>→</span>
          <span style={{ fontWeight: currentStep === "preview" ? 700 : 400, color: currentStep === "preview" ? "var(--color-brand-primary)" : "var(--color-content-secondary)" }}>
            {t("importDialog.stepPreview")}
          </span>
          <span>→</span>
          <span style={{ fontWeight: currentStep === "reconciliation" ? 700 : 400, color: currentStep === "reconciliation" ? "var(--color-brand-primary)" : "var(--color-content-secondary)" }}>
            {t("importDialog.stepReconciliation")}
          </span>
        </div>

        {/* Step 1: Upload */}
        {currentStep === "upload" ? (
          <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
            <p style={{ color: "var(--color-content-secondary)", fontSize: "0.875rem" }}>
              {t("importDialog.uploadDescription")}
            </p>
            <div style={{ display: "flex", gap: "1rem", alignItems: "center" }}>
              <input
                type="file"
                accept=".csv"
                onChange={handleFileUpload}
                style={{ fontSize: "0.875rem" }}
              />
              <Button
                variant="secondary"
                size="compact"
                onPress={() => {
                  setCsvContent(DEFAULT_SAMPLE_CSV);
                  setFileName("sample.csv");
                  parseHeadersFromContent(DEFAULT_SAMPLE_CSV);
                }}
              >
                {t("importDialog.loadSampleTemplate")}
              </Button>
            </div>
            <div style={{ display: "flex", flexDirection: "column", gap: "0.25rem" }}>
              <label style={{ fontSize: "0.8125rem", fontWeight: 600 }}>{t("importDialog.csvDataLabel")}</label>
              <textarea
                rows={6}
                value={csvContent}
                onChange={(e) => {
                  setCsvContent(e.target.value);
                  parseHeadersFromContent(e.target.value);
                }}
                style={{
                  fontFamily: "var(--font-mono, monospace)",
                  fontSize: "0.75rem",
                  padding: "0.5rem",
                  border: "1px solid var(--color-border-default)",
                  borderRadius: "var(--radius-sm)",
                  background: "var(--color-surface-subtle)"
                }}
              />
            </div>
            <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem" }}>
              <Button variant="secondary" onPress={() => handleClose(false)}>
                {t("common.cancel")}
              </Button>
              <Button variant="primary" onPress={handleGoToMapping}>
                {t("importDialog.nextMapping")}
              </Button>
            </div>
          </div>
        ) : null}

        {/* Step 2: Mapping */}
        {currentStep === "mapping" ? (
          <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
            <p style={{ color: "var(--color-content-secondary)", fontSize: "0.875rem" }}>
              {t("importDialog.mappingDescription")}
            </p>
            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
              <Select
                label={t("importDialog.mapName")}
                selectedKey={mapping.name}
                onSelectionChange={(key) => setMapping({ ...mapping, name: String(key) })}
                isRequired
              >
                {headers.map((h) => (
                  <SelectItem key={h} id={h}>{h}</SelectItem>
                ))}
              </Select>

              <Select
                label={t("importDialog.mapBaseUom")}
                selectedKey={mapping.baseUom}
                onSelectionChange={(key) => setMapping({ ...mapping, baseUom: String(key) })}
                isRequired
              >
                {headers.map((h) => (
                  <SelectItem key={h} id={h}>{h}</SelectItem>
                ))}
              </Select>

              <Select
                label={t("importDialog.mapSku")}
                selectedKey={mapping.sku}
                onSelectionChange={(key) => setMapping({ ...mapping, sku: String(key) })}
              >
                <SelectItem id="">{t("importDialog.none")}</SelectItem>
                {headers.map((h) => (
                  <SelectItem key={h} id={h}>{h}</SelectItem>
                ))}
              </Select>

              <Select
                label={t("importDialog.mapType")}
                selectedKey={mapping.type}
                onSelectionChange={(key) => setMapping({ ...mapping, type: String(key) })}
              >
                <SelectItem id="">{t("importDialog.none")}</SelectItem>
                {headers.map((h) => (
                  <SelectItem key={h} id={h}>{h}</SelectItem>
                ))}
              </Select>

              <Select
                label={t("importDialog.mapCategory")}
                selectedKey={mapping.category}
                onSelectionChange={(key) => setMapping({ ...mapping, category: String(key) })}
              >
                <SelectItem id="">{t("importDialog.none")}</SelectItem>
                {headers.map((h) => (
                  <SelectItem key={h} id={h}>{h}</SelectItem>
                ))}
              </Select>

              <Select
                label={t("importDialog.mapBrand")}
                selectedKey={mapping.brand}
                onSelectionChange={(key) => setMapping({ ...mapping, brand: String(key) })}
              >
                <SelectItem id="">{t("importDialog.none")}</SelectItem>
                {headers.map((h) => (
                  <SelectItem key={h} id={h}>{h}</SelectItem>
                ))}
              </Select>

              <Select
                label={t("importDialog.mapBarcode")}
                selectedKey={mapping.barcode}
                onSelectionChange={(key) => setMapping({ ...mapping, barcode: String(key) })}
              >
                <SelectItem id="">{t("importDialog.none")}</SelectItem>
                {headers.map((h) => (
                  <SelectItem key={h} id={h}>{h}</SelectItem>
                ))}
              </Select>

              <Select
                label={t("importDialog.mapExternalRef")}
                selectedKey={mapping.externalReference}
                onSelectionChange={(key) => setMapping({ ...mapping, externalReference: String(key) })}
              >
                <SelectItem id="">{t("importDialog.none")}</SelectItem>
                {headers.map((h) => (
                  <SelectItem key={h} id={h}>{h}</SelectItem>
                ))}
              </Select>
            </div>
            <div style={{ display: "flex", justifyContent: "space-between", marginTop: "1rem" }}>
              <Button variant="secondary" onPress={() => setCurrentStep("upload")}>
                {t("common.back")}
              </Button>
              <Button variant="primary" onPress={handleRunPreview} isLoading={previewMutation.isPending}>
                {t("importDialog.previewAction")}
              </Button>
            </div>
          </div>
        ) : null}

        {/* Step 3: Preview */}
        {currentStep === "preview" && previewSummary ? (
          <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
            <div style={{ display: "grid", gridTemplateColumns: "repeat(4, 1fr)", gap: "0.75rem", textAlign: "center" }}>
              <div style={{ padding: "0.5rem", background: "var(--color-surface-subtle)", borderRadius: "var(--radius-sm)" }}>
                <span style={{ fontSize: "1.25rem", fontWeight: 700 }}>{previewSummary.totalRows}</span>
                <p style={{ fontSize: "0.75rem", color: "var(--color-content-secondary)", margin: 0 }}>{t("importDialog.totalRows")}</p>
              </div>
              <div style={{ padding: "0.5rem", background: "var(--color-surface-subtle)", borderRadius: "var(--radius-sm)" }}>
                <span style={{ fontSize: "1.25rem", fontWeight: 700, color: "var(--color-status-success)" }}>{previewSummary.createCount}</span>
                <p style={{ fontSize: "0.75rem", color: "var(--color-content-secondary)", margin: 0 }}>{t("importDialog.createCount")}</p>
              </div>
              <div style={{ padding: "0.5rem", background: "var(--color-surface-subtle)", borderRadius: "var(--radius-sm)" }}>
                <span style={{ fontSize: "1.25rem", fontWeight: 700, color: "var(--color-status-info)" }}>{previewSummary.updateCount}</span>
                <p style={{ fontSize: "0.75rem", color: "var(--color-content-secondary)", margin: 0 }}>{t("importDialog.updateCount")}</p>
              </div>
              <div style={{ padding: "0.5rem", background: "var(--color-surface-subtle)", borderRadius: "var(--radius-sm)" }}>
                <span style={{ fontSize: "1.25rem", fontWeight: 700, color: previewSummary.errorCount > 0 ? "var(--color-status-danger)" : "inherit" }}>{previewSummary.errorCount}</span>
                <p style={{ fontSize: "0.75rem", color: "var(--color-content-secondary)", margin: 0 }}>{t("importDialog.errorCount")}</p>
              </div>
            </div>

            <div style={{ maxHeight: "250px", overflowY: "auto", border: "1px solid var(--color-border-subtle)", borderRadius: "var(--radius-sm)" }}>
              <Table aria-label={t("importDialog.previewRowsLabel")}>
                <TableHeader>
                  <TableRow>
                    <TableHead>{t("importDialog.rowNum")}</TableHead>
                    <TableHead>{t("importDialog.classification")}</TableHead>
                    <TableHead>{t("importDialog.issues")}</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {previewSummary.rows.map((row) => (
                    <TableRow key={row.rowNumber}>
                      <TableCell>{row.rowNumber}</TableCell>
                      <TableCell>
                        <StatusBadge
                          status={row.classification === "Create" ? "Active" : row.classification === "Update" ? "Draft" : "Archived"}
                          label={row.classification}
                        />
                      </TableCell>
                      <TableCell>
                        {row.issues.length === 0 ? (
                          <span style={{ color: "var(--color-status-success)" }}>✓ {t("importDialog.validRow")}</span>
                        ) : (
                          <span style={{ color: "var(--color-status-danger)", fontSize: "0.75rem" }}>
                            {row.issues.join(", ")}
                          </span>
                        )}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>

            <div style={{ display: "flex", justifyContent: "space-between", marginTop: "1rem" }}>
              <Button variant="secondary" onPress={() => setCurrentStep("mapping")}>
                {t("common.back")}
              </Button>
              <Button
                variant="primary"
                onPress={handleCommit}
                isLoading={commitMutation.isPending}
                isDisabled={previewSummary.errorCount > 0 && previewSummary.createCount === 0 && previewSummary.updateCount === 0}
              >
                {t("importDialog.commitAction")}
              </Button>
            </div>
          </div>
        ) : null}

        {/* Step 4: Reconciliation */}
        {currentStep === "reconciliation" && reconciliation ? (
          <div style={{ display: "flex", flexDirection: "column", gap: "1rem", textAlign: "center" }}>
            <Alert
              variant="success"
              title={t("importDialog.reconciliationTitle")}
            >
              {t("importDialog.reconciliationMessage")}
            </Alert>

            <div style={{ display: "grid", gridTemplateColumns: "repeat(3, 1fr)", gap: "1rem", textAlign: "center", marginTop: "0.5rem" }}>
              <div style={{ padding: "0.75rem", background: "var(--color-surface-subtle)", borderRadius: "var(--radius-sm)" }}>
                <span style={{ fontSize: "1.5rem", fontWeight: 700, color: "var(--color-status-success)" }}>
                  {reconciliation.createCount || reconciliation.created || 0}
                </span>
                <p style={{ fontSize: "0.8125rem", color: "var(--color-content-secondary)", margin: 0 }}>
                  {t("importDialog.createdProducts")}
                </p>
              </div>
              <div style={{ padding: "0.75rem", background: "var(--color-surface-subtle)", borderRadius: "var(--radius-sm)" }}>
                <span style={{ fontSize: "1.5rem", fontWeight: 700, color: "var(--color-status-info)" }}>
                  {reconciliation.updateCount || reconciliation.updated || 0}
                </span>
                <p style={{ fontSize: "0.8125rem", color: "var(--color-content-secondary)", margin: 0 }}>
                  {t("importDialog.updatedProducts")}
                </p>
              </div>
              <div style={{ padding: "0.75rem", background: "var(--color-surface-subtle)", borderRadius: "var(--radius-sm)" }}>
                <span style={{ fontSize: "1.5rem", fontWeight: 700 }}>
                  {reconciliation.skippedCount || 0}
                </span>
                <p style={{ fontSize: "0.8125rem", color: "var(--color-content-secondary)", margin: 0 }}>
                  {t("importDialog.skippedProducts")}
                </p>
              </div>
            </div>

            <div style={{ display: "flex", justifyContent: "center", marginTop: "1rem" }}>
              <Button variant="primary" onPress={() => handleClose(false)}>
                {t("common.done")}
              </Button>
            </div>
          </div>
        ) : null}
      </div>
    </Dialog>
  );
}

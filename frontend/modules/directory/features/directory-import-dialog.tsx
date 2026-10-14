import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { Select, SelectItem } from "../../../components/primitives/select.tsx";
import { StatusBadge } from "../../../components/fields/status-badge.tsx";
import { useImportParties } from "../hooks/party-queries.ts";
import type { DirectoryImportRow, DirectoryImportResult, PartyType } from "../types.ts";

export interface DirectoryImportDialogProps {
  readonly isOpen: boolean;
  readonly onOpenChange: (open: boolean) => void;
  readonly onImportCompleted?: (() => void) | undefined;
}

type ImportStep = "upload" | "mapping" | "validate" | "preview" | "import" | "reconciliation";

interface ColumnMapping {
  externalReference: string;
  displayName: string;
  partyType: string;
  role: string;
  identifierValue: string;
  email: string;
  phone: string;
  city: string;
}

const DEFAULT_SAMPLE_CSV = `المرجع_الخارجي,اسم_المنشأة,نوع_الكيان,الدور,الرقم_الضريبي,البريد_الإلكتروني,رقم_الهاتف,المدينة
IMP-001,مؤسسة الرواد للحلول الرقمية,Organization,Customer,300554433220003,info@alrowad-solutions.com,+201091234567,القاهرة
IMP-002,شركة الشروق للتجارة والتوزيع,Organization,Supplier,300887766550003,procure@alshorouk-trade.com,+201123456789,الإسكندرية
IMP-003,د. طارق عبد الرحمن للاستشارات,Person,Customer,,tareq.consult@gmail.com,+201009876543,الجيزة`;

export function DirectoryImportDialog({
  isOpen,
  onOpenChange,
  onImportCompleted
}: DirectoryImportDialogProps): React.JSX.Element {
  const { t } = useTranslation("directory");
  const [currentStep, setCurrentStep] = useState<ImportStep>("upload");
  const [fileName, setFileName] = useState<string>("parties-import-sample.csv");
  const [rawRows, setRawRows] = useState<Record<string, string>[]>([]);
  const [headers, setHeaders] = useState<string[]>([]);
  const [mappings, setMappings] = useState<ColumnMapping>({
    externalReference: "المرجع_الخارجي",
    displayName: "اسم_المنشأة",
    partyType: "نوع_الكيان",
    role: t("importDialog.roleColumn"),
    identifierValue: "الرقم_الضريبي",
    email: "البريد_الإلكتروني",
    phone: "رقم_الهاتف",
    city: "المدينة"
  });

  const [validationResult, setValidationResult] = useState<DirectoryImportResult | null>(null);
  const [executionResult, setExecutionResult] = useState<DirectoryImportResult | null>(null);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  const importParties = useImportParties();

  const resetState = () => {
    setCurrentStep("upload");
    setRawRows([]);
    setHeaders([]);
    setValidationResult(null);
    setExecutionResult(null);
    setErrorMsg(null);
  };

  const handleClose = (open: boolean) => {
    if (!open) resetState();
    onOpenChange(open);
  };

  const parseCsvText = (text: string) => {
    const lines = text.trim().split(/\r?\n/).filter(l => l.trim().length > 0);
    if (lines.length < 2) {
      setErrorMsg("الملف لا يحتوي على صفوف بيانات كافية.");
      return;
    }
    const firstLine = lines[0];
    if (!firstLine) return;
    const headerCols = firstLine.split(",").map(c => c.trim().replace(/^"|"$/g, ""));
    const dataRows = lines.slice(1).map(line => {
      const vals = line.split(",").map(c => c.trim().replace(/^"|"$/g, ""));
      const obj: Record<string, string> = {};
      headerCols.forEach((col, idx) => {
        obj[col] = vals[idx] ?? "";
      });
      return obj;
    });

    setHeaders(headerCols);
    setRawRows(dataRows);
    setErrorMsg(null);

    // Auto-map if possible
    setMappings({
      externalReference: headerCols.find(h => h.includes("مرجع") || h.toLowerCase().includes("ref")) || headerCols[0] || "",
      displayName: headerCols.find(h => h.includes("اسم") || h.toLowerCase().includes("name")) || headerCols[1] || "",
      partyType: headerCols.find(h => h.includes("نوع") || h.toLowerCase().includes("type")) || "",
      role: headerCols.find(h => h.includes("دور") || h.toLowerCase().includes("role")) || "",
      identifierValue: headerCols.find(h => h.includes("ضريب") || h.toLowerCase().includes("tax") || h.toLowerCase().includes("vat")) || "",
      email: headerCols.find(h => h.includes("بريد") || h.toLowerCase().includes("email")) || "",
      phone: headerCols.find(h => h.includes("هاتف") || h.toLowerCase().includes("phone")) || "",
      city: headerCols.find(h => h.includes("مدين") || h.toLowerCase().includes("city")) || ""
    });
  };

  const handleLoadSample = () => {
    setFileName("parties-import-sample.csv");
    parseCsvText(DEFAULT_SAMPLE_CSV);
    setCurrentStep("mapping");
  };

  const handleFileUpload = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    setFileName(file.name);
    const reader = new FileReader();
    reader.onload = (event) => {
      const content = event.target?.result as string;
      if (content) {
        parseCsvText(content);
        setCurrentStep("mapping");
      }
    };
    reader.readAsText(file);
  };

  const handleDownloadTemplate = () => {
    const blob = new Blob(["\uFEFF" + DEFAULT_SAMPLE_CSV], { type: "text/csv;charset=utf-8;" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = "نموذج_استيراد_الأطراف_التجارية.csv";
    link.click();
    URL.revokeObjectURL(url);
  };

  const buildMappedRows = (): DirectoryImportRow[] => {
    return rawRows.map((r, idx) => {
      const pType = mappings.partyType ? r[mappings.partyType] : "Organization";
      const partyType: PartyType = (pType === "Person" || pType === t("importDialog.individual") || pType === "فرد مستقل") ? "Person" : "Organization";
      const roleVal = mappings.role ? r[mappings.role] : "Customer";
      const role = (roleVal === "Supplier" || roleVal === t("importDialog.supplier")) ? "Supplier" : "Customer";
      const extRef = mappings.externalReference ? r[mappings.externalReference] : `IMP-${Date.now()}-${idx + 1}`;
      const name = mappings.displayName ? r[mappings.displayName] : `طرف مستورد ${idx + 1}`;

      return {
        externalReference: extRef || `IMP-${Date.now()}-${idx + 1}`,
        partyType,
        displayName: name || `طرف مستورد ${idx + 1}`,
        role,
        identifierType: mappings.identifierValue && r[mappings.identifierValue] ? "TaxId" : undefined,
        identifierValue: mappings.identifierValue && r[mappings.identifierValue] ? r[mappings.identifierValue] : undefined,
        email: mappings.email && r[mappings.email] ? r[mappings.email] : undefined,
        phone: mappings.phone && r[mappings.phone] ? r[mappings.phone] : undefined,
        city: mappings.city && r[mappings.city] ? r[mappings.city] : undefined
      };
    });
  };

  const handleStartValidation = async () => {
    if (!mappings.displayName) {
      setErrorMsg("يرجى اختيار عمود اسم الطرف التجاري.");
      return;
    }
    setErrorMsg(null);
    try {
      const rows = buildMappedRows();
      const res = await importParties.mutateAsync({ rows, commit: false });
      setValidationResult(res);
      setCurrentStep("validate");
    } catch (err: unknown) {
      setErrorMsg("تعذر فحص الملف ومراجعة السجلات. راجع مطابقة الأعمدة ثم حاول مرة أخرى.");
    }
  };

  const handleExecuteImport = async () => {
    setErrorMsg(null);
    setCurrentStep("import");
    try {
      const rows = buildMappedRows();
      const res = await importParties.mutateAsync({ rows, commit: true });
      setExecutionResult(res);
      setCurrentStep("reconciliation");
      onImportCompleted?.();
    } catch (err: unknown) {
      setCurrentStep("preview");
      setErrorMsg("تعذر تنفيذ الاستيراد. لم يتم اعتماد أي سجل، ويمكنك مراجعة المعاينة والمحاولة مرة أخرى.");
    }
  };

  const handleDownloadReport = () => {
    if (!executionResult) return;
    const header = "المرجع_الخارجي,الإجراء_المنفذ,معرف_الطرف,الأخطاء\n";
    const body = executionResult.rows.map(r => `${r.externalReference},${r.action},${r.partyId || ""},"${r.errors.join("; ")}"`).join("\n");
    const blob = new Blob(["\uFEFF" + header + body], { type: "text/csv;charset=utf-8;" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = `تقرير_استيراد_الأطراف_${new Date().toISOString().slice(0, 10)}.csv`;
    link.click();
    URL.revokeObjectURL(url);
  };

  const mappedRows = buildMappedRows();

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={handleClose}
      title={t("importDialog.title")}
      description={t("importDialog.description")}
      size="large"
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem", paddingTop: "0.5rem" }}>
        {/* Step Indicator Header */}
        <div
          style={{
            display: "flex",
            justifyContent: "space-between",
            flexWrap: "wrap",
            gap: "0.5rem 1rem",
            padding: "0.75rem 1rem",
            backgroundColor: "var(--color-surface-sunken, #f8fafc)",
            borderRadius: "var(--radius-md)",
            border: "1px solid var(--color-border-subtle, #e2e8f0)",
            fontSize: "0.8125rem",
            fontWeight: 600
          }}
        >
          <span style={{ color: currentStep === "upload" ? "var(--color-brand-primary)" : "var(--color-content-secondary)" }}>
            {t("importDialog.stepUpload")}
          </span>
          <span style={{ color: currentStep === "mapping" ? "var(--color-brand-primary)" : "var(--color-content-secondary)" }}>
            {t("importDialog.stepMapping")}
          </span>
          <span style={{ color: currentStep === "validate" ? "var(--color-brand-primary)" : "var(--color-content-secondary)" }}>
            {t("importDialog.stepReview")}
          </span>
          <span style={{ color: currentStep === "preview" ? "var(--color-brand-primary)" : "var(--color-content-secondary)" }}>
            {t("importDialog.stepPreview")}
          </span>
          <span style={{ color: currentStep === "import" ? "var(--color-brand-primary)" : "var(--color-content-secondary)" }}>
            {t("importDialog.stepImport")}
          </span>
          <span style={{ color: currentStep === "reconciliation" ? "var(--color-brand-primary)" : "var(--color-content-secondary)" }}>
            {t("importDialog.stepReconcile")}
          </span>
        </div>

        {errorMsg && (
          <div
            style={{
              padding: "0.75rem 1rem",
              borderRadius: "var(--radius-md)",
              backgroundColor: "var(--color-status-danger-subtle, #fef2f2)",
              color: "var(--color-status-danger-content, #991b1b)",
              fontSize: "0.875rem",
              border: "1px solid var(--color-status-danger, #f87171)"
            }}
          >
            {errorMsg}
          </div>
        )}

        {/* STEP 1: UPLOAD */}
        {currentStep === "upload" && (
          <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
            <div
              style={{
                border: "2px dashed var(--color-border-subtle, #cbd5e1)",
                borderRadius: "var(--radius-lg, 0.75rem)",
                padding: "2.5rem 1.5rem",
                textAlign: "center",
                display: "flex",
                flexDirection: "column",
                alignItems: "center",
                gap: "0.75rem",
                backgroundColor: "var(--color-surface-elevated, #ffffff)"
              }}
            >
              <div style={{ fontSize: "2rem" }}>📄</div>
              <p style={{ margin: 0, fontWeight: 700, fontSize: "1rem" }}>
                {t("importDialog.chooseCsv")}
              </p>
              <p style={{ margin: 0, fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
                {t("importDialog.csvHelp")}
              </p>

              <label
                style={{
                  display: "inline-block",
                  padding: "0.5rem 1.25rem",
                  backgroundColor: "var(--color-brand-primary, #4f46e5)",
                  color: "#ffffff",
                  borderRadius: "var(--radius-md)",
                  fontWeight: 600,
                  fontSize: "0.875rem",
                  cursor: "pointer",
                  marginTop: "0.5rem"
                }}
              >
                {t("importDialog.browse")}
                <input
                  type="file"
                  accept=".csv,text/csv"
                  onChange={handleFileUpload}
                  style={{ display: "none" }}
                />
              </label>
            </div>

            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", paddingTop: "0.5rem" }}>
              <Button variant="secondary" size="compact" onPress={handleDownloadTemplate}>
                {t("importDialog.downloadTemplate")}
              </Button>
              <Button variant="primary" size="compact" onPress={handleLoadSample}>
                {t("importDialog.sampleData")}
              </Button>
            </div>
          </div>
        )}

        {/* STEP 2: COLUMN MAPPING */}
        {currentStep === "mapping" && (
          <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
            <div style={{ fontSize: "0.875rem", color: "var(--color-content-secondary)" }}>
              {t("importDialog.selectedFile")} <strong>{fileName}</strong> ({rawRows.length} {t("importDialog.mappingPrompt")}
            </div>

            <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(240px, 1fr))", gap: "1rem" }}>
              <div>
                <Select
                  label={t("importDialog.displayName")}
                  selectedKey={mappings.displayName}
                  onSelectionChange={(k) => setMappings({ ...mappings, displayName: String(k ?? "") })}
                >
                  {headers.map(h => <SelectItem key={h} id={h} textValue={h}>{h}</SelectItem>)}
                </Select>
              </div>

              <div>
                <Select
                  label={t("importDialog.externalReference")}
                  selectedKey={mappings.externalReference}
                  onSelectionChange={(k) => setMappings({ ...mappings, externalReference: String(k ?? "") })}
                >
                  {headers.map(h => <SelectItem key={h} id={h} textValue={h}>{h}</SelectItem>)}
                </Select>
              </div>

              <div>
                <Select
                  label={t("importDialog.partyType")}
                  selectedKey={mappings.partyType}
                  onSelectionChange={(k) => setMappings({ ...mappings, partyType: String(k ?? "") })}
                >
                  <SelectItem id="" textValue={t("importDialog.defaultOrganization")}>{t("importDialog.defaultOrganization")}</SelectItem>
                  {headers.map(h => <SelectItem key={h} id={h} textValue={h}>{h}</SelectItem>)}
                </Select>
              </div>

              <div>
                <Select
                  label={t("importDialog.role")}
                  selectedKey={mappings.role}
                  onSelectionChange={(k) => setMappings({ ...mappings, role: String(k ?? "") })}
                >
                  <SelectItem id="" textValue={t("importDialog.defaultCustomer")}>{t("importDialog.defaultCustomer")}</SelectItem>
                  {headers.map(h => <SelectItem key={h} id={h} textValue={h}>{h}</SelectItem>)}
                </Select>
              </div>

              <div>
                <Select
                  label={t("importDialog.identifier")}
                  selectedKey={mappings.identifierValue}
                  onSelectionChange={(k) => setMappings({ ...mappings, identifierValue: String(k ?? "") })}
                >
                  <SelectItem id="" textValue={t("importDialog.none")}>{t("importDialog.none")}</SelectItem>
                  {headers.map(h => <SelectItem key={h} id={h} textValue={h}>{h}</SelectItem>)}
                </Select>
              </div>

              <div>
                <Select
                  label={t("importDialog.email")}
                  selectedKey={mappings.email}
                  onSelectionChange={(k) => setMappings({ ...mappings, email: String(k ?? "") })}
                >
                  <SelectItem id="" textValue={t("importDialog.none")}>{t("importDialog.none")}</SelectItem>
                  {headers.map(h => <SelectItem key={h} id={h} textValue={h}>{h}</SelectItem>)}
                </Select>
              </div>

              <div>
                <Select
                  label={t("importDialog.phone")}
                  selectedKey={mappings.phone}
                  onSelectionChange={(k) => setMappings({ ...mappings, phone: String(k ?? "") })}
                >
                  <SelectItem id="" textValue={t("importDialog.none")}>{t("importDialog.none")}</SelectItem>
                  {headers.map(h => <SelectItem key={h} id={h} textValue={h}>{h}</SelectItem>)}
                </Select>
              </div>

              <div>
                <Select
                  label={t("importDialog.city")}
                  selectedKey={mappings.city}
                  onSelectionChange={(k) => setMappings({ ...mappings, city: String(k ?? "") })}
                >
                  <SelectItem id="" textValue={t("importDialog.none")}>{t("importDialog.none")}</SelectItem>
                  {headers.map(h => <SelectItem key={h} id={h} textValue={h}>{h}</SelectItem>)}
                </Select>
              </div>
            </div>

            <div style={{ display: "flex", gap: "0.75rem", justifyContent: "flex-end", marginTop: "1rem" }}>
              <Button variant="secondary" onPress={() => setCurrentStep("upload")}>
                {t("importDialog.previous")}
              </Button>
              <Button variant="primary" onPress={handleStartValidation} isDisabled={importParties.isPending}>
                {importParties.isPending ? t("importDialog.validating") : t("importDialog.dryRun")}
              </Button>
            </div>
          </div>
        )}

        {/* STEP 3: VALIDATE & REVIEW DUPLICATES */}
        {currentStep === "validate" && validationResult && (
          <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
            <div
              style={{
                display: "grid",
                gridTemplateColumns: "repeat(auto-fit, minmax(140px, 1fr))",
                gap: "0.75rem",
                textAlign: "center"
              }}
            >
              <div style={{ padding: "0.75rem", borderRadius: "var(--radius-md)", backgroundColor: "var(--color-surface-sunken)" }}>
                <span style={{ fontSize: "1.25rem", fontWeight: 700, display: "block" }}>{validationResult.rows.length}</span>
                <span style={{ fontSize: "0.75rem", color: "var(--color-content-secondary)" }}>{t("importDialog.totalRecords")}</span>
              </div>
              <div style={{ padding: "0.75rem", borderRadius: "var(--radius-md)", backgroundColor: "var(--color-status-success-subtle)" }}>
                <span style={{ fontSize: "1.25rem", fontWeight: 700, display: "block", color: "var(--color-status-success-content)" }}>
                {validationResult.rows.filter(r => r.action === "Create").length}
                </span>
                <span style={{ fontSize: "0.75rem", color: "var(--color-status-success-content)" }}>{t("importDialog.newRecords")}</span>
              </div>
              <div style={{ padding: "0.75rem", borderRadius: "var(--radius-md)", backgroundColor: "var(--color-brand-subtle, #eff6ff)" }}>
                <span style={{ fontSize: "1.25rem", fontWeight: 700, display: "block", color: "var(--color-brand-primary)" }}>
                  {validationResult.rows.filter(r => r.action === "Update").length}
                </span>
                <span style={{ fontSize: "0.75rem", color: "var(--color-brand-primary)" }}>{t("importDialog.updatedRecord")}</span>
              </div>
              <div style={{ padding: "0.75rem", borderRadius: "var(--radius-md)", backgroundColor: "var(--color-status-warning-subtle)" }}>
                <span style={{ fontSize: "1.25rem", fontWeight: 700, display: "block", color: "var(--color-status-warning-content)" }}>
                  {validationResult.rows.filter(r => r.action === "Review").length}
                </span>
                <span style={{ fontSize: "0.75rem", color: "var(--color-status-warning-content)" }}>{t("importDialog.reviewRecords")}</span>
              </div>
              <div style={{ padding: "0.75rem", borderRadius: "var(--radius-md)", backgroundColor: "var(--color-status-danger-subtle)" }}>
                <span style={{ fontSize: "1.25rem", fontWeight: 700, display: "block", color: "var(--color-status-danger-content)" }}>
                  {validationResult.rows.filter(r => r.action === "Error" || r.errors.length > 0).length}
                </span>
                <span style={{ fontSize: "0.75rem", color: "var(--color-status-danger-content)" }}>{t("importDialog.dataErrors")}</span>
              </div>
            </div>

            <div style={{ maxHeight: "220px", overflowY: "auto", border: "1px solid var(--color-border-subtle)", borderRadius: "var(--radius-md)" }}>
              <table className="directory-import-table" style={{ width: "100%", borderCollapse: "collapse", fontSize: "0.8125rem", tableLayout: "fixed" }}>
                <thead>
                  <tr style={{ backgroundColor: "var(--color-surface-sunken)", borderBottom: "1px solid var(--color-border-subtle)" }}>
                    <th style={{ padding: "0.5rem 0.75rem", textAlign: "right" }}>{t("importDialog.reference")}</th>
                    <th style={{ padding: "0.5rem 0.75rem", textAlign: "right" }}>{t("importDialog.expectedAction")}</th>
                    <th style={{ padding: "0.5rem 0.75rem", textAlign: "right" }}>{t("importDialog.matchStatus")}</th>
                  </tr>
                </thead>
                <tbody>
                  {validationResult.rows.map((row, idx) => (
                    <tr key={idx} style={{ borderBottom: "1px solid var(--color-border-subtle)" }}>
                      <td style={{ padding: "0.5rem 0.75rem" }}>{row.externalReference}</td>
                      <td style={{ padding: "0.5rem 0.75rem" }}>
                        <StatusBadge
                          status={row.action === "Create" ? "success" : row.action === "Update" ? "info" : "warning"}
                          label={row.action === "Create" ? t("importDialog.create") : row.action === "Update" ? t("importDialog.update") : t("importDialog.duplicateReview")}
                          size="small"
                        />
                      </td>
                      <td style={{ padding: "0.5rem 0.75rem", color: "var(--color-content-muted)" }}>
                        {row.possibleMatches.length > 1
                          ? t("importDialog.possibleMatches", { value: row.possibleMatches.length })
                          : row.action === "Update"
                          ? t("importDialog.confirmedMatch")
                          : t("importDialog.noMatches")}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <div style={{ display: "flex", gap: "0.75rem", justifyContent: "flex-end" }}>
              <Button variant="secondary" onPress={() => setCurrentStep("mapping")}>
                {t("importDialog.previous")}
              </Button>
              <Button variant="primary" onPress={() => setCurrentStep("preview")}>
                {t("importDialog.previewChanges")}
              </Button>
            </div>
          </div>
        )}

        {/* STEP 4: PREVIEW CHANGES */}
        {currentStep === "preview" && (
          <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
            <div style={{ fontSize: "0.875rem", color: "var(--color-content-secondary)" }}>
              {t("importDialog.reviewBeforeCommit")}
            </div>

            <div style={{ maxHeight: "250px", overflowY: "auto", border: "1px solid var(--color-border-subtle)", borderRadius: "var(--radius-md)" }}>
              <table className="directory-import-table" style={{ width: "100%", borderCollapse: "collapse", fontSize: "0.8125rem", tableLayout: "fixed" }}>
                <thead>
                  <tr style={{ backgroundColor: "var(--color-surface-sunken)", borderBottom: "1px solid var(--color-border-subtle)" }}>
                    <th style={{ padding: "0.5rem 0.75rem", textAlign: "right" }}>{t("importDialog.reference")}</th>
                    <th style={{ padding: "0.5rem 0.75rem", textAlign: "right" }}>{t("importDialog.partyName")}</th>
                    <th style={{ padding: "0.5rem 0.75rem", textAlign: "right" }}>{t("importDialog.entity")}</th>
                    <th style={{ padding: "0.5rem 0.75rem", textAlign: "right" }}>{t("importDialog.roleColumn")}</th>
                    <th style={{ padding: "0.5rem 0.75rem", textAlign: "right" }}>{t("importDialog.taxIdentifier")}</th>
                  </tr>
                </thead>
                <tbody>
                  {mappedRows.map((r, i) => (
                    <tr key={i} style={{ borderBottom: "1px solid var(--color-border-subtle)" }}>
                      <td style={{ padding: "0.5rem 0.75rem" }}>{r.externalReference}</td>
                      <td style={{ padding: "0.5rem 0.75rem", fontWeight: 600 }}>{r.displayName}</td>
                      <td style={{ padding: "0.5rem 0.75rem" }}>{r.partyType === "Organization" ? t("importDialog.organization") : t("importDialog.individual")}</td>
                      <td style={{ padding: "0.5rem 0.75rem" }}>{r.role === "Customer" ? t("importDialog.customer") : t("importDialog.supplier")}</td>
                      <td style={{ padding: "0.5rem 0.75rem", fontFamily: "var(--font-mono, monospace)" }}>{r.identifierValue || "—"}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <div style={{ display: "flex", gap: "0.75rem", justifyContent: "flex-end" }}>
              <Button variant="secondary" onPress={() => setCurrentStep("validate")}>
                {t("importDialog.previous")}
              </Button>
              <Button variant="primary" onPress={handleExecuteImport} isDisabled={importParties.isPending}>
                {importParties.isPending ? t("importDialog.executing") : t("importDialog.confirmImport")}
              </Button>
            </div>
          </div>
        )}

        {currentStep === "import" && (
          <div style={{ display: "flex", flexDirection: "column", alignItems: "center", gap: "0.75rem", padding: "2rem 1rem", textAlign: "center" }}>
            <div aria-hidden="true" style={{ fontSize: "2rem" }}>⏳</div>
            <strong>{t("importDialog.processing")}</strong>
            <span style={{ color: "var(--color-content-secondary)", fontSize: "0.875rem" }}>{t("importDialog.keepOpen")}</span>
          </div>
        )}

        {/* STEP 6: RECONCILIATION REPORT */}
        {currentStep === "reconciliation" && executionResult && (
          <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem", textAlign: "center", padding: "1rem 0" }}>
            <div style={{ fontSize: "2.5rem" }}>✅</div>
            <div>
              <h3 style={{ margin: "0 0 0.5rem", fontSize: "1.125rem", fontWeight: 700 }}>
                {t("importDialog.completed")}
              </h3>
              <p style={{ margin: 0, fontSize: "0.875rem", color: "var(--color-content-secondary)" }}>
                {t("importDialog.reconciliationDescription")}
              </p>
            </div>

            <div
              style={{
                display: "flex",
                justifyContent: "center",
                gap: "2rem",
                padding: "1rem",
                borderRadius: "var(--radius-md)",
                backgroundColor: "var(--color-surface-sunken)"
              }}
            >
              <div>
                <span style={{ fontSize: "1.25rem", fontWeight: 700, color: "var(--color-status-success-content)" }}>
                  {executionResult.rows.filter(r => r.action === "Created").length}
                </span>
                <span style={{ fontSize: "0.8125rem", display: "block", color: "var(--color-content-muted)" }}>{t("importDialog.created")}</span>
              </div>
              <div>
                <span style={{ fontSize: "1.25rem", fontWeight: 700, color: "var(--color-brand-primary)" }}>
                  {executionResult.rows.filter(r => r.action === "Updated").length}
                </span>
                <span style={{ fontSize: "0.8125rem", display: "block", color: "var(--color-content-muted)" }}>{t("importDialog.updated")}</span>
              </div>
              <div>
                <span style={{ fontSize: "1.25rem", fontWeight: 700, color: "var(--color-status-warning-content)" }}>
                  {executionResult.rows.filter(r => r.action === "Review" || r.action === "Error").length}
                </span>
                <span style={{ fontSize: "0.8125rem", display: "block", color: "var(--color-content-muted)" }}>{t("importDialog.needsReview")}</span>
              </div>
            </div>

            <div style={{ display: "flex", gap: "0.75rem", justifyContent: "center" }}>
              <Button variant="secondary" size="compact" onPress={handleDownloadReport}>
                {t("importDialog.downloadReport")}
              </Button>
              <Button variant="primary" size="compact" onPress={() => handleClose(false)}>
                {t("importDialog.closeRefresh")}
              </Button>
            </div>
          </div>
        )}
      </div>
    </Dialog>
  );
}

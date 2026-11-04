import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import {
  useImportInputsPreviewMutation,
  useImportInputsCommitMutation
} from "../../hooks/use-payroll-queries.ts";
import { mapPayrollError } from "../../services/payroll-error-map.ts";
import type { PayrollImportRequest, PayrollImportRowRequest, PayrollImportResult } from "../../types.ts";

export interface PayrollImportDialogProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
}

function parseCsvLine(text: string): string[] {
  const result: string[] = [];
  let cur = "";
  let inQuotes = false;
  for (let i = 0; i < text.length; i++) {
    const ch = text[i];
    if (ch === '"') {
      if (inQuotes && text[i + 1] === '"') {
        cur += '"';
        i++;
      } else {
        inQuotes = !inQuotes;
      }
    } else if (ch === ',' && !inQuotes) {
      result.push(cur.trim());
      cur = "";
    } else {
      cur += ch;
    }
  }
  result.push(cur.trim());
  return result;
}

export function PayrollImportDialog({
  isOpen,
  onClose,
  legalEntityId
}: PayrollImportDialogProps): React.JSX.Element {
  const { t } = useTranslation("payroll");
  const previewMutation = useImportInputsPreviewMutation(legalEntityId);
  const commitMutation = useImportInputsCommitMutation(legalEntityId);

  const [rawText, setRawText] = useState<string>("");
  const [currentBatchRef, setCurrentBatchRef] = useState<string>("");
  const [previewResult, setPreviewResult] = useState<PayrollImportResult | null>(null);
  const [parsedRowsState, setParsedRowsState] = useState<readonly PayrollImportRowRequest[]>([]);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [step, setStep] = useState<"input" | "preview">("input");

  const reset = () => {
    setRawText("");
    setCurrentBatchRef("");
    setPreviewResult(null);
    setParsedRowsState([]);
    setErrorMessage(null);
    setStep("input");
  };

  const parseCsvToRows = (text: string): PayrollImportRowRequest[] => {
    const lines = text.trim().split(/\r?\n/).filter((l) => l.trim().length > 0);
    const rows: PayrollImportRowRequest[] = [];
    if (lines.length === 0) return rows;

    const firstLineParts = parseCsvLine(lines[0] || "").map((p) => p.toLowerCase());
    const hasHeader = firstLineParts.some((p) => p.includes("emp") || p.includes("component") || p.includes("amount"));
    const startIndex = hasHeader ? 1 : 0;

    for (let i = startIndex; i < lines.length; i++) {
      const line = lines[i];
      if (!line) continue;
      const parts = parseCsvLine(line);
      if (parts.length >= 3) {
        // Support:
        // Format 1 (6 cols): EmploymentId, ComponentCode, Amount, Currency, InputDate, Notes
        // Format 2 (7 cols): EmploymentId, ComponentCode, ComponentType, InputDate, Amount, Currency, Notes
        let employmentId = parts[0] || "";
        let componentCode = parts[1] || "TRANSPORT";
        let amount = 0;
        let currencyCode = "EGP";
        let inputDate = new Date().toISOString().slice(0, 10);
        let notes: string | null = null;

        if (parts.length >= 7) {
          // Format 2: employmentId, componentCode, componentType, inputDate, amount, currency, notes
          inputDate = parts[3] || inputDate;
          amount = parseFloat(parts[4] || "0") || 0;
          currencyCode = parts[5] || "EGP";
          notes = parts[6] || null;
        } else {
          // Format 1: employmentId, componentCode, amount, currency, inputDate, notes
          amount = parseFloat(parts[2] || "0") || 0;
          currencyCode = parts[3] || "EGP";
          inputDate = parts[4] || inputDate;
          notes = parts[5] || null;
        }

        rows.push({
          rowNumber: rows.length + 1,
          rowKey: `row-${rows.length + 1}`,
          employmentId,
          componentCode: componentCode.toUpperCase(),
          inputDate,
          amount,
          currencyCode,
          notes
        });
      }
    }
    return rows;
  };

  const handlePreview = async () => {
    setErrorMessage(null);
    const parsedRows = parseCsvToRows(rawText);
    if (parsedRows.length === 0) {
      setErrorMessage(t("inputs.importNoValidRows", "لم يتم العثور على أسطر صالحة للاستيراد"));
      return;
    }

    const batchRef = `BATCH-${Date.now()}`;
    setCurrentBatchRef(batchRef);
    setParsedRowsState(parsedRows);

    const request: PayrollImportRequest = {
      importType: "Inputs",
      sourceSystem: "CSV",
      batchReference: batchRef,
      fileSha256: "manual-entry",
      rows: parsedRows
    };

    try {
      const result = await previewMutation.mutateAsync(request);
      setPreviewResult(result);
      setStep("preview");
    } catch (err: unknown) {
      const mapped = mapPayrollError(err);
      setErrorMessage(t(mapped.messageKey, mapped.defaultMessage));
    }
  };

  const handleCommit = async () => {
    if (!previewResult || parsedRowsState.length === 0) return;
    setErrorMessage(null);

    const request: PayrollImportRequest = {
      importType: "Inputs",
      sourceSystem: "CSV",
      batchReference: currentBatchRef,
      fileSha256: "manual-entry",
      rows: parsedRowsState
    };

    try {
      await commitMutation.mutateAsync(request);
      reset();
      onClose();
    } catch (err: unknown) {
      const mapped = mapPayrollError(err);
      setErrorMessage(t(mapped.messageKey, mapped.defaultMessage));
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open: boolean) => {
        if (!open) {
          reset();
          onClose();
        }
      }}
      title={t("inputs.importModalTitle")}
      description={t("inputs.importModalDesc")}
      size="large"
      footer={
        <>
          <Button
            variant="secondary"
            onPress={() => {
              if (step === "preview") {
                setStep("input");
              } else {
                reset();
                onClose();
              }
            }}
            isDisabled={previewMutation.isPending || commitMutation.isPending}
          >
            {step === "preview" ? t("actions.back") : t("actions.cancel")}
          </Button>

          {step === "input" ? (
            <Button
              variant="primary"
              onPress={handlePreview}
              isDisabled={previewMutation.isPending || !rawText.trim()}
            >
              {previewMutation.isPending ? t("actions.processing") : t("inputs.previewImportAction")}
            </Button>
          ) : (
            <Button
              variant="primary"
              onPress={handleCommit}
              isDisabled={commitMutation.isPending || !previewResult}
            >
              {commitMutation.isPending
                ? t("actions.committing")
                : t("inputs.commitValidRows", { count: previewResult?.rowCount ?? 0 })}
            </Button>
          )}
        </>
      }
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
        {errorMessage && (
          <div
            role="alert"
            style={{
              padding: "0.625rem 0.875rem",
              borderRadius: "var(--radius-sm)",
              background: "var(--color-status-danger-subtle, #fee2e2)",
              color: "var(--color-status-danger, #b91c1c)",
              fontSize: "0.8125rem"
            }}
          >
            {errorMessage}
          </div>
        )}

        {step === "input" && (
          <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
            <div
              style={{
                padding: "0.875rem 1rem",
                borderRadius: "var(--radius-md)",
                background: "var(--color-surface-subtle)",
                border: "1px solid var(--color-border-default)",
                fontSize: "0.8125rem",
                color: "var(--color-content-secondary)",
                lineHeight: 1.6
              }}
            >
              <strong>{t("inputs.csvFormatGuide")}:</strong>
              <code
                style={{
                  display: "block",
                  direction: "ltr",
                  marginTop: "0.5rem",
                  padding: "0.5rem 0.75rem",
                  background: "var(--color-surface-card)",
                  border: "1px solid var(--color-border-default)",
                  borderRadius: "var(--radius-sm)",
                  fontFamily: "var(--font-mono)",
                  fontSize: "0.75rem"
                }}
              >
                {t("inputs.csvHeaderSample")}
                <br />
                {t("inputs.csvSampleRow1")}
                <br />
                {t("inputs.csvSampleRow2")}
              </code>
            </div>

            <label style={{ display: "flex", flexDirection: "column", gap: "0.375rem" }}>
              <span style={{ fontSize: "0.875rem", fontWeight: 600, color: "var(--color-content-primary)" }}>
                {t("inputs.pasteCsvData")}
              </span>
              <textarea
                rows={10}
                value={rawText}
                onChange={(e) => setRawText(e.target.value)}
                placeholder={t("inputs.csvPlaceholder")}
                style={{
                  width: "100%",
                  padding: "0.75rem",
                  borderRadius: "var(--radius-md)",
                  border: "1px solid var(--color-border-default)",
                  backgroundColor: "var(--color-surface-card)",
                  color: "var(--color-content-primary)",
                  fontFamily: "var(--font-mono)",
                  fontSize: "0.8125rem",
                  boxSizing: "border-box",
                  outline: "none",
                  resize: "vertical",
                  direction: "ltr"
                }}
              />
            </label>
          </div>
        )}

        {step === "preview" && previewResult && (
          <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
            <div
              style={{
                display: "grid",
                gridTemplateColumns: "repeat(auto-fit, minmax(140px, 1fr))",
                gap: "0.75rem"
              }}
            >
              <div
                style={{
                  padding: "0.75rem 1rem",
                  borderRadius: "var(--radius-md)",
                  background: "var(--color-surface-subtle)",
                  border: "1px solid var(--color-border-default)"
                }}
              >
                <div style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>{t("inputs.totalRows")}</div>
                <div style={{ fontSize: "1.25rem", fontWeight: 700, color: "var(--color-content-primary)" }}>
                  {previewResult.rowCount}
                </div>
              </div>
            </div>

            <div
              style={{
                maxHeight: "20rem",
                overflowY: "auto",
                border: "1px solid var(--color-border-default)",
                borderRadius: "var(--radius-md)"
              }}
            >
              <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "0.8125rem" }}>
                <thead>
                  <tr style={{ background: "var(--color-surface-subtle)", borderBottom: "1px solid var(--color-border-default)", textAlign: "start" }}>
                    <th style={{ padding: "0.5rem 0.75rem" }}>#</th>
                    <th style={{ padding: "0.5rem 0.75rem" }}>{t("inputs.employee")}</th>
                    <th style={{ padding: "0.5rem 0.75rem" }}>{t("inputs.componentCode")}</th>
                    <th style={{ padding: "0.5rem 0.75rem" }}>{t("inputs.amount")}</th>
                    <th style={{ padding: "0.5rem 0.75rem" }}>{t("inputs.status")}</th>
                  </tr>
                </thead>
                <tbody>
                  {previewResult.rows.map((r, idx) => {
                    const parsed = parsedRowsState[r.rowNumber - 1];
                    const isOk = r.status === "Ready" || r.status === "Imported" || r.status === "Valid";
                    return (
                      <tr
                        key={idx}
                        style={{
                          borderBottom: "1px solid var(--color-border-subtle)",
                          background: isOk ? "transparent" : "var(--color-status-danger-subtle, #fef2f2)"
                        }}
                      >
                        <td style={{ padding: "0.5rem 0.75rem" }}>{r.rowNumber}</td>
                        <td style={{ padding: "0.5rem 0.75rem", fontFamily: "var(--font-mono)", fontSize: "0.75rem" }}>
                          {parsed?.employmentId || r.rowKey}
                        </td>
                        <td style={{ padding: "0.5rem 0.75rem", fontWeight: 600 }}>
                          {parsed?.componentCode || "—"}
                        </td>
                        <td style={{ padding: "0.5rem 0.75rem" }}>
                          {parsed ? `${parsed.amount.toLocaleString()} ${parsed.currencyCode}` : "—"}
                        </td>
                        <td style={{ padding: "0.5rem 0.75rem" }}>
                          <span
                            style={{
                              color: isOk ? "var(--color-status-success, #16a34a)" : "var(--color-status-danger, #dc2626)",
                              fontWeight: 600
                            }}
                          >
                            {r.issueCode ? t(`errors.${r.issueCode}`, r.issueCode) : r.status}
                          </span>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          </div>
        )}
      </div>
    </Dialog>
  );
}

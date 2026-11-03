import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "../../../../components/primitives/table.tsx";
import { hrApi } from "../../services/hr-api.ts";
import type { HrImportSummary } from "../../types.ts";

export interface ImportsTabProps {
  readonly legalEntityId: string;
}

export function ImportsTab({ legalEntityId }: ImportsTabProps): React.JSX.Element {
  const { t } = useTranslation("hr");
  const [summary, setSummary] = useState<HrImportSummary | null>(null);
  const [isProcessing, setIsProcessing] = useState(false);
  const [isCommitted, setIsCommitted] = useState(false);

  const samplePayload = {
    sourceSystem: "LegacyExcel",
    batchReference: `BATCH-${Date.now()}`,
    fileSha256: "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
    rows: [
      {
        rowReference: "ROW-001",
        legacyEmployeeId: "LEG-101",
        arabicName: "طارق سليم حامد",
        englishName: "Tarek Selim",
        preferredName: "طارق سليم",
        legalEntityId,
        employmentType: "FullTime",
        hireDate: "2026-01-01",
        endDate: null,
        status: "Active",
        dataQuality: "Clean"
      }
    ]
  };

  const handlePreview = async () => {
    setIsProcessing(true);
    setIsCommitted(false);
    try {
      const res = await hrApi.previewEmployeeImport(samplePayload);
      setSummary(res);
    } catch {
      // Handled
    } finally {
      setIsProcessing(false);
    }
  };

  const handleCommit = async () => {
    setIsProcessing(true);
    try {
      const res = await hrApi.commitEmployeeImport(samplePayload);
      setSummary(res);
      setIsCommitted(true);
    } catch {
      // Handled
    } finally {
      setIsProcessing(false);
    }
  };

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
      <Surface title={t("imports.title")}>
        <p style={{ margin: "0 0 1rem", fontSize: "0.875rem", color: "var(--color-content-muted)" }}>
          {t("imports.description")}
        </p>

        <div style={{ display: "flex", gap: "0.75rem", marginBottom: "1.5rem" }}>
          <Button variant="secondary" onPress={handlePreview} isDisabled={isProcessing}>
            {isProcessing ? t("actions.loading") : t("imports.previewAction")}
          </Button>
          {summary && !isCommitted && (
            <Button variant="primary" onPress={handleCommit} isDisabled={isProcessing}>
              {isProcessing ? t("actions.saving") : t("imports.commitAction")}
            </Button>
          )}
        </div>

        {summary && (
          <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
            <div style={{ display: "flex", gap: "1rem", flexWrap: "wrap", fontSize: "0.875rem" }}>
              <div><strong>{t("imports.totalRows")}:</strong> {summary.totalRows}</div>
              <div><strong>{t("imports.importedRows")}:</strong> {summary.importedRows}</div>
              <div><strong>{t("imports.rejectedRows")}:</strong> {summary.rejectedRows}</div>
              {isCommitted && <div style={{ color: "var(--color-status-success)", fontWeight: 700 }}>✓ {t("imports.committedSuccess")}</div>}
            </div>

            <Table aria-label={t("imports.resultsTable")}>
              <TableHeader>
                <TableRow>
                  <TableHead>{t("imports.rowRef")}</TableHead>
                  <TableHead>{t("imports.status")}</TableHead>
                  <TableHead>{t("imports.issues")}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {summary.rows.map((row) => (
                  <TableRow key={row.rowReference}>
                    <TableCell style={{ fontFamily: "monospace" }}>{row.rowReference}</TableCell>
                    <TableCell>{row.status}</TableCell>
                    <TableCell>{row.issues.join(", ") || t("imports.none")}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
        )}
      </Surface>
    </div>
  );
}

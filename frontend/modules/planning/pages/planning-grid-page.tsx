import React, { useEffect, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../components/pages/surface.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { TextArea } from "../../../components/primitives/text-area.tsx";
import { MoneyField } from "../../../components/fields/money-field.tsx";
import { MoneyDisplay } from "../../../components/fields/money-display.tsx";
import { Alert } from "../../../components/primitives/alert.tsx";
import { EmptyState } from "../../../components/primitives/empty-state.tsx";
import { LoadingState } from "../../../components/primitives/loading-state.tsx";
import { Pagination } from "../../../components/primitives/pagination.tsx";
import { BidiText } from "../../../components/primitives/bidi-text.tsx";
import { PlanningHeader } from "../components/planning-header.tsx";
import { PlanningStatusBadge } from "../components/planning-status-badge.tsx";
import { PlanningNoScopePrompt } from "../components/planning-no-scope-prompt.tsx";
import { PlanningContextBar } from "../components/planning-context-bar.tsx";
import { usePlanningScope } from "../hooks/use-planning-scope.ts";
import { usePlanningAccess } from "../hooks/use-planning-access.ts";
import {
  usePlanVersions,
  usePlanningGrid,
  useWriteGridBatch,
  useSpreadExact,
  useRecalculatePlanVersion,
  useRetryCalculationRun,
  useImportCsvPreview,
  useImportCsvCommit,
  useImportXlsxPreview,
  useImportXlsxCommit
} from "../hooks/use-planning-queries.ts";
import { planningApi } from "../services/planning-api.ts";
import { mapPlanningError } from "../services/planning-error-map.ts";
import { toMoney } from "../utils.ts";
import type { CalculationRunView, CsvImportView, GridCellView } from "../types.ts";

interface DirtyCell {
  readonly coordinateId: string;
  readonly metricCode: string;
  readonly periodStart: string;
  readonly dimensionKey: Record<string, unknown>;
  readonly rawValue: string;
}

function downloadBase64File(fileName: string, contentType: string, base64: string) {
  const bytes = Uint8Array.from(atob(base64), (c) => c.charCodeAt(0));
  const blob = new Blob([bytes], { type: contentType });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = fileName;
  a.click();
  URL.revokeObjectURL(url);
}

export function PlanningGridPage(): React.JSX.Element {
  const { t } = useTranslation("planning");
  const { legalEntityId, currencyCode, isReady } = usePlanningScope();
  const { canEditVersions, canExport } = usePlanningAccess();

  const { data: versions = [], isLoading: versionsLoading } = usePlanVersions(isReady);
  const [selectedVersionId, setSelectedVersionId] = useState("");
  const [fromPeriod, setFromPeriod] = useState("2027-01-01");
  const [toPeriod, setToPeriod] = useState("2027-12-01");
  const [metricFilter, setMetricFilter] = useState("");
  const [page, setPage] = useState(1);

  const [dirtyMap, setDirtyMap] = useState<Record<string, DirtyCell>>({});
  const [lastCalcRun, setLastCalcRun] = useState<CalculationRunView | null>(null);
  const [feedback, setFeedback] = useState<{ tone: "success" | "danger" | "warning"; text: string } | null>(null);

  // Modals
  const [isSpreadOpen, setIsSpreadOpen] = useState(false);
  const [spreadMetric, setSpreadMetric] = useState("Revenue");
  const [spreadTotal, setSpreadTotal] = useState("120000");

  const [isImportOpen, setIsImportOpen] = useState(false);
  const [csvText, setCsvText] = useState("");
  const [importPreviewResult, setImportPreviewResult] = useState<CsvImportView | null>(null);

  useEffect(() => {
    if (versions.length > 0 && !versions.some((v) => v.id === selectedVersionId)) {
      const draft = versions.find((v) => v.state === "Draft") ?? versions[0];
      if (draft) setSelectedVersionId(draft.id);
    }
  }, [versions, selectedVersionId]);

  const activeVersion = versions.find((v) => v.id === selectedVersionId);
  const isVersionLocked = Boolean(activeVersion && activeVersion.state !== "Draft");
  const canEditGrid = canEditVersions && !isVersionLocked;

  const gridQuery = usePlanningGrid(
    {
      planVersionId: selectedVersionId,
      legalEntityId,
      from: fromPeriod,
      to: toPeriod,
      metricCode: metricFilter.trim() || null,
      page,
      pageSize: 100
    },
    isReady && Boolean(selectedVersionId)
  );

  const writeBatch = useWriteGridBatch();
  const spreadExact = useSpreadExact();
  const recalculate = useRecalculatePlanVersion();
  const retryCalc = useRetryCalculationRun();
  const importCsvPreview = useImportCsvPreview();
  const importCsvCommit = useImportCsvCommit();
  const importXlsxPreview = useImportXlsxPreview();
  const importXlsxCommit = useImportXlsxCommit();

  const cells = gridQuery.data ?? [];

  const { periods, rows } = useMemo(() => {
    const periodSet = new Set<string>();
    const rowMap = new Map<string, Map<string, GridCellView>>();
    for (const c of cells) {
      periodSet.add(c.periodStart);
      if (!rowMap.has(c.metricCode)) {
        rowMap.set(c.metricCode, new Map());
      }
      rowMap.get(c.metricCode)!.set(c.periodStart, c);
    }
    return {
      periods: Array.from(periodSet).sort(),
      rows: Array.from(rowMap.entries())
    };
  }, [cells]);

  if (!isReady) {
    return <PlanningNoScopePrompt />;
  }

  const dirtyCount = Object.keys(dirtyMap).length;

  const handleCellChange = (cell: GridCellView, rawValue: string) => {
    const key = `${cell.metricCode}|${cell.periodStart}`;
    setDirtyMap((prev) => ({
      ...prev,
      [key]: {
        coordinateId: cell.coordinateId,
        metricCode: cell.metricCode,
        periodStart: cell.periodStart,
        dimensionKey: cell.dimensionKey,
        rawValue
      }
    }));
  };

  const handleSaveBatch = async () => {
    if (!activeVersion || dirtyCount === 0) return;
    setFeedback(null);
    try {
      const payloadCells = Object.values(dirtyMap).map((d) => {
        const trimmed = d.rawValue.trim();
        const isZero = trimmed === "0" || trimmed === "0.0" || trimmed === "0.00";
        const parsed = trimmed === "" ? null : Number(trimmed);
        return {
          legalEntityId,
          periodStart: d.periodStart,
          metricCode: d.metricCode,
          dimensionKey: d.dimensionKey,
          amount: parsed !== null && Number.isFinite(parsed) ? parsed : null,
          currencyCode,
          explicitZero: isZero,
          sourceLabel: "Planning Grid"
        };
      });

      await writeBatch.mutateAsync({
        planVersionId: activeVersion.id,
        cells: payloadCells,
        expectedVersion: activeVersion.rowVersion
      });
      setDirtyMap({});
      setFeedback({ tone: "success", text: t("messages.savedSuccess") });
    } catch (err) {
      const mapped = mapPlanningError(err);
      setFeedback({ tone: "danger", text: t(mapped.messageKey as never) });
    }
  };

  const handleRecalculate = async () => {
    if (!selectedVersionId) return;
    setFeedback(null);
    try {
      const run = await recalculate.mutateAsync(selectedVersionId);
      setLastCalcRun(run);
      setFeedback({
        tone: run.state === "Failed" ? "danger" : "success",
        text: run.state === "Failed" ? t("messages.recalcFailed") : t("messages.recalcStarted")
      });
    } catch (err) {
      const mapped = mapPlanningError(err);
      setFeedback({ tone: "danger", text: t(mapped.messageKey as never) });
    }
  };

  const handleRetryCalc = async () => {
    if (!lastCalcRun) return;
    setFeedback(null);
    try {
      const run = await retryCalc.mutateAsync(lastCalcRun.id);
      setLastCalcRun(run);
      setFeedback({ tone: "success", text: t("messages.recalcStarted") });
    } catch (err) {
      const mapped = mapPlanningError(err);
      setFeedback({ tone: "danger", text: t(mapped.messageKey as never) });
    }
  };

  const handleExportXlsx = async (isTemplate: boolean) => {
    if (!selectedVersionId) return;
    setFeedback(null);
    try {
      const file = isTemplate
        ? await planningApi.downloadTemplateXlsx(selectedVersionId)
        : await planningApi.exportXlsx(selectedVersionId, legalEntityId, fromPeriod, toPeriod, false);
      downloadBase64File(file.fileName, file.contentType, file.spreadsheetContentBase64);
    } catch (err) {
      const mapped = mapPlanningError(err);
      setFeedback({ tone: "danger", text: t(mapped.messageKey as never) });
    }
  };

  const handleXlsxFileUpload = async (file: File, commit: boolean) => {
    if (!selectedVersionId) return;
    setFeedback(null);
    try {
      const buffer = await file.arrayBuffer();
      const bytes = new Uint8Array(buffer);
      let binary = "";
      for (let i = 0; i < bytes.byteLength; i++) {
        binary += String.fromCharCode(bytes[i]!);
      }
      const base64 = btoa(binary);
      const res = commit
        ? await importXlsxCommit.mutateAsync({ planVersionId: selectedVersionId, spreadsheetContentBase64: base64, commit: true })
        : await importXlsxPreview.mutateAsync({ planVersionId: selectedVersionId, spreadsheetContentBase64: base64, commit: false });
      setImportPreviewResult(res);
      if (commit && res.committed) {
        setIsImportOpen(false);
        setFeedback({ tone: "success", text: t("messages.savedSuccess") });
      }
    } catch (err) {
      const mapped = mapPlanningError(err);
      setFeedback({ tone: "danger", text: t(mapped.messageKey as never) });
    }
  };

  const helpSteps = t("help.grid.workflowSteps", { returnObjects: true }) as readonly string[];
  const helpConcepts = t("help.grid.keyConcepts", { returnObjects: true }) as readonly string[];
  const helpTips = t("help.grid.proTips", { returnObjects: true }) as readonly string[];
  const helpFaqs = t("help.grid.faqs", { returnObjects: true }) as readonly string[];

  const helpContent = {
    title: t("navigation.grid"),
    summary: t("help.grid.summary"),
    workflowSteps: helpSteps.map((s, idx) => ({ step: idx + 1, title: s, description: s })),
    keyConcepts: helpConcepts.map((c) => ({ term: c, explanation: c })),
    proTips: helpTips,
    faqs: helpFaqs.map((f) => ({ question: f, answer: f }))
  };

  return (
    <div data-testid="planning-grid-page" style={{ padding: "0 0 2rem 0" }}>
      <PlanningHeader
        title={t("navigation.grid")}
        subtitle={t("subtitle")}
        primaryAction={
          canEditGrid && dirtyCount > 0
            ? {
                label: `${t("actions.saveChanges")} (${dirtyCount})`,
                onPress: () => void handleSaveBatch(),
                isLoading: writeBatch.isPending
              }
            : undefined
        }
        secondaryActions={
          <div style={{ display: "flex", gap: "0.5rem", flexWrap: "wrap" }}>
            {canEditGrid && dirtyCount > 0 ? (
              <Button variant="secondary" onPress={() => setDirtyMap({})}>
                {t("actions.discardChanges")}
              </Button>
            ) : null}
            {canEditGrid ? (
              <Button variant="secondary" onPress={() => setIsSpreadOpen(true)}>
                {t("actions.spreadExact")}
              </Button>
            ) : null}
            {canEditGrid ? (
              <Button variant="secondary" onPress={() => void handleRecalculate()} isDisabled={recalculate.isPending}>
                {t("actions.recalculate")}
              </Button>
            ) : null}
            {canEditGrid ? (
              <Button variant="secondary" onPress={() => {
                setImportPreviewResult(null);
                setIsImportOpen(true);
              }}>
                {t("actions.importXlsx")}
              </Button>
            ) : null}
            {canExport && selectedVersionId ? (
              <Button variant="secondary" onPress={() => void handleExportXlsx(false)}>
                {t("actions.exportXlsx")}
              </Button>
            ) : null}
            {canExport && selectedVersionId ? (
              <Button variant="secondary" onPress={() => void handleExportXlsx(true)}>
                {t("actions.downloadTemplate")}
              </Button>
            ) : null}
          </div>
        }
        help={helpContent}
      />

      {feedback ? (
        <div style={{ marginBottom: "1rem" }}>
          <Alert variant={feedback.tone}>{feedback.text}</Alert>
        </div>
      ) : null}

      {isVersionLocked ? (
        <div style={{ marginBottom: "1rem" }}>
          <Alert variant="warning">{t("messages.publishedLockedWarning")}</Alert>
        </div>
      ) : null}

      {lastCalcRun && lastCalcRun.state === "Failed" ? (
        <div style={{ marginBottom: "1rem", display: "flex", alignItems: "center", justifyContent: "space-between", gap: "1rem" }}>
          <Alert variant="danger">{`${t("messages.recalcFailed")} ${lastCalcRun.errorDetail ?? ""}`}</Alert>
          {canEditGrid ? (
            <Button variant="primary" onPress={() => void handleRetryCalc()} isDisabled={retryCalc.isPending}>
              {t("actions.retryCalculation")}
            </Button>
          ) : null}
        </div>
      ) : null}

      <PlanningContextBar
        versions={versions}
        selectedVersionId={selectedVersionId}
        onVersionChange={(id) => {
          setSelectedVersionId(id);
          setDirtyMap({});
          setPage(1);
        }}
        fromPeriod={fromPeriod}
        onFromPeriodChange={setFromPeriod}
        toPeriod={toPeriod}
        onToPeriodChange={setToPeriod}
        extraControls={
          <>
            <div style={{ width: "180px" }}>
              <TextField
                label={t("fields.metric")}
                value={metricFilter}
                onChange={(v) => {
                  setMetricFilter(v);
                  setPage(1);
                }}
              />
            </div>
            {activeVersion ? <PlanningStatusBadge status={activeVersion.state} /> : null}
          </>
        }
      />

      <Surface style={{ padding: "1.25rem" }}>
        {versionsLoading || gridQuery.isLoading ? (
          <LoadingState message={t("messages.loading")} />
        ) : rows.length === 0 ? (
          <EmptyState title={t("messages.noData")} />
        ) : (
          <>
            <div style={{ overflowX: "auto" }}>
              <table data-testid="planning-spreadsheet-grid" style={{ width: "100%", borderCollapse: "collapse", fontSize: "0.875rem" }}>
                <thead>
                  <tr style={{ borderBottom: "2px solid #cbd5e1", backgroundColor: "#f8fafc", textAlign: "start", color: "#334155" }}>
                    <th style={{ padding: "10px 12px", minWidth: "150px" }}>{t("fields.metric")}</th>
                    {periods.map((p) => (
                      <th key={p} style={{ padding: "10px 12px", minWidth: "130px" }}>
                        <BidiText>{p}</BidiText>
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {rows.map(([metricCode, cellByPeriod]) => (
                    <tr key={metricCode} style={{ borderBottom: "1px solid #e2e8f0" }}>
                      <td style={{ padding: "10px 12px", fontWeight: 700, color: "#102a43" }}>
                        <BidiText>{metricCode}</BidiText>
                      </td>
                      {periods.map((p) => {
                        const cell = cellByPeriod.get(p);
                        if (!cell) {
                          return <td key={p} style={{ padding: "8px 12px", color: "#94a3b8" }}>{"—"}</td>;
                        }
                        const key = `${cell.metricCode}|${cell.periodStart}`;
                        const dirty = dirtyMap[key];
                        const isActual = cell.actualAmount !== null && cell.actualAmount !== undefined;
                        const isCalc = cell.calculatedAmount !== null && cell.calculatedAmount !== undefined;
                        const cellReadOnly = !canEditGrid || isActual || isCalc;
                        const displayVal = dirty
                          ? dirty.rawValue
                          : cell.amount !== null && cell.amount !== undefined
                            ? String(cell.amount)
                            : "";

                        if (cellReadOnly) {
                          const numericReadVal = cell.actualAmount ?? cell.calculatedAmount ?? cell.amount;
                          const reasonTitle = isActual
                            ? t("messages.cellReadOnlyActual")
                            : isCalc
                              ? t("messages.cellReadOnlyCalculated")
                              : t("messages.cellReadOnlyLocked");
                          return (
                            <td
                              key={p}
                              title={reasonTitle}
                              style={{
                                padding: "8px 12px",
                                backgroundColor: "#f8fafc",
                                color: "#475569"
                              }}
                            >
                              {numericReadVal !== null && numericReadVal !== undefined ? (
                                <MoneyDisplay value={toMoney(numericReadVal, currencyCode)} />
                              ) : (
                                "—"
                              )}
                            </td>
                          );
                        }

                        return (
                          <td key={p} style={{ padding: "6px 8px" }}>
                            <input
                              type="number"
                              step="any"
                              aria-label={`${metricCode} ${p}`}
                              value={displayVal}
                              onChange={(e) => handleCellChange(cell, e.target.value)}
                              style={{
                                width: "100%",
                                padding: "6px 8px",
                                borderRadius: "6px",
                                border: dirty ? "2px solid #2563eb" : "1px solid #cbd5e1",
                                backgroundColor: dirty ? "#eff6ff" : "#ffffff",
                                color: "#0f172a",
                                fontVariantNumeric: "tabular-nums",
                                direction: "ltr",
                                textAlign: "end"
                              }}
                            />
                          </td>
                        );
                      })}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <div style={{ marginTop: "1rem", display: "flex", justifyContent: "flex-end" }}>
              <Pagination
                page={page}
                totalPages={cells.length >= 100 ? page + 1 : page}
                onPageChange={setPage}
              />
            </div>
          </>
        )}
      </Surface>

      {/* Exact Spread Modal */}
      <Dialog isOpen={isSpreadOpen} onClose={() => setIsSpreadOpen(false)} title={t("actions.spreadExact")} size="small">
        <form
          onSubmit={async (e) => {
            e.preventDefault();
            if (!selectedVersionId || periods.length === 0) return;
            try {
              await spreadExact.mutateAsync({
                planVersionId: selectedVersionId,
                legalEntityId,
                metricCode: spreadMetric,
                dimensionKey: {},
                total: Number(spreadTotal) || 0,
                method: "Equal",
                targetPeriods: periods,
                currencyCode
              });
              setIsSpreadOpen(false);
              setFeedback({ tone: "success", text: t("messages.savedSuccess") });
            } catch (err) {
              const mapped = mapPlanningError(err);
              setFeedback({ tone: "danger", text: t(mapped.messageKey as never) });
            }
          }}
          style={{ display: "flex", flexDirection: "column", gap: "1rem" }}
        >
          <TextField label={t("fields.metric")} value={spreadMetric} onChange={setSpreadMetric} isRequired />
          <MoneyField label={t("fields.amount")} value={{ amount: spreadTotal, currency: currencyCode }} onChange={(v) => setSpreadTotal(v.amount)} defaultCurrency={currencyCode} />
          <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem" }}>
            <Button type="button" variant="secondary" onPress={() => setIsSpreadOpen(false)}>{t("actions.cancel")}</Button>
            <Button type="submit" variant="primary" isDisabled={spreadExact.isPending}>{t("actions.spreadExact")}</Button>
          </div>
        </form>
      </Dialog>

      {/* Excel / CSV Import Modal */}
      <Dialog isOpen={isImportOpen} onClose={() => setIsImportOpen(false)} title={t("actions.importXlsx")} size="medium">
        <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
          <div>
            <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.35rem" }}>
              {t("actions.importXlsx")}
            </label>
            <input
              type="file"
              accept=".xlsx"
              onChange={(e) => {
                const file = e.target.files?.[0];
                if (file) void handleXlsxFileUpload(file, false);
              }}
            />
          </div>

          <TextArea
            label={t("actions.importCsv")}
            value={csvText}
            onChange={setCsvText}
            rows={4}
          />

          <div style={{ display: "flex", gap: "0.5rem" }}>
            <Button
              variant="secondary"
              isDisabled={!csvText.trim() || importCsvPreview.isPending}
              onPress={async () => {
                if (!selectedVersionId) return;
                const res = await importCsvPreview.mutateAsync({ planVersionId: selectedVersionId, csv: csvText, commit: false });
                setImportPreviewResult(res);
              }}
            >
              {t("actions.preview")}
            </Button>
            <Button
              variant="primary"
              isDisabled={!csvText.trim() || importCsvCommit.isPending}
              onPress={async () => {
                if (!selectedVersionId) return;
                const res = await importCsvCommit.mutateAsync({ planVersionId: selectedVersionId, csv: csvText, commit: true });
                setImportPreviewResult(res);
                if (res.committed) {
                  setIsImportOpen(false);
                  setFeedback({ tone: "success", text: t("messages.savedSuccess") });
                }
              }}
            >
              {t("actions.commit")}
            </Button>
          </div>

          {importPreviewResult ? (
            <Surface style={{ padding: "0.75rem", backgroundColor: "#f8fafc" }}>
              <div>{t("labels.acceptedRows")}: <strong>{importPreviewResult.acceptedRows}</strong></div>
              {importPreviewResult.errors.length > 0 ? (
                <ul style={{ margin: "0.5rem 0 0", color: "#dc2626", fontSize: "0.8rem" }}>
                  {importPreviewResult.errors.map((err, i) => (
                    <li key={i}>{err}</li>
                  ))}
                </ul>
              ) : null}
            </Surface>
          ) : null}
        </div>
      </Dialog>
    </div>
  );
}

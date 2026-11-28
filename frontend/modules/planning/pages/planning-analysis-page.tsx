import React, { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../components/pages/surface.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { Select } from "../../../components/primitives/select.tsx";
import { MoneyDisplay } from "../../../components/fields/money-display.tsx";
import { LocalDateDisplay } from "../../../components/fields/local-date-display.tsx";
import { Alert } from "../../../components/primitives/alert.tsx";
import { EmptyState } from "../../../components/primitives/empty-state.tsx";
import { LoadingState } from "../../../components/primitives/loading-state.tsx";
import { Tabs, TabList, Tab, TabPanel } from "../../../components/primitives/tabs.tsx";
import { BidiText } from "../../../components/primitives/bidi-text.tsx";
import { PlanningHeader } from "../components/planning-header.tsx";
import { PlanningStatusBadge } from "../components/planning-status-badge.tsx";
import { PlanningNoScopePrompt } from "../components/planning-no-scope-prompt.tsx";
import { PlanningContextBar } from "../components/planning-context-bar.tsx";
import { usePlanningScope } from "../hooks/use-planning-scope.ts";
import { usePlanningAccess } from "../hooks/use-planning-access.ts";
import {
  usePlanVersions,
  useVarianceQuery,
  useVarianceDecomposition,
  useStatementsQuery,
  useAddVarianceCommentary
} from "../hooks/use-planning-queries.ts";
import { mapPlanningError } from "../services/planning-error-map.ts";
import { toMoney } from "../utils.ts";

export function PlanningAnalysisPage(): React.JSX.Element {
  const { t } = useTranslation("planning");
  const { legalEntityId, currencyCode, isReady } = usePlanningScope();
  const { canReadVariance, canEditVersions } = usePlanningAccess();

  const { data: versions = [] } = usePlanVersions(isReady);
  const [selectedVersionId, setSelectedVersionId] = useState("");
  const [fromPeriod, setFromPeriod] = useState("2027-01-01");
  const [toPeriod, setToPeriod] = useState("2027-12-01");
  const [statementType, setStatementType] = useState("PnL");
  const [selectedCoordId, setSelectedCoordId] = useState<string | null>(null);
  const [commentBody, setCommentBody] = useState("");
  const [feedback, setFeedback] = useState<{ tone: "success" | "danger"; text: string } | null>(null);

  useEffect(() => {
    if (versions.length > 0 && !versions.some((v) => v.id === selectedVersionId)) {
      const published = versions.find((v) => v.state === "Published") ?? versions[0];
      if (published) setSelectedVersionId(published.id);
    }
  }, [versions, selectedVersionId]);

  const varianceQuery = useVarianceQuery(
    { planVersionId: selectedVersionId, legalEntityId, from: fromPeriod, to: toPeriod },
    isReady && canReadVariance && Boolean(selectedVersionId)
  );

  const statementsQuery = useStatementsQuery(
    { planVersionId: selectedVersionId, legalEntityId, from: fromPeriod, to: toPeriod, statementType, currencyCode },
    isReady && Boolean(selectedVersionId)
  );

  const decompositionQuery = useVarianceDecomposition(selectedCoordId);
  const addCommentary = useAddVarianceCommentary();

  if (!isReady) {
    return <PlanningNoScopePrompt />;
  }

  return (
    <div data-testid="planning-analysis-page" style={{ padding: "0 0 2rem 0" }}>
      <PlanningHeader
        title={t("navigation.analysis")}
        subtitle={t("subtitle")}
      />

      {feedback ? (
        <div style={{ marginBottom: "1rem" }}>
          <Alert variant={feedback.tone}>{feedback.text}</Alert>
        </div>
      ) : null}

      <Alert variant="info">{t("messages.projectionDisclaimer")}</Alert>

      <div style={{ marginTop: "1rem" }}>
        <PlanningContextBar
          versions={versions}
          selectedVersionId={selectedVersionId}
          onVersionChange={setSelectedVersionId}
          fromPeriod={fromPeriod}
          onFromPeriodChange={setFromPeriod}
          toPeriod={toPeriod}
          onToPeriodChange={setToPeriod}
        />
      </div>

      <Surface style={{ padding: "1.25rem" }}>
        <Tabs defaultSelectedKey={canReadVariance ? "variance" : "statements"}>
          <TabList aria-label={t("navigation.analysis")}>
            {canReadVariance ? <Tab id="variance">{t("labels.varianceAnalysis")}</Tab> : null}
            <Tab id="statements">{t("labels.projectedStatements")}</Tab>
          </TabList>

          {canReadVariance ? (
            <TabPanel id="variance">
              {varianceQuery.isLoading ? (
                <LoadingState message={t("messages.loading")} />
              ) : (varianceQuery.data ?? []).length === 0 ? (
                <EmptyState title={t("messages.noData")} />
              ) : (
                <div style={{ overflowX: "auto", marginTop: "1rem" }}>
                  <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "0.875rem" }}>
                    <thead>
                      <tr style={{ borderBottom: "2px solid #e2e8f0", textAlign: "start", color: "#475569" }}>
                        <th style={{ padding: "8px 12px" }}>{t("fields.period")}</th>
                        <th style={{ padding: "8px 12px" }}>{t("fields.metric")}</th>
                        <th style={{ padding: "8px 12px" }}>{t("fields.currentBudget")}</th>
                        <th style={{ padding: "8px 12px" }}>{t("fields.forecastAmount")}</th>
                        <th style={{ padding: "8px 12px" }}>{t("fields.actualAmount")}</th>
                        <th style={{ padding: "8px 12px" }}>{t("fields.varianceAmount")}</th>
                        <th style={{ padding: "8px 12px" }}>{t("fields.horizon")}</th>
                        <th style={{ padding: "8px 12px" }}></th>
                      </tr>
                    </thead>
                    <tbody>
                      {(varianceQuery.data ?? []).map((v) => (
                        <tr key={v.coordinateId} style={{ borderBottom: "1px solid #f1f5f9" }}>
                          <td style={{ padding: "8px 12px" }}><LocalDateDisplay value={v.periodStart} /></td>
                          <td style={{ padding: "8px 12px", fontWeight: 700 }}><BidiText>{v.metricCode}</BidiText></td>
                          <td style={{ padding: "8px 12px" }}><MoneyDisplay value={toMoney(v.budgetAmount, currencyCode)} /></td>
                          <td style={{ padding: "8px 12px" }}><MoneyDisplay value={toMoney(v.forecastAmount, currencyCode)} /></td>
                          <td style={{ padding: "8px 12px" }}><MoneyDisplay value={toMoney(v.actualAmount, currencyCode)} /></td>
                          <td style={{ padding: "8px 12px" }}><MoneyDisplay value={toMoney(v.varianceAmount, currencyCode)} /></td>
                          <td style={{ padding: "8px 12px" }}><PlanningStatusBadge status={v.favorability} /></td>
                          <td style={{ padding: "8px 12px" }}>
                            <Button variant="secondary" onPress={() => setSelectedCoordId(v.coordinateId)}>
                              {t("labels.drillThrough")}
                            </Button>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </TabPanel>
          ) : null}

          <TabPanel id="statements">
            <div style={{ margin: "1rem 0", maxWidth: "240px" }}>
              <Select
                label={t("fields.statementType")}
                selectedKey={statementType}
                onSelectionChange={(k) => k && setStatementType(String(k))}
                options={[
                  { id: "PnL", label: "PnL" },
                  { id: "BalanceSheet", label: "BalanceSheet" },
                  { id: "CashFlow", label: "CashFlow" }
                ]}
              />
            </div>

            {statementsQuery.isLoading ? (
              <LoadingState message={t("messages.loading")} />
            ) : (statementsQuery.data ?? []).length === 0 ? (
              <EmptyState title={t("messages.noData")} />
            ) : (
              <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "0.875rem" }}>
                <thead>
                  <tr style={{ borderBottom: "2px solid #e2e8f0", textAlign: "start", color: "#475569" }}>
                    <th style={{ padding: "8px 12px" }}>{t("fields.period")}</th>
                    <th style={{ padding: "8px 12px" }}>{t("fields.code")}</th>
                    <th style={{ padding: "8px 12px" }}>{t("fields.amount")}</th>
                    <th style={{ padding: "8px 12px" }}>{t("fields.source")}</th>
                  </tr>
                </thead>
                <tbody>
                  {(statementsQuery.data ?? []).map((line, i) => (
                    <tr key={i} style={{ borderBottom: "1px solid #f1f5f9" }}>
                      <td style={{ padding: "8px 12px" }}><LocalDateDisplay value={line.periodStart} /></td>
                      <td style={{ padding: "8px 12px", fontWeight: 700 }}><BidiText>{line.lineCode}</BidiText></td>
                      <td style={{ padding: "8px 12px" }}><MoneyDisplay value={toMoney(line.amount, currencyCode)} /></td>
                      <td style={{ padding: "8px 12px", color: "#64748b" }}>{line.sourceBasis}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </TabPanel>
        </Tabs>
      </Surface>

      {/* Variance Decomposition & Commentary Modal */}
      <Dialog
        isOpen={Boolean(selectedCoordId)}
        onClose={() => setSelectedCoordId(null)}
        title={t("labels.decompositionTitle")}
        size="medium"
      >
        {decompositionQuery.isLoading ? (
          <LoadingState message={t("messages.loading")} />
        ) : decompositionQuery.data ? (
          <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
            <div style={{ display: "grid", gridTemplateColumns: "repeat(3, 1fr)", gap: "0.75rem" }}>
              <div><strong>{t("fields.currentBudget")}:</strong> <MoneyDisplay value={toMoney(decompositionQuery.data.budgetAmount, currencyCode)} /></div>
              <div><strong>{t("fields.forecastAmount")}:</strong> <MoneyDisplay value={toMoney(decompositionQuery.data.forecastAmount, currencyCode)} /></div>
              <div><strong>{t("fields.actualAmount")}:</strong> <MoneyDisplay value={toMoney(decompositionQuery.data.actualAmount, currencyCode)} /></div>
            </div>

            {canEditVersions ? (
              <form
                onSubmit={async (e) => {
                  e.preventDefault();
                  if (!selectedVersionId || !selectedCoordId || !commentBody.trim()) return;
                  try {
                    await addCommentary.mutateAsync({ planVersionId: selectedVersionId, coordinateId: selectedCoordId, body: commentBody });
                    setCommentBody("");
                    setFeedback({ tone: "success", text: t("messages.savedSuccess") });
                    setSelectedCoordId(null);
                  } catch (err) {
                    const mapped = mapPlanningError(err);
                    setFeedback({ tone: "danger", text: t(mapped.messageKey as never) });
                  }
                }}
                style={{ display: "flex", flexDirection: "column", gap: "0.75rem" }}
              >
                <TextField label={t("actions.addCommentary")} value={commentBody} onChange={setCommentBody} isRequired />
                <Button type="submit" variant="primary" isDisabled={addCommentary.isPending}>
                  {t("actions.addCommentary")}
                </Button>
              </form>
            ) : null}
          </div>
        ) : (
          <EmptyState title={t("messages.noData")} />
        )}
      </Dialog>
    </div>
  );
}

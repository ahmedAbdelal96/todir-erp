import React, { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router-dom";
import { Surface } from "../../../components/pages/surface.tsx";
import { StatCard } from "../../../components/pages/property-grid.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { Alert } from "../../../components/primitives/alert.tsx";
import { EmptyState } from "../../../components/primitives/empty-state.tsx";
import { ErrorState } from "../../../components/primitives/error-state.tsx";
import { LoadingState } from "../../../components/primitives/loading-state.tsx";
import { MoneyDisplay } from "../../../components/fields/money-display.tsx";
import { LocalDateDisplay } from "../../../components/fields/local-date-display.tsx";
import { BidiText } from "../../../components/primitives/bidi-text.tsx";
import { PlanningHeader } from "../components/planning-header.tsx";
import { PlanningStatusBadge } from "../components/planning-status-badge.tsx";
import { PlanningNoScopePrompt } from "../components/planning-no-scope-prompt.tsx";
import { PlanningContextBar } from "../components/planning-context-bar.tsx";
import { SimpleAnnualBudgetModal } from "../features/simple-annual-budget-modal.tsx";
import { usePlanningScope } from "../hooks/use-planning-scope.ts";
import { usePlanningAccess } from "../hooks/use-planning-access.ts";
import {
  usePlanVersions,
  useCfoOverview,
  useActualBasis,
  useRefreshActuals
} from "../hooks/use-planning-queries.ts";
import { mapPlanningError } from "../services/planning-error-map.ts";
import { toMoney } from "../utils.ts";

export function PlanningOverviewPage(): React.JSX.Element {
  const { t } = useTranslation("planning");
  const navigate = useNavigate();
  const { legalEntityId, currencyCode, isReady } = usePlanningScope();
  const { canManageCycles, canEditVersions, canRefreshActuals, canReadVariance } = usePlanningAccess();

  const { data: versions = [], isLoading: versionsLoading, error: versionsError, refetch: refetchVersions } = usePlanVersions(isReady);
  const [selectedVersionId, setSelectedVersionId] = useState("");
  const [fromPeriod, setFromPeriod] = useState("2027-01-01");
  const [toPeriod, setToPeriod] = useState("2027-12-01");
  const [isSimpleModalOpen, setIsSimpleModalOpen] = useState(false);
  const [actionMessage, setActionMessage] = useState<{ tone: "success" | "danger"; text: string } | null>(null);

  useEffect(() => {
    if (versions.length > 0 && !versions.some((v) => v.id === selectedVersionId)) {
      const published = versions.find((v) => v.state === "Published") ?? versions[0];
      if (published) setSelectedVersionId(published.id);
    }
  }, [versions, selectedVersionId]);

  const activeVersion = versions.find((v) => v.id === selectedVersionId);

  const cfoQuery = useCfoOverview(
    {
      planVersionId: selectedVersionId,
      legalEntityIds: legalEntityId ? [legalEntityId] : [],
      from: fromPeriod,
      to: toPeriod,
      reportingCurrencyCode: currencyCode
    },
    isReady && Boolean(selectedVersionId)
  );

  const actualBasisQuery = useActualBasis(selectedVersionId, isReady && Boolean(selectedVersionId));
  const refreshActualsMutation = useRefreshActuals();

  if (!isReady) {
    return <PlanningNoScopePrompt />;
  }

  const handleRefreshActuals = async () => {
    if (!selectedVersionId) return;
    setActionMessage(null);
    try {
      await refreshActualsMutation.mutateAsync({
        mappingVersion: actualBasisQuery.data?.mappingVersion || "seed-v1",
        sourceWatermark: `ui-refresh-${Date.now()}`,
        planVersionId: selectedVersionId,
        actualThrough: toPeriod
      });
      setActionMessage({ tone: "success", text: t("messages.actualsRefreshed") });
    } catch (err) {
      const mapped = mapPlanningError(err);
      setActionMessage({ tone: "danger", text: t(mapped.messageKey as never) });
    }
  };

  const cfoMetrics = cfoQuery.data?.metrics ?? [];
  const primaryMetric = cfoMetrics[0];

  const helpSteps = t("help.overview.workflowSteps", { returnObjects: true }) as readonly string[];
  const helpConcepts = t("help.overview.keyConcepts", { returnObjects: true }) as readonly string[];
  const helpTips = t("help.overview.proTips", { returnObjects: true }) as readonly string[];
  const helpFaqs = t("help.overview.faqs", { returnObjects: true }) as readonly string[];

  const helpContent = {
    title: t("navigation.overview"),
    summary: t("help.overview.summary"),
    workflowSteps: helpSteps.map((s, idx) => ({ step: idx + 1, title: s, description: s })),
    keyConcepts: helpConcepts.map((c) => ({ term: c, explanation: c })),
    proTips: helpTips,
    faqs: helpFaqs.map((f) => ({ question: f, answer: f }))
  };

  return (
    <div data-testid="planning-overview-page" style={{ padding: "0 0 2rem 0" }}>
      <PlanningHeader
        title={t("navigation.overview")}
        subtitle={t("subtitle")}
        primaryAction={
          canManageCycles && canEditVersions && versions.length > 0
            ? {
                label: `+ ${t("actions.createAnnualBudget")}`,
                onPress: () => setIsSimpleModalOpen(true)
              }
            : undefined
        }
        secondaryActions={
          canRefreshActuals && selectedVersionId ? (
            <Button
              variant="secondary"
              isDisabled={refreshActualsMutation.isPending}
              onPress={handleRefreshActuals}
            >
              {t("actions.refreshActuals")}
            </Button>
          ) : undefined
        }
        help={helpContent}
      />

      {actionMessage ? (
        <div style={{ marginBottom: "1rem" }}>
          <Alert variant={actionMessage.tone}>{actionMessage.text}</Alert>
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
          extraControls={
            activeVersion ? (
              <PlanningStatusBadge status={activeVersion.state} />
            ) : undefined
          }
        />
      </div>

      {versionsLoading || cfoQuery.isLoading ? (
        <LoadingState message={t("messages.loading")} />
      ) : versionsError ? (
        <ErrorState
          title={t("errors.generic")}
          onRetry={() => {
            void refetchVersions();
          }}
        />
      ) : versions.length === 0 ? (
        <Surface style={{ padding: "2rem" }}>
          <EmptyState
            title={t("messages.noData")}
            action={
              canManageCycles && canEditVersions ? (
                <Button variant="primary" onPress={() => navigate("/planning/budgets")}>
                  {t("actions.createAnnualBudget")}
                </Button>
              ) : undefined
            }
          />
        </Surface>
      ) : (
        <>
          {/* Max 3 Headline KPI Cards at First Glance */}
          <div
            data-testid="planning-kpi-ribbon"
            style={{
              display: "grid",
              gridTemplateColumns: "repeat(auto-fit, minmax(240px, 1fr))",
              gap: "1rem",
              marginBottom: "1.25rem"
            }}
          >
            <StatCard
              title={t("kpi.forecastTotal")}
              value={
                primaryMetric ? (
                  <MoneyDisplay value={toMoney(primaryMetric.reportingForecastAmount, primaryMetric.reportingCurrencyCode)} />
                ) : (
                  "—"
                )
              }
              subtitle={primaryMetric ? primaryMetric.metricCode : activeVersion?.name}
              intent="info"
            />
            <StatCard
              title={t("kpi.actuals")}
              value={
                primaryMetric ? (
                  <MoneyDisplay value={toMoney(primaryMetric.reportingActualAmount, primaryMetric.reportingCurrencyCode)} />
                ) : (
                  "—"
                )
              }
              subtitle={
                actualBasisQuery.data ? `${t("fields.actualCutoff")}: ${actualBasisQuery.data.actualThrough}` : undefined
              }
              intent="primary"
            />
            <StatCard
              title={t("kpi.variance")}
              value={
                primaryMetric ? (
                  <MoneyDisplay value={toMoney(primaryMetric.reportingVarianceAmount, primaryMetric.reportingCurrencyCode)} />
                ) : (
                  "—"
                )
              }
              subtitle={primaryMetric ? primaryMetric.sourceBasis : undefined}
              intent={primaryMetric && primaryMetric.reportingVarianceAmount < 0 ? "warning" : "success"}
            />
          </div>

          {/* CFO Metrics & Drill-Through Table */}
          <Surface style={{ padding: "1.25rem", marginBottom: "1.25rem" }}>
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "1rem", flexWrap: "wrap", gap: "0.75rem" }}>
              <h2 style={{ margin: 0, fontSize: "1.05rem", color: "#0f172a" }}>
                {t("navigation.analysis")}
              </h2>
              <div style={{ display: "flex", gap: "0.5rem" }}>
                <Button variant="secondary" onPress={() => navigate("/planning/grid")}>
                  {t("navigation.grid")}
                </Button>
                {canReadVariance ? (
                  <Button variant="secondary" onPress={() => navigate("/planning/analysis")}>
                    {t("actions.runAnalysis")}
                  </Button>
                ) : null}
              </div>
            </div>

            {cfoMetrics.length === 0 ? (
              <EmptyState title={t("messages.noData")} />
            ) : (
              <div style={{ overflowX: "auto" }}>
                <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "0.875rem" }}>
                  <thead>
                    <tr style={{ borderBottom: "2px solid #e2e8f0", textAlign: "start", color: "#475569" }}>
                      <th style={{ padding: "10px 12px" }}>{t("fields.metric")}</th>
                      <th style={{ padding: "10px 12px" }}>{t("fields.forecastAmount")}</th>
                      <th style={{ padding: "10px 12px" }}>{t("fields.actualAmount")}</th>
                      <th style={{ padding: "10px 12px" }}>{t("fields.varianceAmount")}</th>
                      <th style={{ padding: "10px 12px" }}>{t("fields.source")}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {cfoMetrics.map((m) => (
                      <tr key={m.metricCode} style={{ borderBottom: "1px solid #f1f5f9" }}>
                        <td style={{ padding: "10px 12px", fontWeight: 700, color: "#102a43" }}>
                          <BidiText>{m.metricCode}</BidiText>
                        </td>
                        <td style={{ padding: "10px 12px" }}>
                          <MoneyDisplay value={toMoney(m.reportingForecastAmount, m.reportingCurrencyCode)} />
                        </td>
                        <td style={{ padding: "10px 12px" }}>
                          <MoneyDisplay value={toMoney(m.reportingActualAmount, m.reportingCurrencyCode)} />
                        </td>
                        <td style={{ padding: "10px 12px" }}>
                          <MoneyDisplay value={toMoney(m.reportingVarianceAmount, m.reportingCurrencyCode)} />
                        </td>
                        <td style={{ padding: "10px 12px", color: "#64748b" }}>{m.sourceBasis}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </Surface>

          {/* Actual Snapshot Provenance Summary */}
          {actualBasisQuery.data ? (
            <Surface style={{ padding: "1rem 1.25rem" }}>
              <div style={{ display: "flex", flexWrap: "wrap", gap: "1.5rem", alignItems: "center", fontSize: "0.85rem", color: "#475569" }}>
                <div>
                  <strong>{t("fields.mappingVersion")}:</strong>{" "}
                  <BidiText>{actualBasisQuery.data.mappingVersion}</BidiText>
                </div>
                <div>
                  <strong>{t("fields.actualCutoff")}:</strong>{" "}
                  <LocalDateDisplay value={actualBasisQuery.data.actualThrough} />
                </div>
                <div>
                  <strong>{t("fields.currency")}:</strong>{" "}
                  <BidiText>{actualBasisQuery.data.currencyBasis}</BidiText>
                </div>
                <PlanningStatusBadge status={actualBasisQuery.data.state} />
              </div>
            </Surface>
          ) : null}
        </>
      )}

      {activeVersion ? (
        <SimpleAnnualBudgetModal
          isOpen={isSimpleModalOpen}
          onClose={() => setIsSimpleModalOpen(false)}
          legalEntityId={legalEntityId}
          currencyCode={currencyCode}
          defaultModelVersionId={activeVersion.modelVersionId}
          onCreated={(newVersionId) => setSelectedVersionId(newVersionId)}
        />
      ) : null}
    </div>
  );
}

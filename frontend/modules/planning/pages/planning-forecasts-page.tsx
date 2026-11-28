import React, { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../components/pages/surface.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { Select } from "../../../components/primitives/select.tsx";
import { LocalDateField } from "../../../components/fields/local-date-field.tsx";
import { MoneyDisplay } from "../../../components/fields/money-display.tsx";
import { PercentageDisplay } from "../../../components/fields/percentage-display.tsx";
import { LocalDateDisplay } from "../../../components/fields/local-date-display.tsx";
import { Alert } from "../../../components/primitives/alert.tsx";
import { EmptyState } from "../../../components/primitives/empty-state.tsx";
import { PlanningHeader } from "../components/planning-header.tsx";
import { PlanningStatusBadge } from "../components/planning-status-badge.tsx";
import { PlanningNoScopePrompt } from "../components/planning-no-scope-prompt.tsx";
import { usePlanningScope } from "../hooks/use-planning-scope.ts";
import { usePlanningAccess } from "../hooks/use-planning-access.ts";
import {
  usePlanVersions,
  useRollForecast,
  useCalculateForecastAccuracy,
  useCreateScenario,
  useCloneScenario,
  useShareScenario,
  useAdoptScenario,
  useCompareVersions
} from "../hooks/use-planning-queries.ts";
import { mapPlanningError } from "../services/planning-error-map.ts";
import { toMoney } from "../utils.ts";
import type { ForecastAccuracyView, ScenarioView } from "../types.ts";

export function PlanningForecastsPage(): React.JSX.Element {
  const { t } = useTranslation("planning");
  const { legalEntityId, currencyCode, isReady } = usePlanningScope();
  const { canEditVersions, canCreateScenarios, canShareScenarios } = usePlanningAccess();

  const { data: versions = [] } = usePlanVersions(isReady);
  const [leftVersionId, setLeftVersionId] = useState("");
  const [rightVersionId, setRightVersionId] = useState("");
  const [scenarios, setScenarios] = useState<ScenarioView[]>([]);
  const [accuracyResult, setAccuracyResult] = useState<ForecastAccuracyView | null>(null);
  const [feedback, setFeedback] = useState<{ tone: "success" | "danger"; text: string } | null>(null);

  // Modals
  const [isRollModalOpen, setIsRollModalOpen] = useState(false);
  const [rollName, setRollName] = useState("Rolling Forecast 12M");
  const [rollCutoff, setRollCutoff] = useState("2027-03-01");
  const [rollHorizon, setRollHorizon] = useState("12");

  const [isScenarioModalOpen, setIsScenarioModalOpen] = useState(false);
  const [scenName, setScenName] = useState("Upside Scenario");
  const [scenType, setScenType] = useState("Upside");
  const [scenVis, setScenVis] = useState("Private");

  const [shareScenarioId, setShareScenarioId] = useState<string | null>(null);
  const [shareUserId, setShareUserId] = useState("");
  const [shareLevel, setShareLevel] = useState<"Read" | "Edit" | "Adopt">("Read");

  useEffect(() => {
    if (versions.length > 0 && !leftVersionId) {
      setLeftVersionId(versions[0]!.id);
      setRightVersionId(versions[1]?.id ?? versions[0]!.id);
    }
  }, [versions, leftVersionId]);

  const rollForecast = useRollForecast();
  const calcAccuracy = useCalculateForecastAccuracy();
  const createScenario = useCreateScenario();
  const cloneScenario = useCloneScenario();
  const shareScenario = useShareScenario();
  const adoptScenario = useAdoptScenario();
  const compareQuery = useCompareVersions(leftVersionId, rightVersionId, legalEntityId);

  if (!isReady) {
    return <PlanningNoScopePrompt />;
  }

  const runMutation = async (fn: () => Promise<unknown>) => {
    setFeedback(null);
    try {
      await fn();
      setFeedback({ tone: "success", text: t("messages.savedSuccess") });
    } catch (err) {
      const mapped = mapPlanningError(err);
      setFeedback({ tone: "danger", text: t(mapped.messageKey as never) });
    }
  };

  return (
    <div data-testid="planning-forecasts-page" style={{ padding: "0 0 2rem 0" }}>
      <PlanningHeader
        title={t("navigation.forecasts")}
        subtitle={t("subtitle")}
        primaryAction={
          canEditVersions && versions.length > 0
            ? { label: t("actions.rollForecast"), onPress: () => setIsRollModalOpen(true) }
            : undefined
        }
        secondaryActions={
          <div style={{ display: "flex", gap: "0.5rem", flexWrap: "wrap" }}>
            {canCreateScenarios ? (
              <Button variant="secondary" onPress={() => setIsScenarioModalOpen(true)}>
                + {t("actions.createScenario")}
              </Button>
            ) : null}
            {leftVersionId && rightVersionId ? (
              <Button
                variant="secondary"
                isDisabled={calcAccuracy.isPending}
                onPress={() =>
                  void runMutation(async () => {
                    const res = await calcAccuracy.mutateAsync({
                      forecastVersionId: leftVersionId,
                      actualVersionId: rightVersionId,
                      from: "2027-01-01",
                      to: "2027-12-01",
                      metricCode: "Revenue"
                    });
                    setAccuracyResult(res);
                  })
                }
              >
                {`${t("actions.runAnalysis")} (${t("labels.accuracyLabel")})`}
              </Button>
            ) : null}
          </div>
        }
      />

      {feedback ? (
        <div style={{ marginBottom: "1rem" }}>
          <Alert variant={feedback.tone}>{feedback.text}</Alert>
        </div>
      ) : null}

      {/* Forecast Accuracy Summary */}
      {accuracyResult ? (
        <Surface style={{ padding: "1rem 1.25rem", marginBottom: "1.25rem" }}>
          <div style={{ display: "flex", gap: "2rem", flexWrap: "wrap", alignItems: "center" }}>
            <div><strong>{t("fields.metric")}:</strong> {accuracyResult.metricCode}</div>
            <div><strong>{t("fields.forecastAmount")}:</strong> <MoneyDisplay value={toMoney(accuracyResult.forecastTotal, currencyCode)} /></div>
            <div><strong>{t("fields.actualAmount")}:</strong> <MoneyDisplay value={toMoney(accuracyResult.actualTotal, currencyCode)} /></div>
            <div><strong>{t("labels.errorPercent")}:</strong> <PercentageDisplay value={accuracyResult.percentageError} /></div>
            <div><strong>{t("labels.biasLabel")}:</strong> <MoneyDisplay value={toMoney(accuracyResult.bias, currencyCode)} /></div>
          </div>
        </Surface>
      ) : null}

      {/* Side-by-Side Version Comparison */}
      <Surface style={{ padding: "1.25rem", marginBottom: "1.25rem" }}>
        <div style={{ display: "flex", gap: "1rem", flexWrap: "wrap", marginBottom: "1rem", alignItems: "flex-end" }}>
          <div style={{ minWidth: "220px" }}>
            <Select
              label={t("labels.versionA")}
              selectedKey={leftVersionId}
              onSelectionChange={(k) => k && setLeftVersionId(String(k))}
              options={versions.map((v) => ({ id: v.id, label: `${v.name} (${v.versionKind})` }))}
            />
          </div>
          <div style={{ minWidth: "220px" }}>
            <Select
              label={t("labels.versionB")}
              selectedKey={rightVersionId}
              onSelectionChange={(k) => k && setRightVersionId(String(k))}
              options={versions.map((v) => ({ id: v.id, label: `${v.name} (${v.versionKind})` }))}
            />
          </div>
        </div>

        {(compareQuery.data ?? []).length === 0 ? (
          <EmptyState title={t("messages.noData")} />
        ) : (
          <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "0.875rem" }}>
            <thead>
              <tr style={{ borderBottom: "2px solid #e2e8f0", textAlign: "start", color: "#475569" }}>
                <th style={{ padding: "8px 12px" }}>{t("fields.period")}</th>
                <th style={{ padding: "8px 12px" }}>{t("fields.metric")}</th>
                <th style={{ padding: "8px 12px" }}>{t("labels.versionA")}</th>
                <th style={{ padding: "8px 12px" }}>{t("labels.versionB")}</th>
                <th style={{ padding: "8px 12px" }}>{t("fields.varianceAmount")}</th>
              </tr>
            </thead>
            <tbody>
              {(compareQuery.data ?? []).map((row, idx) => (
                <tr key={idx} style={{ borderBottom: "1px solid #f1f5f9" }}>
                  <td style={{ padding: "8px 12px" }}><LocalDateDisplay value={row.periodStart} /></td>
                  <td style={{ padding: "8px 12px", fontWeight: 700 }}>{row.metricCode}</td>
                  <td style={{ padding: "8px 12px" }}><MoneyDisplay value={toMoney(row.leftAmount, currencyCode)} /></td>
                  <td style={{ padding: "8px 12px" }}><MoneyDisplay value={toMoney(row.rightAmount, currencyCode)} /></td>
                  <td style={{ padding: "8px 12px" }}><MoneyDisplay value={toMoney(row.difference, currencyCode)} /></td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </Surface>

      {/* Scenarios Created in Session */}
      {scenarios.length > 0 ? (
        <Surface style={{ padding: "1.25rem" }}>
          <h2 style={{ margin: "0 0 1rem 0", fontSize: "1.05rem" }}>{t("fields.scenario")}</h2>
          <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "0.875rem" }}>
            <thead>
              <tr style={{ borderBottom: "2px solid #e2e8f0", textAlign: "start", color: "#475569" }}>
                <th style={{ padding: "8px 12px" }}>{t("fields.name")}</th>
                <th style={{ padding: "8px 12px" }}>{t("fields.scenarioType")}</th>
                <th style={{ padding: "8px 12px" }}>{t("fields.visibility")}</th>
                <th style={{ padding: "8px 12px" }}>{t("fields.horizon")}</th>
                <th style={{ padding: "8px 12px" }}></th>
              </tr>
            </thead>
            <tbody>
              {scenarios.map((s) => (
                <tr key={s.id} style={{ borderBottom: "1px solid #f1f5f9" }}>
                  <td style={{ padding: "8px 12px", fontWeight: 700 }}>{s.name}</td>
                  <td style={{ padding: "8px 12px" }}>{s.scenarioType}</td>
                  <td style={{ padding: "8px 12px" }}><PlanningStatusBadge status={s.visibility} /></td>
                  <td style={{ padding: "8px 12px" }}><PlanningStatusBadge status={s.state} /></td>
                  <td style={{ padding: "8px 12px", display: "flex", gap: "0.35rem" }}>
                    {canCreateScenarios ? (
                      <Button variant="secondary" onPress={() => void runMutation(async () => {
                        const cloned = await cloneScenario.mutateAsync({ id: s.id, name: `${s.name} (Clone)` });
                        setScenarios((prev) => [cloned, ...prev]);
                      })}>
                        {t("actions.clone")}
                      </Button>
                    ) : null}
                    {canShareScenarios ? (
                      <Button variant="secondary" onPress={() => setShareScenarioId(s.id)}>
                        {t("actions.shareScenario")}
                      </Button>
                    ) : null}
                    {canCreateScenarios ? (
                      <Button variant="primary" onPress={() => void runMutation(() =>
                        adoptScenario.mutateAsync({ id: s.id, request: { name: `${s.name} Draft`, versionKind: "Budget" } })
                      )}>
                        {t("actions.adoptScenario")}
                      </Button>
                    ) : null}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </Surface>
      ) : null}

      {/* Roll Forecast Modal */}
      <Dialog isOpen={isRollModalOpen} onClose={() => setIsRollModalOpen(false)} title={t("actions.rollForecast")} size="small">
        <form
          onSubmit={(e) => {
            e.preventDefault();
            if (!leftVersionId) return;
            void runMutation(async () => {
              await rollForecast.mutateAsync({
                sourceVersionId: leftVersionId,
                actualCutoff: rollCutoff,
                name: rollName,
                horizonMonths: Number(rollHorizon) || 12
              });
              setIsRollModalOpen(false);
            });
          }}
          style={{ display: "flex", flexDirection: "column", gap: "1rem" }}
        >
          <TextField label={t("fields.name")} value={rollName} onChange={setRollName} isRequired />
          <LocalDateField label={t("fields.actualCutoff")} value={rollCutoff} onChange={setRollCutoff} />
          <TextField label={t("fields.horizon")} value={rollHorizon} onChange={setRollHorizon} type="number" />
          <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem" }}>
            <Button type="button" variant="secondary" onPress={() => setIsRollModalOpen(false)}>{t("actions.cancel")}</Button>
            <Button type="submit" variant="primary" isDisabled={rollForecast.isPending}>{t("actions.rollForecast")}</Button>
          </div>
        </form>
      </Dialog>

      {/* Create Scenario Modal */}
      <Dialog isOpen={isScenarioModalOpen} onClose={() => setIsScenarioModalOpen(false)} title={t("actions.createScenario")} size="small">
        <form
          onSubmit={(e) => {
            e.preventDefault();
            void runMutation(async () => {
              const created = await createScenario.mutateAsync({
                name: scenName,
                scenarioType: scenType,
                baseVersionId: leftVersionId || null,
                visibility: scenVis
              });
              setScenarios((prev) => [created, ...prev]);
              setIsScenarioModalOpen(false);
            });
          }}
          style={{ display: "flex", flexDirection: "column", gap: "1rem" }}
        >
          <TextField label={t("fields.name")} value={scenName} onChange={setScenName} isRequired />
          <Select
            label={t("fields.scenarioType")}
            selectedKey={scenType}
            onSelectionChange={(k) => k && setScenType(String(k))}
            options={[
              { id: "Base", label: "Base" },
              { id: "Upside", label: "Upside" },
              { id: "Downside", label: "Downside" },
              { id: "Stress", label: "Stress" }
            ]}
          />
          <Select
            label={t("fields.visibility")}
            selectedKey={scenVis}
            onSelectionChange={(k) => k && setScenVis(String(k))}
            options={[
              { id: "Private", label: t("statuses.Private") },
              { id: "Shared", label: t("statuses.Shared") }
            ]}
          />
          <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem" }}>
            <Button type="button" variant="secondary" onPress={() => setIsScenarioModalOpen(false)}>{t("actions.cancel")}</Button>
            <Button type="submit" variant="primary" isDisabled={createScenario.isPending}>{t("actions.confirm")}</Button>
          </div>
        </form>
      </Dialog>

      {/* Share Scenario Modal */}
      <Dialog isOpen={Boolean(shareScenarioId)} onClose={() => setShareScenarioId(null)} title={t("actions.shareScenario")} size="small">
        <form
          onSubmit={(e) => {
            e.preventDefault();
            if (!shareScenarioId) return;
            void runMutation(async () => {
              await shareScenario.mutateAsync({ id: shareScenarioId, userId: shareUserId, accessLevel: shareLevel });
              setShareScenarioId(null);
            });
          }}
          style={{ display: "flex", flexDirection: "column", gap: "1rem" }}
        >
          <TextField label={t("fields.userId")} value={shareUserId} onChange={setShareUserId} isRequired />
          <Select
            label={t("fields.accessLevel")}
            selectedKey={shareLevel}
            onSelectionChange={(k) => k && setShareLevel(k as typeof shareLevel)}
            options={[
              { id: "Read", label: "Read" },
              { id: "Edit", label: "Edit" },
              { id: "Adopt", label: "Adopt" }
            ]}
          />
          <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem" }}>
            <Button type="button" variant="secondary" onPress={() => setShareScenarioId(null)}>{t("actions.cancel")}</Button>
            <Button type="submit" variant="primary" isDisabled={shareScenario.isPending}>{t("actions.shareScenario")}</Button>
          </div>
        </form>
      </Dialog>
    </div>
  );
}

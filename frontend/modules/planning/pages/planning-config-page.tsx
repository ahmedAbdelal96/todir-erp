import React, { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../components/pages/surface.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { Select } from "../../../components/primitives/select.tsx";
import { Alert } from "../../../components/primitives/alert.tsx";
import { EmptyState } from "../../../components/primitives/empty-state.tsx";
import { Tabs, TabList, Tab, TabPanel } from "../../../components/primitives/tabs.tsx";
import { PlanningHeader } from "../components/planning-header.tsx";
import { PlanningStatusBadge } from "../components/planning-status-badge.tsx";
import { PlanningNoScopePrompt } from "../components/planning-no-scope-prompt.tsx";
import { usePlanningScope } from "../hooks/use-planning-scope.ts";
import { usePlanningAccess } from "../hooks/use-planning-access.ts";
import {
  usePlanVersions,
  useDimensions,
  useCreateDimension,
  useCreateMetric,
  useCreateMetricMapping,
  useCreateFormula,
  useCreatePlanningModel
} from "../hooks/use-planning-queries.ts";
import { mapPlanningError } from "../services/planning-error-map.ts";

export function PlanningConfigPage(): React.JSX.Element {
  const { t } = useTranslation("planning");
  const { legalEntityId, currencyCode, isReady } = usePlanningScope();
  const { canManageModels } = usePlanningAccess();

  const { data: versions = [] } = usePlanVersions(isReady);
  const [modelVersionId, setModelVersionId] = useState("");
  const [feedback, setFeedback] = useState<{ tone: "success" | "danger"; text: string } | null>(null);

  useEffect(() => {
    if (versions.length > 0 && !modelVersionId) {
      setModelVersionId(versions[0]!.modelVersionId);
    }
  }, [versions, modelVersionId]);

  const dimensionsQuery = useDimensions(modelVersionId);
  const createModel = useCreatePlanningModel();
  const createDimension = useCreateDimension();
  const createMetric = useCreateMetric();
  const createMapping = useCreateMetricMapping();
  const createFormula = useCreateFormula();

  // Modals
  const [isDimOpen, setIsDimOpen] = useState(false);
  const [dimCode, setDimCode] = useState("COST_CENTER");
  const [dimName, setDimName] = useState("Cost Center");

  const [isMetricOpen, setIsMetricOpen] = useState(false);
  const [metricCode, setMetricCode] = useState("EBITDA");
  const [metricName, setMetricName] = useState("Operating EBITDA");
  const [metricKind, setMetricKind] = useState("Calculated");

  const [isFormulaOpen, setIsFormulaOpen] = useState(false);
  const [formulaCode, setFormulaCode] = useState("EBITDA_CALC");
  const [formulaExpr, setFormulaExpr] = useState("Revenue - OPEX");
  const [formulaOut, setFormulaOut] = useState("EBITDA");
  const [formulaDeps, setFormulaDeps] = useState("Revenue,OPEX");

  const [isModelOpen, setIsModelOpen] = useState(false);
  const [modelName, setModelName] = useState("Integrated FP&A Model");

  const [mappingSource, setMappingSource] = useState("ledger");
  const [mappingKey, setMappingKey] = useState("ExpenseRecognized");
  const [mappingTarget, setMappingTarget] = useState("OPEX");

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
    <div data-testid="planning-config-page" style={{ padding: "0 0 2rem 0" }}>
      <PlanningHeader
        title={t("navigation.config")}
        subtitle={t("subtitle")}
        primaryAction={
          canManageModels
            ? { label: `+ ${t("actions.createModel")}`, onPress: () => setIsModelOpen(true) }
            : undefined
        }
        secondaryActions={
          canManageModels ? (
            <div style={{ display: "flex", gap: "0.5rem", flexWrap: "wrap" }}>
              <Button variant="secondary" onPress={() => setIsDimOpen(true)}>+ {t("fields.dimension")}</Button>
              <Button variant="secondary" onPress={() => setIsMetricOpen(true)}>+ {t("actions.createMetric")}</Button>
              <Button variant="secondary" onPress={() => setIsFormulaOpen(true)}>+ {t("actions.createFormula")}</Button>
            </div>
          ) : undefined
        }
      />

      {feedback ? (
        <div style={{ marginBottom: "1rem" }}>
          <Alert variant={feedback.tone}>{feedback.text}</Alert>
        </div>
      ) : null}

      <Surface style={{ padding: "1.25rem" }}>
        <Tabs defaultSelectedKey="dimensions">
          <TabList aria-label={t("navigation.config")}>
            <Tab id="dimensions">{t("labels.dimensions")}</Tab>
            <Tab id="mappings">{t("labels.metricMappings")}</Tab>
          </TabList>

          <TabPanel id="dimensions">
            {(dimensionsQuery.data ?? []).length === 0 ? (
              <EmptyState title={t("messages.noData")} />
            ) : (
              <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "0.875rem", marginTop: "1rem" }}>
                <thead>
                  <tr style={{ borderBottom: "2px solid #e2e8f0", textAlign: "start", color: "#475569" }}>
                    <th style={{ padding: "8px 12px" }}>{t("fields.code")}</th>
                    <th style={{ padding: "8px 12px" }}>{t("fields.name")}</th>
                    <th style={{ padding: "8px 12px" }}>{t("fields.kind")}</th>
                    <th style={{ padding: "8px 12px" }}>{t("fields.horizon")}</th>
                  </tr>
                </thead>
                <tbody>
                  {(dimensionsQuery.data ?? []).map((d) => (
                    <tr key={d.id} style={{ borderBottom: "1px solid #f1f5f9" }}>
                      <td style={{ padding: "8px 12px", fontWeight: 700 }}>{d.code}</td>
                      <td style={{ padding: "8px 12px" }}>{d.name}</td>
                      <td style={{ padding: "8px 12px" }}>{d.memberKind}</td>
                      <td style={{ padding: "8px 12px" }}><PlanningStatusBadge status={d.active ? "Active" : "Retired"} /></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </TabPanel>

          <TabPanel id="mappings">
            <form
              onSubmit={(e) => {
                e.preventDefault();
                void runMutation(() =>
                  createMapping.mutateAsync({
                    mappingVersion: "v2",
                    sourceModule: mappingSource,
                    sourceType: "FinancialRole",
                    sourceKey: mappingKey,
                    targetMetricCode: mappingTarget,
                    legalEntityId
                  })
                );
              }}
              style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(180px, 1fr))", gap: "1rem", marginTop: "1rem", alignItems: "end" }}
            >
              <TextField label={t("fields.sourceModule")} value={mappingSource} onChange={setMappingSource} isRequired />
              <TextField label={t("fields.sourceKey")} value={mappingKey} onChange={setMappingKey} isRequired />
              <TextField label={t("fields.outputMetric")} value={mappingTarget} onChange={setMappingTarget} isRequired />
              <Button type="submit" variant="primary" isDisabled={!canManageModels || createMapping.isPending}>
                {t("actions.saveChanges")}
              </Button>
            </form>
          </TabPanel>
        </Tabs>
      </Surface>

      {/* Model Modal */}
      <Dialog isOpen={isModelOpen} onClose={() => setIsModelOpen(false)} title={t("actions.createModel")} size="small">
        <form
          onSubmit={(e) => {
            e.preventDefault();
            void runMutation(async () => {
              await createModel.mutateAsync({ name: modelName, modelType: "Integrated", legalEntityId, currencyCode });
              setIsModelOpen(false);
            });
          }}
          style={{ display: "flex", flexDirection: "column", gap: "1rem" }}
        >
          <TextField label={t("fields.name")} value={modelName} onChange={setModelName} isRequired />
          <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem" }}>
            <Button type="button" variant="secondary" onPress={() => setIsModelOpen(false)}>{t("actions.cancel")}</Button>
            <Button type="submit" variant="primary" isDisabled={createModel.isPending}>{t("actions.confirm")}</Button>
          </div>
        </form>
      </Dialog>

      {/* Dimension Modal */}
      <Dialog isOpen={isDimOpen} onClose={() => setIsDimOpen(false)} title={t("fields.dimension")} size="small">
        <form
          onSubmit={(e) => {
            e.preventDefault();
            if (!modelVersionId) return;
            void runMutation(async () => {
              await createDimension.mutateAsync({ modelVersionId, code: dimCode, name: dimName, memberKind: "Custom", required: false });
              setIsDimOpen(false);
            });
          }}
          style={{ display: "flex", flexDirection: "column", gap: "1rem" }}
        >
          <TextField label={t("fields.code")} value={dimCode} onChange={setDimCode} isRequired />
          <TextField label={t("fields.name")} value={dimName} onChange={setDimName} isRequired />
          <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem" }}>
            <Button type="button" variant="secondary" onPress={() => setIsDimOpen(false)}>{t("actions.cancel")}</Button>
            <Button type="submit" variant="primary" isDisabled={createDimension.isPending}>{t("actions.confirm")}</Button>
          </div>
        </form>
      </Dialog>

      {/* Metric Modal */}
      <Dialog isOpen={isMetricOpen} onClose={() => setIsMetricOpen(false)} title={t("actions.createMetric")} size="small">
        <form
          onSubmit={(e) => {
            e.preventDefault();
            void runMutation(async () => {
              await createMetric.mutateAsync({ code: metricCode, name: metricName, metricKind, dataType: "Money", sourcePolicy: "Calculated" });
              setIsMetricOpen(false);
            });
          }}
          style={{ display: "flex", flexDirection: "column", gap: "1rem" }}
        >
          <TextField label={t("fields.code")} value={metricCode} onChange={setMetricCode} isRequired />
          <TextField label={t("fields.name")} value={metricName} onChange={setMetricName} isRequired />
          <Select
            label={t("fields.kind")}
            selectedKey={metricKind}
            onSelectionChange={(k) => k && setMetricKind(String(k))}
            options={[
              { id: "Revenue", label: "Revenue" },
              { id: "Opex", label: "Opex" },
              { id: "Calculated", label: "Calculated" },
              { id: "Cash", label: "Cash" }
            ]}
          />
          <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem" }}>
            <Button type="button" variant="secondary" onPress={() => setIsMetricOpen(false)}>{t("actions.cancel")}</Button>
            <Button type="submit" variant="primary" isDisabled={createMetric.isPending}>{t("actions.confirm")}</Button>
          </div>
        </form>
      </Dialog>

      {/* Formula Modal */}
      <Dialog isOpen={isFormulaOpen} onClose={() => setIsFormulaOpen(false)} title={t("actions.createFormula")} size="small">
        <form
          onSubmit={(e) => {
            e.preventDefault();
            if (!modelVersionId) return;
            void runMutation(async () => {
              await createFormula.mutateAsync({
                modelVersionId,
                code: formulaCode,
                expression: formulaExpr,
                outputMetricCode: formulaOut,
                dependencies: formulaDeps.split(",").map((s) => s.trim()).filter(Boolean)
              });
              setIsFormulaOpen(false);
            });
          }}
          style={{ display: "flex", flexDirection: "column", gap: "1rem" }}
        >
          <Alert variant="info">{t("labels.formulaHint")}</Alert>
          <TextField label={t("fields.code")} value={formulaCode} onChange={setFormulaCode} isRequired />
          <TextField label={t("fields.expression")} value={formulaExpr} onChange={setFormulaExpr} isRequired />
          <TextField label={t("fields.outputMetric")} value={formulaOut} onChange={setFormulaOut} isRequired />
          <TextField label={t("fields.dependencies")} value={formulaDeps} onChange={setFormulaDeps} isRequired />
          <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem" }}>
            <Button type="button" variant="secondary" onPress={() => setIsFormulaOpen(false)}>{t("actions.cancel")}</Button>
            <Button type="submit" variant="primary" isDisabled={createFormula.isPending}>{t("actions.confirm")}</Button>
          </div>
        </form>
      </Dialog>
    </div>
  );
}

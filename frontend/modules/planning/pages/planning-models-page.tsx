import React, { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../components/pages/surface.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { Select } from "../../../components/primitives/select.tsx";
import { LocalDateField } from "../../../components/fields/local-date-field.tsx";
import { MoneyField } from "../../../components/fields/money-field.tsx";
import { MoneyDisplay } from "../../../components/fields/money-display.tsx";
import { LocalDateDisplay } from "../../../components/fields/local-date-display.tsx";
import { Alert } from "../../../components/primitives/alert.tsx";
import { EmptyState } from "../../../components/primitives/empty-state.tsx";
import { LoadingState } from "../../../components/primitives/loading-state.tsx";
import { Tabs, TabList, Tab, TabPanel } from "../../../components/primitives/tabs.tsx";
import { PlanningHeader } from "../components/planning-header.tsx";
import { PlanningStatusBadge } from "../components/planning-status-badge.tsx";
import { PlanningNoScopePrompt } from "../components/planning-no-scope-prompt.tsx";
import { PlanningContextBar } from "../components/planning-context-bar.tsx";
import { usePlanningScope } from "../hooks/use-planning-scope.ts";
import { usePlanningAccess } from "../hooks/use-planning-access.ts";
import {
  usePlanVersions,
  useOpexLines,
  useAddOpexLine,
  useWorkforcePlan,
  useWorkforceCostImpact,
  useAddWorkforceLine,
  useCapexPlan,
  useAddCapexItem,
  usePrioritizeCapex,
  useApproveCapexBudget,
  useAddRevenueDriver,
  useAddWorkingCapital,
  useAddPlanningFx,
  useAssumptions,
  useCreateAssumption
} from "../hooks/use-planning-queries.ts";
import { mapPlanningError } from "../services/planning-error-map.ts";
import { toMoney } from "../utils.ts";

export function PlanningModelsPage(): React.JSX.Element {
  const { t } = useTranslation("planning");
  const { legalEntityId, currencyCode, isReady } = usePlanningScope();
  const {
    canEditVersions,
    canReadWorkforce,
    canReadSensitiveWorkforce,
    canReadCapex,
    canManageCapex,
    canManageAssumptions
  } = usePlanningAccess();

  const { data: versions = [] } = usePlanVersions(isReady);
  const [selectedVersionId, setSelectedVersionId] = useState("");
  const [feedback, setFeedback] = useState<{ tone: "success" | "danger"; text: string } | null>(null);

  useEffect(() => {
    if (versions.length > 0 && !versions.some((v) => v.id === selectedVersionId)) {
      const draft = versions.find((v) => v.state === "Draft") ?? versions[0];
      if (draft) setSelectedVersionId(draft.id);
    }
  }, [versions, selectedVersionId]);

  const activeVersion = versions.find((v) => v.id === selectedVersionId);
  const isDraft = activeVersion?.state === "Draft";

  const opexQuery = useOpexLines(selectedVersionId, legalEntityId);
  const workforceQuery = useWorkforcePlan(selectedVersionId, canReadWorkforce);
  const workforceImpactQuery = useWorkforceCostImpact(selectedVersionId, canReadWorkforce);
  const capexQuery = useCapexPlan(selectedVersionId, canReadCapex);
  const assumptionsQuery = useAssumptions(selectedVersionId);

  const addOpex = useAddOpexLine();
  const addWorkforce = useAddWorkforceLine();
  const addCapex = useAddCapexItem();
  const prioritizeCapex = usePrioritizeCapex();
  const approveCapex = useApproveCapexBudget();
  const addRevenue = useAddRevenueDriver();
  const addWorkingCapital = useAddWorkingCapital();
  const addFx = useAddPlanningFx();
  const createAssumption = useCreateAssumption();

  // Modal states
  const [isOpexModalOpen, setIsOpexModalOpen] = useState(false);
  const [opexCategory, setOpexCategory] = useState("IT Infrastructure");
  const [opexMethod, setOpexMethod] = useState<"Fixed" | "Variable" | "Recurring" | "DriverBased">("Fixed");
  const [opexPeriod, setOpexPeriod] = useState("2027-01-01");
  const [opexBase, setOpexBase] = useState("10000");

  const [isWorkforceModalOpen, setIsWorkforceModalOpen] = useState(false);
  const [wfPeriod, setWfPeriod] = useState("2027-01-01");
  const [wfFte, setWfFte] = useState("5");
  const [wfCost, setWfCost] = useState("15000");

  const [isCapexModalOpen, setIsCapexModalOpen] = useState(false);
  const [capexName, setCapexName] = useState("Server Upgrade");
  const [capexCategory, setCapexCategory] = useState("IT");
  const [capexAmount, setCapexAmount] = useState("50000");
  const [capexPeriod, setCapexPeriod] = useState("2027-02-01");

  const [revKey, setRevKey] = useState("Enterprise Licenses");
  const [revPeriod, setRevPeriod] = useState("2027-01-01");
  const [revVolume, setRevVolume] = useState("100");
  const [revPrice, setRevPrice] = useState("1500");

  const [wcPeriod, setWcPeriod] = useState("2027-01-01");
  const [wcDso, setWcDso] = useState("45");
  const [wcDpo, setWcDpo] = useState("30");
  const [wcInv, setWcInv] = useState("60");

  const [fxPeriod, setFxPeriod] = useState("2027-01-01");
  const [fxFrom, setFxFrom] = useState("USD");
  const [fxRate, setFxRate] = useState("50");

  const [isAssumptionModalOpen, setIsAssumptionModalOpen] = useState(false);
  const [assumptionCode, setAssumptionCode] = useState("HEADCOUNT_GROWTH");
  const [assumptionName, setAssumptionName] = useState("Annual Headcount Growth");
  const [assumptionUnit, setAssumptionUnit] = useState("Percent");

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
    <div data-testid="planning-models-page" style={{ padding: "0 0 2rem 0" }}>
      <PlanningHeader
        title={t("navigation.models")}
        subtitle={t("subtitle")}
      />

      {feedback ? (
        <div style={{ marginBottom: "1rem" }}>
          <Alert variant={feedback.tone}>{feedback.text}</Alert>
        </div>
      ) : null}

      <PlanningContextBar
        versions={versions}
        selectedVersionId={selectedVersionId}
        onVersionChange={setSelectedVersionId}
        extraControls={activeVersion ? <PlanningStatusBadge status={activeVersion.state} /> : undefined}
      />

      <Surface style={{ padding: "1.25rem" }}>
        <Tabs defaultSelectedKey="opex">
          <div style={{ overflowX: "auto", maxWidth: "100%" }}>
            <TabList aria-label={t("navigation.models")}>
              <Tab id="opex">{t("labels.opex")}</Tab>
              <Tab id="revenue">{t("labels.revenue")}</Tab>
              {canReadWorkforce ? <Tab id="workforce">{t("labels.workforce")}</Tab> : null}
              {canReadCapex ? <Tab id="capex">{t("labels.capex")}</Tab> : null}
              <Tab id="wc-fx">{t("labels.wcFx")}</Tab>
              <Tab id="assumptions">{t("labels.assumptions")}</Tab>
            </TabList>
          </div>

          {/* OPEX Panel */}
          <TabPanel id="opex">
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", margin: "1rem 0" }}>
              <h3 style={{ margin: 0, fontSize: "1rem" }}>{t("labels.opex")}</h3>
              {canEditVersions && isDraft ? (
                <Button variant="primary" onPress={() => setIsOpexModalOpen(true)}>+ {t("actions.addLine")}</Button>
              ) : null}
            </div>
            {opexQuery.isLoading ? (
              <LoadingState message={t("messages.loading")} />
            ) : (opexQuery.data ?? []).length === 0 ? (
              <EmptyState title={t("messages.noData")} />
            ) : (
              <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "0.875rem" }}>
                <thead>
                  <tr style={{ borderBottom: "2px solid #e2e8f0", textAlign: "start", color: "#475569" }}>
                    <th style={{ padding: "8px 12px" }}>{t("fields.category")}</th>
                    <th style={{ padding: "8px 12px" }}>{t("fields.method")}</th>
                    <th style={{ padding: "8px 12px" }}>{t("fields.period")}</th>
                    <th style={{ padding: "8px 12px" }}>{t("fields.plannedAmount")}</th>
                    <th style={{ padding: "8px 12px" }}>{t("fields.source")}</th>
                  </tr>
                </thead>
                <tbody>
                  {(opexQuery.data ?? []).map((line) => (
                    <tr key={line.id} style={{ borderBottom: "1px solid #f1f5f9" }}>
                      <td style={{ padding: "8px 12px", fontWeight: 600 }}>{line.categoryCode}</td>
                      <td style={{ padding: "8px 12px" }}>{line.method}</td>
                      <td style={{ padding: "8px 12px" }}><LocalDateDisplay value={line.periodStart} /></td>
                      <td style={{ padding: "8px 12px" }}><MoneyDisplay value={toMoney(line.plannedAmount, line.currencyCode)} /></td>
                      <td style={{ padding: "8px 12px", color: "#64748b" }}>{line.source}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </TabPanel>

          {/* Revenue Drivers Panel */}
          <TabPanel id="revenue">
            <form
              onSubmit={(e) => {
                e.preventDefault();
                if (!selectedVersionId) return;
                void runMutation(() =>
                  addRevenue.mutateAsync({
                    planVersionId: selectedVersionId,
                    legalEntityId,
                    periodStart: revPeriod,
                    driverKey: revKey,
                    volume: Number(revVolume) || 0,
                    price: Number(revPrice) || 0,
                    currencyCode,
                    source: "Model Driver"
                  })
                );
              }}
              style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", gap: "1rem", marginTop: "1rem", alignItems: "end" }}
            >
              <TextField label={t("fields.driverKey")} value={revKey} onChange={setRevKey} isRequired />
              <LocalDateField label={t("fields.period")} value={revPeriod} onChange={setRevPeriod} />
              <TextField label={t("fields.volume")} value={revVolume} onChange={setRevVolume} type="number" />
              <MoneyField label={t("fields.price")} value={{ amount: revPrice, currency: currencyCode }} onChange={(v) => setRevPrice(v.amount)} defaultCurrency={currencyCode} />
              <Button type="submit" variant="primary" isDisabled={!canEditVersions || !isDraft || addRevenue.isPending}>
                {t("actions.saveChanges")}
              </Button>
            </form>
          </TabPanel>

          {/* Workforce Panel */}
          {canReadWorkforce ? (
            <TabPanel id="workforce">
              <div style={{ margin: "1rem 0", display: "flex", justifyContent: "space-between", alignItems: "center", gap: "1rem", flexWrap: "wrap" }}>
                {!canReadSensitiveWorkforce ? (
                  <Alert variant="info">{t("messages.workforceConfidentialNotice")}</Alert>
                ) : <div />}
                {canEditVersions && isDraft ? (
                  <Button variant="primary" onPress={() => setIsWorkforceModalOpen(true)}>+ {t("actions.addLine")}</Button>
                ) : null}
              </div>

              {workforceQuery.isLoading ? (
                <LoadingState message={t("messages.loading")} />
              ) : (workforceQuery.data ?? []).length === 0 ? (
                <EmptyState title={t("messages.noData")} />
              ) : (
                <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "0.875rem" }}>
                  <thead>
                    <tr style={{ borderBottom: "2px solid #e2e8f0", textAlign: "start", color: "#475569" }}>
                      <th style={{ padding: "8px 12px" }}>{t("fields.period")}</th>
                      <th style={{ padding: "8px 12px" }}>{t("fields.fte")}</th>
                      <th style={{ padding: "8px 12px" }}>{t("fields.averageCost")}</th>
                      <th style={{ padding: "8px 12px" }}>{t("fields.source")}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {(workforceQuery.data ?? []).map((wf) => (
                      <tr key={wf.id} style={{ borderBottom: "1px solid #f1f5f9" }}>
                        <td style={{ padding: "8px 12px" }}><LocalDateDisplay value={wf.periodStart} /></td>
                        <td style={{ padding: "8px 12px", fontWeight: 700 }}>{wf.fte}</td>
                        <td style={{ padding: "8px 12px" }}><MoneyDisplay value={toMoney(wf.averageCost, wf.currencyCode)} /></td>
                        <td style={{ padding: "8px 12px", color: "#64748b" }}>{wf.source}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}

              {(workforceImpactQuery.data ?? []).length > 0 ? (
                <div style={{ marginTop: "1rem", fontSize: "0.85rem", color: "#475569" }}>
                  {t("labels.totalWorkforceImpact")}:{" "}
                  <MoneyDisplay
                    value={toMoney((workforceImpactQuery.data ?? []).reduce((acc, i) => acc + i.cost, 0), currencyCode)}
                  />
                </div>
              ) : null}
            </TabPanel>
          ) : null}

          {/* CapEx Panel */}
          {canReadCapex ? (
            <TabPanel id="capex">
              <div style={{ margin: "1rem 0", display: "flex", justifyContent: "space-between", alignItems: "center", gap: "1rem", flexWrap: "wrap" }}>
                <Alert variant="info">{t("messages.capexDisclaimer")}</Alert>
                {canManageCapex && isDraft ? (
                  <Button variant="primary" onPress={() => setIsCapexModalOpen(true)}>+ {t("actions.addLine")}</Button>
                ) : null}
              </div>

              {capexQuery.isLoading ? (
                <LoadingState message={t("messages.loading")} />
              ) : (capexQuery.data ?? []).length === 0 ? (
                <EmptyState title={t("messages.noData")} />
              ) : (
                <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "0.875rem" }}>
                  <thead>
                    <tr style={{ borderBottom: "2px solid #e2e8f0", textAlign: "start", color: "#475569" }}>
                      <th style={{ padding: "8px 12px" }}>{t("fields.name")}</th>
                      <th style={{ padding: "8px 12px" }}>{t("fields.category")}</th>
                      <th style={{ padding: "8px 12px" }}>{t("fields.plannedAmount")}</th>
                      <th style={{ padding: "8px 12px" }}>{t("fields.purchasePeriod")}</th>
                      <th style={{ padding: "8px 12px" }}>{t("fields.priority")}</th>
                      <th style={{ padding: "8px 12px" }}>{t("fields.horizon")}</th>
                      <th style={{ padding: "8px 12px" }}></th>
                    </tr>
                  </thead>
                  <tbody>
                    {(capexQuery.data ?? []).map((item) => (
                      <tr key={item.id} style={{ borderBottom: "1px solid #f1f5f9" }}>
                        <td style={{ padding: "8px 12px", fontWeight: 700 }}>{item.name}</td>
                        <td style={{ padding: "8px 12px" }}>{item.category}</td>
                        <td style={{ padding: "8px 12px" }}><MoneyDisplay value={toMoney(item.plannedAmount, item.currencyCode)} /></td>
                        <td style={{ padding: "8px 12px" }}><LocalDateDisplay value={item.purchasePeriod} /></td>
                        <td style={{ padding: "8px 12px" }}>{item.priority}</td>
                        <td style={{ padding: "8px 12px" }}><PlanningStatusBadge status={item.state} /></td>
                        <td style={{ padding: "8px 12px", display: "flex", gap: "0.35rem" }}>
                          {canManageCapex && item.state === "Draft" ? (
                            <Button variant="secondary" onPress={() => void runMutation(() => prioritizeCapex.mutateAsync({ id: item.id, priority: 1 }))}>
                              {t("labels.priorityOne")}
                            </Button>
                          ) : null}
                          {canManageCapex && item.state !== "Approved" ? (
                            <Button variant="primary" onPress={() => void runMutation(() => approveCapex.mutateAsync(item.id))}>
                              {t("actions.approve")}
                            </Button>
                          ) : null}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </TabPanel>
          ) : null}

          {/* Working Capital & FX Panel */}
          <TabPanel id="wc-fx">
            <div style={{ margin: "1rem 0" }}>
              <Alert variant="info">{t("messages.fxDisclaimer")}</Alert>
            </div>
            <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(280px, 1fr))", gap: "1.5rem" }}>
              <form
                onSubmit={(e) => {
                  e.preventDefault();
                  if (!selectedVersionId) return;
                  void runMutation(() =>
                    addWorkingCapital.mutateAsync({
                      planVersionId: selectedVersionId,
                      legalEntityId,
                      periodStart: wcPeriod,
                      dso: Number(wcDso) || 0,
                      dpo: Number(wcDpo) || 0,
                      inventoryDays: Number(wcInv) || 0
                    })
                  );
                }}
                style={{ display: "flex", flexDirection: "column", gap: "0.75rem" }}
              >
                <LocalDateField label={t("fields.period")} value={wcPeriod} onChange={setWcPeriod} />
                <TextField label={t("fields.dso")} value={wcDso} onChange={setWcDso} type="number" />
                <TextField label={t("fields.dpo")} value={wcDpo} onChange={setWcDpo} type="number" />
                <TextField label={t("fields.inventoryDays")} value={wcInv} onChange={setWcInv} type="number" />
                <Button type="submit" variant="primary" isDisabled={!canEditVersions || !isDraft || addWorkingCapital.isPending}>
                  {t("actions.saveChanges")}
                </Button>
              </form>

              <form
                onSubmit={(e) => {
                  e.preventDefault();
                  if (!selectedVersionId) return;
                  void runMutation(() =>
                    addFx.mutateAsync({
                      planVersionId: selectedVersionId,
                      periodStart: fxPeriod,
                      fromCurrency: fxFrom,
                      toCurrency: currencyCode,
                      rate: Number(fxRate) || 1,
                      rateType: "Planning",
                      source: "Manual Planning FX"
                    })
                  );
                }}
                style={{ display: "flex", flexDirection: "column", gap: "0.75rem" }}
              >
                <LocalDateField label={t("fields.period")} value={fxPeriod} onChange={setFxPeriod} />
                <TextField label={t("fields.fromCurrency")} value={fxFrom} onChange={setFxFrom} />
                <TextField label={t("fields.rate")} value={fxRate} onChange={setFxRate} type="number" />
                <Button type="submit" variant="primary" isDisabled={!canEditVersions || !isDraft || addFx.isPending}>
                  {t("actions.saveChanges")}
                </Button>
              </form>
            </div>
          </TabPanel>

          {/* Assumptions Panel */}
          <TabPanel id="assumptions">
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", margin: "1rem 0" }}>
              <h3 style={{ margin: 0, fontSize: "1rem" }}>{t("labels.assumptions")}</h3>
              {canManageAssumptions && isDraft ? (
                <Button variant="primary" onPress={() => setIsAssumptionModalOpen(true)}>+ {t("actions.addLine")}</Button>
              ) : null}
            </div>
            {(assumptionsQuery.data ?? []).length === 0 ? (
              <EmptyState title={t("messages.noData")} />
            ) : (
              <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "0.875rem" }}>
                <thead>
                  <tr style={{ borderBottom: "2px solid #e2e8f0", textAlign: "start", color: "#475569" }}>
                    <th style={{ padding: "8px 12px" }}>{t("fields.code")}</th>
                    <th style={{ padding: "8px 12px" }}>{t("fields.name")}</th>
                    <th style={{ padding: "8px 12px" }}>{t("fields.unit")}</th>
                  </tr>
                </thead>
                <tbody>
                  {(assumptionsQuery.data ?? []).map((a) => (
                    <tr key={a.id} style={{ borderBottom: "1px solid #f1f5f9" }}>
                      <td style={{ padding: "8px 12px", fontWeight: 700 }}>{a.code}</td>
                      <td style={{ padding: "8px 12px" }}>{a.name}</td>
                      <td style={{ padding: "8px 12px" }}>{a.unit}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </TabPanel>
        </Tabs>
      </Surface>

      {/* OPEX Modal */}
      <Dialog isOpen={isOpexModalOpen} onClose={() => setIsOpexModalOpen(false)} title={t("labels.opex")} size="small">
        <form
          onSubmit={(e) => {
            e.preventDefault();
            if (!selectedVersionId) return;
            void runMutation(async () => {
              await addOpex.mutateAsync({
                planVersionId: selectedVersionId,
                legalEntityId,
                categoryCode: opexCategory,
                periodStart: opexPeriod,
                method: opexMethod,
                baseAmount: Number(opexBase) || 0,
                driverValue: 0,
                rate: 0,
                growthPercent: 0,
                inflationPercent: 0,
                currencyCode
              });
              setIsOpexModalOpen(false);
            });
          }}
          style={{ display: "flex", flexDirection: "column", gap: "1rem" }}
        >
          <TextField label={t("fields.category")} value={opexCategory} onChange={setOpexCategory} isRequired />
          <Select
            label={t("fields.method")}
            selectedKey={opexMethod}
            onSelectionChange={(k) => k && setOpexMethod(k as typeof opexMethod)}
            options={[
              { id: "Fixed", label: "Fixed" },
              { id: "Variable", label: "Variable" },
              { id: "Recurring", label: "Recurring" },
              { id: "DriverBased", label: "DriverBased" }
            ]}
          />
          <LocalDateField label={t("fields.period")} value={opexPeriod} onChange={setOpexPeriod} />
          <MoneyField label={t("fields.baseAmount")} value={{ amount: opexBase, currency: currencyCode }} onChange={(v) => setOpexBase(v.amount)} defaultCurrency={currencyCode} />
          <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem" }}>
            <Button type="button" variant="secondary" onPress={() => setIsOpexModalOpen(false)}>{t("actions.cancel")}</Button>
            <Button type="submit" variant="primary" isDisabled={addOpex.isPending}>{t("actions.confirm")}</Button>
          </div>
        </form>
      </Dialog>

      {/* Workforce Modal */}
      <Dialog isOpen={isWorkforceModalOpen} onClose={() => setIsWorkforceModalOpen(false)} title={t("labels.workforce")} size="small">
        <form
          onSubmit={(e) => {
            e.preventDefault();
            if (!selectedVersionId) return;
            void runMutation(async () => {
              await addWorkforce.mutateAsync({
                planVersionId: selectedVersionId,
                legalEntityId,
                periodStart: wfPeriod,
                fte: Number(wfFte) || 0,
                averageCost: Number(wfCost) || 0,
                currencyCode
              });
              setIsWorkforceModalOpen(false);
            });
          }}
          style={{ display: "flex", flexDirection: "column", gap: "1rem" }}
        >
          <LocalDateField label={t("fields.period")} value={wfPeriod} onChange={setWfPeriod} />
          <TextField label={t("fields.fte")} value={wfFte} onChange={setWfFte} type="number" />
          <MoneyField label={t("fields.averageCost")} value={{ amount: wfCost, currency: currencyCode }} onChange={(v) => setWfCost(v.amount)} defaultCurrency={currencyCode} />
          <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem" }}>
            <Button type="button" variant="secondary" onPress={() => setIsWorkforceModalOpen(false)}>{t("actions.cancel")}</Button>
            <Button type="submit" variant="primary" isDisabled={addWorkforce.isPending}>{t("actions.confirm")}</Button>
          </div>
        </form>
      </Dialog>

      {/* CapEx Modal */}
      <Dialog isOpen={isCapexModalOpen} onClose={() => setIsCapexModalOpen(false)} title={t("labels.capex")} size="small">
        <form
          onSubmit={(e) => {
            e.preventDefault();
            if (!selectedVersionId) return;
            void runMutation(async () => {
              await addCapex.mutateAsync({
                planVersionId: selectedVersionId,
                legalEntityId,
                name: capexName,
                category: capexCategory,
                plannedAmount: Number(capexAmount) || 0,
                currencyCode,
                purchasePeriod: capexPeriod,
                priority: 1
              });
              setIsCapexModalOpen(false);
            });
          }}
          style={{ display: "flex", flexDirection: "column", gap: "1rem" }}
        >
          <TextField label={t("fields.name")} value={capexName} onChange={setCapexName} isRequired />
          <TextField label={t("fields.category")} value={capexCategory} onChange={setCapexCategory} isRequired />
          <MoneyField label={t("fields.plannedAmount")} value={{ amount: capexAmount, currency: currencyCode }} onChange={(v) => setCapexAmount(v.amount)} defaultCurrency={currencyCode} />
          <LocalDateField label={t("fields.purchasePeriod")} value={capexPeriod} onChange={setCapexPeriod} />
          <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem" }}>
            <Button type="button" variant="secondary" onPress={() => setIsCapexModalOpen(false)}>{t("actions.cancel")}</Button>
            <Button type="submit" variant="primary" isDisabled={addCapex.isPending}>{t("actions.confirm")}</Button>
          </div>
        </form>
      </Dialog>

      {/* Assumption Modal */}
      <Dialog isOpen={isAssumptionModalOpen} onClose={() => setIsAssumptionModalOpen(false)} title={t("fields.assumptionSection")} size="small">
        <form
          onSubmit={(e) => {
            e.preventDefault();
            if (!selectedVersionId) return;
            void runMutation(async () => {
              await createAssumption.mutateAsync({
                planVersionId: selectedVersionId,
                code: assumptionCode,
                name: assumptionName,
                unit: assumptionUnit
              });
              setIsAssumptionModalOpen(false);
            });
          }}
          style={{ display: "flex", flexDirection: "column", gap: "1rem" }}
        >
          <TextField label={t("fields.code")} value={assumptionCode} onChange={setAssumptionCode} isRequired />
          <TextField label={t("fields.name")} value={assumptionName} onChange={setAssumptionName} isRequired />
          <TextField label={t("fields.unit")} value={assumptionUnit} onChange={setAssumptionUnit} isRequired />
          <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem" }}>
            <Button type="button" variant="secondary" onPress={() => setIsAssumptionModalOpen(false)}>{t("actions.cancel")}</Button>
            <Button type="submit" variant="primary" isDisabled={createAssumption.isPending}>{t("actions.confirm")}</Button>
          </div>
        </form>
      </Dialog>
    </div>
  );
}

import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../components/pages/surface.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { Select } from "../../../components/primitives/select.tsx";
import { LocalDateField } from "../../../components/fields/local-date-field.tsx";
import { MoneyField } from "../../../components/fields/money-field.tsx";
import { LocalDateDisplay } from "../../../components/fields/local-date-display.tsx";
import { MoneyDisplay } from "../../../components/fields/money-display.tsx";
import { Alert } from "../../../components/primitives/alert.tsx";
import { EmptyState } from "../../../components/primitives/empty-state.tsx";
import { LoadingState } from "../../../components/primitives/loading-state.tsx";
import { PlanningHeader } from "../components/planning-header.tsx";
import { PlanningStatusBadge } from "../components/planning-status-badge.tsx";
import { PlanningNoScopePrompt } from "../components/planning-no-scope-prompt.tsx";
import { usePlanningScope } from "../hooks/use-planning-scope.ts";
import { usePlanningAccess } from "../hooks/use-planning-access.ts";
import {
  usePlanningCycles,
  useCreatePlanningCycle,
  useAdvancePlanningCycle,
  usePlanVersions,
  useCreatePlanVersion,
  useClonePlanVersion,
  useAdvancePlanVersion,
  useCreateBaseline,
  useApproveRevision,
  useCreateBudgetTransfer,
  useCurrentBudget
} from "../hooks/use-planning-queries.ts";
import { mapPlanningError } from "../services/planning-error-map.ts";
import { toMoney } from "../utils.ts";

export function PlanningBudgetsPage(): React.JSX.Element {
  const { t } = useTranslation("planning");
  const { legalEntityId, currencyCode, isReady } = usePlanningScope();
  const {
    canManageCycles,
    canEditVersions,
    canSubmitVersions,
    canApproveVersions,
    canPublishVersions,
    canManageBudget,
    canReviseBudget,
    canTransferBudget
  } = usePlanningAccess();

  const cyclesQuery = usePlanningCycles(legalEntityId, isReady);
  const versionsQuery = usePlanVersions(isReady);

  const createCycle = useCreatePlanningCycle();
  const advanceCycle = useAdvancePlanningCycle();
  const createVersion = useCreatePlanVersion();
  const cloneVersion = useClonePlanVersion();
  const advanceVersion = useAdvancePlanVersion();
  const createBaseline = useCreateBaseline();
  const approveRevision = useApproveRevision();
  const createTransfer = useCreateBudgetTransfer();

  const [feedback, setFeedback] = useState<{ tone: "success" | "danger"; text: string } | null>(null);
  const [isCycleModalOpen, setIsCycleModalOpen] = useState(false);
  const [isVersionModalOpen, setIsVersionModalOpen] = useState(false);
  const [isBaselineModalOpen, setIsBaselineModalOpen] = useState(false);
  const [isRevisionModalOpen, setIsRevisionModalOpen] = useState(false);
  const [isTransferModalOpen, setIsTransferModalOpen] = useState(false);

  // Cycle form
  const [cycleName, setCycleName] = useState("FY2027 Budget Cycle");
  const [cycleType, setCycleType] = useState("AnnualBudget");
  const [horizon, setHorizon] = useState("Monthly");
  const [startPeriod, setStartPeriod] = useState("2027-01-01");
  const [endPeriod, setEndPeriod] = useState("2027-12-01");

  // Version form
  const [versionName, setVersionName] = useState("FY2027 Draft v1");
  const [versionCycleId, setVersionCycleId] = useState("");
  const [versionKind, setVersionKind] = useState("Budget");

  // Baseline & Revision & Transfer form
  const [baselineIdInput, setBaselineIdInput] = useState("");
  const [baselinePlanVersionId, setBaselinePlanVersionId] = useState("");
  const [baselineName, setBaselineName] = useState("FY2027 Baseline");
  const [baselineTotal, setBaselineTotal] = useState("150000");
  const [revDelta, setRevDelta] = useState("10000");
  const [revReason, setRevReason] = useState("");
  const [transferFromCoord, setTransferFromCoord] = useState("");
  const [transferToCoord, setTransferToCoord] = useState("");
  const [transferAmount, setTransferAmount] = useState("5000");
  const [transferReason, setTransferReason] = useState("");

  const currentBudgetQuery = useCurrentBudget(baselineIdInput);

  if (!isReady) {
    return <PlanningNoScopePrompt />;
  }

  const cycles = cyclesQuery.data ?? [];
  const versions = versionsQuery.data ?? [];
  const defaultModelVersionId = versions[0]?.modelVersionId ?? "";

  const handleAction = async (fn: () => Promise<unknown>) => {
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
    <div data-testid="planning-budgets-page" style={{ padding: "0 0 2rem 0" }}>
      <PlanningHeader
        title={t("navigation.budgets")}
        subtitle={t("subtitle")}
        primaryAction={
          canManageCycles
            ? { label: `+ ${t("actions.createCycle")}`, onPress: () => setIsCycleModalOpen(true) }
            : undefined
        }
        secondaryActions={
          <div style={{ display: "flex", gap: "0.5rem", flexWrap: "wrap" }}>
            {canEditVersions && cycles.length > 0 && defaultModelVersionId ? (
              <Button variant="secondary" onPress={() => {
                setVersionCycleId(cycles[0]!.id);
                setIsVersionModalOpen(true);
              }}>
                + {t("actions.createVersion")}
              </Button>
            ) : null}
            {canManageBudget && versions.length > 0 ? (
              <Button variant="secondary" onPress={() => {
                setBaselinePlanVersionId(versions[0]!.id);
                setIsBaselineModalOpen(true);
              }}>
                {t("actions.createBaseline")}
              </Button>
            ) : null}
            {canReviseBudget && baselineIdInput ? (
              <Button variant="secondary" onPress={() => setIsRevisionModalOpen(true)}>
                {t("actions.requestRevision")}
              </Button>
            ) : null}
            {canTransferBudget && baselineIdInput ? (
              <Button variant="secondary" onPress={() => setIsTransferModalOpen(true)}>
                {t("actions.requestTransfer")}
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

      {/* Planning Cycles Table */}
      <Surface style={{ padding: "1.25rem", marginBottom: "1.25rem" }}>
        <h2 style={{ margin: "0 0 1rem 0", fontSize: "1.05rem", color: "#0f172a" }}>{t("fields.cycle")}</h2>
        {cyclesQuery.isLoading ? (
          <LoadingState message={t("messages.loading")} />
        ) : cycles.length === 0 ? (
          <EmptyState title={t("messages.noData")} />
        ) : (
          <div style={{ overflowX: "auto" }}>
            <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "0.875rem" }}>
              <thead>
                <tr style={{ borderBottom: "2px solid #e2e8f0", textAlign: "start", color: "#475569" }}>
                  <th style={{ padding: "10px 12px" }}>{t("fields.name")}</th>
                  <th style={{ padding: "10px 12px" }}>{t("fields.cycleType")}</th>
                  <th style={{ padding: "10px 12px" }}>{t("fields.startPeriod")}</th>
                  <th style={{ padding: "10px 12px" }}>{t("fields.endPeriod")}</th>
                  <th style={{ padding: "10px 12px" }}>{t("fields.horizon")}</th>
                  <th style={{ padding: "10px 12px" }}></th>
                </tr>
              </thead>
              <tbody>
                {cycles.map((c) => (
                  <tr key={c.id} style={{ borderBottom: "1px solid #f1f5f9" }}>
                    <td style={{ padding: "10px 12px", fontWeight: 700, color: "#102a43" }}>{c.name}</td>
                    <td style={{ padding: "10px 12px" }}>{c.cycleType}</td>
                    <td style={{ padding: "10px 12px" }}><LocalDateDisplay value={c.startPeriod} /></td>
                    <td style={{ padding: "10px 12px" }}><LocalDateDisplay value={c.endPeriod} /></td>
                    <td style={{ padding: "10px 12px" }}><PlanningStatusBadge status={c.state} /></td>
                    <td style={{ padding: "10px 12px", display: "flex", gap: "0.35rem", flexWrap: "wrap" }}>
                      {canManageCycles && c.state === "Draft" ? (
                        <Button variant="secondary" onPress={() => void handleAction(() => advanceCycle.mutateAsync({ id: c.id, action: "open" }))}>
                          {t("actions.openCycle")}
                        </Button>
                      ) : null}
                      {canApproveVersions && (c.state === "Open" || c.state === "Review" || c.state === "Collecting") ? (
                        <Button variant="primary" onPress={() => void handleAction(() => advanceCycle.mutateAsync({ id: c.id, action: "approve" }))}>
                          {t("actions.approve")}
                        </Button>
                      ) : null}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Surface>

      {/* Plan Versions Table */}
      <Surface style={{ padding: "1.25rem", marginBottom: "1.25rem" }}>
        <h2 style={{ margin: "0 0 1rem 0", fontSize: "1.05rem", color: "#0f172a" }}>{t("fields.planVersion")}</h2>
        {versionsQuery.isLoading ? (
          <LoadingState message={t("messages.loading")} />
        ) : versions.length === 0 ? (
          <EmptyState title={t("messages.noData")} />
        ) : (
          <div style={{ overflowX: "auto" }}>
            <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "0.875rem" }}>
              <thead>
                <tr style={{ borderBottom: "2px solid #e2e8f0", textAlign: "start", color: "#475569" }}>
                  <th style={{ padding: "10px 12px" }}>{t("fields.name")}</th>
                  <th style={{ padding: "10px 12px" }}>{t("fields.versionKind")}</th>
                  <th style={{ padding: "10px 12px" }}>{t("fields.actualCutoff")}</th>
                  <th style={{ padding: "10px 12px" }}>{t("fields.horizon")}</th>
                  <th style={{ padding: "10px 12px" }}></th>
                </tr>
              </thead>
              <tbody>
                {versions.map((v) => (
                  <tr key={v.id} style={{ borderBottom: "1px solid #f1f5f9" }}>
                    <td style={{ padding: "10px 12px", fontWeight: 700, color: "#102a43" }}>{v.name}</td>
                    <td style={{ padding: "10px 12px" }}>{v.versionKind}</td>
                    <td style={{ padding: "10px 12px" }}>{v.actualCutoff ? <LocalDateDisplay value={v.actualCutoff} /> : "—"}</td>
                    <td style={{ padding: "10px 12px" }}><PlanningStatusBadge status={v.state} /></td>
                    <td style={{ padding: "10px 12px", display: "flex", gap: "0.35rem", flexWrap: "wrap" }}>
                      {canSubmitVersions && v.state === "Draft" ? (
                        <Button variant="secondary" onPress={() => void handleAction(() => advanceVersion.mutateAsync({ id: v.id, action: "submit" }))}>
                          {t("actions.submit")}
                        </Button>
                      ) : null}
                      {canApproveVersions && v.state === "Review" ? (
                        <Button variant="secondary" onPress={() => void handleAction(() => advanceVersion.mutateAsync({ id: v.id, action: "approve" }))}>
                          {t("actions.approve")}
                        </Button>
                      ) : null}
                      {canPublishVersions && v.state === "Approved" ? (
                        <Button variant="primary" onPress={() => void handleAction(() => advanceVersion.mutateAsync({ id: v.id, action: "publish" }))}>
                          {t("actions.publish")}
                        </Button>
                      ) : null}
                      {canEditVersions ? (
                        <Button variant="secondary" onPress={() => void handleAction(() => cloneVersion.mutateAsync({ id: v.id, name: `${v.name} (Copy)` }))}>
                          {t("actions.clone")}
                        </Button>
                      ) : null}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Surface>

      {/* Current Budget Lookup (Original vs Revisions vs Transfers) */}
      {currentBudgetQuery.data ? (
        <Surface style={{ padding: "1.25rem" }}>
          <h3 style={{ margin: "0 0 0.75rem 0", fontSize: "1rem", color: "#0f172a" }}>{t("kpi.currentBudget")}</h3>
          <div style={{ display: "flex", gap: "2rem", flexWrap: "wrap" }}>
            <div>
              <div style={{ fontSize: "0.75rem", color: "#64748b" }}>{t("kpi.originalBudget")}</div>
              <div style={{ fontWeight: 700 }}><MoneyDisplay value={toMoney(currentBudgetQuery.data.originalTotal, currentBudgetQuery.data.currencyCode)} /></div>
            </div>
            <div>
              <div style={{ fontSize: "0.75rem", color: "#64748b" }}>{t("kpi.currentBudget")}</div>
              <div style={{ fontWeight: 700 }}><MoneyDisplay value={toMoney(currentBudgetQuery.data.currentBudget, currentBudgetQuery.data.currencyCode)} /></div>
            </div>
          </div>
        </Surface>
      ) : null}

      {/* Create Cycle Modal */}
      <Dialog isOpen={isCycleModalOpen} onClose={() => setIsCycleModalOpen(false)} title={t("actions.createCycle")} size="medium">
        <form
          onSubmit={(e) => {
            e.preventDefault();
            void handleAction(async () => {
              await createCycle.mutateAsync({ legalEntityId, name: cycleName, cycleType, horizon, startPeriod, endPeriod });
              setIsCycleModalOpen(false);
            });
          }}
          style={{ display: "flex", flexDirection: "column", gap: "1rem" }}
        >
          <TextField label={t("fields.name")} value={cycleName} onChange={setCycleName} isRequired />
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
            <Select
              label={t("fields.cycleType")}
              selectedKey={cycleType}
              onSelectionChange={(k) => k && setCycleType(String(k))}
              options={[
                { id: "AnnualBudget", label: "AnnualBudget" },
                { id: "RollingForecast", label: "RollingForecast" },
                { id: "StrategicPlan", label: "StrategicPlan" }
              ]}
            />
            <Select
              label={t("fields.horizon")}
              selectedKey={horizon}
              onSelectionChange={(k) => k && setHorizon(String(k))}
              options={[
                { id: "Monthly", label: "Monthly" },
                { id: "Quarterly", label: "Quarterly" },
                { id: "Annual", label: "Annual" }
              ]}
            />
          </div>
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
            <LocalDateField label={t("fields.startPeriod")} value={startPeriod} onChange={setStartPeriod} />
            <LocalDateField label={t("fields.endPeriod")} value={endPeriod} onChange={setEndPeriod} />
          </div>
          <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem" }}>
            <Button type="button" variant="secondary" onPress={() => setIsCycleModalOpen(false)}>{t("actions.cancel")}</Button>
            <Button type="submit" variant="primary" isDisabled={createCycle.isPending}>{t("actions.confirm")}</Button>
          </div>
        </form>
      </Dialog>

      {/* Create Plan Version Modal */}
      <Dialog isOpen={isVersionModalOpen} onClose={() => setIsVersionModalOpen(false)} title={t("actions.createVersion")} size="medium">
        <form
          onSubmit={(e) => {
            e.preventDefault();
            void handleAction(async () => {
              await createVersion.mutateAsync({ cycleId: versionCycleId, modelVersionId: defaultModelVersionId, name: versionName, versionKind });
              setIsVersionModalOpen(false);
            });
          }}
          style={{ display: "flex", flexDirection: "column", gap: "1rem" }}
        >
          <TextField label={t("fields.name")} value={versionName} onChange={setVersionName} isRequired />
          <Select
            label={t("fields.cycle")}
            selectedKey={versionCycleId}
            onSelectionChange={(k) => k && setVersionCycleId(String(k))}
            options={cycles.map((c) => ({ id: c.id, label: c.name }))}
          />
          <Select
            label={t("fields.versionKind")}
            selectedKey={versionKind}
            onSelectionChange={(k) => k && setVersionKind(String(k))}
            options={[
              { id: "Budget", label: "Budget" },
              { id: "Forecast", label: "Forecast" },
              { id: "Scenario", label: "Scenario" }
            ]}
          />
          <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem" }}>
            <Button type="button" variant="secondary" onPress={() => setIsVersionModalOpen(false)}>{t("actions.cancel")}</Button>
            <Button type="submit" variant="primary" isDisabled={createVersion.isPending}>{t("actions.confirm")}</Button>
          </div>
        </form>
      </Dialog>

      {/* Create Baseline Modal */}
      <Dialog isOpen={isBaselineModalOpen} onClose={() => setIsBaselineModalOpen(false)} title={t("actions.createBaseline")} size="medium">
        <form
          onSubmit={(e) => {
            e.preventDefault();
            void handleAction(async () => {
              const res = await createBaseline.mutateAsync({
                planVersionId: baselinePlanVersionId,
                name: baselineName,
                originalTotal: Number(baselineTotal) || 0,
                currencyCode
              });
              setBaselineIdInput(res.id);
              setIsBaselineModalOpen(false);
            });
          }}
          style={{ display: "flex", flexDirection: "column", gap: "1rem" }}
        >
          <TextField label={t("fields.name")} value={baselineName} onChange={setBaselineName} isRequired />
          <Select
            label={t("fields.planVersion")}
            selectedKey={baselinePlanVersionId}
            onSelectionChange={(k) => k && setBaselinePlanVersionId(String(k))}
            options={versions.map((v) => ({ id: v.id, label: v.name }))}
          />
          <MoneyField label={t("kpi.originalBudget")} value={{ amount: baselineTotal, currency: currencyCode }} onChange={(v) => setBaselineTotal(v.amount)} defaultCurrency={currencyCode} />
          <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem" }}>
            <Button type="button" variant="secondary" onPress={() => setIsBaselineModalOpen(false)}>{t("actions.cancel")}</Button>
            <Button type="submit" variant="primary" isDisabled={createBaseline.isPending}>{t("actions.confirm")}</Button>
          </div>
        </form>
      </Dialog>

      {/* Revision Modal */}
      <Dialog isOpen={isRevisionModalOpen} onClose={() => setIsRevisionModalOpen(false)} title={t("actions.requestRevision")} size="small">
        <form
          onSubmit={(e) => {
            e.preventDefault();
            void handleAction(async () => {
              await approveRevision.mutateAsync({ baselineId: baselineIdInput, request: { deltaAmount: Number(revDelta) || 0, reason: revReason } });
              setIsRevisionModalOpen(false);
            });
          }}
          style={{ display: "flex", flexDirection: "column", gap: "1rem" }}
        >
          <MoneyField label={t("fields.deltaAmount")} value={{ amount: revDelta, currency: currencyCode }} onChange={(v) => setRevDelta(v.amount)} defaultCurrency={currencyCode} />
          <TextField label={t("fields.reason")} value={revReason} onChange={setRevReason} isRequired />
          <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem" }}>
            <Button type="button" variant="secondary" onPress={() => setIsRevisionModalOpen(false)}>{t("actions.cancel")}</Button>
            <Button type="submit" variant="primary" isDisabled={approveRevision.isPending}>{t("actions.confirm")}</Button>
          </div>
        </form>
      </Dialog>

      {/* Transfer Modal */}
      <Dialog isOpen={isTransferModalOpen} onClose={() => setIsTransferModalOpen(false)} title={t("actions.requestTransfer")} size="medium">
        <form
          onSubmit={(e) => {
            e.preventDefault();
            void handleAction(async () => {
              await createTransfer.mutateAsync({
                budgetId: baselineIdInput,
                request: { fromCoordinateId: transferFromCoord, toCoordinateId: transferToCoord, amount: Number(transferAmount) || 0, reason: transferReason }
              });
              setIsTransferModalOpen(false);
            });
          }}
          style={{ display: "flex", flexDirection: "column", gap: "1rem" }}
        >
          <TextField label={t("fields.fromCoordinate")} value={transferFromCoord} onChange={setTransferFromCoord} isRequired />
          <TextField label={t("fields.toCoordinate")} value={transferToCoord} onChange={setTransferToCoord} isRequired />
          <MoneyField label={t("fields.amount")} value={{ amount: transferAmount, currency: currencyCode }} onChange={(v) => setTransferAmount(v.amount)} defaultCurrency={currencyCode} />
          <TextField label={t("fields.reason")} value={transferReason} onChange={setTransferReason} isRequired />
          <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem" }}>
            <Button type="button" variant="secondary" onPress={() => setIsTransferModalOpen(false)}>{t("actions.cancel")}</Button>
            <Button type="submit" variant="primary" isDisabled={createTransfer.isPending}>{t("actions.confirm")}</Button>
          </div>
        </form>
      </Dialog>
    </div>
  );
}

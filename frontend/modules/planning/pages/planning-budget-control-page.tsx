import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../components/pages/surface.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { Select } from "../../../components/primitives/select.tsx";
import { MoneyField } from "../../../components/fields/money-field.tsx";
import { MoneyDisplay } from "../../../components/fields/money-display.tsx";
import { Alert } from "../../../components/primitives/alert.tsx";
import { PlanningHeader } from "../components/planning-header.tsx";
import { PlanningStatusBadge } from "../components/planning-status-badge.tsx";
import { PlanningNoScopePrompt } from "../components/planning-no-scope-prompt.tsx";
import { usePlanningScope } from "../hooks/use-planning-scope.ts";
import { usePlanningAccess } from "../hooks/use-planning-access.ts";
import {
  useBucketAvailability,
  useCheckBudget,
  useRequestBudgetOverride,
  useDecideBudgetOverride,
  useReconcileBudgetControl,
  useCreateBudgetPolicy
} from "../hooks/use-planning-queries.ts";
import { mapPlanningError } from "../services/planning-error-map.ts";
import { toMoney } from "../utils.ts";
import type { BudgetCheckView, BudgetControlReconciliationView, BudgetOverrideView } from "../types.ts";

export function PlanningBudgetControlPage(): React.JSX.Element {
  const { t } = useTranslation("planning");
  const { legalEntityId, currencyCode, isReady } = usePlanningScope();
  const { canManageBudget, canRequestOverride, canApproveOverride } = usePlanningAccess();

  const [bucketIdInput, setBucketIdInput] = useState("");
  const [proposedAmount, setProposedAmount] = useState("10000");
  const [checkResult, setCheckResult] = useState<BudgetCheckView | null>(null);
  const [overrideResult, setOverrideResult] = useState<BudgetOverrideView | null>(null);
  const [reconcileResult, setReconcileResult] = useState<BudgetControlReconciliationView | null>(null);
  const [feedback, setFeedback] = useState<{ tone: "success" | "danger"; text: string } | null>(null);

  const [isPolicyOpen, setIsPolicyOpen] = useState(false);
  const [policyName, setPolicyName] = useState("Strict OPEX Control");
  const [policyMode, setPolicyMode] = useState("Warn");
  const [policyBasis, setPolicyBasis] = useState("CommitmentAndActual");
  const [policyThreshold, setPolicyThreshold] = useState("90");

  const [isOverrideOpen, setIsOverrideOpen] = useState(false);
  const [overrideDocId, setOverrideDocId] = useState("PO-2027-001");
  const [overrideReason, setOverrideReason] = useState("");

  const availabilityQuery = useBucketAvailability(bucketIdInput);
  const checkBudget = useCheckBudget();
  const requestOverride = useRequestBudgetOverride();
  const decideOverride = useDecideBudgetOverride();
  const reconcileControl = useReconcileBudgetControl();
  const createPolicy = useCreateBudgetPolicy();

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
    <div data-testid="planning-budget-control-page" style={{ padding: "0 0 2rem 0" }}>
      <PlanningHeader
        title={t("navigation.budgetControl")}
        subtitle={t("subtitle")}
        primaryAction={
          canManageBudget
            ? { label: `+ ${t("fields.policyMode")}`, onPress: () => setIsPolicyOpen(true) }
            : undefined
        }
        secondaryActions={
          <div style={{ display: "flex", gap: "0.5rem", flexWrap: "wrap" }}>
            {canRequestOverride ? (
              <Button variant="secondary" onPress={() => setIsOverrideOpen(true)}>
                {t("actions.requestOverride")}
              </Button>
            ) : null}
            {canManageBudget ? (
              <Button
                variant="secondary"
                isDisabled={reconcileControl.isPending}
                onPress={() =>
                  void runMutation(async () => {
                    const res = await reconcileControl.mutateAsync({ bucketId: bucketIdInput || null, rebuild: false });
                    setReconcileResult(res);
                  })
                }
              >
                {t("actions.reconcileControl")}
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

      {/* Availability & Budget Check */}
      <Surface style={{ padding: "1.25rem", marginBottom: "1.25rem" }}>
        <h2 style={{ margin: "0 0 1rem 0", fontSize: "1.05rem" }}>{t("actions.checkAvailability")}</h2>
        <div style={{ display: "flex", gap: "1rem", flexWrap: "wrap", alignItems: "flex-end" }}>
          <div style={{ minWidth: "280px", flex: "1 1 280px" }}>
            <TextField label={t("fields.bucketId")} value={bucketIdInput} onChange={setBucketIdInput} />
          </div>
          <div style={{ width: "180px" }}>
            <MoneyField label={t("fields.amount")} value={{ amount: proposedAmount, currency: currencyCode }} onChange={(v) => setProposedAmount(v.amount)} defaultCurrency={currencyCode} />
          </div>
          <Button
            variant="primary"
            isDisabled={!bucketIdInput.trim() || checkBudget.isPending}
            onPress={() =>
              void runMutation(async () => {
                const res = await checkBudget.mutateAsync({
                  bucketId: bucketIdInput.trim(),
                  proposedAmount: Number(proposedAmount) || 0,
                  sourceModule: "purchasing",
                  sourceDocumentId: "CHECK-PREVIEW",
                  sourceLineId: "1",
                  sourceVersion: "1",
                  reserve: false
                });
                setCheckResult(res);
              })
            }
          >
            {t("actions.checkAvailability")}
          </Button>
        </div>

        {availabilityQuery.data ? (
          <div style={{ marginTop: "1rem", display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(160px, 1fr))", gap: "1rem", padding: "1rem", backgroundColor: "#f8fafc", borderRadius: "8px" }}>
            <div><strong>{t("fields.currentBudget")}:</strong> <MoneyDisplay value={toMoney(availabilityQuery.data.currentBudget, currencyCode)} /></div>
            <div><strong>{t("fields.actualAmount")}:</strong> <MoneyDisplay value={toMoney(availabilityQuery.data.actual, currencyCode)} /></div>
            <div><strong>{t("fields.commitmentAmount")}:</strong> <MoneyDisplay value={toMoney(availabilityQuery.data.commitment, currencyCode)} /></div>
            <div><strong>{t("fields.precommitmentAmount")}:</strong> <MoneyDisplay value={toMoney(availabilityQuery.data.precommitment, currencyCode)} /></div>
            <div><strong>{t("fields.availableAmount")}:</strong> <MoneyDisplay value={toMoney(availabilityQuery.data.available, currencyCode)} /></div>
            <div><PlanningStatusBadge status={availabilityQuery.data.mode} /></div>
          </div>
        ) : null}

        {checkResult ? (
          <div style={{ marginTop: "1rem" }}>
            <Alert
              variant={checkResult.decision === "Block" ? "danger" : checkResult.decision === "Warn" || checkResult.decision === "RequireApproval" ? "warning" : "success"}
            >
              {`${t(`statuses.${checkResult.decision}` as never)} — ${t("fields.availableAmount")}: ${checkResult.availableAfter}`}
            </Alert>
          </div>
        ) : null}
      </Surface>

      {/* Override Result & Approval */}
      {overrideResult ? (
        <Surface style={{ padding: "1.25rem", marginBottom: "1.25rem" }}>
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: "1rem" }}>
            <div>
              <strong>{overrideResult.sourceDocumentId}</strong> — <MoneyDisplay value={toMoney(overrideResult.amount, overrideResult.currencyCode)} /> ({overrideResult.reason})
            </div>
            <div style={{ display: "flex", gap: "0.5rem", alignItems: "center" }}>
              <PlanningStatusBadge status={overrideResult.state} />
              {canApproveOverride && overrideResult.state === "Pending" ? (
                <>
                  <Button variant="primary" onPress={() => void runMutation(async () => {
                    const decided = await decideOverride.mutateAsync({ id: overrideResult.id, request: { decision: "Approve", reason: "Approved by Finance" } });
                    setOverrideResult(decided);
                  })}>
                    {t("actions.approveOverride")}
                  </Button>
                  <Button variant="danger" onPress={() => void runMutation(async () => {
                    const decided = await decideOverride.mutateAsync({ id: overrideResult.id, request: { decision: "Reject", reason: "Rejected" } });
                    setOverrideResult(decided);
                  })}>
                    {t("actions.rejectOverride")}
                  </Button>
                </>
              ) : null}
            </div>
          </div>
        </Surface>
      ) : null}

      {/* Reconciliation Result */}
      {reconcileResult ? (
        <Surface style={{ padding: "1.25rem" }}>
          <div style={{ display: "flex", gap: "1.5rem", alignItems: "center", flexWrap: "wrap" }}>
            <PlanningStatusBadge status={reconcileResult.state} />
            <span>{t("labels.bucketsCount")}: <strong>{reconcileResult.bucketCount}</strong></span>
            <span>{t("labels.discrepancy")}: <MoneyDisplay value={toMoney(reconcileResult.discrepancyAmount, currencyCode)} /></span>
          </div>
        </Surface>
      ) : null}

      {/* Policy Modal */}
      <Dialog isOpen={isPolicyOpen} onClose={() => setIsPolicyOpen(false)} title={t("fields.policyMode")} size="small">
        <form
          onSubmit={(e) => {
            e.preventDefault();
            void runMutation(async () => {
              await createPolicy.mutateAsync({
                legalEntityId,
                name: policyName,
                mode: policyMode,
                basis: policyBasis,
                thresholdPercent: Number(policyThreshold) || 100
              });
              setIsPolicyOpen(false);
            });
          }}
          style={{ display: "flex", flexDirection: "column", gap: "1rem" }}
        >
          <TextField label={t("fields.name")} value={policyName} onChange={setPolicyName} isRequired />
          <Select
            label={t("fields.policyMode")}
            selectedKey={policyMode}
            onSelectionChange={(k) => k && setPolicyMode(String(k))}
            options={[
              { id: "NoControl", label: t("statuses.NoControl") },
              { id: "Warn", label: t("statuses.Warn") },
              { id: "Block", label: t("statuses.Block") },
              { id: "RequireApproval", label: t("statuses.RequireApproval") }
            ]}
          />
          <TextField label={t("fields.thresholdPercent")} value={policyThreshold} onChange={setPolicyThreshold} type="number" />
          <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem" }}>
            <Button type="button" variant="secondary" onPress={() => setIsPolicyOpen(false)}>{t("actions.cancel")}</Button>
            <Button type="submit" variant="primary" isDisabled={createPolicy.isPending}>{t("actions.confirm")}</Button>
          </div>
        </form>
      </Dialog>

      {/* Override Modal */}
      <Dialog isOpen={isOverrideOpen} onClose={() => setIsOverrideOpen(false)} title={t("actions.requestOverride")} size="small">
        <form
          onSubmit={(e) => {
            e.preventDefault();
            void runMutation(async () => {
              const res = await requestOverride.mutateAsync({
                bucketId: bucketIdInput.trim(),
                sourceModule: "purchasing",
                sourceDocumentId: overrideDocId,
                sourceLineId: "1",
                sourceVersion: "1",
                amount: Number(proposedAmount) || 0,
                currencyCode,
                reason: overrideReason
              });
              setOverrideResult(res);
              setIsOverrideOpen(false);
            });
          }}
          style={{ display: "flex", flexDirection: "column", gap: "1rem" }}
        >
          <TextField label={t("fields.bucketId")} value={bucketIdInput} onChange={setBucketIdInput} isRequired />
          <TextField label={t("fields.sourceDocument")} value={overrideDocId} onChange={setOverrideDocId} isRequired />
          <MoneyField label={t("fields.amount")} value={{ amount: proposedAmount, currency: currencyCode }} onChange={(v) => setProposedAmount(v.amount)} defaultCurrency={currencyCode} />
          <TextField label={t("fields.reason")} value={overrideReason} onChange={setOverrideReason} isRequired />
          <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem" }}>
            <Button type="button" variant="secondary" onPress={() => setIsOverrideOpen(false)}>{t("actions.cancel")}</Button>
            <Button type="submit" variant="primary" isDisabled={requestOverride.isPending}>{t("actions.requestOverride")}</Button>
          </div>
        </form>
      </Dialog>
    </div>
  );
}

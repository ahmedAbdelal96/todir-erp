import React, { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../components/pages/surface.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { LocalDateField } from "../../../components/fields/local-date-field.tsx";
import { MoneyField } from "../../../components/fields/money-field.tsx";
import { MoneyDisplay } from "../../../components/fields/money-display.tsx";
import { LocalDateDisplay } from "../../../components/fields/local-date-display.tsx";
import { Alert } from "../../../components/primitives/alert.tsx";
import { EmptyState } from "../../../components/primitives/empty-state.tsx";
import { LoadingState } from "../../../components/primitives/loading-state.tsx";
import { BidiText } from "../../../components/primitives/bidi-text.tsx";
import { PlanningHeader } from "../components/planning-header.tsx";
import { PlanningStatusBadge } from "../components/planning-status-badge.tsx";
import { PlanningNoScopePrompt } from "../components/planning-no-scope-prompt.tsx";
import { PlanningContextBar } from "../components/planning-context-bar.tsx";
import { usePlanningScope } from "../hooks/use-planning-scope.ts";
import { usePlanningAccess } from "../hooks/use-planning-access.ts";
import {
  usePlanVersions,
  useSubmissionUnits,
  useCreateSubmissionUnit,
  useTransitionSubmission,
  useDepartmentPlanner,
  useCreatePlanningTarget,
  useCreatePlanningProposal
} from "../hooks/use-planning-queries.ts";
import { mapPlanningError } from "../services/planning-error-map.ts";
import { toMoney } from "../utils.ts";

export function PlanningSubmissionsPage(): React.JSX.Element {
  const { t } = useTranslation("planning");
  const { legalEntityId, currencyCode, isReady } = usePlanningScope();
  const { canEditVersions, canSubmitVersions, canApproveVersions, canManageBudget } = usePlanningAccess();

  const { data: versions = [] } = usePlanVersions(isReady);
  const [selectedVersionId, setSelectedVersionId] = useState("");
  const [feedback, setFeedback] = useState<{ tone: "success" | "danger"; text: string } | null>(null);

  const [isSubModalOpen, setIsSubModalOpen] = useState(false);
  const [subName, setSubName] = useState("Operations Department Budget");

  const [isTargetModalOpen, setIsTargetModalOpen] = useState(false);
  const [targetMetric, setTargetMetric] = useState("OPEX");
  const [targetPeriod, setTargetPeriod] = useState("2027-01-01");
  const [targetAmount, setTargetAmount] = useState("80000");

  const [isProposalModalOpen, setIsProposalModalOpen] = useState(false);
  const [propMetric, setPropMetric] = useState("OPEX");
  const [propPeriod, setPropPeriod] = useState("2027-01-01");
  const [propAmount, setPropAmount] = useState("75000");
  const [propReason, setPropReason] = useState("");

  const [returnSubId, setReturnSubId] = useState<string | null>(null);
  const [returnReason, setReturnReason] = useState("");

  useEffect(() => {
    if (versions.length > 0 && !versions.some((v) => v.id === selectedVersionId)) {
      const draft = versions.find((v) => v.state === "Draft") ?? versions[0];
      if (draft) setSelectedVersionId(draft.id);
    }
  }, [versions, selectedVersionId]);

  const submissionsQuery = useSubmissionUnits(selectedVersionId || null);
  const deptPlannerQuery = useDepartmentPlanner(
    { planVersionId: selectedVersionId, legalEntityId },
    isReady && Boolean(selectedVersionId)
  );

  const createSubmission = useCreateSubmissionUnit();
  const transitionSubmission = useTransitionSubmission();
  const createTarget = useCreatePlanningTarget();
  const createProposal = useCreatePlanningProposal();

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
    <div data-testid="planning-submissions-page" style={{ padding: "0 0 2rem 0" }}>
      <PlanningHeader
        title={t("navigation.submissions")}
        subtitle={t("subtitle")}
        primaryAction={
          canEditVersions && selectedVersionId
            ? { label: `+ ${t("actions.submit")}`, onPress: () => setIsSubModalOpen(true) }
            : undefined
        }
        secondaryActions={
          <div style={{ display: "flex", gap: "0.5rem", flexWrap: "wrap" }}>
            {canManageBudget && selectedVersionId ? (
              <Button variant="secondary" onPress={() => setIsTargetModalOpen(true)}>
                {t("actions.createTarget")}
              </Button>
            ) : null}
            {canEditVersions && selectedVersionId ? (
              <Button variant="secondary" onPress={() => setIsProposalModalOpen(true)}>
                {t("actions.submitProposal")}
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

      <PlanningContextBar
        versions={versions}
        selectedVersionId={selectedVersionId}
        onVersionChange={setSelectedVersionId}
      />

      {/* Submission Units Table */}
      <Surface style={{ padding: "1.25rem", marginBottom: "1.25rem" }}>
        <h2 style={{ margin: "0 0 1rem 0", fontSize: "1.05rem" }}>{t("navigation.submissions")}</h2>
        {submissionsQuery.isLoading ? (
          <LoadingState message={t("messages.loading")} />
        ) : (submissionsQuery.data ?? []).length === 0 ? (
          <EmptyState title={t("messages.noData")} />
        ) : (
          <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "0.875rem" }}>
            <thead>
              <tr style={{ borderBottom: "2px solid #e2e8f0", textAlign: "start", color: "#475569" }}>
                <th style={{ padding: "8px 12px" }}>{t("fields.name")}</th>
                <th style={{ padding: "8px 12px" }}>{t("fields.horizon")}</th>
                <th style={{ padding: "8px 12px" }}></th>
              </tr>
            </thead>
            <tbody>
              {(submissionsQuery.data ?? []).map((u) => (
                <tr key={u.id} style={{ borderBottom: "1px solid #f1f5f9" }}>
                  <td style={{ padding: "8px 12px", fontWeight: 700 }}>{u.name}</td>
                  <td style={{ padding: "8px 12px" }}><PlanningStatusBadge status={u.state} /></td>
                  <td style={{ padding: "8px 12px", display: "flex", gap: "0.35rem", flexWrap: "wrap" }}>
                    {canSubmitVersions && (u.state === "Draft" || u.state === "Returned") ? (
                      <Button variant="secondary" onPress={() => void runMutation(() => transitionSubmission.mutateAsync({ id: u.id, action: "submit" }))}>
                        {t("actions.submit")}
                      </Button>
                    ) : null}
                    {canApproveVersions && u.state === "Submitted" ? (
                      <>
                        <Button variant="primary" onPress={() => void runMutation(() => transitionSubmission.mutateAsync({ id: u.id, action: "approve" }))}>
                          {t("actions.approve")}
                        </Button>
                        <Button variant="secondary" onPress={() => setReturnSubId(u.id)}>
                          {t("actions.returnSubmission")}
                        </Button>
                      </>
                    ) : null}
                    {canApproveVersions && u.state === "Approved" ? (
                      <Button variant="secondary" onPress={() => void runMutation(() => transitionSubmission.mutateAsync({ id: u.id, action: "lock" }))}>
                        {t("labels.lockUnit")}
                      </Button>
                    ) : null}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </Surface>

      {/* Department Planner Read Model */}
      <Surface style={{ padding: "1.25rem" }}>
        <h2 style={{ margin: "0 0 1rem 0", fontSize: "1.05rem" }}>{t("fields.department")}</h2>
        {deptPlannerQuery.isLoading ? (
          <LoadingState message={t("messages.loading")} />
        ) : (deptPlannerQuery.data ?? []).length === 0 ? (
          <EmptyState title={t("messages.noData")} />
        ) : (
          <div style={{ overflowX: "auto" }}>
            <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "0.875rem" }}>
              <thead>
                <tr style={{ borderBottom: "2px solid #e2e8f0", textAlign: "start", color: "#475569" }}>
                  <th style={{ padding: "8px 12px" }}>{t("fields.period")}</th>
                  <th style={{ padding: "8px 12px" }}>{t("fields.metric")}</th>
                  <th style={{ padding: "8px 12px" }}>{t("fields.currentBudget")}</th>
                  <th style={{ padding: "8px 12px" }}>{t("fields.actualAmount")}</th>
                  <th style={{ padding: "8px 12px" }}>{t("fields.commitmentAmount")}</th>
                  <th style={{ padding: "8px 12px" }}>{t("fields.availableAmount")}</th>
                  <th style={{ padding: "8px 12px" }}>{t("fields.horizon")}</th>
                </tr>
              </thead>
              <tbody>
                {(deptPlannerQuery.data ?? []).map((line, i) => (
                  <tr key={i} style={{ borderBottom: "1px solid #f1f5f9" }}>
                    <td style={{ padding: "8px 12px" }}><LocalDateDisplay value={line.periodStart} /></td>
                    <td style={{ padding: "8px 12px", fontWeight: 700 }}><BidiText>{line.metricCode}</BidiText></td>
                    <td style={{ padding: "8px 12px" }}><MoneyDisplay value={toMoney(line.budgetAmount, currencyCode)} /></td>
                    <td style={{ padding: "8px 12px" }}><MoneyDisplay value={toMoney(line.actualAmount, currencyCode)} /></td>
                    <td style={{ padding: "8px 12px" }}><MoneyDisplay value={toMoney(line.commitmentAmount, currencyCode)} /></td>
                    <td style={{ padding: "8px 12px" }}><MoneyDisplay value={toMoney(line.availableAmount, currencyCode)} /></td>
                    <td style={{ padding: "8px 12px" }}><PlanningStatusBadge status={line.submissionState} /></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Surface>

      {/* Create Submission Unit Modal */}
      <Dialog isOpen={isSubModalOpen} onClose={() => setIsSubModalOpen(false)} title={t("navigation.submissions")} size="small">
        <form
          onSubmit={(e) => {
            e.preventDefault();
            if (!selectedVersionId) return;
            void runMutation(async () => {
              await createSubmission.mutateAsync({ planVersionId: selectedVersionId, legalEntityId, name: subName });
              setIsSubModalOpen(false);
            });
          }}
          style={{ display: "flex", flexDirection: "column", gap: "1rem" }}
        >
          <TextField label={t("fields.name")} value={subName} onChange={setSubName} isRequired />
          <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem" }}>
            <Button type="button" variant="secondary" onPress={() => setIsSubModalOpen(false)}>{t("actions.cancel")}</Button>
            <Button type="submit" variant="primary" isDisabled={createSubmission.isPending}>{t("actions.confirm")}</Button>
          </div>
        </form>
      </Dialog>

      {/* Return Submission Modal */}
      <Dialog isOpen={Boolean(returnSubId)} onClose={() => setReturnSubId(null)} title={t("actions.returnSubmission")} size="small">
        <form
          onSubmit={(e) => {
            e.preventDefault();
            if (!returnSubId) return;
            void runMutation(async () => {
              await transitionSubmission.mutateAsync({ id: returnSubId, action: "return", reason: returnReason });
              setReturnSubId(null);
              setReturnReason("");
            });
          }}
          style={{ display: "flex", flexDirection: "column", gap: "1rem" }}
        >
          <TextField label={t("fields.reason")} value={returnReason} onChange={setReturnReason} isRequired />
          <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem" }}>
            <Button type="button" variant="secondary" onPress={() => setReturnSubId(null)}>{t("actions.cancel")}</Button>
            <Button type="submit" variant="primary" isDisabled={transitionSubmission.isPending}>{t("actions.returnSubmission")}</Button>
          </div>
        </form>
      </Dialog>

      {/* Create Target Modal */}
      <Dialog isOpen={isTargetModalOpen} onClose={() => setIsTargetModalOpen(false)} title={t("actions.createTarget")} size="small">
        <form
          onSubmit={(e) => {
            e.preventDefault();
            if (!selectedVersionId) return;
            void runMutation(async () => {
              await createTarget.mutateAsync({
                planVersionId: selectedVersionId,
                legalEntityId,
                periodStart: targetPeriod,
                metricCode: targetMetric,
                targetAmount: Number(targetAmount) || 0,
                currencyCode
              });
              setIsTargetModalOpen(false);
            });
          }}
          style={{ display: "flex", flexDirection: "column", gap: "1rem" }}
        >
          <TextField label={t("fields.metric")} value={targetMetric} onChange={setTargetMetric} isRequired />
          <LocalDateField label={t("fields.period")} value={targetPeriod} onChange={setTargetPeriod} />
          <MoneyField label={t("fields.targetAmount")} value={{ amount: targetAmount, currency: currencyCode }} onChange={(v) => setTargetAmount(v.amount)} defaultCurrency={currencyCode} />
          <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem" }}>
            <Button type="button" variant="secondary" onPress={() => setIsTargetModalOpen(false)}>{t("actions.cancel")}</Button>
            <Button type="submit" variant="primary" isDisabled={createTarget.isPending}>{t("actions.confirm")}</Button>
          </div>
        </form>
      </Dialog>

      {/* Submit Proposal Modal */}
      <Dialog isOpen={isProposalModalOpen} onClose={() => setIsProposalModalOpen(false)} title={t("actions.submitProposal")} size="small">
        <form
          onSubmit={(e) => {
            e.preventDefault();
            if (!selectedVersionId) return;
            void runMutation(async () => {
              await createProposal.mutateAsync({
                planVersionId: selectedVersionId,
                legalEntityId,
                periodStart: propPeriod,
                metricCode: propMetric,
                proposedAmount: Number(propAmount) || 0,
                currencyCode,
                reason: propReason || null
              });
              setIsProposalModalOpen(false);
            });
          }}
          style={{ display: "flex", flexDirection: "column", gap: "1rem" }}
        >
          <TextField label={t("fields.metric")} value={propMetric} onChange={setPropMetric} isRequired />
          <LocalDateField label={t("fields.period")} value={propPeriod} onChange={setPropPeriod} />
          <MoneyField label={t("fields.proposalAmount")} value={{ amount: propAmount, currency: currencyCode }} onChange={(v) => setPropAmount(v.amount)} defaultCurrency={currencyCode} />
          <TextField label={t("fields.reason")} value={propReason} onChange={setPropReason} />
          <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem" }}>
            <Button type="button" variant="secondary" onPress={() => setIsProposalModalOpen(false)}>{t("actions.cancel")}</Button>
            <Button type="submit" variant="primary" isDisabled={createProposal.isPending}>{t("actions.confirm")}</Button>
          </div>
        </form>
      </Dialog>
    </div>
  );
}

import React from "react";
import { useTranslation } from "react-i18next";
import { StatusBadge, type StatusIntent } from "../../../components/fields/status-badge.tsx";
import type {
  ExpenseState,
  ApprovalState,
  AccountingState,
  SettlementState,
  EvidenceState,
  PolicyState,
  PaymentSource
} from "../types.ts";

export interface ExpenseStateBadgeProps {
  readonly state: ExpenseState | string;
  readonly size?: "small" | "medium";
}

export function ExpenseStateBadge({ state, size = "small" }: ExpenseStateBadgeProps): React.JSX.Element {
  const { t } = useTranslation("expenses");

  let intent: StatusIntent = "neutral";
  if (state === "Draft") intent = "neutral";
  else if (state === "Submitted") intent = "info";
  else if (state === "Approved") intent = "warning";
  else if (state === "Finalized") intent = "success";
  else if (state === "Reversed") intent = "danger";

  const label = t(`states.expense.${state as ExpenseState}`, { defaultValue: state });

  return <StatusBadge intent={intent} label={label} size={size} />;
}

export function ApprovalStateBadge({
  state,
  size = "small"
}: {
  readonly state: ApprovalState | string;
  readonly size?: "small" | "medium";
}): React.JSX.Element {
  const { t } = useTranslation("expenses");

  let intent: StatusIntent = "neutral";
  if (state === "Pending") intent = "warning";
  else if (state === "Approved") intent = "success";
  else if (state === "Rejected") intent = "danger";

  const label = t(`states.approval.${state as ApprovalState}`, { defaultValue: state });

  return <StatusBadge intent={intent} label={label} size={size} />;
}

export function AccountingStateBadge({
  state,
  size = "small"
}: {
  readonly state: AccountingState | string;
  readonly size?: "small" | "medium";
}): React.JSX.Element {
  const { t } = useTranslation("expenses");

  let intent: StatusIntent = "neutral";
  if (state === "NotFinalized") intent = "neutral";
  else if (state === "Pending") intent = "warning";
  else if (state === "Posted") intent = "success";
  else if (state === "Reversed") intent = "danger";

  const label = t(`states.accounting.${state as AccountingState}`, { defaultValue: state });

  return <StatusBadge intent={intent} label={label} size={size} />;
}

export function SettlementStateBadge({
  state,
  size = "small"
}: {
  readonly state: SettlementState | string;
  readonly size?: "small" | "medium";
}): React.JSX.Element {
  const { t } = useTranslation("expenses");

  let intent: StatusIntent = "neutral";
  if (state === "NotSettled") intent = "neutral";
  else if (state === "ReimbursementPending" || state === "RepaymentPending") intent = "warning";
  else if (state === "ReimbursementPaid" || state === "RepaymentSettled" || state === "SettledDirect" || state === "AdvanceApplied")
    intent = "success";

  const label = t(`states.settlement.${state as SettlementState}`, { defaultValue: state });

  return <StatusBadge intent={intent} label={label} size={size} />;
}

export function EvidenceStateBadge({
  state,
  size = "small"
}: {
  readonly state: EvidenceState | string;
  readonly size?: "small" | "medium";
}): React.JSX.Element {
  const { t } = useTranslation("expenses");

  let intent: StatusIntent = "neutral";
  if (state === "Missing") intent = "danger";
  else if (state === "PendingVerification") intent = "warning";
  else if (state === "Verified") intent = "success";
  else if (state === "Rejected") intent = "danger";

  const label = t(`states.evidence.${state as EvidenceState}`, { defaultValue: state });

  return <StatusBadge intent={intent} label={label} size={size} />;
}

export function PolicyStateBadge({
  state,
  size = "small"
}: {
  readonly state: PolicyState | string;
  readonly size?: "small" | "medium";
}): React.JSX.Element {
  const { t } = useTranslation("expenses");

  let intent: StatusIntent = "neutral";
  if (state === "NotEvaluated") intent = "neutral";
  else if (state === "Passed" || state === "ExceptionApproved") intent = "success";
  else if (state === "Warning") intent = "warning";
  else if (state === "Violation") intent = "danger";

  const label = t(`states.policy.${state as PolicyState}`, { defaultValue: state });

  return <StatusBadge intent={intent} label={label} size={size} />;
}

export function PaymentSourceBadge({
  source,
  size = "small"
}: {
  readonly source: PaymentSource | string;
  readonly size?: "small" | "medium";
}): React.JSX.Element {
  const { t } = useTranslation("expenses");

  let intent: StatusIntent = "neutral";
  if (source === "EmployeePaid") intent = "info";
  else if (source === "CompanyCard") intent = "warning";
  else if (source === "PettyCash") intent = "neutral";
  else if (source === "Advance") intent = "success";

  const label = t(`states.source.${source as PaymentSource}`, { defaultValue: source });

  return <StatusBadge intent={intent} label={label} size={size} />;
}

import React from "react";
import { useTranslation } from "react-i18next";
import { StatusBadge } from "../../../components/fields/status-badge.tsx";
import type {
  AccountStatus,
  GeneralJournalDraftStatus,
  JournalType,
  LedgerMode,
  LedgerReadinessState,
  PeriodStatus
} from "../types.ts";

export function LedgerModeBadge({ mode }: { mode?: LedgerMode | undefined }): React.JSX.Element {
  const { t } = useTranslation("accounting");
  if (!mode) return <span>—</span>;

  return (
    <StatusBadge
      status={mode === "Managed" ? "info" : "neutral"}
      label={mode === "Managed" ? t("mode.managed") : t("mode.full")}
    />
  );
}

export function LedgerReadinessBadge({ state }: { state?: LedgerReadinessState | undefined }): React.JSX.Element {
  const { t } = useTranslation("accounting");
  if (!state) return <span>—</span>;

  const statusVariant = state === "Ready" ? "success" : state === "Warning" ? "warning" : "danger";
  const label =
    state === "Ready" ? t("readiness.ready") : state === "Warning" ? t("readiness.warning") : t("readiness.blocked");

  return <StatusBadge status={statusVariant} label={label} />;
}

export function PeriodStatusBadge({ status }: { status?: PeriodStatus | undefined }): React.JSX.Element {
  const { t } = useTranslation("accounting");
  if (!status) return <span>—</span>;

  const statusVariant = status === "Open" ? "success" : status === "SoftClosed" ? "warning" : "neutral";
  const label =
    status === "Open" ? t("period.open") : status === "SoftClosed" ? t("period.softClosed") : t("period.closed");

  return <StatusBadge status={statusVariant} label={label} />;
}

export function AccountStatusBadge({ status }: { status?: AccountStatus | undefined }): React.JSX.Element {
  const { t } = useTranslation("accounting");
  if (!status) return <span>—</span>;

  return (
    <StatusBadge
      status={status === "Active" ? "success" : "neutral"}
      label={status === "Active" ? t("account.active") : t("account.archived")}
    />
  );
}

export function GeneralJournalStatusBadge({
  status
}: {
  status?: GeneralJournalDraftStatus | undefined;
}): React.JSX.Element {
  const { t } = useTranslation("accounting");
  if (!status) return <span>—</span>;

  const variant =
    status === "Posted"
      ? "success"
      : status === "Approved"
        ? "info"
        : status === "Submitted"
          ? "warning"
          : "neutral";

  const label =
    status === "Posted"
      ? t("journal.statusPosted")
      : status === "Approved"
        ? t("journal.statusApproved")
        : status === "Submitted"
          ? t("journal.statusSubmitted")
          : t("journal.statusDraft");

  return <StatusBadge status={variant} label={label} />;
}

export function LedgerEntryStatusBadge({
  status
}: {
  status?: string | undefined;
}): React.JSX.Element {
  const { t } = useTranslation("accounting");
  if (!status) return <span>—</span>;

  const normalized = status.toLowerCase();
  const variant =
    normalized === "posted"
      ? "success"
      : normalized === "approved"
        ? "info"
        : normalized === "submitted"
          ? "warning"
          : normalized === "reversed"
            ? "neutral"
            : "neutral";

  const label =
    normalized === "posted"
      ? t("journal.statusPosted")
      : normalized === "approved"
        ? t("journal.statusApproved")
        : normalized === "submitted"
          ? t("journal.statusSubmitted")
          : normalized === "reversed"
            ? t("journal.statusReversed")
            : t("journal.statusDraft");

  return <StatusBadge status={variant} label={label} />;
}

export function JournalTypeBadge({ type }: { type?: JournalType | undefined }): React.JSX.Element {
  const { t } = useTranslation("accounting");
  if (!type) return <span>—</span>;

  return <StatusBadge status="neutral" label={t(`journalTypes.${type}`)} />;
}

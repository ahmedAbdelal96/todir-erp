import React from "react";
import { useTranslation } from "react-i18next";

type BadgeTone = "default" | "primary" | "success" | "warning" | "danger" | "neutral";

type ServiceLifecycleBadgeProps = Readonly<{
  state?: string | undefined;
  status?: string | undefined;
  size?: "compact" | "normal" | undefined;
}>;

export function ServiceLifecycleBadge({ state, status, size = "normal" }: ServiceLifecycleBadgeProps): React.JSX.Element {
  const { t } = useTranslation("service");

  const effectiveState = (state ?? status ?? "unknown").toString();
  let tone: BadgeTone = "neutral";
  let label = effectiveState;

  switch (effectiveState.toLowerCase()) {
    // Priorities
    case "low":
      tone = "neutral";
      label = t("priorities.low");
      break;
    case "medium":
      tone = "warning";
      label = t("priorities.medium");
      break;
    case "normal":
      tone = "neutral";
      label = t("priorities.normal");
      break;
    case "high":
      tone = "warning";
      label = t("priorities.high");
      break;
    case "urgent":
      tone = "danger";
      label = t("priorities.urgent");
      break;
    case "critical":
      tone = "danger";
      label = t("priorities.critical");
      break;

    // States: Draft / New
    case "draft":
    case "new":
      tone = "neutral";
      label = t("states.draft");
      break;

    // States: Scheduled / Planned / Ready
    case "triaged":
    case "ready":
    case "scheduled":
    case "planned":
    case "assigned":
      tone = "primary";
      label =
        effectiveState.toLowerCase() === "triaged"
          ? t("states.triaged")
          : effectiveState.toLowerCase() === "ready"
          ? t("states.ready")
          : effectiveState.toLowerCase() === "scheduled"
          ? t("states.scheduled")
          : effectiveState.toLowerCase() === "assigned"
          ? t("states.assigned")
          : t("states.planned");
      break;

    // States: In Execution
    case "inprogress":
    case "dispatched":
    case "traveling":
    case "arrived":
    case "running":
      tone = "primary";
      label =
        effectiveState.toLowerCase() === "traveling"
          ? t("states.traveling")
          : effectiveState.toLowerCase() === "arrived"
          ? t("states.arrived")
          : effectiveState.toLowerCase() === "dispatched"
          ? t("states.dispatched")
          : effectiveState.toLowerCase() === "running"
          ? t("states.running")
          : t("states.inProgress");
      break;

    // States: Waiting / Due / Review
    case "waiting":
    case "paused":
    case "postponed":
    case "due":
    case "forecast":
    case "requested":
      tone = "warning";
      label =
        effectiveState.toLowerCase() === "waiting"
          ? t("states.waiting")
          : effectiveState.toLowerCase() === "due"
          ? t("states.due")
          : effectiveState.toLowerCase() === "requested"
          ? t("states.requested")
          : effectiveState.toLowerCase() === "forecast"
          ? t("states.forecast")
          : t("states.paused");
      break;

    // States: Reviewed
    case "reviewed":
      tone = "primary";
      label = t("states.reviewed");
      break;

    // States: Completed / Resolved / Active / Invoiced
    case "technicalcompleted":
    case "completed":
    case "accepted":
    case "resolved":
    case "closed":
    case "active":
    case "converted":
    case "invoiced":
      tone = "success";
      label =
        effectiveState.toLowerCase() === "technicalcompleted"
          ? t("states.technicalCompleted")
          : effectiveState.toLowerCase() === "accepted"
          ? t("states.accepted")
          : effectiveState.toLowerCase() === "resolved"
          ? t("states.resolved")
          : effectiveState.toLowerCase() === "closed"
          ? t("states.closed")
          : effectiveState.toLowerCase() === "active"
          ? t("states.active")
          : effectiveState.toLowerCase() === "invoiced"
          ? t("states.invoiced")
          : t("states.completed");
      break;

    // States: Danger / Breached / Cancelled / WrittenOff
    case "cancelled":
    case "canceled":
    case "noaccess":
    case "retired":
    case "expired":
    case "breached":
    case "failed":
    case "writtenoff":
      tone = "danger";
      label =
        effectiveState.toLowerCase() === "breached"
          ? t("states.breached")
          : effectiveState.toLowerCase() === "noaccess"
          ? t("states.noAccess")
          : effectiveState.toLowerCase() === "writtenoff"
          ? t("states.writtenOff")
          : effectiveState.toLowerCase() === "failed"
          ? t("states.failed")
          : effectiveState.toLowerCase().startsWith("cancel")
          ? t("states.cancelled")
          : t("states.retired");
      break;

    default:
      tone = "neutral";
      label = effectiveState;
  }

  // Guard against missing-translation fallback text
  if (label === "النص غير متاح" || label === "Text unavailable" || !label) {
    label = effectiveState;
  }

  const toneStyles: Record<BadgeTone, { bg: string; text: string; border: string }> = {
    neutral: { bg: "#f1f5f9", text: "#475569", border: "#cbd5e1" },
    primary: { bg: "#eff6ff", text: "#1d4ed8", border: "#bfdbfe" },
    success: { bg: "#f0fdf4", text: "#15803d", border: "#bbf7d0" },
    warning: { bg: "#fffbeb", text: "#b45309", border: "#fde68a" },
    danger: { bg: "#fef2f2", text: "#b91c1c", border: "#fecaca" },
    default: { bg: "#f1f5f9", text: "#334155", border: "#cbd5e1" }
  };

  const current = toneStyles[tone];
  const isCompact = size === "compact";

  return (
    <span
      data-testid={`service-badge-${effectiveState.toLowerCase()}`}
      style={{
        display: "inline-flex",
        alignItems: "center",
        justifyContent: "center",
        padding: isCompact ? "0.15rem 0.5rem" : "0.25rem 0.75rem",
        fontSize: isCompact ? "0.75rem" : "0.8125rem",
        fontWeight: 600,
        borderRadius: "9999px",
        backgroundColor: current.bg,
        color: current.text,
        border: `1px solid ${current.border}`,
        whiteSpace: "nowrap",
        lineHeight: 1.2
      }}
    >
      {label}
    </span>
  );
}

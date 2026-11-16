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
    case "draft":
    case "new":
      tone = "neutral";
      label = t("states.draft", "مسودة");
      break;
    case "triaged":
    case "ready":
    case "scheduled":
    case "planned":
      tone = "primary";
      label =
        effectiveState.toLowerCase() === "triaged"
          ? t("states.triaged", "تم الفرز")
          : effectiveState.toLowerCase() === "ready"
          ? t("states.ready", "جاهز للجدولة")
          : effectiveState.toLowerCase() === "scheduled"
          ? t("states.scheduled", "مجدول")
          : t("states.planned", "مخطط");
      break;
    case "inprogress":
    case "dispatched":
    case "traveling":
    case "arrived":
    case "running":
      tone = "primary";
      label =
        effectiveState.toLowerCase() === "traveling"
          ? t("states.traveling", "في الطريق")
          : effectiveState.toLowerCase() === "arrived"
          ? t("states.arrived", "في الموقع")
          : t("states.inProgress", "قيد التنفيذ");
      break;
    case "waiting":
    case "paused":
    case "postponed":
    case "due":
    case "forecast":
      tone = "warning";
      label =
        effectiveState.toLowerCase() === "waiting"
          ? t("states.waiting", "معلق")
          : effectiveState.toLowerCase() === "due"
          ? t("states.due", "مستحق")
          : t("states.paused", "موقوف مؤقتاً");
      break;
    case "technicalcompleted":
    case "completed":
    case "accepted":
    case "resolved":
    case "closed":
    case "active":
    case "converted":
      tone = "success";
      label =
        effectiveState.toLowerCase() === "technicalcompleted"
          ? t("states.technicalCompleted", "منجز فنياً")
          : effectiveState.toLowerCase() === "accepted"
          ? t("states.accepted", "معتمد من العميل")
          : effectiveState.toLowerCase() === "resolved"
          ? t("states.resolved", "تم الحل")
          : effectiveState.toLowerCase() === "closed"
          ? t("states.closed", "مغلق")
          : effectiveState.toLowerCase() === "active"
          ? t("states.active", "نشط")
          : t("states.completed", "مكتمل");
      break;
    case "cancelled":
    case "noaccess":
    case "retired":
    case "expired":
    case "breached":
    case "failed":
      tone = "danger";
      label =
        effectiveState.toLowerCase() === "breached"
          ? t("states.breached", "تجاوز SLA")
          : effectiveState.toLowerCase() === "noaccess"
          ? t("states.noAccess", "تعذر الوصول")
          : effectiveState.toLowerCase() === "cancelled"
          ? t("states.cancelled", "ملغى")
          : t("states.retired", "خارج الخدمة");
      break;
    default:
      tone = "neutral";
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

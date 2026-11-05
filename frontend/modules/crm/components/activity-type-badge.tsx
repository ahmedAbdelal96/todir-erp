import React from "react";
import { useTranslation } from "react-i18next";
import type { ActivityType } from "../types.ts";

export interface ActivityTypeBadgeProps {
  readonly type: ActivityType;
}

export function ActivityTypeBadge({ type }: ActivityTypeBadgeProps): React.JSX.Element {
  const { t } = useTranslation("crm");

  const getStyle = (): { bg: string; text: string; icon: string } => {
    switch (type) {
      case "Call":
        return { bg: "#eff6ff", text: "#2563eb", icon: "📞" };
      case "Meeting":
        return { bg: "#fdf4ff", text: "#c026d3", icon: "🤝" };
      case "FollowUp":
        return { bg: "#fef3c7", text: "#d97706", icon: "⏰" };
      case "Note":
        return { bg: "#f1f5f9", text: "#475569", icon: "📝" };
      case "Task":
      default:
        return { bg: "#ecfdf5", text: "#059669", icon: "✓" };
    }
  };

  const style = getStyle();

  return (
    <span
      style={{
        display: "inline-flex",
        alignItems: "center",
        gap: "0.25rem",
        padding: "0.125rem 0.5rem",
        borderRadius: "var(--radius-sm, 0.25rem)",
        fontSize: "0.75rem",
        fontWeight: 600,
        backgroundColor: style.bg,
        color: style.text
      }}
    >
      {t(`activities.types.${type.toLowerCase()}` as any, type)}
    </span>
  );
}

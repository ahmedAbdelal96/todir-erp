import React from "react";
import { useTranslation } from "react-i18next";
import { StatusBadge, type StatusIntent } from "../../../components/fields/status-badge.tsx";
import type { ProjectExecutionHealth } from "../types.ts";

export interface ProjectHealthBadgeProps {
  readonly health: ProjectExecutionHealth;
  readonly size?: "small" | "medium";
}

export function ProjectHealthBadge({ health, size = "medium" }: ProjectHealthBadgeProps): React.JSX.Element {
  const { t } = useTranslation("projects");

  let intent: StatusIntent = "neutral";
  switch (health) {
    case "OnTrack":
      intent = "success";
      break;
    case "AtRisk":
      intent = "warning";
      break;
    case "OffTrack":
      intent = "danger";
      break;
    case "NotAssessed":
      intent = "neutral";
      break;
  }

  return (
    <StatusBadge
      intent={intent}
      label={t(`health.${health}` as const)}
      size={size}
    />
  );
}

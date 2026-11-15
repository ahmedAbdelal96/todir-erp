import React from "react";
import { useTranslation } from "react-i18next";
import { StatusBadge, type StatusIntent } from "../../../components/fields/status-badge.tsx";
import type { ProjectLifecycleState } from "../types.ts";

export interface ProjectStatusBadgeProps {
  readonly state: ProjectLifecycleState;
  readonly size?: "small" | "medium";
}

export function ProjectStatusBadge({ state, size = "medium" }: ProjectStatusBadgeProps): React.JSX.Element {
  const { t } = useTranslation("projects");

  let intent: StatusIntent = "neutral";
  switch (state) {
    case "Active":
      intent = "success";
      break;
    case "ExecutionCompleted":
      intent = "info";
      break;
    case "Draft":
      intent = "warning";
      break;
    case "OnHold":
      intent = "warning";
      break;
    case "Closed":
      intent = "neutral";
      break;
    case "Cancelled":
      intent = "danger";
      break;
  }

  return (
    <StatusBadge
      intent={intent}
      label={t(`states.${state}` as const)}
      size={size}
    />
  );
}

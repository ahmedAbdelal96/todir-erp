import React from "react";
import { useTranslation } from "react-i18next";
import { StatusBadge, type StatusIntent } from "../../../components/fields/status-badge.tsx";
import type { ProjectType } from "../types.ts";

export interface ProjectTypeBadgeProps {
  readonly type: ProjectType;
  readonly size?: "small" | "medium";
}

export function ProjectTypeBadge({ type, size = "medium" }: ProjectTypeBadgeProps): React.JSX.Element {
  const { t } = useTranslation("projects");

  let intent: StatusIntent = "neutral";
  switch (type) {
    case "CustomerProject":
      intent = "info";
      break;
    case "InternalProject":
      intent = "neutral";
      break;
    case "CapitalProject":
      intent = "success";
      break;
  }

  return (
    <StatusBadge
      intent={intent}
      label={t(`types.${type}` as const)}
      size={size}
    />
  );
}

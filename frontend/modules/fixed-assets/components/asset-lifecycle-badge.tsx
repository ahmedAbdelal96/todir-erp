import React from "react";
import { useTranslation } from "react-i18next";
import { StatusBadge, type StatusIntent } from "../../../components/fields/status-badge.tsx";
import type { AssetLifecycleState } from "../types.ts";

export interface AssetLifecycleBadgeProps {
  readonly state: AssetLifecycleState;
  readonly size?: "small" | "medium";
}

export function AssetLifecycleBadge({ state, size = "medium" }: AssetLifecycleBadgeProps): React.JSX.Element {
  const { t } = useTranslation("fixedAssets");

  let intent: StatusIntent = "neutral";
  switch (state) {
    case "Active":
    case "Operational":
      intent = "success";
      break;
    case "UnderConstruction":
    case "Capitalized":
      intent = "info";
      break;
    case "Draft":
      intent = "warning";
      break;
    case "FullyDepreciated":
    case "Archived":
      intent = "neutral";
      break;
    case "Disposed":
    case "WrittenOff":
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

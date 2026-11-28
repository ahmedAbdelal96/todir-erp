import React from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../components/pages/surface.tsx";
import { Select } from "../../../components/primitives/select.tsx";
import { LocalDateField } from "../../../components/fields/local-date-field.tsx";
import type { PlanVersionView } from "../types.ts";

export interface PlanningContextBarProps {
  readonly versions: readonly PlanVersionView[];
  readonly selectedVersionId: string;
  readonly onVersionChange: (versionId: string) => void;
  readonly fromPeriod?: string | undefined;
  readonly onFromPeriodChange?: ((value: string) => void) | undefined;
  readonly toPeriod?: string | undefined;
  readonly onToPeriodChange?: ((value: string) => void) | undefined;
  readonly extraControls?: React.ReactNode | undefined;
}

export function PlanningContextBar({
  versions,
  selectedVersionId,
  onVersionChange,
  fromPeriod,
  onFromPeriodChange,
  toPeriod,
  onToPeriodChange,
  extraControls
}: PlanningContextBarProps): React.JSX.Element {
  const { t } = useTranslation("planning");

  const versionOptions = versions.map((v) => ({
    id: v.id,
    label: `${v.name} (${t(`statuses.${v.state}`, v.state)})`
  }));

  return (
    <Surface style={{ padding: "0.875rem 1.25rem", marginBottom: "1rem" }}>
      <div
        style={{
          display: "flex",
          flexWrap: "wrap",
          alignItems: "flex-end",
          gap: "1rem"
        }}
      >
        <div style={{ minWidth: "240px", flex: "1 1 240px", maxWidth: "360px" }}>
        <Select
          label={t("fields.planVersion")}
          selectedKey={selectedVersionId}
          onSelectionChange={(key) => {
            if (key) onVersionChange(String(key));
          }}
          options={versionOptions}
        />
      </div>

      {onFromPeriodChange !== undefined ? (
        <div style={{ minWidth: "160px" }}>
          <LocalDateField
            label={t("fields.startPeriod")}
            value={fromPeriod ?? ""}
            onChange={onFromPeriodChange}
          />
        </div>
      ) : null}

      {onToPeriodChange !== undefined ? (
        <div style={{ minWidth: "160px" }}>
          <LocalDateField
            label={t("fields.endPeriod")}
            value={toPeriod ?? ""}
            onChange={onToPeriodChange}
          />
        </div>
      ) : null}

      {extraControls ? (
        <div style={{ display: "flex", alignItems: "flex-end", gap: "0.5rem", flexWrap: "wrap", marginInlineStart: "auto" }}>
          {extraControls}
        </div>
      ) : null}
      </div>
    </Surface>
  );
}

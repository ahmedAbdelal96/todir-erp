import React from "react";
import { useTranslation } from "react-i18next";
import { PageHeader, type PageAction } from "../../../components/pages/page-header.tsx";
import type { PageHelpContent } from "../../../components/pages/page-help.tsx";
import { Alert } from "../../../components/primitives/alert.tsx";
import { usePlanningAccess } from "../hooks/use-planning-access.ts";

export interface PlanningHeaderProps {
  readonly title: string;
  readonly subtitle?: string | undefined;
  readonly primaryAction?: PageAction | undefined;
  readonly secondaryActions?: React.ReactNode | undefined;
  readonly status?: React.ReactNode | undefined;
  readonly help?: PageHelpContent | undefined;
}

export function PlanningHeader({
  title,
  subtitle,
  primaryAction,
  secondaryActions,
  status,
  help
}: PlanningHeaderProps): React.JSX.Element {
  const { t } = useTranslation("planning");
  const { isReadOnly } = usePlanningAccess();

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "0.75rem", marginBottom: "1.25rem" }}>
      <PageHeader
        title={title}
        subtitle={subtitle}
        primaryAction={primaryAction}
        secondaryActions={secondaryActions}
        status={status}
        help={help}
      />
      {isReadOnly ? (
        <Alert variant="warning">{t("messages.readOnlyMode")}</Alert>
      ) : null}
    </div>
  );
}

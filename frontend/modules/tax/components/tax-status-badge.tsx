import React from "react";
import { useTranslation } from "react-i18next";
import { StatusBadge } from "../../../components/fields/status-badge.tsx";
import { getTaxStatusPresentation, type TaxStatusPresentationType } from "./tax-status-presentation.ts";

export interface TaxStatusBadgeProps {
  readonly status: string;
  readonly type?: TaxStatusPresentationType;
}

export { getTaxStatusPresentation } from "./tax-status-presentation.ts";

export function TaxStatusBadge({ status, type = "health" }: TaxStatusBadgeProps): React.JSX.Element {
  const { t } = useTranslation("tax");
  const { intent, labelKey } = getTaxStatusPresentation(status, type);

  const translatedLabel = t(labelKey as any, { defaultValue: status });

  return <StatusBadge status={status} intent={intent} label={translatedLabel} />;
}

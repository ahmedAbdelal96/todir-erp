import React from "react";
import { useTranslation } from "react-i18next";
import { Alert } from "../../../components/primitives/alert.tsx";
import { Button } from "../../../components/primitives/button.tsx";

export interface TaxDraftImpactBannerProps {
  readonly recalculationRequired: boolean;
  readonly reason?: string | null | undefined;
  readonly onRecalculate?: (() => void) | undefined;
}

export function TaxDraftImpactBanner({
  recalculationRequired,
  reason,
  onRecalculate
}: TaxDraftImpactBannerProps): React.JSX.Element | null {
  const { t } = useTranslation("tax");

  if (!recalculationRequired) {
    return null;
  }

  return (
    <div style={{ marginBlockEnd: "1rem" }}>
      <Alert
        variant="warning"
        title={t("draftImpact.title")}
      >
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", gap: "1rem", flexWrap: "wrap" }}>
          <span>{reason || t("draftImpact.description")}</span>
          {onRecalculate && (
            <Button variant="secondary" size="compact" onClick={onRecalculate}>
              {t("draftImpact.recalculateAction")}
            </Button>
          )}
        </div>
      </Alert>
    </div>
  );
}

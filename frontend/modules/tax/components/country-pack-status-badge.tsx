import React from "react";
import { useTranslation } from "react-i18next";
import { StatusBadge, type StatusIntent } from "../../../components/fields/status-badge.tsx";

export interface CountryPackStatusBadgeProps {
  readonly countryCode: string;
  readonly sourceKind: string;
  readonly reviewStatus: string;
  readonly technicalStatus: string;
}

export function CountryPackStatusBadge({
  countryCode,
  sourceKind,
  reviewStatus,
  technicalStatus
}: CountryPackStatusBadgeProps): React.JSX.Element {
  const { t } = useTranslation("tax");

  const isTestOnly = sourceKind === "TestOnly";
  const isPendingReview = reviewStatus === "Pending" || reviewStatus === "Unreviewed";

  // Statutory countries (EG, SA, AE) are legally PENDING statutory review
  const isStatutoryMarket = countryCode === "EG" || countryCode === "SA" || countryCode === "AE";

  let reviewIntent: StatusIntent = "warning";
  let reviewLabel: string = t("countryPack.reviewPending");

  if (isTestOnly) {
    reviewIntent = "neutral";
    reviewLabel = t("countryPack.testOnlyWarning");
  } else if (isStatutoryMarket || isPendingReview) {
    reviewIntent = "warning";
    reviewLabel = t("countryPack.statutoryPending");
  } else if (reviewStatus === "Approved") {
    reviewIntent = "success";
    reviewLabel = t("countryPack.statutoryApproved");
  }

  const technicalIntent: StatusIntent =
    technicalStatus === "Active" || technicalStatus === "Available"
      ? "success"
      : technicalStatus === "Installed"
      ? "info"
      : technicalStatus === "Incompatible" || technicalStatus === "Missing"
      ? "danger"
      : "neutral";

  return (
    <div style={{ display: "inline-flex", alignItems: "center", gap: "0.5rem", flexWrap: "wrap" }}>
      {/* Technical lifecycle badge */}
      <StatusBadge
        status={technicalStatus}
        intent={technicalIntent}
        label={t(`countryPack.status.${technicalStatus.toLowerCase()}` as any, { defaultValue: technicalStatus })}
      />

      {/* Statutory / legal review distinction */}
      <StatusBadge
        status={reviewStatus}
        intent={reviewIntent}
        label={reviewLabel}
      />
    </div>
  );
}

import React from "react";
import { useTranslation } from "react-i18next";

export interface QualityStatusBadgeProps {
  status: string;
  type?: "status" | "decision" | "severity" | "ownerStatus";
}

export const QualityStatusBadge: React.FC<QualityStatusBadgeProps> = ({ status, type = "status" }) => {
  const { t } = useTranslation();

  const getVariant = (): { bg: string; text: string; border: string } => {
    switch (status) {
      case "Passed":
      case "Released":
      case "Confirmed":
      case "Closed":
      case "Accepted":
      case "Resolved":
      case "Active":
        return { bg: "#dcfce7", text: "#166534", border: "#86efac" };
      case "Failed":
      case "Rejected":
      case "Critical":
      case "Ineffective":
      case "Revoked":
        return { bg: "#fee2e2", text: "#991b1b", border: "#fca5a5" };
      case "Concession":
      case "UseAsIs":
      case "Major":
      case "High":
      case "Quarantine":
      case "QualityHold":
        return { bg: "#fef3c7", text: "#92400e", border: "#fcd34d" };
      case "InProgress":
      case "Executing":
      case "Investigating":
      case "ActionPending":
      case "UnderReview":
      case "ResponsePending":
        return { bg: "#e0f2fe", text: "#075985", border: "#7dd3fc" };
      case "Unknown":
        return { bg: "#ffedd5", text: "#9a3412", border: "#fdba74" };
      case "Draft":
      case "NotRequired":
      default:
        return { bg: "#f1f5f9", text: "#475569", border: "#cbd5e1" };
    }
  };

  const colors = getVariant();
  const label = t(`quality:status.${status}`, status);

  return (
    <span
      style={{
        display: "inline-flex",
        alignItems: "center",
        padding: "2px 8px",
        borderRadius: "9999px",
        fontSize: "12px",
        fontWeight: 600,
        backgroundColor: colors.bg,
        color: colors.text,
        border: `1px solid ${colors.border}`,
        whiteSpace: "nowrap"
      }}
    >
      {label}
    </span>
  );
};

import React from "react";
import { useTranslation } from "react-i18next";
import { ClockIcon, CloseIcon, RefreshIcon } from "../primitives/icons.tsx";

export interface DraftRecoveryBannerProps {
  readonly onRestore: () => void;
  readonly onDiscard: () => void;
  readonly savedTimestamp?: number | undefined;
}

export function DraftRecoveryBanner({
  onRestore,
  onDiscard
}: DraftRecoveryBannerProps): React.JSX.Element {
  const { t } = useTranslation("common");

  return (
    <aside
      role="alert"
      className="draft-recovery-banner"
      style={{
        display: "flex",
        alignItems: "center",
        justifyContent: "space-between",
        gap: "1rem",
        padding: "0.875rem 1.25rem",
        backgroundColor: "#eff6ff",
        border: "1px solid #bfdbfe",
        borderRadius: "var(--radius-md, 0.5rem)",
        marginBottom: "1.25rem",
        boxShadow: "0 1px 3px rgba(37, 99, 235, 0.08)",
        animation: "fade-in 150ms ease"
      }}
    >
      <div style={{ display: "flex", alignItems: "center", gap: "0.75rem" }}>
        <span
          style={{
            display: "inline-flex",
            alignItems: "center",
            justifyContent: "center",
            width: "32px",
            height: "32px",
            borderRadius: "var(--radius-full, 9999px)",
            backgroundColor: "#dbeafe",
            color: "#1d4ed8",
            flexShrink: 0
          }}
        >
          <ClockIcon size={18} />
        </span>
        <div>
          <div style={{ fontWeight: 700, fontSize: "0.875rem", color: "#1e3a8a" }}>
            {t("draftRecovery.bannerTitle", "تم العثور على مسودة محفوظة تلقائياً")}
          </div>
          <div style={{ fontSize: "0.8125rem", color: "#3b82f6" }}>
            {t("draftRecovery.bannerDescription", "توجد بيانات غير محفوظة تم حفظها تلقائياً قبل مغادرتك السابقة.")}
          </div>
        </div>
      </div>

      <div style={{ display: "flex", alignItems: "center", gap: "0.5rem", flexShrink: 0 }}>
        <button
          type="button"
          onClick={onRestore}
          style={{
            display: "inline-flex",
            alignItems: "center",
            gap: "0.375rem",
            padding: "0.45rem 0.875rem",
            backgroundColor: "#1d4ed8",
            color: "#ffffff",
            border: "1px solid #1e40af",
            borderRadius: "var(--radius-sm, 0.375rem)",
            fontSize: "0.8125rem",
            fontWeight: 600,
            cursor: "pointer",
            boxShadow: "0 1px 2px rgba(0, 0, 0, 0.05)"
          }}
        >
          <RefreshIcon size={14} />
          <span>{t("draftRecovery.restoreButton", "استعادة المسودة")}</span>
        </button>

        <button
          type="button"
          onClick={onDiscard}
          style={{
            display: "inline-flex",
            alignItems: "center",
            gap: "0.375rem",
            padding: "0.45rem 0.75rem",
            backgroundColor: "#ffffff",
            color: "#475569",
            border: "1px solid #cbd5e1",
            borderRadius: "var(--radius-sm, 0.375rem)",
            fontSize: "0.8125rem",
            fontWeight: 500,
            cursor: "pointer"
          }}
        >
          <CloseIcon size={14} />
          <span>{t("draftRecovery.discardButton", "تجاهل ومسح")}</span>
        </button>
      </div>
    </aside>
  );
}

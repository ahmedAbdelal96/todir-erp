import React from "react";
import { useTranslation } from "react-i18next";
import { CloseIcon } from "../primitives/icons.tsx";

export interface BatchAction {
  readonly id: string;
  readonly label: string;
  readonly icon?: React.ReactNode | undefined;
  readonly variant?: "primary" | "secondary" | "danger" | undefined;
  readonly onExecute: () => void | Promise<void>;
}

export interface FloatingBatchBarProps {
  readonly selectedCount: number;
  readonly onClearSelection: () => void;
  readonly actions: readonly BatchAction[];
  readonly isProcessing?: boolean | undefined;
}

export function FloatingBatchBar({
  selectedCount,
  onClearSelection,
  actions,
  isProcessing = false
}: FloatingBatchBarProps): React.JSX.Element | null {
  const { t } = useTranslation("common");

  if (selectedCount <= 0) return null;

  return (
    <div
      role="toolbar"
      aria-label={t("batchActions.selectedCount", { count: selectedCount })}
      className="floating-batch-bar"
      style={{
        position: "fixed",
        bottom: "1.75rem",
        left: "50%",
        transform: "translateX(-50%)",
        zIndex: "var(--z-sticky, 1100)" as unknown as number,
        display: "inline-flex",
        alignItems: "center",
        gap: "0.75rem",
        padding: "0.625rem 1.125rem",
        backgroundColor: "#102a43",
        color: "#ffffff",
        borderRadius: "var(--radius-full, 9999px)",
        border: "1px solid rgba(255, 255, 255, 0.22)",
        boxShadow: "0 12px 30px -4px rgba(16, 42, 67, 0.45), 0 4px 12px -2px rgba(0, 0, 0, 0.25)",
        backdropFilter: "blur(8px)",
        maxWidth: "92vw",
        animation: "fade-in 150ms ease"
      }}
    >
      {/* Selected items count badge */}
      <div style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
        <span
          style={{
            display: "inline-flex",
            alignItems: "center",
            justifyContent: "center",
            minWidth: "22px",
            height: "22px",
            padding: "0 0.375rem",
            borderRadius: "var(--radius-full, 9999px)",
            backgroundColor: "#2563eb",
            color: "#ffffff",
            fontSize: "0.75rem",
            fontWeight: 800
          }}
        >
          {selectedCount}
        </span>
        <span style={{ fontSize: "0.8125rem", fontWeight: 700, whiteSpace: "nowrap" }}>
          {t("batchActions.selectedCount", { count: selectedCount })}
        </span>
      </div>

      <span style={{ width: "1px", height: "18px", backgroundColor: "rgba(255, 255, 255, 0.2)" }} />

      {/* Batch Actions */}
      <div style={{ display: "flex", alignItems: "center", gap: "0.375rem", flexWrap: "nowrap" }}>
        {actions.map((act) => {
          const isDanger = act.variant === "danger";
          const isPrimary = act.variant === "primary";

          return (
            <button
              key={act.id}
              type="button"
              disabled={isProcessing}
              onClick={() => void act.onExecute()}
              style={{
                display: "inline-flex",
                alignItems: "center",
                gap: "0.375rem",
                padding: "0.35rem 0.75rem",
                borderRadius: "var(--radius-full, 9999px)",
                border: isPrimary
                  ? "1px solid #3b82f6"
                  : isDanger
                  ? "1px solid #ef4444"
                  : "1px solid rgba(255, 255, 255, 0.25)",
                backgroundColor: isPrimary
                  ? "#2563eb"
                  : isDanger
                  ? "#dc2626"
                  : "rgba(255, 255, 255, 0.1)",
                color: "#ffffff",
                fontSize: "0.8125rem",
                fontWeight: 600,
                cursor: isProcessing ? "not-allowed" : "pointer",
                transition: "all 150ms ease",
                whiteSpace: "nowrap"
              }}
            >
              {act.icon}
              <span>{act.label}</span>
            </button>
          );
        })}
      </div>

      <span style={{ width: "1px", height: "18px", backgroundColor: "rgba(255, 255, 255, 0.2)" }} />

      {/* Clear selection button */}
      <button
        type="button"
        onClick={onClearSelection}
        aria-label={t("batchActions.clearSelection", "إلغاء التحديد")}
        style={{
          display: "inline-flex",
          alignItems: "center",
          gap: "0.25rem",
          padding: "0.35rem 0.625rem",
          borderRadius: "var(--radius-full, 9999px)",
          border: "none",
          backgroundColor: "transparent",
          color: "rgba(255, 255, 255, 0.7)",
          fontSize: "0.75rem",
          fontWeight: 500,
          cursor: "pointer",
          whiteSpace: "nowrap"
        }}
      >
        <CloseIcon size={14} />
        <span>{t("batchActions.clearSelection", "إلغاء التحديد")}</span>
      </button>
    </div>
  );
}

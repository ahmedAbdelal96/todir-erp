import React, { useEffect, useId } from "react";
import { useTranslation } from "react-i18next";
import { CloseIcon, ChevronRightIcon } from "./icons.tsx";

export interface QuickPeekDrawerProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly title: string;
  readonly subtitle?: string | undefined;
  readonly children: React.ReactNode;
  readonly onOpenFullRecord?: (() => void) | undefined;
  readonly fullRecordLabel?: string | undefined;
  readonly width?: string | number | undefined;
}

export function QuickPeekDrawer({
  isOpen,
  onClose,
  title,
  subtitle,
  children,
  onOpenFullRecord,
  fullRecordLabel,
  width = "460px"
}: QuickPeekDrawerProps): React.JSX.Element | null {
  const { t } = useTranslation("common");
  const titleId = useId();

  // Close on Escape key
  useEffect(() => {
    if (!isOpen) return;

    function handleKeyDown(e: KeyboardEvent) {
      if (e.key === "Escape") {
        e.preventDefault();
        onClose();
      }
    }

    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [isOpen, onClose]);

  if (!isOpen) return null;

  return (
    <div
      role="presentation"
      onClick={onClose}
      className="quick-peek-backdrop"
      style={{
        position: "fixed",
        inset: 0,
        backgroundColor: "rgba(16, 42, 67, 0.45)",
        backdropFilter: "blur(2px)",
        zIndex: "var(--z-modal, 1400)" as unknown as number,
        display: "flex",
        justifyContent: "flex-end",
        animation: "fade-in 150ms ease"
      }}
    >
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        onClick={(e) => e.stopPropagation()}
        className="quick-peek-panel"
        style={{
          width: "100%",
          maxWidth: typeof width === "number" ? `${width}px` : width,
          height: "100%",
          backgroundColor: "#ffffff",
          boxShadow: "-8px 0 25px -4px rgba(16, 42, 67, 0.25)",
          borderInlineStart: "1px solid #cbd5e1",
          display: "flex",
          flexDirection: "column",
          animation: "slide-in 200ms ease"
        }}
      >
        {/* Oxford Navy Header */}
        <div
          style={{
            display: "flex",
            alignItems: "center",
            justifyContent: "space-between",
            padding: "1rem 1.25rem",
            backgroundColor: "#102a43",
            color: "#ffffff",
            borderBottom: "1px solid #0c1f31"
          }}
        >
          <div>
            <h3 id={titleId} style={{ margin: 0, fontSize: "1rem", fontWeight: 700, color: "#ffffff" }}>
              {title}
            </h3>
            {subtitle && (
              <p style={{ margin: "0.25rem 0 0", fontSize: "0.8125rem", color: "#93c5fd" }}>
                {subtitle}
              </p>
            )}
          </div>

          <button
            type="button"
            onClick={onClose}
            aria-label={t("quickPeek.closeAria", "إغلاق المعاينة السريعة")}
            style={{
              border: "none",
              background: "rgba(255, 255, 255, 0.1)",
              color: "#ffffff",
              borderRadius: "var(--radius-sm, 0.375rem)",
              cursor: "pointer",
              padding: "0.375rem",
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              transition: "background-color 150ms ease"
            }}
          >
            <CloseIcon size={18} />
          </button>
        </div>

        {/* Drawer Body */}
        <div
          style={{
            flex: 1,
            overflowY: "auto",
            padding: "1.25rem",
            backgroundColor: "#f8fafc"
          }}
        >
          {children}
        </div>

        {/* Footer (Optional full record link) */}
        {onOpenFullRecord && (
          <div
            style={{
              padding: "0.875rem 1.25rem",
              borderTop: "1px solid #e2e8f0",
              backgroundColor: "#ffffff",
              display: "flex",
              justifyContent: "flex-end"
            }}
          >
            <button
              type="button"
              onClick={onOpenFullRecord}
              style={{
                display: "inline-flex",
                alignItems: "center",
                gap: "0.5rem",
                padding: "0.5rem 1rem",
                borderRadius: "var(--radius-md, 0.375rem)",
                backgroundColor: "#102a43",
                color: "#ffffff",
                border: "1px solid #0c1f31",
                fontSize: "0.8125rem",
                fontWeight: 600,
                cursor: "pointer"
              }}
            >
              <span>{fullRecordLabel ?? t("quickPeek.openFullRecord", "فتح السجل بالكامل")}</span>
              <ChevronRightIcon size={16} />
            </button>
          </div>
        )}
      </div>
    </div>
  );
}

import React, { createContext, useContext, useState, useRef, useCallback, type ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { CheckCircleIcon, UndoIcon, CloseIcon } from "./icons.tsx";

export interface UndoToastOptions {
  readonly message: string;
  readonly onUndo: () => void | Promise<void>;
  readonly durationMs?: number | undefined;
}

interface ToastState {
  readonly id: string;
  readonly message: string;
  readonly onUndo: () => void | Promise<void>;
  readonly durationMs: number;
}

interface UndoToastContextValue {
  readonly showUndoToast: (options: UndoToastOptions) => void;
}

const UndoToastContext = createContext<UndoToastContextValue | null>(null);

export function useUndoToast(): UndoToastContextValue {
  const ctx = useContext(UndoToastContext);
  if (!ctx) {
    throw new Error("useUndoToast must be used within an UndoToastProvider");
  }
  return ctx;
}

export function UndoToastProvider({ children }: { readonly children: ReactNode }): React.JSX.Element {
  const { t } = useTranslation("common");
  const [activeToast, setActiveToast] = useState<ToastState | null>(null);
  const [isUndoing, setIsUndoing] = useState(false);
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const clearTimer = () => {
    if (timerRef.current) {
      clearTimeout(timerRef.current);
      timerRef.current = null;
    }
  };

  const showUndoToast = useCallback(({ message, onUndo, durationMs = 5000 }: UndoToastOptions) => {
    clearTimer();
    const id = crypto.randomUUID();
    setActiveToast({ id, message, onUndo, durationMs });

    timerRef.current = setTimeout(() => {
      setActiveToast(null);
    }, durationMs);
  }, []);

  const handleUndo = async () => {
    if (!activeToast || isUndoing) return;
    setIsUndoing(true);
    clearTimer();

    try {
      await activeToast.onUndo();
      setActiveToast(null);
    } finally {
      setIsUndoing(false);
    }
  };

  const handleDismiss = () => {
    clearTimer();
    setActiveToast(null);
  };

  return (
    <UndoToastContext.Provider value={{ showUndoToast }}>
      {children}
      {activeToast && (
        <div
          role="status"
          aria-live="polite"
          className="undo-toast-dock"
          style={{
            position: "fixed",
            bottom: "1.75rem",
            insetInlineEnd: "1.75rem",
            zIndex: "var(--z-popover, 1500)" as unknown as number,
            display: "flex",
            flexDirection: "column",
            minWidth: "320px",
            maxWidth: "420px",
            backgroundColor: "#102a43",
            color: "#ffffff",
            borderRadius: "var(--radius-lg, 0.75rem)",
            boxShadow: "0 10px 25px -5px rgba(16, 42, 67, 0.4), 0 8px 10px -6px rgba(0, 0, 0, 0.2)",
            border: "1px solid rgba(255, 255, 255, 0.2)",
            overflow: "hidden",
            animation: "fade-in 150ms ease"
          }}
        >
          <div
            style={{
              display: "flex",
              alignItems: "center",
              justifyContent: "space-between",
              gap: "0.75rem",
              padding: "0.875rem 1rem"
            }}
          >
            <div style={{ display: "flex", alignItems: "center", gap: "0.625rem" }}>
              <CheckCircleIcon size={18} style={{ color: "#34d399", flexShrink: 0 }} />
              <span style={{ fontSize: "0.875rem", fontWeight: 600 }}>{activeToast.message}</span>
            </div>

            <div style={{ display: "flex", alignItems: "center", gap: "0.375rem" }}>
              <button
                type="button"
                disabled={isUndoing}
                onClick={() => void handleUndo()}
                style={{
                  display: "inline-flex",
                  alignItems: "center",
                  gap: "0.375rem",
                  padding: "0.35rem 0.75rem",
                  borderRadius: "var(--radius-sm, 0.375rem)",
                  backgroundColor: "#2563eb",
                  color: "#ffffff",
                  border: "1px solid #3b82f6",
                  fontSize: "0.8125rem",
                  fontWeight: 700,
                  cursor: isUndoing ? "not-allowed" : "pointer"
                }}
              >
                <UndoIcon size={14} />
                <span>{t("undoToast.undoAction", "تراجع")}</span>
              </button>

              <button
                type="button"
                onClick={handleDismiss}
                aria-label={t("actions.close", "إغلاق")}
                style={{
                  border: "none",
                  background: "transparent",
                  color: "rgba(255, 255, 255, 0.6)",
                  cursor: "pointer",
                  padding: "0.25rem",
                  display: "flex",
                  alignItems: "center"
                }}
              >
                <CloseIcon size={16} />
              </button>
            </div>
          </div>

          {/* Animated countdown progress bar */}
          <div
            style={{
              height: "3px",
              width: "100%",
              backgroundColor: "rgba(255, 255, 255, 0.15)",
              overflow: "hidden"
            }}
          >
            <div
              style={{
                height: "100%",
                backgroundColor: "#3b82f6",
                animation: `shrink-progress ${activeToast.durationMs}ms linear forwards`
              }}
            />
          </div>
        </div>
      )}
    </UndoToastContext.Provider>
  );
}

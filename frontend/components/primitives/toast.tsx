import React, { createContext, useContext, useState, useCallback, type ReactNode } from "react";
import clsx from "clsx";
import { useTranslation } from "react-i18next";

export type ToastType = "success" | "error" | "info" | "warning";

export interface ToastItem {
  readonly id: string;
  readonly type: ToastType;
  readonly title: string;
  readonly description?: string | undefined;
  readonly durationMs?: number | undefined;
}

export interface ToastContextValue {
  readonly toasts: readonly ToastItem[];
  readonly showToast: (toast: Omit<ToastItem, "id">) => string;
  readonly dismissToast: (id: string) => void;
}

const ToastContext = createContext<ToastContextValue | null>(null);

export function useToast(): ToastContextValue {
  const context = useContext(ToastContext);
  if (!context) {
    throw new Error("useToast must be used within a ToastProvider");
  }
  return context;
}

export interface ToastProviderProps {
  readonly children: ReactNode;
}

export function ToastProvider({ children }: ToastProviderProps): React.JSX.Element {
  const [toasts, setToasts] = useState<ToastItem[]>([]);

  const dismissToast = useCallback((id: string) => {
    setToasts((prev) => prev.filter((t) => t.id !== id));
  }, []);

  const showToast = useCallback(
    (toast: Omit<ToastItem, "id">): string => {
      const id = `toast-${Date.now()}-${Math.random().toString(36).slice(2, 7)}`;
      const item: ToastItem = { ...toast, id };
      setToasts((prev) => [...prev, item]);

      const duration = toast.durationMs ?? 4000;
      if (duration > 0) {
        setTimeout(() => {
          dismissToast(id);
        }, duration);
      }

      return id;
    },
    [dismissToast]
  );

  return (
    <ToastContext.Provider value={{ toasts, showToast, dismissToast }}>
      {children}
      <ToastContainer toasts={toasts} onDismiss={dismissToast} />
    </ToastContext.Provider>
  );
}

export function ToastContainer({
  toasts,
  onDismiss
}: {
  readonly toasts: readonly ToastItem[];
  readonly onDismiss: (id: string) => void;
}): React.JSX.Element {
  const { t } = useTranslation("common");
  if (toasts.length === 0) return <></>;

  return (
    <div
      className="ui-toast-container"
      role="region"
      aria-label={t("components.notifications")}
      style={{
        position: "fixed",
        bottom: "1.5rem",
        insetInlineEnd: "1.5rem",
        display: "flex",
        flexDirection: "column",
        gap: "0.75rem",
        zIndex: 9999,
        maxWidth: "380px",
        width: "100%",
        pointerEvents: "none"
      }}
    >
      {toasts.map((toast) => (
        <ToastCard key={toast.id} toast={toast} onDismiss={() => onDismiss(toast.id)} />
      ))}
    </div>
  );
}

function ToastCard({
  toast,
  onDismiss
}: {
  readonly toast: ToastItem;
  readonly onDismiss: () => void;
}): React.JSX.Element {
  const { t } = useTranslation("common");
  const typeIcons: Record<ToastType, string> = {
    success: "✓",
    error: "✕",
    warning: "⚠️",
    info: "ℹ️"
  };

  const typeBorderColors: Record<ToastType, string> = {
    success: "var(--color-status-success)",
    error: "var(--color-status-danger)",
    warning: "var(--color-status-warning)",
    info: "var(--color-brand-primary)"
  };

  return (
    <div
      className={clsx("ui-toast-card", `ui-toast-card--${toast.type}`)}
      role={toast.type === "error" ? "alert" : "status"}
      aria-live={toast.type === "error" ? "assertive" : "polite"}
      style={{
        pointerEvents: "auto",
        display: "flex",
        alignItems: "flex-start",
        gap: "0.75rem",
        padding: "0.875rem 1rem",
        borderRadius: "var(--radius-md)",
        backgroundColor: "var(--color-surface-default)",
        borderInlineStart: `4px solid ${typeBorderColors[toast.type]}`,
        boxShadow: "0 4px 12px rgba(0, 0, 0, 0.15)",
        border: "1px solid var(--color-border-default)",
        color: "var(--color-content-default)",
        fontSize: "0.875rem"
      }}
    >
      <span style={{ fontSize: "1rem", lineHeight: 1 }}>{typeIcons[toast.type]}</span>
      <div style={{ flex: 1 }}>
        <div style={{ fontWeight: 600 }}>{toast.title}</div>
        {toast.description && (
          <div style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)", marginTop: "0.25rem" }}>
            {toast.description}
          </div>
        )}
      </div>
      <button
        type="button"
        onClick={onDismiss}
        aria-label={t("components.closeNotification")}
        style={{
          background: "none",
          border: "none",
          padding: "0.25rem",
          cursor: "pointer",
          color: "var(--color-content-muted)",
          fontSize: "0.75rem"
        }}
      >
        ✕
      </button>
    </div>
  );
}

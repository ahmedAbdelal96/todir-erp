import React, { type ReactNode } from "react";
import { AlertCircleIcon, RefreshIcon } from "./icons.tsx";
import { Button } from "./button.tsx";
import clsx from "clsx";
import { useTranslation } from "react-i18next";

export interface ErrorStateProps {
  readonly title?: string | undefined;
  readonly message?: string | undefined;
  readonly onRetry?: (() => void) | undefined;
  readonly isRetrying?: boolean | undefined;
  readonly action?: ReactNode | undefined;
  readonly className?: string | undefined;
  readonly style?: React.CSSProperties | undefined;
}

export function ErrorState({
  title,
  message,
  onRetry,
  isRetrying = false,
  action,
  className,
  style
}: ErrorStateProps): React.JSX.Element {
  const { t } = useTranslation("common");
  return (
    <div
      role="alert"
      className={clsx("ui-error-state", className)}
      style={{
        display: "flex",
        flexDirection: "column",
        alignItems: "center",
        justifyContent: "center",
        textAlign: "center",
        padding: "2.5rem 1.5rem",
        backgroundColor: "var(--color-surface-card, #ffffff)",
        border: "1px solid var(--color-border-default, #e2e8f0)",
        borderRadius: "var(--radius-lg, 0.75rem)",
        boxShadow: "var(--shadow-sm)",
        margin: "1.5rem auto",
        maxWidth: "520px",
        width: "100%",
        boxSizing: "border-box",
        ...style
      }}
    >
      <div
        style={{
          width: "48px",
          height: "48px",
          borderRadius: "50%",
          backgroundColor: "var(--color-status-danger-subtle, #fef2f2)",
          color: "var(--color-status-danger, #ef4444)",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          marginBottom: "1rem",
          boxShadow: "0 0 0 6px var(--color-status-danger-subtle, #fef2f2)"
        }}
      >
        <AlertCircleIcon size={24} />
      </div>

      <h3
        style={{
          margin: "0 0 0.5rem 0",
          fontSize: "1.0625rem",
          fontWeight: 700,
          color: "var(--color-content-primary, #0f172a)",
          fontFamily: "var(--font-sans)"
        }}
      >
        {title ?? t("components.loadErrorTitle")}
      </h3>

      {(message ?? t("components.loadErrorMessage")) && (
        <p
          style={{
            margin: "0 0 1.25rem 0",
            fontSize: "0.875rem",
            color: "var(--color-content-muted, #64748b)",
            maxWidth: "420px",
            lineHeight: 1.5
          }}
        >
          {message}
        </p>
      )}

      <div style={{ display: "flex", alignItems: "center", gap: "0.75rem", flexWrap: "wrap", justifyContent: "center" }}>
        {onRetry && (
          <Button
            variant="secondary"
            size="compact"
            onPress={onRetry}
            isLoading={isRetrying}
            startIcon={<RefreshIcon size={14} />}
          >
            {t("components.retry")}
          </Button>
        )}
        {action}
      </div>
    </div>
  );
}

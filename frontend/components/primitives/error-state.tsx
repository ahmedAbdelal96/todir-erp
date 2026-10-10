import React, { type ReactNode } from "react";
import { AlertCircleIcon, RefreshIcon } from "./icons.tsx";
import { Button } from "./button.tsx";
import clsx from "clsx";

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
  title = "تعذر تحميل البيانات",
  message = "حدث خطأ غير متوقع أثناء استرجاع البيانات من الخادم. يرجى المحاولة مرة أخرى.",
  onRetry,
  isRetrying = false,
  action,
  className,
  style
}: ErrorStateProps): React.JSX.Element {
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
        padding: "3rem 1.5rem",
        backgroundColor: "var(--color-surface-card, #ffffff)",
        border: "1px solid var(--color-border-default, #e2e8f0)",
        borderRadius: "var(--radius-md, 0.5rem)",
        margin: "0 auto",
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
          color: "var(--color-status-danger, #dc2626)",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          marginBottom: "1rem"
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
        {title}
      </h3>

      {message && (
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
            إعادة المحاولة
          </Button>
        )}
        {action}
      </div>
    </div>
  );
}

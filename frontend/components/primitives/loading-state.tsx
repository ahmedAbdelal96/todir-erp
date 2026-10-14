import React from "react";
import { Spinner } from "./spinner.tsx";
import { Skeleton } from "./skeleton.tsx";
import clsx from "clsx";
import { useTranslation } from "react-i18next";

export interface LoadingStateProps {
  readonly message?: string | undefined;
  readonly variant?: "spinner" | "skeleton" | undefined;
  readonly skeletonRows?: number | undefined;
  readonly className?: string | undefined;
  readonly style?: React.CSSProperties | undefined;
}

export function LoadingState({
  message,
  variant = "spinner",
  skeletonRows = 5,
  className,
  style
}: LoadingStateProps): React.JSX.Element {
  const { t } = useTranslation("common");
  const displayMessage = message ?? t("components.loading");
  if (variant === "skeleton") {
    return (
      <div
        className={clsx("ui-loading-state-skeleton", className)}
        style={{
          display: "flex",
          flexDirection: "column",
          gap: "0.75rem",
          padding: "1.5rem",
          backgroundColor: "var(--color-surface-card, #ffffff)",
          border: "1px solid var(--color-border-default, #e2e8f0)",
          borderRadius: "var(--radius-md, 0.5rem)",
          ...style
        }}
      >
        <Skeleton variant="rectangular" height={36} style={{ borderRadius: "var(--radius-sm)" }} />
        {Array.from({ length: skeletonRows }).map((_, index) => (
          <Skeleton key={index} variant="text" height={24} />
        ))}
      </div>
    );
  }

  return (
    <div
      className={clsx("ui-loading-state", className)}
      style={{
        display: "flex",
        flexDirection: "column",
        alignItems: "center",
        justifyContent: "center",
        textAlign: "center",
        padding: "3.5rem 1.5rem",
        backgroundColor: "var(--color-surface-card, #ffffff)",
        border: "1px solid var(--color-border-default, #e2e8f0)",
        borderRadius: "var(--radius-md, 0.5rem)",
        width: "100%",
        boxSizing: "border-box",
        ...style
      }}
    >
      <Spinner size="large" aria-label={displayMessage} />
      {displayMessage && (
        <p
          style={{
            marginTop: "1rem",
            marginBottom: 0,
            fontSize: "0.875rem",
            fontWeight: 500,
            color: "var(--color-content-muted, #64748b)"
          }}
        >
          {displayMessage}
        </p>
      )}
    </div>
  );
}

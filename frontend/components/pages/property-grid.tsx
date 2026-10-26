import React, { useState, type ReactNode } from "react";
import clsx from "clsx";
import { CheckIcon } from "../primitives/icons.tsx";
import { useTranslation } from "react-i18next";

export interface PropertyItemProps {
  readonly label: string;
  readonly value: ReactNode;
  readonly icon?: ReactNode | undefined;
  readonly badge?: ReactNode | undefined;
  readonly helperText?: string | undefined;
  readonly isCopyable?: boolean | undefined;
  readonly rawCopyValue?: string | undefined;
  readonly isFullWidth?: boolean | undefined;
  readonly className?: string | undefined;
}

export function PropertyItem({
  label,
  value,
  icon,
  badge,
  helperText,
  isCopyable = false,
  rawCopyValue,
  isFullWidth = false,
  className
}: PropertyItemProps): React.JSX.Element {
  const { t } = useTranslation("common");
  const [hasCopied, setHasCopied] = useState(false);

  const handleCopy = () => {
    const textToCopy = rawCopyValue ?? (typeof value === "string" ? value : "");
    if (textToCopy && typeof navigator !== "undefined" && navigator.clipboard) {
      void navigator.clipboard.writeText(textToCopy);
      setHasCopied(true);
      setTimeout(() => setHasCopied(false), 2000);
    }
  };

  return (
    <div
      className={clsx("ui-property-item", className)}
      style={{
        display: "flex",
        flexDirection: "column",
        gap: "0.375rem",
        gridColumn: isFullWidth ? "1 / -1" : undefined,
        padding: "0.875rem 1rem",
        backgroundColor: "var(--color-surface-subtle, #f8fafc)",
        borderRadius: "var(--radius-md, 0.5rem)",
        border: "1px solid var(--color-border-subtle, #e2e8f0)",
        boxSizing: "border-box"
      }}
    >
      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: "0.5rem" }}>
        <div style={{ display: "flex", alignItems: "center", gap: "0.375rem", color: "var(--color-content-muted, #64748b)", fontSize: "0.75rem", fontWeight: 600 }}>
          {icon && <span style={{ display: "inline-flex", color: "var(--color-brand-primary, #4f46e5)" }}>{icon}</span>}
          <span>{label}</span>
        </div>
        {badge && <div>{badge}</div>}
      </div>

      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: "0.5rem" }}>
        <div style={{ fontSize: "0.9375rem", fontWeight: 600, color: "var(--color-content-primary, #0f172a)", wordBreak: "break-word" }}>
          {value || value === 0 ? value : <span style={{ color: "var(--color-content-muted, #94a3b8)", fontWeight: 400 }}>—</span>}
        </div>
        {isCopyable && (
          <button
            type="button"
            onClick={handleCopy}
            title={hasCopied ? t("components.copied") : t("components.copyValue")}
            aria-label={hasCopied ? t("components.copied") : t("components.copyValue")}
            style={{
              background: "transparent",
              border: "none",
              cursor: "pointer",
              padding: "0.25rem",
              borderRadius: "var(--radius-sm)",
              color: hasCopied ? "var(--color-status-success, #16a34a)" : "var(--color-content-muted, #94a3b8)",
              display: "inline-flex",
              alignItems: "center",
              justifyContent: "center"
            }}
          >
            {hasCopied ? <CheckIcon size={14} /> : (
              <svg width={14} height={14} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <rect width="14" height="14" x="8" y="8" rx="2" ry="2" />
                <path d="M4 16c-1.1 0-2-.9-2-2V4c0-1.1.9-2 2-2h10c1.1 0 2 .9 2 2" />
              </svg>
            )}
          </button>
        )}
      </div>

      {helperText && (
        <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted, #64748b)" }}>
          {helperText}
        </span>
      )}
    </div>
  );
}

export interface PropertyGridProps {
  readonly columns?: 1 | 2 | 3 | 4 | undefined;
  readonly children: ReactNode;
  readonly className?: string | undefined;
}

export function PropertyGrid({
  columns = 3,
  children,
  className
}: PropertyGridProps): React.JSX.Element {
  const minWidthMap = {
    1: "100%",
    2: "280px",
    3: "220px",
    4: "180px"
  };

  return (
    <div
      className={clsx("ui-property-grid", className)}
      style={{
        display: "grid",
        gridTemplateColumns: `repeat(auto-fit, minmax(${minWidthMap[columns]}, 1fr))`,
        gap: "0.875rem",
        width: "100%"
      }}
    >
      {children}
    </div>
  );
}

export interface StatCardProps {
  readonly label?: string | undefined;
  readonly title?: string | undefined;
  readonly value: ReactNode;
  readonly description?: string | undefined;
  readonly subtitle?: string | undefined;
  readonly intent?: "primary" | "success" | "warning" | "danger" | "info" | "neutral" | undefined;
  readonly icon?: ReactNode | undefined;
  readonly className?: string | undefined;
}

export function StatCard({
  label,
  title,
  value,
  description,
  subtitle,
  intent = "primary",
  icon,
  className
}: StatCardProps): React.JSX.Element {
  const displayLabel = title ?? label ?? "";
  const displayDescription = subtitle ?? description;

  const intentColorMap = {
    primary: "var(--color-brand-primary, #4f46e5)",
    success: "var(--color-status-success, #059669)",
    warning: "var(--color-status-warning, #d97706)",
    danger: "var(--color-status-danger, #dc2626)",
    info: "var(--color-status-info, #0284c7)",
    neutral: "var(--color-content-secondary, #475569)"
  };

  const intentBgMap = {
    primary: "var(--color-brand-primary-subtle, rgba(79, 70, 229, 0.1))",
    success: "rgba(16, 185, 129, 0.12)",
    warning: "rgba(245, 158, 11, 0.12)",
    danger: "rgba(239, 68, 68, 0.12)",
    info: "rgba(2, 132, 199, 0.12)",
    neutral: "var(--color-surface-subtle, #f1f5f9)"
  };

  const intentBorderMap = {
    primary: "rgba(79, 70, 229, 0.22)",
    success: "rgba(16, 185, 129, 0.25)",
    warning: "rgba(245, 158, 11, 0.25)",
    danger: "rgba(239, 68, 68, 0.25)",
    info: "rgba(2, 132, 199, 0.25)",
    neutral: "var(--color-border-default, #cbd5e1)"
  };

  return (
    <div
      className={clsx("ui-stat-card", className)}
      style={{
        display: "flex",
        alignItems: "center",
        justifyContent: "space-between",
        gap: "1rem",
        padding: "1.125rem 1.375rem",
        backgroundColor: "var(--color-surface-card, #ffffff)",
        border: "1px solid var(--color-border-default, #cbd5e1)",
        borderRadius: "var(--radius-lg, 0.75rem)",
        boxShadow: "var(--shadow-sm, 0 1px 3px 0 rgba(15, 23, 42, 0.08))"
      }}
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "0.3125rem" }}>
        <span style={{ fontSize: "0.8125rem", fontWeight: 600, letterSpacing: "0.02em", color: "var(--color-content-secondary, #334155)" }}>
          {displayLabel}
        </span>
        <div style={{ fontSize: "1.5rem", fontWeight: 800, color: "var(--color-content-primary, #0f172a)", letterSpacing: "-0.03em", lineHeight: 1.2 }}>
          {value}
        </div>
        {displayDescription && (
          <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted, #64748b)" }}>
            {displayDescription}
          </span>
        )}
      </div>

      {icon && (
        <div
          style={{
            display: "inline-flex",
            alignItems: "center",
            justifyContent: "center",
            width: "46px",
            height: "46px",
            borderRadius: "var(--radius-lg, 0.75rem)",
            backgroundColor: intentBgMap[intent],
            color: intentColorMap[intent],
            border: `1px solid ${intentBorderMap[intent]}`,
            boxShadow: "0 1px 2px 0 rgba(0, 0, 0, 0.04)",
            flexShrink: 0
          }}
        >
          {icon}
        </div>
      )}
    </div>
  );
}

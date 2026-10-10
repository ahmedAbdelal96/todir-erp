import React, { type ReactNode } from "react";
import clsx from "clsx";

export interface SurfaceProps {
  readonly title?: string | undefined;
  readonly description?: string | undefined;
  readonly headerActions?: ReactNode | undefined;
  readonly footer?: ReactNode | undefined;
  readonly children: ReactNode;
  readonly padding?: "none" | "compact" | "normal" | "spacious" | undefined;
  readonly variant?: "card" | "subtle" | "flat" | undefined;
  readonly className?: string | undefined;
  readonly style?: React.CSSProperties | undefined;
}

export function Surface({
  title,
  description,
  headerActions,
  footer,
  children,
  padding = "normal",
  variant = "card",
  className,
  style
}: SurfaceProps): React.JSX.Element {
  const paddingMap = {
    none: "0",
    compact: "0.75rem 1rem",
    normal: "1.25rem 1.5rem",
    spacious: "2rem"
  };

  const backgroundMap = {
    card: "var(--color-surface-card, #ffffff)",
    subtle: "var(--color-surface-subtle, #f8fafc)",
    flat: "transparent"
  };

  const borderMap = {
    card: "1px solid var(--color-border-default, #e2e8f0)",
    subtle: "1px solid var(--color-border-subtle, #f1f5f9)",
    flat: "none"
  };

  const hasHeader = Boolean(title || headerActions || description);

  return (
    <div
      className={clsx("ui-surface", `ui-surface--${variant}`, className)}
      style={{
        backgroundColor: backgroundMap[variant],
        border: borderMap[variant],
        borderRadius: "var(--radius-lg, 0.75rem)",
        boxShadow: variant === "card" ? "var(--shadow-xs)" : "none",
        width: "100%",
        boxSizing: "border-box",
        overflow: "hidden",
        ...style
      }}
    >
      {hasHeader && (
        <div
          style={{
            display: "flex",
            alignItems: "center",
            justifyContent: "space-between",
            flexWrap: "wrap",
            gap: "0.75rem",
            padding: "0.875rem 1.25rem",
            borderBottom: "1px solid var(--color-border-subtle, #f1f5f9)",
            backgroundColor: variant === "card" ? "var(--color-surface-subtle, #f8fafc)" : "transparent"
          }}
        >
          <div>
            {title && (
              <h3 style={{ margin: 0, fontSize: "0.9375rem", fontWeight: 700, color: "var(--color-content-primary, #0f172a)" }}>
                {title}
              </h3>
            )}
            {description && (
              <p style={{ margin: "0.25rem 0 0 0", fontSize: "0.8125rem", color: "var(--color-content-muted, #64748b)" }}>
                {description}
              </p>
            )}
          </div>
          {headerActions && <div>{headerActions}</div>}
        </div>
      )}

      <div style={{ padding: paddingMap[padding] }}>
        {children}
      </div>

      {footer && (
        <div
          style={{
            padding: "0.75rem 1.25rem",
            borderTop: "1px solid var(--color-border-subtle, #f1f5f9)",
            backgroundColor: "var(--color-surface-subtle, #f8fafc)"
          }}
        >
          {footer}
        </div>
      )}
    </div>
  );
}

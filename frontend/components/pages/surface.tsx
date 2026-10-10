import React, { type ReactNode } from "react";
import clsx from "clsx";

export interface SurfaceProps {
  readonly children: ReactNode;
  readonly padding?: "none" | "compact" | "normal" | "spacious" | undefined;
  readonly variant?: "card" | "subtle" | "flat" | undefined;
  readonly className?: string | undefined;
  readonly style?: React.CSSProperties | undefined;
}

export function Surface({
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
    subtle: "var(--color-surface-subtle, #f1f5f9)",
    flat: "transparent"
  };

  const borderMap = {
    card: "1px solid var(--color-border-default, #e2e8f0)",
    subtle: "1px solid var(--color-border-subtle, #f1f5f9)",
    flat: "none"
  };

  return (
    <div
      className={clsx("ui-surface", `ui-surface--${variant}`, className)}
      style={{
        backgroundColor: backgroundMap[variant],
        border: borderMap[variant],
        borderRadius: "var(--radius-lg, 0.75rem)",
        boxShadow: variant === "card" ? "var(--shadow-xs)" : "none",
        padding: paddingMap[padding],
        width: "100%",
        boxSizing: "border-box",
        overflow: "hidden",
        ...style
      }}
    >
      {children}
    </div>
  );
}

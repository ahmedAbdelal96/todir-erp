import React, { type CSSProperties } from "react";
import clsx from "clsx";

export interface SkeletonProps {
  readonly width?: string | number | undefined;
  readonly height?: string | number | undefined;
  readonly variant?: "text" | "rectangular" | "circular" | undefined;
  readonly className?: string | undefined;
  readonly style?: CSSProperties | undefined;
}

export function Skeleton({
  width,
  height,
  variant = "text",
  className,
  style
}: SkeletonProps): React.JSX.Element {
  const isCircular = variant === "circular";
  const defaultHeight = variant === "text" ? "1em" : variant === "circular" ? width ?? "40px" : "100px";

  return (
    <div
      className={clsx("ui-skeleton", `ui-skeleton--${variant}`, className)}
      style={{
        width: width ?? "100%",
        height: height ?? defaultHeight,
        borderRadius: isCircular ? "50%" : variant === "text" ? "var(--radius-sm, 4px)" : "var(--radius-md, 6px)",
        backgroundColor: "var(--color-surface-subtle, #f3f4f6)",
        opacity: 0.7,
        animation: "ui-skeleton-pulse 1.5s ease-in-out infinite",
        ...style
      }}
      aria-hidden="true"
    />
  );
}

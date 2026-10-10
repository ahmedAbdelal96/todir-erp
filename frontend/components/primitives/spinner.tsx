import React from "react";
import clsx from "clsx";

export interface SpinnerProps {
  readonly size?: "small" | "medium" | "large";
  readonly className?: string;
  readonly "aria-label"?: string;
}

export function Spinner({
  size = "medium",
  className,
  "aria-label": ariaLabel = "Loading"
}: SpinnerProps): React.JSX.Element {
  const getDimensions = () => {
    switch (size) {
      case "small":
        return { width: "1rem", height: "1rem", borderWidth: "2px" };
      case "large":
        return { width: "2.5rem", height: "2.5rem", borderWidth: "3px" };
      case "medium":
      default:
        return { width: "1.5rem", height: "1.5rem", borderWidth: "2px" };
    }
  };

  const dims = getDimensions();

  return (
    <div
      role="progressbar"
      aria-label={ariaLabel}
      className={clsx("ui-spinner", `ui-spinner--${size}`, className)}
      style={{
        display: "inline-block",
        width: dims.width,
        height: dims.height,
        border: `${dims.borderWidth} solid var(--color-border-default)`,
        borderTopColor: "var(--color-action-primary)",
        borderRadius: "50%",
        animation: "spin 0.75s linear infinite"
      }}
    />
  );
}

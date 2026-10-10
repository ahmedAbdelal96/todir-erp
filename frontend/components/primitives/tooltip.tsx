import React, { type ReactNode } from "react";
import {
  Tooltip as AriaTooltip,
  TooltipTrigger as AriaTooltipTrigger,
  type TooltipProps as AriaTooltipProps
} from "react-aria-components";
import clsx from "clsx";

export { AriaTooltipTrigger as TooltipTrigger };

export interface TooltipProps extends Omit<AriaTooltipProps, "className"> {
  readonly className?: string;
  readonly children?: ReactNode;
}

export function Tooltip({ className, children, ...props }: TooltipProps): React.JSX.Element {
  return (
    <AriaTooltip
      {...props}
      className={clsx("ui-tooltip", className)}
      style={{
        backgroundColor: "var(--color-surface-contrast, #1e293b)",
        color: "var(--color-content-inverse, #ffffff)",
        borderRadius: "var(--radius-sm, 0.25rem)",
        padding: "0.25rem 0.5rem",
        fontSize: "0.75rem",
        zIndex: 2500,
        boxShadow: "0 4px 6px -1px rgba(0, 0, 0, 0.1)",
        pointerEvents: "none"
      }}
    >
      {children}
    </AriaTooltip>
  );
}

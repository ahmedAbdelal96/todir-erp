import React, { type ReactNode } from "react";
import {
  Popover as AriaPopover,
  Dialog as AriaDialog,
  DialogTrigger as AriaDialogTrigger,
  type PopoverProps as AriaPopoverProps
} from "react-aria-components";
import clsx from "clsx";

export { AriaDialogTrigger as DialogTrigger };

export interface PopoverProps extends Omit<AriaPopoverProps, "className"> {
  readonly className?: string;
  readonly children?: ReactNode;
}

export function Popover({ className, children, ...props }: PopoverProps): React.JSX.Element {
  return (
    <AriaPopover
      {...props}
      className={clsx("ui-popover", className)}
      style={{
        backgroundColor: "var(--color-surface-card)",
        border: "1px solid var(--color-border-default)",
        borderRadius: "var(--radius-md)",
        boxShadow: "0 10px 15px -3px rgba(0, 0, 0, 0.1), 0 4px 6px -2px rgba(0, 0, 0, 0.05)",
        padding: "1rem",
        zIndex: 1500,
        outline: "none"
      }}
    >
      <AriaDialog style={{ outline: "none" }}>{children}</AriaDialog>
    </AriaPopover>
  );
}

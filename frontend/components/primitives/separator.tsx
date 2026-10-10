import React from "react";
import {
  Separator as AriaSeparator,
  type SeparatorProps as AriaSeparatorProps
} from "react-aria-components";
import clsx from "clsx";

export interface SeparatorProps extends Omit<AriaSeparatorProps, "className"> {
  readonly className?: string;
}

export function Separator({ orientation = "horizontal", className, ...props }: SeparatorProps): React.JSX.Element {
  return (
    <AriaSeparator
      {...props}
      orientation={orientation}
      className={clsx("ui-separator", className)}
      style={{
        backgroundColor: "var(--color-border-default)",
        ...(orientation === "horizontal"
          ? { height: "1px", width: "100%", margin: "0.5rem 0" }
          : { width: "1px", height: "100%", margin: "0 0.5rem" })
      }}
    />
  );
}

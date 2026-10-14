import React, { type HTMLAttributes, type ReactNode } from "react";

export interface BidiTextProps extends Omit<HTMLAttributes<HTMLElement>, "children" | "dir"> {
  readonly children: ReactNode;
  readonly direction?: "auto" | "ltr" | "rtl";
}

/** Isolates mixed-direction business data from the surrounding interface copy. */
export function BidiText({
  children,
  direction = "auto",
  ...props
}: BidiTextProps): React.JSX.Element {
  return (
    <bdi dir={direction} {...props}>
      {children}
    </bdi>
  );
}

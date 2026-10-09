import React from "react";
import clsx from "clsx";

export interface UomDisplayProps {
  readonly code: string | null | undefined;
  readonly name?: string | undefined;
  readonly className?: string | undefined;
}

export function UomDisplay({
  code,
  name,
  className
}: UomDisplayProps): React.JSX.Element {
  if (!code) return <span>—</span>;

  return (
    <span
      className={clsx("ui-uom-display", className)}
      style={{
        fontWeight: 500,
        fontSize: "0.875rem",
        color: "var(--color-content-secondary)"
      }}
    >
      {code}
      {name && <span style={{ color: "var(--color-content-muted)", marginInlineStart: "0.25rem" }}>({name})</span>}
    </span>
  );
}

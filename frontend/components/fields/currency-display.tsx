import React from "react";
import clsx from "clsx";

export interface CurrencyDisplayProps {
  readonly code: string | null | undefined;
  readonly name?: string | undefined;
  readonly className?: string | undefined;
}

export function CurrencyDisplay({
  code,
  name,
  className
}: CurrencyDisplayProps): React.JSX.Element {
  if (!code) return <span>—</span>;

  return (
    <span
      className={clsx("ui-currency-display", className)}
      style={{
        fontWeight: 600,
        fontFamily: "var(--font-mono, monospace)",
        fontSize: "0.875rem",
        color: "var(--color-content-secondary)"
      }}
    >
      {code}
      {name && <span style={{ fontWeight: 400, marginInlineStart: "0.375rem" }}>({name})</span>}
    </span>
  );
}

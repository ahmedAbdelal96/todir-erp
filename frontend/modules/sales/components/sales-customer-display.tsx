import React from "react";
import {
  type ResolvedCustomer,
  KNOWN_SALES_CUSTOMERS,
  resolveSalesCustomer
} from "./sales-customer-resolver.js";

export type { ResolvedCustomer };
export { KNOWN_SALES_CUSTOMERS, resolveSalesCustomer };

export interface SalesCustomerDisplayProps {
  readonly partyId?: string | null | undefined;
  readonly showCodeOnly?: boolean | undefined;
  readonly inline?: boolean | undefined;
}

export function SalesCustomerDisplay({
  partyId,
  showCodeOnly = false,
  inline = false
}: SalesCustomerDisplayProps): React.JSX.Element {
  const isAr = document.documentElement.lang !== "en" && document.documentElement.dir !== "ltr";
  const resolved = resolveSalesCustomer(partyId, isAr);

  if (showCodeOnly) {
    return (
      <span style={{ fontFamily: "monospace", fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
        {resolved.code}
      </span>
    );
  }

  if (inline) {
    return (
      <span style={{ fontSize: "0.875rem" }}>
        <strong>{resolved.name}</strong>
        {" "}
        <span style={{ fontFamily: "monospace", fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
          ({resolved.code})
        </span>
      </span>
    );
  }

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "0.125rem" }}>
      <span style={{ fontWeight: 600, color: "var(--color-content-primary, #0f172a)", fontSize: "0.875rem" }}>
        {resolved.name}
      </span>
      <span style={{ fontFamily: "monospace", fontSize: "0.75rem", color: "var(--color-content-muted, #64748b)" }}>
        {resolved.code}
      </span>
    </div>
  );
}

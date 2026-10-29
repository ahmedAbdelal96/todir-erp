import React from "react";

export interface PurchasingDataTableProps {
  readonly children: React.ReactNode;
  readonly minWidth?: string | undefined;
}

export function PurchasingDataTable({ children, minWidth = "100%" }: PurchasingDataTableProps): React.JSX.Element {
  return (
    <div
      style={{
        width: "100%",
        overflowX: "auto",
        WebkitOverflowScrolling: "touch",
        borderRadius: "var(--radius-md)",
        border: "1px solid var(--color-border-default)",
        backgroundColor: "var(--color-surface-card)"
      }}
    >
      <div style={{ minWidth, width: "100%" }}>
        {children}
      </div>
    </div>
  );
}

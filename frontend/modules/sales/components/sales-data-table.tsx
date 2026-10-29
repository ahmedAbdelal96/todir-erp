import React from "react";

export interface SalesDataTableProps {
  readonly children: React.ReactNode;
  readonly minWidth?: string;
}

export function SalesDataTable({ children, minWidth = "950px" }: SalesDataTableProps): React.JSX.Element {
  return (
    <div
      style={{
        width: "100%",
        overflowX: "auto",
        WebkitOverflowScrolling: "touch",
        borderRadius: "var(--radius-md)",
        border: "1px solid var(--color-border-default)"
      }}
    >
      <div style={{ minWidth, width: "100%" }}>
        {children}
      </div>
    </div>
  );
}

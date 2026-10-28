import React from "react";

export interface InventoryDataTableProps {
  readonly children: React.ReactNode;
  readonly minWidth?: string;
  readonly ariaLabel?: string;
}

export function InventoryDataTable({
  children,
  minWidth = "100%",
  ariaLabel
}: InventoryDataTableProps): React.JSX.Element {
  return (
    <div
      className="inventory-table-container"
      style={{
        width: "100%",
        maxWidth: "100%",
        overflowX: "auto",
        border: "1px solid var(--color-border-default)",
        borderRadius: "var(--radius-md, 0.5rem)",
        backgroundColor: "var(--color-surface-card)",
        boxShadow: "0 1px 3px rgba(0, 0, 0, 0.05), 0 1px 2px rgba(0, 0, 0, 0.03)"
      }}
    >
      <table
        aria-label={ariaLabel}
        style={{
          width: "100%",
          minWidth,
          borderCollapse: "collapse",
          fontSize: "0.8125rem",
          textAlign: "start"
        }}
      >
        {children}
      </table>
    </div>
  );
}

import React, { type ReactNode } from "react";
import clsx from "clsx";

export interface PageToolbarProps {
  readonly searchInput?: ReactNode | undefined;
  readonly filters?: ReactNode | undefined;
  readonly actions?: ReactNode | undefined;
  readonly children?: ReactNode | undefined;
  readonly className?: string | undefined;
  readonly style?: React.CSSProperties | undefined;
}

export function PageToolbar({
  searchInput,
  filters,
  actions,
  children,
  className,
  style
}: PageToolbarProps): React.JSX.Element {
  return (
    <div
      className={clsx("ui-page-toolbar", className)}
      style={{
        display: "flex",
        alignItems: "center",
        justifyContent: "space-between",
        flexWrap: "wrap",
        gap: "0.75rem",
        padding: "0.75rem 1rem",
        backgroundColor: "var(--color-surface-card, #ffffff)",
        border: "1px solid var(--color-border-default, #e2e8f0)",
        borderRadius: "var(--radius-md, 0.5rem)",
        width: "100%",
        boxSizing: "border-box",
        ...style
      }}
    >
      <div
        style={{
          display: "flex",
          alignItems: "center",
          gap: "0.75rem",
          flexWrap: "wrap",
          flex: 1,
          minWidth: "260px"
        }}
      >
        {searchInput}
        {filters}
        {children}
      </div>

      {actions && (
        <div
          style={{
            display: "flex",
            alignItems: "center",
            gap: "0.5rem",
            flexWrap: "wrap"
          }}
        >
          {actions}
        </div>
      )}
    </div>
  );
}

import React from "react";

export type PayrollTabId =
  | "home"
  | "runs"
  | "setup"
  | "settings"
  | "inputs"
  | "loans"
  | "payslips"
  | "reports"
  | "openingBalances";

export interface PayrollTabDef {
  readonly id: PayrollTabId;
  readonly label: string;
  readonly icon?: React.JSX.Element;
  readonly badge?: number | string | undefined;
}

export interface PayrollTabNavProps {
  readonly activeTab: PayrollTabId;
  readonly onTabChange: (id: PayrollTabId) => void;
  readonly tabs: readonly PayrollTabDef[];
}

export function PayrollTabNav({ activeTab, onTabChange, tabs }: PayrollTabNavProps): React.JSX.Element {
  return (
    <nav
      style={{
        display: "flex",
        gap: "0.5rem",
        borderBottom: "1px solid var(--color-border-default)",
        marginBottom: "1.25rem",
        overflowX: "auto",
        paddingBottom: "0.25rem"
      }}
    >
      {tabs.map((tab) => {
        const isActive = tab.id === activeTab;
        return (
          <button
            key={tab.id}
            type="button"
            data-testid={`payroll-tab-${tab.id}`}
            onClick={() => onTabChange(tab.id)}
            style={{
              display: "inline-flex",
              alignItems: "center",
              gap: "0.5rem",
              padding: "0.625rem 1rem",
              background: isActive ? "var(--color-surface-card)" : "transparent",
              color: isActive ? "var(--color-brand-primary, #102a43)" : "var(--color-content-muted)",
              fontWeight: isActive ? 700 : 500,
              fontSize: "0.875rem",
              borderTop: `1px solid ${isActive ? "var(--color-border-default)" : "transparent"}`,
              borderInlineStart: `1px solid ${isActive ? "var(--color-border-default)" : "transparent"}`,
              borderInlineEnd: `1px solid ${isActive ? "var(--color-border-default)" : "transparent"}`,
              borderBottom: "1px solid transparent",
              borderRadius: "var(--radius-md) var(--radius-md) 0 0",
              cursor: "pointer",
              whiteSpace: "nowrap",
              position: "relative",
              bottom: isActive ? "-1px" : "0"
            }}
          >
            {tab.icon}
            <span>{tab.label}</span>
            {tab.badge !== undefined && (
              <span
                style={{
                  display: "inline-flex",
                  alignItems: "center",
                  justifyContent: "center",
                  padding: "0.125rem 0.375rem",
                  fontSize: "0.75rem",
                  fontWeight: 600,
                  borderRadius: "var(--radius-full, 9999px)",
                  background: isActive ? "var(--color-brand-primary, #102a43)" : "var(--color-surface-subtle)",
                  color: isActive ? "#ffffff" : "var(--color-content-muted)"
                }}
              >
                {tab.badge}
              </span>
            )}
          </button>
        );
      })}
    </nav>
  );
}

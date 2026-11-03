import React from "react";

export type HrTabId =
  | "home"
  | "directory"
  | "attendance"
  | "leave"
  | "organization"
  | "selfservice"
  | "manager"
  | "reports"
  | "imports";

export interface HrTabDef {
  readonly id: HrTabId;
  readonly label: string;
  readonly icon?: React.JSX.Element;
  readonly badge?: number | string | undefined;
}

export interface HrTabNavProps {
  readonly activeTab: HrTabId;
  readonly onTabChange: (id: HrTabId) => void;
  readonly tabs: readonly HrTabDef[];
}

export function HrTabNav({ activeTab, onTabChange, tabs }: HrTabNavProps): React.JSX.Element {
  return (
    <nav
      style={{
        display: "flex",
        gap: "0.5rem",
        borderBottom: "1px solid var(--color-border-default)",
        marginBottom: "1.5rem",
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
            data-testid={`hr-tab-${tab.id}`}
            onClick={() => onTabChange(tab.id)}
            style={{
              display: "inline-flex",
              alignItems: "center",
              gap: "0.5rem",
              padding: "0.625rem 1rem",
              background: isActive ? "var(--color-surface-card)" : "transparent",
              color: isActive ? "var(--color-brand-primary)" : "var(--color-content-muted)",
              fontWeight: isActive ? 600 : 500,
              fontSize: "0.875rem",
              border: "1px solid",
              borderColor: isActive ? "var(--color-border-default)" : "transparent",
              borderBottomColor: isActive ? "transparent" : "transparent",
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
                  display: "inline-block",
                  padding: "0.125rem 0.375rem",
                  fontSize: "0.75rem",
                  fontWeight: 600,
                  borderRadius: "9999px",
                  background: isActive ? "var(--color-brand-primary)" : "var(--color-border-default)",
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

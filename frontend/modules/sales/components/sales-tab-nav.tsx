import React, { useRef, useEffect } from "react";
import { useTranslation } from "react-i18next";

export type SalesTabId = "overview" | "quotations" | "orders" | "returns" | "history" | "settings";

export interface SalesTabDef {
  readonly id: SalesTabId;
  readonly label: string;
  readonly badgeCount?: number | undefined;
  readonly isAdvanced?: boolean | undefined;
}

export interface SalesTabNavProps {
  readonly activeTab: SalesTabId;
  readonly onTabChange: (tab: SalesTabId) => void;
  readonly tabs: readonly SalesTabDef[];
}

export function SalesTabNav({ activeTab, onTabChange, tabs }: SalesTabNavProps): React.JSX.Element {
  const { t, i18n } = useTranslation("sales");
  const isRtl = i18n.dir?.() === "rtl" || i18n.language?.startsWith("ar");
  const scrollContainerRef = useRef<HTMLDivElement>(null);
  const activeTabRef = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    if (activeTabRef.current) {
      activeTabRef.current.scrollIntoView({
        behavior: "smooth",
        block: "nearest",
        inline: "nearest"
      });
    }
  }, [activeTab]);

  return (
    <nav
      aria-label={t("overview.title")}
      style={{
        position: "relative",
        width: "100%",
        maxWidth: "100%",
        minWidth: 0,
        marginBottom: "1rem",
        display: "flex",
        alignItems: "center",
        borderBottom: "1px solid var(--color-border-default)",
        backgroundColor: "var(--color-surface-card)",
        borderRadius: "var(--radius-md, 0.5rem) var(--radius-md, 0.5rem) 0 0",
        padding: "0 0.5rem"
      }}
    >
      <div
        ref={scrollContainerRef}
        role="tablist"
        aria-label={t("overview.title")}
        style={{
          display: "flex",
          gap: "0.5rem",
          overflowX: "auto",
          padding: "0.375rem 0.25rem",
          flexGrow: 1,
          minWidth: 0
        }}
      >
        {tabs.map((tab) => {
          const isActive = tab.id === activeTab;
          return (
            <button
              key={tab.id}
              ref={isActive ? activeTabRef : undefined}
              role="tab"
              aria-selected={isActive}
              aria-controls={`sales-panel-${tab.id}`}
              id={`sales-tab-${tab.id}`}
              tabIndex={isActive ? 0 : -1}
              onClick={() => onTabChange(tab.id)}
              type="button"
              style={{
                display: "inline-flex",
                alignItems: "center",
                gap: "0.5rem",
                padding: "0.5rem 0.875rem",
                fontSize: "0.875rem",
                fontWeight: isActive ? 600 : 500,
                color: isActive ? "var(--color-brand-primary)" : "var(--color-content-muted)",
                backgroundColor: isActive ? "var(--color-brand-subtle, rgba(16, 42, 67, 0.06))" : "transparent",
                border: "none",
                borderBottom: isActive ? "2px solid var(--color-brand-primary)" : "2px solid transparent",
                borderRadius: "var(--radius-sm, 0.25rem) var(--radius-sm, 0.25rem) 0 0",
                cursor: "pointer",
                whiteSpace: "nowrap",
                flexShrink: 0,
                transition: "color 0.15s ease, background-color 0.15s ease"
              }}
            >
              <span>{tab.label}</span>
              {tab.badgeCount != null && tab.badgeCount > 0 && (
                <span
                  style={{
                    backgroundColor: isActive ? "var(--color-brand-primary, #1d4ed8)" : "var(--color-surface-subtle, #f1f5f9)",
                    color: isActive ? "#ffffff" : "var(--color-content-muted, #64748b)",
                    borderRadius: "9999px",
                    padding: "0.125rem 0.5rem",
                    fontSize: "0.75rem",
                    fontWeight: 600
                  }}
                >
                  {tab.badgeCount}
                </span>
              )}
            </button>
          );
        })}
      </div>
    </nav>
  );
}

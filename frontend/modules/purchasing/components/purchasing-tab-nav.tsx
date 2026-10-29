import React, { useRef, useEffect } from "react";
import { useTranslation } from "react-i18next";

export type PurchasingTabId =
  | "overview"
  | "orders"
  | "receipts"
  | "matching"
  | "returns"
  | "suppliers"
  | "history"
  | "settings";

export interface PurchasingTabDef {
  readonly id: PurchasingTabId;
  readonly label: string;
  readonly badgeCount?: number | undefined;
  readonly isAdvanced?: boolean | undefined;
}

export interface PurchasingTabNavProps {
  readonly activeTab: PurchasingTabId;
  readonly onTabChange: (tab: PurchasingTabId) => void;
  readonly tabs: readonly PurchasingTabDef[];
}

export function PurchasingTabNav({ activeTab, onTabChange, tabs }: PurchasingTabNavProps): React.JSX.Element {
  const { t } = useTranslation("purchasing");
  const scrollContainerRef = useRef<HTMLDivElement>(null);
  const activeTabRef = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    // Only scroll if active tab is partially outside container on narrow viewports
    if (activeTabRef.current && scrollContainerRef.current) {
      const container = scrollContainerRef.current;
      const tab = activeTabRef.current;
      const cRect = container.getBoundingClientRect();
      const tRect = tab.getBoundingClientRect();
      if (tRect.left < cRect.left || tRect.right > cRect.right) {
        tab.scrollIntoView({ behavior: "smooth", block: "nearest", inline: "nearest" });
      }
    }
  }, [activeTab]);

  return (
    <nav
      aria-label={t("overview.title")}
      className="purchasing-tab-nav"
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
        padding: "0 0.25rem"
      }}
    >
      <div
        ref={scrollContainerRef}
        role="tablist"
        aria-label={t("overview.title")}
        style={{
          display: "flex",
          gap: "0.25rem",
          overflowX: "auto",
          scrollbarWidth: "thin",
          WebkitOverflowScrolling: "touch",
          width: "100%",
          padding: "0.375rem 0.25rem"
        }}
      >
        {tabs.map((tab) => {
          const isActive = tab.id === activeTab;
          return (
            <button
              key={tab.id}
              ref={isActive ? activeTabRef : null}
              type="button"
              role="tab"
              aria-selected={isActive}
              id={`purchasing-tab-${tab.id}`}
              onClick={() => onTabChange(tab.id)}
              style={{
                display: "inline-flex",
                alignItems: "center",
                gap: "0.375rem",
                padding: "0.4375rem 0.75rem",
                fontSize: "0.8125rem",
                fontWeight: isActive ? 600 : 500,
                color: isActive
                  ? "var(--color-brand-primary, #1d4ed8)"
                  : "var(--color-content-muted, #64748b)",
                backgroundColor: isActive
                  ? "var(--color-brand-primary-light, #eff6ff)"
                  : "transparent",
                border: "none",
                borderBottom: isActive ? "2px solid var(--color-brand-primary, #1d4ed8)" : "2px solid transparent",
                borderRadius: "var(--radius-sm, 0.25rem) var(--radius-sm, 0.25rem) 0 0",
                cursor: "pointer",
                whiteSpace: "nowrap",
                flexShrink: 0,
                transition: "all 0.15s ease"
              }}
            >
              <span>{tab.label}</span>
              {tab.badgeCount !== undefined && tab.badgeCount > 0 && (
                <span
                  style={{
                    display: "inline-flex",
                    alignItems: "center",
                    justifyContent: "center",
                    padding: "0.0625rem 0.3125rem",
                    fontSize: "0.6875rem",
                    fontWeight: 600,
                    borderRadius: "9999px",
                    backgroundColor: isActive
                      ? "var(--color-brand-primary, #1d4ed8)"
                      : "var(--color-surface-subtle, #f1f5f9)",
                    color: isActive ? "#ffffff" : "var(--color-content-muted, #64748b)"
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

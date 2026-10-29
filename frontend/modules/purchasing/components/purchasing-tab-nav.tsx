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
        style={{
          display: "flex",
          gap: "0.25rem",
          overflowX: "auto",
          scrollbarWidth: "none",
          msOverflowStyle: "none",
          width: "100%",
          padding: "0.5rem 0"
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
              onClick={() => onTabChange(tab.id)}
              style={{
                display: "inline-flex",
                alignItems: "center",
                gap: "0.5rem",
                padding: "0.5rem 1rem",
                fontSize: "0.875rem",
                fontWeight: isActive ? 600 : 500,
                color: isActive
                  ? "var(--color-brand-primary, #1d4ed8)"
                  : "var(--color-content-muted, #64748b)",
                backgroundColor: isActive
                  ? "var(--color-brand-primary-light, #eff6ff)"
                  : "transparent",
                border: "none",
                borderRadius: "var(--radius-md, 0.5rem)",
                cursor: "pointer",
                whiteSpace: "nowrap",
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
                    padding: "0.125rem 0.375rem",
                    fontSize: "0.75rem",
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

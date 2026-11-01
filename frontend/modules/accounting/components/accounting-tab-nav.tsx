import React, { useRef, useEffect } from "react";
import { useTranslation } from "react-i18next";

export interface AccountingTabItem {
  readonly id: string;
  readonly label: string;
}

export interface AccountingTabNavProps {
  readonly tabs: readonly AccountingTabItem[];
  readonly activeTab: string;
  readonly onSelectTab: (id: string) => void;
}

export function AccountingTabNav({
  tabs,
  activeTab,
  onSelectTab
}: AccountingTabNavProps): React.JSX.Element {
  const { t, i18n } = useTranslation("accounting");
  const isRtl = i18n.dir?.() === "rtl" || i18n.language?.startsWith("ar");
  const scrollContainerRef = useRef<HTMLDivElement>(null);
  const activeTabRef = useRef<HTMLButtonElement>(null);

  // Auto-scroll active tab into view whenever it changes
  useEffect(() => {
    if (activeTabRef.current) {
      activeTabRef.current.scrollIntoView({
        behavior: "smooth",
        block: "nearest",
        inline: "nearest"
      });
    }
  }, [activeTab]);

  const handleScroll = (direction: "start" | "end") => {
    const el = scrollContainerRef.current;
    if (!el) return;
    const distance = 240;
    const sign = isRtl
      ? (direction === "start" ? 1 : -1)
      : (direction === "start" ? -1 : 1);
    el.scrollBy({ left: sign * distance, behavior: "smooth" });
  };

  const handleKeyDown = (e: React.KeyboardEvent, currentIndex: number) => {
    if (e.key === "ArrowRight") {
      e.preventDefault();
      const nextIdx = isRtl
        ? (currentIndex - 1 + tabs.length) % tabs.length
        : (currentIndex + 1) % tabs.length;
      const nextTab = tabs[nextIdx];
      if (nextTab) onSelectTab(nextTab.id);
    } else if (e.key === "ArrowLeft") {
      e.preventDefault();
      const nextIdx = isRtl
        ? (currentIndex + 1) % tabs.length
        : (currentIndex - 1 + tabs.length) % tabs.length;
      const nextTab = tabs[nextIdx];
      if (nextTab) onSelectTab(nextTab.id);
    }
  };

  return (
    <nav
      aria-label={t("title")}
      style={{
        position: "relative",
        width: "100%",
        maxWidth: "100%",
        minWidth: 0,
        marginBottom: "1.5rem",
        display: "flex",
        alignItems: "center",
        borderBottom: "1px solid var(--color-border-default)",
        backgroundColor: "var(--color-surface-card)",
        borderRadius: "var(--radius-md, 0.5rem) var(--radius-md, 0.5rem) 0 0",
        padding: "0 0.25rem"
      }}
    >
      {/* Scroll Prev / Start button */}
      <button
        type="button"
        aria-label={t("tabs.scrollPrev")}
        onClick={() => handleScroll("start")}
        style={{
          display: "inline-flex",
          alignItems: "center",
          justifyContent: "center",
          width: "28px",
          height: "36px",
          border: "none",
          background: "transparent",
          color: "var(--color-content-muted)",
          cursor: "pointer",
          flexShrink: 0,
          borderRadius: "var(--radius-sm, 0.25rem)"
        }}
      >
        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
          {isRtl ? <polyline points="9 18 15 12 9 6" /> : <polyline points="15 18 9 12 15 6" />}
        </svg>
      </button>

      {/* Horizontally scrollable tab strip */}
      <div
        ref={scrollContainerRef}
        role="tablist"
        style={{
          display: "flex",
          gap: "0.25rem",
          overflowX: "auto",
          overflowY: "hidden",
          scrollbarWidth: "none",
          WebkitOverflowScrolling: "touch",
          flex: "1 1 auto",
          minWidth: 0,
          scrollBehavior: "smooth"
        }}
      >
        {tabs.map((tab, idx) => {
          const isActive = activeTab === tab.id;
          return (
            <button
              key={tab.id}
              ref={isActive ? activeTabRef : undefined}
              role="tab"
              aria-selected={isActive}
              tabIndex={isActive ? 0 : -1}
              type="button"
              data-testid={`accounting-tab-${tab.id}`}
              onClick={() => onSelectTab(tab.id)}
              onKeyDown={(e) => handleKeyDown(e, idx)}
              style={{
                display: "inline-flex",
                alignItems: "center",
                padding: "0.75rem 1rem",
                fontSize: "0.875rem",
                fontWeight: isActive ? 700 : 500,
                color: isActive ? "var(--color-brand-primary)" : "var(--color-content-secondary)",
                backgroundColor: isActive ? "var(--color-surface-sunken)" : "transparent",
                border: "none",
                borderBottom: isActive ? "2px solid var(--color-brand-primary)" : "2px solid transparent",
                cursor: "pointer",
                whiteSpace: "nowrap",
                flexShrink: 0,
                outline: "none",
                transition: "all 120ms ease"
              }}
            >
              {tab.label}
            </button>
          );
        })}
      </div>

      {/* Scroll Next / End button */}
      <button
        type="button"
        aria-label={t("tabs.scrollNext")}
        onClick={() => handleScroll("end")}
        style={{
          display: "inline-flex",
          alignItems: "center",
          justifyContent: "center",
          width: "28px",
          height: "36px",
          border: "none",
          background: "transparent",
          color: "var(--color-content-muted)",
          cursor: "pointer",
          flexShrink: 0,
          borderRadius: "var(--radius-sm, 0.25rem)"
        }}
      >
        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
          {isRtl ? <polyline points="15 18 9 12 15 6" /> : <polyline points="9 18 15 12 9 6" />}
        </svg>
      </button>
    </nav>
  );
}

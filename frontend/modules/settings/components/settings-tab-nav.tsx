import React, { useEffect, useRef } from "react";
import { useTranslation } from "react-i18next";

export interface SettingsTabItem {
  readonly id: string;
  readonly label: string;
}

export interface SettingsTabNavProps {
  readonly tabs: readonly SettingsTabItem[];
  readonly activeTab: string;
  readonly panelId: string;
  readonly onSelectTab: (id: string) => void;
}

export function SettingsTabNav({ tabs, activeTab, panelId, onSelectTab }: SettingsTabNavProps): React.JSX.Element {
  const { t, i18n } = useTranslation("settings");
  const isRtl = i18n.dir?.() === "rtl" || i18n.language?.startsWith("ar");
  const scrollContainerRef = useRef<HTMLDivElement>(null);
  const activeTabRef = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    activeTabRef.current?.scrollIntoView({ behavior: "smooth", block: "nearest", inline: "nearest" });
  }, [activeTab]);

  const handleScroll = (direction: "start" | "end") => {
    const element = scrollContainerRef.current;
    if (!element) return;
    const sign = isRtl ? direction === "start" ? 1 : -1 : direction === "start" ? -1 : 1;
    element.scrollBy({ left: sign * 240, behavior: "smooth" });
  };

  const handleKeyDown = (event: React.KeyboardEvent, currentIndex: number) => {
    if (event.key !== "ArrowRight" && event.key !== "ArrowLeft") return;
    event.preventDefault();
    const nextIndex = isRtl
      ? event.key === "ArrowRight" ? currentIndex - 1 : currentIndex + 1
      : event.key === "ArrowRight" ? currentIndex + 1 : currentIndex - 1;
    const nextTab = tabs[(nextIndex + tabs.length) % tabs.length];
    if (nextTab) onSelectTab(nextTab.id);
  };

  return (
    <nav aria-label={t("tabs.ariaLabel")} style={{ position: "relative", width: "100%", maxWidth: "100%", minWidth: 0, display: "flex", alignItems: "center", borderBottom: "1px solid var(--color-border-default)", backgroundColor: "var(--color-surface-card)", borderRadius: "var(--radius-md, 0.5rem) var(--radius-md, 0.5rem) 0 0", padding: "0 0.25rem" }}>
      <button type="button" aria-label={t("tabs.scrollPrev")} onClick={() => handleScroll("start")} style={scrollButtonStyle}>
        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
          <polyline points={isRtl ? "9 18 15 12 9 6" : "15 18 9 12 15 6"} />
        </svg>
      </button>
      <div ref={scrollContainerRef} role="tablist" aria-label={t("tabs.ariaLabel")} style={{ display: "flex", gap: "0.25rem", overflowX: "auto", scrollbarWidth: "none", msOverflowStyle: "none", padding: "0.375rem 0.25rem", flexGrow: 1, minWidth: 0 }}>
        {tabs.map((tab, index) => {
          const isActive = tab.id === activeTab;
          return (
            <button key={tab.id} ref={isActive ? activeTabRef : undefined} id={tab.id} type="button" role="tab" aria-selected={isActive} aria-controls={panelId} tabIndex={isActive ? 0 : -1} onClick={() => onSelectTab(tab.id)} onKeyDown={(event) => handleKeyDown(event, index)} style={{ display: "inline-flex", alignItems: "center", padding: "0.5rem 0.875rem", fontSize: "0.875rem", fontWeight: isActive ? 600 : 500, color: isActive ? "var(--color-brand-primary)" : "var(--color-content-muted)", backgroundColor: isActive ? "var(--color-brand-subtle, rgba(16, 42, 67, 0.06))" : "transparent", border: "none", borderBottom: isActive ? "2px solid var(--color-brand-primary)" : "2px solid transparent", borderRadius: "var(--radius-sm, 0.25rem) var(--radius-sm, 0.25rem) 0 0", cursor: "pointer", whiteSpace: "nowrap", flexShrink: 0 }}>
              {tab.label}
            </button>
          );
        })}
      </div>
      <button type="button" aria-label={t("tabs.scrollNext")} onClick={() => handleScroll("end")} style={scrollButtonStyle}>
        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
          <polyline points={isRtl ? "15 18 9 12 15 6" : "9 18 15 12 9 6"} />
        </svg>
      </button>
    </nav>
  );
}

const scrollButtonStyle: React.CSSProperties = {
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
};

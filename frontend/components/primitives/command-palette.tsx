import React, { useEffect, useRef, useState, useId } from "react";
import { useTranslation } from "react-i18next";
import { SearchIcon, CloseIcon, SparklesIcon, LayersIcon, ClockIcon } from "./icons.tsx";

export interface CommandItem {
  readonly id: string;
  readonly title: string;
  readonly subtitle?: string | undefined;
  readonly category: "navigation" | "actions" | "recent" | "records";
  readonly icon?: React.ReactNode | undefined;
  readonly keywords?: readonly string[] | undefined;
  readonly shortcut?: string | undefined;
  readonly onSelect: () => void;
}

export interface CommandPaletteProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly items: readonly CommandItem[];
  readonly placeholder?: string | undefined;
  readonly noResultsMessage?: string | undefined;
}

const STORAGE_KEY = "erp.command_palette.recents";

export function getStoredRecentCommandIds(): string[] {
  try {
    const raw = window.localStorage.getItem(STORAGE_KEY);
    return raw ? (JSON.parse(raw) as string[]) : [];
  } catch {
    return [];
  }
}

export function saveRecentCommandId(id: string): void {
  try {
    const existing = getStoredRecentCommandIds().filter((item) => item !== id);
    const updated = [id, ...existing].slice(0, 8);
    window.localStorage.setItem(STORAGE_KEY, JSON.stringify(updated));
  } catch {
    // Graceful fallback for private/restricted storage
  }
}

export function CommandPalette({
  isOpen,
  onClose,
  items,
  placeholder,
  noResultsMessage
}: CommandPaletteProps): React.JSX.Element | null {
  const { t } = useTranslation("common");
  const [query, setQuery] = useState("");
  const [selectedIndex, setSelectedIndex] = useState(0);
  const inputRef = useRef<HTMLInputElement>(null);
  const listRef = useRef<HTMLUListElement>(null);
  const titleId = useId();

  // Reset query and focus on open
  useEffect(() => {
    if (isOpen) {
      setQuery("");
      setSelectedIndex(0);
      const timer = setTimeout(() => {
        inputRef.current?.focus();
      }, 50);
      return () => clearTimeout(timer);
    }
  }, [isOpen]);

  // Global escape key handler while open
  useEffect(() => {
    if (!isOpen) return;

    function handleKeyDown(e: KeyboardEvent) {
      if (e.key === "Escape") {
        e.preventDefault();
        onClose();
      }
    }

    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [isOpen, onClose]);

  if (!isOpen) return null;

  // Filter items
  const cleanQuery = query.trim().toLowerCase();
  const recentIds = getStoredRecentCommandIds();

  const filteredItems = items
    .filter((item) => {
      if (!cleanQuery) return true;
      const matchesTitle = item.title.toLowerCase().includes(cleanQuery);
      const matchesSubtitle = item.subtitle?.toLowerCase().includes(cleanQuery);
      const matchesKeywords = item.keywords?.some((k) => k.toLowerCase().includes(cleanQuery));
      return matchesTitle || matchesSubtitle || matchesKeywords;
    })
    .sort((a, b) => {
      // Prioritize recents when no query is typed
      if (!cleanQuery) {
        const aRecent = recentIds.indexOf(a.id);
        const bRecent = recentIds.indexOf(b.id);
        if (aRecent !== -1 && bRecent !== -1) return aRecent - bRecent;
        if (aRecent !== -1) return -1;
        if (bRecent !== -1) return 1;
      }
      return 0;
    });

  const handleSelect = (item: CommandItem) => {
    saveRecentCommandId(item.id);
    onClose();
    item.onSelect();
  };

  const handleInputKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (filteredItems.length === 0) return;

    if (e.key === "ArrowDown") {
      e.preventDefault();
      setSelectedIndex((prev) => (prev + 1) % filteredItems.length);
      scrollSelectedIntoView((selectedIndex + 1) % filteredItems.length);
    } else if (e.key === "ArrowUp") {
      e.preventDefault();
      setSelectedIndex((prev) => (prev - 1 + filteredItems.length) % filteredItems.length);
      scrollSelectedIntoView((selectedIndex - 1 + filteredItems.length) % filteredItems.length);
    } else if (e.key === "Enter") {
      e.preventDefault();
      const current = filteredItems[selectedIndex];
      if (current) {
        handleSelect(current);
      }
    }
  };

  const scrollSelectedIntoView = (index: number) => {
    if (!listRef.current) return;
    const items = listRef.current.querySelectorAll<HTMLLIElement>('[role="option"]');
    const target = items[index];
    target?.scrollIntoView({ block: "nearest" });
  };

  const categoryLabels: Record<CommandItem["category"], string> = {
    actions: t("commandPalette.categoryActions", "إجراءات سريعة"),
    navigation: t("commandPalette.categoryNavigation", "شاشات ووحدات النظام"),
    recent: t("commandPalette.categoryRecent", "سجلات وعمليات حديثة"),
    records: t("commandPalette.categoryRecords", "سجلات وبيانات")
  };

  const getCategoryIcon = (category: CommandItem["category"]) => {
    switch (category) {
      case "actions":
        return <SparklesIcon size={14} style={{ color: "#3b82f6" }} />;
      case "recent":
        return <ClockIcon size={14} style={{ color: "#f59e0b" }} />;
      default:
        return <LayersIcon size={14} style={{ color: "#10b981" }} />;
    }
  };

  return (
    <div
      className="command-palette-backdrop"
      onClick={onClose}
      role="presentation"
      style={{
        position: "fixed",
        inset: 0,
        backgroundColor: "rgba(16, 42, 67, 0.65)",
        backdropFilter: "blur(4px)",
        zIndex: "var(--z-modal, 1400)" as unknown as number,
        display: "flex",
        alignItems: "flex-start",
        justifyContent: "center",
        paddingTop: "12vh",
        paddingInline: "1rem",
        animation: "fade-in 150ms ease"
      }}
    >
      <div
        className="command-palette-card"
        onClick={(e) => e.stopPropagation()}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        style={{
          width: "100%",
          maxWidth: "640px",
          backgroundColor: "#ffffff",
          borderRadius: "var(--radius-lg, 0.75rem)",
          boxShadow: "0 20px 35px -8px rgba(16, 42, 67, 0.3), 0 8px 16px -4px rgba(0, 0, 0, 0.1)",
          border: "1px solid #cbd5e1",
          overflow: "hidden",
          display: "flex",
          flexDirection: "column"
        }}
      >
        <span id={titleId} style={{ position: "absolute", width: "1px", height: "1px", overflow: "hidden", clip: "rect(0,0,0,0)" }}>
          {t("commandPalette.title", "لوحة الأوامر والتنقل السريع")}
        </span>

        {/* Search Input Bar */}
        <div
          style={{
            display: "flex",
            alignItems: "center",
            gap: "0.75rem",
            padding: "1rem 1.25rem",
            borderBottom: "1px solid #e2e8f0",
            backgroundColor: "#f8fafc"
          }}
        >
          <SearchIcon size={20} style={{ color: "#64748b", flexShrink: 0 }} />
          <input
            ref={inputRef}
            type="text"
            role="combobox"
            aria-expanded="true"
            aria-controls="command-palette-list"
            value={query}
            onChange={(e) => {
              setQuery(e.target.value);
              setSelectedIndex(0);
            }}
            onKeyDown={handleInputKeyDown}
            placeholder={placeholder ?? t("commandPalette.placeholder", "البحث في الشاشات، العمليات السريعة، أو الاختصارات... (Ctrl + K)")}
            style={{
              flex: 1,
              border: "none",
              outline: "none",
              backgroundColor: "transparent",
              fontSize: "1rem",
              fontWeight: 500,
              color: "#0f172a",
              fontFamily: "var(--font-sans)"
            }}
          />
          <button
            type="button"
            onClick={onClose}
            aria-label={t("actions.close", "إغلاق")}
            style={{
              border: "none",
              background: "transparent",
              color: "#94a3b8",
              cursor: "pointer",
              padding: "0.25rem",
              display: "flex",
              alignItems: "center"
            }}
          >
            <CloseIcon size={18} />
          </button>
        </div>

        {/* Results List */}
        <div style={{ maxHeight: "380px", overflowY: "auto", padding: "0.5rem" }}>
          {filteredItems.length === 0 ? (
            <div style={{ padding: "2.5rem 1rem", textAlign: "center", color: "#64748b" }}>
              <p style={{ margin: 0, fontSize: "0.9375rem" }}>
                {noResultsMessage ?? t("commandPalette.noResults", "لا توجد نتائج مطابقة لبحثك")}
              </p>
            </div>
          ) : (
            <ul
              id="command-palette-list"
              ref={listRef}
              role="listbox"
              style={{ listStyle: "none", margin: 0, padding: 0, display: "flex", flexDirection: "column", gap: "0.25rem" }}
            >
              {filteredItems.map((item, index) => {
                const isSelected = index === selectedIndex;
                const isRecent = !cleanQuery && recentIds.includes(item.id);

                return (
                  <li
                    key={item.id}
                    role="option"
                    aria-selected={isSelected}
                    onClick={() => handleSelect(item)}
                    onMouseEnter={() => setSelectedIndex(index)}
                    style={{
                      display: "flex",
                      alignItems: "center",
                      gap: "0.75rem",
                      padding: "0.625rem 0.875rem",
                      borderRadius: "var(--radius-md, 0.5rem)",
                      backgroundColor: isSelected ? "#102a43" : "transparent",
                      color: isSelected ? "#ffffff" : "#0f172a",
                      cursor: "pointer",
                      transition: "background-color 100ms ease, color 100ms ease"
                    }}
                  >
                    <span
                      style={{
                        display: "inline-flex",
                        alignItems: "center",
                        justifyContent: "center",
                        width: "30px",
                        height: "30px",
                        borderRadius: "var(--radius-sm, 0.375rem)",
                        backgroundColor: isSelected ? "rgba(255, 255, 255, 0.15)" : "#f1f5f9",
                        color: isSelected ? "#ffffff" : "#334155",
                        flexShrink: 0
                      }}
                    >
                      {item.icon ?? getCategoryIcon(item.category)}
                    </span>

                    <div style={{ flex: 1, minWidth: 0 }}>
                      <div style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
                        <span style={{ fontWeight: 600, fontSize: "0.875rem" }}>{item.title}</span>
                        {isRecent && (
                          <span
                            style={{
                              fontSize: "0.6875rem",
                              padding: "0.1rem 0.375rem",
                              borderRadius: "var(--radius-full, 9999px)",
                              backgroundColor: isSelected ? "rgba(255, 255, 255, 0.2)" : "#fef3c7",
                              color: isSelected ? "#ffffff" : "#92400e",
                              fontWeight: 600
                            }}
                          >
                            {t("commandPalette.badgeRecent", "سابق")}
                          </span>
                        )}
                      </div>
                      {item.subtitle && (
                        <div
                          style={{
                            fontSize: "0.75rem",
                            color: isSelected ? "rgba(255, 255, 255, 0.75)" : "#64748b",
                            textOverflow: "ellipsis",
                            overflow: "hidden",
                            whiteSpace: "nowrap"
                          }}
                        >
                          {item.subtitle}
                        </div>
                      )}
                    </div>

                    <span
                      style={{
                        fontSize: "0.6875rem",
                        padding: "0.15rem 0.5rem",
                        borderRadius: "var(--radius-sm, 0.25rem)",
                        backgroundColor: isSelected ? "rgba(255, 255, 255, 0.2)" : "#f1f5f9",
                        color: isSelected ? "#ffffff" : "#64748b",
                        fontWeight: 500,
                        flexShrink: 0
                      }}
                    >
                      {item.shortcut ?? categoryLabels[item.category]}
                    </span>
                  </li>
                );
              })}
            </ul>
          )}
        </div>

        {/* Footer shortcuts hint */}
        <div
          style={{
            display: "flex",
            alignItems: "center",
            justifyContent: "space-between",
            padding: "0.625rem 1.25rem",
            borderTop: "1px solid #e2e8f0",
            backgroundColor: "#f8fafc",
            fontSize: "0.75rem",
            color: "#64748b"
          }}
        >
          <div style={{ display: "flex", alignItems: "center", gap: "1rem" }}>
            <span>
              <kbd style={kbdStyle}>↑</kbd> <kbd style={kbdStyle}>↓</kbd> {t("commandPalette.navigate", "للتنقل")}
            </span>
            <span>
              <kbd style={kbdStyle}>{t("commandPalette.keyEnter", "Enter")}</kbd> {t("commandPalette.select", "للاختيار")}
            </span>
            <span>
              <kbd style={kbdStyle}>{t("commandPalette.keyEsc", "Esc")}</kbd> {t("commandPalette.dismiss", "للإغلاق")}
            </span>
          </div>
          <span style={{ fontWeight: 600, color: "#102a43" }}>{t("commandPalette.spotlightBrand", "TODIR Spotlight")}</span>
        </div>
      </div>
    </div>
  );
}

const kbdStyle: React.CSSProperties = {
  display: "inline-block",
  padding: "0.1rem 0.35rem",
  fontSize: "0.6875rem",
  fontFamily: "var(--font-mono)",
  fontWeight: 600,
  lineHeight: 1,
  color: "#334155",
  backgroundColor: "#e2e8f0",
  border: "1px solid #cbd5e1",
  borderRadius: "var(--radius-sm, 0.25rem)",
  boxShadow: "0 1px 0 rgba(0,0,0,0.1)"
};

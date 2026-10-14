import React, { useState, useRef, useEffect } from "react";
import { useTranslation } from "react-i18next";
import {
  GlobeIcon,
  SunMoonIcon,
  DensityIcon,
  LogOutIcon,
  ChevronDownIcon
} from "../../components/primitives/icons.tsx";
import { Separator } from "../../components/primitives/separator.tsx";

export interface UserMenuProps {
  readonly userName?: string | undefined;
  readonly userEmail?: string | undefined;
  readonly onToggleLanguage: () => void;
  readonly theme: string;
  readonly onToggleTheme: () => void;
  readonly density: "comfortable" | "compact";
  readonly onToggleDensity: () => void;
  readonly onSignOut: () => void;
}

export function UserMenu({
  userName = "مسؤول النظام",
  userEmail = "admin@atlas.com",
  onToggleLanguage,
  theme,
  onToggleTheme,
  density,
  onToggleDensity,
  onSignOut
}: UserMenuProps): React.JSX.Element {
  const [isOpen, setIsOpen] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);

  const { t, i18n } = useTranslation("common");
  const isRtl = i18n.dir() === "rtl";

  useEffect(() => {
    function handleClickOutside(event: MouseEvent) {
      if (menuRef.current && !menuRef.current.contains(event.target as Node)) {
        setIsOpen(false);
      }
    }
    if (isOpen) {
      document.addEventListener("mousedown", handleClickOutside);
      return () => document.removeEventListener("mousedown", handleClickOutside);
    }
  }, [isOpen]);

  const initials = userName
    ? userName
        .split(" ")
        .map((n) => n[0])
        .slice(0, 2)
        .join("")
    : "U";

  return (
    <div ref={menuRef} style={{ position: "relative" }}>
      {/* Trigger Button */}
      <button
        type="button"
        id="user-menu-trigger"
        aria-haspopup="true"
        aria-expanded={isOpen}
        onClick={() => setIsOpen((prev) => !prev)}
        style={{
          display: "flex",
          alignItems: "center",
          gap: "0.5rem",
          padding: "0.375rem 0.625rem",
          borderRadius: "var(--radius-md, 0.375rem)",
          backgroundColor: isOpen ? "var(--color-surface-subtle, #f1f5f9)" : "transparent",
          border: "1px solid var(--color-border-default, #e2e8f0)",
          color: "var(--color-content-primary, #0f172a)",
          cursor: "pointer",
          fontSize: "0.875rem",
          fontWeight: 600,
          outline: "none",
          transition: "background-color 150ms ease, border-color 150ms ease"
        }}
        onMouseEnter={(e) => {
          if (!isOpen) e.currentTarget.style.backgroundColor = "var(--color-surface-subtle, #f1f5f9)";
        }}
        onMouseLeave={(e) => {
          if (!isOpen) e.currentTarget.style.backgroundColor = "transparent";
        }}
      >
        {/* User Avatar Initials */}
        <span
          style={{
            width: "28px",
            height: "28px",
            borderRadius: "50%",
            background: "linear-gradient(135deg, var(--color-brand-primary, #4f46e5) 0%, var(--color-brand-primary-hover, #4338ca) 100%)",
            color: "#ffffff",
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            fontSize: "0.75rem",
            fontWeight: 700,
            flexShrink: 0,
            boxShadow: "0 1px 2px rgba(79, 70, 229, 0.2)"
          }}
        >
          {initials}
        </span>

        <span style={{ fontSize: "0.875rem", fontWeight: 600 }}>{userName}</span>
        <ChevronDownIcon size={14} style={{ color: "var(--color-content-muted, #64748b)" }} />
      </button>

      {/* Dropdown Menu */}
      {isOpen && (
        <div
          role="menu"
          style={{
            position: "absolute",
            top: "calc(100% + 6px)",
            insetInlineEnd: 0,
            width: "240px",
            backgroundColor: "var(--color-surface-card, #ffffff)",
            border: "1px solid var(--color-border-default, #e2e8f0)",
            borderRadius: "var(--radius-lg, 0.75rem)",
            boxShadow: "var(--shadow-lg, 0 10px 15px -3px rgba(0, 0, 0, 0.1), 0 4px 6px -4px rgba(0, 0, 0, 0.05))",
            padding: "0.5rem",
            zIndex: 1200,
            display: "flex",
            flexDirection: "column",
            gap: "0.25rem"
          }}
        >
          {/* User Profile Info */}
          <div style={{ padding: "0.5rem 0.625rem" }}>
            <div style={{ fontSize: "0.875rem", fontWeight: 700, color: "var(--color-content-primary, #0f172a)" }}>
              {userName}
            </div>
            <div
              dir="ltr"
              style={{
                fontSize: "0.75rem",
                color: "var(--color-content-muted, #64748b)",
                marginTop: "0.125rem",
                textAlign: isRtl ? "right" : "left"
              }}
            >
              {userEmail}
            </div>
          </div>

          <Separator style={{ margin: "0.25rem 0" }} />

          {/* Preferences items */}
          <button
            type="button"
            role="menuitem"
            onClick={() => {
              onToggleLanguage();
              setIsOpen(false);
            }}
            style={menuItemStyle}
            onMouseEnter={handleItemHover}
            onMouseLeave={handleItemLeave}
          >
            <span style={{ display: "flex", alignItems: "center", gap: "0.625rem" }}>
              <GlobeIcon size={16} />
              <span>{t("preferences.language")}</span>
            </span>
            <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>{t(i18n.resolvedLanguage === "ar" ? "language.switchToEnglish" : "language.switchToArabic")}</span>
          </button>

          <button
            type="button"
            role="menuitem"
            onClick={() => {
              onToggleTheme();
              setIsOpen(false);
            }}
            style={menuItemStyle}
            onMouseEnter={handleItemHover}
            onMouseLeave={handleItemLeave}
          >
            <span style={{ display: "flex", alignItems: "center", gap: "0.625rem" }}>
              <SunMoonIcon size={16} />
              <span>{t("preferences.theme")}</span>
            </span>
            <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
              {theme === "default" ? t("preferences.alternateTheme") : t("preferences.defaultTheme")}
            </span>
          </button>

          <button
            type="button"
            role="menuitem"
            onClick={() => {
              onToggleDensity();
              setIsOpen(false);
            }}
            style={menuItemStyle}
            onMouseEnter={handleItemHover}
            onMouseLeave={handleItemLeave}
          >
            <span style={{ display: "flex", alignItems: "center", gap: "0.625rem" }}>
              <DensityIcon size={16} />
              <span>{t("preferences.density")}</span>
            </span>
            <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
              {density === "comfortable" ? t("preferences.compact") : t("preferences.comfortable")}
            </span>
          </button>

          <Separator style={{ margin: "0.25rem 0" }} />

          {/* Sign Out Item */}
          <button
            type="button"
            role="menuitem"
            onClick={() => {
              setIsOpen(false);
              onSignOut();
            }}
            style={{
              ...menuItemStyle,
              color: "var(--color-status-danger, #dc2626)"
            }}
            onMouseEnter={(e) => {
              e.currentTarget.style.backgroundColor = "var(--color-status-danger-subtle, #fef2f2)";
            }}
            onMouseLeave={(e) => {
              e.currentTarget.style.backgroundColor = "transparent";
            }}
          >
            <span style={{ display: "flex", alignItems: "center", gap: "0.625rem" }}>
              <LogOutIcon size={16} />
              <span>{t("shell.signOut")}</span>
            </span>
          </button>
        </div>
      )}
    </div>
  );
}

const menuItemStyle: React.CSSProperties = {
  display: "flex",
  alignItems: "center",
  justifyContent: "space-between",
  padding: "0.5rem 0.625rem",
  borderRadius: "var(--radius-sm, 0.375rem)",
  border: "none",
  backgroundColor: "transparent",
  color: "var(--color-content-primary, #0f172a)",
  fontSize: "0.8125rem",
  fontWeight: 500,
  cursor: "pointer",
  textAlign: "start",
  width: "100%",
  transition: "background-color 150ms ease, color 150ms ease"
};

function handleItemHover(e: React.MouseEvent<HTMLButtonElement>) {
  e.currentTarget.style.backgroundColor = "var(--color-surface-subtle, #f1f5f9)";
}

function handleItemLeave(e: React.MouseEvent<HTMLButtonElement>) {
  e.currentTarget.style.backgroundColor = "transparent";
}

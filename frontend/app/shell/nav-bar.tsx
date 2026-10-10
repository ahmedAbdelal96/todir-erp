import React, { type ReactNode } from "react";
import clsx from "clsx";

export interface NavItem {
  readonly id: string;
  readonly label: string;
  readonly href?: string | undefined;
  readonly icon?: ReactNode | undefined;
  readonly badge?: ReactNode | undefined;
  readonly requiredPermission?: string | undefined;
  readonly requiredCapability?: string | undefined;
}

export interface NavBarProps {
  readonly items: readonly NavItem[];
  readonly activeItemId?: string | undefined;
  readonly onSelect?: ((id: string) => void) | undefined;
  readonly className?: string | undefined;
}

export function NavBar({
  items,
  activeItemId,
  onSelect,
  className
}: NavBarProps): React.JSX.Element {
  return (
    <nav
      className={clsx("ui-navbar", className)}
      style={{
        display: "flex",
        flexDirection: "column",
        gap: "0.25rem",
        padding: "0.5rem"
      }}
    >
      {items.map((item) => {
        const isActive = item.id === activeItemId;
        return (
          <a
            key={item.id}
            href={item.href ?? `#${item.id}`}
            onClick={(e) => {
              if (onSelect) {
                e.preventDefault();
                onSelect(item.id);
              }
            }}
            className={clsx("ui-nav-item", { "ui-nav-item--active": isActive })}
            style={{
              display: "flex",
              alignItems: "center",
              gap: "0.75rem",
              padding: "0.625rem 0.875rem",
              borderRadius: "var(--radius-md)",
              textDecoration: "none",
              fontSize: "0.875rem",
              fontWeight: isActive ? 600 : 500,
              color: isActive ? "var(--color-action-primary)" : "var(--color-content-secondary)",
              backgroundColor: isActive ? "var(--color-action-primary-subtle, rgba(37, 99, 235, 0.08))" : "transparent",
              transition: "background-color 150ms ease, color 150ms ease"
            }}
          >
            {item.icon && <span style={{ flexShrink: 0 }}>{item.icon}</span>}
            <span style={{ flex: 1 }}>{item.label}</span>
            {item.badge && <span style={{ marginInlineStart: "auto" }}>{item.badge}</span>}
          </a>
        );
      })}
    </nav>
  );
}

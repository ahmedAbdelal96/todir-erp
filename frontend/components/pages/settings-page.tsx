import React, { type ReactNode } from "react";
import { Breadcrumbs, type BreadcrumbItem } from "../../app/shell/breadcrumbs.tsx";
import clsx from "clsx";

export interface SettingsNavItem {
  readonly id: string;
  readonly label: string;
  readonly icon?: ReactNode | undefined;
}

export interface SettingsPageProps {
  readonly title: string;
  readonly subtitle?: string | undefined;
  readonly maxWidth?: string | undefined;
  readonly breadcrumbs?: readonly BreadcrumbItem[] | undefined;
  readonly navItems?: readonly SettingsNavItem[] | undefined;
  readonly activeNavId?: string | undefined;
  readonly onSelectNav?: ((id: string) => void) | undefined;
  readonly children: ReactNode;
  readonly className?: string | undefined;
}

export function SettingsPage({
  title,
  subtitle,
  maxWidth = "1200px",
  breadcrumbs,
  navItems,
  activeNavId,
  onSelectNav,
  children,
  className
}: SettingsPageProps): React.JSX.Element {
  return (
    <div
      className={clsx("ui-settings-page", className)}
      style={{
        display: "flex",
        flexDirection: "column",
        gap: "1.25rem",
        padding: "1.5rem",
        maxWidth,
        margin: "0 auto",
        width: "100%",
        boxSizing: "border-box"
      }}
    >
      {breadcrumbs && <Breadcrumbs items={breadcrumbs} />}

      <header style={{ paddingBottom: "1rem", borderBottom: "1px solid var(--color-border-default)" }}>
        <h1
          style={{
            margin: 0,
            fontSize: "1.5rem",
            fontWeight: 700,
            color: "var(--color-content-primary)",
            fontFamily: "var(--font-sans)"
          }}
        >
          {title}
        </h1>
        {subtitle && (
          <p
            style={{
              margin: "0.25rem 0 0 0",
              fontSize: "0.875rem",
              color: "var(--color-content-muted)"
            }}
          >
            {subtitle}
          </p>
        )}
      </header>

      <div
        style={{
          display: "grid",
          gridTemplateColumns: navItems && navItems.length > 0 ? "240px 1fr" : "1fr",
          gap: "2rem",
          alignItems: "start"
        }}
      >
        {navItems && navItems.length > 0 && (
          <nav
            style={{
              display: "flex",
              flexDirection: "column",
              gap: "0.25rem",
              padding: "0.5rem",
              backgroundColor: "var(--color-surface-card)",
              border: "1px solid var(--color-border-default)",
              borderRadius: "var(--radius-md)"
            }}
          >
            {navItems.map((item) => {
              const isActive = item.id === activeNavId;
              return (
                <button
                  key={item.id}
                  type="button"
                  onClick={() => onSelectNav?.(item.id)}
                  style={{
                    display: "flex",
                    alignItems: "center",
                    gap: "0.5rem",
                    padding: "0.5rem 0.75rem",
                    border: "none",
                    borderRadius: "var(--radius-sm, 0.25rem)",
                    backgroundColor: isActive ? "var(--color-surface-subtle)" : "transparent",
                    color: isActive ? "var(--color-action-primary)" : "var(--color-content-secondary)",
                    fontWeight: isActive ? 600 : 500,
                    fontSize: "0.875rem",
                    cursor: "pointer",
                    textAlign: "start",
                    width: "100%"
                  }}
                >
                  {item.icon}
                  <span>{item.label}</span>
                </button>
              );
            })}
          </nav>
        )}

        <main style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
          {children}
        </main>
      </div>
    </div>
  );
}

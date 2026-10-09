import React, { type ReactNode } from "react";
import clsx from "clsx";

export interface BreadcrumbItem {
  readonly label: ReactNode;
  readonly href?: string | undefined;
  readonly onClick?: (() => void) | undefined;
}

export interface BreadcrumbsProps {
  readonly items: readonly BreadcrumbItem[];
  readonly className?: string | undefined;
}

export function Breadcrumbs({ items, className }: BreadcrumbsProps): React.JSX.Element {
  return (
    <nav aria-label="Breadcrumbs" className={clsx("ui-breadcrumbs", className)}>
      <ol
        style={{
          display: "flex",
          alignItems: "center",
          gap: "0.5rem",
          listStyle: "none",
          padding: 0,
          margin: 0,
          fontSize: "0.875rem"
        }}
      >
        {items.map((item, index) => {
          const isLast = index === items.length - 1;
          return (
            <li key={index} style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
              {index > 0 && (
                <span style={{ color: "var(--color-content-muted)", userSelect: "none" }}>/</span>
              )}
              {isLast || (!item.href && !item.onClick) ? (
                <span
                  style={{
                    color: isLast ? "var(--color-content-primary)" : "var(--color-content-muted)",
                    fontWeight: isLast ? 600 : 400
                  }}
                  aria-current={isLast ? "page" : undefined}
                >
                  {item.label}
                </span>
              ) : (
                <a
                  href={item.href ?? "#"}
                  onClick={(e) => {
                    if (item.onClick) {
                      e.preventDefault();
                      item.onClick();
                    }
                  }}
                  style={{
                    color: "var(--color-content-secondary)",
                    textDecoration: "none"
                  }}
                >
                  {item.label}
                </a>
              )}
            </li>
          );
        })}
      </ol>
    </nav>
  );
}

import React, { type ReactNode } from "react";
import clsx from "clsx";

export interface FormSectionProps {
  readonly title?: string | undefined;
  readonly description?: string | undefined;
  readonly columns?: 1 | 2 | 3 | 4 | undefined;
  readonly className?: string | undefined;
  readonly children: ReactNode;
}

export function FormSection({
  title,
  description,
  columns = 2,
  className,
  children
}: FormSectionProps): React.JSX.Element {
  const getGridTemplateColumns = () => {
    switch (columns) {
      case 1:
        return "1fr";
      case 2:
        return "repeat(auto-fit, minmax(280px, 1fr))";
      case 3:
        return "repeat(auto-fit, minmax(220px, 1fr))";
      case 4:
        return "repeat(auto-fit, minmax(180px, 1fr))";
    }
  };

  return (
    <section
      className={clsx("ui-form-section", className)}
      style={{
        display: "flex",
        flexDirection: "column",
        gap: "1rem",
        padding: "1.25rem 1.5rem",
        backgroundColor: "var(--color-surface-card)",
        border: "1px solid var(--color-border-default)",
        borderRadius: "var(--radius-lg, 0.75rem)",
        boxShadow: "var(--shadow-sm, 0 1px 3px 0 rgba(15, 23, 42, 0.08))"
      }}
    >
      {(title || description) && (
        <div style={{ borderBottom: "1px solid var(--color-border-subtle, #e2e8f0)", paddingBottom: "0.75rem" }}>
          {title && (
            <h3
              style={{
                margin: 0,
                fontSize: "1rem",
                fontWeight: 600,
                color: "var(--color-content-primary)"
              }}
            >
              {title}
            </h3>
          )}
          {description && (
            <p
              style={{
                margin: "0.25rem 0 0 0",
                fontSize: "0.8125rem",
                color: "var(--color-content-muted)"
              }}
            >
              {description}
            </p>
          )}
        </div>
      )}

      <div
        style={{
          display: "grid",
          gridTemplateColumns: getGridTemplateColumns(),
          gap: "1rem",
          alignItems: "start"
        }}
      >
        {children}
      </div>
    </section>
  );
}

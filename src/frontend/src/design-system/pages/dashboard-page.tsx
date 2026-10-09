import React, { type ReactNode } from "react";
import clsx from "clsx";

export interface KpiCardProps {
  readonly title: string;
  readonly value: ReactNode;
  readonly change?: {
    readonly value: string;
    readonly isPositive?: boolean | undefined;
  } | undefined;
  readonly icon?: ReactNode | undefined;
}

export function KpiCard({ title, value, change, icon }: KpiCardProps): React.JSX.Element {
  return (
    <div
      style={{
        backgroundColor: "var(--color-surface-card)",
        border: "1px solid var(--color-border-default)",
        borderRadius: "var(--radius-lg, 0.5rem)",
        padding: "1.25rem",
        display: "flex",
        flexDirection: "column",
        gap: "0.5rem"
      }}
    >
      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between" }}>
        <span style={{ fontSize: "0.875rem", color: "var(--color-content-muted)", fontWeight: 500 }}>
          {title}
        </span>
        {icon && <span style={{ color: "var(--color-content-muted)" }}>{icon}</span>}
      </div>
      <div style={{ fontSize: "1.75rem", fontWeight: 700, color: "var(--color-content-primary)" }}>
        {value}
      </div>
      {change && (
        <div
          style={{
            fontSize: "0.75rem",
            fontWeight: 600,
            color: change.isPositive ? "var(--color-status-success)" : "var(--color-status-danger)"
          }}
        >
          {change.value}
        </div>
      )}
    </div>
  );
}

export interface DashboardPageProps {
  readonly title: string;
  readonly subtitle?: string | undefined;
  readonly kpis?: readonly KpiCardProps[] | undefined;
  readonly children: ReactNode;
  readonly className?: string | undefined;
}

export function DashboardPage({
  title,
  subtitle,
  kpis = [],
  children,
  className
}: DashboardPageProps): React.JSX.Element {
  return (
    <div
      className={clsx("ui-dashboard-page", className)}
      style={{
        display: "flex",
        flexDirection: "column",
        gap: "1.5rem",
        padding: "1.5rem",
        maxWidth: "1400px",
        margin: "0 auto",
        width: "100%",
        boxSizing: "border-box"
      }}
    >
      <header>
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
          <p style={{ margin: "0.25rem 0 0 0", fontSize: "0.875rem", color: "var(--color-content-muted)" }}>
            {subtitle}
          </p>
        )}
      </header>

      {kpis.length > 0 && (
        <div
          style={{
            display: "grid",
            gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))",
            gap: "1rem"
          }}
        >
          {kpis.map((kpi, idx) => (
            <KpiCard key={idx} {...kpi} />
          ))}
        </div>
      )}

      <main style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
        {children}
      </main>
    </div>
  );
}

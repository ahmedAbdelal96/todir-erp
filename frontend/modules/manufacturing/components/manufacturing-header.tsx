import React from "react";
import { Link, useLocation } from "react-router-dom";
import { useTranslation } from "react-i18next";

export interface ManufacturingHeaderProps {
  readonly title: string;
  readonly subtitle?: string;
  readonly primaryAction?: React.ReactNode;
  readonly secondaryActions?: React.ReactNode;
}

interface TabLink {
  readonly path: string;
  readonly labelKey: string;
  readonly fallback: string;
}

const TABS: readonly TabLink[] = [
  { path: "/manufacturing", labelKey: "tabs.overview", fallback: "لوحة التحكم" },
  { path: "/manufacturing/orders", labelKey: "tabs.orders", fallback: "أوامر الإنتاج" },
  { path: "/manufacturing/shop-floor", labelKey: "tabs.shopFloor", fallback: "صالة التشغيل" },
  { path: "/manufacturing/materials", labelKey: "tabs.materials", fallback: "المواد والخامات" },
  { path: "/manufacturing/mrp", labelKey: "tabs.mrp", fallback: "تخطيط المتطلبات (MRP)" },
  { path: "/manufacturing/definitions", labelKey: "tabs.definitions", fallback: "قوائم المواد والتعريفات" },
  { path: "/manufacturing/capacity", labelKey: "tabs.capacity", fallback: "الطاقة ومراكز العمل" },
  { path: "/manufacturing/genealogy", labelKey: "tabs.genealogy", fallback: "تتبع التشغيلات" },
  { path: "/manufacturing/costing", labelKey: "tabs.costing", fallback: "تكاليف الإنتاج والـ WIP" }
];

export function ManufacturingHeader({
  title,
  subtitle,
  primaryAction,
  secondaryActions
}: ManufacturingHeaderProps): React.JSX.Element {
  const { t } = useTranslation("manufacturing");
  const location = useLocation();

  const isCurrent = (path: string) => {
    if (path === "/manufacturing") {
      return location.pathname === "/manufacturing";
    }
    return location.pathname.startsWith(path);
  };

  return (
    <header
      style={{
        marginBottom: "1.25rem",
        background: "#ffffff",
        border: "1px solid #cbd5e1",
        borderRadius: "8px",
        padding: "1rem 1.25rem 0",
        boxShadow: "0 1px 3px rgba(0, 0, 0, 0.05)"
      }}
    >
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", flexWrap: "wrap", gap: "12px", marginBottom: "0.75rem" }}>
        <div>
          <nav style={{ fontSize: "0.8125rem", color: "#64748b", marginBottom: "0.25rem" }}>
            <Link to="/manufacturing" style={{ color: "inherit", textDecoration: "none" }}>
              {t("navigationKey", "التصنيع وتخطيط الإنتاج (MRP)")}
            </Link>
            {" / "}
            <span style={{ color: "#0f172a", fontWeight: 600 }}>{title}</span>
          </nav>
          <h1 style={{ margin: "0 0 0.25rem", fontSize: "1.375rem", color: "#102a43", fontWeight: 700 }}>
            {title}
          </h1>
          {subtitle && (
            <p style={{ margin: 0, fontSize: "0.875rem", color: "#64748b" }}>
              {subtitle}
            </p>
          )}
        </div>

        <div style={{ display: "flex", alignItems: "center", gap: "8px" }}>
          {secondaryActions}
          {primaryAction}
        </div>
      </div>

      <nav
        style={{
          display: "flex",
          gap: "4px",
          borderTop: "1px solid #f1f5f9",
          paddingTop: "0.5rem",
          paddingBottom: "4px",
          overflowX: "auto",
          WebkitOverflowScrolling: "touch",
          scrollbarWidth: "none",
          msOverflowStyle: "none"
        }}
      >
        {TABS.map((tab) => {
          const active = isCurrent(tab.path);
          return (
            <Link
              key={tab.path}
              to={tab.path}
              style={{
                padding: "8px 12px",
                fontSize: "0.875rem",
                fontWeight: active ? 700 : 500,
                color: active ? "#0284c7" : "#475569",
                borderBottom: active ? "2px solid #0284c7" : "2px solid transparent",
                textDecoration: "none",
                whiteSpace: "nowrap",
                flexShrink: 0,
                transition: "all 0.15s ease-in-out"
              }}
            >
              {t(tab.labelKey, tab.fallback)}
            </Link>
          );
        })}
      </nav>
    </header>
  );
}

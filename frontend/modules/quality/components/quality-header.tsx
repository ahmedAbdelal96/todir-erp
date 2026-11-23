import React from "react";
import { Link, useLocation } from "react-router-dom";
import { useTranslation } from "react-i18next";

export interface QualityHeaderProps {
  readonly title: string;
  readonly subtitle?: string;
  readonly primaryAction?: React.ReactNode;
  readonly secondaryActions?: React.ReactNode;
  readonly showHoldBanner?: boolean;
}

interface TabLink {
  readonly path: string;
  readonly labelKey: string;
  readonly fallback: string;
}

const TABS: readonly TabLink[] = [
  { path: "/quality", labelKey: "tabs.overview", fallback: "لوحة التحكم والجودة" },
  { path: "/quality/inspections", labelKey: "tabs.inspections", fallback: "فحوصات الجودة" },
  { path: "/quality/plans", labelKey: "tabs.plans", fallback: "خطط ومعايير الجودة" },
  { path: "/quality/restrictions", labelKey: "tabs.restrictions", fallback: "الحظر واحتجاز الجودة" },
  { path: "/quality/dispositions", labelKey: "tabs.dispositions", fallback: "القرارات الفنية (MRB)" },
  { path: "/quality/ncrs", labelKey: "tabs.ncrs", fallback: "حالات عدم المطابقة (NCR)" },
  { path: "/quality/capas", labelKey: "tabs.capas", fallback: "الإجراءات التصحيحية (CAPA)" },
  { path: "/quality/suppliers", labelKey: "tabs.supplierQuality", fallback: "جودة الموردين و(SCAR)" },
  { path: "/quality/certificates", labelKey: "tabs.certificates", fallback: "شهادات التحليل (COA)" }
];

export function QualityHeader({
  title,
  subtitle,
  primaryAction,
  secondaryActions,
  showHoldBanner = true
}: QualityHeaderProps): React.JSX.Element {
  const { t } = useTranslation("quality");
  const location = useLocation();

  const isCurrent = (path: string) => {
    if (path === "/quality") {
      return location.pathname === "/quality" || location.pathname === "/quality/overview";
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
      <div
        style={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "flex-start",
          flexWrap: "wrap",
          gap: "12px",
          marginBottom: "0.75rem"
        }}
      >
        <div>
          <nav style={{ fontSize: "0.8125rem", color: "#64748b", marginBottom: "0.25rem" }}>
            <Link to="/quality" style={{ color: "inherit", textDecoration: "none" }}>
              {t("navigation.quality", "إدارة الجودة الشاملة")}
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

      {showHoldBanner && (
        <div
          data-testid="quality-domain-invariant-banner"
          style={{
            display: "flex",
            alignItems: "center",
            gap: "8px",
            backgroundColor: "#f0fdf4",
            border: "1px solid #bbf7d0",
            borderRadius: "6px",
            padding: "0.4rem 0.75rem",
            marginBottom: "0.75rem",
            fontSize: "0.8125rem",
            color: "#166534"
          }}
        >
          <span style={{ fontWeight: 700 }}>{t("messages.passedNote", "تنبيه تشغيلي صارم: نجاح الفحص لا يعني الإفراج التلقائي عن المخزون؛ يتطلب الإفراج إجراءً صريحاً.")}</span>
        </div>
      )}

      <nav
        style={{
          display: "flex",
          gap: "4px",
          borderTop: "1px solid #f1f5f9",
          paddingTop: "0.5rem",
          paddingBottom: "4px",
          overflowX: "auto",
          WebkitOverflowScrolling: "touch"
        }}
      >
        {TABS.map((tab) => {
          const active = isCurrent(tab.path);
          return (
            <Link
              key={tab.path}
              to={tab.path}
              style={{
                display: "inline-flex",
                alignItems: "center",
                padding: "0.5rem 0.875rem",
                borderRadius: "6px",
                fontSize: "0.875rem",
                fontWeight: active ? 600 : 500,
                color: active ? "#0f172a" : "#64748b",
                backgroundColor: active ? "#f1f5f9" : "transparent",
                textDecoration: "none",
                whiteSpace: "nowrap",
                borderBottom: active ? "2px solid #0284c7" : "2px solid transparent"
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

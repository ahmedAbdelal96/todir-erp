import React from "react";
import { Link } from "react-router-dom";
import { useTranslation } from "react-i18next";

export interface QualityHeaderProps {
  readonly title: string;
  readonly subtitle?: string;
  readonly primaryAction?: React.ReactNode;
  readonly secondaryActions?: React.ReactNode;
  readonly showHoldBanner?: boolean;
}

export function QualityHeader({
  title,
  subtitle,
  primaryAction,
  secondaryActions,
  showHoldBanner = false
}: QualityHeaderProps): React.JSX.Element {
  const { t } = useTranslation("quality");

  return (
    <header
      style={{
        marginBottom: "1.25rem",
        background: "#ffffff",
        border: "1px solid #cbd5e1",
        borderRadius: "8px",
        padding: "1rem 1.25rem",
        boxShadow: "0 1px 3px rgba(0, 0, 0, 0.05)"
      }}
    >
      <div
        style={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "flex-start",
          flexWrap: "wrap",
          gap: "12px"
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
            padding: "0.5rem 0.75rem",
            marginTop: "0.75rem",
            fontSize: "0.8125rem",
            color: "#166534"
          }}
        >
          <span style={{ fontWeight: 700 }}>{t("messages.passedNote", "تنبيه تشغيلي صارم: نجاح الفحص لا يعني الإفراج التلقائي عن المخزون؛ يتطلب الإفراج إجراءً صريحاً.")}</span>
        </div>
      )}
    </header>
  );
}

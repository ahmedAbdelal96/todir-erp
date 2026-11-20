import React from "react";
import { useTranslation } from "react-i18next";
import { useOutletContext, Link } from "react-router-dom";
import type { WorkspaceOutletContext, PermissionCondition } from "../../app/router/route-contract.ts";
import { evaluatePermissionCondition, isModuleOperational } from "../runtime/effective-access.ts";

export interface PermissionGuardProps {
  readonly permission?: PermissionCondition | undefined;
  readonly moduleId?: string | undefined;
  readonly children: React.ReactNode;
  readonly fallback?: React.ReactNode | undefined;
}

export function PermissionGuard({
  permission,
  moduleId,
  children,
  fallback
}: PermissionGuardProps): React.JSX.Element {
  const { t } = useTranslation("common");
  const outletCtx = useOutletContext<WorkspaceOutletContext | undefined>();
  const effectiveAccess = outletCtx?.effectiveAccess;

  // Fail closed if effectiveAccess context is missing
  if (!effectiveAccess) {
    return fallback ? <>{fallback}</> : <DefaultAccessDeniedView />;
  }

  // Enforce module operational readiness (entitlement + lifecycle activation)
  if (moduleId && !isModuleOperational(effectiveAccess, moduleId)) {
    return fallback ? <>{fallback}</> : <DefaultAccessDeniedView />;
  }

  // Enforce specific permission condition
  if (permission && !evaluatePermissionCondition(permission, effectiveAccess)) {
    return fallback ? <>{fallback}</> : <DefaultAccessDeniedView />;
  }

  return <>{children}</>;
}

export function DefaultAccessDeniedView(): React.JSX.Element {
  const { t } = useTranslation("common");

  return (
    <div
      role="alert"
      data-testid="permission-denied-view"
      style={{
        margin: "1.5rem",
        padding: "2.5rem 2rem",
        backgroundColor: "var(--color-surface-card, #ffffff)",
        border: "1px solid var(--color-border-default, #cbd5e1)",
        borderRadius: "var(--radius-lg, 0.75rem)",
        boxShadow: "var(--shadow-sm, 0 1px 3px 0 rgba(15, 23, 42, 0.08))",
        textAlign: "center",
        maxWidth: "36rem",
        marginInline: "auto"
      }}
    >
      <div
        style={{
          display: "inline-flex",
          alignItems: "center",
          justifyContent: "center",
          width: "3.5rem",
          height: "3.5rem",
          borderRadius: "50%",
          backgroundColor: "rgba(239, 68, 68, 0.1)",
          color: "var(--color-status-danger, #ef4444)",
          marginBottom: "1rem"
        }}
      >
        <svg width="28" height="28" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
          <rect x="3" y="11" width="18" height="11" rx="2" ry="2" />
          <path d="M7 11V7a5 5 0 0 1 10 0v4" />
        </svg>
      </div>
      <h2 style={{ fontSize: "1.25rem", fontWeight: 700, color: "var(--color-content-primary, #0f172a)", marginBottom: "0.5rem" }}>
        {t("access.deniedTitle", "غير مصرح لك بالوصول")}
      </h2>
      <p style={{ fontSize: "0.875rem", color: "var(--color-content-muted, #64748b)", margin: "0 0 1.5rem", lineHeight: 1.6 }}>
        {t("access.deniedDescription", "لا تملك الصلاحيات الكافية للوصول إلى هذا المورد في نطاق العمل الحالي.")}
      </p>
      <Link
        to="/"
        style={{
          display: "inline-flex",
          alignItems: "center",
          gap: "0.5rem",
          padding: "0.5rem 1.25rem",
          borderRadius: "var(--radius-md, 0.5rem)",
          backgroundColor: "var(--color-brand-primary, #102a43)",
          color: "#ffffff",
          textDecoration: "none",
          fontWeight: 600,
          fontSize: "0.875rem"
        }}
      >
        {t("actions.back", "العودة للرئيسية")}
      </Link>
    </div>
  );
}

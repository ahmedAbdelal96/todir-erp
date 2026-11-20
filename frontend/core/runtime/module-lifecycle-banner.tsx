import React from "react";
import { useTranslation } from "react-i18next";
import { useOutletContext } from "react-router-dom";
import type { WorkspaceOutletContext } from "../../app/router/route-contract.ts";
import { isModuleReadOnly } from "./effective-access.ts";

export interface ModuleLifecycleBannerProps {
  readonly moduleId: string;
}

export function ModuleLifecycleBanner({ moduleId }: ModuleLifecycleBannerProps): React.JSX.Element | null {
  const { t } = useTranslation("common");
  const outletCtx = useOutletContext<WorkspaceOutletContext | undefined>();
  const effectiveAccess = outletCtx?.effectiveAccess;

  if (!isModuleReadOnly(effectiveAccess, moduleId)) {
    return null;
  }

  return (
    <div
      role="status"
      data-testid="module-readonly-banner"
      style={{
        display: "flex",
        alignItems: "center",
        gap: "0.75rem",
        padding: "0.875rem 1.25rem",
        backgroundColor: "#fffbeb",
        color: "#92400e",
        border: "1px solid #fde68a",
        borderRadius: "var(--radius-md, 0.5rem)",
        marginBottom: "1rem",
        fontSize: "0.875rem",
        fontWeight: 500
      }}
    >
      <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true" style={{ flexShrink: 0 }}>
        <circle cx="12" cy="12" r="10" />
        <line x1="12" y1="8" x2="12" y2="12" />
        <line x1="12" y1="16" x2="12.01" y2="16" />
      </svg>
      <span>
        {t("lifecycle.readOnlyNotice", "الوحدة في وضع القراءة فقط / التصفية. العمليات وعمليات الإنشاء والتعديل الجديدة معطلة.")}
      </span>
    </div>
  );
}

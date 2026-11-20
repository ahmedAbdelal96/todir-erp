import React from "react";
import { useTranslation } from "react-i18next";
import { Link, useOutletContext } from "react-router-dom";
import type { WorkspaceOutletContext } from "../../../app/router/route-contract.ts";
import { useInventoryScope } from "../hooks/use-inventory-queries.ts";
import { TraceabilityTab } from "../features/traceability/traceability-tab.tsx";

export function InventoryTraceabilityPage(): React.JSX.Element {
  const { t } = useTranslation("inventory");
  const outletCtx = useOutletContext<WorkspaceOutletContext | undefined>();
  const { legalEntityId } = useInventoryScope({ legalEntityId: outletCtx?.legalEntityId });

  return (
    <div
      data-testid="inventory-traceability-page"
      style={{
        padding: "1.25rem 1.5rem",
        width: "100%",
        maxWidth: "1600px",
        margin: "0 auto",
        boxSizing: "border-box",
        minWidth: 0
      }}
    >
      <header
        style={{
          marginBottom: "1.25rem",
          paddingBottom: "1rem",
          borderBottom: "1px solid var(--color-border-default)"
        }}
      >
        <nav  style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)", marginBottom: "0.25rem" }}>
          <Link to="/inventory" style={{ color: "inherit", textDecoration: "none" }}>
            {t("title", "إدارة المخزون")}
          </Link>
          {" / "}
          <span style={{ color: "var(--color-content-primary)", fontWeight: 600 }}>
            {t("tabs.traceability", "تتبع التشغيلات والأرقام التسلسلية")}
          </span>
        </nav>
        <h1 style={{ margin: "0 0 0.25rem", fontSize: "1.5rem", color: "var(--color-brand-primary, #102a43)", fontWeight: 700 }}>
          {t("tabs.traceability", "تتبع التشغيلات والأرقام التسلسلية")}
        </h1>
        <p style={{ margin: 0, fontSize: "0.875rem", color: "var(--color-content-muted)" }}>
          {t("traceability.subtitle", "تتبع حركة أرقام التشغيلات واللوت وتواريخ الصلاحية والأرقام التسلسلية للأصناف")}
        </p>
      </header>

      <main>
        <TraceabilityTab legalEntityId={legalEntityId} />
      </main>
    </div>
  );
}

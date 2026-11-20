import React from "react";
import { useTranslation } from "react-i18next";
import { Link, useOutletContext } from "react-router-dom";
import type { WorkspaceOutletContext } from "../../../app/router/route-contract.ts";
import { useInventoryScope, useWarehouses } from "../hooks/use-inventory-queries.ts";
import { HistoryTab } from "../features/history/history-tab.tsx";

export function InventoryMovementsPage(): React.JSX.Element {
  const { t } = useTranslation("inventory");
  const outletCtx = useOutletContext<WorkspaceOutletContext | undefined>();
  const { legalEntityId } = useInventoryScope({ legalEntityId: outletCtx?.legalEntityId });
  const { data: warehouses = [] } = useWarehouses(legalEntityId);

  return (
    <div
      data-testid="inventory-movements-page"
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
        <nav style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)", marginBottom: "0.25rem" }}>
          <Link to="/inventory" style={{ color: "inherit", textDecoration: "none" }}>
            {t("title", "إدارة المخزون")}
          </Link>
          {" / "}
          <span style={{ color: "var(--color-content-primary)", fontWeight: 600 }}>
            {t("tabs.history", "سجل حركة الأصناف المخزنية")}
          </span>
        </nav>
        <h1 style={{ margin: "0 0 0.25rem", fontSize: "1.5rem", color: "var(--color-brand-primary, #102a43)", fontWeight: 700 }}>
          {t("tabs.history", "سجل حركة الأصناف المخزنية")}
        </h1>
        <p style={{ margin: 0, fontSize: "0.875rem", color: "var(--color-content-muted)" }}>
          {t("history.subtitle", "تتبع حركات الوارد والمنصرف والتحويل والتسوية لكل صنف مع أرقام الأذون والمستندات المرجعية")}
        </p>
      </header>

      <main>
        <HistoryTab legalEntityId={legalEntityId} warehouses={warehouses} />
      </main>
    </div>
  );
}

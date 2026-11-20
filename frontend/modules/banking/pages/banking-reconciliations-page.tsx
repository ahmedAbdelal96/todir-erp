import React from "react";
import { useTranslation } from "react-i18next";
import { Link, useOutletContext } from "react-router-dom";
import { useOptionalLegalEntityContext } from "../../../core/runtime/runtime-provider.tsx";
import type { WorkspaceOutletContext } from "../../../app/router/route-contract.ts";
import { ReconciliationTab } from "../features/reconciliation/reconciliation-tab.tsx";

export function BankingReconciliationsPage(): React.JSX.Element {
  const { t } = useTranslation("banking");
  const outletCtx = useOutletContext<WorkspaceOutletContext | undefined>();
  const runtimeContext = useOptionalLegalEntityContext();
  const legalEntityId = outletCtx?.legalEntityId || runtimeContext?.legalEntityId || "";

  return (
    <div
      data-testid="banking-reconciliations-page"
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
          <Link to="/banking" style={{ color: "inherit", textDecoration: "none" }}>
            {t("tabs.overview", "الخزينة والبنوك")}
          </Link>
          {" / "}
          <span style={{ color: "var(--color-content-primary)", fontWeight: 600 }}>
            {t("tabs.reconciliation", "مطابقة كشف الحساب البنكي")}
          </span>
        </nav>
        <h1 style={{ margin: "0 0 0.25rem", fontSize: "1.5rem", color: "var(--color-brand-primary, #102a43)", fontWeight: 700 }}>
          {t("tabs.reconciliation", "مطابقة كشف الحساب البنكي")}
        </h1>
        <p style={{ margin: 0, fontSize: "0.875rem", color: "var(--color-content-muted)" }}>
          {t("reconciliation.subtitle", "مطابقة السجلات الدفترية مع كشوف الحسابات البنكية واعتماد التسويات")}
        </p>
      </header>

      <main>
        <ReconciliationTab legalEntityId={legalEntityId} />
      </main>
    </div>
  );
}

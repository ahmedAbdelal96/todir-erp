import React from "react";
import { useTranslation } from "react-i18next";
import { Link, useOutletContext } from "react-router-dom";
import type { WorkspaceOutletContext } from "../../../app/router/route-contract.ts";
import { useLedgerScope } from "../hooks/use-ledger-queries.ts";
import { FinancialStatementsView } from "../features/statements/financial-statements-view.tsx";

export function AccountingStatementsPage(): React.JSX.Element {
  const { t } = useTranslation("accounting");
  const outletCtx = useOutletContext<WorkspaceOutletContext | undefined>();
  const { legalEntityId } = useLedgerScope({ legalEntityId: outletCtx?.legalEntityId });

  return (
    <div
      data-testid="accounting-statements-page"
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
          <Link to="/accounting" style={{ color: "inherit", textDecoration: "none" }}>
            {t("title", "المحاسبة العامة")}
          </Link>
          {" / "}
          <span style={{ color: "var(--color-content-primary)", fontWeight: 600 }}>
            {t("tabs.financialStatements", "القوائم المالية والختامية")}
          </span>
        </nav>
        <h1 style={{ margin: "0 0 0.25rem", fontSize: "1.5rem", color: "var(--color-brand-primary, #102a43)", fontWeight: 700 }}>
          {t("tabs.financialStatements", "القوائم المالية والختامية")}
        </h1>
        <p style={{ margin: 0, fontSize: "0.875rem", color: "var(--color-content-muted)" }}>
          {t("financialStatements.subtitle", "قائمة المركز المالي، قائمة الدخل، وقائمة التدفقات النقدية وفق المعايير المحاسبية")}
        </p>
      </header>

      <main>
        <FinancialStatementsView legalEntityId={legalEntityId} />
      </main>
    </div>
  );
}

import React from "react";
import { useTranslation } from "react-i18next";
import { Link, useOutletContext, useSearchParams } from "react-router-dom";
import type { WorkspaceOutletContext } from "../../../app/router/route-contract.ts";
import { useLedgerScope } from "../hooks/use-ledger-queries.ts";
import { GeneralLedgerView } from "../features/reports/general-ledger-view.tsx";

export function AccountingLedgerPage(): React.JSX.Element {
  const { t } = useTranslation("accounting");
  const outletCtx = useOutletContext<WorkspaceOutletContext | undefined>();
  const { legalEntityId, baseCurrency } = useLedgerScope({ legalEntityId: outletCtx?.legalEntityId });
  const [searchParams] = useSearchParams();
  const initialAccountCode = searchParams.get("accountCode") || undefined;

  return (
    <div
      data-testid="accounting-ledger-page"
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
          <Link to="/accounting" style={{ color: "inherit", textDecoration: "none" }}>
            {t("title", "المحاسبة العامة")}
          </Link>
          {" / "}
          <span style={{ color: "var(--color-content-primary)", fontWeight: 600 }}>
            {t("tabs.generalLedger", "دفتر الأستاذ العام")}
          </span>
        </nav>
        <h1 style={{ margin: "0 0 0.25rem", fontSize: "1.5rem", color: "var(--color-brand-primary, #102a43)", fontWeight: 700 }}>
          {t("tabs.generalLedger", "دفتر الأستاذ العام")}
        </h1>
        <p style={{ margin: 0, fontSize: "0.875rem", color: "var(--color-content-muted)" }}>
          {t("generalLedger.subtitle", "كشف تفصيلي لحركات الحسابات المحاسبية وأرصدتها الافتتاحية والختامية")}
        </p>
      </header>

      <main>
        <GeneralLedgerView
          legalEntityId={legalEntityId}
          baseCurrency={baseCurrency}
          initialAccountCode={initialAccountCode}
        />
      </main>
    </div>
  );
}

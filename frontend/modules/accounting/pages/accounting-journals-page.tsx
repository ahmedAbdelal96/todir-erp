import React from "react";
import { useTranslation } from "react-i18next";
import { Link, useOutletContext } from "react-router-dom";
import type { WorkspaceOutletContext } from "../../../app/router/route-contract.ts";
import { useLedgerScope, useLedgerPermissions, useJournals } from "../hooks/use-ledger-queries.ts";
import { JournalsView } from "../features/journals/journals-view.tsx";

export function AccountingJournalsPage(): React.JSX.Element {
  const { t } = useTranslation("accounting");
  const outletCtx = useOutletContext<WorkspaceOutletContext | undefined>();
  const { legalEntityId } = useLedgerScope({ legalEntityId: outletCtx?.legalEntityId });
  const permissions = useLedgerPermissions();
  const { data: journals = [], isLoading: isJournalsLoading } = useJournals(legalEntityId);

  return (
    <div
      data-testid="accounting-journals-page"
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
            {t("tabs.journals", "قيود اليومية والترحيل")}
          </span>
        </nav>
        <h1 style={{ margin: "0 0 0.25rem", fontSize: "1.5rem", color: "var(--color-brand-primary, #102a43)", fontWeight: 700 }}>
          {t("tabs.journals", "قيود اليومية والترحيل")}
        </h1>
        <p style={{ margin: 0, fontSize: "0.875rem", color: "var(--color-content-muted)" }}>
          {t("journals.subtitle", "تسجيل ومراجعة قيود اليومية المحاسبية وترحيلها إلى دفاتر الأستاذ")}
        </p>
      </header>

      <main>
        <JournalsView
          legalEntityId={legalEntityId}
          journals={journals}
          isLoading={isJournalsLoading}
          canManageJournals={permissions.canManageJournals}
        />
      </main>
    </div>
  );
}

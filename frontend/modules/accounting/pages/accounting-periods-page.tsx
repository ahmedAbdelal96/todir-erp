import React from "react";
import { useTranslation } from "react-i18next";
import { Link, useOutletContext } from "react-router-dom";
import type { WorkspaceOutletContext } from "../../../app/router/route-contract.ts";
import {
  useLedgerScope,
  useLedgerPermissions,
  useFiscalYears,
  usePeriods,
  useLocks
} from "../hooks/use-ledger-queries.ts";
import { PeriodsView } from "../features/periods/periods-view.tsx";
import { FinancialLocksView } from "../features/periods/financial-locks-view.tsx";

export function AccountingPeriodsPage(): React.JSX.Element {
  const { t } = useTranslation("accounting");
  const outletCtx = useOutletContext<WorkspaceOutletContext | undefined>();
  const { legalEntityId } = useLedgerScope({ legalEntityId: outletCtx?.legalEntityId });
  const permissions = useLedgerPermissions();

  const { data: fiscalYears = [] } = useFiscalYears(legalEntityId);
  const { data: periods = [], isLoading: isPeriodsLoading } = usePeriods(legalEntityId);
  const { data: locks = [], isLoading: isLocksLoading } = useLocks(legalEntityId);

  return (
    <div
      data-testid="accounting-periods-page"
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
            {t("tabs.periods", "الفترات المالية والإقفال")}
          </span>
        </nav>
        <h1 style={{ margin: "0 0 0.25rem", fontSize: "1.5rem", color: "var(--color-brand-primary, #102a43)", fontWeight: 700 }}>
          {t("tabs.periods", "الفترات المالية والإقفال")}
        </h1>
        <p style={{ margin: 0, fontSize: "0.875rem", color: "var(--color-content-muted)" }}>
          {t("periods.subtitle", "إدارة السنوات والفترات المالية والإقفال المرحلي والنهائي وتثبيت الأقفال")}
        </p>
      </header>

      <main style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
        <PeriodsView
          legalEntityId={legalEntityId}
          fiscalYears={fiscalYears}
          periods={periods}
          isLoading={isPeriodsLoading}
          canManagePeriods={permissions.canManagePeriods}
          canSoftClose={permissions.canSoftClosePeriod}
          canClose={permissions.canClosePeriod}
          canReopen={permissions.canReopenPeriod}
        />

        <FinancialLocksView
          legalEntityId={legalEntityId}
          locks={locks}
          isLoading={isLocksLoading}
          canManageLocks={permissions.canManageLocks}
        />
      </main>
    </div>
  );
}

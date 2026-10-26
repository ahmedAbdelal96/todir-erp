import React from "react";
import { useTranslation } from "react-i18next";
import type { ModuleRouteRegistration } from "../../app/router/route-contract.ts";
import { SubledgerLandingPage } from "./pages/subledger-landing-page.tsx";
import { OpenItemsPage } from "./pages/open-items-page.tsx";
import { PartyFinancePage } from "./pages/party-finance-page.tsx";
import { AgingReportView } from "./features/aging-report-view.tsx";
import { PartyStatementView } from "./features/party-statement-view.tsx";
import { ControlReconciliationView } from "./features/control-reconciliation-view.tsx";
import { OpeningMigrationView } from "./features/opening-migration-view.tsx";
import { useSubledgerScope } from "./hooks/subledger-queries.ts";

function AgingRouteWrapper(): React.JSX.Element {
  const { t } = useTranslation("subledger");
  const { legalEntityId } = useSubledgerScope();
  return (
    <div className="space-y-6">
      <div className="pb-3 border-b border-slate-200">
        <h2 className="text-base font-bold text-slate-900">{t("routes.agingTitle")}</h2>
        <p className="text-xs text-slate-500 mt-0.5">{t("routes.agingSubtitle")}</p>
      </div>
      <AgingReportView legalEntityId={legalEntityId} partyId="11111111-1111-1111-1111-111111111111" />
    </div>
  );
}

function StatementRouteWrapper(): React.JSX.Element {
  const { t } = useTranslation("subledger");
  const { legalEntityId } = useSubledgerScope();
  return (
    <div className="space-y-6">
      <div className="pb-3 border-b border-slate-200">
        <h2 className="text-base font-bold text-slate-900">{t("routes.statementTitle")}</h2>
        <p className="text-xs text-slate-500 mt-0.5">{t("routes.statementSubtitle")}</p>
      </div>
      <PartyStatementView legalEntityId={legalEntityId} partyId="11111111-1111-1111-1111-111111111111" />
    </div>
  );
}

function ReconciliationRouteWrapper(): React.JSX.Element {
  const { t } = useTranslation("subledger");
  const { legalEntityId } = useSubledgerScope();
  return (
    <div className="space-y-6">
      <div className="pb-3 border-b border-slate-200">
        <h2 className="text-base font-bold text-slate-900">{t("routes.reconciliationTitle")}</h2>
        <p className="text-xs text-slate-500 mt-0.5">{t("routes.reconciliationSubtitle")}</p>
      </div>
      <ControlReconciliationView legalEntityId={legalEntityId} />
    </div>
  );
}

function OpeningRouteWrapper(): React.JSX.Element {
  const { legalEntityId } = useSubledgerScope();
  return (
    <OpeningMigrationView legalEntityId={legalEntityId} />
  );
}

export const subledgerRoutes: ModuleRouteRegistration = {
  navigation: {
    id: "subledger",
    moduleIds: ["subledger"],
    path: "/subledger",
    navigationKey: "subledger",
    permission: "subledger.openitems.read",
    icon: "subledger"
  },
  routes: [
    {
      path: "subledger",
      element: <SubledgerLandingPage />
    },
    {
      path: "subledger/open-items",
      element: <OpenItemsPage />
    },
    {
      path: "subledger/applications",
      element: <PartyFinancePage />
    },
    {
      path: "subledger/parties",
      element: <PartyFinancePage />
    },
    {
      path: "subledger/parties/:partyId",
      element: <PartyFinancePage />
    },
    {
      path: "subledger/aging",
      element: <AgingRouteWrapper />
    },
    {
      path: "subledger/statement",
      element: <StatementRouteWrapper />
    },
    {
      path: "subledger/reconciliation",
      element: <ReconciliationRouteWrapper />
    },
    {
      path: "subledger/opening",
      element: <OpeningRouteWrapper />
    }
  ]
};

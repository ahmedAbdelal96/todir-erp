import React from "react";
import { useTranslation } from "react-i18next";
import { Link, useOutletContext } from "react-router-dom";
import { useOptionalLegalEntityContext } from "../../../core/runtime/runtime-provider.tsx";
import type { WorkspaceOutletContext } from "../../../app/router/route-contract.ts";
import { CashForecastTab } from "../features/forecast/cash-forecast-tab.tsx";

export function BankingForecastPage(): React.JSX.Element {
  const { t } = useTranslation("banking");
  const outletCtx = useOutletContext<WorkspaceOutletContext | undefined>();
  const runtimeContext = useOptionalLegalEntityContext();
  const legalEntityId = outletCtx?.legalEntityId || runtimeContext?.legalEntityId || "";

  return (
    <div
      data-testid="banking-forecast-page"
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
            {t("tabs.forecast", "توقعات السيولة النقدية")}
          </span>
        </nav>
        <h1 style={{ margin: "0 0 0.25rem", fontSize: "1.5rem", color: "var(--color-brand-primary, #102a43)", fontWeight: 700 }}>
          {t("tabs.forecast", "توقعات السيولة النقدية")}
        </h1>
        <p style={{ margin: 0, fontSize: "0.875rem", color: "var(--color-content-muted)" }}>
          {t("forecast.subtitle", "تحليل التدفقات النقدية المتوقعة ومراقبة الفجوات التمويلية المستقبلية")}
        </p>
      </header>

      <main>
        <CashForecastTab legalEntityId={legalEntityId} />
      </main>
    </div>
  );
}

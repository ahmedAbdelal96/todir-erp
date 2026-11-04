import { createBrowserRouter, isRouteErrorResponse, Link, Outlet, useRouteError, type RouteObject } from "react-router-dom";
import { AppShell } from "../shell/app-shell.tsx";
import { PreviewPage } from "../preview-page.tsx";
import { WorkspaceHomePage } from "../workspace-home-page.tsx";
import { accountingRoutes } from "@modules/accounting/public.ts";
import { catalogRoutes } from "@modules/catalog/public.ts";
import { directoryRoutes } from "@modules/directory/public.ts";
import { finbaseRoutes } from "@modules/finbase/public.ts";
import { pricingRoutes } from "@modules/pricing/public.ts";
import { subledgerRoutes } from "@modules/subledger/public.ts";
import { taxRoutes } from "@modules/tax/public.ts";
import { inventoryRoutes } from "@modules/inventory/public.ts";
import { salesRoutes } from "@modules/sales/public.ts";
import { purchasingRoutes } from "@modules/purchasing/public.ts";
import { hrRoutes } from "@modules/hr/public.ts";
import { payrollRoutes } from "@modules/payroll/public.ts";
import { identityRoutes } from "@modules/identity/public.ts";
import { invoicingRoutes } from "@modules/invoicing/public.ts";
import { migrationRoutes } from "@modules/migration/public.ts";
import { paymentsRoutes } from "@modules/payments/public.ts";
import { useTranslation } from "react-i18next";

function SettingsRoute(): React.JSX.Element {
  const { t } = useTranslation("navigation");
  return <div style={{ padding: "1.5rem" }}><h1>{t("settings")}</h1><p>{t("settingsDescription")}</p></div>;
}

function NotFoundRoute(): React.JSX.Element {
  const { t } = useTranslation("navigation");
  return (
    <div style={{ padding: "3rem", textAlign: "center" }}>
      <h1>{t("notFound.title")}</h1>
      <p>{t("notFound.description")}</p>
      <Link to="/">{t("notFound.backHome")}</Link>
    </div>
  );
}

function RouteErrorView(): React.JSX.Element {
  const error = useRouteError();
  const { t } = useTranslation("navigation");
  const status = isRouteErrorResponse(error) ? error.status : undefined;
  const title = status === 404 ? t("notFound.title") : t("routeError.title");
  const message = status === 404 ? t("notFound.description") : t("routeError.description");

  return (
    <main style={{ minHeight: "100vh", display: "grid", placeItems: "center", padding: "2rem", background: "var(--color-surface-page)" }}>
      <section role="alert" style={{ maxWidth: "34rem", textAlign: "center", padding: "2rem", border: "1px solid var(--color-border-default)", borderRadius: "var(--radius-lg)", background: "var(--color-surface-card)" }}>
        <h1 style={{ margin: "0 0 0.75rem", color: "var(--color-status-danger)" }}>{title}</h1>
        <p style={{ margin: "0 0 1.5rem", color: "var(--color-content-muted)" }}>{message}</p>
        <div style={{ display: "flex", justifyContent: "center", gap: "0.75rem", flexWrap: "wrap" }}>
          <button type="button" onClick={() => window.location.reload()} style={{ padding: "0.5rem 1rem", borderRadius: "var(--radius-sm)", border: "1px solid var(--color-border-default)", cursor: "pointer" }}>{t("routeError.retry")}</button>
          <Link to="/" style={{ padding: "0.5rem 1rem", borderRadius: "var(--radius-sm)", background: "var(--color-brand-primary)", color: "white", textDecoration: "none" }}>{t("routeError.backHome")}</Link>
        </div>
      </section>
    </main>
  );
}

const workspaceRoutes: RouteObject = {
  path: "/",
  element: <AppShell />,
  errorElement: <RouteErrorView />,
  children: [
    { index: true, element: <WorkspaceHomePage /> },
    ...identityRoutes.routes,
    ...directoryRoutes.routes,
    ...catalogRoutes.routes,
    ...invoicingRoutes.routes,
    ...paymentsRoutes.routes,
    ...accountingRoutes.routes,
    ...migrationRoutes.routes,
    ...finbaseRoutes.routes,
    ...pricingRoutes.routes,
    ...subledgerRoutes.routes,
    ...taxRoutes.routes,
    ...inventoryRoutes.routes,
    ...salesRoutes.routes,
    ...purchasingRoutes.routes,
    ...hrRoutes.routes,
    ...payrollRoutes.routes,
    { path: "preview", element: <PreviewPage /> },
    { path: "settings", element: <SettingsRoute /> },
    { path: "*", element: <NotFoundRoute /> }
  ]
};

const publicRoutes: RouteObject[] = [
  ...(paymentsRoutes.publicRoutes ?? []).map((route) => ({
    ...route,
    errorElement: <RouteErrorView />
  }))
];

export const appRouter = createBrowserRouter([
  workspaceRoutes,
  ...publicRoutes
]);

export function RouterOutlet(): React.JSX.Element {
  return <Outlet />;
}

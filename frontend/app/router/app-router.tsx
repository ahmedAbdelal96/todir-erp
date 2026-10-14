import { createBrowserRouter, isRouteErrorResponse, Link, Outlet, useRouteError, type RouteObject } from "react-router-dom";
import { AppShell } from "../shell/app-shell.tsx";
import { PreviewPage } from "../preview-page.tsx";
import { WorkspaceHomePage } from "../workspace-home-page.tsx";
import { accountingRoutes } from "@modules/accounting/public.ts";
import { catalogRoutes } from "@modules/catalog/public.ts";
import { directoryRoutes } from "@modules/directory/public.ts";
import { finbaseRoutes } from "@modules/finbase/public.ts";
import { identityRoutes } from "@modules/identity/public.ts";
import { invoicingRoutes } from "@modules/invoicing/public.ts";
import { migrationRoutes } from "@modules/migration/public.ts";
import { paymentsRoutes } from "@modules/payments/public.ts";

function SettingsRoute(): React.JSX.Element {
  return <div style={{ padding: "1.5rem" }}><h1>الإعدادات</h1><p>اختر إعدادًا من الوحدات المتاحة.</p></div>;
}

function NotFoundRoute(): React.JSX.Element {
  return (
    <div style={{ padding: "3rem", textAlign: "center" }}>
      <h1>الصفحة غير موجودة</h1>
      <p>تحقق من الرابط أو عد إلى الصفحة الرئيسية.</p>
      <Link to="/">العودة للرئيسية</Link>
    </div>
  );
}

function RouteErrorView(): React.JSX.Element {
  const error = useRouteError();
  const status = isRouteErrorResponse(error) ? error.status : undefined;
  const title = status === 404 ? "الصفحة غير موجودة" : "تعذر فتح الصفحة";
  const message = status === 404
    ? "تحقق من الرابط أو عد إلى الصفحة الرئيسية."
    : "حدث خلل مؤقت أثناء فتح هذه الصفحة. يمكنك إعادة المحاولة أو العودة للرئيسية.";

  return (
    <main dir="rtl" style={{ minHeight: "100vh", display: "grid", placeItems: "center", padding: "2rem", background: "var(--color-surface-page)" }}>
      <section role="alert" style={{ maxWidth: "34rem", textAlign: "center", padding: "2rem", border: "1px solid var(--color-border-default)", borderRadius: "var(--radius-lg)", background: "var(--color-surface-card)" }}>
        <h1 style={{ margin: "0 0 0.75rem", color: "var(--color-status-danger)" }}>{title}</h1>
        <p style={{ margin: "0 0 1.5rem", color: "var(--color-content-muted)" }}>{message}</p>
        <div style={{ display: "flex", justifyContent: "center", gap: "0.75rem", flexWrap: "wrap" }}>
          <button type="button" onClick={() => window.location.reload()} style={{ padding: "0.5rem 1rem", borderRadius: "var(--radius-sm)", border: "1px solid var(--color-border-default)", cursor: "pointer" }}>إعادة المحاولة</button>
          <Link to="/" style={{ padding: "0.5rem 1rem", borderRadius: "var(--radius-sm)", background: "var(--color-brand-primary)", color: "white", textDecoration: "none" }}>العودة للرئيسية</Link>
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
    { path: "preview", element: <PreviewPage /> },
    { path: "settings", element: <SettingsRoute /> },
    { path: "*", element: <NotFoundRoute /> }
  ]
};

export const appRouter = createBrowserRouter([workspaceRoutes]);

export function RouterOutlet(): React.JSX.Element {
  return <Outlet />;
}

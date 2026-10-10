import { createBrowserRouter, Link, Outlet, type RouteObject } from "react-router-dom";
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

const workspaceRoutes: RouteObject = {
  path: "/",
  element: <AppShell />,
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

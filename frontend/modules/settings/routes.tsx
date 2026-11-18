import React, { lazy, Suspense } from "react";
import type { ModuleRouteRegistration } from "../../app/router/route-contract.ts";
import { Spinner } from "../../components/primitives/spinner.tsx";

const CompanySettingsPage = lazy(() => import("./pages/company-settings-page.tsx").then((module) => ({ default: module.CompanySettingsPage })));

function SettingsRoute(): React.JSX.Element {
  return (
    <Suspense fallback={<div style={{ display: "grid", placeItems: "center", minHeight: "16rem" }}><Spinner size="medium" /></div>}>
      <CompanySettingsPage />
    </Suspense>
  );
}

export const settingsRoutes: ModuleRouteRegistration = {
  navigation: {
    id: "settings",
    moduleIds: ["settings"],
    path: "/settings",
    navigationKey: "settings",
    permission: "platform.settings.read",
    icon: "settings"
  },
  routes: [
    {
      path: "settings",
      element: <SettingsRoute />
    }
  ]
};

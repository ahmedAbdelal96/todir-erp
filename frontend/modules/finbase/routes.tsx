import { lazy } from "react";
import type { ModuleRouteRegistration } from "@app/router/route-contract.ts";

const FinBasePage = lazy(() => import("./pages/finbase-page.tsx").then(module => ({ default: module.FinBasePage })));

export const finbaseRoutes: ModuleRouteRegistration = {
  navigation: {
    id: "finbase",
    moduleIds: ["finbase"],
    path: "/finbase",
    navigationKey: "financeBase",
    permission: "finbase.currency.read",
    icon: "finbase"
  },
  routes: [{ path: "finbase", element: <FinBasePage /> }]
};

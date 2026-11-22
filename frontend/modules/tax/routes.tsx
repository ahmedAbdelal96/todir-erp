import React from "react";
import type { ModuleRouteRegistration } from "../../app/router/route-contract.ts";
import { lazyNamed } from "../../app/router/lazy-route-component.ts";
const TaxPage = lazyNamed(() => import("./pages/tax-page.tsx"), "TaxPage");

export const taxRoutes: ModuleRouteRegistration = {
  navigation: {
    id: "tax",
    moduleIds: ["tax"],
    path: "/tax",
    navigationKey: "tax",
    permission: "tax.read",
    icon: "tax"
  },
  routes: [
    {
      path: "tax",
      element: <TaxPage />
    }
  ]
};

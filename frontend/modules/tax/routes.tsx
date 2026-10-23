import React from "react";
import type { ModuleRouteRegistration } from "../../app/router/route-contract.ts";
import { TaxPage } from "./pages/tax-page.tsx";

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

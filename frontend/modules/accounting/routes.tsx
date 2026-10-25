import React from "react";
import type { ModuleRouteRegistration } from "../../app/router/route-contract.ts";
import { AccountingPage } from "./pages/accounting-page.tsx";

export const accountingRoutes: ModuleRouteRegistration = {
  navigation: {
    id: "accounting",
    moduleIds: ["accounting", "ledger"],
    path: "/accounting",
    navigationKey: "accounting",
    permission: "ledger.account.read",
    icon: "accounting"
  },
  routes: [
    {
      path: "accounting",
      element: <AccountingPage />
    }
  ]
};

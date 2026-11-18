import React from "react";
import type { ModuleRouteRegistration } from "../../app/router/route-contract.ts";
import { BankingPage } from "./pages/banking-page.tsx";

export const bankingRoutes: ModuleRouteRegistration = {
  navigation: {
    id: "banking",
    moduleIds: ["banking"],
    path: "/banking",
    navigationKey: "banking",
    permission: "banking.accounts.read",
    icon: "banking"
  },
  routes: [
    {
      path: "banking",
      element: <BankingPage />
    }
  ]
};

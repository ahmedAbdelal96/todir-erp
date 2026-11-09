import React from "react";
import type { ModuleRouteRegistration } from "../../app/router/route-contract.ts";
import { ExpensesPage } from "./pages/expenses-page.tsx";

export const expensesRoutes: ModuleRouteRegistration = {
  navigation: {
    id: "expenses",
    moduleIds: ["expenses"],
    path: "/expenses",
    navigationKey: "expenses",
    permission: "expenses.read",
    icon: "expenses"
  },
  routes: [
    {
      path: "expenses",
      element: <ExpensesPage />
    }
  ]
};

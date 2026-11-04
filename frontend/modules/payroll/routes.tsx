import React from "react";
import type { ModuleRouteRegistration } from "../../app/router/route-contract.ts";
import { PayrollPage } from "./pages/payroll-page.tsx";

export const payrollRoutes: ModuleRouteRegistration = {
  navigation: {
    id: "payroll",
    moduleIds: ["payroll"],
    path: "/payroll",
    navigationKey: "payroll",
    permission: "payroll.run.read",
    icon: "banknotes"
  },
  routes: [
    {
      path: "payroll",
      element: <PayrollPage />
    }
  ]
};

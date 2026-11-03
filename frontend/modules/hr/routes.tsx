import React from "react";
import type { ModuleRouteRegistration } from "../../app/router/route-contract.ts";
import { HrPage } from "./pages/hr-page.tsx";

export const hrRoutes: ModuleRouteRegistration = {
  navigation: {
    id: "hr",
    moduleIds: ["hr"],
    path: "/hr",
    navigationKey: "hr",
    permission: "hr.employee.read_basic",
    icon: "users"
  },
  routes: [
    {
      path: "hr",
      element: <HrPage />
    }
  ]
};

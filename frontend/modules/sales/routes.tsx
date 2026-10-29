import React from "react";
import type { ModuleRouteRegistration } from "../../app/router/route-contract.ts";
import { SalesPage } from "./pages/sales-page.tsx";

export const salesRoutes: ModuleRouteRegistration = {
  navigation: {
    id: "sales",
    moduleIds: ["sales"],
    path: "/sales",
    navigationKey: "sales",
    permission: "sales.quotation.read",
    icon: "catalog"
  },
  routes: [
    {
      path: "sales",
      element: <SalesPage />
    }
  ]
};

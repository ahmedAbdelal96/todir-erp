import React from "react";
import type { ModuleRouteRegistration } from "../../app/router/route-contract.ts";
import { PurchasingPage } from "./pages/purchasing-page.tsx";

export const purchasingRoutes: ModuleRouteRegistration = {
  navigation: {
    id: "purchasing",
    moduleIds: ["purchasing"],
    path: "/purchasing",
    navigationKey: "purchasing",
    permission: "purchasing.po.read",
    icon: "catalog"
  },
  routes: [
    {
      path: "purchasing",
      element: <PurchasingPage />
    }
  ]
};

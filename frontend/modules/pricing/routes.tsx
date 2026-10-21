import React from "react";
import type { ModuleRouteRegistration } from "../../app/router/route-contract.ts";
import { PricingPage } from "./pages/pricing-page.tsx";

export const pricingRoutes: ModuleRouteRegistration = {
  navigation: {
    id: "pricing",
    moduleIds: ["pricing"],
    path: "/pricing",
    navigationKey: "pricing",
    permission: "pricing.read",
    icon: "pricing"
  },
  routes: [
    {
      path: "pricing",
      element: <PricingPage />
    }
  ]
};

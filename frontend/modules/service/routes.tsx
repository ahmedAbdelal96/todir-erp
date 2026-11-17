import React from "react";
import type { ModuleRouteRegistration } from "../../app/router/route-contract.ts";
import { ServicePage } from "./pages/service-page.tsx";

export const serviceRoutes: ModuleRouteRegistration = {
  navigation: {
    id: "service",
    moduleIds: ["service"],
    path: "/service",
    navigationKey: "service",
    permission: "service.work.read",
    icon: "service"
  },
  routes: [
    {
      path: "service",
      element: <ServicePage />
    }
  ]
};

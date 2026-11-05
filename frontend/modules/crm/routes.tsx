import React from "react";
import type { ModuleRouteRegistration } from "../../app/router/route-contract.ts";
import { CrmPage } from "./pages/crm-page.tsx";

export const crmRoutes: ModuleRouteRegistration = {
  navigation: {
    id: "crm",
    moduleIds: ["crm"],
    path: "/crm",
    navigationKey: "crm",
    permission: "crm.lead.read",
    icon: "crm"
  },
  routes: [
    {
      path: "crm",
      element: <CrmPage />
    }
  ]
};

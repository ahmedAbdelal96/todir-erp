import React from "react";
import type { ModuleRouteRegistration } from "../../app/router/route-contract.ts";
import { PosPage } from "./pages/pos-page.tsx";

export const posRoutes: ModuleRouteRegistration = {
  navigation: {
    id: "pos",
    moduleIds: ["pos"],
    path: "/pos",
    navigationKey: "pos",
    permission: "pos.register.read",
    icon: "pos",
  },
  routes: [
    {
      path: "pos",
      element: <PosPage />,
    },
  ],
};

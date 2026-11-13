import React from "react";
import type { ModuleRouteRegistration } from "../../app/router/route-contract.ts";
import { FixedAssetsPage } from "./pages/fixed-assets-page.tsx";

export const fixedAssetsRoutes: ModuleRouteRegistration = {
  navigation: {
    id: "fixed-assets",
    moduleIds: ["fixed-assets"],
    path: "/fixed-assets",
    navigationKey: "fixedAssets",
    permission: "assets.read",
    icon: "fixed-assets"
  },
  routes: [
    {
      path: "fixed-assets",
      element: <FixedAssetsPage />
    }
  ]
};

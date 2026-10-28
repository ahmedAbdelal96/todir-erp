import React from "react";
import type { ModuleRouteRegistration } from "../../app/router/route-contract.ts";
import { InventoryPage } from "./pages/inventory-page.tsx";

export const inventoryRoutes: ModuleRouteRegistration = {
  navigation: {
    id: "inventory",
    moduleIds: ["inventory"],
    path: "/inventory",
    navigationKey: "inventory",
    permission: "inventory.stock.read",
    icon: "catalog"
  },
  routes: [
    {
      path: "inventory",
      element: <InventoryPage />
    }
  ]
};

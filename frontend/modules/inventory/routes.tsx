import React from "react";
import type { ModuleRouteRegistration, PermissionCondition } from "../../app/router/route-contract.ts";
import { PermissionGuard } from "../../core/runtime/permission-guard.tsx";
import { ModuleRootRoute } from "../../app/router/module-root-route.tsx";
import { INVENTORY_DESTINATIONS } from "../../app/router/destination-registry.ts";
import { InventoryPage } from "./pages/inventory-page.tsx";
import { InventoryBalancesPage } from "./pages/inventory-balances-page.tsx";
import { InventoryMovementsPage } from "./pages/inventory-movements-page.tsx";
import { InventoryTransfersPage } from "./pages/inventory-transfers-page.tsx";
import { InventoryCountsPage } from "./pages/inventory-counts-page.tsx";
import { InventoryAdjustmentsPage } from "./pages/inventory-adjustments-page.tsx";
import { InventoryWarehousesPage } from "./pages/inventory-warehouses-page.tsx";
import { InventoryTraceabilityPage } from "./pages/inventory-traceability-page.tsx";

const inventoryModulePermissions: PermissionCondition = {
  anyOf: [
    "inventory.stock.read",
    "inventory.history.read",
    "inventory.transfer.create",
    "inventory.transfer.ship",
    "inventory.transfer.receive",
    "inventory.count.create",
    "inventory.count.enter",
    "inventory.count.approve",
    "inventory.count.post",
    "inventory.adjustment.create",
    "inventory.adjustment.approve",
    "inventory.adjustment.post",
    "inventory.warehouse.manage",
    "inventory.location.manage",
    "inventory.traceability.manage"
  ]
};

const inventoryNavigation = {
  id: "inventory" as const,
  moduleIds: ["inventory"] as const,
  path: "/inventory",
  navigationKey: "inventory" as const,
  category: "inventory" as const,
  destinations: INVENTORY_DESTINATIONS,
  permissions: inventoryModulePermissions,
  icon: "inventory"
};

export const inventoryRoutes: ModuleRouteRegistration = {
  navigation: inventoryNavigation,
  routes: [
    {
      path: "inventory",
      element: (
        <ModuleRootRoute
          module={inventoryNavigation}
          overviewPermission="inventory.stock.read"
          defaultElement={<InventoryPage />}
        />
      )
    },
    {
      path: "inventory/balances",
      element: (
        <PermissionGuard permission="inventory.stock.read" moduleId="inventory">
          <InventoryBalancesPage />
        </PermissionGuard>
      )
    },
    {
      path: "inventory/movements",
      element: (
        <PermissionGuard permission="inventory.history.read" moduleId="inventory">
          <InventoryMovementsPage />
        </PermissionGuard>
      )
    },
    {
      path: "inventory/transfers",
      element: (
        <PermissionGuard
          permission={{ anyOf: ["inventory.transfer.create", "inventory.transfer.ship", "inventory.transfer.receive"] }}
          moduleId="inventory"
        >
          <InventoryTransfersPage />
        </PermissionGuard>
      )
    },
    {
      path: "inventory/counts",
      element: (
        <PermissionGuard
          permission={{ anyOf: ["inventory.count.create", "inventory.count.enter", "inventory.count.approve", "inventory.count.post"] }}
          moduleId="inventory"
        >
          <InventoryCountsPage />
        </PermissionGuard>
      )
    },
    {
      path: "inventory/adjustments",
      element: (
        <PermissionGuard
          permission={{ anyOf: ["inventory.adjustment.create", "inventory.adjustment.approve", "inventory.adjustment.post"] }}
          moduleId="inventory"
        >
          <InventoryAdjustmentsPage />
        </PermissionGuard>
      )
    },
    {
      path: "inventory/warehouses",
      element: (
        <PermissionGuard
          permission={{ anyOf: ["inventory.warehouse.manage", "inventory.location.manage"] }}
          moduleId="inventory"
        >
          <InventoryWarehousesPage />
        </PermissionGuard>
      )
    },
    {
      path: "inventory/traceability",
      element: (
        <PermissionGuard
          permission="inventory.traceability.manage"
          moduleId="inventory"
        >
          <InventoryTraceabilityPage />
        </PermissionGuard>
      )
    }
  ]
};

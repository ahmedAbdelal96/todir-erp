import React from "react";
import type { ModuleRouteRegistration, PermissionCondition } from "../../app/router/route-contract.ts";
import { lazyNamed } from "../../app/router/lazy-route-component.ts";
import { PermissionGuard } from "../../core/runtime/permission-guard.tsx";
import { ModuleRootRoute } from "../../app/router/module-root-route.tsx";
import { INVENTORY_DESTINATIONS } from "../../app/router/destination-registry.ts";
const InventoryPage = lazyNamed(() => import("./pages/inventory-page.tsx"), "InventoryPage");
const InventoryBalancesPage = lazyNamed(() => import("./pages/inventory-balances-page.tsx"), "InventoryBalancesPage");
const InventoryMovementsPage = lazyNamed(() => import("./pages/inventory-movements-page.tsx"), "InventoryMovementsPage");
const InventoryTransfersPage = lazyNamed(() => import("./pages/inventory-transfers-page.tsx"), "InventoryTransfersPage");
const InventoryCountsPage = lazyNamed(() => import("./pages/inventory-counts-page.tsx"), "InventoryCountsPage");
const InventoryAdjustmentsPage = lazyNamed(() => import("./pages/inventory-adjustments-page.tsx"), "InventoryAdjustmentsPage");
const InventoryWarehousesPage = lazyNamed(() => import("./pages/inventory-warehouses-page.tsx"), "InventoryWarehousesPage");
const InventoryTraceabilityPage = lazyNamed(() => import("./pages/inventory-traceability-page.tsx"), "InventoryTraceabilityPage");

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
        <PermissionGuard permission="inventory.stock.read" moduleId="inventory" mode="read">
          <InventoryBalancesPage />
        </PermissionGuard>
      )
    },
    {
      path: "inventory/movements",
      element: (
        <PermissionGuard permission="inventory.history.read" moduleId="inventory" mode="read">
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
          mode="mixed"
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
          mode="mixed"
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
          mode="mixed"
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
          mode="mixed"
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
          mode="read"
        >
          <InventoryTraceabilityPage />
        </PermissionGuard>
      )
    }
  ]
};

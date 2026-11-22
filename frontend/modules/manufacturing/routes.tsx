import React from "react";
import type { ModuleRouteRegistration, PermissionCondition } from "../../app/router/route-contract.ts";
import { lazyNamed } from "../../app/router/lazy-route-component.ts";
import { PermissionGuard } from "../../core/runtime/permission-guard.tsx";
import { ModuleRootRoute } from "../../app/router/module-root-route.tsx";
import { MANUFACTURING_DESTINATIONS } from "../../app/router/destination-registry.ts";
const ManufacturingPage = lazyNamed(() => import("./pages/manufacturing-page.tsx"), "ManufacturingPage");
const ProductionOrdersPage = lazyNamed(() => import("./pages/production-orders-page.tsx"), "ProductionOrdersPage");
const ProductionOrderWorkspacePage = lazyNamed(() => import("./pages/production-order-workspace-page.tsx"), "ProductionOrderWorkspacePage");
const ShopFloorPage = lazyNamed(() => import("./pages/shop-floor-page.tsx"), "ShopFloorPage");
const MaterialsPage = lazyNamed(() => import("./pages/materials-page.tsx"), "MaterialsPage");
const MrpPage = lazyNamed(() => import("./pages/mrp-page.tsx"), "MrpPage");
const DefinitionsPage = lazyNamed(() => import("./pages/definitions-page.tsx"), "DefinitionsPage");
const CapacityPage = lazyNamed(() => import("./pages/capacity-page.tsx"), "CapacityPage");
const GenealogyPage = lazyNamed(() => import("./pages/genealogy-page.tsx"), "GenealogyPage");
const ManufacturingCostingPage = lazyNamed(() => import("./pages/manufacturing-costing-page.tsx"), "ManufacturingCostingPage");

const manufacturingModulePermissions: PermissionCondition = {
  anyOf: [
    "manufacturing.read",
    "manufacturing.production.read",
    "manufacturing.production.manage",
    "manufacturing.execution.manage",
    "manufacturing.mrp.read",
    "manufacturing.mrp.manage",
    "manufacturing.mrp.run",
    "manufacturing.definition.read",
    "manufacturing.definition.manage",
    "manufacturing.plant.manage",
    "manufacturing.bom.read",
    "manufacturing.capacity.manage",
    "manufacturing.cost.read",
    "manufacturing.cost.manage"
  ]
};

const manufacturingNavigation = {
  id: "manufacturing" as const,
  moduleIds: ["manufacturing"] as const,
  path: "/manufacturing",
  navigationKey: "manufacturing" as const,
  category: "inventory" as const,
  destinations: MANUFACTURING_DESTINATIONS,
  permissions: manufacturingModulePermissions,
  icon: "precision_manufacturing"
};

export const manufacturingRoutes: ModuleRouteRegistration = {
  navigation: manufacturingNavigation,
  routes: [
    {
      path: "manufacturing",
      element: (
        <ModuleRootRoute
          module={manufacturingNavigation}
          overviewPermission="manufacturing.read"
          defaultElement={<ManufacturingPage />}
        />
      )
    },
    {
      path: "manufacturing/orders",
      element: (
        <PermissionGuard
          permission={{ anyOf: ["manufacturing.production.read", "manufacturing.production.manage"] }}
          moduleId="manufacturing"
          mode="mixed"
        >
          <ProductionOrdersPage />
        </PermissionGuard>
      )
    },
    {
      path: "manufacturing/orders/:orderId",
      element: (
        <PermissionGuard
          permission={{ anyOf: ["manufacturing.production.read", "manufacturing.production.manage"] }}
          moduleId="manufacturing"
          mode="mixed"
        >
          <ProductionOrderWorkspacePage />
        </PermissionGuard>
      )
    },
    {
      path: "manufacturing/shop-floor",
      element: (
        <PermissionGuard
          permission={{ anyOf: ["manufacturing.execution.manage", "manufacturing.production.execute"] }}
          moduleId="manufacturing"
          mode="mixed"
        >
          <ShopFloorPage />
        </PermissionGuard>
      )
    },
    {
      path: "manufacturing/materials",
      element: (
        <PermissionGuard
          permission={{ anyOf: ["manufacturing.material.read", "manufacturing.material.issue", "manufacturing.material.consume"] }}
          moduleId="manufacturing"
          mode="mixed"
        >
          <MaterialsPage />
        </PermissionGuard>
      )
    },
    {
      path: "manufacturing/mrp",
      element: (
        <PermissionGuard
          permission={{ anyOf: ["manufacturing.mrp.read", "manufacturing.mrp.manage", "manufacturing.mrp.run"] }}
          moduleId="manufacturing"
          mode="mixed"
        >
          <MrpPage />
        </PermissionGuard>
      )
    },
    {
      path: "manufacturing/definitions",
      element: (
        <PermissionGuard
          permission={{ anyOf: ["manufacturing.definition.read", "manufacturing.definition.manage", "manufacturing.plant.manage", "manufacturing.bom.read"] }}
          moduleId="manufacturing"
          mode="mixed"
        >
          <DefinitionsPage />
        </PermissionGuard>
      )
    },
    {
      path: "manufacturing/capacity",
      element: (
        <PermissionGuard
          permission={{ anyOf: ["manufacturing.capacity.manage", "manufacturing.read"] }}
          moduleId="manufacturing"
          mode="mixed"
        >
          <CapacityPage />
        </PermissionGuard>
      )
    },
    {
      path: "manufacturing/genealogy",
      element: (
        <PermissionGuard
          permission={{ anyOf: ["manufacturing.production.read", "manufacturing.read"] }}
          moduleId="manufacturing"
          mode="read"
        >
          <GenealogyPage />
        </PermissionGuard>
      )
    },
    {
      path: "manufacturing/costing",
      element: (
        <PermissionGuard
          permission={{ anyOf: ["manufacturing.cost.read", "manufacturing.cost.manage", "manufacturing.cost.sensitive.read"] }}
          moduleId="manufacturing"
          mode="mixed"
        >
          <ManufacturingCostingPage />
        </PermissionGuard>
      )
    }
  ]
};

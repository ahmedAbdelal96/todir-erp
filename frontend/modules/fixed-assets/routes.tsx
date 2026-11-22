import React from "react";
import type { ModuleRouteRegistration } from "../../app/router/route-contract.ts";
import { ModuleRootRoute } from "../../app/router/module-root-route.tsx";
import { PermissionGuard } from "../../core/runtime/permission-guard.tsx";
import { FIXED_ASSETS_DESTINATIONS } from "../../app/router/destination-registry.ts";
import { FixedAssetsPage } from "./pages/fixed-assets-page.tsx";

const fixedAssetsNavigation = {
    id: "fixed-assets",
    moduleIds: ["fixed-assets"],
    path: "/fixed-assets",
    navigationKey: "fixedAssets",
    permissions: {
      anyOf: ["assets.read", "assets.financial.read", "assets.create", "assets.assign", "assets.capitalize", "assets.book.manage", "assets.depreciation.prepare", "assets.depreciation.post", "assets.verification.manage", "assets.audit.read"]
    },
    destinations: FIXED_ASSETS_DESTINATIONS,
    icon: "fixed-assets"
} as const;

export const fixedAssetsRoutes: ModuleRouteRegistration = {
  navigation: fixedAssetsNavigation,
  routes: [
    {
      path: "fixed-assets",
      element: <ModuleRootRoute module={fixedAssetsNavigation} overviewPermission="assets.read" defaultElement={<FixedAssetsPage />} />
    },
    { path: "fixed-assets/register", element: <PermissionGuard permission={{ anyOf: ["assets.read", "assets.create", "assets.edit"] }} moduleId="fixed-assets" mode="mixed"><FixedAssetsPage initialTab="register" /></PermissionGuard> },
    { path: "fixed-assets/custody", element: <PermissionGuard permission={{ anyOf: ["assets.read", "assets.assign", "assets.verification.manage"] }} moduleId="fixed-assets" mode="mixed"><FixedAssetsPage initialTab="custody" /></PermissionGuard> },
    { path: "fixed-assets/capitalization", element: <PermissionGuard permission={{ anyOf: ["assets.read", "assets.capitalize", "assets.opening.manage"] }} moduleId="fixed-assets" mode="mixed"><FixedAssetsPage initialTab="capitalization" /></PermissionGuard> },
    { path: "fixed-assets/depreciation", element: <PermissionGuard permission={{ anyOf: ["assets.financial.read", "assets.book.manage", "assets.depreciation.prepare", "assets.depreciation.post", "assets.depreciation.reverse"] }} moduleId="fixed-assets" mode="mixed"><FixedAssetsPage initialTab="depreciation" /></PermissionGuard> },
    { path: "fixed-assets/reports", element: <PermissionGuard permission={{ anyOf: ["assets.financial.read", "assets.audit.read", "assets.export"] }} moduleId="fixed-assets" mode="read"><FixedAssetsPage initialTab="reports" /></PermissionGuard> },
    { path: "fixed-assets/my-assets", element: <PermissionGuard permission="assets.read" moduleId="fixed-assets" mode="read"><FixedAssetsPage initialTab="my-assets" /></PermissionGuard> }
  ]
};

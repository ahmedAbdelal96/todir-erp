import React from "react";
import type { ModuleRouteRegistration, PermissionCondition } from "../../app/router/route-contract.ts";
import { lazyNamed } from "../../app/router/lazy-route-component.ts";
import { PermissionGuard } from "../../core/runtime/permission-guard.tsx";
import { ModuleRootRoute } from "../../app/router/module-root-route.tsx";
import { PURCHASING_DESTINATIONS } from "../../app/router/destination-registry.ts";
const PurchasingOverviewPage = lazyNamed(() => import("./pages/purchasing-overview-page.tsx"), "PurchasingOverviewPage");
const PurchaseOrdersPage = lazyNamed(() => import("./pages/purchase-orders-page.tsx"), "PurchaseOrdersPage");
const PurchasingReceiptsPage = lazyNamed(() => import("./pages/purchasing-receipts-page.tsx"), "PurchasingReceiptsPage");
const PurchasingMatchingPage = lazyNamed(() => import("./pages/purchasing-matching-page.tsx"), "PurchasingMatchingPage");
const PurchasingReturnsPage = lazyNamed(() => import("./pages/purchasing-returns-page.tsx"), "PurchasingReturnsPage");
const PurchasingSuppliersPage = lazyNamed(() => import("./pages/purchasing-suppliers-page.tsx"), "PurchasingSuppliersPage");
const PurchasingHistoryPage = lazyNamed(() => import("./pages/purchasing-history-page.tsx"), "PurchasingHistoryPage");
const PurchasingSettingsPage = lazyNamed(() => import("./pages/purchasing-settings-page.tsx"), "PurchasingSettingsPage");

const purchasingModulePermissions: PermissionCondition = {
  anyOf: [
    "purchasing.po.read",
    "purchasing.po.create",
    "purchasing.po.edit",
    "purchasing.po.approve",
    "purchasing.po.reject",
    "purchasing.po.amend",
    "purchasing.po.cancel",
    "purchasing.po.close",
    "purchasing.price.read",
    "purchasing.price.override",
    "purchasing.receipt.request",
    "purchasing.receipt.view",
    "purchasing.invoice.create_from_po",
    "purchasing.invoice.view_link",
    "purchasing.match.read",
    "purchasing.match.override",
    "purchasing.supplier_profile.manage",
    "purchasing.settings.manage",
    "purchasing.history.read",
    "purchasing.audit.read",
    "purchasing.return.create",
    "purchasing.return.approve",
    "purchasing.return.execute"
  ]
};

const purchasingNavigation = {
  id: "purchasing" as const,
  moduleIds: ["purchasing"] as const,
  path: "/purchasing",
  navigationKey: "purchasing" as const,
  category: "purchasing" as const,
  destinations: PURCHASING_DESTINATIONS,
  permissions: purchasingModulePermissions,
  icon: "catalog"
};

export const purchasingRoutes: ModuleRouteRegistration = {
  navigation: purchasingNavigation,
  routes: [
    {
      path: "purchasing",
      element: (
        <ModuleRootRoute
          module={purchasingNavigation}
          overviewPermission={PURCHASING_DESTINATIONS[0]?.permission}
          defaultElement={<PurchasingOverviewPage />}
        />
      )
    },
    {
      path: "purchasing/orders",
      element: (
        <PermissionGuard permission={PURCHASING_DESTINATIONS[1]?.permission} moduleId="purchasing" mode="mixed">
          <PurchaseOrdersPage />
        </PermissionGuard>
      )
    },
    {
      path: "purchasing/receipts",
      element: (
        <PermissionGuard permission={PURCHASING_DESTINATIONS[2]?.permission} moduleId="purchasing" mode="mixed">
          <PurchasingReceiptsPage />
        </PermissionGuard>
      )
    },
    {
      path: "purchasing/matching",
      element: (
        <PermissionGuard permission={PURCHASING_DESTINATIONS[3]?.permission} moduleId="purchasing" mode="mixed">
          <PurchasingMatchingPage />
        </PermissionGuard>
      )
    },
    {
      path: "purchasing/returns",
      element: (
        <PermissionGuard permission={PURCHASING_DESTINATIONS[4]?.permission} moduleId="purchasing" mode="mixed">
          <PurchasingReturnsPage />
        </PermissionGuard>
      )
    },
    {
      path: "purchasing/suppliers",
      element: (
        <PermissionGuard permission={PURCHASING_DESTINATIONS[5]?.permission} moduleId="purchasing" mode="mixed">
          <PurchasingSuppliersPage />
        </PermissionGuard>
      )
    },
    {
      path: "purchasing/history",
      element: (
        <PermissionGuard permission={PURCHASING_DESTINATIONS[6]?.permission} moduleId="purchasing" mode="read">
          <PurchasingHistoryPage />
        </PermissionGuard>
      )
    },
    {
      path: "purchasing/settings",
      element: (
        <PermissionGuard permission={PURCHASING_DESTINATIONS[7]?.permission} moduleId="purchasing" mode="mixed">
          <PurchasingSettingsPage />
        </PermissionGuard>
      )
    }
  ]
};

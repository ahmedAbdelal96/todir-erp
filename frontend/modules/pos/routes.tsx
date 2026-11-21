import React from "react";
import type { ModuleRouteRegistration, PermissionCondition } from "../../app/router/route-contract.ts";
import { PermissionGuard } from "../../core/runtime/permission-guard.tsx";
import { ModuleRootRoute } from "../../app/router/module-root-route.tsx";
import { POS_DESTINATIONS } from "../../app/router/destination-registry.ts";
import { PosCheckoutPage } from "./pages/pos-checkout-page.tsx";
import { PosSessionsPage } from "./pages/pos-sessions-page.tsx";
import { PosRegistersPage } from "./pages/pos-registers-page.tsx";
import { PosReturnsPage } from "./pages/pos-returns-page.tsx";
import { PosOfflinePage } from "./pages/pos-offline-page.tsx";
import { PosReportsPage } from "./pages/pos-reports-page.tsx";

const posModulePermissions: PermissionCondition = {
  anyOf: [
    "pos.register.read",
    "pos.register.manage",
    "pos.session.open",
    "pos.session.close",
    "pos.cart.manage",
    "pos.checkout.create",
    "pos.return.create",
    "pos.exchange.create",
    "pos.cash.manage",
    "pos.report.read",
    "pos.offline.sync",
    "pos.override.approve"
  ]
};

const posNavigation = {
  id: "pos" as const,
  moduleIds: ["pos"] as const,
  path: "/pos",
  navigationKey: "pos" as const,
  category: "sales" as const,
  destinations: POS_DESTINATIONS,
  permissions: posModulePermissions,
  icon: "pos"
};

export const posRoutes: ModuleRouteRegistration = {
  navigation: posNavigation,
  routes: [
    {
      path: "pos",
      element: (
        <ModuleRootRoute
          module={posNavigation}
          overviewPermission={POS_DESTINATIONS[0]?.permission}
          defaultElement={<PosCheckoutPage />}
        />
      )
    },
    {
      path: "pos/sessions",
      element: (
        <PermissionGuard permission={POS_DESTINATIONS[1]?.permission} moduleId="pos" mode="mixed">
          <PosSessionsPage />
        </PermissionGuard>
      )
    },
    {
      path: "pos/registers",
      element: (
        <PermissionGuard permission={POS_DESTINATIONS[2]?.permission} moduleId="pos" mode="mixed">
          <PosRegistersPage />
        </PermissionGuard>
      )
    },
    {
      path: "pos/returns",
      element: (
        <PermissionGuard permission={POS_DESTINATIONS[3]?.permission} moduleId="pos" mode="mixed">
          <PosReturnsPage />
        </PermissionGuard>
      )
    },
    {
      path: "pos/offline",
      element: (
        <PermissionGuard permission={POS_DESTINATIONS[4]?.permission} moduleId="pos" mode="mixed">
          <PosOfflinePage />
        </PermissionGuard>
      )
    },
    {
      path: "pos/reports",
      element: (
        <PermissionGuard permission={POS_DESTINATIONS[5]?.permission} moduleId="pos" mode="read">
          <PosReportsPage />
        </PermissionGuard>
      )
    }
  ]
};

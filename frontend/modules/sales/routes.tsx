import React from "react";
import type { ModuleRouteRegistration, PermissionCondition } from "../../app/router/route-contract.ts";
import { PermissionGuard } from "../../core/runtime/permission-guard.tsx";
import { ModuleRootRoute } from "../../app/router/module-root-route.tsx";
import { SALES_DESTINATIONS } from "../../app/router/destination-registry.ts";
import { SalesOverviewPage } from "./pages/sales-overview-page.tsx";
import { SalesQuotationsPage } from "./pages/sales-quotations-page.tsx";
import { SalesOrdersPage } from "./pages/sales-orders-page.tsx";
import { SalesReturnsPage } from "./pages/sales-returns-page.tsx";
import { SalesHistoryPage } from "./pages/sales-history-page.tsx";
import { SalesSettingsPage } from "./pages/sales-settings-page.tsx";

const salesModulePermissions: PermissionCondition = {
  anyOf: [
    "sales.quotation.read",
    "sales.quotation.create",
    "sales.quotation.edit",
    "sales.quotation.open",
    "sales.quotation.accept",
    "sales.quotation.decline",
    "sales.quotation.revise",
    "sales.quotation.cancel",
    "sales.quotation.convert_invoice",
    "sales.quotation.convert_order",
    "sales.order.read",
    "sales.order.create",
    "sales.order.edit",
    "sales.order.confirm",
    "sales.order.amend",
    "sales.order.cancel",
    "sales.order.fulfill",
    "sales.order.invoice",
    "sales.return.create",
    "sales.return.approve",
    "sales.return.execute",
    "sales.customer_profile.manage",
    "sales.channel.manage",
    "sales.team.manage",
    "sales.history.read",
    "sales.audit.read"
  ]
};

const salesNavigation = {
  id: "sales" as const,
  moduleIds: ["sales"] as const,
  path: "/sales",
  navigationKey: "sales" as const,
  category: "sales" as const,
  destinations: SALES_DESTINATIONS,
  permissions: salesModulePermissions,
  icon: "sales"
};

export const salesRoutes: ModuleRouteRegistration = {
  navigation: salesNavigation,
  routes: [
    {
      path: "sales",
      element: (
        <ModuleRootRoute
          module={salesNavigation}
          overviewPermission={SALES_DESTINATIONS[0]?.permission}
          defaultElement={<SalesOverviewPage />}
        />
      )
    },
    {
      path: "sales/quotations",
      element: (
        <PermissionGuard permission={SALES_DESTINATIONS[1]?.permission} moduleId="sales" mode="mixed">
          <SalesQuotationsPage />
        </PermissionGuard>
      )
    },
    {
      path: "sales/orders",
      element: (
        <PermissionGuard permission={SALES_DESTINATIONS[2]?.permission} moduleId="sales" mode="mixed">
          <SalesOrdersPage />
        </PermissionGuard>
      )
    },
    {
      path: "sales/returns",
      element: (
        <PermissionGuard permission={SALES_DESTINATIONS[3]?.permission} moduleId="sales" mode="mixed">
          <SalesReturnsPage />
        </PermissionGuard>
      )
    },
    {
      path: "sales/history",
      element: (
        <PermissionGuard permission={SALES_DESTINATIONS[4]?.permission} moduleId="sales" mode="read">
          <SalesHistoryPage />
        </PermissionGuard>
      )
    },
    {
      path: "sales/settings",
      element: (
        <PermissionGuard permission={SALES_DESTINATIONS[5]?.permission} moduleId="sales" mode="mutation">
          <SalesSettingsPage />
        </PermissionGuard>
      )
    }
  ]
};

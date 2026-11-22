import React from "react";
import type { ModuleRouteRegistration } from "../../app/router/route-contract.ts";
import { ModuleRootRoute } from "../../app/router/module-root-route.tsx";
import { PermissionGuard } from "../../core/runtime/permission-guard.tsx";
import { PAYMENTS_DESTINATIONS } from "../../app/router/destination-registry.ts";
import { PaymentListPage } from "./pages/payment-list-page.tsx";
import { PaymentDetailPage } from "./pages/payment-detail-page.tsx";
import { PublicPaymentLinkPage } from "./pages/public-payment-link-page.tsx";

const paymentsNavigation = {
    id: "payments",
    moduleIds: ["payments"],
    path: "/payments",
    navigationKey: "payments",
    permissions: { anyOf: ["payments.read", "payments.incoming.create", "payments.outgoing.create", "payments.internal_transfer.create", "payments.account.read", "payments.account.manage", "payments.provider.read", "payments.provider.manage"] },
    destinations: PAYMENTS_DESTINATIONS,
    icon: "payments"
} as const;

export const paymentsRoutes: ModuleRouteRegistration = {
  navigation: paymentsNavigation,
  routes: [
    { path: "payments", element: <ModuleRootRoute module={paymentsNavigation} overviewPermission={{ anyOf: ["payments.read", "payments.incoming.create", "payments.outgoing.create", "payments.account.read"] }} defaultElement={<PaymentListPage />} /> },
    { path: "payments/list", element: <PermissionGuard permission={{ anyOf: ["payments.read", "payments.incoming.create", "payments.outgoing.create"] }} moduleId="payments" mode="mixed"><PaymentListPage /></PermissionGuard> },
    { path: "payments/incoming", element: <PermissionGuard permission={{ anyOf: ["payments.read", "payments.incoming.create", "payments.complete_manual"] }} moduleId="payments" mode="mixed"><PaymentListPage initialTab="incoming" /></PermissionGuard> },
    { path: "payments/outgoing", element: <PermissionGuard permission={{ anyOf: ["payments.read", "payments.outgoing.create", "payments.complete_manual"] }} moduleId="payments" mode="mixed"><PaymentListPage initialTab="outgoing" /></PermissionGuard> },
    { path: "payments/transfers", element: <PermissionGuard permission={{ anyOf: ["payments.read", "payments.internal_transfer.create"] }} moduleId="payments" mode="mixed"><PaymentListPage initialTab="transfers" /></PermissionGuard> },
    { path: "payments/accounts", element: <PermissionGuard permission={{ anyOf: ["payments.account.read", "payments.account.manage"] }} moduleId="payments" mode="mixed"><PaymentListPage initialTab="accounts" /></PermissionGuard> },
    { path: "payments/requests", element: <PermissionGuard permission={{ anyOf: ["payments.provider.read", "payments.provider.manage", "payments.provider.retry", "payments.provider.reconcile", "payments.incoming.create"] }} moduleId="payments" mode="mixed"><PaymentListPage initialTab="requests" /></PermissionGuard> },
    { path: "payments/:id", element: <PermissionGuard permission="payments.read" moduleId="payments" mode="read"><PaymentDetailPage /></PermissionGuard> }
  ],
  publicRoutes: [
    { path: "payments/pay/:token", element: <PublicPaymentLinkPage /> }
  ]
};

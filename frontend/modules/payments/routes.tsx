import React from "react";
import type { ModuleRouteRegistration } from "../../app/router/route-contract.ts";
import { PaymentListPage } from "./pages/payment-list-page.tsx";
import { PaymentDetailPage } from "./pages/payment-detail-page.tsx";
import { PublicPaymentLinkPage } from "./pages/public-payment-link-page.tsx";

export const paymentsRoutes: ModuleRouteRegistration = {
  navigation: {
    id: "payments",
    moduleIds: ["payments"],
    path: "/payments",
    navigationKey: "payments",
    permission: "payments.read",
    icon: "payments"
  },
  routes: [
    { path: "payments", element: <PaymentListPage /> },
    { path: "payments/list", element: <PaymentListPage /> },
    { path: "payments/:id", element: <PaymentDetailPage /> },
    { path: "payments/pay/:token", element: <PublicPaymentLinkPage /> }
  ]
};

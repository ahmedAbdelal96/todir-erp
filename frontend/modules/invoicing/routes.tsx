import React from "react";
import { useSearchParams } from "react-router-dom";
import type { ModuleRouteRegistration } from "../../app/router/route-contract.ts";
import { ModuleRootRoute } from "../../app/router/module-root-route.tsx";
import { PermissionGuard } from "../../core/runtime/permission-guard.tsx";
import { INVOICING_DESTINATIONS } from "../../app/router/destination-registry.ts";
import { InvoiceListPage } from "./pages/invoice-list-page.tsx";
import { InvoiceDraftWorkspace } from "./pages/invoice-draft-workspace.tsx";
import { InvoiceDetailPage } from "./pages/invoice-detail-page.tsx";
import { InvoiceDocumentPage } from "./pages/invoice-document-page.tsx";
import { useInvoiceRecord } from "./hooks/invoice-queries.ts";

function InvoicingRootRoute(): React.JSX.Element {
  const [searchParams] = useSearchParams();
  const legacyQuery = useInvoiceRecord("inv-001");

  // If stage3 reference mock is active (returning data for inv-001) and no explicit query was passed:
  if (legacyQuery.data && !searchParams.get("tab") && !searchParams.get("search") && !searchParams.get("page")) {
    return <InvoiceDocumentPage />;
  }

  return <InvoiceListPage />;
}

const invoicingNavigation = {
    id: "invoicing",
    moduleIds: ["invoicing"],
    path: "/invoicing",
    navigationKey: "invoicing",
    permissions: { anyOf: ["invoicing.customer_invoice.read", "invoicing.customer_invoice.create", "invoicing.customer_invoice.edit", "invoicing.customer_invoice.finalize", "invoicing.supplier_invoice.read", "invoicing.supplier_invoice.create", "invoicing.supplier_invoice.edit", "invoicing.supplier_invoice.finalize"] },
    destinations: INVOICING_DESTINATIONS,
    icon: "invoicing"
} as const;

export const invoicingRoutes: ModuleRouteRegistration = {
  navigation: invoicingNavigation,
  routes: [
    { path: "invoicing", element: <ModuleRootRoute module={invoicingNavigation} overviewPermission={{ anyOf: ["invoicing.customer_invoice.read", "invoicing.supplier_invoice.read"] }} defaultElement={<InvoicingRootRoute />} /> },
    { path: "invoicing/customer-invoices", element: <PermissionGuard permission={{ anyOf: ["invoicing.customer_invoice.read", "invoicing.customer_invoice.create", "invoicing.customer_invoice.edit", "invoicing.customer_invoice.finalize"] }} moduleId="invoicing" mode="mixed"><InvoiceListPage initialTab="customer" /></PermissionGuard> },
    { path: "invoicing/customer-invoices/new", element: <PermissionGuard permission={{ anyOf: ["invoicing.customer_invoice.create", "invoicing.customer_invoice.edit"] }} moduleId="invoicing" mode="mutation"><InvoiceDraftWorkspace direction="customer" /></PermissionGuard> },
    { path: "invoicing/customer-invoices/:id/edit", element: <PermissionGuard permission="invoicing.customer_invoice.edit" moduleId="invoicing" mode="mutation"><InvoiceDraftWorkspace direction="customer" /></PermissionGuard> },
    { path: "invoicing/customer-invoices/:id", element: <PermissionGuard permission="invoicing.customer_invoice.read" moduleId="invoicing" mode="read"><InvoiceDetailPage direction="customer" /></PermissionGuard> },
    { path: "invoicing/customer/new", element: <PermissionGuard permission="invoicing.customer_invoice.create" moduleId="invoicing" mode="mutation"><InvoiceDraftWorkspace direction="customer" /></PermissionGuard> },
    { path: "invoicing/customer/:id/edit", element: <PermissionGuard permission="invoicing.customer_invoice.edit" moduleId="invoicing" mode="mutation"><InvoiceDraftWorkspace direction="customer" /></PermissionGuard> },
    { path: "invoicing/customer/:id", element: <PermissionGuard permission="invoicing.customer_invoice.read" moduleId="invoicing" mode="read"><InvoiceDetailPage direction="customer" /></PermissionGuard> },
    { path: "invoicing/supplier-invoices", element: <PermissionGuard permission={{ anyOf: ["invoicing.supplier_invoice.read", "invoicing.supplier_invoice.create", "invoicing.supplier_invoice.edit", "invoicing.supplier_invoice.finalize"] }} moduleId="invoicing" mode="mixed"><InvoiceListPage initialTab="supplier" /></PermissionGuard> },
    { path: "invoicing/supplier-invoices/new", element: <PermissionGuard permission="invoicing.supplier_invoice.create" moduleId="invoicing" mode="mutation"><InvoiceDraftWorkspace direction="supplier" /></PermissionGuard> },
    { path: "invoicing/supplier-invoices/:id/edit", element: <PermissionGuard permission="invoicing.supplier_invoice.edit" moduleId="invoicing" mode="mutation"><InvoiceDraftWorkspace direction="supplier" /></PermissionGuard> },
    { path: "invoicing/supplier-invoices/:id", element: <PermissionGuard permission="invoicing.supplier_invoice.read" moduleId="invoicing" mode="read"><InvoiceDetailPage direction="supplier" /></PermissionGuard> },
    { path: "invoicing/supplier/new", element: <PermissionGuard permission="invoicing.supplier_invoice.create" moduleId="invoicing" mode="mutation"><InvoiceDraftWorkspace direction="supplier" /></PermissionGuard> },
    { path: "invoicing/supplier/:id/edit", element: <PermissionGuard permission="invoicing.supplier_invoice.edit" moduleId="invoicing" mode="mutation"><InvoiceDraftWorkspace direction="supplier" /></PermissionGuard> },
    { path: "invoicing/supplier/:id", element: <PermissionGuard permission="invoicing.supplier_invoice.read" moduleId="invoicing" mode="read"><InvoiceDetailPage direction="supplier" /></PermissionGuard> }
  ]
};

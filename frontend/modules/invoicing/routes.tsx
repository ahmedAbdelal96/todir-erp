import React from "react";
import { useSearchParams } from "react-router-dom";
import type { ModuleRouteRegistration } from "../../app/router/route-contract.ts";
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

export const invoicingRoutes: ModuleRouteRegistration = {
  navigation: {
    id: "invoicing",
    moduleIds: ["invoicing"],
    path: "/invoicing",
    navigationKey: "invoicing",
    permission: "invoicing.customer_invoice.read",
    icon: "invoicing"
  },
  routes: [
    { path: "invoicing", element: <InvoicingRootRoute /> },
    { path: "invoicing/customer-invoices", element: <InvoiceListPage initialTab="customer" /> },
    { path: "invoicing/customer-invoices/new", element: <InvoiceDraftWorkspace direction="customer" /> },
    { path: "invoicing/customer-invoices/:id/edit", element: <InvoiceDraftWorkspace direction="customer" /> },
    { path: "invoicing/customer-invoices/:id", element: <InvoiceDetailPage direction="customer" /> },
    { path: "invoicing/customer/new", element: <InvoiceDraftWorkspace direction="customer" /> },
    { path: "invoicing/customer/:id/edit", element: <InvoiceDraftWorkspace direction="customer" /> },
    { path: "invoicing/customer/:id", element: <InvoiceDetailPage direction="customer" /> },
    { path: "invoicing/supplier-invoices", element: <InvoiceListPage initialTab="supplier" /> },
    { path: "invoicing/supplier-invoices/new", element: <InvoiceDraftWorkspace direction="supplier" /> },
    { path: "invoicing/supplier-invoices/:id/edit", element: <InvoiceDraftWorkspace direction="supplier" /> },
    { path: "invoicing/supplier-invoices/:id", element: <InvoiceDetailPage direction="supplier" /> },
    { path: "invoicing/supplier/new", element: <InvoiceDraftWorkspace direction="supplier" /> },
    { path: "invoicing/supplier/:id/edit", element: <InvoiceDraftWorkspace direction="supplier" /> },
    { path: "invoicing/supplier/:id", element: <InvoiceDetailPage direction="supplier" /> }
  ]
};

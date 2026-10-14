import { lazy } from "react";
import { useNavigate } from "react-router-dom";
import type { ModuleRouteRegistration } from "@app/router/route-contract.ts";

const InvoiceDocumentPage = lazy(() => import("./pages/invoice-document-page.tsx").then(module => ({ default: module.InvoiceDocumentPage })));

function InvoicingRoute(): React.JSX.Element {
  const navigate = useNavigate();
  return <InvoiceDocumentPage onBack={() => navigate("/")} />;
}

export const invoicingRoutes: ModuleRouteRegistration = {
  navigation: {
    id: "invoicing",
    moduleIds: ["invoicing"],
    path: "/invoicing",
    navigationKey: "invoicing",
    permission: "invoicing.invoices.read",
    icon: "invoicing"
  },
  routes: [{ path: "invoicing", element: <InvoicingRoute /> }]
};

import { lazy } from "react";
import { useNavigate } from "react-router-dom";
import type { ModuleRouteRegistration } from "@app/router/route-contract.ts";

const ManualJournalPage = lazy(() => import("./pages/manual-journal-page.tsx").then(module => ({ default: module.ManualJournalPage })));

function AccountingRoute(): React.JSX.Element {
  const navigate = useNavigate();
  return <ManualJournalPage onBack={() => navigate("/")} />;
}

export const accountingRoutes: ModuleRouteRegistration = {
  navigation: {
    id: "accounting",
    moduleIds: ["accounting"],
    path: "/accounting",
    label: { ar: "المحاسبة العامة", en: "Accounting" },
    permission: "accounting.journals.read",
    icon: "accounting"
  },
  routes: [{ path: "accounting", element: <AccountingRoute /> }]
};

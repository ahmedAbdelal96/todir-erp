import { lazy } from "react";
import { useNavigate } from "react-router-dom";
import type { ModuleRouteRegistration } from "@app/router/route-contract.ts";

const ImportWizardPage = lazy(() => import("./pages/import-wizard-page.tsx").then(module => ({ default: module.ImportWizardPage })));

function MigrationRoute(): React.JSX.Element {
  const navigate = useNavigate();
  return <ImportWizardPage onFinish={() => navigate("/directory")} />;
}

export const migrationRoutes: ModuleRouteRegistration = {
  navigation: {
    id: "migration",
    moduleIds: ["migration"],
    path: "/migration",
    label: { ar: "معالج الاستيراد", en: "Import Wizard" },
    permission: "platform.migration.execute",
    icon: "migration"
  },
  routes: [{ path: "migration", element: <MigrationRoute /> }]
};

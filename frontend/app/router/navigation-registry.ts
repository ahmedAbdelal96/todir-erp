import { accountingRoutes } from "@modules/accounting/public.ts";
import { catalogRoutes } from "@modules/catalog/public.ts";
import { directoryRoutes } from "@modules/directory/public.ts";
import { finbaseRoutes } from "@modules/finbase/public.ts";
import { pricingRoutes } from "@modules/pricing/public.ts";
import { subledgerRoutes } from "@modules/subledger/public.ts";
import { taxRoutes } from "@modules/tax/public.ts";
import { inventoryRoutes } from "@modules/inventory/public.ts";
import { salesRoutes } from "@modules/sales/public.ts";
import { purchasingRoutes } from "@modules/purchasing/public.ts";
import { hrRoutes } from "@modules/hr/public.ts";
import { payrollRoutes } from "@modules/payroll/public.ts";
import { crmRoutes } from "@modules/crm/public.ts";
import { posRoutes } from "@modules/pos/public.ts";
import { identityRoutes } from "@modules/identity/public.ts";
import { invoicingRoutes } from "@modules/invoicing/public.ts";
import { migrationRoutes } from "@modules/migration/public.ts";
import { paymentsRoutes } from "@modules/payments/public.ts";
import { expensesRoutes } from "@modules/expenses/public.ts";
import { fixedAssetsRoutes } from "@modules/fixed-assets/public.ts";
import { projectsRoutes } from "@modules/projects/public.ts";
import type { ModuleNavigationRegistration } from "./route-contract.ts";

const homeNavigation: ModuleNavigationRegistration = {
  id: "home", moduleIds: ["home"], path: "/", navigationKey: "home", icon: "home"
};
const domainSettingsNavigation: ModuleNavigationRegistration = {
  id: "domain-settings", moduleIds: ["domain-settings", "tenancy"], path: "/domain-settings",
  navigationKey: "loginDomain", permission: "platform.tenancy.read", icon: "domain-settings"
};
const previewNavigation: ModuleNavigationRegistration = {
  id: "preview", moduleIds: ["preview"], path: "/preview", navigationKey: "preview", icon: "preview"
};
const settingsNavigation: ModuleNavigationRegistration = {
  id: "settings", moduleIds: ["settings"], path: "/settings", navigationKey: "settings", permission: "platform.settings.read", icon: "settings"
};

export const navigationRegistry: readonly ModuleNavigationRegistration[] = [
  homeNavigation,
  identityRoutes.navigation,
  domainSettingsNavigation,
  directoryRoutes.navigation,
  catalogRoutes.navigation,
  invoicingRoutes.navigation,
  paymentsRoutes.navigation,
  accountingRoutes.navigation,
  migrationRoutes.navigation,
  finbaseRoutes.navigation,
  pricingRoutes.navigation,
  subledgerRoutes.navigation,
  taxRoutes.navigation,
  inventoryRoutes.navigation,
  salesRoutes.navigation,
  purchasingRoutes.navigation,
  hrRoutes.navigation,
  payrollRoutes.navigation,
  crmRoutes.navigation,
  posRoutes.navigation,
  expensesRoutes.navigation,
  fixedAssetsRoutes.navigation,
  projectsRoutes.navigation,
  previewNavigation,
  settingsNavigation
];

export function resolveNavigation(moduleIds: readonly string[]): readonly ModuleNavigationRegistration[] {
  return navigationRegistry.filter(item => item.id === "home" || item.moduleIds.some(moduleId => moduleIds.includes(moduleId)));
}

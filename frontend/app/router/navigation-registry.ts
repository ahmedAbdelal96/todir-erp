import { accountingRoutes } from "../../modules/accounting/public.ts";
import { catalogRoutes } from "../../modules/catalog/public.ts";
import { directoryRoutes } from "../../modules/directory/public.ts";
import { finbaseRoutes } from "../../modules/finbase/public.ts";
import { pricingRoutes } from "../../modules/pricing/public.ts";
import { subledgerRoutes } from "../../modules/subledger/public.ts";
import { taxRoutes } from "../../modules/tax/public.ts";
import { inventoryRoutes } from "../../modules/inventory/public.ts";
import { salesRoutes } from "../../modules/sales/public.ts";
import { purchasingRoutes } from "../../modules/purchasing/public.ts";
import { hrRoutes } from "../../modules/hr/public.ts";
import { payrollRoutes } from "../../modules/payroll/public.ts";
import { crmRoutes } from "../../modules/crm/public.ts";
import { posRoutes } from "../../modules/pos/public.ts";
import { identityRoutes } from "../../modules/identity/public.ts";
import { invoicingRoutes } from "../../modules/invoicing/public.ts";
import { migrationRoutes } from "../../modules/migration/public.ts";
import { paymentsRoutes } from "../../modules/payments/public.ts";
import { expensesRoutes } from "../../modules/expenses/public.ts";
import { fixedAssetsRoutes } from "../../modules/fixed-assets/public.ts";
import { projectsRoutes } from "../../modules/projects/public.ts";
import { serviceRoutes } from "../../modules/service/public.ts";
import { bankingRoutes } from "../../modules/banking/public.ts";
import { settingsRoutes } from "../../modules/settings/public.ts";
import { manufacturingRoutes } from "../../modules/manufacturing/public.ts";
import { qualityRoutes } from "../../modules/quality/public.ts";
import { planningRoutes } from "../../modules/planning/public.ts";
import type { ModuleNavigationRegistration, NavigationCategory } from "./route-contract.ts";
import type { EffectiveAccess } from "../../core/runtime/effective-access.ts";
import { isModulePermitted } from "./destination-registry.ts";

const MODULE_CATEGORIES: Record<string, NavigationCategory> = {
  home: "workspace",
  crm: "sales",
  sales: "sales",
  invoicing: "sales",
  directory: "sales",
  pricing: "sales",
  purchasing: "purchasing",
  catalog: "inventory",
  inventory: "inventory",
  manufacturing: "inventory",
  quality: "inventory",
  pos: "sales",
  projects: "inventory",
  service: "inventory",
  accounting: "finance",
  finbase: "finance",
  banking: "finance",
  payments: "finance",
  subledger: "finance",
  tax: "finance",
  expenses: "finance",
  fixedAssets: "finance",
  "fixed-assets": "finance",
  planning: "finance",
  hr: "hr",
  payroll: "hr",
  identity: "admin",
  users: "admin",
  settings: "admin",
  migration: "admin",
  "domain-settings": "admin",
  preview: "admin"
};

const homeNavigation: ModuleNavigationRegistration = {
  id: "home", moduleIds: ["home"], path: "/", navigationKey: "home", icon: "home", category: "workspace"
};
const domainSettingsNavigation: ModuleNavigationRegistration = {
  id: "domain-settings", moduleIds: ["domain-settings", "tenancy"], path: "/domain-settings",
  navigationKey: "loginDomain", permission: "platform.tenancy.read", icon: "domain-settings", category: "admin"
};
const previewNavigation: ModuleNavigationRegistration = {
  id: "preview", moduleIds: ["preview"], path: "/preview", navigationKey: "preview", icon: "preview", category: "admin"
};

const rawRegistry: readonly ModuleNavigationRegistration[] = [
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
  manufacturingRoutes.navigation,
  qualityRoutes.navigation,
  planningRoutes.navigation,
  salesRoutes.navigation,
  purchasingRoutes.navigation,
  hrRoutes.navigation,
  payrollRoutes.navigation,
  crmRoutes.navigation,
  posRoutes.navigation,
  expensesRoutes.navigation,
  fixedAssetsRoutes.navigation,
  projectsRoutes.navigation,
  serviceRoutes.navigation,
  bankingRoutes.navigation,
  previewNavigation,
  settingsRoutes.navigation
];

export const navigationRegistry: readonly ModuleNavigationRegistration[] = rawRegistry.map(item => ({
  ...item,
  category: item.category ?? MODULE_CATEGORIES[item.id] ?? "workspace"
}));

export function resolveNavigation(moduleIds: readonly string[]): readonly ModuleNavigationRegistration[] {
  return navigationRegistry.filter(item => item.id === "home" || item.moduleIds.some(moduleId => moduleIds.includes(moduleId)));
}

export function resolveEffectiveNavigation(access: EffectiveAccess | null | undefined): readonly ModuleNavigationRegistration[] {
  if (!access) return [];
  return navigationRegistry.filter(item => {
    if (item.id === "home") return true;
    return isModulePermitted(item, access);
  });
}

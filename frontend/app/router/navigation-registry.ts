import { accountingRoutes } from "@modules/accounting/public.ts";
import { catalogRoutes } from "@modules/catalog/public.ts";
import { directoryRoutes } from "@modules/directory/public.ts";
import { finbaseRoutes } from "@modules/finbase/public.ts";
import { identityRoutes } from "@modules/identity/public.ts";
import { invoicingRoutes } from "@modules/invoicing/public.ts";
import { migrationRoutes } from "@modules/migration/public.ts";
import { paymentsRoutes } from "@modules/payments/public.ts";
import type { ModuleNavigationRegistration } from "./route-contract.ts";

const homeNavigation: ModuleNavigationRegistration = {
  id: "home", moduleIds: ["home"], path: "/", label: { ar: "الرئيسية", en: "Home" }, icon: "home"
};
const domainSettingsNavigation: ModuleNavigationRegistration = {
  id: "domain-settings", moduleIds: ["domain-settings", "tenancy"], path: "/domain-settings",
  label: { ar: "إعدادات تسجيل الدخول", en: "Login Domain" }, permission: "platform.tenancy.read", icon: "domain-settings"
};
const previewNavigation: ModuleNavigationRegistration = {
  id: "preview", moduleIds: ["preview"], path: "/preview", label: { ar: "معاينة النظام", en: "System Preview" }, icon: "preview"
};
const settingsNavigation: ModuleNavigationRegistration = {
  id: "settings", moduleIds: ["settings"], path: "/settings", label: { ar: "الإعدادات", en: "Settings" }, permission: "platform.settings.read", icon: "settings"
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
  previewNavigation,
  settingsNavigation
];

export function resolveNavigation(moduleIds: readonly string[]): readonly ModuleNavigationRegistration[] {
  return navigationRegistry.filter(item => item.id === "home" || item.moduleIds.some(moduleId => moduleIds.includes(moduleId)));
}

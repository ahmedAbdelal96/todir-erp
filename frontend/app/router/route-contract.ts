import type { RouteObject } from "react-router-dom";
import type { PermissionSet } from "../../core/runtime/permissions.ts";
import type { EffectiveAccess, PermissionCondition } from "../../core/runtime/effective-access.ts";

export type { PermissionCondition };

export type NavigationCategory =
  | "workspace"
  | "sales"
  | "purchasing"
  | "inventory"
  | "finance"
  | "hr"
  | "admin";

export type DestinationMode = "read" | "mutation" | "mixed";

export type NavDestination = Readonly<{
  id: string;
  category: NavigationCategory;
  moduleId: string;
  path: string;
  labelKey: string;
  fallbackLabel: string;
  icon: string;
  permission?: PermissionCondition | undefined;
  order: number;
  isPrimary?: boolean;
  keywords?: readonly string[];
  mode?: DestinationMode;
}>;

export type ModuleNavigationRegistration = Readonly<{
  id: string;
  moduleIds: readonly string[];
  path: string;
  navigationKey: "home" | "users" | "loginDomain" | "directory" | "catalog" | "invoicing" | "payments" | "accounting" | "import" | "financeBase" | "pricing" | "subledger" | "tax" | "inventory" | "sales" | "purchasing" | "hr" | "payroll" | "crm" | "pos" | "expenses" | "fixedAssets" | "projects" | "service" | "banking" | "manufacturing" | "preview" | "settings";
  permission?: string | undefined;
  permissions?: PermissionCondition | undefined;
  category?: NavigationCategory | undefined;
  destinations?: readonly NavDestination[] | undefined;
  icon: string;
}>;

export type ModuleRouteRegistration = Readonly<{
  navigation: ModuleNavigationRegistration;
  routes: readonly RouteObject[];
  publicRoutes?: readonly RouteObject[] | undefined;
}>;

export type WorkspaceOutletContext = Readonly<{
  density: "comfortable" | "compact";
  legalEntityId?: string | undefined;
  legalEntityCode?: string | undefined;
  legalEntityName?: string | undefined;
  branchId?: string | undefined;
  permissions?: PermissionSet | undefined;
  effectiveAccess?: EffectiveAccess | undefined;
  tenantId?: string | undefined;
  userId?: string | undefined;
}>;

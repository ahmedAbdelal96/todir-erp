import type { RouteObject } from "react-router-dom";
import type { PermissionSet } from "../../core/runtime/permissions.ts";

export type ModuleNavigationRegistration = Readonly<{
  id: string;
  moduleIds: readonly string[];
  path: string;
  navigationKey: "home" | "users" | "loginDomain" | "directory" | "catalog" | "invoicing" | "payments" | "accounting" | "import" | "financeBase" | "pricing" | "subledger" | "tax" | "inventory" | "sales" | "purchasing" | "hr" | "payroll" | "crm" | "pos" | "expenses" | "fixedAssets" | "projects" | "service" | "preview" | "settings";
  permission?: string | undefined;
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
  branchId?: string | undefined;
  permissions?: PermissionSet | undefined;
}>;

import type { RouteObject } from "react-router-dom";

export type ModuleNavigationRegistration = Readonly<{
  id: string;
  moduleIds: readonly string[];
  path: string;
  navigationKey: "home" | "users" | "loginDomain" | "directory" | "catalog" | "invoicing" | "payments" | "accounting" | "import" | "financeBase" | "pricing" | "subledger" | "tax" | "inventory" | "sales" | "purchasing" | "preview" | "settings";
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
}>;

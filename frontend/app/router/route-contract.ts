import type { RouteObject } from "react-router-dom";

export type RouteLabel = Readonly<{
  ar: string;
  en: string;
}>;

export type ModuleNavigationRegistration = Readonly<{
  id: string;
  moduleIds: readonly string[];
  path: string;
  label: RouteLabel;
  permission?: string | undefined;
  icon: string;
}>;

export type ModuleRouteRegistration = Readonly<{
  navigation: ModuleNavigationRegistration;
  routes: readonly RouteObject[];
}>;

export type WorkspaceOutletContext = Readonly<{
  language: "ar" | "en";
  density: "comfortable" | "compact";
}>;

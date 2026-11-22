import React from "react";
import { Navigate, useLocation, useOutletContext, useSearchParams } from "react-router-dom";
import type { WorkspaceOutletContext, ModuleNavigationRegistration, PermissionCondition } from "./route-contract.ts";
import { evaluateEffectivePermissionCondition, isModuleOperational } from "../../core/runtime/effective-access.ts";
import { getPermittedDestinationsForModule, isDestinationPermitted } from "./destination-registry.ts";
import { DefaultAccessDeniedView } from "../../core/runtime/permission-guard.tsx";
import { removeLegacyTabQuery, resolveLegacyTaskDestination } from "./legacy-task-navigation.ts";

export interface ModuleRootRouteProps {
  readonly module: ModuleNavigationRegistration;
  readonly overviewPermission?: PermissionCondition | undefined;
  readonly defaultElement: React.ReactNode;
  readonly allowReadOnly?: boolean | undefined;
}

export function ModuleRootRoute({
  module,
  overviewPermission,
  defaultElement,
  allowReadOnly = true
}: ModuleRootRouteProps): React.JSX.Element {
  const outletCtx = useOutletContext<WorkspaceOutletContext | undefined>();
  const effectiveAccess = outletCtx?.effectiveAccess;
  const location = useLocation();
  const [searchParams] = useSearchParams();

  if (!effectiveAccess) {
    return <DefaultAccessDeniedView />;
  }

  // Check if module is entitled and operational
  const moduleIds = module.moduleIds && module.moduleIds.length > 0 ? module.moduleIds : [module.id];
  if (!moduleIds.some(m => isModuleOperational(effectiveAccess, m, allowReadOnly))) {
    return <DefaultAccessDeniedView />;
  }

  // `?tab=` is a legacy entry point only. Resolve it against the same
  // destination registry used by the sidebar, then enforce that destination's
  // permission before changing the URL. This prevents a query value from
  // selecting a protected task inside an otherwise permitted module.
  const legacyTab = searchParams.get("tab");
  if (legacyTab) {
    const destination = resolveLegacyTaskDestination(module, legacyTab);
    if (destination && !isDestinationPermitted(destination, effectiveAccess)) {
      return <DefaultAccessDeniedView />;
    }

    const canonicalPath = destination?.path ?? module.path;
    const cleanedPath = removeLegacyTabQuery(canonicalPath, location.search);
    return <Navigate to={cleanedPath} replace />;
  }

  // If user has permission for overview, render defaultElement
  const hasOverview = overviewPermission
    ? evaluateEffectivePermissionCondition(overviewPermission, effectiveAccess, allowReadOnly)
    : true;

  if (hasOverview) {
    return <>{defaultElement}</>;
  }

  // If user lacks overview permission, check if any sub-destination is permitted
  const permitted = getPermittedDestinationsForModule(module, effectiveAccess);
  const fallback = permitted.find(d => d.path !== module.path) ?? permitted[0];

  if (fallback && fallback.path !== module.path) {
    return <Navigate to={fallback.path} replace />;
  }

  // No permitted destinations at all for this user
  return <DefaultAccessDeniedView />;
}

import React from "react";
import { Navigate, useOutletContext } from "react-router-dom";
import type { WorkspaceOutletContext, ModuleNavigationRegistration, PermissionCondition } from "./route-contract.ts";
import { evaluateEffectivePermissionCondition, isModuleOperational } from "../../core/runtime/effective-access.ts";
import { getPermittedDestinationsForModule } from "./destination-registry.ts";
import { DefaultAccessDeniedView } from "../../core/runtime/permission-guard.tsx";

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

  if (!effectiveAccess) {
    return <DefaultAccessDeniedView />;
  }

  // Check if module is entitled and operational
  const moduleIds = module.moduleIds && module.moduleIds.length > 0 ? module.moduleIds : [module.id];
  if (!moduleIds.some(m => isModuleOperational(effectiveAccess, m, allowReadOnly))) {
    return <DefaultAccessDeniedView />;
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

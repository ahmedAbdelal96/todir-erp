import { useOutletContext } from "react-router-dom";
import type { WorkspaceOutletContext } from "../../../app/router/route-contract.ts";
import { hasPermissionInAccess, isModuleReadOnly } from "../../../core/runtime/effective-access.ts";

export interface QualityAccess {
  readonly isReadOnly: boolean;
  readonly canManagePlans: boolean;
  readonly canManageCharacteristics: boolean;
  readonly canCreateInspection: boolean;
  readonly canExecuteInspection: boolean;
  readonly canReviewInspection: boolean;
  readonly canManageHolds: boolean;
  readonly canManageDispositions: boolean;
  readonly canManageIssues: boolean;
  readonly canManageNcrs: boolean;
  readonly canManageCapas: boolean;
  readonly canManageSuppliers: boolean;
  readonly canManageCertificates: boolean;
  readonly can: (permission: string) => boolean;
}

export function useQualityAccess(): QualityAccess {
  let outletCtx: WorkspaceOutletContext | undefined;
  try {
    outletCtx = useOutletContext<WorkspaceOutletContext | undefined>();
  } catch {
    // rendered outside router context
  }

  const effectiveAccess = outletCtx?.effectiveAccess;
  const permissions = outletCtx?.permissions;
  const isReadOnly = effectiveAccess ? isModuleReadOnly(effectiveAccess, "quality") : false;

  const can = (permission: string): boolean => {
    if (isReadOnly && permission !== "quality.read") {
      return false;
    }
    if (effectiveAccess) {
      return hasPermissionInAccess(effectiveAccess, permission);
    }
    if (permissions) {
      return permissions.hasPermission(permission);
    }
    // Fail-closed: deny permission when no security context is present
    return false;
  };

  return {
    isReadOnly,
    canManagePlans: can("quality.plan.manage"),
    canManageCharacteristics: can("quality.characteristic.manage"),
    canCreateInspection: can("quality.inspection.create"),
    canExecuteInspection: can("quality.inspection.execute"),
    canReviewInspection: can("quality.inspection.review"),
    canManageHolds: can("quality.hold.manage"),
    canManageDispositions: can("quality.disposition.manage"),
    canManageIssues: can("quality.issue.manage"),
    canManageNcrs: can("quality.ncr.manage"),
    canManageCapas: can("quality.capa.manage"),
    canManageSuppliers: can("quality.supplier.manage"),
    canManageCertificates: can("quality.certificate.manage"),
    can
  };
}

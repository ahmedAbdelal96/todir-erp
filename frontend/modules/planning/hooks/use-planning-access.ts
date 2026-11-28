import { useOutletContext } from "react-router-dom";
import type { WorkspaceOutletContext } from "../../../app/router/route-contract.ts";
import { hasPermissionInAccess, isModuleReadOnly } from "../../../core/runtime/effective-access.ts";

const READ_ONLY_PERMISSIONS = new Set([
  "planning.cycles.read",
  "planning.models.read",
  "planning.versions.read",
  "planning.budget.read",
  "planning.budget_control.read",
  "planning.workforce.read",
  "planning.workforce.sensitive.read",
  "planning.capex.read",
  "planning.variance.read",
  "planning.export",
  "planning.audit.read"
]);

export interface PlanningAccess {
  readonly isReadOnly: boolean;
  readonly canReadCycles: boolean;
  readonly canManageCycles: boolean;
  readonly canReadModels: boolean;
  readonly canManageModels: boolean;
  readonly canReadVersions: boolean;
  readonly canEditVersions: boolean;
  readonly canSubmitVersions: boolean;
  readonly canApproveVersions: boolean;
  readonly canPublishVersions: boolean;
  readonly canCreateScenarios: boolean;
  readonly canShareScenarios: boolean;
  readonly canManageAssumptions: boolean;
  readonly canReadBudget: boolean;
  readonly canManageBudget: boolean;
  readonly canReviseBudget: boolean;
  readonly canTransferBudget: boolean;
  readonly canApproveTransfer: boolean;
  readonly canReadBudgetControl: boolean;
  readonly canRequestOverride: boolean;
  readonly canApproveOverride: boolean;
  readonly canReadWorkforce: boolean;
  readonly canReadSensitiveWorkforce: boolean;
  readonly canReadCapex: boolean;
  readonly canManageCapex: boolean;
  readonly canReadVariance: boolean;
  readonly canRefreshActuals: boolean;
  readonly canExport: boolean;
  readonly canReadAudit: boolean;
  readonly can: (permission: string) => boolean;
}

export function usePlanningAccess(): PlanningAccess {
  let outletCtx: WorkspaceOutletContext | undefined;
  try {
    outletCtx = useOutletContext<WorkspaceOutletContext | undefined>();
  } catch {
    // rendered outside router context
  }

  const effectiveAccess = outletCtx?.effectiveAccess;
  const permissions = outletCtx?.permissions;
  const isReadOnly = effectiveAccess ? isModuleReadOnly(effectiveAccess, "planning") : false;

  const can = (permission: string): boolean => {
    if (isReadOnly && !READ_ONLY_PERMISSIONS.has(permission)) {
      return false;
    }
    if (effectiveAccess) {
      return hasPermissionInAccess(effectiveAccess, permission);
    }
    if (permissions) {
      return permissions.hasPermission(permission);
    }
    return false;
  };

  return {
    isReadOnly,
    canReadCycles: can("planning.cycles.read"),
    canManageCycles: can("planning.cycles.manage"),
    canReadModels: can("planning.models.read"),
    canManageModels: can("planning.models.manage"),
    canReadVersions: can("planning.versions.read"),
    canEditVersions: can("planning.versions.edit"),
    canSubmitVersions: can("planning.versions.submit"),
    canApproveVersions: can("planning.versions.approve"),
    canPublishVersions: can("planning.versions.publish"),
    canCreateScenarios: can("planning.scenarios.create"),
    canShareScenarios: can("planning.scenarios.share"),
    canManageAssumptions: can("planning.assumptions.manage"),
    canReadBudget: can("planning.budget.read"),
    canManageBudget: can("planning.budget.manage"),
    canReviseBudget: can("planning.budget.revision"),
    canTransferBudget: can("planning.budget.transfer"),
    canApproveTransfer: can("planning.budget.transfer.approve"),
    canReadBudgetControl: can("planning.budget_control.read"),
    canRequestOverride: can("planning.budget_control.override"),
    canApproveOverride: can("planning.budget_control.override.approve"),
    canReadWorkforce: can("planning.workforce.read"),
    canReadSensitiveWorkforce: can("planning.workforce.sensitive.read"),
    canReadCapex: can("planning.capex.read"),
    canManageCapex: can("planning.capex.manage"),
    canReadVariance: can("planning.variance.read"),
    canRefreshActuals: can("planning.actuals.refresh"),
    canExport: can("planning.export"),
    canReadAudit: can("planning.audit.read"),
    can
  };
}

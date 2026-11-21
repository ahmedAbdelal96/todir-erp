export interface EffectiveAccess {
  readonly tenantId: string;
  readonly membershipId: string;
  readonly legalEntityId?: string | null;
  readonly branchId?: string | null;
  readonly isSuper: boolean;
  readonly permissions: readonly string[];
  readonly entitledModules: readonly string[];
  readonly moduleStatuses: Readonly<Record<string, string>>;
  readonly version: string;
}

export interface ExpectedSessionContext {
  readonly tenantId?: string;
  readonly membershipId?: string;
  readonly legalEntityId?: string | null;
  readonly branchId?: string | null;
}

let effectiveAccessAbortController: AbortController | null = null;
let effectiveAccessSequenceCounter = 0;

export async function fetchEffectiveAccess(
  legalEntityId?: string | null,
  branchId?: string | null,
  expectedContext?: ExpectedSessionContext
): Promise<EffectiveAccess> {
  const currentSequence = ++effectiveAccessSequenceCounter;

  // Cancel any in-flight request to prevent stale out-of-order resolution during rapid switching
  if (effectiveAccessAbortController) {
    effectiveAccessAbortController.abort();
  }
  effectiveAccessAbortController = new AbortController();
  const { signal } = effectiveAccessAbortController;

  const params = new URLSearchParams();
  if (legalEntityId) {
    params.set("legalEntityId", legalEntityId);
  }
  if (branchId) {
    params.set("branchId", branchId);
  }
  const queryString = params.toString() ? `?${params.toString()}` : "";

  const response = await fetch(`/api/v1/authorization/effective-access${queryString}`, {
    signal,
    headers: {
      "X-Correlation-Id": crypto.randomUUID(),
      Accept: "application/json"
    }
  });
  if (!response.ok) {
    throw new Error(`effective_access_failed: ${response.status}`);
  }
  const data = (await response.json()) as EffectiveAccess;

  // Guard against sequence races: discard if a newer request was dispatched while waiting
  if (currentSequence !== effectiveAccessSequenceCounter) {
    throw new Error("effective_access_superseded");
  }

  // Validate response payload integrity
  if (
    !data ||
    typeof data !== "object" ||
    typeof data.tenantId !== "string" ||
    typeof data.membershipId !== "string" ||
    typeof data.isSuper !== "boolean" ||
    typeof data.version !== "string" ||
    !Array.isArray(data.permissions) ||
    !Array.isArray(data.entitledModules) ||
    typeof data.moduleStatuses !== "object" ||
    data.moduleStatuses === null
  ) {
    throw new Error("effective_access_invalid_payload");
  }

  // Validate context agreement if expected context is provided
  if (expectedContext) {
    if (expectedContext.tenantId && data.tenantId !== expectedContext.tenantId) {
      throw new Error("effective_access_tenant_mismatch");
    }
    if (expectedContext.membershipId && data.membershipId !== expectedContext.membershipId) {
      throw new Error("effective_access_membership_mismatch");
    }
    const normExpectedLid = expectedContext.legalEntityId || null;
    const normDataLid = data.legalEntityId || null;
    if (normExpectedLid !== normDataLid) {
      throw new Error("effective_access_legal_entity_mismatch");
    }
    const normExpectedBid = expectedContext.branchId || null;
    const normDataBid = data.branchId || null;
    if (normExpectedBid !== normDataBid) {
      throw new Error("effective_access_branch_mismatch");
    }
  }

  return data;
}

export function hasPermissionInAccess(
  access: EffectiveAccess | null | undefined,
  permission: string
): boolean {
  if (!access) return false;
  if (access.isSuper) return true;
  if (access.permissions.includes("*") || access.permissions.includes("admin")) return true;
  if (access.permissions.includes(permission)) return true;
  const dotIndex = permission.indexOf(".");
  if (dotIndex > 0) {
    const wildcard = `${permission.slice(0, dotIndex)}.*`;
    if (access.permissions.includes(wildcard)) return true;
  }
  return false;
}

export function hasAnyPermissionInAccess(
  access: EffectiveAccess | null | undefined,
  ...permissions: readonly string[]
): boolean {
  if (!access) return false;
  if (permissions.length === 0) return true;
  return permissions.some(p => hasPermissionInAccess(access, p));
}

export function hasAllPermissionsInAccess(
  access: EffectiveAccess | null | undefined,
  ...permissions: readonly string[]
): boolean {
  if (!access) return false;
  if (permissions.length === 0) return true;
  return permissions.every(p => hasPermissionInAccess(access, p));
}

export function getOwningModuleForPermission(permission: string): string {
  const dotIndex = permission.indexOf(".");
  if (dotIndex <= 0) return permission.toLowerCase();
  const prefix = permission.slice(0, dotIndex).toLowerCase();
  if (prefix === "assets") return "fixed-assets";
  return prefix;
}

export function isMutationPermission(permission: string): boolean {
  const dotIndex = permission.lastIndexOf(".");
  const action = dotIndex >= 0 ? permission.slice(dotIndex + 1).toLowerCase() : permission.toLowerCase();
  if (
    action === "read" ||
    action === "view" ||
    action === "search" ||
    action === "explain" ||
    permission.endsWith(".read_basic")
  ) {
    return false;
  }
  return true;
}

export function isPermissionEffective(
  access: EffectiveAccess | null | undefined,
  permission: string,
  allowReadOnly = true
): boolean {
  if (!access) return false;
  if (!hasPermissionInAccess(access, permission)) return false;

  const owningModule = getOwningModuleForPermission(permission);
  const isMutation = isMutationPermission(permission);
  const effectiveAllowReadOnly = allowReadOnly && !isMutation;

  return isModuleOperational(access, owningModule, effectiveAllowReadOnly);
}

export type PermissionCondition =
  | string
  | {
      readonly anyOf?: readonly string[];
      readonly allOf?: readonly string[];
    };

export function evaluatePermissionCondition(
  condition: PermissionCondition | undefined,
  access: EffectiveAccess | null | undefined
): boolean {
  if (!condition) return true;
  if (!access) return false;
  if (access.isSuper) return true;
  if (typeof condition === "string") {
    return hasPermissionInAccess(access, condition);
  }
  if (condition.allOf && condition.allOf.length > 0) {
    if (!hasAllPermissionsInAccess(access, ...condition.allOf)) return false;
  }
  if (condition.anyOf && condition.anyOf.length > 0) {
    if (!hasAnyPermissionInAccess(access, ...condition.anyOf)) return false;
  }
  return true;
}

export function evaluateEffectivePermissionCondition(
  condition: PermissionCondition | undefined,
  access: EffectiveAccess | null | undefined,
  allowReadOnly = true
): boolean {
  if (!condition) return true;
  if (!access) return false;
  if (typeof condition === "string") {
    return isPermissionEffective(access, condition, allowReadOnly);
  }
  if (condition.allOf && condition.allOf.length > 0) {
    if (!condition.allOf.every(p => isPermissionEffective(access, p, allowReadOnly))) return false;
  }
  if (condition.anyOf && condition.anyOf.length > 0) {
    if (!condition.anyOf.some(p => isPermissionEffective(access, p, allowReadOnly))) return false;
  }
  return true;
}

export function isModuleOperational(
  access: EffectiveAccess | null | undefined,
  moduleId: string,
  allowReadOnly = true
): boolean {
  if (!access) return false;

  // System surfaces are always operational
  const normalized = moduleId.toLowerCase();
  if (
    normalized === "home" ||
    normalized === "settings" ||
    normalized === "preview" ||
    normalized === "domain-settings" ||
    normalized === "workspace" ||
    normalized === "identity" ||
    normalized === "users"
  ) {
    return true;
  }

  // Commercial entitlement check (fail closed: empty or missing entitledModules rejects access)
  if (!access.entitledModules || access.entitledModules.length === 0) {
    return false;
  }
  if (!access.entitledModules.some(m => m.toLowerCase() === normalized)) {
    return false;
  }

  // Lifecycle status check
  const rawStatus = access.moduleStatuses?.[moduleId] ?? access.moduleStatuses?.[normalized];
  if (rawStatus) {
    const status = rawStatus.toLowerCase();

    // Inactive / unready / disabled statuses
    if (
      status === "requested" ||
      status === "validating" ||
      status === "provisioning" ||
      status === "awaitinggolive" ||
      status === "disabled" ||
      status === "failed" ||
      status === "deactivated"
    ) {
      return false;
    }

    // Historical read-only / wind-down statuses
    if (status === "readonly" || status === "winddown") {
      return allowReadOnly;
    }
  }

  // Default for entitled modules without a disabling instance status is Active
  return true;
}

export function isModuleReadOnly(
  access: EffectiveAccess | null | undefined,
  moduleId: string
): boolean {
  if (!access) return false;
  const rawStatus = access.moduleStatuses?.[moduleId] ?? access.moduleStatuses?.[moduleId.toLowerCase()];
  if (!rawStatus) return false;
  const status = rawStatus.toLowerCase();
  return status === "readonly" || status === "winddown";
}

export function canPerformAction(
  access: EffectiveAccess | null | undefined,
  permission: string,
  moduleId?: string
): boolean {
  if (!access) return false;
  const targetModule = moduleId ?? getOwningModuleForPermission(permission);
  if (isModuleReadOnly(access, targetModule)) return false;
  return isPermissionEffective(access, permission, false);
}

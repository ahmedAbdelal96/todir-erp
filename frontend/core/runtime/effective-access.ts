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

export async function fetchEffectiveAccess(): Promise<EffectiveAccess> {
  const response = await fetch("/api/v1/authorization/effective-access", {
    headers: {
      "X-Correlation-Id": crypto.randomUUID(),
      Accept: "application/json"
    }
  });
  if (!response.ok) {
    throw new Error(`effective_access_failed: ${response.status}`);
  }
  return (await response.json()) as EffectiveAccess;
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

export function isModuleOperational(
  access: EffectiveAccess | null | undefined,
  moduleId: string
): boolean {
  if (!access) return false;
  if (access.isSuper) return true;
  if (moduleId === "home" || moduleId === "settings" || moduleId === "preview" || moduleId === "domain-settings") {
    return true;
  }
  if (access.entitledModules && access.entitledModules.length > 0) {
    if (!access.entitledModules.some(m => m.toLowerCase() === moduleId.toLowerCase())) {
      return false;
    }
  }
  const status = access.moduleStatuses?.[moduleId];
  if (status && status.toLowerCase() === "deactivated") {
    return false;
  }
  return true;
}

/**
 * Structured Query Key Factory guaranteeing cache isolation per tenant and legal entity.
 */
export const queryKeyFactory = {
  all: ["todir"] as const,
  tenant: (tenantId: string) => [...queryKeyFactory.all, "tenant", tenantId] as const,
  entity: (tenantId: string, entityId: string) =>
    [...queryKeyFactory.tenant(tenantId), "entity", entityId] as const,
  module: (tenantId: string, entityId: string, moduleName: string) =>
    [...queryKeyFactory.entity(tenantId, entityId), "module", moduleName] as const,
  resource: (
    tenantId: string,
    entityId: string,
    moduleName: string,
    resourceName: string
  ) =>
    [
      ...queryKeyFactory.module(tenantId, entityId, moduleName),
      "resource",
      resourceName
    ] as const,
  list: (
    tenantId: string,
    entityId: string,
    moduleName: string,
    resourceName: string,
    params?: unknown
  ) =>
    [
      ...queryKeyFactory.resource(tenantId, entityId, moduleName, resourceName),
      "list",
      params ?? {}
    ] as const,
  detail: (
    tenantId: string,
    entityId: string,
    moduleName: string,
    resourceName: string,
    id: string
  ) =>
    [
      ...queryKeyFactory.resource(tenantId, entityId, moduleName, resourceName),
      "detail",
      id
    ] as const
  ,userNavigation: (tenantId: string, identityId: string) =>
    [...queryKeyFactory.tenant(tenantId), "user-navigation", identityId] as const
};

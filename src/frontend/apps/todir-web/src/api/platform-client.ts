// Generated from docs/api/platform-openapi.json. Do not edit manually.
export const platformApiPaths = [
  "/api/v1/auth/csrf",
  "/api/v1/auth/login",
  "/api/v1/auth/logout",
  "/api/v1/auth/mfa/enroll",
  "/api/v1/auth/sessions/revoke",
  "/api/v1/authorization/check",
  "/api/v1/context/organization",
  "/api/v1/context/tenant",
  "/api/v1/messaging/replay",
  "/api/v1/modules",
  "/api/v1/sample/items",
  "/api/v1/session",
  "/api/v1/session/context",
  "/api/v1/tenants/{tenantId}/audit",
  "/api/v1/tenants/{tenantId}/entitlement",
  "/api/v1/tenants/{tenantId}/memberships",
  "/api/v1/tenants/{tenantId}/memberships/invitations",
  "/api/v1/tenants/{tenantId}/memberships/invitations/accept",
  "/api/v1/tenants/{tenantId}/memberships/{membershipId}/suspend",
  "/api/v1/tenants/{tenantId}/modules",
  "/api/v1/tenants/{tenantId}/organization",
  "/api/v1/tenants/{tenantId}/roles",
  "/api/v1/tenants/{tenantId}/settings"
] as const;

export type PlatformApiPath = (typeof platformApiPaths)[number];

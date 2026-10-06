// Generated from docs/api/platform-openapi.json. Do not edit manually.
export const platformApiPaths = [
  "/api/v1/auth/csrf",
  "/api/v1/auth/login",
  "/api/v1/auth/logout",
  "/api/v1/auth/mfa/enroll",
  "/api/v1/auth/sessions/revoke",
  "/api/v1/authorization/check",
  "/api/v1/context/tenant",
  "/api/v1/modules",
  "/api/v1/session"
] as const;

export type PlatformApiPath = (typeof platformApiPaths)[number];

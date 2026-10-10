import { httpClient } from "../../../core/api/http-client.ts";
import { requestCsrf } from "../../../core/api/auth-service.ts";
import type {
  UserAccountSummary,
  CreatedUserAccount,
  ResetPasswordResult,
  RoleSummary,
  LoginDomainInfo,
  CreateUserPayload,
  ChangeLoginDomainPayload,
  ListUsersParams
} from "../types.ts";

export async function fetchUsers(
  tenantId: string,
  params: ListUsersParams = {}
): Promise<UserAccountSummary[]> {
  const query = new URLSearchParams();
  if (params.search) query.set("search", params.search);
  if (params.active !== undefined) query.set("active", String(params.active));
  if (params.offset !== undefined) query.set("offset", String(params.offset));
  if (params.limit !== undefined) query.set("limit", String(params.limit));

  const qs = query.toString();
  const url = `/api/v1/tenants/${tenantId}/users${qs ? `?${qs}` : ""}`;
  return httpClient<UserAccountSummary[]>(url, { tenantId });
}

export async function fetchUser(
  tenantId: string,
  identityId: string
): Promise<UserAccountSummary> {
  return httpClient<UserAccountSummary>(`/api/v1/tenants/${tenantId}/users/${identityId}`, {
    tenantId
  });
}

export async function createUser(
  tenantId: string,
  payload: CreateUserPayload
): Promise<CreatedUserAccount> {
  const csrf = await requestCsrf();
  return httpClient<CreatedUserAccount>(`/api/v1/tenants/${tenantId}/users`, {
    method: "POST",
    tenantId,
    headers: csrf ? { "X-CSRF-TOKEN": csrf } : {},
    body: JSON.stringify(payload)
  });
}

export async function resetUserPassword(
  tenantId: string,
  identityId: string
): Promise<ResetPasswordResult> {
  const csrf = await requestCsrf();
  return httpClient<ResetPasswordResult>(
    `/api/v1/tenants/${tenantId}/users/${identityId}/password-reset`,
    {
      method: "POST",
      tenantId,
      headers: csrf ? { "X-CSRF-TOKEN": csrf } : {}
    }
  );
}

export async function deactivateUser(
  tenantId: string,
  identityId: string
): Promise<void> {
  const csrf = await requestCsrf();
  return httpClient<void>(`/api/v1/tenants/${tenantId}/users/${identityId}/deactivate`, {
    method: "POST",
    tenantId,
    headers: csrf ? { "X-CSRF-TOKEN": csrf } : {}
  });
}

export async function reactivateUser(
  tenantId: string,
  identityId: string
): Promise<void> {
  const csrf = await requestCsrf();
  return httpClient<void>(`/api/v1/tenants/${tenantId}/users/${identityId}/reactivate`, {
    method: "POST",
    tenantId,
    headers: csrf ? { "X-CSRF-TOKEN": csrf } : {}
  });
}

export async function fetchLoginDomain(tenantId: string): Promise<LoginDomainInfo> {
  return httpClient<LoginDomainInfo>(`/api/v1/tenants/${tenantId}/login-domain`, {
    tenantId
  });
}

export async function updateLoginDomain(
  tenantId: string,
  payload: ChangeLoginDomainPayload
): Promise<LoginDomainInfo> {
  const csrf = await requestCsrf();
  return httpClient<LoginDomainInfo>(`/api/v1/tenants/${tenantId}/login-domain`, {
    method: "PUT",
    tenantId,
    headers: csrf ? { "X-CSRF-TOKEN": csrf } : {},
    body: JSON.stringify(payload)
  });
}

export async function fetchRoles(
  tenantId: string,
  offset = 0,
  limit = 100
): Promise<RoleSummary[]> {
  const query = new URLSearchParams({ offset: String(offset), limit: String(limit) });
  return httpClient<RoleSummary[]>(`/api/v1/tenants/${tenantId}/roles?${query.toString()}`, {
    tenantId
  });
}

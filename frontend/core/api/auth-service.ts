import { httpClient, resetCsrfToken, type RequestOptions } from "./http-client.ts";

export type AuthNextStep = "Authenticated" | "ChangePassword" | "VerifyMfa" | "Rejected";

export interface AuthLoginResponse {
  readonly state: AuthNextStep;
  readonly authenticated?: boolean | undefined;
  readonly tenantId?: string | undefined;
  readonly challengeToken?: string | undefined;
  readonly errorCode?: string | undefined;
}

export interface SessionSnapshot {
  readonly authenticated: boolean;
  readonly tenantId: string;
  readonly context?: {
    readonly legalEntities: ReadonlyArray<{ readonly id: string; readonly code: string; readonly name: string }>;
    readonly branches: ReadonlyArray<{ readonly id: string; readonly legalEntityId: string; readonly code: string; readonly name: string }>;
  } | undefined;
}

let cachedCsrfToken: string | null = null;

/**
 * Requests and caches a CSRF token for mutating requests.
 */
export async function requestCsrf(): Promise<string> {
  if (cachedCsrfToken) return cachedCsrfToken;
  try {
    const response = await fetch("/api/v1/auth/csrf", {
      headers: { "X-Correlation-Id": crypto.randomUUID() }
    });
    if (!response.ok) throw new Error("csrf.failed");
    const data = (await response.json()) as { token?: string };
    if (data.token) {
      cachedCsrfToken = data.token;
      return data.token;
    }
    throw new Error("csrf.missing");
  } catch {
    return "";
  }
}

export function clearCsrfToken(): void {
  cachedCsrfToken = null;
  resetCsrfToken();
}

/**
 * Initial login attempt with email and password.
 * Tenant is server-derived from login email domain.
 */
export async function loginUser(email: string, password: string): Promise<AuthLoginResponse> {
  const csrf = await requestCsrf();
  const options: RequestOptions = {
    method: "POST",
    headers: csrf ? { "X-CSRF-TOKEN": csrf } : {},
    body: JSON.stringify({ email, password })
  };

  const result = await httpClient<AuthLoginResponse>("/api/v1/auth/login", options);
  clearCsrfToken();
  return result;
}

/**
 * Sets initial password for new user accounts requiring password rotation.
 */
export async function changeInitialPassword(
  challengeToken: string,
  newPassword: string
): Promise<AuthLoginResponse> {
  const csrf = await requestCsrf();
  const options: RequestOptions = {
    method: "POST",
    headers: csrf ? { "X-CSRF-TOKEN": csrf } : {},
    body: JSON.stringify({ challengeToken, newPassword })
  };

  return httpClient<AuthLoginResponse>("/api/v1/auth/password/initial", options);
}

/**
 * Verifies 6-digit TOTP / Authenticator code during progressive MFA step.
 */
export async function verifyMfa(
  challengeToken: string,
  code: string
): Promise<AuthLoginResponse> {
  const csrf = await requestCsrf();
  const options: RequestOptions = {
    method: "POST",
    headers: csrf ? { "X-CSRF-TOKEN": csrf } : {},
    body: JSON.stringify({ challengeToken, code })
  };

  return httpClient<AuthLoginResponse>("/api/v1/auth/mfa/verify", options);
}

/**
 * Signs out the current user session and clears CSRF cache.
 */
export async function logoutUser(): Promise<void> {
  const csrf = await requestCsrf();
  clearCsrfToken();
  try {
    await httpClient<void>("/api/v1/auth/logout", {
      method: "POST",
      headers: csrf ? { "X-CSRF-TOKEN": csrf } : {}
    });
  } catch {
    // If logout endpoint fails, session cookie will still be cleared locally
  }
}

/**
 * Loads the active server-derived tenant and organization context.
 */
export async function loadSessionContext(): Promise<SessionSnapshot> {
  return httpClient<SessionSnapshot>("/api/v1/session/context");
}

import type { ApiProblemDetails } from "../../components/forms/server-error-mapper.ts";

export interface RequestOptions extends Omit<RequestInit, "headers"> {
  readonly headers?: Record<string, string>;
  readonly tenantId?: string;
  readonly legalEntityId?: string;
  readonly correlationId?: string;
  readonly idempotencyKey?: string;
  readonly etag?: string;
}

export class ApiError extends Error {
  readonly status: number;
  readonly problemDetails?: ApiProblemDetails | undefined;

  constructor(status: number, message: string, problemDetails?: ApiProblemDetails | undefined) {
    super(message);
    this.name = "ApiError";
    this.status = status;
    this.problemDetails = problemDetails;
  }
}

let cachedCsrfToken: string | null = null;

export async function fetchCsrfToken(): Promise<string> {
  if (cachedCsrfToken) return cachedCsrfToken;
  try {
    const res = await fetch("/api/v1/auth/csrf", {
      headers: { "X-Correlation-Id": generateKey() }
    });
    if (!res.ok) return "";
    const data = (await res.json()) as { token?: string };
    if (data.token) {
      cachedCsrfToken = data.token;
      return data.token;
    }
  } catch {
    // ignore
  }
  return "";
}

export function resetCsrfToken(): void {
  cachedCsrfToken = null;
}

/**
 * Creates a unique correlation ID or idempotency key.
 */
export function generateKey(): string {
  if (typeof crypto !== "undefined" && typeof crypto.randomUUID === "function") {
    return crypto.randomUUID();
  }
  return `${Date.now()}-${Math.random().toString(36).slice(2, 9)}`;
}

/**
 * Core HTTP Client for TODIR Platform & Modules.
 */
export async function httpClient<T>(
  url: string,
  options: RequestOptions = {}
): Promise<T> {
  const {
    headers = {},
    tenantId,
    legalEntityId,
    correlationId = generateKey(),
    idempotencyKey,
    etag,
    method = "GET",
    ...rest
  } = options;

  const requestHeaders: Record<string, string> = {
    "Accept": "application/json",
    "Content-Type": "application/json",
    "X-Correlation-Id": correlationId,
    ...headers
  };

  if (tenantId) {
    requestHeaders["X-Tenant-Id"] = tenantId;
  }

  if (legalEntityId) {
    requestHeaders["X-Legal-Entity-Id"] = legalEntityId;
  }

  const isMutative = ["POST", "PUT", "PATCH", "DELETE"].includes(method.toUpperCase());
  if (isMutative) {
    requestHeaders["X-Idempotency-Key"] = idempotencyKey ?? generateKey();
    if (!requestHeaders["X-CSRF-TOKEN"]) {
      const csrf = await fetchCsrfToken();
      if (csrf) {
        requestHeaders["X-CSRF-TOKEN"] = csrf;
      }
    }
  }

  if (etag) {
    requestHeaders["If-Match"] = etag;
  }

  let response = await fetch(url, {
    method,
    headers: requestHeaders,
    ...rest
  });

  const isSafeMethod = ["GET", "HEAD", "OPTIONS"].includes(method.toUpperCase());
  if (response.status === 429 && isSafeMethod) {
    const retryAfter = response.headers.get("Retry-After");
    const delay = retryAfter ? Math.min(Number(retryAfter) * 1000, 3000) : 1500;
    await new Promise((resolve) => setTimeout(resolve, delay));
    response = await fetch(url, {
      method,
      headers: requestHeaders,
      ...rest
    });
  }

  if (!response.ok) {
    let problemDetails: ApiProblemDetails | undefined;
    let message = `API request failed with status ${response.status}`;

    try {
      problemDetails = await response.json();
      if (problemDetails?.code === "security.csrf_invalid" && isMutative) {
        resetCsrfToken();
        const freshCsrf = await fetchCsrfToken();
        if (freshCsrf) {
          requestHeaders["X-CSRF-TOKEN"] = freshCsrf;
          const retryResponse = await fetch(url, {
            method,
            headers: requestHeaders,
            ...rest
          });
          if (retryResponse.ok) {
            if (retryResponse.status === 204) {
              return null as T;
            }
            return (await retryResponse.json()) as T;
          }
        }
      }
      if (problemDetails?.detail) {
        message = problemDetails.detail;
      } else if (problemDetails?.title) {
        message = problemDetails.title;
      }
    } catch {
      // Non-JSON error body
    }

    throw new ApiError(response.status, message, problemDetails);
  }

  if (response.status === 204) {
    return null as T;
  }

  return response.json() as Promise<T>;
}

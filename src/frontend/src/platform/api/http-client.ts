import type { ApiProblemDetails } from "../../design-system/forms/server-error-mapper.ts";

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
  }

  if (etag) {
    requestHeaders["If-Match"] = etag;
  }

  const response = await fetch(url, {
    method,
    headers: requestHeaders,
    ...rest
  });

  if (!response.ok) {
    let problemDetails: ApiProblemDetails | undefined;
    let message = `API request failed with status ${response.status}`;

    try {
      problemDetails = await response.json();
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

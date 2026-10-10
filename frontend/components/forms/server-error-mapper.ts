import type { UseFormSetError, FieldValues, Path } from "react-hook-form";

export interface ApiProblemDetails {
  readonly type?: string;
  readonly title?: string;
  readonly status?: number;
  readonly detail?: string;
  readonly instance?: string;
  readonly errors?: Readonly<Record<string, readonly string[]>>;
  readonly code?: string;
}

/**
 * Maps standard RFC-7807 / TODIR backend validation errors to React Hook Form field errors.
 */
export function mapServerErrorsToForm<T extends FieldValues>(
  error: unknown,
  setError: UseFormSetError<T>,
  setGeneralError?: (msg: string) => void
): boolean {
  if (!error || typeof error !== "object") return false;

  const problem = error as ApiProblemDetails;
  let hasMappedField = false;

  if (problem.errors && typeof problem.errors === "object") {
    for (const [field, messages] of Object.entries(problem.errors)) {
      const message = Array.isArray(messages) ? messages[0] : String(messages);
      if (message) {
        // Lowercase first letter of field name if camelCase matches form
        const normalizedField = (field.charAt(0).toLowerCase() + field.slice(1)) as Path<T>;
        setError(normalizedField, {
          type: "server",
          message
        });
        hasMappedField = true;
      }
    }
  }

  if (problem.detail && setGeneralError) {
    setGeneralError(problem.detail);
  }

  return hasMappedField;
}

import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { mapServerErrorsToForm, type ApiProblemDetails } from "./server-error-mapper.ts";

describe("Form System & Server Error Mapping", () => {
  it("maps RFC-7807 field validation errors to form setError calls", () => {
    const problem: ApiProblemDetails = {
      type: "https://tools.ietf.org/html/rfc7231#section-6.5.1",
      title: "One or more validation errors occurred.",
      status: 400,
      errors: {
        DisplayName: ["Display name is required", "Must be at least 2 characters"],
        PrimaryEmail: ["Invalid email format"]
      }
    };

    const recordedErrors: Record<string, { type: string; message: string }> = {};
    const mockSetError = (field: string, error: { type: string; message: string }) => {
      recordedErrors[field] = error;
    };

    let generalError: string | undefined;
    const mockSetGeneralError = (msg: string) => {
      generalError = msg;
    };

    const mapped = mapServerErrorsToForm(problem, mockSetError as any, mockSetGeneralError);

    assert.equal(mapped, true);
    assert.equal(recordedErrors["displayName"]?.message, "Display name is required");
    assert.equal(recordedErrors["primaryEmail"]?.message, "Invalid email format");
  });

  it("handles problem details without field errors gracefully", () => {
    const problem: ApiProblemDetails = {
      status: 409,
      title: "Conflict",
      detail: "The party with this tax ID already exists in this tenant."
    };

    let generalError: string | undefined;
    const mockSetGeneralError = (msg: string) => {
      generalError = msg;
    };

    const mapped = mapServerErrorsToForm(problem, (() => {}) as any, mockSetGeneralError);

    assert.equal(mapped, false);
    assert.equal(generalError, "The party with this tax ID already exists in this tenant.");
  });

  it("safely ignores non-object or null error inputs", () => {
    const mapped = mapServerErrorsToForm(null, (() => {}) as any);
    assert.equal(mapped, false);
  });
});

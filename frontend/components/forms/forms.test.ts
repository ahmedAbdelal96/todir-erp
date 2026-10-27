import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { mapServerErrorsToForm, type ApiProblemDetails } from "./server-error-mapper.ts";
import {
  saveFormDraft,
  loadFormDraft,
  clearFormDraft,
  setDraftStorageAdapter,
  type DraftStorageAdapter
} from "./draft-storage.ts";

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

  describe("Draft Storage & Recovery System", () => {
    it("saves, loads, and clears form drafts safely", async () => {
      const draftKey = "invoice-draft-test-123";
      const sampleData = { customerId: "cust-1", total: "1500.00", lines: [{ id: "l1", qty: "2" }] };

      await saveFormDraft(draftKey, sampleData);
      const loaded = await loadFormDraft<typeof sampleData>(draftKey);

      assert.ok(loaded);
      assert.deepEqual(loaded.data, sampleData);
      assert.ok(typeof loaded.timestamp === "number");

      await clearFormDraft(draftKey);
      const cleared = await loadFormDraft<typeof sampleData>(draftKey);
      assert.equal(cleared, null);
    });

    it("handles custom draft storage adapters seamlessly", async () => {
      const mockStore = new Map<string, any>();
      const customAdapter: DraftStorageAdapter = {
        saveDraft: (key, data) => { mockStore.set(key, { data, timestamp: 99999 }); },
        loadDraft: (key) => mockStore.get(key) ?? null,
        clearDraft: (key) => { mockStore.delete(key); }
      };

      setDraftStorageAdapter(customAdapter);

      await saveFormDraft("custom-key", { test: true });
      const loaded = await loadFormDraft<{ test: boolean }>("custom-key");
      assert.ok(loaded);
      assert.equal(loaded.data.test, true);
      assert.equal(loaded.timestamp, 99999);

      await clearFormDraft("custom-key");
      const empty = await loadFormDraft("custom-key");
      assert.equal(empty, null);
    });
  });
});

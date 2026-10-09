import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { partyFormSchema } from "../schemas/party-schema.ts";
import { partyApi } from "../api/party-api.ts";

describe("Directory / Party Vertical Slice", () => {
  it("validates party schema for organization with valid roles", () => {
    const validData = {
      partyType: "Organization",
      displayName: "Al-Ahram Industrial LLC",
      identifierType: "tax",
      identifierValue: "100-200-300",
      email: "info@alahram.com",
      roles: ["Customer", "Supplier"]
    };

    const result = partyFormSchema.safeParse(validData);
    assert.equal(result.success, true);
  });

  it("rejects party form without roles", () => {
    const invalidData = {
      partyType: "Person",
      displayName: "John Doe",
      roles: []
    };

    const result = partyFormSchema.safeParse(invalidData);
    assert.equal(result.success, false);
    if (!result.success) {
      assert.ok(result.error.issues.some((i) => i.path.includes("roles")));
    }
  });

  it("rejects party form with invalid email format", () => {
    const invalidData = {
      partyType: "Person",
      displayName: "Jane Doe",
      email: "not-an-email",
      roles: ["Customer"]
    };

    const result = partyFormSchema.safeParse(invalidData);
    assert.equal(result.success, false);
    if (!result.success) {
      assert.ok(result.error.issues.some((i) => i.path.includes("email")));
    }
  });

  it("provides partyApi methods without throwing on import", () => {
    assert.equal(typeof partyApi.list, "function");
    assert.equal(typeof partyApi.getById, "function");
    assert.equal(typeof partyApi.create, "function");
    assert.equal(typeof partyApi.update, "function");
  });
});

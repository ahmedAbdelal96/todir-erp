import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { partyFormSchema } from "../validation/party-schema.ts";
import { normalizeParty, partyApi } from "../services/party-api.ts";
import { auditActionLabel, auditChanges, auditEntityLabel } from "../audit-format.ts";

describe("Directory / Party Vertical Slice", () => {
  it("validates party schema for organization with valid roles", () => {
    const validData = {
      partyType: "Organization",
      displayName: "شركة الأهرام للصناعات المتطورة",
      legalName: "الأهرام للصناعات ذ.م.م",
      identifierType: "TaxId",
      identifierValue: "100-200-300",
      email: "info@alahram.com",
      roles: ["Customer", "Supplier"]
    };

    const result = partyFormSchema.safeParse(validData);
    assert.equal(result.success, true);
  });

  it("validates party schema for individual person with first and last name", () => {
    const validPerson = {
      partyType: "Person",
      displayName: "أحمد محمود النجار",
      firstName: "أحمد",
      middleName: "محمود",
      lastName: "النجار",
      email: "ahmed@example.com",
      phone: "+201012345678",
      roles: ["Customer"]
    };

    const result = partyFormSchema.safeParse(validPerson);
    assert.equal(result.success, true);
  });

  it("rejects organization with person name fields according to domain boundary", () => {
    const invalidOrg = {
      partyType: "Organization",
      displayName: "شركة النيل للتجارة",
      firstName: "أحمد",
      roles: ["Customer"]
    };

    const result = partyFormSchema.safeParse(invalidOrg);
    assert.equal(result.success, false);
    if (!result.success) {
      assert.ok(result.error.issues.some((i) => i.path.includes("firstName")));
    }
  });

  it("accepts party form without roles according to canonical pack", () => {
    const validData = {
      partyType: "Person",
      displayName: "John Doe",
      roles: []
    };

    const result = partyFormSchema.safeParse(validData);
    assert.equal(result.success, true);
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

  it("provides comprehensive partyApi methods covering full backend lifecycle", () => {
    assert.equal(typeof partyApi.list, "function");
    assert.equal(typeof partyApi.getById, "function");
    assert.equal(typeof partyApi.create, "function");
    assert.equal(typeof partyApi.update, "function");
    assert.equal(typeof partyApi.archive, "function");
    assert.equal(typeof partyApi.restore, "function");
    assert.equal(typeof partyApi.addRole, "function");
    assert.equal(typeof partyApi.disableRole, "function");
    assert.equal(typeof partyApi.addIdentifier, "function");
    assert.equal(typeof partyApi.addContactPoint, "function");
    assert.equal(typeof partyApi.addAddress, "function");
    assert.equal(typeof partyApi.getDuplicates, "function");
    assert.equal(typeof partyApi.getReadiness, "function");
  });

  it("normalizes projected list parties before PartyPicker business-context rendering", () => {
    const projectedParty = {
      id: "party-1",
      partyType: "Organization",
      status: "Active",
      displayName: "شركة المنارة للتوريدات",
      roles: [{ code: "Supplier", status: "Active", version: 1 }],
      updatedAt: "2026-01-01T09:00:00Z",
      version: 1
    } as unknown as import("../types.ts").Party;

    const normalized = normalizeParty(projectedParty);
    assert.deepEqual(normalized.identifiers, []);
    assert.deepEqual(normalized.addresses, []);
    assert.deepEqual(normalized.contactPoints, []);
    assert.deepEqual(normalized.roles.map((role) => role.code), ["Supplier"]);
  });

  it("formats returned Directory audit events and omits internal identifiers", () => {
    assert.equal(auditActionLabel("party_identity_changed"), "تعديل بيانات الهوية");
    assert.equal(auditEntityLabel("party"), "سجل الطرف التجاري");
    assert.deepEqual(auditChanges('{"displayName":"شركة النور للتجارة","partyId":"0192e2b3-0000-7000-8000-000000000000","version":3}'), [
      { label: "الاسم المعروض", value: "شركة النور للتجارة" }
    ]);
  });

  it("does not surface malformed audit JSON or raw enum values", () => {
    assert.deepEqual(auditChanges("not-json"), []);
    assert.deepEqual(auditChanges('{"role":"FutureBackendRole","status":"FutureStatus"}'), [
      { label: "الدور التجاري", value: "غير محدد" },
      { label: "الحالة", value: "غير محدد" }
    ]);
  });
});

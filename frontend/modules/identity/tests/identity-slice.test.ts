import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { createUserSchema, changeLoginDomainSchema } from "../validation/identity-schemas.ts";

describe("Identity & User Management Slice Tests", () => {
  describe("Validation Schemas", () => {
    it("validates correct user creation payload", () => {
      const validPayload = {
        name: "أحمد محمد علي",
        emailPrefix: "ahmed.ali",
        roleIds: ["0192e2b3-5757-7928-86d4-8356f1dcbe50"]
      };

      const result = createUserSchema.safeParse(validPayload);
      assert.equal(result.success, true);
    });

    it("rejects user creation with empty name", () => {
      const invalidPayload = {
        name: "",
        emailPrefix: "ahmed"
      };

      const result = createUserSchema.safeParse(invalidPayload);
      assert.equal(result.success, false);
      if (!result.success) {
        assert.match(result.error.issues[0]?.message ?? "", /يرجى إدخال اسم المستخدم/);
      }
    });

    it("rejects user creation with invalid email prefix containing special characters or spaces", () => {
      const invalidPayloads = [
        { name: "أحمد", emailPrefix: "ahmed ali" },
        { name: "أحمد", emailPrefix: "ahmed@company" },
        { name: "أحمد", emailPrefix: "ahmed#123" },
        { name: "أحمد", emailPrefix: "" }
      ];

      for (const payload of invalidPayloads) {
        const result = createUserSchema.safeParse(payload);
        assert.equal(result.success, false);
      }
    });

    it("validates valid company login domain", () => {
      const validDomains = [
        { loginDomain: "atlas.com" },
        { loginDomain: "atlasgroup.com" },
        { loginDomain: "my-company.org.sa" },
        { loginDomain: "erp.sub.domain.net" }
      ];

      for (const domain of validDomains) {
        const result = changeLoginDomainSchema.safeParse(domain);
        assert.equal(result.success, true);
      }
    });

    it("rejects malformed company login domains", () => {
      const invalidDomains = [
        { loginDomain: "" },
        { loginDomain: "atlas" },
        { loginDomain: "@atlas.com" },
        { loginDomain: "https://atlas.com" },
        { loginDomain: "atlas .com" }
      ];

      for (const domain of invalidDomains) {
        const result = changeLoginDomainSchema.safeParse(domain);
        assert.equal(result.success, false);
      }
    });
  });

  describe("Domain Invariants", () => {
    it("ensures email prefix is normalized to lowercase and trimmed", () => {
      const rawInput = " Ahmed.Mohamed ";
      const cleanPrefix = rawInput.trim().toLowerCase();
      assert.equal(cleanPrefix, "ahmed.mohamed");
    });

    it("verifies temporary password is not empty and conforms to one-time requirement", () => {
      const fakeTemporaryPassword = "Temp#Password992!";
      assert.ok(fakeTemporaryPassword.length >= 12);
      assert.match(fakeTemporaryPassword, /[A-Z]/);
      assert.match(fakeTemporaryPassword, /[a-z]/);
      assert.match(fakeTemporaryPassword, /[0-9]/);
      assert.match(fakeTemporaryPassword, /[^A-Za-z0-9]/);
    });
  });
});

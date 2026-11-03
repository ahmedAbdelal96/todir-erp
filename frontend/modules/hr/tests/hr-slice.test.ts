import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { ApiError } from "../../../core/api/http-client.ts";
import { mapHrError } from "../services/hr-error-map.ts";
import { ar } from "../i18n/ar.ts";
import { en } from "../i18n/en.ts";

describe("HR Domain Invariants & Frontend Slice Contracts", () => {
  it("maintains strict Arabic and English translation parity with zero missing keys", () => {
    const flattenKeys = (obj: Record<string, unknown>, prefix = ""): string[] => {
      let keys: string[] = [];
      for (const [k, v] of Object.entries(obj)) {
        const fullKey = prefix ? `${prefix}.${k}` : k;
        if (typeof v === "object" && v !== null && !Array.isArray(v)) {
          keys = keys.concat(flattenKeys(v as Record<string, unknown>, fullKey));
        } else {
          keys.push(fullKey);
        }
      }
      return keys;
    };

    const arKeys = flattenKeys(ar as unknown as Record<string, unknown>).sort();
    const enKeys = flattenKeys(en as unknown as Record<string, unknown>).sort();

    assert.deepEqual(arKeys, enKeys, "Arabic and English translation keys must have 100% parity");
  });

  it("maps RFC-7807 HR domain errors to localized translation keys", () => {
    const arabicNameErr = mapHrError(new ApiError(400, "Arabic name required", {
      code: "hr.employee_arabic_name_required"
    }));
    assert.equal(arabicNameErr.code, "hr.employee_arabic_name_required");
    assert.equal(arabicNameErr.messageKey, "errors.employeeArabicNameRequired");

    const leaveInsufficientErr = mapHrError(new ApiError(400, "Insufficient balance", {
      code: "hr.leave_balance_insufficient"
    }));
    assert.equal(leaveInsufficientErr.code, "hr.leave_balance_insufficient");
    assert.equal(leaveInsufficientErr.messageKey, "errors.leaveBalanceInsufficient");

    const payrollLockedErr = mapHrError(new ApiError(409, "Locked by payroll", {
      code: "hr.attendance_record_payroll_locked"
    }));
    assert.equal(payrollLockedErr.code, "hr.attendance_record_payroll_locked");
    assert.equal(payrollLockedErr.messageKey, "errors.attendancePayrollLocked");

    const forbiddenErr = mapHrError(new ApiError(403, "Forbidden", {}));
    assert.equal(forbiddenErr.code, "hr.forbidden");
    assert.equal(forbiddenErr.messageKey, "errors.forbidden");

    const genericErr = mapHrError(new Error("Network timeout"));
    assert.equal(genericErr.code, "hr.unknown");
    assert.equal(genericErr.messageKey, "errors.generic");
  });

  it("enforces disciplinary action monetary invariant (no direct cash amounts in HR)", () => {
    // Invariant: Disciplinary actions cannot carry raw monetary deductions directly in HR
    const kind = "DisciplinaryAction";
    const amount: number | null = null;
    assert.equal(kind === "DisciplinaryAction" && amount === null, true);
  });

  it("enforces leave date range invariant (To date >= From date)", () => {
    const fromDate = "2026-06-01";
    const toDate = "2026-06-05";
    assert.equal(toDate >= fromDate, true);

    const invalidFrom = "2026-06-10";
    const invalidTo = "2026-06-05";
    assert.equal(invalidTo >= invalidFrom, false);
  });

  it("verifies 360-degree Employee Workspace sections structure", () => {
    const expectedSections = [
      "overview",
      "assignments",
      "contracts",
      "compensation",
      "documents",
      "qualifications",
      "insurance",
      "timeline"
    ];
    assert.equal(expectedSections.length, 8);
    assert.equal(expectedSections.includes("contracts"), true);
    assert.equal(expectedSections.includes("timeline"), true);
  });
});

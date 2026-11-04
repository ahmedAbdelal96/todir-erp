import { test, describe } from "node:test";
import assert from "node:assert/strict";
import { mapPayrollError } from "../services/payroll-error-map.ts";
import { payrollAr } from "../i18n/ar.ts";
import { payrollEn } from "../i18n/en.ts";

describe("Payroll Slice Contracts & Invariants", () => {
  test("RFC-7807 error mapper translates known backend codes to natural messages", () => {
    const error1 = mapPayrollError({ code: "payroll.run_not_ready" });
    assert.equal(error1.messageKey, "errors.runNotReady");
    assert.ok(error1.defaultMessage.length > 0);

    const error2 = mapPayrollError({ code: "payroll.run_already_finalized" });
    assert.equal(error2.messageKey, "errors.runAlreadyFinalized");

    const error3 = mapPayrollError({ code: "payroll.settings_version_conflict" });
    assert.equal(error3.messageKey, "errors.settingsVersionConflict");

    const unknown = mapPayrollError({ code: "payroll.unexpected_custom_code" });
    assert.equal(unknown.messageKey, "errors.operationFailed");
  });

  test("1:1 Arabic and English translation parity for payroll resources", () => {
    const getKeys = (obj: Record<string, any>, prefix = ""): string[] => {
      let keys: string[] = [];
      for (const [k, v] of Object.entries(obj)) {
        const fullKey = prefix ? `${prefix}.${k}` : k;
        if (typeof v === "object" && v !== null) {
          keys = keys.concat(getKeys(v, fullKey));
        } else {
          keys.push(fullKey);
        }
      }
      return keys;
    };

    const arKeys = new Set(getKeys(payrollAr));
    const enKeys = new Set(getKeys(payrollEn));

    assert.equal(arKeys.size, enKeys.size, `Keys count mismatch: AR=${arKeys.size}, EN=${enKeys.size}`);
    for (const key of arKeys) {
      assert.ok(enKeys.has(key), `Missing EN key: ${key}`);
    }
  });

  test("Statutory mode invariant: NotManaged is explicit and distinct", () => {
    assert.equal(payrollAr.statutoryModes.notManaged, "غير مدار");
    assert.equal(payrollEn.statutoryModes.notManaged, "Not Managed");
    assert.notEqual(payrollAr.statutoryModes.notManaged, payrollAr.statutoryModes.manual);
    assert.notEqual(payrollAr.statutoryModes.notManaged, payrollAr.statutoryModes.automatic);
  });
});

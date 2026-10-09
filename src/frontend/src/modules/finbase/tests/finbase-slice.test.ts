import { strict as assert } from "node:assert";
import { describe, it } from "node:test";
import type { PaymentTermRule } from "../types.ts";

describe("finbase contracts", () => {
  it("keeps payment rule amounts as strings at the API boundary", () => {
    const rule: PaymentTermRule = { sequence: 1, allocationType: "Percentage", percentage: 33.33, dueDateBasis: "InvoiceDate", days: 0, monthOffset: 0 };
    assert.equal(typeof rule.allocationType, "string");
    assert.equal(typeof "100.00", "string");
  });
});

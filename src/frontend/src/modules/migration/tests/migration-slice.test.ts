import { describe, it } from "node:test";
import assert from "node:assert/strict";
import type { ImportJob, FieldMapping } from "../public.ts";

describe("F21 — Import Wizard Reference Validation", () => {
  it("structures multi-step field mappings safely", () => {
    const mappings: FieldMapping = {
      customer_name: "displayName",
      vat_number: "taxId",
      type: "partyType"
    };

    assert.equal(mappings.customer_name, "displayName");
    assert.equal(mappings.vat_number, "taxId");
    assert.equal(mappings.type, "partyType");
  });

  it("handles partial success status and reconciliation reporting", () => {
    const job: ImportJob = {
      id: "job-001",
      entityType: "Parties",
      fileName: "customers_october.csv",
      totalRows: 1000,
      processedRows: 1000,
      successCount: 980,
      errorCount: 20,
      status: "PartialSuccess",
      errors: [
        {
          row: 42,
          field: "taxId",
          code: "INVALID_FORMAT",
          message: "الرقم الضريبي غير صالح",
          severity: "error"
        }
      ],
      createdAt: "2026-10-09T08:00:00Z",
      completedAt: "2026-10-09T08:02:15Z"
    };

    assert.equal(job.status, "PartialSuccess");
    assert.equal(job.successCount, 980);
    assert.equal(job.errorCount, 20);
    assert.equal(job.errors.length, 1);
    assert.equal(job.errors[0]?.row, 42);
  });
});

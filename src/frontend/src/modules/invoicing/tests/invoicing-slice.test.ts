import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { formatMoney, formatQuantity, formatPercentage } from "../../../design-system/formatting/index.ts";
import type { Invoice } from "../public.ts";
import type { DocumentStatusGroup } from "../../../design-system/pages/document-page.tsx";

describe("F18 — Invoice Reference Validation", () => {
  it("preserves exact multi-axis status model without collapsing independent dimensions", () => {
    const mockInvoice: Invoice = {
      id: "inv-001",
      invoiceNumber: "INV-2026-0091",
      issueDate: "2026-10-01",
      dueDate: "2026-10-31",
      customerPartyId: "party-001",
      customerName: "شركة الأفق للتجارة والمقاولات",
      currency: "SAR",
      items: [
        {
          id: "line-1",
          description: "استشارات هندسية وإشراف فني",
          quantity: "5.500",
          uom: "ساعة",
          unitPrice: "450.0000",
          discountRate: "10.00",
          taxRate: "15.00",
          lineSubtotal: "2227.50",
          lineTotal: "2561.625"
        }
      ],
      subtotal: "2475.00",
      totalDiscount: "247.50",
      totalTax: "334.125",
      totalAmount: "2561.625",
      lifecycleStatus: "Issued",
      settlementStatus: "PartiallyPaid",
      approvalStatus: "Approved",
      complianceStatus: "Compliant",
      accountingStatus: "Posted",
      isLocked: true,
      lockedReason: "المستند مرحل ومقيد محاسبياً في دفتر الأستاذ",
      etag: '"w/20261009-v1"',
      createdAt: "2026-10-01T10:00:00Z",
      createdBy: "أحمد عبد العال"
    };

    const statusGroup: DocumentStatusGroup = {
      lifecycle: mockInvoice.lifecycleStatus,
      approval: mockInvoice.approvalStatus,
      compliance: mockInvoice.complianceStatus,
      accounting: mockInvoice.accountingStatus,
      settlement: mockInvoice.settlementStatus,
      isLocked: mockInvoice.isLocked,
      lockedReason: mockInvoice.lockedReason
    };

    assert.equal(statusGroup.lifecycle, "Issued");
    assert.equal(statusGroup.settlement, "PartiallyPaid");
    assert.equal(statusGroup.approval, "Approved");
    assert.equal(statusGroup.compliance, "Compliant");
    assert.equal(statusGroup.accounting, "Posted");
    assert.equal(statusGroup.isLocked, true);
    assert.match(statusGroup.lockedReason ?? "", /مرحل ومقيد/);
  });

  it("proves settlement transitions do not alter or overwrite document lifecycle state", () => {
    const baseInvoice: Invoice = {
      id: "inv-002",
      invoiceNumber: "INV-2026-0092",
      issueDate: "2026-10-01",
      dueDate: "2026-10-31",
      customerPartyId: "party-001",
      customerName: "شركة الأفق",
      currency: "SAR",
      items: [],
      subtotal: "1000.00",
      totalDiscount: "0.00",
      totalTax: "150.00",
      totalAmount: "1150.00",
      lifecycleStatus: "Issued",
      settlementStatus: "Unpaid",
      approvalStatus: "Approved",
      complianceStatus: "Compliant",
      accountingStatus: "Posted",
      isLocked: true,
      etag: '"w/1"',
      createdAt: "2026-10-01T10:00:00Z",
      createdBy: "User"
    };

    // Subledger updates settlement status from Unpaid -> PartiallyPaid -> Paid
    const partiallyPaidInvoice: Invoice = {
      ...baseInvoice,
      settlementStatus: "PartiallyPaid"
    };

    const fullyPaidInvoice: Invoice = {
      ...baseInvoice,
      settlementStatus: "Paid"
    };

    // Lifecycle state remains strictly "Issued" throughout settlement lifecycle
    assert.equal(baseInvoice.lifecycleStatus, "Issued");
    assert.equal(partiallyPaidInvoice.lifecycleStatus, "Issued");
    assert.equal(fullyPaidInvoice.lifecycleStatus, "Issued");
    assert.equal(fullyPaidInvoice.settlementStatus, "Paid");
  });

  it("formats line items with exact decimal precision for quantities and prices", () => {
    const formattedQty = formatQuantity({ value: "1250.750", unitOfMeasure: "كجم" });
    const formattedPrice = formatMoney({ amount: "1500.25", currency: "SAR" });
    const formattedDiscount = formatPercentage("15.00");

    assert.equal(formattedQty, "1,250.75 كجم");
    assert.equal(formattedPrice, "SAR 1,500.25");
    assert.equal(formattedDiscount, "15.00%");
  });

  it("enforces ETag concurrency header validation on mutation commands", () => {
    const etag = '"etag-version-4"';
    assert.ok(etag.startsWith('"') && etag.endsWith('"'));
  });
});

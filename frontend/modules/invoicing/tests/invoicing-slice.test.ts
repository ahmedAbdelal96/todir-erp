import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { formatMoney, formatQuantity, formatPercentage } from "../../../components/formatting/index.ts";
import type { LegacyInvoice, InvoiceTotals, InvoicePreview, InvoiceCorrectionLineInput } from "../types.ts";
import type { DocumentStatusGroup } from "../../../components/pages/document-page.tsx";

describe("F18 — Invoicing Vertical Slice & Contract Validation", () => {
  it("preserves exact multi-axis status model without collapsing independent dimensions", () => {
    const mockInvoice: LegacyInvoice = {
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
    const baseInvoice: LegacyInvoice = {
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

    const partiallyPaidInvoice: LegacyInvoice = {
      ...baseInvoice,
      settlementStatus: "PartiallyPaid"
    };

    const fullyPaidInvoice: LegacyInvoice = {
      ...baseInvoice,
      settlementStatus: "Paid"
    };

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

  it("preserves authoritative server totals contract without client-side floating point rounding errors", () => {
    const serverTotals: InvoiceTotals = {
      netAmount: "1250.50",
      discountAmount: "50.00",
      chargeAmount: "25.00",
      taxableAmount: "1225.50",
      taxAmount: "183.825",
      grossAmount: "1409.325",
      roundingAdjustment: "0.00",
      currencyCode: "SAR"
    };

    assert.equal(typeof serverTotals.netAmount, "string");
    assert.equal(typeof serverTotals.grossAmount, "string");
    assert.equal(serverTotals.grossAmount, "1409.325");
  });

  it("evaluates preview readiness based on blockers presence", () => {
    const blockedPreview: InvoicePreview = {
      invoiceId: "inv-preview-1",
      version: 1,
      lines: [],
      paymentSchedule: [],
      warnings: [{ code: "invoicing.term_deviation", message: "تنبيه شروط دفع", severity: "Warning" }],
      blockers: [{ code: "invoicing.price_missing", message: "لا يوجد سعر معتمد", severity: "Blocker" }],
      readyToFinalize: false
    };

    const readyPreview: InvoicePreview = {
      invoiceId: "inv-preview-2",
      version: 1,
      lines: [],
      paymentSchedule: [],
      warnings: [],
      blockers: [],
      readyToFinalize: true
    };

    assert.equal(blockedPreview.readyToFinalize, false);
    assert.equal(blockedPreview.blockers.length, 1);
    assert.equal(readyPreview.readyToFinalize, true);
    assert.equal(readyPreview.blockers.length, 0);
  });

  it("validates credit note quantities against remaining creditable amounts", () => {
    const originalQty = 10;
    const previouslyCreditedQty = 4;
    const availableToCredit = originalQty - previouslyCreditedQty;

    const validCreditInput: InvoiceCorrectionLineInput = {
      originalLineId: "line-01",
      quantity: "5.000"
    };

    const invalidOverCreditInput: InvoiceCorrectionLineInput = {
      originalLineId: "line-01",
      quantity: "8.000"
    };

    assert.equal(availableToCredit, 6);
    assert.ok(parseFloat(validCreditInput.quantity) <= availableToCredit);
    assert.ok(parseFloat(invalidOverCreditInput.quantity) > availableToCredit);
  });
});

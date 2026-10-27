import { describe, it } from "node:test";
import assert from "node:assert/strict";

describe("Shared Primitives & Forms Logic Validation", () => {
  describe("Pagination Range & Edge Case Calculations", () => {
    function computePageRange(page: number, totalPages: number): (number | "ellipsis")[] {
      const safePage = Math.max(1, Math.min(page, Math.max(1, totalPages)));
      if (totalPages <= 7) {
        return Array.from({ length: totalPages }, (_, i) => i + 1);
      }
      if (safePage <= 4) {
        return [1, 2, 3, 4, 5, "ellipsis", totalPages];
      }
      if (safePage >= totalPages - 3) {
        return [1, "ellipsis", totalPages - 4, totalPages - 3, totalPages - 2, totalPages - 1, totalPages];
      }
      return [1, "ellipsis", safePage - 1, safePage, safePage + 1, "ellipsis", totalPages];
    }

    it("handles totalItems = 0 and single page gracefully", () => {
      const range = computePageRange(1, 1);
      assert.deepEqual(range, [1]);
    });

    it("handles small page counts without ellipsis", () => {
      const range = computePageRange(3, 5);
      assert.deepEqual(range, [1, 2, 3, 4, 5]);
    });

    it("handles beginning of large page count with trailing ellipsis", () => {
      const range = computePageRange(2, 20);
      assert.deepEqual(range, [1, 2, 3, 4, 5, "ellipsis", 20]);
    });

    it("handles middle of large page count with leading and trailing ellipsis", () => {
      const range = computePageRange(10, 20);
      assert.deepEqual(range, [1, "ellipsis", 9, 10, 11, "ellipsis", 20]);
    });

    it("handles end of large page count with leading ellipsis", () => {
      const range = computePageRange(19, 20);
      assert.deepEqual(range, [1, "ellipsis", 16, 17, 18, 19, 20]);
    });

    it("corrects out-of-bounds page requests safely", () => {
      const rangeNegative = computePageRange(-5, 10);
      assert.equal(rangeNegative[0], 1);

      const rangeExceeded = computePageRange(999, 10);
      assert.equal(rangeExceeded[rangeExceeded.length - 1], 10);
    });
  });

  describe("ProgressBar Calculation Logic", () => {
    function computeProgress(value: number | undefined, maxValue = 100) {
      if (value === undefined) return { isIndeterminate: true, percentage: 0 };
      const clamped = Math.max(0, Math.min(value, maxValue));
      const percentage = Math.round((clamped / maxValue) * 100);
      return { isIndeterminate: false, percentage, clamped };
    }

    it("calculates percentage accurately for standard values", () => {
      const res = computeProgress(45, 100);
      assert.equal(res.percentage, 45);
      assert.equal(res.isIndeterminate, false);
    });

    it("clamps negative values to 0", () => {
      const res = computeProgress(-10, 100);
      assert.equal(res.percentage, 0);
      assert.equal(res.clamped, 0);
    });

    it("clamps values exceeding maxValue to 100%", () => {
      const res = computeProgress(150, 100);
      assert.equal(res.percentage, 100);
      assert.equal(res.clamped, 100);
    });

    it("flags indeterminate state when value is undefined", () => {
      const res = computeProgress(undefined);
      assert.equal(res.isIndeterminate, true);
    });
  });

  describe("ValidationSummary Error Normalization", () => {
    function normalizeFormErrors(errors: readonly { field?: string; message: string }[] | Record<string, string | undefined>) {
      if (Array.isArray(errors)) return errors;
      return Object.entries(errors)
        .filter(([_, msg]) => Boolean(msg))
        .map(([field, message]) => ({ field, message: message! }));
    }

    it("normalizes record-based field error maps into error list", () => {
      const errorMap = {
        name: "اسم الطرف مطلوب",
        email: "البريد الإلكتروني غير صالح",
        notes: undefined
      };
      const normalized = normalizeFormErrors(errorMap);
      assert.equal(normalized.length, 2);
      assert.equal(normalized[0]?.field, "name");
      assert.equal(normalized[1]?.field, "email");
    });

    it("passes through array error lists unmodified", () => {
      const errorList = [
        { field: "taxNumber", message: "الرقم الضريبي يجب أن يتكون من 15 رقماً" },
        { message: "تعذر حفظ السجل بسبب تعارض في البيانات" }
      ];
      const normalized = normalizeFormErrors(errorList);
      assert.equal(normalized.length, 2);
    });
  });

  describe("Command Palette Search & Ranking Logic", () => {
    interface TestItem {
      id: string;
      title: string;
      subtitle?: string;
      keywords?: string[];
      category: string;
    }

    function filterCommandItems(items: TestItem[], query: string): TestItem[] {
      const clean = query.trim().toLowerCase();
      if (!clean) return items;
      return items.filter((item) => {
        const matchesTitle = item.title.toLowerCase().includes(clean);
        const matchesSub = item.subtitle?.toLowerCase().includes(clean);
        const matchesKey = item.keywords?.some((k) => k.toLowerCase().includes(clean));
        return matchesTitle || matchesSub || matchesKey;
      });
    }

    const testItems: TestItem[] = [
      { id: "1", title: "فاتورة مبيعات جديدة", subtitle: "إدارة فواتير العملاء", keywords: ["invoice", "sales", "عميل"], category: "actions" },
      { id: "2", title: "فاتورة مشتريات جديدة", subtitle: "إدارة فواتير الموردين", keywords: ["bill", "purchase", "مورد"], category: "actions" },
      { id: "3", title: "دليل الحسابات", subtitle: "شجرة الحسابات", keywords: ["accounting", "ledger"], category: "navigation" }
    ];

    it("matches items by Arabic title", () => {
      const results = filterCommandItems(testItems, "مبيعات");
      assert.equal(results.length, 1);
      assert.equal(results[0]?.id, "1");
    });

    it("matches items by English keyword alias", () => {
      const results = filterCommandItems(testItems, "bill");
      assert.equal(results.length, 1);
      assert.equal(results[0]?.id, "2");
    });

    it("returns empty array when query does not match", () => {
      const results = filterCommandItems(testItems, "غير موجود");
      assert.equal(results.length, 0);
    });
  });

  describe("Floating Batch Action Bar State Computation", () => {
    function computeBatchBarVisibility(selectedCount: number) {
      return {
        isVisible: selectedCount > 0,
        badgeText: `${selectedCount}`
      };
    }

    it("hides batch bar when selected count is 0", () => {
      const state = computeBatchBarVisibility(0);
      assert.equal(state.isVisible, false);
    });

    it("shows batch bar with correct badge when items are selected", () => {
      const state = computeBatchBarVisibility(5);
      assert.equal(state.isVisible, true);
      assert.equal(state.badgeText, "5");
    });
  });
});

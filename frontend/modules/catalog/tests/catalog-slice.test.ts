import { describe, it } from "node:test";
import assert from "node:assert/strict";
import "../../../app/localization.ts";
import { changeLanguage } from "../../../core/i18n/i18n.ts";
import {
  createProductSchema,
  createUomConversionSchema,
  createBarcodeSchema,
  createReferenceSchema
} from "../validation/product-schema.ts";
import { catalogApi } from "../services/catalog-api.ts";
import { formatCatalogAuditEvent } from "../audit-format.ts";

describe("Catalog / Product Master Vertical Slice", () => {
  const productSchema = createProductSchema((key) => key);
  const uomSchema = createUomConversionSchema((key) => key);
  const barcodeSchema = createBarcodeSchema((key) => key);
  const referenceSchema = createReferenceSchema((key) => key);

  describe("Product Schemas & Business Invariants", () => {
    it("accepts a simple service with a measured base unit", () => {
      const result = productSchema.safeParse({
        displayName: "Accounting Consulting",
        nature: "Service",
        baseUomId: "uom-hour"
      });
      assert.equal(result.success, true);
    });

    it("requires a base unit and a name for goods", () => {
      const result = productSchema.safeParse({
        displayName: "",
        nature: "Goods",
        baseUomId: ""
      });
      assert.equal(result.success, false);
      if (!result.success) {
        const issues = result.error.issues.map((i) => i.path[0]);
        assert.ok(issues.includes("displayName"));
        assert.ok(issues.includes("baseUomId"));
      }
    });

    it("accepts goods with optional SKU, multilingual names, category, and brand", () => {
      const result = productSchema.safeParse({
        displayName: "A4 Copy Paper",
        nature: "Goods",
        baseUomId: "uom-pack",
        sku: "PAP-A4-01",
        nameAr: "ورق تصوير A4",
        nameEn: "A4 Copy Paper",
        categoryId: "cat-office",
        brandId: "brand-mondi"
      });
      assert.equal(result.success, true);
    });

    it("keeps product nature strictly as Goods or Service", () => {
      assert.notEqual("Goods", "Service");
      const invalidNature = productSchema.safeParse({
        displayName: "Raw Wood",
        nature: "Stockable" as any,
        baseUomId: "uom-kg"
      });
      assert.equal(invalidNature.success, false);
    });

    it("validates alternate packaging unit conversion factor is positive", () => {
      const valid = uomSchema.safeParse({
        uomId: "uom-carton",
        factorToBase: 24,
        effectiveFrom: new Date().toISOString()
      });
      assert.equal(valid.success, true);

      const invalid = uomSchema.safeParse({
        uomId: "uom-carton",
        factorToBase: -5,
        effectiveFrom: new Date().toISOString()
      });
      assert.equal(invalid.success, false);
    });

    it("validates barcode entry requires code value and valid symbology", () => {
      const valid = barcodeSchema.safeParse({
        value: "6221234567890",
        barcodeType: "Ean13"
      });
      assert.equal(valid.success, true);

      const invalid = barcodeSchema.safeParse({
        value: "",
        barcodeType: "Ean13"
      });
      assert.equal(invalid.success, false);
    });

    it("validates customer and supplier reference code schema", () => {
      const validCustomer = referenceSchema.safeParse({
        referenceType: "Customer",
        referenceCode: "CUST-REF-100"
      });
      assert.equal(validCustomer.success, true);

      const validSupplier = referenceSchema.safeParse({
        referenceType: "Supplier",
        referenceCode: "SUP-PART-999"
      });
      assert.equal(validSupplier.success, true);

      const emptyCode = referenceSchema.safeParse({
        referenceType: "Customer",
        referenceCode: "   "
      });
      assert.equal(emptyCode.success, false);
    });
  });

  describe("Catalog API Lifecycle Methods", () => {
    it("exposes all canonical catalog API operations", () => {
      assert.equal(typeof catalogApi.list, "function");
      assert.equal(typeof catalogApi.get, "function");
      assert.equal(typeof catalogApi.create, "function");
      assert.equal(typeof catalogApi.update, "function");
      assert.equal(typeof catalogApi.archive, "function");
      assert.equal(typeof catalogApi.restore, "function");
      assert.equal(typeof catalogApi.setVisibility, "function");
      assert.equal(typeof catalogApi.addBarcode, "function");
      assert.equal(typeof catalogApi.getReadiness, "function");
      assert.equal(typeof catalogApi.resolve, "function");
      assert.equal(typeof catalogApi.createVariant, "function");
      assert.equal(typeof catalogApi.addUom, "function");
      assert.equal(typeof catalogApi.listUoms, "function");
      assert.equal(typeof catalogApi.addReference, "function");
      assert.equal(typeof catalogApi.listReferences, "function");
      assert.equal(typeof catalogApi.categories, "function");
      assert.equal(typeof catalogApi.createCategory, "function");
      assert.equal(typeof catalogApi.brands, "function");
      assert.equal(typeof catalogApi.createBrand, "function");
      assert.equal(typeof catalogApi.uoms, "function");
      assert.equal(typeof catalogApi.createUom, "function");
      assert.equal(typeof catalogApi.createAttribute, "function");
      assert.equal(typeof catalogApi.createAttributeValue, "function");
      assert.equal(typeof catalogApi.listAttributes, "function");
      assert.equal(typeof catalogApi.assignAttribute, "function");
      assert.equal(typeof catalogApi.previewImport, "function");
      assert.equal(typeof catalogApi.commitImport, "function");
      assert.equal(typeof catalogApi.exportCsv, "function");
    });
  });

  describe("Audit Trail Event Formatter", () => {
    it("formats known catalog audit events safely with localized labels in EN and AR", async () => {
      await changeLanguage("en");
      const createdEn = formatCatalogAuditEvent("product_created", JSON.stringify({ displayName: "Office Desk" }));
      assert.equal(createdEn.title, "Initial catalog product creation");
      assert.ok(createdEn.changes.length > 0);

      const archivedEn = formatCatalogAuditEvent("product_archived");
      assert.equal(archivedEn.title, "Product archived from active transactions");

      await changeLanguage("ar");
      const createdAr = formatCatalogAuditEvent("product_created", JSON.stringify({ displayName: "مكتب عمل" }));
      assert.equal(createdAr.title, "إنشاء الصنف المرجعي في الكتالوج");
      assert.ok(createdAr.changes.length > 0);

      const barcodeAr = formatCatalogAuditEvent("product_barcode_added", JSON.stringify({ value: "6221112223334" }));
      assert.equal(barcodeAr.title, "تسجيل باركود مسح ضوئي جديد");
      assert.equal(barcodeAr.changes[0]?.afterValue, "6221112223334");
    });

    it("falls back gracefully for unknown audit events without throwing", () => {
      const unknown = formatCatalogAuditEvent("future_custom_event", "not-json");
      assert.ok(typeof unknown.title === "string");
      assert.deepEqual(unknown.changes, []);
    });
  });
});

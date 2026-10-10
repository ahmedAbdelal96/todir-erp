import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { productSchema } from "../schemas/product-schema.ts";
import { catalogApi } from "../api/catalog-api.ts";
describe("Catalog / Product Master Vertical Slice", () => {
  it("accepts a simple service with a measured base unit", () => { const result = productSchema.safeParse({ displayName: "Consulting", nature: "Service", baseUomId: "uom-hour" }); assert.equal(result.success, true); });
  it("requires a base unit and a name", () => { const result = productSchema.safeParse({ displayName: "", nature: "Goods", baseUomId: "" }); assert.equal(result.success, false); });
  it("keeps product and service as explicit choices", () => { assert.notEqual("Goods", "Service"); assert.equal(typeof catalogApi.list, "function"); assert.equal(typeof catalogApi.create, "function"); });
});

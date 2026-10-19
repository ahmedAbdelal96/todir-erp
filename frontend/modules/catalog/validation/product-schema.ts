import { z } from "zod";
import type { ProductNature, UomDimension, BarcodeType, ReferenceType, VisibilityScope } from "../types.ts";

export type ProductValidationKey =
  | "validation.nameRequired"
  | "validation.baseUnitRequired"
  | "validation.factorPositive"
  | "validation.barcodeRequired"
  | "validation.referenceRequired"
  | "validation.codeRequired";

export interface ProductFormData {
  displayName: string;
  nature: ProductNature;
  sku?: string | undefined;
  nameAr?: string | undefined;
  nameEn?: string | undefined;
  categoryId?: string | undefined;
  brandId?: string | undefined;
  baseUomId: string;
}

export function createProductSchema(translate: (key: ProductValidationKey) => string) {
  return z.object({
    displayName: z.string().trim().min(1, translate("validation.nameRequired")),
    nature: z.enum(["Goods", "Service"]),
    sku: z.string().trim().max(80).optional(),
    nameAr: z.string().trim().max(300).optional(),
    nameEn: z.string().trim().max(300).optional(),
    categoryId: z.string().optional(),
    brandId: z.string().optional(),
    baseUomId: z.string().min(1, translate("validation.baseUnitRequired"))
  });
}

export function createUomConversionSchema(translate: (key: ProductValidationKey) => string) {
  return z.object({
    uomId: z.string().min(1, translate("validation.codeRequired")),
    factorToBase: z.number().positive(translate("validation.factorPositive")),
    effectiveFrom: z.string().min(1),
    effectiveTo: z.string().optional(),
    usageFlags: z.array(z.string()).optional(),
    roundingPrecision: z.number().min(0).max(6).optional()
  });
}

export function createBarcodeSchema(translate: (key: ProductValidationKey) => string) {
  return z.object({
    barcodeType: z.enum(["Ean13", "Ean8", "Upc", "Code128", "Other"]),
    value: z.string().trim().min(1, translate("validation.barcodeRequired")),
    uomId: z.string().optional(),
    isPrimary: z.boolean().default(false)
  });
}

export function createReferenceSchema(translate: (key: ProductValidationKey) => string) {
  return z.object({
    referenceType: z.enum(["Customer", "Supplier"]),
    referenceCode: z.string().trim().min(1, translate("validation.referenceRequired")),
    partyId: z.string().optional()
  });
}

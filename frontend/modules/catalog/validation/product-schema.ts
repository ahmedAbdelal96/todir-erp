import { z } from "zod";

export type ProductFormData = {
  displayName: string;
  nature: "Goods" | "Service";
  sku?: string | undefined;
  nameAr?: string | undefined;
  nameEn?: string | undefined;
  categoryId?: string | undefined;
  brandId?: string | undefined;
  baseUomId: string;
};

export type ProductValidationKey = "productForm.nameRequired" | "productForm.baseUnitRequired";

export function createProductSchema(translate: (key: ProductValidationKey) => string) {
  return z.object({
    displayName: z.string().trim().min(1, translate("productForm.nameRequired")),
    nature: z.enum(["Goods", "Service"]),
    sku: z.string().trim().max(80).optional(),
    nameAr: z.string().trim().max(300).optional(),
    nameEn: z.string().trim().max(300).optional(),
    categoryId: z.string().optional(),
    brandId: z.string().optional(),
    baseUomId: z.string().min(1, translate("productForm.baseUnitRequired"))
  });
}

import { z } from "zod";
export const productSchema = z.object({ displayName: z.string().trim().min(1, "اسم المنتج مطلوب"), nature: z.enum(["Goods", "Service"]), sku: z.string().trim().max(80).optional(), nameAr: z.string().trim().max(300).optional(), nameEn: z.string().trim().max(300).optional(), categoryId: z.string().optional(), brandId: z.string().optional(), baseUomId: z.string().min(1, "وحدة الأساس مطلوبة") });
export type ProductFormData = z.infer<typeof productSchema>;

export type ProductNature = "Goods" | "Service";
export type CatalogStatus = "Active" | "Archived";
export type UomDimension = "Count" | "Mass" | "Length" | "Area" | "Volume" | "Time" | "BusinessUnit";
export interface ProductSummary { readonly id: string; readonly defaultVariantId: string; readonly displayName: string; readonly nameAr?: string | null; readonly nameEn?: string | null; readonly nature: ProductNature; readonly sku?: string | null; readonly baseUomCode?: string | null; readonly categoryName?: string | null; readonly brandName?: string | null; readonly status: CatalogStatus; readonly updatedAt: string; readonly version: number; }
export interface ProductDetails extends ProductSummary { readonly categoryId?: string | null; readonly brandId?: string | null; readonly barcodes: readonly Barcode[]; readonly createdAt: string; }
export interface Barcode { readonly id: string; readonly variantId: string; readonly uomId?: string | null; readonly barcodeType: string; readonly value: string; readonly isPrimary: boolean; }
export interface Category { readonly id: string; readonly name: string; readonly parentId?: string | null; readonly status: CatalogStatus; readonly version: number; }
export interface Brand { readonly id: string; readonly name: string; readonly status: CatalogStatus; readonly version: number; }
export interface Uom { readonly id: string; readonly code: string; readonly nameAr: string; readonly nameEn: string; readonly dimension: UomDimension; readonly allowFraction: boolean; readonly precision: number; readonly status: CatalogStatus; }
export interface CreateProductInput { readonly displayName: string; readonly nature: ProductNature; readonly sku?: string | undefined; readonly nameAr?: string | undefined; readonly nameEn?: string | undefined; readonly categoryId?: string | undefined; readonly brandId?: string | undefined; readonly baseUomId: string; }

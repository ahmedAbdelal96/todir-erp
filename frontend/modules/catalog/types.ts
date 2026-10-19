export type ProductNature = "Goods" | "Service";
export type CatalogStatus = "Active" | "Archived";
export type UomDimension = "Count" | "Mass" | "Length" | "Area" | "Volume" | "Time" | "BusinessUnit";
export type BarcodeType = "Ean13" | "Ean8" | "Upc" | "Code128" | "Other";
export type ReferenceType = "Customer" | "Supplier";
export type VisibilityScope = "TenantWide" | "LegalEntity";

export interface ProductSummary {
  readonly id: string; // TemplateId
  readonly defaultVariantId: string;
  readonly displayName: string;
  readonly nameAr?: string | null;
  readonly nameEn?: string | null;
  readonly nature: ProductNature;
  readonly sku?: string | null;
  readonly baseUomCode?: string | null;
  readonly categoryName?: string | null;
  readonly brandName?: string | null;
  readonly status: CatalogStatus;
  readonly updatedAt: string;
  readonly version: number;
}

export interface Barcode {
  readonly id: string;
  readonly variantId: string;
  readonly uomId?: string | null;
  readonly barcodeType: string;
  readonly value: string;
  readonly isPrimary: boolean;
}

export interface ProductDetails extends ProductSummary {
  readonly categoryId?: string | null;
  readonly brandId?: string | null;
  readonly barcodes: readonly Barcode[];
  readonly createdAt: string;
  readonly variants?: readonly VariantDto[] | undefined;
}

export interface PagedProducts {
  readonly items: readonly ProductSummary[];
  readonly totalCount: number;
  readonly page: number;
  readonly pageSize: number;
}

export interface Category {
  readonly id: string;
  readonly name: string;
  readonly parentId?: string | null;
  readonly status: CatalogStatus;
  readonly version: number;
}

export interface Brand {
  readonly id: string;
  readonly name: string;
  readonly status: CatalogStatus;
  readonly version: number;
}

export interface Uom {
  readonly id: string;
  readonly code: string;
  readonly nameAr: string;
  readonly nameEn: string;
  readonly dimension: UomDimension;
  readonly allowFraction: boolean;
  readonly precision: number;
  readonly status: CatalogStatus;
}

export interface ProductUom {
  readonly id: string;
  readonly variantId: string;
  readonly uomId: string;
  readonly uomCode: string;
  readonly factorToBase: number;
  readonly isBase: boolean;
  readonly effectiveFrom: string;
  readonly effectiveTo?: string | null;
  readonly status: CatalogStatus;
  readonly usageFlags?: readonly string[] | null;
  readonly roundingPrecision?: number | null;
}

export interface VariantDto {
  readonly id: string;
  readonly templateId?: string | undefined;
  readonly sku?: string | null | undefined;
  readonly displaySuffix?: string | null | undefined;
  readonly isDefaultVariant?: boolean | undefined;
  readonly isDefault?: boolean | undefined;
  readonly status: CatalogStatus;
  readonly version?: number | undefined;
  readonly attributes?: Record<string, string> | undefined;
  readonly barcodes?: readonly Barcode[] | undefined;
}

export interface ProductReference {
  readonly id: string;
  readonly variantId: string;
  readonly referenceType: string;
  readonly partyId?: string | null;
  readonly referenceCode: string;
  readonly status: CatalogStatus;
}

export interface PagedProductReferences {
  readonly items: readonly ProductReference[];
  readonly page: number;
  readonly pageSize: number;
  readonly hasMore: boolean;
}

export interface ProductReadiness {
  readonly productId: string;
  readonly ready: boolean;
  readonly missing: readonly string[];
  readonly warnings: readonly string[];
}

export interface CatalogAttribute {
  readonly id: string;
  readonly code: string;
  readonly nameAr: string;
  readonly nameEn: string;
  readonly isVariantDefining: boolean;
  readonly status: CatalogStatus;
  readonly version: number;
}

export interface CatalogAttributeValue {
  readonly id: string;
  readonly attributeId: string;
  readonly code: string;
  readonly nameAr: string;
  readonly nameEn: string;
  readonly status: CatalogStatus;
  readonly version: number;
}

export interface TemplateAttribute {
  readonly attribute: CatalogAttribute;
  readonly isRequired: boolean;
  readonly displayOrder: number;
  readonly values: readonly CatalogAttributeValue[];
}

export interface ProductVariantSnapshot {
  readonly variantId: string;
  readonly templateId: string;
  readonly sku?: string | null;
  readonly displayName: string;
  readonly baseUomId: string;
  readonly conversionFactor: number;
  readonly resolvedUomId?: string | null;
  readonly resolvedUomCode?: string | null;
  readonly effectiveAt?: string | null;
}

export interface ProductVisibility {
  readonly scopeType: VisibilityScope;
  readonly legalEntityId?: string | null;
}

export interface CatalogImportUomItem {
  readonly uomCode: string;
  readonly factorToBase: number;
  readonly effectiveFrom: string;
  readonly effectiveTo?: string | null;
  readonly barcode?: string | null;
  readonly usageFlags?: readonly string[] | null;
  readonly roundingPrecision?: number | null;
}

export interface CatalogImportRowItem {
  readonly templateKey?: string | null;
  readonly displayName: string;
  readonly nature: ProductNature;
  readonly sku?: string | null;
  readonly nameAr?: string | null;
  readonly nameEn?: string | null;
  readonly category?: string | null;
  readonly brand?: string | null;
  readonly baseUomCode: string;
  readonly barcode?: string | null;
  readonly externalReference?: string | null;
  readonly additionalUoms?: readonly CatalogImportUomItem[] | null;
  readonly attributes?: Readonly<Record<string, string>> | null;
}

export interface CatalogImportRowResult {
  readonly rowNumber: number;
  readonly classification: string;
  readonly issues: readonly string[];
}

export interface CatalogImportSummary {
  readonly batchId: string;
  readonly status: string;
  readonly totalRows: number;
  readonly createCount: number;
  readonly updateCount: number;
  readonly reviewCount: number;
  readonly errorCount: number;
  readonly rows: readonly CatalogImportRowResult[];
  readonly created?: number | null;
  readonly updated?: number | null;
  readonly skippedCount: number;
  readonly duplicateSkuCount: number;
  readonly duplicateBarcodeCount: number;
  readonly uomIssueCount: number;
  readonly unknownCategoryCount: number;
}

export interface CatalogImportPreviewInput {
  readonly fileName: string;
  readonly rows?: readonly CatalogImportRowItem[] | undefined;
  readonly csvContent?: string | undefined;
  readonly spreadsheetContentBase64?: string | undefined;
  readonly columnMapping?: Readonly<Record<string, string>> | undefined;
}

export interface CreateProductInput {
  readonly displayName: string;
  readonly nature: ProductNature;
  readonly sku?: string | undefined;
  readonly nameAr?: string | undefined;
  readonly nameEn?: string | undefined;
  readonly categoryId?: string | undefined;
  readonly brandId?: string | undefined;
  readonly baseUomId: string;
}

export interface UpdateProductInput {
  readonly displayName: string;
  readonly sku?: string | undefined;
  readonly nameAr?: string | undefined;
  readonly nameEn?: string | undefined;
  readonly categoryId?: string | undefined;
  readonly brandId?: string | undefined;
  readonly version: number;
}

export interface CreateCategoryInput {
  readonly name: string;
  readonly parentId?: string | undefined;
}

export interface CreateBrandInput {
  readonly name: string;
}

export interface CreateUomInput {
  readonly code: string;
  readonly nameAr: string;
  readonly nameEn: string;
  readonly dimension: UomDimension;
  readonly allowFraction: boolean;
  readonly precision: number;
}

export interface AddProductUomInput {
  readonly uomId: string;
  readonly factorToBase: number;
  readonly effectiveFrom: string;
  readonly effectiveTo?: string | null | undefined;
  readonly usageFlags?: readonly string[] | undefined;
  readonly roundingPrecision?: number | undefined;
}

export interface AddBarcodeRequest {
  readonly uomId?: string | null | undefined;
  readonly barcodeType: BarcodeType;
  readonly value: string;
  readonly isPrimary: boolean;
}

export interface CreateVariantRequest {
  readonly sku?: string | null | undefined;
  readonly displaySuffix?: string | null | undefined;
  readonly attributeValues?: Readonly<Record<string, string>> | undefined;
}

export interface AddAliasRequest {
  readonly variantId: string;
  readonly alias: string;
  readonly aliasType: string;
  readonly languageCode?: string | undefined;
}

export interface AddTranslationRequest {
  readonly languageCode: string;
  readonly name: string;
  readonly description?: string | undefined;
}

export interface AddReferenceRequest {
  readonly variantId: string;
  readonly referenceType: ReferenceType;
  readonly partyId?: string | null | undefined;
  readonly referenceCode: string;
}

export interface CreateAttributeInput {
  readonly code: string;
  readonly nameAr: string;
  readonly nameEn: string;
  readonly isVariantDefining: boolean;
}

export interface CreateAttributeValueInput {
  readonly code: string;
  readonly nameAr: string;
  readonly nameEn: string;
}

export interface AssignTemplateAttributeInput {
  readonly isRequired?: boolean | undefined;
  readonly displayOrder?: number | undefined;
}

export interface ProductVisibilityInput {
  readonly scopeType: VisibilityScope;
  readonly legalEntityId?: string | null | undefined;
}

export interface ProductListParams {
  readonly search?: string | undefined;
  readonly nature?: ProductNature | undefined;
  readonly status?: CatalogStatus | undefined;
  readonly categoryId?: string | undefined;
  readonly brandId?: string | undefined;
  readonly page?: number | undefined;
  readonly pageSize?: number | undefined;
}

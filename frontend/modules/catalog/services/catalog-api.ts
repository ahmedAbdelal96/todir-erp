import { httpClient, type RequestOptions } from "../../../core/api/http-client.ts";
import type {
  AddBarcodeRequest,
  AddProductUomInput,
  AddReferenceRequest,
  AddTranslationRequest,
  AddAliasRequest,
  AssignTemplateAttributeInput,
  Barcode,
  Brand,
  CatalogAttribute,
  CatalogAttributeValue,
  CatalogImportPreviewInput,
  CatalogImportSummary,
  Category,
  CreateBrandInput,
  CreateCategoryInput,
  CreateProductInput,
  CreateUomInput,
  CreateVariantRequest,
  CreateAttributeInput,
  CreateAttributeValueInput,
  PagedProductReferences,
  PagedProducts,
  ProductDetails,
  ProductListParams,
  ProductReadiness,
  ProductReference,
  ProductUom,
  ProductVariantSnapshot,
  ResolvedVariantIdentity,
  ProductVisibility,
  ProductVisibilityInput,
  TemplateAttribute,
  Uom,
  UpdateProductInput,
  VariantDto
} from "../types.ts";

export type { ProductListParams };

const basePath = "/api/v1/catalog";

function opts(legalEntityId?: string, extra?: RequestOptions): RequestOptions | undefined {
  if (!legalEntityId && !extra) return undefined;
  const res: RequestOptions = extra ? { ...extra } : {};
  if (legalEntityId) {
    return { ...res, legalEntityId };
  }
  return res;
}

export const catalogApi = {
  list: (params: ProductListParams = {}, legalEntityId?: string): Promise<PagedProducts> => {
    const query = new URLSearchParams();
    if (params.search) query.set("search", params.search);
    if (params.nature) query.set("nature", params.nature);
    if (params.status) query.set("status", params.status);
    if (params.categoryId) query.set("categoryId", params.categoryId);
    if (params.brandId) query.set("brandId", params.brandId);
    if (params.page) query.set("page", String(params.page));
    if (params.pageSize) query.set("pageSize", String(params.pageSize));
    const suffix = query.size ? `?${query.toString()}` : "";
    return httpClient<PagedProducts>(`${basePath}/products${suffix}`, opts(legalEntityId));
  },

  get: (id: string, legalEntityId?: string): Promise<ProductDetails> =>
    httpClient<ProductDetails>(`${basePath}/products/${encodeURIComponent(id)}`, opts(legalEntityId)),

  create: (input: CreateProductInput): Promise<ProductDetails> =>
    httpClient<ProductDetails>(`${basePath}/products`, {
      method: "POST",
      body: JSON.stringify(input)
    }),

  update: (id: string, input: UpdateProductInput, legalEntityId?: string): Promise<ProductDetails> =>
    httpClient<ProductDetails>(`${basePath}/products/${encodeURIComponent(id)}`, opts(legalEntityId, {
      method: "PATCH",
      body: JSON.stringify(input)
    })),

  archive: (id: string, version: number, legalEntityId?: string): Promise<ProductDetails> =>
    httpClient<ProductDetails>(`${basePath}/products/${encodeURIComponent(id)}/archive`, opts(legalEntityId, {
      method: "POST",
      body: JSON.stringify({ version })
    })),

  restore: (id: string, version: number, legalEntityId?: string): Promise<ProductDetails> =>
    httpClient<ProductDetails>(`${basePath}/products/${encodeURIComponent(id)}/restore`, opts(legalEntityId, {
      method: "POST",
      body: JSON.stringify({ version })
    })),

  setVisibility: (id: string, input: ProductVisibilityInput): Promise<readonly ProductVisibility[]> =>
    httpClient<readonly ProductVisibility[]>(`${basePath}/products/${encodeURIComponent(id)}/visibility`, {
      method: "PUT",
      body: JSON.stringify(input)
    }),

  addBarcode: (productId: string, input: AddBarcodeRequest, variantId?: string, legalEntityId?: string): Promise<Barcode> => {
    const url = variantId
      ? `${basePath}/products/${encodeURIComponent(productId)}/variants/${encodeURIComponent(variantId)}/barcodes`
      : `${basePath}/products/${encodeURIComponent(productId)}/barcodes`;
    return httpClient<Barcode>(url, opts(legalEntityId, {
      method: "POST",
      body: JSON.stringify(input)
    }));
  },

  getReadiness: (productId: string, legalEntityId?: string): Promise<ProductReadiness> =>
    httpClient<ProductReadiness>(`${basePath}/products/${encodeURIComponent(productId)}/readiness`, opts(legalEntityId)),

  resolve: (value: string, legalEntityId?: string): Promise<ProductVariantSnapshot> =>
    httpClient<ProductVariantSnapshot>(`${basePath}/resolve/${encodeURIComponent(value)}`, opts(legalEntityId)),

  resolveBatch: (values: readonly string[], legalEntityId?: string): Promise<readonly ResolvedVariantIdentity[]> =>
    httpClient<readonly ResolvedVariantIdentity[]>(`${basePath}/resolve/batch`, opts(legalEntityId, {
      method: "POST",
      body: JSON.stringify({ values })
    })),

  createVariant: (productId: string, input: CreateVariantRequest, legalEntityId?: string): Promise<VariantDto> =>
    httpClient<VariantDto>(`${basePath}/products/${encodeURIComponent(productId)}/variants`, opts(legalEntityId, {
      method: "POST",
      body: JSON.stringify(input)
    })),

  addUom: (productId: string, variantId: string, input: AddProductUomInput, legalEntityId?: string): Promise<ProductUom> =>
    httpClient<ProductUom>(`${basePath}/products/${encodeURIComponent(productId)}/variants/${encodeURIComponent(variantId)}/uoms`, opts(legalEntityId, {
      method: "POST",
      body: JSON.stringify(input)
    })),

  listUoms: (variantId: string, legalEntityId?: string): Promise<readonly ProductUom[]> =>
    httpClient<readonly ProductUom[]>(`${basePath}/variants/${encodeURIComponent(variantId)}/uoms`, opts(legalEntityId)),

  addAlias: (productId: string, input: AddAliasRequest, legalEntityId?: string): Promise<boolean> =>
    httpClient<boolean>(`${basePath}/products/${encodeURIComponent(productId)}/aliases`, opts(legalEntityId, {
      method: "POST",
      body: JSON.stringify(input)
    })),

  addTranslation: (productId: string, input: AddTranslationRequest, legalEntityId?: string): Promise<boolean> =>
    httpClient<boolean>(`${basePath}/products/${encodeURIComponent(productId)}/translations`, opts(legalEntityId, {
      method: "POST",
      body: JSON.stringify(input)
    })),

  addReference: (productId: string, input: AddReferenceRequest, legalEntityId?: string): Promise<ProductReference> =>
    httpClient<ProductReference>(`${basePath}/products/${encodeURIComponent(productId)}/references`, opts(legalEntityId, {
      method: "POST",
      body: JSON.stringify(input)
    })),

  listReferences: (productId: string, page = 1, pageSize = 25, legalEntityId?: string): Promise<PagedProductReferences> => {
    const query = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
    return httpClient<PagedProductReferences>(`${basePath}/products/${encodeURIComponent(productId)}/references?${query.toString()}`, opts(legalEntityId));
  },

  categories: (): Promise<readonly Category[]> =>
    httpClient<readonly Category[]>(`${basePath}/categories`),

  createCategory: (input: CreateCategoryInput): Promise<Category> =>
    httpClient<Category>(`${basePath}/categories`, {
      method: "POST",
      body: JSON.stringify(input)
    }),

  brands: (): Promise<readonly Brand[]> =>
    httpClient<readonly Brand[]>(`${basePath}/brands`),

  createBrand: (input: CreateBrandInput): Promise<Brand> =>
    httpClient<Brand>(`${basePath}/brands`, {
      method: "POST",
      body: JSON.stringify(input)
    }),

  uoms: (): Promise<readonly Uom[]> =>
    httpClient<readonly Uom[]>(`${basePath}/uoms`),

  createUom: (input: CreateUomInput): Promise<Uom> =>
    httpClient<Uom>(`${basePath}/uoms`, {
      method: "POST",
      body: JSON.stringify(input)
    }),

  createAttribute: (input: CreateAttributeInput): Promise<CatalogAttribute> =>
    httpClient<CatalogAttribute>(`${basePath}/attributes`, {
      method: "POST",
      body: JSON.stringify(input)
    }),

  createAttributeValue: (attributeId: string, input: CreateAttributeValueInput): Promise<CatalogAttributeValue> =>
    httpClient<CatalogAttributeValue>(`${basePath}/attributes/${encodeURIComponent(attributeId)}/values`, {
      method: "POST",
      body: JSON.stringify(input)
    }),

  listAttributes: (productId: string, legalEntityId?: string): Promise<readonly TemplateAttribute[]> =>
    httpClient<readonly TemplateAttribute[]>(`${basePath}/products/${encodeURIComponent(productId)}/attributes`, opts(legalEntityId)),

  assignAttribute: (productId: string, attributeId: string, input: AssignTemplateAttributeInput, legalEntityId?: string): Promise<TemplateAttribute> =>
    httpClient<TemplateAttribute>(`${basePath}/products/${encodeURIComponent(productId)}/attributes/${encodeURIComponent(attributeId)}`, opts(legalEntityId, {
      method: "PUT",
      body: JSON.stringify(input)
    })),

  previewImport: (input: CatalogImportPreviewInput): Promise<CatalogImportSummary> =>
    httpClient<CatalogImportSummary>(`${basePath}/imports/preview`, {
      method: "POST",
      body: JSON.stringify(input)
    }),

  commitImport: (batchId: string): Promise<CatalogImportSummary> =>
    httpClient<CatalogImportSummary>(`${basePath}/imports/${encodeURIComponent(batchId)}/commit`, {
      method: "POST"
    }),

  getImportSummary: (batchId: string): Promise<CatalogImportSummary> =>
    httpClient<CatalogImportSummary>(`${basePath}/imports/${encodeURIComponent(batchId)}`),

  exportCsv: async (legalEntityId?: string): Promise<string> => {
    const res = await fetch(`${basePath}/export`, {
      headers: {
        ...(legalEntityId ? { "X-Legal-Entity-Id": legalEntityId } : {})
      }
    });
    if (!res.ok) throw new Error("Failed to export catalog CSV");
    return res.text();
  }
};

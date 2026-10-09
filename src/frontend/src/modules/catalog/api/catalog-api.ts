import { httpClient } from "../../../platform/api/http-client.ts";
import type { Brand, Category, CreateProductInput, ProductDetails, ProductNature, ProductSummary, Uom } from "../types.ts";
export interface ProductListParams { readonly search?: string | undefined; readonly nature?: ProductNature | undefined; readonly status?: "Active" | "Archived" | undefined; readonly categoryId?: string | undefined; readonly brandId?: string | undefined; readonly page?: number | undefined; readonly pageSize?: number | undefined; }
export interface ProductListResult { readonly items: readonly ProductSummary[]; readonly totalCount: number; readonly page: number; readonly pageSize: number; }
export interface CreateCategoryInput { readonly name: string; readonly parentId?: string; }
export interface CreateUomInput { readonly code: string; readonly nameAr: string; readonly nameEn: string; readonly dimension: Uom["dimension"]; readonly allowFraction: boolean; readonly precision: number; }
export const catalogApi = {
  list: (params: ProductListParams = {}) => { const query = new URLSearchParams(); Object.entries(params).forEach(([key, value]) => value !== undefined && query.set(key, String(value))); return httpClient<ProductListResult>(`/api/v1/catalog/products?${query.toString()}`); },
  get: (id: string) => httpClient<ProductDetails>(`/api/v1/catalog/products/${id}`),
  create: (input: CreateProductInput) => httpClient<ProductDetails>("/api/v1/catalog/products", { method: "POST", body: JSON.stringify(input) }),
  update: (id: string, input: CreateProductInput & { readonly version: number }) => httpClient<ProductDetails>(`/api/v1/catalog/products/${id}`, { method: "PATCH", body: JSON.stringify(input) }),
  changeStatus: (id: string, archive: boolean, version: number) => httpClient<ProductDetails>(`/api/v1/catalog/products/${id}/${archive ? "archive" : "restore"}`, { method: "POST", body: JSON.stringify({ version }) }),
  categories: () => httpClient<readonly Category[]>("/api/v1/catalog/categories"),
  brands: () => httpClient<readonly Brand[]>("/api/v1/catalog/brands"),
  createCategory: (input: CreateCategoryInput) => httpClient<Category>("/api/v1/catalog/categories", { method: "POST", body: JSON.stringify(input) }),
  uoms: () => httpClient<readonly Uom[]>("/api/v1/catalog/uoms"),
  createUom: (input: CreateUomInput) => httpClient<Uom>("/api/v1/catalog/uoms", { method: "POST", body: JSON.stringify(input) })
};

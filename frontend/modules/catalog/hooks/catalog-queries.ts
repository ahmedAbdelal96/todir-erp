import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { catalogApi, type ProductListParams } from "../services/catalog-api.ts";
import type { CreateProductInput } from "../types.ts";
import { queryKeyFactory } from "../../../core/api/query-keys.ts";
const tenant = "tenant-default"; const entity = "entity-default";
export const catalogKeys = { products: () => queryKeyFactory.resource(tenant, entity, "catalog", "products"), list: (params: ProductListParams) => queryKeyFactory.list(tenant, entity, "catalog", "products", params), detail: (id: string) => queryKeyFactory.detail(tenant, entity, "catalog", "products", id) };
export function useProductList(params: ProductListParams) { return useQuery({ queryKey: catalogKeys.list(params), queryFn: () => catalogApi.list(params), staleTime: 20_000 }); }
export function useProduct(id: string) { return useQuery({ queryKey: catalogKeys.detail(id), queryFn: () => catalogApi.get(id), enabled: Boolean(id), staleTime: 30_000 }); }
export function useCatalogCategories() { return useQuery({ queryKey: ["catalog", "categories", tenant], queryFn: catalogApi.categories, staleTime: 60_000 }); }
export function useCatalogBrands() { return useQuery({ queryKey: ["catalog", "brands", tenant], queryFn: catalogApi.brands, staleTime: 60_000 }); }
export function useCatalogUoms() { return useQuery({ queryKey: ["catalog", "uoms", tenant], queryFn: catalogApi.uoms, staleTime: 60_000 }); }
export function useCreateProduct() { const client = useQueryClient(); return useMutation({ mutationFn: (input: CreateProductInput) => catalogApi.create(input), onSuccess: () => { void client.invalidateQueries({ queryKey: catalogKeys.products() }); } }); }
export function useUpdateProduct() { const client = useQueryClient(); return useMutation({ mutationFn: ({ id, input }: { readonly id: string; readonly input: CreateProductInput & { readonly version: number } }) => catalogApi.update(id, input), onSuccess: (_data, variables) => { void client.invalidateQueries({ queryKey: catalogKeys.detail(variables.id) }); void client.invalidateQueries({ queryKey: catalogKeys.products() }); } }); }
export function useChangeProductStatus() { const client = useQueryClient(); return useMutation({ mutationFn: ({ id, archive, version }: { readonly id: string; readonly archive: boolean; readonly version: number }) => catalogApi.changeStatus(id, archive, version), onSuccess: (_data, variables) => { void client.invalidateQueries({ queryKey: catalogKeys.detail(variables.id) }); void client.invalidateQueries({ queryKey: catalogKeys.products() }); } }); }

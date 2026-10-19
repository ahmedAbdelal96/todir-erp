import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { catalogApi, type ProductListParams } from "../services/catalog-api.ts";
import type {
  AddBarcodeRequest,
  AddProductUomInput,
  AddReferenceRequest,
  AddTranslationRequest,
  AddAliasRequest,
  AssignTemplateAttributeInput,
  CatalogImportPreviewInput,
  CreateBrandInput,
  CreateCategoryInput,
  CreateProductInput,
  CreateUomInput,
  CreateVariantRequest,
  CreateAttributeInput,
  CreateAttributeValueInput,
  ProductDetails,
  ProductVisibilityInput,
  UpdateProductInput
} from "../types.ts";
import { queryKeyFactory } from "../../../core/api/query-keys.ts";
import { useRuntimeContext } from "../../../core/runtime/runtime-provider.tsx";

export interface CatalogContextScope {
  readonly tenantId?: string | undefined;
  readonly legalEntityId?: string | undefined;
}

function useResolvedScope(explicit?: CatalogContextScope): { tenantId: string; legalEntityId?: string } {
  try {
    const runtime = useRuntimeContext();
    const legalEntityId = explicit?.legalEntityId || runtime.legalEntity.legalEntityId || undefined;
    return {
      tenantId: explicit?.tenantId || runtime.tenant.tenantId || "default-tenant",
      ...(legalEntityId ? { legalEntityId } : {})
    };
  } catch {
    const legalEntityId = explicit?.legalEntityId || undefined;
    return {
      tenantId: explicit?.tenantId || "default-tenant",
      ...(legalEntityId ? { legalEntityId } : {})
    };
  }
}

export const catalogKeys = {
  all: (tenantId: string) => ["todir", "tenant", tenantId, "module", "catalog"] as const,
  products: (tenantId: string, legalEntityId?: string) =>
    legalEntityId
      ? queryKeyFactory.resource(tenantId, legalEntityId, "catalog", "products")
      : ["todir", "tenant", tenantId, "module", "catalog", "products"] as const,
  list: (tenantId: string, legalEntityId: string | undefined, params: ProductListParams) =>
    legalEntityId
      ? queryKeyFactory.list(tenantId, legalEntityId, "catalog", "products", params)
      : ["todir", "tenant", tenantId, "module", "catalog", "products", "list", params] as const,
  detail: (tenantId: string, legalEntityId: string | undefined, id: string) =>
    legalEntityId
      ? queryKeyFactory.detail(tenantId, legalEntityId, "catalog", "products", id)
      : ["todir", "tenant", tenantId, "module", "catalog", "products", "detail", id] as const,
  readiness: (tenantId: string, id: string) =>
    ["todir", "tenant", tenantId, "module", "catalog", "products", "readiness", id] as const,
  uoms: (tenantId: string, variantId: string) =>
    ["todir", "tenant", tenantId, "module", "catalog", "variants", variantId, "uoms"] as const,
  references: (tenantId: string, productId: string, page: number, pageSize: number) =>
    ["todir", "tenant", tenantId, "module", "catalog", "products", productId, "references", { page, pageSize }] as const,
  attributes: (tenantId: string, productId: string) =>
    ["todir", "tenant", tenantId, "module", "catalog", "products", productId, "attributes"] as const,
  masterCategories: (tenantId: string) => ["todir", "tenant", tenantId, "module", "catalog", "categories"] as const,
  masterBrands: (tenantId: string) => ["todir", "tenant", tenantId, "module", "catalog", "brands"] as const,
  masterUoms: (tenantId: string) => ["todir", "tenant", tenantId, "module", "catalog", "uoms"] as const,
  importSummary: (tenantId: string, batchId: string) => ["todir", "tenant", tenantId, "module", "catalog", "imports", batchId] as const
};

export function useProductList(params: ProductListParams = {}, scope?: CatalogContextScope) {
  const { tenantId, legalEntityId } = useResolvedScope(scope);
  return useQuery({
    queryKey: catalogKeys.list(tenantId, legalEntityId, params),
    queryFn: () => catalogApi.list(params, legalEntityId),
    staleTime: 15_000
  });
}

export function useProduct(id: string, scope?: CatalogContextScope) {
  const { tenantId, legalEntityId } = useResolvedScope(scope);
  return useQuery({
    queryKey: catalogKeys.detail(tenantId, legalEntityId, id),
    queryFn: () => catalogApi.get(id, legalEntityId),
    enabled: Boolean(id),
    staleTime: 30_000
  });
}

export function useProductReadiness(productId: string, scope?: CatalogContextScope) {
  const { tenantId, legalEntityId } = useResolvedScope(scope);
  return useQuery({
    queryKey: catalogKeys.readiness(tenantId, productId),
    queryFn: () => catalogApi.getReadiness(productId, legalEntityId),
    enabled: Boolean(productId),
    staleTime: 10_000
  });
}

export function useProductUoms(variantId: string, scope?: CatalogContextScope) {
  const { tenantId, legalEntityId } = useResolvedScope(scope);
  return useQuery({
    queryKey: catalogKeys.uoms(tenantId, variantId),
    queryFn: () => catalogApi.listUoms(variantId, legalEntityId),
    enabled: Boolean(variantId),
    staleTime: 20_000
  });
}

export function useProductReferences(productId: string, page = 1, pageSize = 25, scope?: CatalogContextScope) {
  const { tenantId, legalEntityId } = useResolvedScope(scope);
  return useQuery({
    queryKey: catalogKeys.references(tenantId, productId, page, pageSize),
    queryFn: () => catalogApi.listReferences(productId, page, pageSize, legalEntityId),
    enabled: Boolean(productId),
    staleTime: 20_000
  });
}

export function useProductAttributes(productId: string, scope?: CatalogContextScope) {
  const { tenantId, legalEntityId } = useResolvedScope(scope);
  return useQuery({
    queryKey: catalogKeys.attributes(tenantId, productId),
    queryFn: () => catalogApi.listAttributes(productId, legalEntityId),
    enabled: Boolean(productId),
    staleTime: 20_000
  });
}

export function useCatalogCategories(scope?: CatalogContextScope) {
  const { tenantId } = useResolvedScope(scope);
  return useQuery({
    queryKey: catalogKeys.masterCategories(tenantId),
    queryFn: catalogApi.categories,
    staleTime: 60_000
  });
}

export function useCatalogBrands(scope?: CatalogContextScope) {
  const { tenantId } = useResolvedScope(scope);
  return useQuery({
    queryKey: catalogKeys.masterBrands(tenantId),
    queryFn: catalogApi.brands,
    staleTime: 60_000
  });
}

export function useCatalogUoms(scope?: CatalogContextScope) {
  const { tenantId } = useResolvedScope(scope);
  return useQuery({
    queryKey: catalogKeys.masterUoms(tenantId),
    queryFn: catalogApi.uoms,
    staleTime: 60_000
  });
}

export function useCreateProduct(scope?: CatalogContextScope) {
  const { tenantId, legalEntityId } = useResolvedScope(scope);
  const client = useQueryClient();
  return useMutation({
    mutationFn: (input: CreateProductInput) => catalogApi.create(input),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: catalogKeys.products(tenantId, legalEntityId) });
      void client.invalidateQueries({ queryKey: ["todir", "tenant", tenantId, "module", "catalog"] });
    }
  });
}

export function useUpdateProduct(scope?: CatalogContextScope) {
  const { tenantId, legalEntityId } = useResolvedScope(scope);
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, input }: { readonly id: string; readonly input: UpdateProductInput }) =>
      catalogApi.update(id, input, legalEntityId),
    onSuccess: (_data, variables) => {
      void client.invalidateQueries({ queryKey: catalogKeys.detail(tenantId, legalEntityId, variables.id) });
      void client.invalidateQueries({ queryKey: catalogKeys.products(tenantId, legalEntityId) });
      void client.invalidateQueries({ queryKey: catalogKeys.readiness(tenantId, variables.id) });
    }
  });
}

export function useChangeProductStatus(scope?: CatalogContextScope) {
  const { tenantId, legalEntityId } = useResolvedScope(scope);
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, archive, version }: { readonly id: string; readonly archive: boolean; readonly version: number }) =>
      archive ? catalogApi.archive(id, version, legalEntityId) : catalogApi.restore(id, version, legalEntityId),
    onSuccess: (_data, variables) => {
      void client.invalidateQueries({ queryKey: catalogKeys.detail(tenantId, legalEntityId, variables.id) });
      void client.invalidateQueries({ queryKey: catalogKeys.products(tenantId, legalEntityId) });
      void client.invalidateQueries({ queryKey: catalogKeys.readiness(tenantId, variables.id) });
    }
  });
}

export function useSetProductVisibility(scope?: CatalogContextScope) {
  const { tenantId, legalEntityId } = useResolvedScope(scope);
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, input }: { readonly id: string; readonly input: ProductVisibilityInput }) =>
      catalogApi.setVisibility(id, input),
    onSuccess: (_data, variables) => {
      void client.invalidateQueries({ queryKey: catalogKeys.detail(tenantId, legalEntityId, variables.id) });
      void client.invalidateQueries({ queryKey: catalogKeys.products(tenantId, legalEntityId) });
    }
  });
}

export function useAddBarcode(scope?: CatalogContextScope) {
  const { tenantId, legalEntityId } = useResolvedScope(scope);
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ productId, input, variantId }: { readonly productId: string; readonly input: AddBarcodeRequest; readonly variantId?: string }) =>
      catalogApi.addBarcode(productId, input, variantId, legalEntityId),
    onSuccess: (_data, variables) => {
      void client.invalidateQueries({ queryKey: catalogKeys.detail(tenantId, legalEntityId, variables.productId) });
      void client.invalidateQueries({ queryKey: catalogKeys.readiness(tenantId, variables.productId) });
    }
  });
}

export function useCreateVariant(scope?: CatalogContextScope) {
  const { tenantId, legalEntityId } = useResolvedScope(scope);
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ productId, input }: { readonly productId: string; readonly input: CreateVariantRequest }) =>
      catalogApi.createVariant(productId, input, legalEntityId),
    onSuccess: (_data, variables) => {
      void client.invalidateQueries({ queryKey: catalogKeys.detail(tenantId, legalEntityId, variables.productId) });
      void client.invalidateQueries({ queryKey: catalogKeys.products(tenantId, legalEntityId) });
      void client.invalidateQueries({ queryKey: catalogKeys.attributes(tenantId, variables.productId) });
    }
  });
}

export function useAddProductUom(scope?: CatalogContextScope) {
  const { tenantId, legalEntityId } = useResolvedScope(scope);
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ productId, variantId, input }: { readonly productId: string; readonly variantId: string; readonly input: AddProductUomInput }) =>
      catalogApi.addUom(productId, variantId, input, legalEntityId),
    onSuccess: (_data, variables) => {
      void client.invalidateQueries({ queryKey: catalogKeys.uoms(tenantId, variables.variantId) });
      void client.invalidateQueries({ queryKey: catalogKeys.detail(tenantId, legalEntityId, variables.productId) });
      void client.invalidateQueries({ queryKey: catalogKeys.readiness(tenantId, variables.productId) });
    }
  });
}

export function useAddProductAlias(scope?: CatalogContextScope) {
  const { tenantId, legalEntityId } = useResolvedScope(scope);
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ productId, input }: { readonly productId: string; readonly input: AddAliasRequest }) =>
      catalogApi.addAlias(productId, input, legalEntityId),
    onSuccess: (_data, variables) => {
      void client.invalidateQueries({ queryKey: catalogKeys.detail(tenantId, legalEntityId, variables.productId) });
    }
  });
}

export function useAddProductTranslation(scope?: CatalogContextScope) {
  const { tenantId, legalEntityId } = useResolvedScope(scope);
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ productId, input }: { readonly productId: string; readonly input: AddTranslationRequest }) =>
      catalogApi.addTranslation(productId, input, legalEntityId),
    onSuccess: (_data, variables) => {
      void client.invalidateQueries({ queryKey: catalogKeys.detail(tenantId, legalEntityId, variables.productId) });
      void client.invalidateQueries({ queryKey: catalogKeys.products(tenantId, legalEntityId) });
    }
  });
}

export function useAddProductReference(scope?: CatalogContextScope) {
  const { tenantId, legalEntityId } = useResolvedScope(scope);
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ productId, input }: { readonly productId: string; readonly input: AddReferenceRequest }) =>
      catalogApi.addReference(productId, input, legalEntityId),
    onSuccess: (_data, variables) => {
      void client.invalidateQueries({ queryKey: ["todir", "tenant", tenantId, "module", "catalog", "products", variables.productId, "references"] });
    }
  });
}

export function useCreateCategory(scope?: CatalogContextScope) {
  const { tenantId } = useResolvedScope(scope);
  const client = useQueryClient();
  return useMutation({
    mutationFn: (input: CreateCategoryInput) => catalogApi.createCategory(input),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: catalogKeys.masterCategories(tenantId) });
    }
  });
}

export function useCreateBrand(scope?: CatalogContextScope) {
  const { tenantId } = useResolvedScope(scope);
  const client = useQueryClient();
  return useMutation({
    mutationFn: (input: CreateBrandInput) => catalogApi.createBrand(input),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: catalogKeys.masterBrands(tenantId) });
    }
  });
}

export function useCreateUom(scope?: CatalogContextScope) {
  const { tenantId } = useResolvedScope(scope);
  const client = useQueryClient();
  return useMutation({
    mutationFn: (input: CreateUomInput) => catalogApi.createUom(input),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: catalogKeys.masterUoms(tenantId) });
    }
  });
}

export function useCreateAttribute(scope?: CatalogContextScope) {
  const { tenantId } = useResolvedScope(scope);
  const client = useQueryClient();
  return useMutation({
    mutationFn: (input: CreateAttributeInput) => catalogApi.createAttribute(input),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: ["todir", "tenant", tenantId, "module", "catalog"] });
    }
  });
}

export function useCreateAttributeValue(scope?: CatalogContextScope) {
  const { tenantId } = useResolvedScope(scope);
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ attributeId, input }: { readonly attributeId: string; readonly input: CreateAttributeValueInput }) =>
      catalogApi.createAttributeValue(attributeId, input),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: ["todir", "tenant", tenantId, "module", "catalog"] });
    }
  });
}

export function useAssignTemplateAttribute(scope?: CatalogContextScope) {
  const { tenantId, legalEntityId } = useResolvedScope(scope);
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ productId, attributeId, input }: { readonly productId: string; readonly attributeId: string; readonly input: AssignTemplateAttributeInput }) =>
      catalogApi.assignAttribute(productId, attributeId, input, legalEntityId),
    onSuccess: (_data, variables) => {
      void client.invalidateQueries({ queryKey: catalogKeys.attributes(tenantId, variables.productId) });
    }
  });
}

export function useImportPreview() {
  return useMutation({
    mutationFn: (input: CatalogImportPreviewInput) => catalogApi.previewImport(input)
  });
}

export function useImportCommit(scope?: CatalogContextScope) {
  const { tenantId, legalEntityId } = useResolvedScope(scope);
  const client = useQueryClient();
  return useMutation({
    mutationFn: (batchId: string) => catalogApi.commitImport(batchId),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: catalogKeys.products(tenantId, legalEntityId) });
    }
  });
}

export function useImportSummary(batchId: string, scope?: CatalogContextScope) {
  const { tenantId } = useResolvedScope(scope);
  return useQuery({
    queryKey: catalogKeys.importSummary(tenantId, batchId),
    queryFn: () => catalogApi.getImportSummary(batchId),
    enabled: Boolean(batchId)
  });
}

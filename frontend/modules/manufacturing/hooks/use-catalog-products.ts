import { useMemo } from "react";
import { useQuery } from "@tanstack/react-query";
import { catalogApi, type ProductSummary, type ResolvedVariantIdentity } from "../../catalog/public.ts";
import { useManufacturingScope } from "./use-manufacturing-scope.ts";

export interface CatalogProductLookup {
  readonly products: readonly ProductSummary[];
  readonly isLoading: boolean;
  readonly isResolving: boolean;
  readonly isError: boolean;
  readonly getProductDisplayName: (variantOrProductId?: string | null) => string;
  readonly getProductSku: (variantOrProductId?: string | null) => string | undefined;
}

const UUID_PATTERN = /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;

export function useCatalogProducts(targetIds?: readonly (string | null | undefined)[]): CatalogProductLookup {
  const { legalEntityId } = useManufacturingScope();

  // 1. Fetch available products list for pickers/dropdowns
  const { data: listData, isLoading: isListLoading } = useQuery({
    queryKey: ["todir", "catalog", "products-lookup", legalEntityId],
    queryFn: async () => {
      try {
        const res = await catalogApi.list({ pageSize: 100 }, legalEntityId ?? undefined);
        return res.items ?? [];
      } catch {
        return [];
      }
    },
    staleTime: 60_000
  });

  const products = listData ?? [];

  // 2. Extract unique target IDs to batch resolve
  const uniqueIds = useMemo(() => {
    if (!targetIds || targetIds.length === 0) return [];
    const set = new Set<string>();
    for (const id of targetIds) {
      if (id && typeof id === "string" && id.trim()) {
        set.add(id.trim());
      }
    }
    return Array.from(set).sort();
  }, [targetIds]);

  // 3. Batch resolve identities for target IDs
  const {
    data: batchData,
    isLoading: isBatchLoading,
    isError: isBatchError
  } = useQuery({
    queryKey: ["todir", "catalog", "resolve-batch", legalEntityId, uniqueIds.join(",")],
    queryFn: async () => {
      if (uniqueIds.length === 0) return [];
      return await catalogApi.resolveBatch(uniqueIds, legalEntityId ?? undefined);
    },
    enabled: uniqueIds.length > 0,
    staleTime: 300_000
  });

  // 4. Index resolved identities by variantId, templateId, and sku
  const resolvedMap = useMemo(() => {
    const map = new Map<string, ResolvedVariantIdentity>();
    if (batchData) {
      for (const item of batchData) {
        if (item.variantId) map.set(item.variantId, item);
        if (item.templateId) map.set(item.templateId, item);
        if (item.sku) map.set(item.sku, item);
      }
    }
    return map;
  }, [batchData]);

  const getProductDisplayName = (id?: string | null): string => {
    if (!id) return "—";
    const cleanId = id.trim();

    // Check batch resolved map first
    const resolved = resolvedMap.get(cleanId);
    if (resolved) {
      return resolved.displayName || resolved.nameAr || resolved.nameEn || resolved.sku || "—";
    }

    // Check loaded products list
    const match = products.find((p) => p.id === cleanId || p.defaultVariantId === cleanId);
    if (match) {
      return match.displayName || match.nameAr || match.nameEn || match.sku || "—";
    }

    // If query failed with network/server error
    if (isBatchError) {
      return "تعذر جلب بيانات الصنف";
    }

    // If still resolving
    if (isBatchLoading) {
      return "جاري التحميل...";
    }

    // Not found in catalog: never expose raw UUID to the user!
    if (UUID_PATTERN.test(cleanId)) {
      return "غير متوفر بالكتالوج";
    }

    // If it's a human-readable identifier (e.g. var-raw-api-powder), show it
    return cleanId;
  };

  const getProductSku = (id?: string | null): string | undefined => {
    if (!id) return undefined;
    const cleanId = id.trim();

    const resolved = resolvedMap.get(cleanId);
    if (resolved?.sku) return resolved.sku;

    const match = products.find((p) => p.id === cleanId || p.defaultVariantId === cleanId);
    return match?.sku ?? undefined;
  };

  return {
    products,
    isLoading: isListLoading,
    isResolving: isBatchLoading,
    isError: isBatchError,
    getProductDisplayName,
    getProductSku
  };
}

import { useQuery } from "@tanstack/react-query";
import { catalogApi, type ProductSummary } from "../../catalog/public.ts";
import { useManufacturingScope } from "./use-manufacturing-scope.ts";

export interface CatalogProductLookup {
  readonly products: readonly ProductSummary[];
  readonly isLoading: boolean;
  readonly getProductDisplayName: (variantOrProductId?: string | null) => string;
  readonly getProductSku: (variantOrProductId?: string | null) => string | undefined;
}

export function useCatalogProducts(): CatalogProductLookup {
  const { legalEntityId } = useManufacturingScope();

  const { data, isLoading } = useQuery({
    queryKey: ["todir", "catalog", "products-lookup", legalEntityId],
    queryFn: async () => {
      try {
        const res = await catalogApi.list({ pageSize: 200 }, legalEntityId ?? undefined);
        return res.items ?? [];
      } catch {
        return [];
      }
    },
    staleTime: 60_000
  });

  const products = data ?? [];

  const getProductDisplayName = (id?: string | null): string => {
    if (!id) return "—";
    const match = products.find((p) => p.id === id || p.defaultVariantId === id);
    if (match) {
      return match.displayName || match.nameAr || match.nameEn || id;
    }
    return id;
  };

  const getProductSku = (id?: string | null): string | undefined => {
    if (!id) return undefined;
    const match = products.find((p) => p.id === id || p.defaultVariantId === id);
    return match?.sku ?? undefined;
  };

  return {
    products,
    isLoading,
    getProductDisplayName,
    getProductSku
  };
}

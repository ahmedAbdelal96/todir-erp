import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { pricingApi } from "../services/pricing-api.ts";
import { loadSessionContext } from "../../../core/api/auth-service.ts";
import { useRuntimeContext, useOptionalPermissions } from "../../../core/runtime/runtime-provider.tsx";
import type {
  CreatePriceEntryRequest,
  CreatePriceGroupRequest,
  CreatePriceListAssignmentRequest,
  CreatePriceListRequest,
  CreatePriceRuleRequest,
  CreatePriceVersionRequest,
  PriceCoverageDto,
  PriceListDto,
  PriceResolution,
  PriceVersionDto,
  PriceVersionContentPageDto,
  PricingImportCommitDto,
  PricingImportPreviewDto,
  PricingImportRequest,
  PricingPolicyDto,
  PricingPolicyRequest,
  ResolvePriceRequest
} from "../types.ts";

export interface PricingContextScope {
  readonly tenantId?: string | undefined;
  readonly legalEntityId?: string | undefined;
}

export function usePricingScope(explicit?: PricingContextScope): {
  tenantId: string;
  legalEntityId: string;
} {
  const sessionQuery = useQuery({
    queryKey: ["session", "context"],
    queryFn: loadSessionContext,
    staleTime: 60_000
  });

  let runtimeEntityId = "";
  let runtimeTenantId = "";

  try {
    const runtime = useRuntimeContext();
    runtimeEntityId = (runtime as unknown as { legalEntity?: { legalEntityId?: string } }).legalEntity?.legalEntityId || "";
    runtimeTenantId = (runtime as unknown as { tenant?: { tenantId?: string } }).tenant?.tenantId || "";
  } catch {
    // Graceful fallback outside RuntimeProvider
  }

  const tenantId =
    explicit?.tenantId ||
    sessionQuery.data?.tenantId ||
    runtimeTenantId ||
    "";

  let legalEntityId =
    explicit?.legalEntityId ||
    sessionQuery.data?.context?.legalEntities?.[0]?.id ||
    runtimeEntityId ||
    "";

  if (typeof window !== "undefined") {
    const storedEntity = window.sessionStorage.getItem("todir_active_legal_entity");
    if (storedEntity && !explicit?.legalEntityId) {
      legalEntityId = storedEntity;
    }
  }

  return { tenantId, legalEntityId };
}

export const pricingQueryKeys = {
  all: ["pricing"] as const,
  policy: (entityId: string) => ["pricing", "policy", entityId] as const,
  lists: (entityId: string) => ["pricing", "lists", entityId] as const,
  versions: (listId: string, entityId: string) => ["pricing", "versions", listId, entityId] as const,
  versionContent: (listId: string, versionId: string, entityId: string, page: number, pageSize: number) => ["pricing", "version-content", listId, versionId, entityId, page, pageSize] as const,
  coverage: (entityId: string, listId: string, date: string) =>
    ["pricing", "coverage", entityId, listId, date] as const,
  resolve: (req: ResolvePriceRequest) =>
    ["pricing", "resolve", req.legalEntityId, req.direction, req.productVariantId, req.unitOfMeasureId, req.quantity, req.pricingDate, req.transactionCurrencyCode, req.partyId, req.priceListId] as const
};

export function usePricingPolicy(entityId: string) {
  return useQuery<PricingPolicyDto>({
    queryKey: pricingQueryKeys.policy(entityId),
    queryFn: () => pricingApi.getPolicy(entityId),
    enabled: Boolean(entityId),
    staleTime: 60_000
  });
}

export function usePriceLists(entityId: string) {
  return useQuery<readonly PriceListDto[]>({
    queryKey: pricingQueryKeys.lists(entityId),
    queryFn: () => pricingApi.listPriceLists(entityId),
    enabled: Boolean(entityId),
    staleTime: 60_000
  });
}

export function usePriceVersions(listId: string, entityId: string) {
  return useQuery<readonly PriceVersionDto[]>({
    queryKey: pricingQueryKeys.versions(listId, entityId),
    queryFn: () => pricingApi.listPriceVersions(listId, entityId),
    enabled: Boolean(listId && entityId),
    staleTime: 60_000
  });
}

export function usePriceVersionContent(listId: string, versionId: string, entityId: string, page: number, pageSize: number) {
  return useQuery<PriceVersionContentPageDto>({
    queryKey: pricingQueryKeys.versionContent(listId, versionId, entityId, page, pageSize),
    queryFn: () => pricingApi.listVersionContent(listId, versionId, entityId, page, pageSize),
    enabled: Boolean(listId && versionId && entityId),
    staleTime: 30_000
  });
}

export function usePricingCoverage(entityId: string, priceListId: string, pricingDate: string) {
  return useQuery<PriceCoverageDto>({
    queryKey: pricingQueryKeys.coverage(entityId, priceListId, pricingDate),
    queryFn: () => pricingApi.getCoverage(entityId, priceListId, pricingDate),
    enabled: Boolean(entityId && priceListId && pricingDate),
    retry: false,
    staleTime: 60_000
  });
}

export function useResolvePriceQuery(request: ResolvePriceRequest | null) {
  return useQuery<PriceResolution>({
    queryKey: request ? pricingQueryKeys.resolve(request) : ["pricing", "resolve", "none"],
    queryFn: () => {
      if (!request) throw new Error("No request");
      return pricingApi.resolvePrice(request);
    },
    enabled: Boolean(request && request.legalEntityId && request.productVariantId && request.unitOfMeasureId),
    retry: 1,
    staleTime: 30_000
  });
}

// Mutations
export function useCreatePriceListMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (req: CreatePriceListRequest) => pricingApi.createPriceList(req),
    onSuccess: (_, variables) => {
      void queryClient.invalidateQueries({ queryKey: pricingQueryKeys.lists(variables.legalEntityId) });
    }
  });
}

export function useArchivePriceListMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ listId, legalEntityId, expectedVersion }: { listId: string; legalEntityId: string; expectedVersion: number }) =>
      pricingApi.archivePriceList(listId, legalEntityId, expectedVersion),
    onSuccess: (_, variables) => {
      void queryClient.invalidateQueries({ queryKey: pricingQueryKeys.lists(variables.legalEntityId) });
    }
  });
}

export function useCreatePriceVersionMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ listId, legalEntityId, request }: { listId: string; legalEntityId: string; request: CreatePriceVersionRequest }) =>
      pricingApi.createPriceVersion(listId, legalEntityId, request),
    onSuccess: (_, variables) => {
      void queryClient.invalidateQueries({ queryKey: pricingQueryKeys.versions(variables.listId, variables.legalEntityId) });
    }
  });
}

export function useAddPriceEntryMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ versionId, listId, legalEntityId, request }: { versionId: string; listId: string; legalEntityId: string; request: CreatePriceEntryRequest }) =>
      pricingApi.addPriceEntry(versionId, listId, legalEntityId, request),
    onSuccess: (_, variables) => {
      void queryClient.invalidateQueries({ queryKey: pricingQueryKeys.versions(variables.listId, variables.legalEntityId) });
      void queryClient.invalidateQueries({ queryKey: pricingQueryKeys.all });
    }
  });
}

export function useAddPriceRuleMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ versionId, listId, legalEntityId, request }: { versionId: string; listId: string; legalEntityId: string; request: CreatePriceRuleRequest }) =>
      pricingApi.addPriceRule(versionId, listId, legalEntityId, request),
    onSuccess: (_, variables) => {
      void queryClient.invalidateQueries({ queryKey: pricingQueryKeys.versions(variables.listId, variables.legalEntityId) });
      void queryClient.invalidateQueries({ queryKey: pricingQueryKeys.all });
    }
  });
}

export function useValidateVersionMutation() {
  return useMutation({
    mutationFn: ({ versionId, legalEntityId }: { versionId: string; legalEntityId: string }) =>
      pricingApi.validateVersion(versionId, legalEntityId)
  });
}

export function useActivateVersionMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ versionId, legalEntityId, listId }: { versionId: string; legalEntityId: string; listId: string }) =>
      pricingApi.activateVersion(versionId, legalEntityId),
    onSuccess: (_, variables) => {
      void queryClient.invalidateQueries({ queryKey: pricingQueryKeys.versions(variables.listId, variables.legalEntityId) });
      void queryClient.invalidateQueries({ queryKey: pricingQueryKeys.lists(variables.legalEntityId) });
      void queryClient.invalidateQueries({ queryKey: pricingQueryKeys.all });
    }
  });
}

export function useScheduleVersionMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ versionId, legalEntityId, listId }: { versionId: string; legalEntityId: string; listId: string }) =>
      pricingApi.scheduleVersion(versionId, legalEntityId),
    onSuccess: (_, variables) => {
      void queryClient.invalidateQueries({ queryKey: pricingQueryKeys.versions(variables.listId, variables.legalEntityId) });
      void queryClient.invalidateQueries({ queryKey: pricingQueryKeys.lists(variables.legalEntityId) });
    }
  });
}

export function useUpdatePricingPolicyMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ legalEntityId, request }: { legalEntityId: string; request: PricingPolicyRequest }) =>
      pricingApi.updatePolicy(legalEntityId, request),
    onSuccess: (_, variables) => {
      void queryClient.invalidateQueries({ queryKey: pricingQueryKeys.policy(variables.legalEntityId) });
    }
  });
}

export function useCreatePriceGroupMutation() {
  return useMutation({
    mutationFn: ({ legalEntityId, request }: { legalEntityId: string; request: CreatePriceGroupRequest }) =>
      pricingApi.createGroup(legalEntityId, request)
  });
}

export function useCreateAssignmentMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreatePriceListAssignmentRequest) => pricingApi.createAssignment(request),
    onSuccess: (_, variables) => {
      void queryClient.invalidateQueries({ queryKey: pricingQueryKeys.lists(variables.legalEntityId) });
    }
  });
}

export function useSimulatePriceMutation() {
  return useMutation({
    mutationFn: (req: ResolvePriceRequest) => pricingApi.simulatePrice(req)
  });
}

export function useExplainPriceMutation() {
  return useMutation({
    mutationFn: (req: ResolvePriceRequest) => pricingApi.explainPrice(req)
  });
}

export function usePreviewImportMutation() {
  return useMutation({
    mutationFn: (req: PricingImportRequest) => pricingApi.previewImport(req)
  });
}

export function useCommitImportMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ batchId, legalEntityId }: { batchId: string; legalEntityId: string }) =>
      pricingApi.commitImport(batchId, legalEntityId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: pricingQueryKeys.all });
    }
  });
}

export function useLookupProducts(legalEntityId: string, search = "") {
  const normalizedSearch = search.trim();
  return useQuery({
    queryKey: ["pricing", "lookup", "products", legalEntityId, normalizedSearch],
    queryFn: () => pricingApi.getLookupProducts(legalEntityId, normalizedSearch),
    enabled: Boolean(legalEntityId),
    staleTime: 60_000
  });
}

export function useLookupUoms() {
  return useQuery({
    queryKey: ["pricing", "lookup", "uoms"],
    queryFn: () => pricingApi.getLookupUoms(),
    staleTime: 60_000
  });
}

export function useLookupParties(legalEntityId: string) {
  return useQuery({
    queryKey: ["pricing", "lookup", "parties", legalEntityId],
    queryFn: () => pricingApi.getLookupParties(legalEntityId),
    staleTime: 60_000
  });
}

export function usePricingManagePermission(): boolean {
  let optionalPermissions;
  try {
    optionalPermissions = useOptionalPermissions();
  } catch {
    // outside provider
  }

  const userEmail = typeof window !== "undefined" ? window.sessionStorage.getItem("erp.session.email") ?? "anon" : "server";
  const isAdmin = userEmail.toLowerCase().includes("admin");

  const { data: serverCanManage } = useQuery({
    queryKey: ["auth", "check", "pricing.list.manage", userEmail],
    queryFn: async () => {
      try {
        const res = await fetch("/api/v1/authorization/check?permission=pricing.list.manage", {
          headers: { "X-Correlation-Id": crypto.randomUUID() }
        });
        return res.ok;
      } catch {
        return false;
      }
    },
    staleTime: 30_000,
    initialData: isAdmin ? true : false
  });

  if (optionalPermissions) {
    return optionalPermissions.hasAnyPermission("pricing.list.manage", "pricing.manage", "pricing.write", "pricing.*", "*", "admin");
  }

  return serverCanManage ?? isAdmin;
}

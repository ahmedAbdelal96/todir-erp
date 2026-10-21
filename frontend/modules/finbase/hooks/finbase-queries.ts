import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { finbaseApi } from "../services/finbase-api.ts";
import { loadSessionContext } from "../../../core/api/auth-service.ts";
import type {
  ConvertedMoneyDto,
  ConvertMoneyRequest,
  CreateExchangeRateRequest,
  CreateExchangeRateSourceConfigRequest,
  CreatePaymentTermRequest,
  CreatePaymentTermVersionRequest,
  CreateRoundingPolicyRequest,
  EnableCurrencyRequest,
  PaymentScheduleLineDto,
  PaymentScheduleRequest,
  PaymentTermLifecycleRequest,
  UpdateFinancialPolicyRequest
} from "../types.ts";
import { queryKeyFactory } from "../../../core/api/query-keys.ts";
import { useRuntimeContext } from "../../../core/runtime/runtime-provider.tsx";

export interface FinBaseContextScope {
  readonly tenantId?: string;
  readonly legalEntityId?: string;
}

export function useFinBaseScope(explicit?: FinBaseContextScope): {
  tenantId: string;
  legalEntityId: string;
  baseCurrencyCode: string;
} {
  const sessionQuery = useQuery({
    queryKey: ["session", "context"],
    queryFn: loadSessionContext,
    staleTime: 60_000
  });

  let runtimeEntityId = "";
  let runtimeTenantId = "";
  let runtimeBaseCurrency = "";

  try {
    const runtime = useRuntimeContext();
    runtimeEntityId = runtime.legalEntity.legalEntityId || "";
    runtimeTenantId = runtime.tenant.tenantId || "";
    runtimeBaseCurrency = runtime.legalEntity.baseCurrencyCode || "";
  } catch {
    // Handled gracefully outside RuntimeProvider
  }

  const tenantId =
    explicit?.tenantId ||
    runtimeTenantId ||
    sessionQuery.data?.tenantId ||
    "default-tenant";

  const legalEntityId =
    explicit?.legalEntityId ||
    runtimeEntityId ||
    sessionQuery.data?.context?.legalEntities?.[0]?.id ||
    "";

  const baseCurrencyCode =
    runtimeBaseCurrency ||
    "EGP";

  return { tenantId, legalEntityId, baseCurrencyCode };
}

export const finbaseKeys = {
  currencies: (tenantId: string) =>
    [...queryKeyFactory.tenant(tenantId), "finbase", "currencies"] as const,
  entityCurrencies: (tenantId: string, entityId: string) =>
    [
      ...queryKeyFactory.entity(tenantId, entityId),
      "finbase",
      "entity-currencies"
    ] as const,
  exchangeRates: (tenantId: string, entityId: string) =>
    [
      ...queryKeyFactory.entity(tenantId, entityId),
      "finbase",
      "exchange-rates"
    ] as const,
  rateSources: (tenantId: string, entityId: string) =>
    [
      ...queryKeyFactory.entity(tenantId, entityId),
      "finbase",
      "rate-sources"
    ] as const,
  financialPolicy: (tenantId: string, entityId: string) =>
    [
      ...queryKeyFactory.entity(tenantId, entityId),
      "finbase",
      "financial-policy"
    ] as const,
  roundingPolicies: (tenantId: string) =>
    [...queryKeyFactory.tenant(tenantId), "finbase", "rounding-policies"] as const,
  paymentTerms: (tenantId: string, includeArchived: boolean) =>
    [
      ...queryKeyFactory.tenant(tenantId),
      "finbase",
      "payment-terms",
      { includeArchived }
    ] as const,
  paymentTermDetail: (tenantId: string, termId: string) =>
    [
      ...queryKeyFactory.tenant(tenantId),
      "finbase",
      "payment-term",
      termId
    ] as const
};

export function useFinBaseCurrencies() {
  const { tenantId } = useFinBaseScope();
  return useQuery({
    queryKey: finbaseKeys.currencies(tenantId),
    queryFn: () => finbaseApi.listCurrencies()
  });
}

export function useEntityCurrencies(explicitEntityId?: string) {
  const { tenantId, legalEntityId: defaultEntityId } = useFinBaseScope();
  const entityId = explicitEntityId || defaultEntityId;
  return useQuery({
    queryKey: finbaseKeys.entityCurrencies(tenantId, entityId),
    queryFn: () => finbaseApi.listEntityCurrencies(entityId),
    enabled: Boolean(entityId)
  });
}

export function useEnableCurrencyMutation() {
  const queryClient = useQueryClient();
  const { tenantId, legalEntityId } = useFinBaseScope();
  return useMutation({
    mutationFn: ({
      entityId,
      code,
      request
    }: {
      entityId?: string;
      code: string;
      request?: EnableCurrencyRequest;
    }) =>
      finbaseApi.enableCurrency(entityId || legalEntityId, code, request),
    onSuccess: (_, variables) => {
      const targetEntityId = variables.entityId || legalEntityId;
      void queryClient.invalidateQueries({
        queryKey: finbaseKeys.entityCurrencies(tenantId, targetEntityId)
      });
    }
  });
}

export function useExchangeRates(explicitEntityId?: string) {
  const { tenantId, legalEntityId: defaultEntityId } = useFinBaseScope();
  const entityId = explicitEntityId || defaultEntityId;
  return useQuery({
    queryKey: finbaseKeys.exchangeRates(tenantId, entityId),
    queryFn: () => finbaseApi.listExchangeRates(entityId),
    enabled: Boolean(entityId)
  });
}

export function useCreateExchangeRateMutation() {
  const queryClient = useQueryClient();
  const { tenantId, legalEntityId } = useFinBaseScope();
  return useMutation({
    mutationFn: (request: CreateExchangeRateRequest) =>
      finbaseApi.createExchangeRate(request),
    onSuccess: (_, variables) => {
      const targetEntityId = variables.legalEntityId || legalEntityId;
      void queryClient.invalidateQueries({
        queryKey: finbaseKeys.exchangeRates(tenantId, targetEntityId)
      });
    }
  });
}

export function useApproveExchangeRateMutation() {
  const queryClient = useQueryClient();
  const { tenantId, legalEntityId } = useFinBaseScope();
  return useMutation({
    mutationFn: ({
      entityId,
      id
    }: {
      entityId?: string;
      id: string;
    }) =>
      finbaseApi.approveExchangeRate(entityId || legalEntityId, id),
    onSuccess: (_, variables) => {
      const targetEntityId = variables.entityId || legalEntityId;
      void queryClient.invalidateQueries({
        queryKey: finbaseKeys.exchangeRates(tenantId, targetEntityId)
      });
    }
  });
}

export function useRateSources(explicitEntityId?: string) {
  const { tenantId, legalEntityId: defaultEntityId } = useFinBaseScope();
  const entityId = explicitEntityId || defaultEntityId;
  return useQuery({
    queryKey: finbaseKeys.rateSources(tenantId, entityId),
    queryFn: () => finbaseApi.listRateSources(entityId),
    enabled: Boolean(entityId)
  });
}

export function useCreateRateSourceMutation() {
  const queryClient = useQueryClient();
  const { tenantId, legalEntityId } = useFinBaseScope();
  return useMutation({
    mutationFn: ({
      entityId,
      request
    }: {
      entityId?: string;
      request: CreateExchangeRateSourceConfigRequest;
    }) =>
      finbaseApi.createRateSource(entityId || legalEntityId, request),
    onSuccess: (_, variables) => {
      const targetEntityId = variables.entityId || legalEntityId;
      void queryClient.invalidateQueries({
        queryKey: finbaseKeys.rateSources(tenantId, targetEntityId)
      });
    }
  });
}

export function useFinancialPolicy(explicitEntityId?: string) {
  const { tenantId, legalEntityId: defaultEntityId } = useFinBaseScope();
  const entityId = explicitEntityId || defaultEntityId;
  return useQuery({
    queryKey: finbaseKeys.financialPolicy(tenantId, entityId),
    queryFn: () => finbaseApi.getFinancialPolicy(entityId),
    enabled: Boolean(entityId)
  });
}

export function useUpdateFinancialPolicyMutation() {
  const queryClient = useQueryClient();
  const { tenantId, legalEntityId } = useFinBaseScope();
  return useMutation({
    mutationFn: ({
      entityId,
      request
    }: {
      entityId?: string;
      request: UpdateFinancialPolicyRequest;
    }) =>
      finbaseApi.updateFinancialPolicy(entityId || legalEntityId, request),
    onSuccess: (_, variables) => {
      const targetEntityId = variables.entityId || legalEntityId;
      void queryClient.invalidateQueries({
        queryKey: finbaseKeys.financialPolicy(tenantId, targetEntityId)
      });
    }
  });
}

export function useRoundingPolicies() {
  const { tenantId } = useFinBaseScope();
  return useQuery({
    queryKey: finbaseKeys.roundingPolicies(tenantId),
    queryFn: () => finbaseApi.listRoundingPolicies()
  });
}

export function useCreateRoundingPolicyMutation() {
  const queryClient = useQueryClient();
  const { tenantId } = useFinBaseScope();
  return useMutation({
    mutationFn: (request: CreateRoundingPolicyRequest) =>
      finbaseApi.createRoundingPolicy(request),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: finbaseKeys.roundingPolicies(tenantId)
      });
    }
  });
}

export function usePaymentTerms(includeArchived = false) {
  const { tenantId } = useFinBaseScope();
  return useQuery({
    queryKey: finbaseKeys.paymentTerms(tenantId, includeArchived),
    queryFn: () => finbaseApi.listPaymentTerms(includeArchived)
  });
}

export function usePaymentTermHistory(termId?: string) {
  const { tenantId } = useFinBaseScope();
  return useQuery({
    queryKey: finbaseKeys.paymentTermDetail(tenantId, termId || ""),
    queryFn: () => finbaseApi.getPaymentTermHistory(termId!),
    enabled: Boolean(termId)
  });
}

export function useCreatePaymentTermMutation() {
  const queryClient = useQueryClient();
  const { tenantId } = useFinBaseScope();
  return useMutation({
    mutationFn: (request: CreatePaymentTermRequest) =>
      finbaseApi.createPaymentTerm(request),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: [...queryKeyFactory.tenant(tenantId), "finbase", "payment-terms"]
      });
    }
  });
}

export function useCreatePaymentTermVersionMutation() {
  const queryClient = useQueryClient();
  const { tenantId } = useFinBaseScope();
  return useMutation({
    mutationFn: ({
      termId,
      request
    }: {
      termId: string;
      request: CreatePaymentTermVersionRequest;
    }) =>
      finbaseApi.createPaymentTermVersion(termId, request),
    onSuccess: (_, variables) => {
      void queryClient.invalidateQueries({
        queryKey: finbaseKeys.paymentTermDetail(tenantId, variables.termId)
      });
      void queryClient.invalidateQueries({
        queryKey: [...queryKeyFactory.tenant(tenantId), "finbase", "payment-terms"]
      });
    }
  });
}

export function useActivatePaymentTermVersionMutation() {
  const queryClient = useQueryClient();
  const { tenantId } = useFinBaseScope();
  return useMutation({
    mutationFn: ({
      termId,
      versionId,
      request
    }: {
      termId: string;
      versionId: string;
      request: PaymentTermLifecycleRequest;
    }) =>
      finbaseApi.activatePaymentTermVersion(termId, versionId, request),
    onSuccess: (_, variables) => {
      void queryClient.invalidateQueries({
        queryKey: finbaseKeys.paymentTermDetail(tenantId, variables.termId)
      });
      void queryClient.invalidateQueries({
        queryKey: [...queryKeyFactory.tenant(tenantId), "finbase", "payment-terms"]
      });
    }
  });
}

export function useArchivePaymentTermMutation() {
  const queryClient = useQueryClient();
  const { tenantId } = useFinBaseScope();
  return useMutation({
    mutationFn: ({
      termId,
      request
    }: {
      termId: string;
      request: PaymentTermLifecycleRequest;
    }) =>
      finbaseApi.archivePaymentTerm(termId, request),
    onSuccess: (_, variables) => {
      void queryClient.invalidateQueries({
        queryKey: finbaseKeys.paymentTermDetail(tenantId, variables.termId)
      });
      void queryClient.invalidateQueries({
        queryKey: [...queryKeyFactory.tenant(tenantId), "finbase", "payment-terms"]
      });
    }
  });
}

export function useConversionPreviewMutation() {
  return useMutation<ConvertedMoneyDto, Error, ConvertMoneyRequest>({
    mutationFn: (request: ConvertMoneyRequest) => finbaseApi.previewConversion(request)
  });
}

export function useSchedulePreviewMutation() {
  return useMutation<readonly PaymentScheduleLineDto[], Error, PaymentScheduleRequest>({
    mutationFn: (request: PaymentScheduleRequest) => finbaseApi.previewSchedule(request)
  });
}

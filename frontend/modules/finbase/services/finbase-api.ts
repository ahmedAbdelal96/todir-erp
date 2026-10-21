import { httpClient, type RequestOptions } from "../../../core/api/http-client.ts";
import type {
  ConvertedMoneyDto,
  ConvertMoneyRequest,
  CreateExchangeRateRequest,
  CreateExchangeRateSourceConfigRequest,
  CreatePaymentTermRequest,
  CreatePaymentTermVersionRequest,
  CreateRoundingPolicyRequest,
  CurrencyDto,
  EnableCurrencyRequest,
  EntityCurrencyDto,
  ExchangeRateDto,
  ExchangeRateSourceConfigDto,
  FinancialPolicyDto,
  PaymentScheduleLineDto,
  PaymentScheduleRequest,
  PaymentTermDto,
  PaymentTermHistoryDto,
  PaymentTermLifecycleRequest,
  PaymentTermListItemDto,
  PaymentTermVersionDto,
  RoundingPolicyDto,
  UpdateFinancialPolicyRequest
} from "../types.ts";

const basePath = "/api/v1/finbase";

function opts(legalEntityId?: string, extra?: RequestOptions): RequestOptions | undefined {
  if (!legalEntityId && !extra) return undefined;
  const res: RequestOptions = extra ? { ...extra } : {};
  if (legalEntityId) {
    return { ...res, legalEntityId };
  }
  return res;
}

export const finbaseApi = {
  // Currencies
  listCurrencies: (options?: RequestOptions) =>
    httpClient<readonly CurrencyDto[]>(`${basePath}/currencies`, options),

  listEntityCurrencies: (entityId: string, options?: RequestOptions) =>
    httpClient<readonly EntityCurrencyDto[]>(
      `${basePath}/legal-entities/${encodeURIComponent(entityId)}/currencies`,
      opts(entityId, options)
    ),

  enableCurrency: (
    entityId: string,
    code: string,
    request: EnableCurrencyRequest = {},
    options?: RequestOptions
  ) =>
    httpClient<EntityCurrencyDto>(
      `${basePath}/legal-entities/${encodeURIComponent(entityId)}/currencies/${encodeURIComponent(code)}/enable`,
      opts(entityId, {
        method: "POST",
        body: JSON.stringify(request),
        ...options
      })
    ),

  // Exchange Rates
  listExchangeRates: (entityId: string, options?: RequestOptions) =>
    httpClient<readonly ExchangeRateDto[]>(
      `${basePath}/legal-entities/${encodeURIComponent(entityId)}/exchange-rates`,
      opts(entityId, options)
    ),

  createExchangeRate: (request: CreateExchangeRateRequest, options?: RequestOptions) =>
    httpClient<ExchangeRateDto>(
      `${basePath}/exchange-rates`,
      opts(request.legalEntityId, {
        method: "POST",
        body: JSON.stringify(request),
        ...options
      })
    ),

  approveExchangeRate: (entityId: string, id: string, options?: RequestOptions) =>
    httpClient<ExchangeRateDto>(
      `${basePath}/legal-entities/${encodeURIComponent(entityId)}/exchange-rates/${encodeURIComponent(id)}/approve`,
      opts(entityId, {
        method: "POST",
        ...options
      })
    ),

  listRateSources: (entityId: string, options?: RequestOptions) =>
    httpClient<readonly ExchangeRateSourceConfigDto[]>(
      `${basePath}/legal-entities/${encodeURIComponent(entityId)}/rate-sources`,
      opts(entityId, options)
    ),

  createRateSource: (
    entityId: string,
    request: CreateExchangeRateSourceConfigRequest,
    options?: RequestOptions
  ) =>
    httpClient<ExchangeRateSourceConfigDto>(
      `${basePath}/legal-entities/${encodeURIComponent(entityId)}/rate-sources`,
      opts(entityId, {
        method: "POST",
        body: JSON.stringify(request),
        ...options
      })
    ),

  previewConversion: (request: ConvertMoneyRequest, options?: RequestOptions) =>
    httpClient<ConvertedMoneyDto>(
      `${basePath}/conversion/preview`,
      opts(request.legalEntityId, {
        method: "POST",
        body: JSON.stringify(request),
        ...options
      })
    ),

  // Financial Policy
  getFinancialPolicy: (entityId: string, options?: RequestOptions) =>
    httpClient<FinancialPolicyDto>(
      `${basePath}/legal-entities/${encodeURIComponent(entityId)}/financial-policy`,
      opts(entityId, options)
    ),

  updateFinancialPolicy: (
    entityId: string,
    request: UpdateFinancialPolicyRequest,
    options?: RequestOptions
  ) =>
    httpClient<FinancialPolicyDto>(
      `${basePath}/legal-entities/${encodeURIComponent(entityId)}/financial-policy`,
      opts(entityId, {
        method: "PUT",
        body: JSON.stringify(request),
        ...options
      })
    ),

  // Rounding Policies
  listRoundingPolicies: (options?: RequestOptions) =>
    httpClient<readonly RoundingPolicyDto[]>(`${basePath}/rounding-policies`, options),

  createRoundingPolicy: (request: CreateRoundingPolicyRequest, options?: RequestOptions) =>
    httpClient<RoundingPolicyDto>(`${basePath}/rounding-policies`, {
      method: "POST",
      body: JSON.stringify(request),
      ...options
    }),

  // Payment Terms
  listPaymentTerms: (includeArchived = false, options?: RequestOptions) => {
    const query = includeArchived ? "?includeArchived=true" : "";
    return httpClient<readonly PaymentTermListItemDto[]>(
      `${basePath}/payment-terms${query}`,
      options
    );
  },

  getPaymentTermHistory: (termId: string, options?: RequestOptions) =>
    httpClient<PaymentTermHistoryDto>(
      `${basePath}/payment-terms/${encodeURIComponent(termId)}`,
      options
    ),

  createPaymentTerm: (request: CreatePaymentTermRequest, options?: RequestOptions) =>
    httpClient<PaymentTermDto>(`${basePath}/payment-terms`, {
      method: "POST",
      body: JSON.stringify(request),
      ...options
    }),

  createPaymentTermVersion: (
    termId: string,
    request: CreatePaymentTermVersionRequest,
    options?: RequestOptions
  ) =>
    httpClient<PaymentTermVersionDto>(
      `${basePath}/payment-terms/${encodeURIComponent(termId)}/versions`,
      {
        method: "POST",
        body: JSON.stringify(request),
        ...options
      }
    ),

  activatePaymentTermVersion: (
    termId: string,
    versionId: string,
    request: PaymentTermLifecycleRequest,
    options?: RequestOptions
  ) =>
    httpClient<PaymentTermVersionDto>(
      `${basePath}/payment-terms/${encodeURIComponent(termId)}/versions/${encodeURIComponent(versionId)}/activate`,
      {
        method: "POST",
        body: JSON.stringify(request),
        ...options
      }
    ),

  archivePaymentTerm: (
    termId: string,
    request: PaymentTermLifecycleRequest,
    options?: RequestOptions
  ) =>
    httpClient<{ readonly version: number }>(
      `${basePath}/payment-terms/${encodeURIComponent(termId)}/archive`,
      {
        method: "POST",
        body: JSON.stringify(request),
        ...options
      }
    ),

  previewSchedule: (request: PaymentScheduleRequest, options?: RequestOptions) =>
    httpClient<readonly PaymentScheduleLineDto[]>(
      `${basePath}/payment-schedule/preview`,
      {
        method: "POST",
        body: JSON.stringify(request),
        ...options
      }
    )
};

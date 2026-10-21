import { httpClient, type RequestOptions } from "../../../core/api/http-client.ts";
import type {
  CreatePriceEntryRequest,
  CreatePriceGroupRequest,
  CreatePriceListAssignmentRequest,
  CreatePriceListRequest,
  CreatePriceRuleRequest,
  CreatePriceVersionRequest,
  PriceCoverageDto,
  PriceListDto,
  PriceListLifecycleRequest,
  PriceResolution,
  PriceVersionDto,
  PricingImportCommitDto,
  PricingImportPreviewDto,
  PricingImportRequest,
  PricingPolicyDto,
  PricingPolicyRequest,
  ResolvePriceBatchRequest,
  ResolvePriceRequest
} from "../types.ts";

const basePath = "/api/v1/pricing";

function opts(legalEntityId?: string, extra?: RequestOptions): RequestOptions | undefined {
  if (!legalEntityId && !extra) return undefined;
  const res: RequestOptions = extra ? { ...extra } : {};
  if (legalEntityId) {
    return { ...res, legalEntityId };
  }
  return res;
}

export const pricingApi = {
  // Policies
  getPolicy: (legalEntityId: string, options?: RequestOptions) =>
    httpClient<PricingPolicyDto>(
      `${basePath}/policy?legalEntityId=${encodeURIComponent(legalEntityId)}`,
      opts(legalEntityId, options)
    ),

  updatePolicy: (legalEntityId: string, request: PricingPolicyRequest, options?: RequestOptions) =>
    httpClient<PricingPolicyDto>(
      `${basePath}/policy?legalEntityId=${encodeURIComponent(legalEntityId)}`,
      opts(legalEntityId, {
        ...options,
        method: "PUT",
        body: JSON.stringify(request)
      })
    ),

  // Price Lists
  listPriceLists: (legalEntityId: string, options?: RequestOptions) =>
    httpClient<readonly PriceListDto[]>(
      `${basePath}/price-lists?legalEntityId=${encodeURIComponent(legalEntityId)}`,
      opts(legalEntityId, options)
    ),

  createPriceList: (request: CreatePriceListRequest, options?: RequestOptions) =>
    httpClient<PriceListDto>(
      `${basePath}/price-lists`,
      opts(request.legalEntityId, {
        ...options,
        method: "POST",
        body: JSON.stringify(request)
      })
    ),

  archivePriceList: (listId: string, legalEntityId: string, expectedVersion: number, options?: RequestOptions) =>
    httpClient<{ listId: string; status: string }>(
      `${basePath}/price-lists/${encodeURIComponent(listId)}/archive?legalEntityId=${encodeURIComponent(legalEntityId)}`,
      opts(legalEntityId, {
        ...options,
        method: "POST",
        body: JSON.stringify({ expectedVersion } as PriceListLifecycleRequest)
      })
    ),

  // Versions
  listPriceVersions: (listId: string, legalEntityId: string, options?: RequestOptions) =>
    httpClient<readonly PriceVersionDto[]>(
      `${basePath}/price-lists/${encodeURIComponent(listId)}/versions?legalEntityId=${encodeURIComponent(legalEntityId)}`,
      opts(legalEntityId, options)
    ),

  createPriceVersion: (listId: string, legalEntityId: string, request: CreatePriceVersionRequest, options?: RequestOptions) =>
    httpClient<PriceVersionDto>(
      `${basePath}/price-lists/${encodeURIComponent(listId)}/versions?legalEntityId=${encodeURIComponent(legalEntityId)}`,
      opts(legalEntityId, {
        ...options,
        method: "POST",
        body: JSON.stringify(request)
      })
    ),

  // Entries & Rules
  addPriceEntry: (
    versionId: string,
    listId: string,
    legalEntityId: string,
    request: CreatePriceEntryRequest,
    options?: RequestOptions
  ) =>
    httpClient<{ versionId: string }>(
      `${basePath}/versions/${encodeURIComponent(versionId)}/entries?listId=${encodeURIComponent(listId)}&legalEntityId=${encodeURIComponent(legalEntityId)}`,
      opts(legalEntityId, {
        ...options,
        method: "POST",
        body: JSON.stringify(request)
      })
    ),

  addPriceRule: (
    versionId: string,
    listId: string,
    legalEntityId: string,
    request: CreatePriceRuleRequest,
    options?: RequestOptions
  ) =>
    httpClient<{ versionId: string }>(
      `${basePath}/versions/${encodeURIComponent(versionId)}/rules?listId=${encodeURIComponent(listId)}&legalEntityId=${encodeURIComponent(legalEntityId)}`,
      opts(legalEntityId, {
        ...options,
        method: "POST",
        body: JSON.stringify(request)
      })
    ),

  validateVersion: (versionId: string, legalEntityId: string, options?: RequestOptions) =>
    httpClient<{ ruleCount: number; entryCount: number }>(
      `${basePath}/versions/${encodeURIComponent(versionId)}/validate?legalEntityId=${encodeURIComponent(legalEntityId)}`,
      opts(legalEntityId, {
        ...options,
        method: "POST"
      })
    ),

  activateVersion: (versionId: string, legalEntityId: string, options?: RequestOptions) =>
    httpClient<PriceVersionDto>(
      `${basePath}/versions/${encodeURIComponent(versionId)}/activate?legalEntityId=${encodeURIComponent(legalEntityId)}`,
      opts(legalEntityId, {
        ...options,
        method: "POST"
      })
    ),

  scheduleVersion: (versionId: string, legalEntityId: string, options?: RequestOptions) =>
    httpClient<PriceVersionDto>(
      `${basePath}/versions/${encodeURIComponent(versionId)}/schedule?legalEntityId=${encodeURIComponent(legalEntityId)}`,
      opts(legalEntityId, {
        ...options,
        method: "POST"
      })
    ),

  // Groups & Assignments
  createGroup: (legalEntityId: string, request: CreatePriceGroupRequest, options?: RequestOptions) =>
    httpClient<{ id: string }>(
      `${basePath}/price-groups?legalEntityId=${encodeURIComponent(legalEntityId)}`,
      opts(legalEntityId, {
        ...options,
        method: "POST",
        body: JSON.stringify(request)
      })
    ),

  createAssignment: (request: CreatePriceListAssignmentRequest, options?: RequestOptions) =>
    httpClient<{ priceListId: string }>(
      `${basePath}/assignments`,
      opts(request.legalEntityId, {
        ...options,
        method: "POST",
        body: JSON.stringify(request)
      })
    ),

  // Resolution & Simulation
  resolvePrice: (request: ResolvePriceRequest, options?: RequestOptions) =>
    httpClient<PriceResolution>(
      `${basePath}/resolve`,
      opts(request.legalEntityId, {
        ...options,
        method: "POST",
        body: JSON.stringify(request)
      })
    ),

  resolveBatch: (request: ResolvePriceBatchRequest, options?: RequestOptions) =>
    httpClient<readonly PriceResolution[]>(
      `${basePath}/resolve-batch`,
      opts(request.items[0]?.legalEntityId, {
        ...options,
        method: "POST",
        body: JSON.stringify(request)
      })
    ),

  simulatePrice: (request: ResolvePriceRequest, options?: RequestOptions) =>
    httpClient<PriceResolution>(
      `${basePath}/simulate`,
      opts(request.legalEntityId, {
        ...options,
        method: "POST",
        body: JSON.stringify(request)
      })
    ),

  explainPrice: (request: ResolvePriceRequest, options?: RequestOptions) =>
    httpClient<PriceResolution>(
      `${basePath}/explain`,
      opts(request.legalEntityId, {
        ...options,
        method: "POST",
        body: JSON.stringify(request)
      })
    ),

  // Imports & Exports
  previewImport: (request: PricingImportRequest, options?: RequestOptions) =>
    httpClient<PricingImportPreviewDto>(
      `${basePath}/imports`,
      opts(request.legalEntityId, {
        ...options,
        method: "POST",
        body: JSON.stringify(request)
      })
    ),

  commitImport: (batchId: string, legalEntityId: string, options?: RequestOptions) =>
    httpClient<PricingImportCommitDto>(
      `${basePath}/imports/${encodeURIComponent(batchId)}/commit?legalEntityId=${encodeURIComponent(legalEntityId)}`,
      opts(legalEntityId, {
        ...options,
        method: "POST"
      })
    ),

  exportCsv: async (listId: string, versionId: string, legalEntityId: string): Promise<string> => {
    const res = await fetch(
      `${basePath}/price-lists/${encodeURIComponent(listId)}/versions/${encodeURIComponent(versionId)}/export?legalEntityId=${encodeURIComponent(legalEntityId)}`,
      {
        headers: { "X-Legal-Entity-Id": legalEntityId }
      }
    );
    if (!res.ok) throw new Error("Failed to export price list CSV");
    return res.text();
  },

  getCoverage: (legalEntityId: string, priceListId: string, pricingDate: string, options?: RequestOptions) =>
    httpClient<PriceCoverageDto>(
      `${basePath}/coverage?legalEntityId=${encodeURIComponent(legalEntityId)}&priceListId=${encodeURIComponent(priceListId)}&pricingDate=${encodeURIComponent(pricingDate)}`,
      opts(legalEntityId, options)
    ),

  // Commercial lookup contracts (Catalog / Directory / FinBase)
  getLookupProducts: (legalEntityId: string, options?: RequestOptions) =>
    httpClient<{ items?: readonly any[] } | readonly any[]>(
      `/api/v1/catalog/products?legalEntityId=${encodeURIComponent(legalEntityId)}&pageSize=100`,
      opts(legalEntityId, options)
    ),

  getLookupUoms: (options?: RequestOptions) =>
    httpClient<readonly any[]>(`/api/v1/catalog/uoms`, options),

  getLookupParties: (legalEntityId: string, options?: RequestOptions) =>
    httpClient<{ items?: readonly any[] } | readonly any[]>(
      `/api/v1/directory/parties?pageSize=50`,
      opts(legalEntityId, options)
    )
};

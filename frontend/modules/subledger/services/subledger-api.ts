import { httpClient, type RequestOptions } from "../../../core/api/http-client.ts";
import type {
  AgingBucketResult,
  ApplicationPreviewResult,
  ApplyOpenItemsCommand,
  AutoApplySuggestionQuery,
  AutoApplySuggestionResult,
  CreateOpeningSubledgerBatchCommand,
  CreateWriteOffCommand,
  OpenItemListFilter,
  OpenItemPage,
  OpeningSubledgerBatchResult,
  OpeningSubledgerLineCommand,
  OpeningSubledgerValidationResult,
  PartyBalanceResult,
  PartyFinancialProfile,
  PartyOpenItem,
  PartyStatementResult,
  ReconciliationQuery,
  ReconciliationResult,
  ReconciliationSourceContext,
  ReverseApplicationCommand,
  SubledgerApplicationResult,
  SubledgerDirection,
  UpdateFinancialProfilePayload,
  UpdateWriteOffPolicyPayload,
  WriteOffResult
} from "../types.ts";

const basePath = "/api/v1/subledger";

function opts(legalEntityId?: string, extra?: RequestOptions): RequestOptions | undefined {
  if (!legalEntityId && !extra) return undefined;
  const res: RequestOptions = extra ? { ...extra } : {};
  if (legalEntityId) {
    return { ...res, legalEntityId };
  }
  return res;
}

export const subledgerApi = {
  // Open Items
  listOpenItems: (filter: OpenItemListFilter, options?: RequestOptions) => {
    const params = new URLSearchParams();
    params.set("legalEntityId", filter.legalEntityId);
    if (filter.partyId) params.set("partyId", filter.partyId);
    if (filter.direction) params.set("direction", filter.direction);
    if (filter.openOnly !== undefined) params.set("openOnly", String(filter.openOnly));
    if (filter.limit !== undefined) params.set("limit", String(filter.limit));
    if (filter.page !== undefined) params.set("page", String(filter.page));
    if (filter.cursor) params.set("cursor", filter.cursor);
    if (filter.search) params.set("search", filter.search);
    if (filter.dueFrom) params.set("dueFrom", filter.dueFrom);
    if (filter.dueTo) params.set("dueTo", filter.dueTo);
    if (filter.currencyCode) params.set("currencyCode", filter.currencyCode);
    if (filter.overdueOnly !== undefined) params.set("overdueOnly", String(filter.overdueOnly));
    if (filter.asOfDate) params.set("asOfDate", filter.asOfDate);
    if (filter.status) params.set("status", filter.status);
    if (filter.sort) params.set("sort", filter.sort);

    return httpClient<OpenItemPage>(
      `${basePath}/open-items?${params.toString()}`,
      opts(filter.legalEntityId, options)
    );
  },

  getOpenItem: (legalEntityId: string, openItemId: string, options?: RequestOptions) =>
    httpClient<PartyOpenItem>(
      `${basePath}/open-items/${encodeURIComponent(openItemId)}?legalEntityId=${encodeURIComponent(legalEntityId)}`,
      opts(legalEntityId, options)
    ),

  // Applications & Settlements
  suggestApplications: (query: AutoApplySuggestionQuery, options?: RequestOptions) =>
    httpClient<AutoApplySuggestionResult>(
      `${basePath}/applications/suggest`,
      opts(query.legalEntityId, {
        ...options,
        method: "POST",
        body: JSON.stringify(query)
      })
    ),

  previewApplication: (command: ApplyOpenItemsCommand, options?: RequestOptions) =>
    httpClient<ApplicationPreviewResult>(
      `${basePath}/applications/preview`,
      opts(command.legalEntityId, {
        ...options,
        method: "POST",
        body: JSON.stringify(command)
      })
    ),

  applyOpenItems: (command: ApplyOpenItemsCommand, options?: RequestOptions) =>
    httpClient<SubledgerApplicationResult>(
      `${basePath}/applications`,
      opts(command.legalEntityId, {
        ...options,
        method: "POST",
        body: JSON.stringify(command)
      })
    ),

  reverseApplication: (command: ReverseApplicationCommand, options?: RequestOptions) =>
    httpClient<SubledgerApplicationResult>(
      `${basePath}/applications/${encodeURIComponent(command.applicationId)}/reverse`,
      opts(command.legalEntityId, {
        ...options,
        method: "POST",
        body: JSON.stringify(command)
      })
    ),

  // Party Financial Profile & Exposure
  getFinancialProfile: (legalEntityId: string, partyId: string, options?: RequestOptions) =>
    httpClient<PartyFinancialProfile>(
      `${basePath}/parties/${encodeURIComponent(partyId)}/financial-profile?legalEntityId=${encodeURIComponent(legalEntityId)}`,
      opts(legalEntityId, options)
    ),

  updateFinancialProfile: (partyId: string, payload: UpdateFinancialProfilePayload, options?: RequestOptions) =>
    httpClient<PartyFinancialProfile>(
      `${basePath}/parties/${encodeURIComponent(partyId)}/financial-profile`,
      opts(payload.legalEntityId, {
        ...options,
        method: "PUT",
        body: JSON.stringify(payload)
      })
    ),

  getPartyExposure: (
    legalEntityId: string,
    partyId: string,
    direction: SubledgerDirection,
    asOfDate?: string,
    options?: RequestOptions
  ) => {
    const params = new URLSearchParams();
    params.set("legalEntityId", legalEntityId);
    params.set("direction", direction);
    if (asOfDate) params.set("asOfDate", asOfDate);

    return httpClient<PartyBalanceResult>(
      `${basePath}/parties/${encodeURIComponent(partyId)}/exposure?${params.toString()}`,
      opts(legalEntityId, options)
    );
  },

  // Reports: Aging & Statements
  getAgingReport: (
    legalEntityId: string,
    partyId: string,
    direction: SubledgerDirection,
    asOfDate: string,
    currencyCode?: string,
    options?: RequestOptions
  ) => {
    const params = new URLSearchParams();
    params.set("legalEntityId", legalEntityId);
    params.set("partyId", partyId);
    params.set("direction", direction);
    params.set("asOfDate", asOfDate);
    if (currencyCode) params.set("currencyCode", currencyCode);

    return httpClient<readonly AgingBucketResult[]>(
      `${basePath}/reports/aging?${params.toString()}`,
      opts(legalEntityId, options)
    );
  },

  getPartyStatement: (
    legalEntityId: string,
    partyId: string,
    direction: SubledgerDirection,
    fromDate: string,
    toDate: string,
    currencyCode?: string,
    limit?: number,
    page?: number,
    options?: RequestOptions
  ) => {
    const params = new URLSearchParams();
    params.set("legalEntityId", legalEntityId);
    params.set("direction", direction);
    params.set("fromDate", fromDate);
    params.set("toDate", toDate);
    if (currencyCode) params.set("currencyCode", currencyCode);
    if (limit !== undefined) params.set("limit", String(limit));
    if (page !== undefined) params.set("page", String(page));

    return httpClient<PartyStatementResult>(
      `${basePath}/parties/${encodeURIComponent(partyId)}/statement?${params.toString()}`,
      opts(legalEntityId, options)
    );
  },

  // Write-Offs
  updateWriteOffPolicy: (payload: UpdateWriteOffPolicyPayload, options?: RequestOptions) =>
    httpClient<unknown>(
      `${basePath}/write-off-policies`,
      opts(payload.legalEntityId, {
        ...options,
        method: "PUT",
        body: JSON.stringify(payload)
      })
    ),

  createWriteOffRequest: (command: CreateWriteOffCommand, options?: RequestOptions) =>
    httpClient<string>(
      `${basePath}/write-offs/requests`,
      opts(command.legalEntityId, {
        ...options,
        method: "POST",
        body: JSON.stringify(command)
      })
    ),

  approveWriteOff: (requestId: string, legalEntityId: string, options?: RequestOptions) =>
    httpClient<WriteOffResult>(
      `${basePath}/write-offs/requests/${encodeURIComponent(requestId)}/approve?legalEntityId=${encodeURIComponent(legalEntityId)}`,
      opts(legalEntityId, {
        ...options,
        method: "POST"
      })
    ),

  // Control Reconciliation
  getReconciliationControlAccounts: (legalEntityId: string, asOfDate: string, options?: RequestOptions) =>
    httpClient<readonly ReconciliationSourceContext[]>(
      `${basePath}/reconciliation/control-accounts?legalEntityId=${encodeURIComponent(legalEntityId)}&asOfDate=${encodeURIComponent(asOfDate)}`,
      opts(legalEntityId, options)
    ),

  runReconciliation: (query: ReconciliationQuery, options?: RequestOptions) =>
    httpClient<ReconciliationResult>(
      `${basePath}/reconciliation/run`,
      opts(query.legalEntityId, {
        ...options,
        method: "POST",
        body: JSON.stringify(query)
      })
    ),

  getReconciliationRun: (legalEntityId: string, runId: string, options?: RequestOptions) =>
    httpClient<ReconciliationResult>(
      `${basePath}/reconciliation/runs/${encodeURIComponent(runId)}?legalEntityId=${encodeURIComponent(legalEntityId)}`,
      opts(legalEntityId, options)
    ),

  // Opening Balance Migration
  createOpeningBatch: (command: CreateOpeningSubledgerBatchCommand, options?: RequestOptions) =>
    httpClient<string>(
      `${basePath}/opening-batches`,
      opts(command.legalEntityId, {
        ...options,
        method: "POST",
        body: JSON.stringify(command)
      })
    ),

  addOpeningLines: (
    batchId: string,
    legalEntityId: string,
    lines: readonly OpeningSubledgerLineCommand[],
    options?: RequestOptions
  ) =>
    httpClient<{ batchId: string; added: number }>(
      `${basePath}/opening-batches/${encodeURIComponent(batchId)}/lines?legalEntityId=${encodeURIComponent(legalEntityId)}`,
      opts(legalEntityId, {
        ...options,
        method: "POST",
        body: JSON.stringify(lines)
      })
    ),

  validateOpeningBatch: (batchId: string, legalEntityId: string, options?: RequestOptions) =>
    httpClient<OpeningSubledgerValidationResult>(
      `${basePath}/opening-batches/${encodeURIComponent(batchId)}/validate?legalEntityId=${encodeURIComponent(legalEntityId)}`,
      opts(legalEntityId, {
        ...options,
        method: "POST"
      })
    ),

  finalizeOpeningBatch: (batchId: string, legalEntityId: string, options?: RequestOptions) =>
    httpClient<OpeningSubledgerBatchResult>(
      `${basePath}/opening-batches/${encodeURIComponent(batchId)}/finalize?legalEntityId=${encodeURIComponent(legalEntityId)}`,
      opts(legalEntityId, {
        ...options,
        method: "POST"
      })
    )
};

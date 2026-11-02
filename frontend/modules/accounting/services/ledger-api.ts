import { httpClient } from "../../../core/api/http-client.ts";
import type {
  AccountBalanceDto,
  AccountDto,
  AccountSearchPageDto,
  AccountWorkspaceDto,
  CreateAccountRequest,
  CreateFinancialLockRequest,
  CreateFiscalYearRequest,
  CreateGeneralJournalDraftRequest,
  CreateJournalRequest,
  CreateOpeningBalanceBatchRequest,
  CreatePeriodRequest,
  DimensionDefinitionRequest,
  DimensionPolicyRequest,
  DimensionValueRequest,
  EntryExplanationDto,
  FinancialLockDto,
  FiscalYearDto,
  GeneralJournalDraftDto,
  GeneralLedgerPageDto,
  JournalDto,
  JournalEntryDto,
  JournalEntryPageDto,
  LedgerMode,
  LedgerModeDto,
  LedgerPostingResult,
  LedgerReadinessDto,
  ManagedLedgerSetupDto,
  ManagedLedgerSetupRequest,
  OpeningBalancePreviewDto,
  PeriodCloseCheckDto,
  PeriodDto,
  PostingPreviewDto,
  PostingPreviewRequest,
  ProjectionRebuildResult,
  ProjectionReconciliationResultDto,
  ReverseEntryRequest,
  TrialBalanceRowDto,
  TrialBalancePageDto
} from "../types.ts";

export const ledgerApi = {
  // Mode & Readiness
  getReadiness: async (legalEntityId: string): Promise<LedgerReadinessDto> => {
    return httpClient<LedgerReadinessDto>(`/api/v1/ledger/legal-entities/${legalEntityId}/readiness`);
  },

  getMode: async (legalEntityId: string): Promise<LedgerModeDto> => {
    return httpClient<LedgerModeDto>(`/api/v1/ledger/legal-entities/${legalEntityId}/mode`);
  },

  changeMode: async (legalEntityId: string, mode: LedgerMode, version: number): Promise<LedgerModeDto> => {
    return httpClient<LedgerModeDto>(`/api/v1/ledger/legal-entities/${legalEntityId}/mode`, {
      method: "PUT",
      body: JSON.stringify({ legalEntityId, mode, version })
    });
  },

  setupManaged: async (
    legalEntityId: string,
    request?: ManagedLedgerSetupRequest
  ): Promise<ManagedLedgerSetupDto> => {
    return httpClient<ManagedLedgerSetupDto>(`/api/v1/ledger/legal-entities/${legalEntityId}/managed-setup`, {
      method: "POST",
      body: JSON.stringify(request ?? { templateCode: "development-core", mode: "Managed" })
    });
  },

  // Chart of Accounts
  listAccounts: async (legalEntityId: string): Promise<readonly AccountDto[]> => {
    return httpClient<readonly AccountDto[]>(`/api/v1/ledger/legal-entities/${legalEntityId}/accounts`);
  },

  searchAccounts: async (
    legalEntityId: string,
    query?: string,
    cursor?: string,
    limit: number = 50
  ): Promise<AccountSearchPageDto> => {
    const params = new URLSearchParams();
    if (query) params.set("query", query);
    if (cursor) params.set("cursor", cursor);
    if (limit) params.set("limit", limit.toString());
    const queryStr = params.toString() ? `?${params.toString()}` : "";
    return httpClient<AccountSearchPageDto>(`/api/v1/ledger/legal-entities/${legalEntityId}/accounts/search${queryStr}`);
  },

  createAccount: async (legalEntityId: string, request: CreateAccountRequest): Promise<AccountDto> => {
    return httpClient<AccountDto>(`/api/v1/ledger/legal-entities/${legalEntityId}/accounts`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  archiveAccount: async (legalEntityId: string, accountId: string): Promise<boolean> => {
    return httpClient<boolean>(`/api/v1/ledger/legal-entities/${legalEntityId}/accounts/${accountId}/archive`, {
      method: "POST"
    });
  },

  getAccountWorkspace: async (legalEntityId: string, accountId: string): Promise<AccountWorkspaceDto> => {
    return httpClient<AccountWorkspaceDto>(`/api/v1/ledger/legal-entities/${legalEntityId}/accounts/${accountId}/workspace`);
  },

  getAccountBalance: async (
    legalEntityId: string,
    accountId: string,
    from?: string,
    to?: string
  ): Promise<AccountBalanceDto> => {
    const params = new URLSearchParams();
    if (from) params.set("from", from);
    if (to) params.set("to", to);
    const queryStr = params.toString() ? `?${params.toString()}` : "";
    return httpClient<AccountBalanceDto>(`/api/v1/ledger/legal-entities/${legalEntityId}/accounts/${accountId}/balance${queryStr}`);
  },

  // Journals
  listJournals: async (legalEntityId: string): Promise<readonly JournalDto[]> => {
    return httpClient<readonly JournalDto[]>(`/api/v1/ledger/legal-entities/${legalEntityId}/journals`);
  },

  createJournal: async (legalEntityId: string, request: CreateJournalRequest): Promise<JournalDto> => {
    return httpClient<JournalDto>(`/api/v1/ledger/legal-entities/${legalEntityId}/journals`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  // Fiscal Years & Periods
  listFiscalYears: async (legalEntityId: string): Promise<readonly FiscalYearDto[]> => {
    return httpClient<readonly FiscalYearDto[]>(`/api/v1/ledger/legal-entities/${legalEntityId}/fiscal-years`);
  },

  createFiscalYear: async (legalEntityId: string, request: CreateFiscalYearRequest): Promise<FiscalYearDto> => {
    return httpClient<FiscalYearDto>(`/api/v1/ledger/legal-entities/${legalEntityId}/fiscal-years`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  listPeriods: async (legalEntityId: string): Promise<readonly PeriodDto[]> => {
    return httpClient<readonly PeriodDto[]>(`/api/v1/ledger/legal-entities/${legalEntityId}/periods`);
  },

  createPeriod: async (legalEntityId: string, request: CreatePeriodRequest): Promise<PeriodDto> => {
    return httpClient<PeriodDto>(`/api/v1/ledger/legal-entities/${legalEntityId}/periods`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  softClosePeriod: async (legalEntityId: string, periodId: string): Promise<boolean> => {
    return httpClient<boolean>(`/api/v1/ledger/legal-entities/${legalEntityId}/periods/${periodId}/soft-close`, {
      method: "POST"
    });
  },

  closePeriod: async (legalEntityId: string, periodId: string): Promise<boolean> => {
    return httpClient<boolean>(`/api/v1/ledger/legal-entities/${legalEntityId}/periods/${periodId}/close`, {
      method: "POST"
    });
  },

  reopenPeriod: async (legalEntityId: string, periodId: string): Promise<boolean> => {
    return httpClient<boolean>(`/api/v1/ledger/legal-entities/${legalEntityId}/periods/${periodId}/reopen`, {
      method: "POST"
    });
  },

  getPeriodCloseReadiness: async (legalEntityId: string, periodId: string): Promise<PeriodCloseCheckDto> => {
    return httpClient<PeriodCloseCheckDto>(`/api/v1/ledger/legal-entities/${legalEntityId}/periods/${periodId}/close-readiness`);
  },

  // Locks
  listLocks: async (legalEntityId: string): Promise<readonly FinancialLockDto[]> => {
    return httpClient<readonly FinancialLockDto[]>(`/api/v1/ledger/legal-entities/${legalEntityId}/locks`);
  },

  createLock: async (legalEntityId: string, request: CreateFinancialLockRequest): Promise<FinancialLockDto> => {
    return httpClient<FinancialLockDto>(`/api/v1/ledger/legal-entities/${legalEntityId}/locks`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  // Journal Entries & Explanation
  listEntries: async (legalEntityId: string, limit: number = 50): Promise<readonly JournalEntryDto[]> => {
    return httpClient<readonly JournalEntryDto[]>(`/api/v1/ledger/legal-entities/${legalEntityId}/entries?limit=${limit}`);
  },

  listEntriesPage: async (
    legalEntityId: string,
    params?: { from?: string | undefined; to?: string | undefined; search?: string | undefined; status?: string | undefined; cursor?: string | undefined; limit?: number | undefined; sort?: string | undefined }
  ): Promise<JournalEntryPageDto> => {
    const searchParams = new URLSearchParams();
    for (const [key, value] of Object.entries(params ?? {})) if (value) searchParams.set(key, String(value));
    const query = searchParams.toString();
    return httpClient<JournalEntryPageDto>(`/api/v1/ledger/legal-entities/${legalEntityId}/entries/page${query ? `?${query}` : ""}`);
  },

  explainEntry: async (legalEntityId: string, entryId: string): Promise<EntryExplanationDto> => {
    return httpClient<EntryExplanationDto>(`/api/v1/ledger/legal-entities/${legalEntityId}/entries/${entryId}/explanation`);
  },

  reverseEntry: async (
    legalEntityId: string,
    entryId: string,
    request: ReverseEntryRequest
  ): Promise<LedgerPostingResult> => {
    return httpClient<LedgerPostingResult>(`/api/v1/ledger/legal-entities/${legalEntityId}/entries/${entryId}/reverse`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  // Manual General Journal Drafts
  createGeneralJournalDraft: async (
    legalEntityId: string,
    request: CreateGeneralJournalDraftRequest
  ): Promise<GeneralJournalDraftDto> => {
    return httpClient<GeneralJournalDraftDto>(`/api/v1/ledger/legal-entities/${legalEntityId}/general-journals`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  updateGeneralJournalDraft: async (
    legalEntityId: string,
    draftId: string,
    request: CreateGeneralJournalDraftRequest
  ): Promise<GeneralJournalDraftDto> => {
    return httpClient<GeneralJournalDraftDto>(`/api/v1/ledger/legal-entities/${legalEntityId}/general-journals/${draftId}`, {
      method: "PUT",
      body: JSON.stringify(request)
    });
  },

  submitGeneralJournal: async (legalEntityId: string, draftId: string): Promise<GeneralJournalDraftDto> => {
    return httpClient<GeneralJournalDraftDto>(`/api/v1/ledger/legal-entities/${legalEntityId}/general-journals/${draftId}/submit`, {
      method: "POST"
    });
  },

  approveGeneralJournal: async (legalEntityId: string, draftId: string): Promise<GeneralJournalDraftDto> => {
    return httpClient<GeneralJournalDraftDto>(`/api/v1/ledger/legal-entities/${legalEntityId}/general-journals/${draftId}/approve`, {
      method: "POST"
    });
  },

  postGeneralJournal: async (legalEntityId: string, draftId: string): Promise<LedgerPostingResult> => {
    return httpClient<LedgerPostingResult>(`/api/v1/ledger/legal-entities/${legalEntityId}/general-journals/${draftId}/post`, {
      method: "POST"
    });
  },

  previewPosting: async (
    legalEntityId: string,
    request: PostingPreviewRequest
  ): Promise<PostingPreviewDto> => {
    return httpClient<PostingPreviewDto>(`/api/v1/ledger/legal-entities/${legalEntityId}/posting-preview`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  // Opening Balances
  createOpeningBatch: async (
    legalEntityId: string,
    request: CreateOpeningBalanceBatchRequest
  ): Promise<string> => {
    return httpClient<string>(`/api/v1/ledger/legal-entities/${legalEntityId}/opening-balances`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  previewOpeningBatch: async (legalEntityId: string, batchId: string): Promise<OpeningBalancePreviewDto> => {
    return httpClient<OpeningBalancePreviewDto>(`/api/v1/ledger/legal-entities/${legalEntityId}/opening-balances/${batchId}/preview`, {
      method: "POST"
    });
  },

  postOpeningBatch: async (legalEntityId: string, batchId: string): Promise<LedgerPostingResult> => {
    return httpClient<LedgerPostingResult>(`/api/v1/ledger/legal-entities/${legalEntityId}/opening-balances/${batchId}/post`, {
      method: "POST"
    });
  },

  // Reports
  getTrialBalance: async (
    legalEntityId: string,
    params?: { from?: string | undefined; to?: string | undefined; limit?: number | undefined }
  ): Promise<readonly TrialBalanceRowDto[]> => {
    const searchParams = new URLSearchParams();
    if (params?.from) searchParams.set("from", params.from);
    if (params?.to) searchParams.set("to", params.to);
    if (params?.limit) searchParams.set("limit", params.limit.toString());
    const queryStr = searchParams.toString() ? `?${searchParams.toString()}` : "";
    return httpClient<readonly TrialBalanceRowDto[]>(`/api/v1/ledger/legal-entities/${legalEntityId}/reports/trial-balance${queryStr}`);
  },

  getTrialBalancePage: async (
    legalEntityId: string,
    params?: { from?: string | undefined; to?: string | undefined; search?: string | undefined; cursor?: string | undefined; limit?: number | undefined; sort?: string | undefined }
  ): Promise<TrialBalancePageDto> => {
    const searchParams = new URLSearchParams();
    for (const [key, value] of Object.entries(params ?? {})) if (value) searchParams.set(key, String(value));
    const query = searchParams.toString();
    return httpClient<TrialBalancePageDto>(`/api/v1/ledger/legal-entities/${legalEntityId}/reports/trial-balance/page${query ? `?${query}` : ""}`);
  },

  getGeneralLedger: async (
    legalEntityId: string,
    params?: {
      from?: string | undefined;
      to?: string | undefined;
      accountId?: string | undefined;
      journalId?: string | undefined;
      sourceModule?: string | undefined;
      sourceReference?: string | undefined;
      dimensionDefinitionId?: string | undefined;
      dimensionValueId?: string | undefined;
      entryNumber?: string | undefined;
      cursor?: string | undefined;
      limit?: number | undefined;
    }
  ): Promise<GeneralLedgerPageDto> => {
    const searchParams = new URLSearchParams();
    if (params?.from) searchParams.set("from", params.from);
    if (params?.to) searchParams.set("to", params.to);
    if (params?.accountId) searchParams.set("accountId", params.accountId);
    if (params?.journalId) searchParams.set("journalId", params.journalId);
    if (params?.sourceModule) searchParams.set("sourceModule", params.sourceModule);
    if (params?.sourceReference) searchParams.set("sourceReference", params.sourceReference);
    if (params?.dimensionDefinitionId) searchParams.set("dimensionDefinitionId", params.dimensionDefinitionId);
    if (params?.dimensionValueId) searchParams.set("dimensionValueId", params.dimensionValueId);
    if (params?.entryNumber) searchParams.set("entryNumber", params.entryNumber);
    if (params?.cursor) searchParams.set("cursor", params.cursor);
    if (params?.limit) searchParams.set("limit", params.limit.toString());
    const queryStr = searchParams.toString() ? `?${searchParams.toString()}` : "";
    return httpClient<GeneralLedgerPageDto>(`/api/v1/ledger/legal-entities/${legalEntityId}/reports/general-ledger${queryStr}`);
  },

  reconcileBalances: async (
    legalEntityId: string,
    fiscalYearId?: string,
    periodId?: string
  ): Promise<ProjectionReconciliationResultDto> => {
    const searchParams = new URLSearchParams();
    if (fiscalYearId) searchParams.set("fiscalYearId", fiscalYearId);
    if (periodId) searchParams.set("periodId", periodId);
    const queryStr = searchParams.toString() ? `?${searchParams.toString()}` : "";
    return httpClient<ProjectionReconciliationResultDto>(`/api/v1/ledger/legal-entities/${legalEntityId}/reports/reconcile-balances${queryStr}`);
  },

  rebuildBalances: async (
    legalEntityId: string,
    fiscalYearId?: string,
    periodId?: string
  ): Promise<ProjectionRebuildResult> => {
    const searchParams = new URLSearchParams();
    if (fiscalYearId) searchParams.set("fiscalYearId", fiscalYearId);
    if (periodId) searchParams.set("periodId", periodId);
    const queryStr = searchParams.toString() ? `?${searchParams.toString()}` : "";
    return httpClient<ProjectionRebuildResult>(`/api/v1/ledger/legal-entities/${legalEntityId}/reports/rebuild-balances${queryStr}`, {
      method: "POST"
    });
  },

  // Dimensions
  createDimension: async (legalEntityId: string, request: DimensionDefinitionRequest): Promise<string> => {
    return httpClient<string>(`/api/v1/ledger/legal-entities/${legalEntityId}/dimensions`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  createDimensionValue: async (legalEntityId: string, request: DimensionValueRequest): Promise<string> => {
    return httpClient<string>(`/api/v1/ledger/legal-entities/${legalEntityId}/dimensions/values`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  createDimensionPolicy: async (legalEntityId: string, request: DimensionPolicyRequest): Promise<string> => {
    return httpClient<string>(`/api/v1/ledger/legal-entities/${legalEntityId}/dimension-policies`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  }
};

import { httpClient, generateKey } from "../../../core/api/http-client.ts";
import type {
  TreasuryAccountDto,
  TreasuryAccountPage,
  TreasuryAccountType,
  TreasuryAccountStatus,
  CreateTreasuryAccountRequest,
  UpdateTreasuryAccountRequest,
  StatusRequest,
  AccountChangeRequestDto,
  CreateAccountChangeRequest,
  StatementDto,
  StatementPage,
  ImportStatementRequest,
  StatementLineDto,
  StatementLinePage,
  ValidateStatementRequest,
  ClassifyStatementLineRequest,
  CreatePaymentProposalRequest,
  BankOriginatedCandidateDto,
  ReconciliationDto,
  ReconciliationPage,
  CreateReconciliationRequest,
  CreateMatchRequest,
  ReconciliationSuggestionDto,
  UnmatchRequest,
  OpeningReconciliationDto,
  OpeningReconciliationPage,
  CreateOpeningReconciliationRequest,
  CashCountDto,
  CashCountPage,
  CreateCashCountRequest,
  ResolveCashDifferenceRequest,
  CashDifferenceResolutionDto,
  CustodyTransferDto,
  TransferCustodyRequest,
  CashPositionDto,
  ForecastPage,
  CreateForecastVersionRequest,
  ForecastVersionDto,
  CreateForecastItemRequest,
  ForecastItemDto,
  ConnectionDto,
  CreateConnectionRequest,
  PosCashSettlementDto,
  PosCashSettlementPage,
  ConfirmPosCashSettlementRequest,
  ProviderSettlementDto,
  ProviderSettlementPage,
  MatchProviderSettlementRequest,
  CreateTransferRequest,
  TransferDto,
  TransferReconciliationDto,
  TransferReconciliationPage,
  ReconcileTransferRequest,
  BankActivityPage,
  CashToLedgerReportDto,
  ChequeInTransitPage,
  TransactionCodeMappingDto,
  CreateTransactionCodeMappingRequest,
  CreateReconciliationRuleSetRequest,
  ReconciliationRuleSetDto
} from "../types.ts";

const BASE_URL = "/api/v1/banking";

function buildQuery(params: Record<string, string | number | boolean | undefined | null>): string {
  const searchParams = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== null && value !== "") {
      searchParams.append(key, String(value));
    }
  }
  const str = searchParams.toString();
  return str ? `?${str}` : "";
}

export const bankingApi = {
  // Treasury Accounts
  listAccounts(
    legalEntityId: string,
    filters?: {
      search?: string | undefined;
      type?: TreasuryAccountType | undefined;
      status?: TreasuryAccountStatus | undefined;
      page?: number | undefined;
      pageSize?: number | undefined;
    }
  ): Promise<TreasuryAccountPage> {
    const qs = buildQuery({ legalEntityId, ...filters });
    return httpClient<TreasuryAccountPage>(`${BASE_URL}/treasury-accounts${qs}`, { legalEntityId });
  },

  getAccount(legalEntityId: string, accountId: string): Promise<TreasuryAccountDto> {
    const qs = buildQuery({ legalEntityId });
    return httpClient<TreasuryAccountDto>(`${BASE_URL}/treasury-accounts/${accountId}${qs}`, { legalEntityId });
  },

  createAccount(legalEntityId: string, request: CreateTreasuryAccountRequest): Promise<TreasuryAccountDto> {
    return httpClient<TreasuryAccountDto>(`${BASE_URL}/treasury-accounts`, {
      method: "POST",
      body: JSON.stringify(request),
      legalEntityId
    });
  },

  updateAccount(legalEntityId: string, accountId: string, request: UpdateTreasuryAccountRequest): Promise<TreasuryAccountDto> {
    const qs = buildQuery({ legalEntityId });
    return httpClient<TreasuryAccountDto>(`${BASE_URL}/treasury-accounts/${accountId}${qs}`, {
      method: "PUT",
      body: JSON.stringify(request),
      legalEntityId
    });
  },

  updateAccountStatus(legalEntityId: string, accountId: string, request: StatusRequest): Promise<TreasuryAccountDto> {
    const qs = buildQuery({ legalEntityId });
    return httpClient<TreasuryAccountDto>(`${BASE_URL}/treasury-accounts/${accountId}/status${qs}`, {
      method: "PUT",
      body: JSON.stringify(request),
      legalEntityId
    });
  },

  activateAccount(legalEntityId: string, accountId: string, version: number): Promise<TreasuryAccountDto> {
    const qs = buildQuery({ legalEntityId });
    return httpClient<TreasuryAccountDto>(`${BASE_URL}/treasury-accounts/${accountId}/activate${qs}`, {
      method: "POST",
      body: JSON.stringify({ version }),
      legalEntityId
    });
  },

  suspendAccount(legalEntityId: string, accountId: string, version: number): Promise<TreasuryAccountDto> {
    const qs = buildQuery({ legalEntityId });
    return httpClient<TreasuryAccountDto>(`${BASE_URL}/treasury-accounts/${accountId}/suspend${qs}`, {
      method: "POST",
      body: JSON.stringify({ version }),
      legalEntityId
    });
  },

  closeAccount(legalEntityId: string, accountId: string, version: number): Promise<TreasuryAccountDto> {
    const qs = buildQuery({ legalEntityId });
    return httpClient<TreasuryAccountDto>(`${BASE_URL}/treasury-accounts/${accountId}/close-request${qs}`, {
      method: "POST",
      body: JSON.stringify({ version }),
      legalEntityId
    });
  },

  reopenAccount(legalEntityId: string, accountId: string, version: number): Promise<TreasuryAccountDto> {
    const qs = buildQuery({ legalEntityId });
    return httpClient<TreasuryAccountDto>(`${BASE_URL}/treasury-accounts/${accountId}/reopen${qs}`, {
      method: "POST",
      body: JSON.stringify({ version }),
      legalEntityId
    });
  },

  createChangeRequest(legalEntityId: string, accountId: string, request: CreateAccountChangeRequest): Promise<AccountChangeRequestDto> {
    const qs = buildQuery({ legalEntityId });
    return httpClient<AccountChangeRequestDto>(`${BASE_URL}/treasury-accounts/${accountId}/change-requests${qs}`, {
      method: "POST",
      body: JSON.stringify(request),
      legalEntityId
    });
  },

  decideChangeRequest(legalEntityId: string, requestId: string, decision: "approve" | "reject", version: number): Promise<AccountChangeRequestDto> {
    const qs = buildQuery({ legalEntityId });
    return httpClient<AccountChangeRequestDto>(`${BASE_URL}/treasury-account-change-requests/${requestId}/${decision}${qs}`, {
      method: "POST",
      body: JSON.stringify({ version, decision: decision === "approve" ? "Approved" : "Rejected" }),
      legalEntityId
    });
  },

  // Bank Connections
  listConnections(legalEntityId: string, accountId: string): Promise<readonly ConnectionDto[]> {
    const qs = buildQuery({ legalEntityId });
    return httpClient<readonly ConnectionDto[]>(`${BASE_URL}/treasury-accounts/${accountId}/connections${qs}`, { legalEntityId });
  },

  createConnection(legalEntityId: string, accountId: string, request: CreateConnectionRequest): Promise<ConnectionDto> {
    const qs = buildQuery({ legalEntityId });
    return httpClient<ConnectionDto>(`${BASE_URL}/treasury-accounts/${accountId}/connections${qs}`, {
      method: "POST",
      body: JSON.stringify(request),
      legalEntityId
    });
  },

  refreshConnection(legalEntityId: string, connectionId: string, version: number = 1): Promise<void> {
    const qs = buildQuery({ legalEntityId });
    return httpClient<void>(`${BASE_URL}/connections/${connectionId}/refresh${qs}`, {
      method: "POST",
      body: JSON.stringify({ version }),
      legalEntityId
    });
  },

  disconnectConnection(legalEntityId: string, connectionId: string, version: number = 1): Promise<void> {
    const qs = buildQuery({ legalEntityId });
    return httpClient<void>(`${BASE_URL}/connections/${connectionId}/disconnect${qs}`, {
      method: "POST",
      body: JSON.stringify({ version }),
      legalEntityId
    });
  },

  // Bank Statements
  listStatements(legalEntityId: string, accountId: string, page = 1, pageSize = 25): Promise<StatementPage> {
    const qs = buildQuery({ legalEntityId, page, pageSize });
    return httpClient<StatementPage>(`${BASE_URL}/treasury-accounts/${accountId}/statements${qs}`, { legalEntityId });
  },

  getStatement(legalEntityId: string, statementId: string): Promise<StatementDto> {
    const qs = buildQuery({ legalEntityId });
    return httpClient<StatementDto>(`${BASE_URL}/statements/${statementId}${qs}`, { legalEntityId });
  },

  listStatementLines(legalEntityId: string, statementId: string, page = 1, pageSize = 25): Promise<StatementLinePage> {
    const qs = buildQuery({ legalEntityId, page, pageSize });
    return httpClient<StatementLinePage>(`${BASE_URL}/statements/${statementId}/lines${qs}`, { legalEntityId });
  },

  importStatement(legalEntityId: string, accountId: string, request: ImportStatementRequest, idempotencyKey?: string): Promise<StatementDto> {
    const key = idempotencyKey || generateKey();
    const qs = buildQuery({ legalEntityId });
    return httpClient<StatementDto>(`${BASE_URL}/treasury-accounts/${accountId}/statements/import${qs}`, {
      method: "POST",
      body: JSON.stringify(request),
      legalEntityId,
      idempotencyKey: key,
      headers: { "Idempotency-Key": key }
    });
  },

  validateStatement(legalEntityId: string, statementId: string, request?: ValidateStatementRequest): Promise<StatementDto> {
    const qs = buildQuery({ legalEntityId });
    return httpClient<StatementDto>(`${BASE_URL}/statements/${statementId}/validate${qs}`, {
      method: "POST",
      body: JSON.stringify(request ?? { requireBalanceContinuity: true }),
      legalEntityId
    });
  },

  classifyStatementLine(legalEntityId: string, lineId: string, request: ClassifyStatementLineRequest): Promise<StatementLineDto> {
    const qs = buildQuery({ legalEntityId });
    return httpClient<StatementLineDto>(`${BASE_URL}/statement-lines/${lineId}/classify${qs}`, {
      method: "POST",
      body: JSON.stringify(request),
      legalEntityId
    });
  },

  createPaymentProposal(legalEntityId: string, lineId: string, request?: CreatePaymentProposalRequest): Promise<BankOriginatedCandidateDto> {
    const qs = buildQuery({ legalEntityId });
    return httpClient<BankOriginatedCandidateDto>(`${BASE_URL}/statement-lines/${lineId}/create-payment-proposal${qs}`, {
      method: "POST",
      body: JSON.stringify(request ?? {}),
      legalEntityId
    });
  },

  approveBankOriginatedCandidate(legalEntityId: string, candidateId: string, version: number): Promise<BankOriginatedCandidateDto> {
    const qs = buildQuery({ legalEntityId });
    return httpClient<BankOriginatedCandidateDto>(`${BASE_URL}/bank-originated-candidates/${candidateId}/approve${qs}`, {
      method: "POST",
      body: JSON.stringify({ decision: "Approved", version }),
      legalEntityId
    });
  },

  // Reconciliation
  listReconciliations(legalEntityId: string, accountId?: string, page = 1, pageSize = 25): Promise<ReconciliationPage> {
    if (accountId) {
      const qs = buildQuery({ legalEntityId, page, pageSize });
      return httpClient<ReconciliationPage>(`${BASE_URL}/treasury-accounts/${accountId}/reconciliations${qs}`, { legalEntityId });
    }
    const qs = buildQuery({ legalEntityId, page, pageSize });
    return httpClient<ReconciliationPage>(`${BASE_URL}/reconciliations${qs}`, { legalEntityId });
  },

  getReconciliation(legalEntityId: string, reconciliationId: string): Promise<ReconciliationDto> {
    const qs = buildQuery({ legalEntityId });
    return httpClient<ReconciliationDto>(`${BASE_URL}/reconciliations/${reconciliationId}${qs}`, { legalEntityId });
  },

  createReconciliation(legalEntityId: string, request: CreateReconciliationRequest): Promise<ReconciliationDto> {
    return httpClient<ReconciliationDto>(`${BASE_URL}/reconciliations`, {
      method: "POST",
      body: JSON.stringify(request),
      legalEntityId
    });
  },

  addMatch(legalEntityId: string, reconciliationId: string, request: CreateMatchRequest): Promise<ReconciliationDto> {
    const qs = buildQuery({ legalEntityId });
    return httpClient<ReconciliationDto>(`${BASE_URL}/reconciliations/${reconciliationId}/match${qs}`, {
      method: "POST",
      body: JSON.stringify(request),
      legalEntityId
    });
  },

  unmatch(legalEntityId: string, reconciliationId: string, request: UnmatchRequest): Promise<ReconciliationDto> {
    const qs = buildQuery({ legalEntityId });
    return httpClient<ReconciliationDto>(`${BASE_URL}/reconciliations/${reconciliationId}/unmatch${qs}`, {
      method: "POST",
      body: JSON.stringify(request),
      legalEntityId
    });
  },

  getSuggestions(legalEntityId: string, reconciliationId: string): Promise<readonly ReconciliationSuggestionDto[]> {
    const qs = buildQuery({ legalEntityId });
    return httpClient<readonly ReconciliationSuggestionDto[]>(`${BASE_URL}/reconciliations/${reconciliationId}/suggestions${qs}`, { legalEntityId });
  },

  getClaims(legalEntityId: string, reconciliationId: string): Promise<Record<string, number>> {
    const qs = buildQuery({ legalEntityId });
    return httpClient<Record<string, number>>(`${BASE_URL}/reconciliations/${reconciliationId}/claims${qs}`, { legalEntityId });
  },

  runRules(legalEntityId: string, reconciliationId: string): Promise<readonly ReconciliationSuggestionDto[]> {
    const qs = buildQuery({ legalEntityId });
    return httpClient<readonly ReconciliationSuggestionDto[]>(`${BASE_URL}/reconciliations/${reconciliationId}/run-rules${qs}`, {
      method: "POST",
      body: JSON.stringify({}),
      legalEntityId
    });
  },

  finalizeReconciliation(legalEntityId: string, reconciliationId: string, version: number): Promise<ReconciliationDto> {
    const qs = buildQuery({ legalEntityId });
    return httpClient<ReconciliationDto>(`${BASE_URL}/reconciliations/${reconciliationId}/finalize${qs}`, {
      method: "POST",
      body: JSON.stringify({ targetState: "Finalized", version }),
      legalEntityId
    });
  },

  reopenReconciliation(legalEntityId: string, reconciliationId: string, version: number): Promise<ReconciliationDto> {
    const qs = buildQuery({ legalEntityId });
    return httpClient<ReconciliationDto>(`${BASE_URL}/reconciliations/${reconciliationId}/reopen${qs}`, {
      method: "POST",
      body: JSON.stringify({ targetState: "Reopened", version }),
      legalEntityId
    });
  },

  // Opening Reconciliations
  listOpeningReconciliations(legalEntityId: string, treasuryAccountId?: string, page = 1, pageSize = 25): Promise<OpeningReconciliationPage> {
    const qs = buildQuery({ legalEntityId, treasuryAccountId, page, pageSize });
    return httpClient<OpeningReconciliationPage>(`${BASE_URL}/opening-reconciliations${qs}`, { legalEntityId });
  },

  createOpeningReconciliation(legalEntityId: string, request: CreateOpeningReconciliationRequest): Promise<OpeningReconciliationDto> {
    return httpClient<OpeningReconciliationDto>(`${BASE_URL}/opening-reconciliations`, {
      method: "POST",
      body: JSON.stringify(request),
      legalEntityId
    });
  },

  finalizeOpeningReconciliation(legalEntityId: string, id: string, version: number): Promise<OpeningReconciliationDto> {
    const qs = buildQuery({ legalEntityId });
    return httpClient<OpeningReconciliationDto>(`${BASE_URL}/opening-reconciliations/${id}/finalize${qs}`, {
      method: "POST",
      body: JSON.stringify({ version }),
      legalEntityId
    });
  },

  // Cash Management & Counts
  listCashCounts(legalEntityId: string, accountId: string, page = 1, pageSize = 25): Promise<CashCountPage> {
    const qs = buildQuery({ legalEntityId, page, pageSize });
    return httpClient<CashCountPage>(`${BASE_URL}/cash-accounts/${accountId}/counts${qs}`, { legalEntityId });
  },

  getCashCount(legalEntityId: string, cashCountId: string): Promise<CashCountDto> {
    const qs = buildQuery({ legalEntityId });
    return httpClient<CashCountDto>(`${BASE_URL}/cash-counts/${cashCountId}${qs}`, { legalEntityId });
  },

  createCashCount(legalEntityId: string, accountId: string, request: CreateCashCountRequest): Promise<CashCountDto> {
    const qs = buildQuery({ legalEntityId });
    return httpClient<CashCountDto>(`${BASE_URL}/cash-accounts/${accountId}/counts${qs}`, {
      method: "POST",
      body: JSON.stringify(request),
      legalEntityId
    });
  },

  submitCashCount(legalEntityId: string, cashCountId: string, version: number): Promise<CashCountDto> {
    const qs = buildQuery({ legalEntityId });
    return httpClient<CashCountDto>(`${BASE_URL}/cash-counts/${cashCountId}/submit${qs}`, {
      method: "POST",
      body: JSON.stringify({ targetState: "Submitted", version }),
      legalEntityId
    });
  },

  approveCashCount(legalEntityId: string, cashCountId: string, version: number): Promise<CashCountDto> {
    const qs = buildQuery({ legalEntityId });
    return httpClient<CashCountDto>(`${BASE_URL}/cash-counts/${cashCountId}/approve${qs}`, {
      method: "POST",
      body: JSON.stringify({ targetState: "Approved", version }),
      legalEntityId
    });
  },

  finalizeCashCount(legalEntityId: string, cashCountId: string, version: number): Promise<CashCountDto> {
    const qs = buildQuery({ legalEntityId });
    return httpClient<CashCountDto>(`${BASE_URL}/cash-counts/${cashCountId}/finalize${qs}`, {
      method: "POST",
      body: JSON.stringify({ targetState: "Finalized", version }),
      legalEntityId
    });
  },

  resolveCashDifference(legalEntityId: string, cashCountId: string, request: ResolveCashDifferenceRequest): Promise<CashDifferenceResolutionDto> {
    const qs = buildQuery({ legalEntityId });
    return httpClient<CashDifferenceResolutionDto>(`${BASE_URL}/cash-counts/${cashCountId}/difference-resolutions${qs}`, {
      method: "POST",
      body: JSON.stringify(request),
      legalEntityId
    });
  },

  transferCustody(legalEntityId: string, accountId: string, request: TransferCustodyRequest): Promise<CustodyTransferDto> {
    const qs = buildQuery({ legalEntityId });
    return httpClient<CustodyTransferDto>(`${BASE_URL}/cash-accounts/${accountId}/custody-transfers${qs}`, {
      method: "POST",
      body: JSON.stringify(request),
      legalEntityId
    });
  },

  // Cash Position
  getCashPosition(legalEntityId: string): Promise<readonly CashPositionDto[]> {
    const qs = buildQuery({ legalEntityId });
    return httpClient<readonly CashPositionDto[]>(`${BASE_URL}/cash-position${qs}`, { legalEntityId });
  },

  // Cash Forecast
  getForecast(legalEntityId: string, asOfDate?: string, horizonDays = 30): Promise<ForecastPage> {
    const qs = buildQuery({ legalEntityId, asOfDate, horizonDays });
    return httpClient<ForecastPage>(`${BASE_URL}/cash-forecast${qs}`, { legalEntityId });
  },

  createForecastVersion(legalEntityId: string, request: CreateForecastVersionRequest): Promise<ForecastVersionDto> {
    return httpClient<ForecastVersionDto>(`${BASE_URL}/cash-forecast/versions`, {
      method: "POST",
      body: JSON.stringify(request),
      legalEntityId
    });
  },

  createForecastItem(legalEntityId: string, request: CreateForecastItemRequest): Promise<ForecastItemDto> {
    return httpClient<ForecastItemDto>(`${BASE_URL}/cash-forecast/manual-items`, {
      method: "POST",
      body: JSON.stringify(request),
      legalEntityId
    });
  },

  // Internal Transfers
  createTransfer(legalEntityId: string, request: CreateTransferRequest, idempotencyKey?: string): Promise<TransferDto> {
    const key = idempotencyKey || generateKey();
    return httpClient<TransferDto>(`${BASE_URL}/transfers`, {
      method: "POST",
      body: JSON.stringify(request),
      legalEntityId,
      idempotencyKey: key,
      headers: { "Idempotency-Key": key }
    });
  },

  listTransferReconciliations(
    legalEntityId: string,
    filters?: { accountId?: string; state?: string; page?: number; pageSize?: number }
  ): Promise<TransferReconciliationPage> {
    const qs = buildQuery({ legalEntityId, ...filters });
    return httpClient<TransferReconciliationPage>(`${BASE_URL}/transfers/reconciliation${qs}`, { legalEntityId });
  },

  reconcileTransfer(legalEntityId: string, transferId: string, request: ReconcileTransferRequest): Promise<TransferReconciliationDto> {
    const qs = buildQuery({ legalEntityId });
    return httpClient<TransferReconciliationDto>(`${BASE_URL}/transfers/${transferId}/reconcile${qs}`, {
      method: "POST",
      body: JSON.stringify(request),
      legalEntityId
    });
  },

  // Settlements
  listPosCashSettlements(legalEntityId: string, status?: string, page = 1, pageSize = 25): Promise<PosCashSettlementPage> {
    const qs = buildQuery({ legalEntityId, status, page, pageSize });
    return httpClient<PosCashSettlementPage>(`${BASE_URL}/pos-cash-settlements${qs}`, { legalEntityId });
  },

  confirmPosCashSettlement(legalEntityId: string, settlementId: string, request: ConfirmPosCashSettlementRequest): Promise<PosCashSettlementDto> {
    const qs = buildQuery({ legalEntityId });
    return httpClient<PosCashSettlementDto>(`${BASE_URL}/pos-cash-settlements/${settlementId}/confirm${qs}`, {
      method: "POST",
      body: JSON.stringify(request),
      legalEntityId
    });
  },

  listProviderSettlements(legalEntityId: string, status?: string, page = 1, pageSize = 25): Promise<ProviderSettlementPage> {
    const qs = buildQuery({ legalEntityId, status, page, pageSize });
    return httpClient<ProviderSettlementPage>(`${BASE_URL}/provider-settlements${qs}`, { legalEntityId });
  },

  matchProviderSettlement(legalEntityId: string, settlementId: string, request: MatchProviderSettlementRequest): Promise<ProviderSettlementDto> {
    const qs = buildQuery({ legalEntityId });
    return httpClient<ProviderSettlementDto>(`${BASE_URL}/provider-settlements/${settlementId}/match${qs}`, {
      method: "POST",
      body: JSON.stringify(request),
      legalEntityId
    });
  },

  // Reports
  listBankActivity(
    legalEntityId: string,
    filters?: { treasuryAccountId?: string; classification?: string; direction?: string; fromDate?: string; toDate?: string; page?: number; pageSize?: number }
  ): Promise<BankActivityPage> {
    const qs = buildQuery({ legalEntityId, ...filters });
    return httpClient<BankActivityPage>(`${BASE_URL}/reports/bank-activity${qs}`, { legalEntityId });
  },

  getCashToLedgerReport(legalEntityId: string, asOfDate?: string): Promise<CashToLedgerReportDto> {
    const qs = buildQuery({ legalEntityId, asOfDate });
    return httpClient<CashToLedgerReportDto>(`${BASE_URL}/reports/cash-to-ledger${qs}`, { legalEntityId });
  },

  listChequesInTransit(legalEntityId: string, page = 1, pageSize = 50): Promise<ChequeInTransitPage> {
    const qs = buildQuery({ legalEntityId, page, pageSize });
    return httpClient<ChequeInTransitPage>(`${BASE_URL}/cheques/in-transit${qs}`, { legalEntityId });
  },

  // Settings
  listTransactionCodeMappings(legalEntityId: string): Promise<readonly TransactionCodeMappingDto[]> {
    const qs = buildQuery({ legalEntityId });
    return httpClient<readonly TransactionCodeMappingDto[]>(`${BASE_URL}/transaction-code-mappings${qs}`, { legalEntityId });
  },

  createTransactionCodeMapping(legalEntityId: string, request: CreateTransactionCodeMappingRequest): Promise<TransactionCodeMappingDto> {
    const qs = buildQuery({ legalEntityId });
    return httpClient<TransactionCodeMappingDto>(`${BASE_URL}/transaction-code-mappings${qs}`, {
      method: "POST",
      body: JSON.stringify(request),
      legalEntityId
    });
  },

  createReconciliationRuleSet(legalEntityId: string, request: CreateReconciliationRuleSetRequest): Promise<ReconciliationRuleSetDto> {
    const qs = buildQuery({ legalEntityId });
    return httpClient<ReconciliationRuleSetDto>(`${BASE_URL}/reconciliation-rule-sets${qs}`, {
      method: "POST",
      body: JSON.stringify(request),
      legalEntityId
    });
  }
};

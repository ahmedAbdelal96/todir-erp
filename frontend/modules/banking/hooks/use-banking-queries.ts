import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { bankingApi } from "../services/banking-api.ts";
import type {
  TreasuryAccountType,
  TreasuryAccountStatus,
  CreateTreasuryAccountRequest,
  UpdateTreasuryAccountRequest,
  StatusRequest,
  CreateAccountChangeRequest,
  ImportStatementRequest,
  ValidateStatementRequest,
  ClassifyStatementLineRequest,
  CreatePaymentProposalRequest,
  CreateReconciliationRequest,
  CreateMatchRequest,
  UnmatchRequest,
  CreateOpeningReconciliationRequest,
  CreateCashCountRequest,
  ResolveCashDifferenceRequest,
  TransferCustodyRequest,
  CreateForecastVersionRequest,
  CreateForecastItemRequest,
  CreateConnectionRequest,
  ConfirmPosCashSettlementRequest,
  MatchProviderSettlementRequest,
  CreateTransferRequest,
  ReconcileTransferRequest,
  CreateTransactionCodeMappingRequest,
  CreateReconciliationRuleSetRequest
} from "../types.ts";

export const bankingKeys = {
  all: ["banking"] as const,
  accounts: (legalEntityId: string) => [...bankingKeys.all, "accounts", legalEntityId] as const,
  accountsList: (legalEntityId: string, filters?: unknown) => [...bankingKeys.accounts(legalEntityId), "list", filters] as const,
  accountDetail: (legalEntityId: string, id: string) => [...bankingKeys.accounts(legalEntityId), "detail", id] as const,
  connections: (legalEntityId: string, accountId: string) => [...bankingKeys.accounts(legalEntityId), "connections", accountId] as const,
  statements: (legalEntityId: string, accountId: string) => [...bankingKeys.all, "statements", legalEntityId, accountId] as const,
  statementDetail: (legalEntityId: string, id: string) => [...bankingKeys.all, "statements", legalEntityId, "detail", id] as const,
  statementLines: (legalEntityId: string, statementId: string, page?: number) => [...bankingKeys.all, "statement-lines", legalEntityId, statementId, page] as const,
  reconciliations: (legalEntityId: string) => [...bankingKeys.all, "reconciliations", legalEntityId] as const,
  reconciliationDetail: (legalEntityId: string, id: string) => [...bankingKeys.reconciliations(legalEntityId), "detail", id] as const,
  reconciliationSuggestions: (legalEntityId: string, id: string) => [...bankingKeys.reconciliations(legalEntityId), "suggestions", id] as const,
  openingReconciliations: (legalEntityId: string) => [...bankingKeys.all, "opening-reconciliations", legalEntityId] as const,
  cashCounts: (legalEntityId: string, accountId: string) => [...bankingKeys.all, "cash-counts", legalEntityId, accountId] as const,
  cashCountDetail: (legalEntityId: string, id: string) => [...bankingKeys.all, "cash-counts", legalEntityId, "detail", id] as const,
  cashPosition: (legalEntityId: string) => [...bankingKeys.all, "cash-position", legalEntityId] as const,
  cashForecast: (legalEntityId: string, asOfDate?: string, horizonDays?: number) => [...bankingKeys.all, "cash-forecast", legalEntityId, asOfDate, horizonDays] as const,
  transfers: (legalEntityId: string) => [...bankingKeys.all, "transfers", legalEntityId] as const,
  posSettlements: (legalEntityId: string) => [...bankingKeys.all, "pos-settlements", legalEntityId] as const,
  providerSettlements: (legalEntityId: string) => [...bankingKeys.all, "provider-settlements", legalEntityId] as const,
  bankActivity: (legalEntityId: string, filters?: unknown) => [...bankingKeys.all, "bank-activity", legalEntityId, filters] as const,
  cashToLedger: (legalEntityId: string, asOfDate?: string) => [...bankingKeys.all, "cash-to-ledger", legalEntityId, asOfDate] as const,
  chequesInTransit: (legalEntityId: string) => [...bankingKeys.all, "cheques-in-transit", legalEntityId] as const,
  transactionCodeMappings: (legalEntityId: string) => [...bankingKeys.all, "transaction-code-mappings", legalEntityId] as const
};

// ==========================================
// Accounts Hooks
// ==========================================

export function useTreasuryAccounts(
  legalEntityId: string,
  filters?: { search?: string; type?: TreasuryAccountType; status?: TreasuryAccountStatus; page?: number; pageSize?: number }
) {
  return useQuery({
    queryKey: bankingKeys.accountsList(legalEntityId, filters),
    queryFn: () => bankingApi.listAccounts(legalEntityId, filters),
    enabled: Boolean(legalEntityId)
  });
}

export function useTreasuryAccount(legalEntityId: string, accountId: string) {
  return useQuery({
    queryKey: bankingKeys.accountDetail(legalEntityId, accountId),
    queryFn: () => bankingApi.getAccount(legalEntityId, accountId),
    enabled: Boolean(legalEntityId && accountId)
  });
}

export function useAccountConnections(legalEntityId: string, accountId: string) {
  return useQuery({
    queryKey: bankingKeys.connections(legalEntityId, accountId),
    queryFn: () => bankingApi.listConnections(legalEntityId, accountId),
    enabled: Boolean(legalEntityId && accountId)
  });
}

export function useCreateTreasuryAccount(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateTreasuryAccountRequest) => bankingApi.createAccount(legalEntityId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: bankingKeys.accounts(legalEntityId) });
      queryClient.invalidateQueries({ queryKey: bankingKeys.cashPosition(legalEntityId) });
    }
  });
}

export function useUpdateTreasuryAccount(legalEntityId: string, accountId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: UpdateTreasuryAccountRequest) => bankingApi.updateAccount(legalEntityId, accountId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: bankingKeys.accounts(legalEntityId) });
    }
  });
}

export function useUpdateTreasuryAccountStatus(legalEntityId: string, accountId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: StatusRequest) => bankingApi.updateAccountStatus(legalEntityId, accountId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: bankingKeys.accounts(legalEntityId) });
      queryClient.invalidateQueries({ queryKey: bankingKeys.cashPosition(legalEntityId) });
    }
  });
}

export function useActivateTreasuryAccount(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ accountId, version }: { accountId: string; version: number }) =>
      bankingApi.activateAccount(legalEntityId, accountId, version),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: bankingKeys.accounts(legalEntityId) });
      queryClient.invalidateQueries({ queryKey: bankingKeys.cashPosition(legalEntityId) });
    }
  });
}

export function useSuspendTreasuryAccount(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ accountId, version }: { accountId: string; version: number }) =>
      bankingApi.suspendAccount(legalEntityId, accountId, version),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: bankingKeys.accounts(legalEntityId) });
      queryClient.invalidateQueries({ queryKey: bankingKeys.cashPosition(legalEntityId) });
    }
  });
}

export function useCloseTreasuryAccount(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ accountId, version }: { accountId: string; version: number }) =>
      bankingApi.closeAccount(legalEntityId, accountId, version),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: bankingKeys.accounts(legalEntityId) });
      queryClient.invalidateQueries({ queryKey: bankingKeys.cashPosition(legalEntityId) });
    }
  });
}

export function useReopenTreasuryAccount(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ accountId, version }: { accountId: string; version: number }) =>
      bankingApi.reopenAccount(legalEntityId, accountId, version),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: bankingKeys.accounts(legalEntityId) });
      queryClient.invalidateQueries({ queryKey: bankingKeys.cashPosition(legalEntityId) });
    }
  });
}

export function useCreateAccountChangeRequest(legalEntityId: string, accountId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateAccountChangeRequest) => bankingApi.createChangeRequest(legalEntityId, accountId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: bankingKeys.accountDetail(legalEntityId, accountId) });
    }
  });
}

export function useDecideAccountChangeRequest(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ requestId, decision, version }: { requestId: string; decision: "approve" | "reject"; version: number }) =>
      bankingApi.decideChangeRequest(legalEntityId, requestId, decision, version),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: bankingKeys.accounts(legalEntityId) });
    }
  });
}

// ==========================================
// Statements Hooks
// ==========================================

export function useBankStatements(legalEntityId: string, accountId: string, page = 1, pageSize = 25) {
  return useQuery({
    queryKey: [...bankingKeys.statements(legalEntityId, accountId), page, pageSize],
    queryFn: () => bankingApi.listStatements(legalEntityId, accountId, page, pageSize),
    enabled: Boolean(legalEntityId && accountId)
  });
}

export function useBankStatement(legalEntityId: string, statementId: string) {
  return useQuery({
    queryKey: bankingKeys.statementDetail(legalEntityId, statementId),
    queryFn: () => bankingApi.getStatement(legalEntityId, statementId),
    enabled: Boolean(legalEntityId && statementId)
  });
}

export function useStatementLines(legalEntityId: string, statementId: string, page = 1, pageSize = 25) {
  return useQuery({
    queryKey: bankingKeys.statementLines(legalEntityId, statementId, page),
    queryFn: () => bankingApi.listStatementLines(legalEntityId, statementId, page, pageSize),
    enabled: Boolean(legalEntityId && statementId)
  });
}

export function useImportStatement(legalEntityId: string, accountId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ request, idempotencyKey }: { request: ImportStatementRequest; idempotencyKey?: string }) =>
      bankingApi.importStatement(legalEntityId, accountId, request, idempotencyKey),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: bankingKeys.statements(legalEntityId, accountId) });
      queryClient.invalidateQueries({ queryKey: bankingKeys.cashPosition(legalEntityId) });
    }
  });
}

export function useValidateStatement(legalEntityId: string, accountId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ statementId, request }: { statementId: string; request?: ValidateStatementRequest }) =>
      bankingApi.validateStatement(legalEntityId, statementId, request),
    onSuccess: (_, vars) => {
      queryClient.invalidateQueries({ queryKey: bankingKeys.statementDetail(legalEntityId, vars.statementId) });
      queryClient.invalidateQueries({ queryKey: bankingKeys.statements(legalEntityId, accountId) });
    }
  });
}

export function useClassifyStatementLine(legalEntityId: string, statementId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ lineId, request }: { lineId: string; request: ClassifyStatementLineRequest }) =>
      bankingApi.classifyStatementLine(legalEntityId, lineId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: bankingKeys.statementLines(legalEntityId, statementId) });
    }
  });
}

export function useCreatePaymentProposal(legalEntityId: string, statementId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ lineId, request }: { lineId: string; request?: CreatePaymentProposalRequest }) =>
      bankingApi.createPaymentProposal(legalEntityId, lineId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: bankingKeys.statementLines(legalEntityId, statementId) });
    }
  });
}

// ==========================================
// Reconciliation Hooks
// ==========================================

export function useReconciliations(legalEntityId: string, accountId?: string, page = 1, pageSize = 25) {
  return useQuery({
    queryKey: [...bankingKeys.reconciliations(legalEntityId), accountId, page, pageSize],
    queryFn: () => bankingApi.listReconciliations(legalEntityId, accountId, page, pageSize),
    enabled: Boolean(legalEntityId)
  });
}

export function useReconciliation(legalEntityId: string, reconciliationId: string) {
  return useQuery({
    queryKey: bankingKeys.reconciliationDetail(legalEntityId, reconciliationId),
    queryFn: () => bankingApi.getReconciliation(legalEntityId, reconciliationId),
    enabled: Boolean(legalEntityId && reconciliationId)
  });
}

export function useReconciliationSuggestions(legalEntityId: string, reconciliationId: string) {
  return useQuery({
    queryKey: bankingKeys.reconciliationSuggestions(legalEntityId, reconciliationId),
    queryFn: () => bankingApi.getSuggestions(legalEntityId, reconciliationId),
    enabled: Boolean(legalEntityId && reconciliationId)
  });
}

export function useCreateReconciliation(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateReconciliationRequest) => bankingApi.createReconciliation(legalEntityId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: bankingKeys.reconciliations(legalEntityId) });
    }
  });
}

export function useAddMatch(legalEntityId: string, reconciliationId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateMatchRequest) => bankingApi.addMatch(legalEntityId, reconciliationId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: bankingKeys.reconciliationDetail(legalEntityId, reconciliationId) });
      queryClient.invalidateQueries({ queryKey: bankingKeys.reconciliations(legalEntityId) });
    }
  });
}

export function useUnmatch(legalEntityId: string, reconciliationId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: UnmatchRequest) => bankingApi.unmatch(legalEntityId, reconciliationId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: bankingKeys.reconciliationDetail(legalEntityId, reconciliationId) });
      queryClient.invalidateQueries({ queryKey: bankingKeys.reconciliations(legalEntityId) });
    }
  });
}

export function useFinalizeReconciliation(legalEntityId: string, reconciliationId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (version: number) => bankingApi.finalizeReconciliation(legalEntityId, reconciliationId, version),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: bankingKeys.reconciliationDetail(legalEntityId, reconciliationId) });
      queryClient.invalidateQueries({ queryKey: bankingKeys.reconciliations(legalEntityId) });
      queryClient.invalidateQueries({ queryKey: bankingKeys.cashPosition(legalEntityId) });
    }
  });
}

export function useReopenReconciliation(legalEntityId: string, reconciliationId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (version: number) => bankingApi.reopenReconciliation(legalEntityId, reconciliationId, version),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: bankingKeys.reconciliationDetail(legalEntityId, reconciliationId) });
      queryClient.invalidateQueries({ queryKey: bankingKeys.reconciliations(legalEntityId) });
    }
  });
}

export function useOpeningReconciliations(legalEntityId: string, treasuryAccountId?: string, page = 1, pageSize = 25) {
  return useQuery({
    queryKey: [...bankingKeys.openingReconciliations(legalEntityId), treasuryAccountId, page, pageSize],
    queryFn: () => bankingApi.listOpeningReconciliations(legalEntityId, treasuryAccountId, page, pageSize),
    enabled: Boolean(legalEntityId)
  });
}

export function useCreateOpeningReconciliation(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateOpeningReconciliationRequest) => bankingApi.createOpeningReconciliation(legalEntityId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: bankingKeys.openingReconciliations(legalEntityId) });
    }
  });
}

export function useFinalizeOpeningReconciliation(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, version }: { id: string; version: number }) =>
      bankingApi.finalizeOpeningReconciliation(legalEntityId, id, version),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: bankingKeys.openingReconciliations(legalEntityId) });
    }
  });
}

// ==========================================
// Cash Management Hooks
// ==========================================

export function useCashCounts(legalEntityId: string, accountId: string, page = 1, pageSize = 25) {
  return useQuery({
    queryKey: [...bankingKeys.cashCounts(legalEntityId, accountId), page, pageSize],
    queryFn: () => bankingApi.listCashCounts(legalEntityId, accountId, page, pageSize),
    enabled: Boolean(legalEntityId && accountId)
  });
}

export function useCashCount(legalEntityId: string, cashCountId: string) {
  return useQuery({
    queryKey: bankingKeys.cashCountDetail(legalEntityId, cashCountId),
    queryFn: () => bankingApi.getCashCount(legalEntityId, cashCountId),
    enabled: Boolean(legalEntityId && cashCountId)
  });
}

export function useCreateCashCount(legalEntityId: string, accountId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateCashCountRequest) => bankingApi.createCashCount(legalEntityId, accountId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: bankingKeys.cashCounts(legalEntityId, accountId) });
    }
  });
}

export function useSubmitCashCount(legalEntityId: string, accountId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ cashCountId, version }: { cashCountId: string; version: number }) =>
      bankingApi.submitCashCount(legalEntityId, cashCountId, version),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: bankingKeys.cashCounts(legalEntityId, accountId) });
    }
  });
}

export function useApproveCashCount(legalEntityId: string, accountId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ cashCountId, version }: { cashCountId: string; version: number }) =>
      bankingApi.approveCashCount(legalEntityId, cashCountId, version),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: bankingKeys.cashCounts(legalEntityId, accountId) });
    }
  });
}

export function useFinalizeCashCount(legalEntityId: string, accountId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ cashCountId, version }: { cashCountId: string; version: number }) =>
      bankingApi.finalizeCashCount(legalEntityId, cashCountId, version),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: bankingKeys.cashCounts(legalEntityId, accountId) });
      queryClient.invalidateQueries({ queryKey: bankingKeys.cashPosition(legalEntityId) });
    }
  });
}

export function useResolveCashDifference(legalEntityId: string, accountId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ cashCountId, request }: { cashCountId: string; request: ResolveCashDifferenceRequest }) =>
      bankingApi.resolveCashDifference(legalEntityId, cashCountId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: bankingKeys.cashCounts(legalEntityId, accountId) });
    }
  });
}

export function useTransferCustody(legalEntityId: string, accountId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: TransferCustodyRequest) => bankingApi.transferCustody(legalEntityId, accountId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: bankingKeys.accountDetail(legalEntityId, accountId) });
    }
  });
}

// ==========================================
// Cash Position, Forecast, Transfers & Reports
// ==========================================

export function useCashPosition(legalEntityId: string) {
  return useQuery({
    queryKey: bankingKeys.cashPosition(legalEntityId),
    queryFn: () => bankingApi.getCashPosition(legalEntityId),
    enabled: Boolean(legalEntityId)
  });
}

export function useCashForecast(legalEntityId: string, asOfDate?: string, horizonDays = 30) {
  return useQuery({
    queryKey: bankingKeys.cashForecast(legalEntityId, asOfDate, horizonDays),
    queryFn: () => bankingApi.getForecast(legalEntityId, asOfDate, horizonDays),
    enabled: Boolean(legalEntityId)
  });
}

export function useCreateForecastVersion(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateForecastVersionRequest) => bankingApi.createForecastVersion(legalEntityId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: bankingKeys.all });
    }
  });
}

export function useCreateForecastItem(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateForecastItemRequest) => bankingApi.createForecastItem(legalEntityId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: bankingKeys.all });
    }
  });
}

export function useCreateTransfer(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ request, idempotencyKey }: { request: CreateTransferRequest; idempotencyKey?: string }) =>
      bankingApi.createTransfer(legalEntityId, request, idempotencyKey),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: bankingKeys.transfers(legalEntityId) });
      queryClient.invalidateQueries({ queryKey: bankingKeys.cashPosition(legalEntityId) });
    }
  });
}

export function useTransferReconciliations(
  legalEntityId: string,
  filters?: { accountId?: string; state?: string; page?: number; pageSize?: number }
) {
  return useQuery({
    queryKey: [...bankingKeys.transfers(legalEntityId), filters],
    queryFn: () => bankingApi.listTransferReconciliations(legalEntityId, filters),
    enabled: Boolean(legalEntityId)
  });
}

export function useReconcileTransfer(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ transferId, request }: { transferId: string; request: ReconcileTransferRequest }) =>
      bankingApi.reconcileTransfer(legalEntityId, transferId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: bankingKeys.transfers(legalEntityId) });
    }
  });
}

export function usePosCashSettlements(legalEntityId: string, status?: string, page = 1, pageSize = 25) {
  return useQuery({
    queryKey: [...bankingKeys.posSettlements(legalEntityId), status, page, pageSize],
    queryFn: () => bankingApi.listPosCashSettlements(legalEntityId, status, page, pageSize),
    enabled: Boolean(legalEntityId)
  });
}

export function useConfirmPosCashSettlement(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ settlementId, request }: { settlementId: string; request: ConfirmPosCashSettlementRequest }) =>
      bankingApi.confirmPosCashSettlement(legalEntityId, settlementId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: bankingKeys.posSettlements(legalEntityId) });
      queryClient.invalidateQueries({ queryKey: bankingKeys.cashPosition(legalEntityId) });
    }
  });
}

export function useProviderSettlements(legalEntityId: string, status?: string, page = 1, pageSize = 25) {
  return useQuery({
    queryKey: [...bankingKeys.providerSettlements(legalEntityId), status, page, pageSize],
    queryFn: () => bankingApi.listProviderSettlements(legalEntityId, status, page, pageSize),
    enabled: Boolean(legalEntityId)
  });
}

export function useMatchProviderSettlement(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ settlementId, request }: { settlementId: string; request: MatchProviderSettlementRequest }) =>
      bankingApi.matchProviderSettlement(legalEntityId, settlementId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: bankingKeys.providerSettlements(legalEntityId) });
      queryClient.invalidateQueries({ queryKey: bankingKeys.cashPosition(legalEntityId) });
    }
  });
}

export function useBankActivity(
  legalEntityId: string,
  filters?: { treasuryAccountId?: string; classification?: string; direction?: string; fromDate?: string; toDate?: string; page?: number; pageSize?: number }
) {
  return useQuery({
    queryKey: bankingKeys.bankActivity(legalEntityId, filters),
    queryFn: () => bankingApi.listBankActivity(legalEntityId, filters),
    enabled: Boolean(legalEntityId)
  });
}

export function useCashToLedgerReport(legalEntityId: string, asOfDate?: string) {
  return useQuery({
    queryKey: bankingKeys.cashToLedger(legalEntityId, asOfDate),
    queryFn: () => bankingApi.getCashToLedgerReport(legalEntityId, asOfDate),
    enabled: Boolean(legalEntityId)
  });
}

export function useChequesInTransit(legalEntityId: string, page = 1, pageSize = 50) {
  return useQuery({
    queryKey: [...bankingKeys.chequesInTransit(legalEntityId), page, pageSize],
    queryFn: () => bankingApi.listChequesInTransit(legalEntityId, page, pageSize),
    enabled: Boolean(legalEntityId)
  });
}

export function useTransactionCodeMappings(legalEntityId: string) {
  return useQuery({
    queryKey: bankingKeys.transactionCodeMappings(legalEntityId),
    queryFn: () => bankingApi.listTransactionCodeMappings(legalEntityId),
    enabled: Boolean(legalEntityId)
  });
}

export function useCreateTransactionCodeMapping(legalEntityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateTransactionCodeMappingRequest) => bankingApi.createTransactionCodeMapping(legalEntityId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: bankingKeys.transactionCodeMappings(legalEntityId) });
    }
  });
}

export function useCreateReconciliationRuleSet(legalEntityId: string) {
  return useMutation({
    mutationFn: (request: CreateReconciliationRuleSetRequest) => bankingApi.createReconciliationRuleSet(legalEntityId, request)
  });
}

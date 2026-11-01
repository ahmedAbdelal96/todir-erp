import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useOutletContext } from "react-router-dom";
import type { WorkspaceOutletContext } from "../../../app/router/route-contract.ts";
import { useOptionalPermissions, useLegalEntityContext } from "../../../core/runtime/runtime-provider.tsx";
import { PermissionSet } from "../../../core/runtime/permissions.ts";
import { loadSessionContext } from "../../../core/api/auth-service.ts";
import { ledgerApi } from "../services/ledger-api.ts";
import type {
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
  LedgerMode,
  ManagedLedgerSetupRequest,
  PostingPreviewRequest,
  ReverseEntryRequest
} from "../types.ts";

export const ledgerQueryKeys = {
  all: ["ledger"] as const,
  readiness: (legalEntityId: string) => ["ledger", "readiness", legalEntityId] as const,
  mode: (legalEntityId: string) => ["ledger", "mode", legalEntityId] as const,
  accounts: (legalEntityId: string) => ["ledger", "accounts", legalEntityId] as const,
  accountSearch: (legalEntityId: string, query?: string, cursor?: string) =>
    ["ledger", "accountSearch", legalEntityId, query ?? "", cursor ?? ""] as const,
  accountWorkspace: (legalEntityId: string, accountId: string) =>
    ["ledger", "accountWorkspace", legalEntityId, accountId] as const,
  accountBalance: (legalEntityId: string, accountId: string, from?: string, to?: string) =>
    ["ledger", "accountBalance", legalEntityId, accountId, from ?? "", to ?? ""] as const,
  journals: (legalEntityId: string) => ["ledger", "journals", legalEntityId] as const,
  fiscalYears: (legalEntityId: string) => ["ledger", "fiscalYears", legalEntityId] as const,
  periods: (legalEntityId: string) => ["ledger", "periods", legalEntityId] as const,
  periodCloseReadiness: (legalEntityId: string, periodId: string) =>
    ["ledger", "periodCloseReadiness", legalEntityId, periodId] as const,
  locks: (legalEntityId: string) => ["ledger", "locks", legalEntityId] as const,
  entries: (legalEntityId: string, limit?: number) =>
    ["ledger", "entries", legalEntityId, limit ?? 50] as const,
  entryExplanation: (legalEntityId: string, entryId: string) =>
    ["ledger", "entryExplanation", legalEntityId, entryId] as const,
  trialBalance: (legalEntityId: string, from?: string, to?: string) =>
    ["ledger", "trialBalance", legalEntityId, from ?? "", to ?? ""] as const,
  generalLedger: (
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
  ) =>
    [
      "ledger",
      "generalLedger",
      legalEntityId,
      params?.from ?? "",
      params?.to ?? "",
      params?.accountId ?? "",
      params?.journalId ?? "",
      params?.sourceModule ?? "",
      params?.sourceReference ?? "",
      params?.cursor ?? ""
    ] as const,
  reconcileBalances: (legalEntityId: string, fiscalYearId?: string, periodId?: string) =>
    ["ledger", "reconcileBalances", legalEntityId, fiscalYearId ?? "", periodId ?? ""] as const
};

export function useLedgerPermissions() {
  const permissions = useOptionalPermissions();
  const sessionQuery = useQuery({
    queryKey: ["session", "context"],
    queryFn: loadSessionContext,
    staleTime: 60_000
  });

  const sessionPerms = (sessionQuery.data as { permissions?: readonly string[] } | undefined)?.permissions;
  const permSet = permissions ?? (sessionPerms ? new PermissionSet(sessionPerms) : undefined);

  return {
    canReadAccounts: permSet ? permSet.hasPermission("ledger.account.read") : true,
    canManageAccounts: permSet ? permSet.hasPermission("ledger.account.manage") : true,
    canReadJournals: permSet ? permSet.hasPermission("ledger.journal.read") : true,
    canManageJournals: permSet ? permSet.hasPermission("ledger.journal.manage") : true,
    canReadPeriods: permSet ? permSet.hasPermission("ledger.period.read") : true,
    canManagePeriods: permSet ? permSet.hasPermission("ledger.period.manage") : true,
    canSoftClosePeriod: permSet ? permSet.hasPermission("ledger.period.soft_close") : true,
    canClosePeriod: permSet ? permSet.hasPermission("ledger.period.close") : true,
    canReopenPeriod: permSet ? permSet.hasPermission("ledger.period.reopen") : true,
    canReadLocks: permSet ? permSet.hasPermission("ledger.lock.read") : true,
    canManageLocks: permSet ? permSet.hasPermission("ledger.lock.manage") : true,
    canReadEntries: permSet ? permSet.hasPermission("ledger.entry.read") : true,
    canReverseEntries: permSet ? permSet.hasPermission("ledger.entry.reverse") : true,
    canExplainEntries: permSet ? permSet.hasPermission("ledger.entry.explain") : true,
    canCreateGeneralJournal: permSet ? permSet.hasPermission("ledger.general_journal.create") : true,
    canEditGeneralJournal: permSet ? permSet.hasPermission("ledger.general_journal.edit") : true,
    canSubmitGeneralJournal: permSet ? permSet.hasPermission("ledger.general_journal.submit") : true,
    canApproveGeneralJournal: permSet ? permSet.hasPermission("ledger.general_journal.approve") : true,
    canPostGeneralJournal: permSet ? permSet.hasPermission("ledger.general_journal.post") : true,
    canReadDimensions: permSet ? permSet.hasPermission("ledger.dimension.read") : true,
    canManageDimensions: permSet ? permSet.hasPermission("ledger.dimension.manage") : true,
    canReadOpening: permSet ? permSet.hasPermission("ledger.opening.read") : true,
    canManageOpening: permSet ? permSet.hasPermission("ledger.opening.manage") : true,
    canPostOpening: permSet ? permSet.hasPermission("ledger.opening.post") : true,
    canReadReports: permSet ? permSet.hasPermission("ledger.reports.read") : true,
    canReadConfig: permSet ? permSet.hasPermission("ledger.configuration.read") : true,
    canManageConfig: permSet ? permSet.hasPermission("ledger.configuration.manage") : true
  };
}

export function useLedgerScope(props?: { legalEntityId?: string | undefined }) {
  let contextEntityId: string | undefined;
  let contextCountryCode: string | undefined;
  let contextBaseCurrency: string | undefined;

  try {
    const entity = useLegalEntityContext();
    contextEntityId = entity?.legalEntityId;
    contextCountryCode = entity?.countryCode;
    contextBaseCurrency = entity?.baseCurrencyCode;
  } catch {
    // Rendered outside RuntimeProvider
  }

  let outletEntityId: string | undefined;
  try {
    const outlet = useOutletContext<WorkspaceOutletContext | undefined>();
    outletEntityId = outlet?.legalEntityId;
  } catch {
    // Rendered outside Outlet context
  }

  const legalEntityId = props?.legalEntityId || outletEntityId || contextEntityId || "00000000-0000-0000-0000-000000000001";
  const countryCode = contextCountryCode || "EG";
  const baseCurrency = contextBaseCurrency || "EGP";

  const now = new Date();
  const today = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, "0")}-${String(now.getDate()).padStart(2, "0")}`;

  return { legalEntityId, countryCode, baseCurrency, today };
}

// Queries
export function useLedgerReadiness(legalEntityId: string) {
  return useQuery({
    queryKey: ledgerQueryKeys.readiness(legalEntityId),
    queryFn: () => ledgerApi.getReadiness(legalEntityId),
    enabled: Boolean(legalEntityId)
  });
}

export function useLedgerMode(legalEntityId: string) {
  return useQuery({
    queryKey: ledgerQueryKeys.mode(legalEntityId),
    queryFn: () => ledgerApi.getMode(legalEntityId),
    enabled: Boolean(legalEntityId)
  });
}

export function useAccounts(legalEntityId: string) {
  return useQuery({
    queryKey: ledgerQueryKeys.accounts(legalEntityId),
    queryFn: () => ledgerApi.listAccounts(legalEntityId),
    enabled: Boolean(legalEntityId)
  });
}

export function useAccountSearch(legalEntityId: string, query?: string, cursor?: string) {
  return useQuery({
    queryKey: ledgerQueryKeys.accountSearch(legalEntityId, query, cursor),
    queryFn: () => ledgerApi.searchAccounts(legalEntityId, query, cursor),
    enabled: Boolean(legalEntityId)
  });
}

export function useAccountWorkspace(legalEntityId: string, accountId: string) {
  return useQuery({
    queryKey: ledgerQueryKeys.accountWorkspace(legalEntityId, accountId),
    queryFn: () => ledgerApi.getAccountWorkspace(legalEntityId, accountId),
    enabled: Boolean(legalEntityId && accountId)
  });
}

export function useJournals(legalEntityId: string) {
  return useQuery({
    queryKey: ledgerQueryKeys.journals(legalEntityId),
    queryFn: () => ledgerApi.listJournals(legalEntityId),
    enabled: Boolean(legalEntityId)
  });
}

export function useFiscalYears(legalEntityId: string) {
  return useQuery({
    queryKey: ledgerQueryKeys.fiscalYears(legalEntityId),
    queryFn: () => ledgerApi.listFiscalYears(legalEntityId),
    enabled: Boolean(legalEntityId)
  });
}

export function usePeriods(legalEntityId: string) {
  return useQuery({
    queryKey: ledgerQueryKeys.periods(legalEntityId),
    queryFn: () => ledgerApi.listPeriods(legalEntityId),
    enabled: Boolean(legalEntityId)
  });
}

export function usePeriodCloseReadiness(legalEntityId: string, periodId: string) {
  return useQuery({
    queryKey: ledgerQueryKeys.periodCloseReadiness(legalEntityId, periodId),
    queryFn: () => ledgerApi.getPeriodCloseReadiness(legalEntityId, periodId),
    enabled: Boolean(legalEntityId && periodId)
  });
}

export function useLocks(legalEntityId: string) {
  return useQuery({
    queryKey: ledgerQueryKeys.locks(legalEntityId),
    queryFn: () => ledgerApi.listLocks(legalEntityId),
    enabled: Boolean(legalEntityId)
  });
}

export function useJournalEntries(legalEntityId: string, limit: number = 50) {
  return useQuery({
    queryKey: ledgerQueryKeys.entries(legalEntityId, limit),
    queryFn: () => ledgerApi.listEntries(legalEntityId, limit),
    enabled: Boolean(legalEntityId)
  });
}

export function useExplainEntry(legalEntityId: string, entryId: string) {
  return useQuery({
    queryKey: ledgerQueryKeys.entryExplanation(legalEntityId, entryId),
    queryFn: () => ledgerApi.explainEntry(legalEntityId, entryId),
    enabled: Boolean(legalEntityId && entryId)
  });
}

export function useTrialBalance(legalEntityId: string, from?: string, to?: string) {
  return useQuery({
    queryKey: ledgerQueryKeys.trialBalance(legalEntityId, from, to),
    queryFn: () =>
      ledgerApi.getTrialBalance(legalEntityId, {
        ...(from ? { from } : {}),
        ...(to ? { to } : {})
      }),
    enabled: Boolean(legalEntityId)
  });
}

export function useGeneralLedger(
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
) {
  return useQuery({
    queryKey: ledgerQueryKeys.generalLedger(legalEntityId, params),
    queryFn: () => ledgerApi.getGeneralLedger(legalEntityId, params),
    enabled: Boolean(legalEntityId)
  });
}

export function useReconcileBalances(legalEntityId: string, fiscalYearId?: string, periodId?: string) {
  return useQuery({
    queryKey: ledgerQueryKeys.reconcileBalances(legalEntityId, fiscalYearId, periodId),
    queryFn: () => ledgerApi.reconcileBalances(legalEntityId, fiscalYearId, periodId),
    enabled: Boolean(legalEntityId)
  });
}

// Mutations
export function useChangeLedgerMode() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ legalEntityId, mode, version }: { legalEntityId: string; mode: LedgerMode; version: number }) =>
      ledgerApi.changeMode(legalEntityId, mode, version),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ledgerQueryKeys.mode(variables.legalEntityId) });
      queryClient.invalidateQueries({ queryKey: ledgerQueryKeys.readiness(variables.legalEntityId) });
    }
  });
}

export function useManagedLedgerSetup() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ legalEntityId, request }: { legalEntityId: string; request?: ManagedLedgerSetupRequest }) =>
      ledgerApi.setupManaged(legalEntityId, request),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ledgerQueryKeys.all });
    }
  });
}

export function useCreateAccount() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ legalEntityId, request }: { legalEntityId: string; request: CreateAccountRequest }) =>
      ledgerApi.createAccount(legalEntityId, request),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ledgerQueryKeys.accounts(variables.legalEntityId) });
    }
  });
}

export function useArchiveAccount() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ legalEntityId, accountId }: { legalEntityId: string; accountId: string }) =>
      ledgerApi.archiveAccount(legalEntityId, accountId),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ledgerQueryKeys.accounts(variables.legalEntityId) });
    }
  });
}

export function useCreateJournal() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ legalEntityId, request }: { legalEntityId: string; request: CreateJournalRequest }) =>
      ledgerApi.createJournal(legalEntityId, request),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ledgerQueryKeys.journals(variables.legalEntityId) });
    }
  });
}

export function useCreateFiscalYear() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ legalEntityId, request }: { legalEntityId: string; request: CreateFiscalYearRequest }) =>
      ledgerApi.createFiscalYear(legalEntityId, request),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ledgerQueryKeys.fiscalYears(variables.legalEntityId) });
    }
  });
}

export function useCreatePeriod() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ legalEntityId, request }: { legalEntityId: string; request: CreatePeriodRequest }) =>
      ledgerApi.createPeriod(legalEntityId, request),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ledgerQueryKeys.periods(variables.legalEntityId) });
    }
  });
}

export function useSoftClosePeriod() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ legalEntityId, periodId }: { legalEntityId: string; periodId: string }) =>
      ledgerApi.softClosePeriod(legalEntityId, periodId),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ledgerQueryKeys.periods(variables.legalEntityId) });
      queryClient.invalidateQueries({ queryKey: ledgerQueryKeys.readiness(variables.legalEntityId) });
    }
  });
}

export function useClosePeriod() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ legalEntityId, periodId }: { legalEntityId: string; periodId: string }) =>
      ledgerApi.closePeriod(legalEntityId, periodId),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ledgerQueryKeys.periods(variables.legalEntityId) });
      queryClient.invalidateQueries({ queryKey: ledgerQueryKeys.readiness(variables.legalEntityId) });
    }
  });
}

export function useReopenPeriod() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ legalEntityId, periodId }: { legalEntityId: string; periodId: string }) =>
      ledgerApi.reopenPeriod(legalEntityId, periodId),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ledgerQueryKeys.periods(variables.legalEntityId) });
      queryClient.invalidateQueries({ queryKey: ledgerQueryKeys.readiness(variables.legalEntityId) });
    }
  });
}

export function useCreateLock() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ legalEntityId, request }: { legalEntityId: string; request: CreateFinancialLockRequest }) =>
      ledgerApi.createLock(legalEntityId, request),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ledgerQueryKeys.locks(variables.legalEntityId) });
    }
  });
}

export function useReverseEntry() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({
      legalEntityId,
      entryId,
      request
    }: {
      legalEntityId: string;
      entryId: string;
      request: ReverseEntryRequest;
    }) => ledgerApi.reverseEntry(legalEntityId, entryId, request),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ledgerQueryKeys.entries(variables.legalEntityId) });
      queryClient.invalidateQueries({ queryKey: ledgerQueryKeys.trialBalance(variables.legalEntityId) });
      queryClient.invalidateQueries({ queryKey: ledgerQueryKeys.generalLedger(variables.legalEntityId) });
    }
  });
}

export function useCreateGeneralJournalDraft() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ legalEntityId, request }: { legalEntityId: string; request: CreateGeneralJournalDraftRequest }) =>
      ledgerApi.createGeneralJournalDraft(legalEntityId, request)
  });
}

export function useUpdateGeneralJournalDraft() {
  return useMutation({
    mutationFn: ({
      legalEntityId,
      draftId,
      request
    }: {
      legalEntityId: string;
      draftId: string;
      request: CreateGeneralJournalDraftRequest;
    }) => ledgerApi.updateGeneralJournalDraft(legalEntityId, draftId, request)
  });
}

export function useSubmitGeneralJournal() {
  return useMutation({
    mutationFn: ({ legalEntityId, draftId }: { legalEntityId: string; draftId: string }) =>
      ledgerApi.submitGeneralJournal(legalEntityId, draftId)
  });
}

export function useApproveGeneralJournal() {
  return useMutation({
    mutationFn: ({ legalEntityId, draftId }: { legalEntityId: string; draftId: string }) =>
      ledgerApi.approveGeneralJournal(legalEntityId, draftId)
  });
}

export function usePostGeneralJournal() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ legalEntityId, draftId }: { legalEntityId: string; draftId: string }) =>
      ledgerApi.postGeneralJournal(legalEntityId, draftId),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ledgerQueryKeys.entries(variables.legalEntityId) });
      queryClient.invalidateQueries({ queryKey: ledgerQueryKeys.trialBalance(variables.legalEntityId) });
      queryClient.invalidateQueries({ queryKey: ledgerQueryKeys.generalLedger(variables.legalEntityId) });
    }
  });
}

export function usePostingPreview() {
  return useMutation({
    mutationFn: ({ legalEntityId, request }: { legalEntityId: string; request: PostingPreviewRequest }) =>
      ledgerApi.previewPosting(legalEntityId, request)
  });
}

export function useCreateOpeningBatch() {
  return useMutation({
    mutationFn: ({
      legalEntityId,
      request
    }: {
      legalEntityId: string;
      request: CreateOpeningBalanceBatchRequest;
    }) => ledgerApi.createOpeningBatch(legalEntityId, request)
  });
}

export function usePreviewOpeningBatch() {
  return useMutation({
    mutationFn: ({ legalEntityId, batchId }: { legalEntityId: string; batchId: string }) =>
      ledgerApi.previewOpeningBatch(legalEntityId, batchId)
  });
}

export function usePostOpeningBatch() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ legalEntityId, batchId }: { legalEntityId: string; batchId: string }) =>
      ledgerApi.postOpeningBatch(legalEntityId, batchId),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ledgerQueryKeys.entries(variables.legalEntityId) });
      queryClient.invalidateQueries({ queryKey: ledgerQueryKeys.trialBalance(variables.legalEntityId) });
      queryClient.invalidateQueries({ queryKey: ledgerQueryKeys.generalLedger(variables.legalEntityId) });
    }
  });
}

export function useRebuildBalances() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({
      legalEntityId,
      fiscalYearId,
      periodId
    }: {
      legalEntityId: string;
      fiscalYearId?: string;
      periodId?: string;
    }) => ledgerApi.rebuildBalances(legalEntityId, fiscalYearId, periodId),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ledgerQueryKeys.trialBalance(variables.legalEntityId) });
      queryClient.invalidateQueries({ queryKey: ledgerQueryKeys.generalLedger(variables.legalEntityId) });
      queryClient.invalidateQueries({ queryKey: ledgerQueryKeys.reconcileBalances(variables.legalEntityId) });
    }
  });
}

export function useCreateDimension() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ legalEntityId, request }: { legalEntityId: string; request: DimensionDefinitionRequest }) =>
      ledgerApi.createDimension(legalEntityId, request),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ledgerQueryKeys.all });
    }
  });
}

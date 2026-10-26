import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { subledgerApi } from "../services/subledger-api.ts";
import { loadSessionContext } from "../../../core/api/auth-service.ts";
import { useRuntimeContext } from "../../../core/runtime/runtime-provider.tsx";
import type {
  ApplyOpenItemsCommand,
  AutoApplySuggestionQuery,
  CreateOpeningSubledgerBatchCommand,
  CreateWriteOffCommand,
  OpenItemListFilter,
  OpeningSubledgerLineCommand,
  ReconciliationQuery,
  ReverseApplicationCommand,
  SubledgerDirection,
  UpdateFinancialProfilePayload,
  UpdateWriteOffPolicyPayload
} from "../types.ts";

export interface SubledgerContextScope {
  readonly tenantId?: string | undefined;
  readonly legalEntityId?: string | undefined;
}

export function useSubledgerScope(explicit?: SubledgerContextScope): {
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
    // Graceful fallback outside provider
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

  if (!legalEntityId && typeof window !== "undefined") {
    legalEntityId = window.sessionStorage.getItem("erp.current.legalEntityId") || "";
  }

  return { tenantId, legalEntityId };
}

export const subledgerKeys = {
  all: ["subledger"] as const,
  openItems: (legalEntityId: string, filter?: Partial<OpenItemListFilter>) =>
    [...subledgerKeys.all, "open-items", legalEntityId, filter] as const,
  openItemDetail: (legalEntityId: string, openItemId: string) =>
    [...subledgerKeys.all, "open-item", legalEntityId, openItemId] as const,
  partyExposure: (legalEntityId: string, partyId: string, direction: SubledgerDirection, asOfDate?: string) =>
    [...subledgerKeys.all, "exposure", legalEntityId, partyId, direction, asOfDate] as const,
  financialProfile: (legalEntityId: string, partyId: string) =>
    [...subledgerKeys.all, "financial-profile", legalEntityId, partyId] as const,
  aging: (legalEntityId: string, partyId: string, direction: SubledgerDirection, asOfDate: string, currencyCode?: string) =>
    [...subledgerKeys.all, "aging", legalEntityId, partyId, direction, asOfDate, currencyCode] as const,
  statement: (legalEntityId: string, partyId: string, direction: SubledgerDirection, fromDate: string, toDate: string, currencyCode?: string, page?: number) =>
    [...subledgerKeys.all, "statement", legalEntityId, partyId, direction, fromDate, toDate, currencyCode, page] as const,
  controlAccounts: (legalEntityId: string, asOfDate: string) =>
    [...subledgerKeys.all, "control-accounts", legalEntityId, asOfDate] as const,
  reconciliationRun: (legalEntityId: string, runId: string) =>
    [...subledgerKeys.all, "reconciliation-run", legalEntityId, runId] as const
};

export function useOpenItemsQuery(filter: OpenItemListFilter, enabled = true) {
  return useQuery({
    queryKey: subledgerKeys.openItems(filter.legalEntityId, filter),
    queryFn: ({ signal }) => subledgerApi.listOpenItems(filter, { signal }),
    enabled: Boolean(filter.legalEntityId && enabled),
    staleTime: 30_000
  });
}

export function useOpenItemDetailQuery(legalEntityId: string, openItemId?: string) {
  return useQuery({
    queryKey: subledgerKeys.openItemDetail(legalEntityId, openItemId || ""),
    queryFn: ({ signal }) => subledgerApi.getOpenItem(legalEntityId, openItemId!, { signal }),
    enabled: Boolean(legalEntityId && openItemId),
    staleTime: 30_000
  });
}

export function usePartyExposureQuery(
  legalEntityId: string,
  partyId?: string,
  direction: SubledgerDirection = "Receivable",
  asOfDate?: string,
  enabled = true
) {
  return useQuery({
    queryKey: subledgerKeys.partyExposure(legalEntityId, partyId || "", direction, asOfDate),
    queryFn: ({ signal }) => subledgerApi.getPartyExposure(legalEntityId, partyId!, direction, asOfDate, { signal }),
    enabled: Boolean(legalEntityId && partyId && enabled),
    staleTime: 30_000
  });
}

export function usePartyFinancialProfileQuery(legalEntityId: string, partyId?: string) {
  return useQuery({
    queryKey: subledgerKeys.financialProfile(legalEntityId, partyId || ""),
    queryFn: ({ signal }) => subledgerApi.getFinancialProfile(legalEntityId, partyId!, { signal }),
    enabled: Boolean(legalEntityId && partyId),
    staleTime: 60_000
  });
}

export function usePartyAgingQuery(
  legalEntityId: string,
  partyId?: string,
  direction: SubledgerDirection = "Receivable",
  asOfDate = "",
  currencyCode?: string,
  enabled = true
) {
  return useQuery({
    queryKey: subledgerKeys.aging(legalEntityId, partyId || "", direction, asOfDate, currencyCode),
    queryFn: ({ signal }) => subledgerApi.getAgingReport(legalEntityId, partyId!, direction, asOfDate, currencyCode, { signal }),
    enabled: Boolean(legalEntityId && partyId && asOfDate && enabled),
    staleTime: 60_000
  });
}

export function usePartyStatementQuery(
  legalEntityId: string,
  partyId?: string,
  direction: SubledgerDirection = "Receivable",
  fromDate = "",
  toDate = "",
  currencyCode?: string,
  page = 1,
  limit = 200,
  enabled = true
) {
  return useQuery({
    queryKey: subledgerKeys.statement(legalEntityId, partyId || "", direction, fromDate, toDate, currencyCode, page),
    queryFn: ({ signal }) => subledgerApi.getPartyStatement(legalEntityId, partyId!, direction, fromDate, toDate, currencyCode, limit, page, { signal }),
    enabled: Boolean(legalEntityId && partyId && fromDate && toDate && enabled),
    staleTime: 60_000
  });
}

export function useReconciliationControlAccountsQuery(legalEntityId: string, asOfDate: string, enabled = true) {
  return useQuery({
    queryKey: subledgerKeys.controlAccounts(legalEntityId, asOfDate),
    queryFn: ({ signal }) => subledgerApi.getReconciliationControlAccounts(legalEntityId, asOfDate, { signal }),
    enabled: Boolean(legalEntityId && asOfDate && enabled),
    staleTime: 60_000
  });
}

// Mutations
export function useAutoApplySuggestionsMutation() {
  return useMutation({
    mutationFn: (query: AutoApplySuggestionQuery) => subledgerApi.suggestApplications(query)
  });
}

export function useApplicationPreviewMutation() {
  return useMutation({
    mutationFn: (command: ApplyOpenItemsCommand) => subledgerApi.previewApplication(command)
  });
}

export function useApplyOpenItemsMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (command: ApplyOpenItemsCommand) => subledgerApi.applyOpenItems(command),
    onSuccess: (_, variables) => {
      void queryClient.invalidateQueries({ queryKey: [...subledgerKeys.all, "open-items", variables.legalEntityId] });
      void queryClient.invalidateQueries({ queryKey: [...subledgerKeys.all, "exposure", variables.legalEntityId] });
      void queryClient.invalidateQueries({ queryKey: [...subledgerKeys.all, "statement", variables.legalEntityId] });
    }
  });
}

export function useReverseApplicationMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (command: ReverseApplicationCommand) => subledgerApi.reverseApplication(command),
    onSuccess: (_, variables) => {
      void queryClient.invalidateQueries({ queryKey: [...subledgerKeys.all, "open-items", variables.legalEntityId] });
      void queryClient.invalidateQueries({ queryKey: [...subledgerKeys.all, "exposure", variables.legalEntityId] });
      void queryClient.invalidateQueries({ queryKey: [...subledgerKeys.all, "statement", variables.legalEntityId] });
    }
  });
}

export function useUpdateFinancialProfileMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ partyId, payload }: { partyId: string; payload: UpdateFinancialProfilePayload }) =>
      subledgerApi.updateFinancialProfile(partyId, payload),
    onSuccess: (data) => {
      void queryClient.invalidateQueries({ queryKey: subledgerKeys.financialProfile(data.legalEntityId, data.partyId) });
      void queryClient.invalidateQueries({ queryKey: [...subledgerKeys.all, "exposure", data.legalEntityId, data.partyId] });
    }
  });
}

export function useCreateWriteOffMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (command: CreateWriteOffCommand) => subledgerApi.createWriteOffRequest(command),
    onSuccess: (_, variables) => {
      void queryClient.invalidateQueries({ queryKey: [...subledgerKeys.all, "open-items", variables.legalEntityId] });
    }
  });
}

export function useApproveWriteOffMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ requestId, legalEntityId }: { requestId: string; legalEntityId: string }) =>
      subledgerApi.approveWriteOff(requestId, legalEntityId),
    onSuccess: (_, variables) => {
      void queryClient.invalidateQueries({ queryKey: [...subledgerKeys.all, "open-items", variables.legalEntityId] });
      void queryClient.invalidateQueries({ queryKey: [...subledgerKeys.all, "exposure", variables.legalEntityId] });
    }
  });
}

export function useRunReconciliationMutation() {
  return useMutation({
    mutationFn: (query: ReconciliationQuery) => subledgerApi.runReconciliation(query)
  });
}

export function useCreateOpeningBatchMutation() {
  return useMutation({
    mutationFn: (command: CreateOpeningSubledgerBatchCommand) => subledgerApi.createOpeningBatch(command)
  });
}

export function useAddOpeningLinesMutation() {
  return useMutation({
    mutationFn: ({ batchId, legalEntityId, lines }: { batchId: string; legalEntityId: string; lines: readonly OpeningSubledgerLineCommand[] }) =>
      subledgerApi.addOpeningLines(batchId, legalEntityId, lines)
  });
}

export function useValidateOpeningBatchMutation() {
  return useMutation({
    mutationFn: ({ batchId, legalEntityId }: { batchId: string; legalEntityId: string }) =>
      subledgerApi.validateOpeningBatch(batchId, legalEntityId)
  });
}

export function useFinalizeOpeningBatchMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ batchId, legalEntityId }: { batchId: string; legalEntityId: string }) =>
      subledgerApi.finalizeOpeningBatch(batchId, legalEntityId),
    onSuccess: (_, variables) => {
      void queryClient.invalidateQueries({ queryKey: [...subledgerKeys.all, "open-items", variables.legalEntityId] });
    }
  });
}

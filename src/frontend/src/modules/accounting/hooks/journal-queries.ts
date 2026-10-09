import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { journalApi } from "../api/journal-api.ts";
import type { Account, JournalEntry, JournalLine, JournalValidationResult } from "../types.ts";
import { queryKeyFactory } from "../../../platform/api/query-keys.ts";

export interface UseAccountingContext {
  readonly tenantId?: string | undefined;
  readonly entityId?: string | undefined;
}

const DEFAULT_TENANT = "tenant-default";
const DEFAULT_ENTITY = "entity-default";

export function useJournalRecord(id: string, context: UseAccountingContext = {}) {
  const tenantId = context.tenantId || DEFAULT_TENANT;
  const entityId = context.entityId || DEFAULT_ENTITY;

  return useQuery<JournalEntry>({
    queryKey: queryKeyFactory.detail(tenantId, entityId, "accounting", "journals", id),
    queryFn: () => journalApi.getById(id, tenantId),
    enabled: Boolean(id),
    staleTime: 60_000,
  });
}

export function useAccountSearch(searchTerm: string, context: UseAccountingContext = {}) {
  const tenantId = context.tenantId || DEFAULT_TENANT;
  const entityId = context.entityId || DEFAULT_ENTITY;

  return useQuery<readonly Account[]>({
    queryKey: queryKeyFactory.list(tenantId, entityId, "accounting", "accounts", { search: searchTerm }),
    queryFn: () => journalApi.searchAccounts(searchTerm, tenantId),
    enabled: searchTerm.trim().length >= 1,
    staleTime: 60_000,
  });
}

export function useValidateJournal(context: UseAccountingContext = {}) {
  const tenantId = context.tenantId || DEFAULT_TENANT;

  return useMutation<JournalValidationResult, Error, { lines: readonly JournalLine[] }>({
    mutationFn: ({ lines }) => journalApi.validate(lines, tenantId),
  });
}

export function usePostJournal(context: UseAccountingContext = {}) {
  const queryClient = useQueryClient();
  const tenantId = context.tenantId || DEFAULT_TENANT;
  const entityId = context.entityId || DEFAULT_ENTITY;

  return useMutation<JournalEntry, Error, { id: string }>({
    mutationFn: ({ id }) => journalApi.post(id, tenantId),
    onSuccess: (_data, { id }) => {
      void queryClient.invalidateQueries({
        queryKey: queryKeyFactory.detail(tenantId, entityId, "accounting", "journals", id),
      });
      void queryClient.invalidateQueries({
        queryKey: queryKeyFactory.resource(tenantId, entityId, "accounting", "journals"),
      });
    },
  });
}

import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { partyApi, type PartyListParams, type PartyListResult } from "../api/party-api.ts";
import type { Party, CreatePartyInput } from "../types.ts";
import { queryKeyFactory } from "../../../platform/api/query-keys.ts";

export interface UsePartyContext {
  readonly tenantId?: string | undefined;
  readonly entityId?: string | undefined;
}

const DEFAULT_TENANT = "tenant-default";
const DEFAULT_ENTITY = "entity-default";

export function usePartyList(
  params: PartyListParams = {},
  context: UsePartyContext = {}
) {
  const tenantId = context.tenantId || DEFAULT_TENANT;
  const entityId = context.entityId || DEFAULT_ENTITY;

  return useQuery<PartyListResult>({
    queryKey: queryKeyFactory.list(tenantId, entityId, "directory", "parties", params),
    queryFn: () => partyApi.list(params, tenantId),
    staleTime: 30_000,
  });
}

export function usePartyRecord(
  id: string,
  context: UsePartyContext = {}
) {
  const tenantId = context.tenantId || DEFAULT_TENANT;
  const entityId = context.entityId || DEFAULT_ENTITY;

  return useQuery<Party>({
    queryKey: queryKeyFactory.detail(tenantId, entityId, "directory", "parties", id),
    queryFn: () => partyApi.getById(id, tenantId),
    enabled: Boolean(id),
    staleTime: 60_000,
  });
}

export function usePartySearch(
  searchTerm: string,
  context: UsePartyContext = {}
) {
  const tenantId = context.tenantId || DEFAULT_TENANT;
  const entityId = context.entityId || DEFAULT_ENTITY;

  return useQuery<PartyListResult>({
    queryKey: queryKeyFactory.list(tenantId, entityId, "directory", "parties", { search: searchTerm }),
    queryFn: () => partyApi.list({ search: searchTerm }, tenantId),
    enabled: searchTerm.trim().length >= 1,
    staleTime: 10_000,
  });
}

export function useCreateParty(context: UsePartyContext = {}) {
  const queryClient = useQueryClient();
  const tenantId = context.tenantId || DEFAULT_TENANT;
  const entityId = context.entityId || DEFAULT_ENTITY;

  return useMutation<Party, Error, CreatePartyInput>({
    mutationFn: (input: CreatePartyInput) => partyApi.create(input, tenantId),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: queryKeyFactory.resource(tenantId, entityId, "directory", "parties"),
      });
    },
  });
}

export function useUpdateParty(context: UsePartyContext = {}) {
  const queryClient = useQueryClient();
  const tenantId = context.tenantId || DEFAULT_TENANT;
  const entityId = context.entityId || DEFAULT_ENTITY;

  return useMutation<Party, Error, { id: string; input: Partial<CreatePartyInput> }>({
    mutationFn: ({ id, input }) => partyApi.update(id, input, tenantId),
    onSuccess: (_data, { id }) => {
      void queryClient.invalidateQueries({
        queryKey: queryKeyFactory.detail(tenantId, entityId, "directory", "parties", id),
      });
      void queryClient.invalidateQueries({
        queryKey: queryKeyFactory.resource(tenantId, entityId, "directory", "parties"),
      });
    },
  });
}

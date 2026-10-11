import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { partyApi, type PartyListParams, type PartyListResult } from "../services/party-api.ts";
import type {
  Party,
  CreatePartyInput,
  UpdatePartyInput,
  PartyRoleCode,
  IdentifierInput,
  ContactPointInput,
  AddressInput,
  AddContactPersonRequest,
  AliasInput,
  VisibilityInput,
  MergePartyRequest,
  DuplicateCandidate,
  PartyReadiness,
  AuditEntryDto,
  DirectoryImportRow,
  DirectoryImportResult,
  DirectoryMergeResult
} from "../types.ts";
import { queryKeyFactory } from "../../../core/api/query-keys.ts";

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

export function usePartyDuplicates(
  partyId: string,
  context: UsePartyContext = {}
) {
  const tenantId = context.tenantId || DEFAULT_TENANT;
  const entityId = context.entityId || DEFAULT_ENTITY;

  return useQuery<readonly DuplicateCandidate[]>({
    queryKey: [...queryKeyFactory.detail(tenantId, entityId, "directory", "parties", partyId), "duplicates"],
    queryFn: () => partyApi.getDuplicates(partyId, tenantId),
    enabled: Boolean(partyId),
    staleTime: 60_000,
  });
}

export function usePartyReadiness(
  partyId: string,
  context: UsePartyContext = {}
) {
  const tenantId = context.tenantId || DEFAULT_TENANT;
  const entityId = context.entityId || DEFAULT_ENTITY;

  return useQuery<PartyReadiness>({
    queryKey: [...queryKeyFactory.detail(tenantId, entityId, "directory", "parties", partyId), "readiness"],
    queryFn: () => partyApi.getReadiness(partyId, tenantId),
    enabled: Boolean(partyId),
    staleTime: 60_000,
  });
}

export function usePartyAudit(
  partyId: string,
  context: UsePartyContext = {}
) {
  const tenantId = context.tenantId || DEFAULT_TENANT;
  const entityId = context.entityId || DEFAULT_ENTITY;

  return useQuery<readonly AuditEntryDto[]>({
    queryKey: [...queryKeyFactory.detail(tenantId, entityId, "directory", "parties", partyId), "audit"],
    queryFn: () => partyApi.getAudit(partyId, tenantId),
    enabled: Boolean(partyId),
    staleTime: 30_000,
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

export function useCreatePartyWithOverride(context: UsePartyContext = {}) {
  const queryClient = useQueryClient();
  const tenantId = context.tenantId || DEFAULT_TENANT;
  const entityId = context.entityId || DEFAULT_ENTITY;

  return useMutation<Party, Error, CreatePartyInput>({
    mutationFn: (input: CreatePartyInput) => partyApi.createWithDuplicateOverride(input, tenantId),
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

  return useMutation<Party, Error, { id: string; input: UpdatePartyInput }>({
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

export function useArchiveParty(context: UsePartyContext = {}) {
  const queryClient = useQueryClient();
  const tenantId = context.tenantId || DEFAULT_TENANT;
  const entityId = context.entityId || DEFAULT_ENTITY;

  return useMutation<Party, Error, { id: string; version: number }>({
    mutationFn: ({ id, version }) => partyApi.archive(id, version, tenantId),
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

export function useRestoreParty(context: UsePartyContext = {}) {
  const queryClient = useQueryClient();
  const tenantId = context.tenantId || DEFAULT_TENANT;
  const entityId = context.entityId || DEFAULT_ENTITY;

  return useMutation<Party, Error, { id: string; version: number }>({
    mutationFn: ({ id, version }) => partyApi.restore(id, version, tenantId),
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

export function useAddPartyRole(context: UsePartyContext = {}) {
  const queryClient = useQueryClient();
  const tenantId = context.tenantId || DEFAULT_TENANT;
  const entityId = context.entityId || DEFAULT_ENTITY;

  return useMutation<Party, Error, { id: string; role: PartyRoleCode; version: number }>({
    mutationFn: ({ id, role, version }) => partyApi.addRole(id, role, version, tenantId),
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

export function useDisablePartyRole(context: UsePartyContext = {}) {
  const queryClient = useQueryClient();
  const tenantId = context.tenantId || DEFAULT_TENANT;
  const entityId = context.entityId || DEFAULT_ENTITY;

  return useMutation<Party, Error, { id: string; role: PartyRoleCode }>({
    mutationFn: ({ id, role }) => partyApi.disableRole(id, role, tenantId),
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

export function useAddPartyIdentifier(context: UsePartyContext = {}) {
  const queryClient = useQueryClient();
  const tenantId = context.tenantId || DEFAULT_TENANT;
  const entityId = context.entityId || DEFAULT_ENTITY;

  return useMutation<Party, Error, { id: string; input: IdentifierInput }>({
    mutationFn: ({ id, input }) => partyApi.addIdentifier(id, input, tenantId),
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

export function useAddPartyContactPoint(context: UsePartyContext = {}) {
  const queryClient = useQueryClient();
  const tenantId = context.tenantId || DEFAULT_TENANT;
  const entityId = context.entityId || DEFAULT_ENTITY;

  return useMutation<Party, Error, { id: string; input: ContactPointInput }>({
    mutationFn: ({ id, input }) => partyApi.addContactPoint(id, input, tenantId),
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

export function useAddPartyAddress(context: UsePartyContext = {}) {
  const queryClient = useQueryClient();
  const tenantId = context.tenantId || DEFAULT_TENANT;
  const entityId = context.entityId || DEFAULT_ENTITY;

  return useMutation<Party, Error, { id: string; input: AddressInput }>({
    mutationFn: ({ id, input }) => partyApi.addAddress(id, input, tenantId),
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

export function useAddPartyContactPerson(context: UsePartyContext = {}) {
  const queryClient = useQueryClient();
  const tenantId = context.tenantId || DEFAULT_TENANT;
  const entityId = context.entityId || DEFAULT_ENTITY;

  return useMutation<Party, Error, { id: string; input: AddContactPersonRequest }>({
    mutationFn: ({ id, input }) => partyApi.addContactPerson(id, input, tenantId),
    onSuccess: (_data, { id }) => {
      void queryClient.invalidateQueries({
        queryKey: queryKeyFactory.detail(tenantId, entityId, "directory", "parties", id),
      });
    },
  });
}

export function useAddPartyAlias(context: UsePartyContext = {}) {
  const queryClient = useQueryClient();
  const tenantId = context.tenantId || DEFAULT_TENANT;
  const entityId = context.entityId || DEFAULT_ENTITY;

  return useMutation<Party, Error, { id: string; input: AliasInput }>({
    mutationFn: ({ id, input }) => partyApi.addAlias(id, input, tenantId),
    onSuccess: (_data, { id }) => {
      void queryClient.invalidateQueries({
        queryKey: queryKeyFactory.detail(tenantId, entityId, "directory", "parties", id),
      });
    },
  });
}

export function useSetPartyVisibility(context: UsePartyContext = {}) {
  const queryClient = useQueryClient();
  const tenantId = context.tenantId || DEFAULT_TENANT;
  const entityId = context.entityId || DEFAULT_ENTITY;

  return useMutation<Party, Error, { id: string; input: VisibilityInput }>({
    mutationFn: ({ id, input }) => partyApi.setVisibility(id, input, tenantId),
    onSuccess: (_data, { id }) => {
      void queryClient.invalidateQueries({
        queryKey: queryKeyFactory.detail(tenantId, entityId, "directory", "parties", id),
      });
    },
  });
}

export function useMergeParty(context: UsePartyContext = {}) {
  const queryClient = useQueryClient();
  const tenantId = context.tenantId || DEFAULT_TENANT;
  const entityId = context.entityId || DEFAULT_ENTITY;

  return useMutation<DirectoryMergeResult, Error, { sourcePartyId: string; input: MergePartyRequest }>({
    mutationFn: ({ sourcePartyId, input }) => partyApi.merge(sourcePartyId, input, tenantId),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: queryKeyFactory.resource(tenantId, entityId, "directory", "parties"),
      });
    },
  });
}

export function useImportParties(context: UsePartyContext = {}) {
  const queryClient = useQueryClient();
  const tenantId = context.tenantId || DEFAULT_TENANT;
  const entityId = context.entityId || DEFAULT_ENTITY;

  return useMutation<DirectoryImportResult, Error, { rows: readonly DirectoryImportRow[]; commit: boolean }>({
    mutationFn: ({ rows, commit }) => partyApi.importRows(rows, commit, tenantId),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: queryKeyFactory.resource(tenantId, entityId, "directory", "parties"),
      });
    },
  });
}

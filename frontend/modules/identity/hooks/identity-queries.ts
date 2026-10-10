import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { queryKeyFactory } from "../../../core/api/query-keys.ts";
import * as identityApi from "../services/identity-api.ts";
import type {
  UserAccountSummary,
  CreatedUserAccount,
  ResetPasswordResult,
  RoleSummary,
  LoginDomainInfo,
  CreateUserPayload,
  ChangeLoginDomainPayload,
  ListUsersParams
} from "../types.ts";

export interface UseIdentityContext {
  readonly tenantId?: string | undefined;
  readonly entityId?: string | undefined;
}

const DEFAULT_TENANT = "tenant-default";
const DEFAULT_ENTITY = "entity-default";

export function useUsersList(
  params: ListUsersParams = {},
  context: UseIdentityContext = {}
) {
  const tenantId = context.tenantId || DEFAULT_TENANT;
  const entityId = context.entityId || DEFAULT_ENTITY;

  return useQuery<UserAccountSummary[]>({
    queryKey: queryKeyFactory.list(tenantId, entityId, "identity", "users", params as Record<string, unknown>),
    queryFn: () => identityApi.fetchUsers(tenantId, params),
    staleTime: 30_000
  });
}

export function useUserRecord(
  identityId: string,
  context: UseIdentityContext = {}
) {
  const tenantId = context.tenantId || DEFAULT_TENANT;
  const entityId = context.entityId || DEFAULT_ENTITY;

  return useQuery<UserAccountSummary>({
    queryKey: queryKeyFactory.detail(tenantId, entityId, "identity", "users", identityId),
    queryFn: () => identityApi.fetchUser(tenantId, identityId),
    enabled: Boolean(identityId),
    staleTime: 60_000
  });
}

export function useRolesList(
  context: UseIdentityContext = {}
) {
  const tenantId = context.tenantId || DEFAULT_TENANT;
  const entityId = context.entityId || DEFAULT_ENTITY;

  return useQuery<RoleSummary[]>({
    queryKey: queryKeyFactory.list(tenantId, entityId, "identity", "roles", {}),
    queryFn: () => identityApi.fetchRoles(tenantId),
    staleTime: 300_000
  });
}

export function useLoginDomain(
  context: UseIdentityContext = {}
) {
  const tenantId = context.tenantId || DEFAULT_TENANT;
  const entityId = context.entityId || DEFAULT_ENTITY;

  return useQuery<LoginDomainInfo>({
    queryKey: queryKeyFactory.detail(tenantId, entityId, "identity", "login-domain", "current"),
    queryFn: () => identityApi.fetchLoginDomain(tenantId),
    staleTime: 120_000
  });
}

export function useCreateUser(context: UseIdentityContext = {}) {
  const queryClient = useQueryClient();
  const tenantId = context.tenantId || DEFAULT_TENANT;
  const entityId = context.entityId || DEFAULT_ENTITY;

  return useMutation<CreatedUserAccount, Error, CreateUserPayload>({
    mutationFn: (payload) => identityApi.createUser(tenantId, payload),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: queryKeyFactory.list(tenantId, entityId, "identity", "users")
      });
    }
  });
}

export function useResetPassword(context: UseIdentityContext = {}) {
  const queryClient = useQueryClient();
  const tenantId = context.tenantId || DEFAULT_TENANT;
  const entityId = context.entityId || DEFAULT_ENTITY;

  return useMutation<ResetPasswordResult, Error, string>({
    mutationFn: (identityId) => identityApi.resetUserPassword(tenantId, identityId),
    onSuccess: (_, identityId) => {
      void queryClient.invalidateQueries({
        queryKey: queryKeyFactory.detail(tenantId, entityId, "identity", "users", identityId)
      });
    }
  });
}

export function useDeactivateUser(context: UseIdentityContext = {}) {
  const queryClient = useQueryClient();
  const tenantId = context.tenantId || DEFAULT_TENANT;
  const entityId = context.entityId || DEFAULT_ENTITY;

  return useMutation<void, Error, string>({
    mutationFn: (identityId) => identityApi.deactivateUser(tenantId, identityId),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: queryKeyFactory.list(tenantId, entityId, "identity", "users")
      });
    }
  });
}

export function useReactivateUser(context: UseIdentityContext = {}) {
  const queryClient = useQueryClient();
  const tenantId = context.tenantId || DEFAULT_TENANT;
  const entityId = context.entityId || DEFAULT_ENTITY;

  return useMutation<void, Error, string>({
    mutationFn: (identityId) => identityApi.reactivateUser(tenantId, identityId),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: queryKeyFactory.list(tenantId, entityId, "identity", "users")
      });
    }
  });
}

export function useUpdateLoginDomain(context: UseIdentityContext = {}) {
  const queryClient = useQueryClient();
  const tenantId = context.tenantId || DEFAULT_TENANT;
  const entityId = context.entityId || DEFAULT_ENTITY;

  return useMutation<LoginDomainInfo, Error, ChangeLoginDomainPayload>({
    mutationFn: (payload) => identityApi.updateLoginDomain(tenantId, payload),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: queryKeyFactory.detail(tenantId, entityId, "identity", "login-domain", "current")
      });
      void queryClient.invalidateQueries({
        queryKey: queryKeyFactory.list(tenantId, entityId, "identity", "users")
      });
    }
  });
}

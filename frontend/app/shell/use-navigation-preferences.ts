import { useCallback, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { queryKeyFactory } from "../../core/api/query-keys.ts";
import { userNavigationApi, type UserNavigationSnapshot } from "../../core/api/user-navigation.ts";

const emptySnapshot: UserNavigationSnapshot = { favorites: [], recent: [] };

function storageKey(tenantId: string, identityId: string): string {
  return `erp.navigation.${tenantId}.${identityId}`;
}

function readCachedSnapshot(tenantId: string, identityId: string): UserNavigationSnapshot | undefined {
  try {
    const serialized = window.localStorage.getItem(storageKey(tenantId, identityId));
    if (!serialized) return undefined;
    const value = JSON.parse(serialized) as Partial<UserNavigationSnapshot>;
    if (!Array.isArray(value.favorites) || !Array.isArray(value.recent)) return undefined;
    return {
      favorites: value.favorites.filter((key): key is string => typeof key === "string"),
      recent: value.recent.filter((key): key is string => typeof key === "string")
    };
  } catch {
    return undefined;
  }
}

function writeCachedSnapshot(tenantId: string, identityId: string, snapshot: UserNavigationSnapshot): void {
  try {
    window.localStorage.setItem(storageKey(tenantId, identityId), JSON.stringify(snapshot));
  } catch {
    // The server remains authoritative when browser storage is unavailable.
  }
}

export function useNavigationPreferences(tenantId: string, identityId: string, enabled: boolean) {
  const queryClient = useQueryClient();
  const [saveFailed, setSaveFailed] = useState(false);
  const queryKey = queryKeyFactory.userNavigation(tenantId, identityId);

  const preferencesQuery = useQuery({
    queryKey,
    queryFn: async ({ signal }) => {
      const snapshot = await userNavigationApi.get(signal);
      writeCachedSnapshot(tenantId, identityId, snapshot);
      return snapshot;
    },
    enabled: enabled && Boolean(tenantId) && Boolean(identityId),
    initialData: () => tenantId && identityId ? readCachedSnapshot(tenantId, identityId) : undefined,
    staleTime: 0,
    refetchOnMount: "always",
    retry: false
  });

  const favoriteMutation = useMutation({
    mutationFn: ({ navigationKey, favorite }: { navigationKey: string; favorite: boolean }) =>
      favorite ? userNavigationApi.addFavorite(navigationKey) : userNavigationApi.removeFavorite(navigationKey),
    onMutate: async ({ navigationKey, favorite }) => {
      setSaveFailed(false);
      await queryClient.cancelQueries({ queryKey });
      const previous = queryClient.getQueryData<UserNavigationSnapshot>(queryKey) ?? emptySnapshot;
      const favorites = favorite
        ? [...new Set([navigationKey, ...previous.favorites])]
        : previous.favorites.filter((item) => item !== navigationKey);
      const next = { ...previous, favorites };
      queryClient.setQueryData(queryKey, next);
      writeCachedSnapshot(tenantId, identityId, next);
      return { previous };
    },
    onError: (_error, _variables, context) => {
      if (context?.previous) {
        queryClient.setQueryData(queryKey, context.previous);
        writeCachedSnapshot(tenantId, identityId, context.previous);
      }
      setSaveFailed(true);
    },
    onSuccess: () => setSaveFailed(false)
  });

  const recentMutation = useMutation({
    mutationFn: (navigationKey: string) => userNavigationApi.recordRecent(navigationKey),
    onMutate: async (navigationKey) => {
      await queryClient.cancelQueries({ queryKey });
      const previous = queryClient.getQueryData<UserNavigationSnapshot>(queryKey) ?? emptySnapshot;
      const next = { ...previous, recent: [navigationKey, ...previous.recent.filter((item) => item !== navigationKey)].slice(0, 10) };
      queryClient.setQueryData(queryKey, next);
      writeCachedSnapshot(tenantId, identityId, next);
      return { previous };
    },
    onError: (_error, _navigationKey, context) => {
      if (context?.previous) {
        queryClient.setQueryData(queryKey, context.previous);
        writeCachedSnapshot(tenantId, identityId, context.previous);
      }
    }
  });

  const setFavorite = useCallback((navigationKey: string, favorite: boolean) => {
    favoriteMutation.mutate({ navigationKey, favorite });
  }, [favoriteMutation.mutate]);
  const recordRecent = useCallback((navigationKey: string) => {
    recentMutation.mutate(navigationKey);
  }, [recentMutation.mutate]);

  return {
    snapshot: preferencesQuery.data ?? emptySnapshot,
    setFavorite,
    recordRecent,
    isSavingFavorite: favoriteMutation.isPending,
    saveFailed
  };
}

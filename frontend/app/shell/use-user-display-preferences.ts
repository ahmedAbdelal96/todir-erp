import { useCallback, useMemo, useRef, useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { userPreferencesApi, defaultUserDisplayPreferences, type UserDisplayPreferences, type UserDisplayPreferencesPatch } from "../../core/api/user-preferences.ts";

function cacheKey(tenantId: string, identityId: string): string {
  return `erp.display-preferences.${tenantId}.${identityId}`;
}

function readCache(tenantId: string, identityId: string): UserDisplayPreferences | undefined {
  try {
    const serialized = window.localStorage.getItem(cacheKey(tenantId, identityId));
    if (!serialized) return undefined;
    const value = JSON.parse(serialized) as Partial<UserDisplayPreferences>;
    if ((value.language !== "ar" && value.language !== "en") ||
      (value.theme !== "default" && value.theme !== "dark") ||
      (value.density !== "comfortable" && value.density !== "compact") ||
      typeof value.sidebarCollapsed !== "boolean") return undefined;
    return value as UserDisplayPreferences;
  } catch {
    return undefined;
  }
}

function writeCache(tenantId: string, identityId: string, preferences: UserDisplayPreferences): void {
  try {
    window.localStorage.setItem(cacheKey(tenantId, identityId), JSON.stringify(preferences));
  } catch {
    // The authenticated server remains the cross-device source of truth.
  }
}

export function useUserDisplayPreferences(tenantId: string, identityId: string, enabled: boolean) {
  const queryClient = useQueryClient();
  const [saveFailed, setSaveFailed] = useState(false);
  const pendingPatches = useRef(new Map<string, UserDisplayPreferencesPatch>());
  const saveQueues = useRef(new Map<string, Promise<void>>());
  const scopeKey = `${tenantId}.${identityId}`;
  const queryKey = useMemo(() => ["todir", "tenant", tenantId, "identity", identityId, "display-preferences"] as const, [identityId, tenantId]);

  const preferencesQuery = useQuery({
    queryKey,
    queryFn: async ({ signal }) => {
      const remotePreferences = await userPreferencesApi.get(signal);
      const pending = pendingPatches.current.get(scopeKey) ?? {};
      const preferences = { ...remotePreferences, ...pending };
      writeCache(tenantId, identityId, preferences);
      if (Object.keys(pending).length === 0) setSaveFailed(false);
      return preferences;
    },
    enabled: enabled && Boolean(tenantId) && Boolean(identityId),
    initialData: () => tenantId && identityId ? readCache(tenantId, identityId) : undefined,
    staleTime: 0,
    refetchOnMount: "always",
    retry: false
  });

  const update = useCallback((patch: UserDisplayPreferencesPatch) => {
    const previous = queryClient.getQueryData<UserDisplayPreferences>(queryKey) ?? defaultUserDisplayPreferences;
    const next = { ...previous, ...patch };
    queryClient.setQueryData(queryKey, next);
    writeCache(tenantId, identityId, next);
    pendingPatches.current.set(scopeKey, { ...pendingPatches.current.get(scopeKey), ...patch });

    const patchToSave = { ...patch };
    const save = (saveQueues.current.get(scopeKey) ?? Promise.resolve())
      .catch(() => undefined)
      .then(() => userPreferencesApi.update(patchToSave));
    saveQueues.current.set(scopeKey, save);
    void save.then(() => {
      const pending = pendingPatches.current.get(scopeKey);
      if (pending) {
        for (const key of Object.keys(patchToSave) as Array<keyof UserDisplayPreferences>) {
          if (pending[key] === patchToSave[key]) delete pending[key];
        }
      }
      setSaveFailed(false);
    }, (err: unknown) => {
      if (err instanceof Error && err.name === "AbortError") return;
      setSaveFailed(true);
    });
  }, [identityId, queryClient, queryKey, scopeKey, tenantId]);

  return {
    preferences: preferencesQuery.data ?? defaultUserDisplayPreferences,
    update,
    saveFailed
  };
}

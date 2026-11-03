import { httpClient } from "./http-client.ts";

export interface UserNavigationSnapshot {
  readonly favorites: readonly string[];
  readonly recent: readonly string[];
}

const path = "/api/v1/me/navigation-preferences";

export const userNavigationApi = {
  get: (signal?: AbortSignal): Promise<UserNavigationSnapshot> =>
    httpClient<UserNavigationSnapshot>(path, signal ? { signal } : {}),
  addFavorite: (navigationKey: string): Promise<void> =>
    httpClient<void>(`${path}/favorites`, { method: "PUT", body: JSON.stringify({ navigationKey }) }),
  removeFavorite: (navigationKey: string): Promise<void> =>
    httpClient<void>(`${path}/favorites/${encodeURIComponent(navigationKey)}`, { method: "DELETE" }),
  recordRecent: (navigationKey: string): Promise<void> =>
    httpClient<void>(`${path}/recent`, { method: "POST", body: JSON.stringify({ navigationKey }) })
};

import { httpClient } from "./http-client.ts";

export type UserDisplayPreferences = {
  readonly language: "ar" | "en";
  readonly theme: "default" | "dark";
  readonly density: "comfortable" | "compact";
  readonly sidebarCollapsed: boolean;
};

export type UserDisplayPreferencesPatch = Partial<UserDisplayPreferences>;

const path = "/api/v1/me/preferences";

export const defaultUserDisplayPreferences: UserDisplayPreferences = {
  language: "ar",
  theme: "default",
  density: "comfortable",
  sidebarCollapsed: false
};

export const userPreferencesApi = {
  get: (signal?: AbortSignal): Promise<UserDisplayPreferences> =>
    httpClient<UserDisplayPreferences>(path, signal ? { signal } : {}),
  update: (preferences: UserDisplayPreferencesPatch): Promise<void> =>
    httpClient<void>(path, { method: "PATCH", body: JSON.stringify(preferences) })
};

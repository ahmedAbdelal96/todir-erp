import { httpClient } from "../../../platform/api/http-client.ts";
import type { Account, JournalEntry, JournalLine, JournalValidationResult } from "../types.ts";

export const journalApi = {
  getById: async (id: string, tenantId?: string): Promise<JournalEntry> => {
    return httpClient<JournalEntry>(`/api/v1/journals/${id}`, {
      ...(tenantId !== undefined ? { tenantId } : {})
    });
  },

  validate: async (
    lines: readonly JournalLine[],
    tenantId?: string
  ): Promise<JournalValidationResult> => {
    return httpClient<JournalValidationResult>("/api/v1/journals/validate", {
      method: "POST",
      body: JSON.stringify({ lines }),
      ...(tenantId !== undefined ? { tenantId } : {})
    });
  },

  post: async (id: string, tenantId?: string): Promise<JournalEntry> => {
    return httpClient<JournalEntry>(`/api/v1/journals/${id}/post`, {
      method: "POST",
      ...(tenantId !== undefined ? { tenantId } : {})
    });
  },

  searchAccounts: async (
    query: string,
    tenantId?: string
  ): Promise<readonly Account[]> => {
    const params = new URLSearchParams();
    if (query) params.set("search", query);
    return httpClient<readonly Account[]>(`/api/v1/accounts?${params.toString()}`, {
      ...(tenantId !== undefined ? { tenantId } : {})
    });
  }
};

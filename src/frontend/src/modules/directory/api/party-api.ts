import { httpClient } from "../../../platform/api/http-client.ts";
import type { Party, CreatePartyInput } from "../types.ts";

export interface PartyListParams {
  readonly search?: string | undefined;
  readonly partyType?: string | undefined;
  readonly role?: string | undefined;
  readonly status?: string | undefined;
  readonly page?: number | undefined;
  readonly pageSize?: number | undefined;
}

export interface PartyListResult {
  readonly items: readonly Party[];
  readonly totalCount: number;
}

export const partyApi = {
  list: async (params: PartyListParams = {}, tenantId?: string): Promise<PartyListResult> => {
    const query = new URLSearchParams();
    if (params.search) query.set("search", params.search);
    if (params.partyType) query.set("partyType", params.partyType);
    if (params.role) query.set("role", params.role);
    if (params.status) query.set("status", params.status);
    if (params.page) query.set("page", String(params.page));
    if (params.pageSize) query.set("pageSize", String(params.pageSize));

    const url = `/api/v1/directory/parties?${query.toString()}`;
    return httpClient<PartyListResult>(url, {
      ...(tenantId !== undefined ? { tenantId } : {})
    });
  },

  getById: async (id: string, tenantId?: string): Promise<Party> => {
    return httpClient<Party>(`/api/v1/directory/parties/${id}`, {
      ...(tenantId !== undefined ? { tenantId } : {})
    });
  },

  create: async (input: CreatePartyInput, tenantId?: string): Promise<Party> => {
    return httpClient<Party>("/api/v1/directory/parties", {
      method: "POST",
      body: JSON.stringify(input),
      ...(tenantId !== undefined ? { tenantId } : {})
    });
  },

  update: async (id: string, input: Partial<CreatePartyInput> & { version: number }, tenantId?: string): Promise<Party> => {
    return httpClient<Party>(`/api/v1/directory/parties/${id}`, {
      method: "PATCH",
      body: JSON.stringify(input),
      ...(tenantId !== undefined ? { tenantId } : {})
    });
  }
};

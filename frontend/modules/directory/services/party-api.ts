import { httpClient } from "../../../core/api/http-client.ts";
import type {
  Party,
  CreatePartyInput,
  UpdatePartyInput,
  PartyRoleCode,
  IdentifierInput,
  ContactPointInput,
  AddressInput,
  DuplicateCandidate,
  PartyReadiness
} from "../types.ts";

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
  readonly page?: number | undefined;
  readonly pageSize?: number | undefined;
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

  update: async (id: string, input: UpdatePartyInput, tenantId?: string): Promise<Party> => {
    return httpClient<Party>(`/api/v1/directory/parties/${id}`, {
      method: "PATCH",
      body: JSON.stringify(input),
      ...(tenantId !== undefined ? { tenantId } : {})
    });
  },

  addRole: async (id: string, role: PartyRoleCode, version: number, tenantId?: string): Promise<Party> => {
    return httpClient<Party>(`/api/v1/directory/parties/${id}/roles`, {
      method: "POST",
      body: JSON.stringify({ role, version }),
      ...(tenantId !== undefined ? { tenantId } : {})
    });
  },

  disableRole: async (id: string, role: PartyRoleCode, tenantId?: string): Promise<Party> => {
    return httpClient<Party>(`/api/v1/directory/parties/${id}/roles/${role}`, {
      method: "DELETE",
      ...(tenantId !== undefined ? { tenantId } : {})
    });
  },

  archive: async (id: string, version: number, tenantId?: string): Promise<Party> => {
    return httpClient<Party>(`/api/v1/directory/parties/${id}/archive`, {
      method: "POST",
      body: JSON.stringify({ version }),
      ...(tenantId !== undefined ? { tenantId } : {})
    });
  },

  restore: async (id: string, version: number, tenantId?: string): Promise<Party> => {
    return httpClient<Party>(`/api/v1/directory/parties/${id}/restore`, {
      method: "POST",
      body: JSON.stringify({ version }),
      ...(tenantId !== undefined ? { tenantId } : {})
    });
  },

  addIdentifier: async (id: string, input: IdentifierInput, tenantId?: string): Promise<Party> => {
    return httpClient<Party>(`/api/v1/directory/parties/${id}/identifiers`, {
      method: "POST",
      body: JSON.stringify(input),
      ...(tenantId !== undefined ? { tenantId } : {})
    });
  },

  addContactPoint: async (id: string, input: ContactPointInput, tenantId?: string): Promise<Party> => {
    return httpClient<Party>(`/api/v1/directory/parties/${id}/contacts`, {
      method: "POST",
      body: JSON.stringify(input),
      ...(tenantId !== undefined ? { tenantId } : {})
    });
  },

  addAddress: async (id: string, input: AddressInput, tenantId?: string): Promise<Party> => {
    return httpClient<Party>(`/api/v1/directory/parties/${id}/addresses`, {
      method: "POST",
      body: JSON.stringify(input),
      ...(tenantId !== undefined ? { tenantId } : {})
    });
  },

  getDuplicates: async (id: string, tenantId?: string): Promise<readonly DuplicateCandidate[]> => {
    return httpClient<readonly DuplicateCandidate[]>(`/api/v1/directory/parties/${id}/duplicate-candidates`, {
      ...(tenantId !== undefined ? { tenantId } : {})
    });
  },

  getReadiness: async (id: string, tenantId?: string): Promise<PartyReadiness> => {
    return httpClient<PartyReadiness>(`/api/v1/directory/parties/${id}/readiness`, {
      ...(tenantId !== undefined ? { tenantId } : {})
    });
  }
};

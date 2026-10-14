import { httpClient } from "../../../core/api/http-client.ts";
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
  DirectoryImportResult,
  DirectoryMergeResult,
  DirectoryImportRow
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

/**
 * List responses intentionally project only search columns. Normalize omitted
 * related collections at the API boundary so shared pickers can render safely.
 */
export function normalizeParty(party: Party): Party {
  return {
    ...party,
    roles: Array.isArray(party.roles) ? party.roles : [],
    identifiers: Array.isArray(party.identifiers) ? party.identifiers : [],
    contactPoints: Array.isArray(party.contactPoints) ? party.contactPoints : [],
    addresses: Array.isArray(party.addresses) ? party.addresses : [],
    contactPersons: Array.isArray(party.contactPersons) ? party.contactPersons : [],
    aliases: Array.isArray(party.aliases) ? party.aliases : [],
    visibility: Array.isArray(party.visibility) ? party.visibility : []
  };
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
    const result = await httpClient<PartyListResult>(url, {
      ...(tenantId !== undefined ? { tenantId } : {})
    });
    return { ...result, items: result.items.map(normalizeParty) };
  },

  getById: async (id: string, tenantId?: string): Promise<Party> => {
    const result = await httpClient<Party>(`/api/v1/directory/parties/${id}`, {
      ...(tenantId !== undefined ? { tenantId } : {})
    });
    return normalizeParty(result);
  },

  create: async (input: CreatePartyInput, tenantId?: string): Promise<Party> => {
    return httpClient<Party>("/api/v1/directory/parties", {
      method: "POST",
      body: JSON.stringify(input),
      ...(tenantId !== undefined ? { tenantId } : {})
    });
  },

  createWithDuplicateOverride: async (input: CreatePartyInput, tenantId?: string): Promise<Party> => {
    return httpClient<Party>("/api/v1/directory/parties", {
      method: "POST",
      body: JSON.stringify(input),
      headers: { "X-Duplicate-Override": "true" },
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

  addContactPerson: async (id: string, input: AddContactPersonRequest, tenantId?: string): Promise<Party> => {
    return httpClient<Party>(`/api/v1/directory/parties/${id}/contact-persons`, {
      method: "POST",
      body: JSON.stringify(input),
      ...(tenantId !== undefined ? { tenantId } : {})
    });
  },

  addAlias: async (id: string, input: AliasInput, tenantId?: string): Promise<Party> => {
    return httpClient<Party>(`/api/v1/directory/parties/${id}/aliases`, {
      method: "POST",
      body: JSON.stringify(input),
      ...(tenantId !== undefined ? { tenantId } : {})
    });
  },

  setVisibility: async (id: string, input: VisibilityInput, tenantId?: string): Promise<Party> => {
    return httpClient<Party>(`/api/v1/directory/parties/${id}/visibility`, {
      method: "PUT",
      body: JSON.stringify(input),
      ...(tenantId !== undefined ? { tenantId } : {})
    });
  },

  merge: async (sourcePartyId: string, input: MergePartyRequest, tenantId?: string): Promise<DirectoryMergeResult> => {
    return httpClient<DirectoryMergeResult>(`/api/v1/directory/parties/${sourcePartyId}/merge`, {
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
  },

  getAudit: async (id: string, tenantId?: string): Promise<readonly AuditEntryDto[]> => {
    return httpClient<readonly AuditEntryDto[]>(`/api/v1/directory/parties/${id}/audit`, {
      ...(tenantId !== undefined ? { tenantId } : {})
    });
  },

  exportCsv: async (params: { search?: string; includeArchived?: boolean } = {}, tenantId?: string): Promise<string> => {
    const query = new URLSearchParams();
    if (params.search) query.set("search", params.search);
    if (params.includeArchived) query.set("includeArchived", "true");
    return httpClient<string>(`/api/v1/directory/parties/export?${query.toString()}`, {
      ...(tenantId !== undefined ? { tenantId } : {})
    });
  },

  importRows: async (rows: readonly DirectoryImportRow[], commit: boolean, tenantId?: string): Promise<DirectoryImportResult> => {
    return httpClient<DirectoryImportResult>("/api/v1/directory/parties/import", {
      method: "POST",
      headers: {
        "Idempotency-Key": `imp-${Date.now()}-${Math.random().toString(36).slice(2, 9)}`
      },
      body: JSON.stringify({ rows, commit }),
      ...(tenantId !== undefined ? { tenantId } : {})
    });
  }
};

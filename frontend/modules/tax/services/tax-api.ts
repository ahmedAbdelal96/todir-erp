import { httpClient, type RequestOptions } from "../../../core/api/http-client.ts";
import type {
  InstallTaxCountryPackRequest,
  SetTaxCapabilityRequest,
  TaxCalculationRequest,
  TaxCapabilityDto,
  TaxCountryPackActionRequest,
  TaxCountryPackDto,
  TaxCountryPackValidationDto,
  TaxCoverageResult,
  TaxDraftImpactRequest,
  TaxDraftImpactResult,
  TaxPartyProfileRequest,
  TaxProductProfileRequest,
  TaxProfileDto,
  TaxProfileExportDto,
  TaxProfileImportCommitDto,
  TaxProfileImportPreviewDto,
  TaxProfileImportPreviewRequest,
  TaxReferenceDataDto,
  TaxRegistrationChangeRequest,
  TaxRegistrationDto,
  TaxRegistrationMemberDto,
  TaxRegistrationMemberRequest,
  TaxRegistrationRequest,
  TaxResultContract,
  TaxRuleDto,
  TaxRuleRequest,
  TaxRuleSetDto,
  TaxRuleSetRequest
} from "../types.ts";

const basePath = "/api/v1/tax";

function opts(legalEntityId?: string, extra?: RequestOptions): RequestOptions | undefined {
  if (!legalEntityId && !extra) return extra;
  const res: RequestOptions = extra ? { ...extra } : {};
  if (legalEntityId) {
    return { ...res, legalEntityId };
  }
  return res;
}

export const taxApi = {
  // Capability
  getCapability: (legalEntityId: string, at: string, options?: RequestOptions) =>
    httpClient<TaxCapabilityDto>(
      `${basePath}/legal-entities/${encodeURIComponent(legalEntityId)}/capability?at=${encodeURIComponent(at)}`,
      opts(legalEntityId, options)
    ),

  setCapability: (legalEntityId: string, request: SetTaxCapabilityRequest, options?: RequestOptions) =>
    httpClient<TaxCapabilityDto>(
      `${basePath}/legal-entities/${encodeURIComponent(legalEntityId)}/capability`,
      opts(legalEntityId, {
        ...options,
        method: "PUT",
        body: JSON.stringify(request)
      })
    ),

  // Coverage
  getCoverage: (
    params: {
      legalEntityId: string;
      at: string;
      countryCode: string;
      schemeCode?: string;
      productTemplateId?: string;
      productVariantId?: string;
      partyId?: string;
      direction?: string;
      documentKind?: string;
      transactionType?: string;
      jurisdictionCode?: string;
    },
    options?: RequestOptions
  ) => {
    const q = new URLSearchParams();
    q.set("legalEntityId", params.legalEntityId);
    q.set("at", params.at);
    q.set("countryCode", params.countryCode);
    if (params.schemeCode) q.set("schemeCode", params.schemeCode);
    if (params.productTemplateId) q.set("productTemplateId", params.productTemplateId);
    if (params.productVariantId) q.set("productVariantId", params.productVariantId);
    if (params.partyId) q.set("partyId", params.partyId);
    if (params.direction) q.set("direction", params.direction);
    if (params.documentKind) q.set("documentKind", params.documentKind);
    if (params.transactionType) q.set("transactionType", params.transactionType);
    if (params.jurisdictionCode) q.set("jurisdictionCode", params.jurisdictionCode);

    return httpClient<TaxCoverageResult>(
      `${basePath}/coverage?${q.toString()}`,
      opts(params.legalEntityId, options)
    );
  },

  // Registrations
  listRegistrations: (legalEntityId: string, schemeCode?: string, options?: RequestOptions) => {
    const q = new URLSearchParams({ legalEntityId });
    if (schemeCode) q.set("schemeCode", schemeCode);
    return httpClient<readonly TaxRegistrationDto[]>(
      `${basePath}/registrations?${q.toString()}`,
      opts(legalEntityId, options)
    );
  },

  getRegistration: (id: string, legalEntityId: string, options?: RequestOptions) =>
    httpClient<TaxRegistrationDto>(
      `${basePath}/registrations/${encodeURIComponent(id)}?legalEntityId=${encodeURIComponent(legalEntityId)}`,
      opts(legalEntityId, options)
    ),

  getRegistrationHistory: (id: string, legalEntityId: string, options?: RequestOptions) =>
    httpClient<readonly TaxRegistrationDto[]>(
      `${basePath}/registrations/${encodeURIComponent(id)}/history?legalEntityId=${encodeURIComponent(legalEntityId)}`,
      opts(legalEntityId, options)
    ),

  createRegistration: (request: TaxRegistrationRequest, options?: RequestOptions) =>
    httpClient<TaxRegistrationDto>(
      `${basePath}/registrations`,
      opts(request.legalEntityId, {
        ...options,
        method: "POST",
        body: JSON.stringify(request)
      })
    ),

  updateRegistration: (id: string, legalEntityId: string, request: TaxRegistrationChangeRequest, options?: RequestOptions) =>
    httpClient<TaxRegistrationDto>(
      `${basePath}/registrations/${encodeURIComponent(id)}?legalEntityId=${encodeURIComponent(legalEntityId)}`,
      opts(legalEntityId, {
        ...options,
        method: "PATCH",
        body: JSON.stringify(request)
      })
    ),

  deregister: (id: string, legalEntityId: string, request: TaxRegistrationChangeRequest, options?: RequestOptions) =>
    httpClient<TaxRegistrationDto>(
      `${basePath}/registrations/${encodeURIComponent(id)}/deregister?legalEntityId=${encodeURIComponent(legalEntityId)}`,
      opts(legalEntityId, {
        ...options,
        method: "POST",
        body: JSON.stringify(request)
      })
    ),

  // Registration Membership
  listRegistrationMembers: (id: string, legalEntityId: string, at?: string, options?: RequestOptions) => {
    const q = new URLSearchParams({ legalEntityId });
    if (at) q.set("at", at);
    return httpClient<readonly TaxRegistrationMemberDto[]>(
      `${basePath}/registrations/${encodeURIComponent(id)}/members?${q.toString()}`,
      opts(legalEntityId, options)
    );
  },

  addRegistrationMember: (id: string, request: TaxRegistrationMemberRequest, options?: RequestOptions) =>
    httpClient<TaxRegistrationMemberDto>(
      `${basePath}/registrations/${encodeURIComponent(id)}/members`,
      opts(request.legalEntityId, {
        ...options,
        method: "POST",
        body: JSON.stringify(request)
      })
    ),

  endRegistrationMember: (id: string, legalEntityId: string, effectiveTo: string, options?: RequestOptions) =>
    httpClient<TaxRegistrationMemberDto>(
      `${basePath}/registration-members/${encodeURIComponent(id)}/end?legalEntityId=${encodeURIComponent(legalEntityId)}&effectiveTo=${encodeURIComponent(effectiveTo)}`,
      opts(legalEntityId, {
        ...options,
        method: "POST"
      })
    ),

  // Product Profiles
  listProductProfiles: (
    params: {
      legalEntityId: string;
      productTemplateId?: string;
      productVariantId?: string;
      at?: string;
    },
    options?: RequestOptions
  ) => {
    const q = new URLSearchParams({ legalEntityId: params.legalEntityId });
    if (params.productTemplateId) q.set("productTemplateId", params.productTemplateId);
    if (params.productVariantId) q.set("productVariantId", params.productVariantId);
    if (params.at) q.set("at", params.at);
    return httpClient<readonly TaxProfileDto[]>(
      `${basePath}/product-profiles?${q.toString()}`,
      opts(params.legalEntityId, options)
    );
  },

  getProductProfileHistory: (id: string, legalEntityId: string, options?: RequestOptions) =>
    httpClient<readonly TaxProfileDto[]>(
      `${basePath}/product-profiles/${encodeURIComponent(id)}/history?legalEntityId=${encodeURIComponent(legalEntityId)}`,
      opts(legalEntityId, options)
    ),

  createProductProfile: (request: TaxProductProfileRequest, options?: RequestOptions) =>
    httpClient<TaxProfileDto>(
      `${basePath}/product-profiles`,
      opts(request.legalEntityId ?? undefined, {
        ...options,
        method: "POST",
        body: JSON.stringify(request)
      })
    ),

  updateProductProfile: (id: string, request: TaxProductProfileRequest, options?: RequestOptions) =>
    httpClient<TaxProfileDto>(
      `${basePath}/product-profiles/${encodeURIComponent(id)}`,
      opts(request.legalEntityId ?? undefined, {
        ...options,
        method: "PUT",
        body: JSON.stringify(request)
      })
    ),

  endProductProfile: (id: string, legalEntityId: string, effectiveTo: string, options?: RequestOptions) =>
    httpClient<TaxProfileDto>(
      `${basePath}/product-profiles/${encodeURIComponent(id)}/end?legalEntityId=${encodeURIComponent(legalEntityId)}&effectiveTo=${encodeURIComponent(effectiveTo)}`,
      opts(legalEntityId, {
        ...options,
        method: "POST"
      })
    ),

  // Party Profiles
  listPartyProfiles: (
    params: {
      legalEntityId: string;
      partyId?: string;
      at?: string;
    },
    options?: RequestOptions
  ) => {
    const q = new URLSearchParams({ legalEntityId: params.legalEntityId });
    if (params.partyId) q.set("partyId", params.partyId);
    if (params.at) q.set("at", params.at);
    return httpClient<readonly TaxProfileDto[]>(
      `${basePath}/party-profiles?${q.toString()}`,
      opts(params.legalEntityId, options)
    );
  },

  getPartyProfileHistory: (id: string, legalEntityId: string, options?: RequestOptions) =>
    httpClient<readonly TaxProfileDto[]>(
      `${basePath}/party-profiles/${encodeURIComponent(id)}/history?legalEntityId=${encodeURIComponent(legalEntityId)}`,
      opts(legalEntityId, options)
    ),

  createPartyProfile: (request: TaxPartyProfileRequest, options?: RequestOptions) =>
    httpClient<TaxProfileDto>(
      `${basePath}/party-profiles`,
      opts(request.legalEntityId ?? undefined, {
        ...options,
        method: "POST",
        body: JSON.stringify(request)
      })
    ),

  updatePartyProfile: (id: string, request: TaxPartyProfileRequest, options?: RequestOptions) =>
    httpClient<TaxProfileDto>(
      `${basePath}/party-profiles/${encodeURIComponent(id)}`,
      opts(request.legalEntityId ?? undefined, {
        ...options,
        method: "PUT",
        body: JSON.stringify(request)
      })
    ),

  endPartyProfile: (id: string, legalEntityId: string, effectiveTo: string, options?: RequestOptions) =>
    httpClient<TaxProfileDto>(
      `${basePath}/party-profiles/${encodeURIComponent(id)}/end?legalEntityId=${encodeURIComponent(legalEntityId)}&effectiveTo=${encodeURIComponent(effectiveTo)}`,
      opts(legalEntityId, {
        ...options,
        method: "POST"
      })
    ),

  // Reference Data
  getReferenceData: (legalEntityId: string, options?: RequestOptions) =>
    httpClient<TaxReferenceDataDto>(
      `${basePath}/reference-data?legalEntityId=${encodeURIComponent(legalEntityId)}`,
      opts(legalEntityId, options)
    ),

  // Rule Sets & Rules
  listRuleSets: (legalEntityId: string, options?: RequestOptions) =>
    httpClient<readonly TaxRuleSetDto[]>(
      `${basePath}/rule-sets?legalEntityId=${encodeURIComponent(legalEntityId)}`,
      opts(legalEntityId, options)
    ),

  getRuleSet: (id: string, legalEntityId: string, options?: RequestOptions) =>
    httpClient<TaxRuleSetDto>(
      `${basePath}/rule-sets/${encodeURIComponent(id)}?legalEntityId=${encodeURIComponent(legalEntityId)}`,
      opts(legalEntityId, options)
    ),

  createRuleSet: (request: TaxRuleSetRequest, options?: RequestOptions) =>
    httpClient<TaxRuleSetDto>(
      `${basePath}/rule-sets`,
      opts(request.legalEntityId ?? undefined, {
        ...options,
        method: "POST",
        body: JSON.stringify(request)
      })
    ),

  listRules: (ruleSetId: string, legalEntityId: string, options?: RequestOptions) =>
    httpClient<readonly TaxRuleDto[]>(
      `${basePath}/rule-sets/${encodeURIComponent(ruleSetId)}/rules?legalEntityId=${encodeURIComponent(legalEntityId)}`,
      opts(legalEntityId, options)
    ),

  addRule: (ruleSetId: string, legalEntityId: string, request: TaxRuleRequest, options?: RequestOptions) =>
    httpClient<TaxRuleDto>(
      `${basePath}/rule-sets/${encodeURIComponent(ruleSetId)}/rules?legalEntityId=${encodeURIComponent(legalEntityId)}`,
      opts(legalEntityId, {
        ...options,
        method: "POST",
        body: JSON.stringify(request)
      })
    ),

  updateRule: (ruleId: string, legalEntityId: string, request: TaxRuleRequest, options?: RequestOptions) =>
    httpClient<TaxRuleDto>(
      `${basePath}/rules/${encodeURIComponent(ruleId)}?legalEntityId=${encodeURIComponent(legalEntityId)}`,
      opts(legalEntityId, {
        ...options,
        method: "PUT",
        body: JSON.stringify(request)
      })
    ),

  validateRuleSet: (ruleSetId: string, legalEntityId: string, options?: RequestOptions) =>
    httpClient<{ validated: boolean; id: string }>(
      `${basePath}/rule-sets/${encodeURIComponent(ruleSetId)}/validate?legalEntityId=${encodeURIComponent(legalEntityId)}`,
      opts(legalEntityId, {
        ...options,
        method: "POST"
      })
    ),

  scheduleRuleSet: (ruleSetId: string, legalEntityId: string, options?: RequestOptions) =>
    httpClient<TaxRuleSetDto>(
      `${basePath}/rule-sets/${encodeURIComponent(ruleSetId)}/schedule?legalEntityId=${encodeURIComponent(legalEntityId)}`,
      opts(legalEntityId, {
        ...options,
        method: "POST"
      })
    ),

  approveRuleSet: (ruleSetId: string, legalEntityId: string, options?: RequestOptions) =>
    httpClient<TaxRuleSetDto>(
      `${basePath}/rule-sets/${encodeURIComponent(ruleSetId)}/approve?legalEntityId=${encodeURIComponent(legalEntityId)}`,
      opts(legalEntityId, {
        ...options,
        method: "POST"
      })
    ),

  activateRuleSet: (ruleSetId: string, legalEntityId: string, options?: RequestOptions) =>
    httpClient<TaxRuleSetDto>(
      `${basePath}/rule-sets/${encodeURIComponent(ruleSetId)}/activate?legalEntityId=${encodeURIComponent(legalEntityId)}`,
      opts(legalEntityId, {
        ...options,
        method: "POST"
      })
    ),

  supersedeRuleSet: (ruleSetId: string, legalEntityId: string, options?: RequestOptions) =>
    httpClient<TaxRuleSetDto>(
      `${basePath}/rule-sets/${encodeURIComponent(ruleSetId)}/supersede?legalEntityId=${encodeURIComponent(legalEntityId)}`,
      opts(legalEntityId, {
        ...options,
        method: "POST"
      })
    ),

  retireRuleSet: (ruleSetId: string, legalEntityId: string, options?: RequestOptions) =>
    httpClient<TaxRuleSetDto>(
      `${basePath}/rule-sets/${encodeURIComponent(ruleSetId)}/retire?legalEntityId=${encodeURIComponent(legalEntityId)}`,
      opts(legalEntityId, {
        ...options,
        method: "POST"
      })
    ),

  // Country Packs
  listCountryPacks: (options?: RequestOptions) =>
    httpClient<readonly TaxCountryPackDto[]>(
      `${basePath}/country-packs`,
      options
    ),

  getCountryPack: (id: string, options?: RequestOptions) =>
    httpClient<TaxCountryPackDto>(
      `${basePath}/country-packs/${encodeURIComponent(id)}`,
      options
    ),

  installCountryPack: (request: InstallTaxCountryPackRequest, options?: RequestOptions) =>
    httpClient<TaxCountryPackDto>(
      `${basePath}/country-packs`,
      {
        ...options,
        method: "POST",
        body: JSON.stringify(request)
      }
    ),

  validateCountryPack: (id: string, options?: RequestOptions) =>
    httpClient<TaxCountryPackValidationDto>(
      `${basePath}/country-packs/${encodeURIComponent(id)}/validate`,
      {
        ...options,
        method: "POST"
      }
    ),

  scheduleCountryPack: (id: string, request: TaxCountryPackActionRequest, options?: RequestOptions) =>
    httpClient<TaxCountryPackDto>(
      `${basePath}/country-packs/${encodeURIComponent(id)}/schedule`,
      {
        ...options,
        method: "POST",
        body: JSON.stringify(request)
      }
    ),

  activateCountryPack: (id: string, request: TaxCountryPackActionRequest, options?: RequestOptions) =>
    httpClient<TaxCountryPackDto>(
      `${basePath}/country-packs/${encodeURIComponent(id)}/activate`,
      {
        ...options,
        method: "POST",
        body: JSON.stringify(request)
      }
    ),

  retireCountryPack: (id: string, request: TaxCountryPackActionRequest, options?: RequestOptions) =>
    httpClient<TaxCountryPackDto>(
      `${basePath}/country-packs/${encodeURIComponent(id)}/retire`,
      {
        ...options,
        method: "POST",
        body: JSON.stringify(request)
      }
    ),

  rollbackCountryPackForFuture: (id: string, request: TaxCountryPackActionRequest, options?: RequestOptions) =>
    httpClient<TaxCountryPackDto>(
      `${basePath}/country-packs/${encodeURIComponent(id)}/rollback-for-future`,
      {
        ...options,
        method: "POST",
        body: JSON.stringify(request)
      }
    ),

  // Draft Impact
  getDraftImpact: (request: TaxDraftImpactRequest, options?: RequestOptions) =>
    httpClient<TaxDraftImpactResult>(
      `${basePath}/draft-impact`,
      opts(request.legalEntityId, {
        ...options,
        method: "POST",
        body: JSON.stringify(request)
      })
    ),

  // Calculation / Simulation
  calculate: (request: TaxCalculationRequest, options?: RequestOptions) =>
    httpClient<TaxResultContract>(
      `${basePath}/calculate`,
      opts(request.legalEntityId, {
        ...options,
        method: "POST",
        body: JSON.stringify(request)
      })
    ),

  // Import / Export
  previewProductImport: (request: TaxProfileImportPreviewRequest, options?: RequestOptions) =>
    httpClient<TaxProfileImportPreviewDto>(
      `${basePath}/imports/product-profiles/preview`,
      opts(request.legalEntityId, {
        ...options,
        method: "POST",
        body: JSON.stringify(request)
      })
    ),

  previewPartyImport: (request: TaxProfileImportPreviewRequest, options?: RequestOptions) =>
    httpClient<TaxProfileImportPreviewDto>(
      `${basePath}/imports/party-profiles/preview`,
      opts(request.legalEntityId, {
        ...options,
        method: "POST",
        body: JSON.stringify(request)
      })
    ),

  commitImport: (batchId: string, legalEntityId: string, options?: RequestOptions) =>
    httpClient<TaxProfileImportCommitDto>(
      `${basePath}/imports/${encodeURIComponent(batchId)}/commit?legalEntityId=${encodeURIComponent(legalEntityId)}`,
      opts(legalEntityId, {
        ...options,
        method: "POST"
      })
    ),

  exportProfiles: (kind: "Product" | "Party", legalEntityId: string, at?: string, options?: RequestOptions) => {
    const q = new URLSearchParams({ legalEntityId });
    if (at) q.set("at", at);
    return httpClient<TaxProfileExportDto>(
      `${basePath}/exports/${encodeURIComponent(kind)}?${q.toString()}`,
      opts(legalEntityId, options)
    );
  }
};

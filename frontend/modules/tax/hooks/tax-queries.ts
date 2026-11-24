import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useOptionalPermissions, useLegalEntityContext } from "../../../core/runtime/runtime-provider.tsx";
import { taxApi } from "../services/tax-api.ts";
import type {
  InstallTaxCountryPackRequest,
  SetTaxCapabilityRequest,
  TaxCalculationRequest,
  TaxCountryPackActionRequest,
  TaxCountryPackDto,
  TaxCountryPackValidationDto,
  TaxDraftImpactRequest,
  TaxPartyProfileRequest,
  TaxProductProfileRequest,
  TaxProfileImportPreviewRequest,
  TaxRegistrationChangeRequest,
  TaxRegistrationMemberRequest,
  TaxRegistrationRequest,
  TaxRuleRequest,
  TaxRuleSetRequest
} from "../types.ts";

export const taxQueryKeys = {
  all: ["tax"] as const,
  capability: (legalEntityId: string, at: string) => ["tax", "capability", legalEntityId, at] as const,
  coverage: (legalEntityId: string, at: string, countryCode: string, extra?: Record<string, unknown>) =>
    ["tax", "coverage", legalEntityId, at, countryCode, extra ?? {}] as const,
  registrations: (legalEntityId: string, schemeCode?: string) =>
    ["tax", "registrations", legalEntityId, schemeCode ?? "all"] as const,
  registration: (id: string, legalEntityId: string) =>
    ["tax", "registration", id, legalEntityId] as const,
  registrationHistory: (id: string, legalEntityId: string) =>
    ["tax", "registration-history", id, legalEntityId] as const,
  registrationMembers: (id: string, legalEntityId: string, at?: string) =>
    ["tax", "registration-members", id, legalEntityId, at ?? "current"] as const,
  productProfiles: (legalEntityId: string, templateId?: string, variantId?: string, at?: string) =>
    ["tax", "product-profiles", legalEntityId, templateId ?? "all", variantId ?? "all", at ?? "current"] as const,
  productProfileHistory: (id: string, legalEntityId: string) =>
    ["tax", "product-profile-history", id, legalEntityId] as const,
  partyProfiles: (legalEntityId: string, partyId?: string, at?: string) =>
    ["tax", "party-profiles", legalEntityId, partyId ?? "all", at ?? "current"] as const,
  partyProfileHistory: (id: string, legalEntityId: string) =>
    ["tax", "party-profile-history", id, legalEntityId] as const,
  referenceData: (legalEntityId: string) =>
    ["tax", "reference-data", legalEntityId] as const,
  ruleSets: (legalEntityId: string) =>
    ["tax", "rule-sets", legalEntityId] as const,
  ruleSet: (id: string, legalEntityId: string) =>
    ["tax", "rule-set", id, legalEntityId] as const,
  rules: (ruleSetId: string, legalEntityId: string) =>
    ["tax", "rules", ruleSetId, legalEntityId] as const,
  countryPacks: () => ["tax", "country-packs"] as const,
  countryPack: (id: string) => ["tax", "country-pack", id] as const
};

export function useTaxPermissions() {
  const permissions = useOptionalPermissions();
  return {
    canRead: Boolean(permissions?.hasPermission("tax.read")),
    canConfigureCapability: Boolean(permissions?.hasPermission("tax.capability.configure")),
    canReadRegistrations: Boolean(permissions?.hasPermission("tax.registration.read")),
    canManageRegistrations: Boolean(permissions?.hasPermission("tax.registration.manage")),
    canReadPartyProfiles: Boolean(permissions?.hasPermission("tax.party_profile.read")),
    canManagePartyProfiles: Boolean(permissions?.hasPermission("tax.party_profile.manage")),
    canReadProductProfiles: Boolean(permissions?.hasPermission("tax.product_profile.read")),
    canManageProductProfiles: Boolean(permissions?.hasPermission("tax.product_profile.manage")),
    canReadPolicies: Boolean(permissions?.hasPermission("tax.policy.read")),
    canManagePolicies: Boolean(permissions?.hasPermission("tax.policy.manage")),
    canReadRules: Boolean(permissions?.hasPermission("tax.rule.read")),
    canManageRules: Boolean(permissions?.hasPermission("tax.rule.manage")),
    canActivateRules: Boolean(permissions?.hasPermission("tax.rule.activate")),
    canSimulate: Boolean(permissions?.hasPermission("tax.simulate")),
    canViewAudit: Boolean(permissions?.hasPermission("tax.audit.view")),
    canImport: Boolean(permissions?.hasPermission("tax.import")),
    canExport: Boolean(permissions?.hasPermission("tax.export")),
    canReadCountryPacks: Boolean(permissions?.hasPermission("tax.country_pack.read")),
    canManageCountryPacks: Boolean(permissions?.hasPermission("tax.country_pack.manage")),
    canActivateCountryPacks: Boolean(permissions?.hasPermission("tax.country_pack.activate"))
  };
}

export function useTaxScope(props?: { legalEntityId?: string | undefined }) {
  let contextEntityId: string | undefined;
  let contextCountryCode: string | undefined;
  try {
    const entity = useLegalEntityContext();
    contextEntityId = entity?.legalEntityId;
    contextCountryCode = entity?.countryCode;
  } catch {
    // Rendered outside RuntimeProvider
  }

  const legalEntityId = props?.legalEntityId || contextEntityId || "";
  const countryCode = contextCountryCode || "EG";
  const now = new Date();
  const today = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, "0")}-${String(now.getDate()).padStart(2, "0")}`;

  return { legalEntityId, countryCode, today };
}

export function useTaxCapability(legalEntityId: string, at: string) {
  return useQuery({
    queryKey: taxQueryKeys.capability(legalEntityId, at),
    queryFn: () => taxApi.getCapability(legalEntityId, at),
    enabled: Boolean(legalEntityId && at),
    staleTime: 60_000
  });
}

export function useTaxCoverage(params: {
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
}) {
  return useQuery({
    queryKey: taxQueryKeys.coverage(params.legalEntityId, params.at, params.countryCode, {
      schemeCode: params.schemeCode,
      productTemplateId: params.productTemplateId,
      productVariantId: params.productVariantId,
      partyId: params.partyId,
      direction: params.direction,
      documentKind: params.documentKind,
      transactionType: params.transactionType,
      jurisdictionCode: params.jurisdictionCode
    }),
    queryFn: () => taxApi.getCoverage(params),
    enabled: Boolean(params.legalEntityId && params.at && params.countryCode),
    staleTime: 30_000
  });
}

export function useTaxRegistrations(legalEntityId: string, schemeCode?: string) {
  return useQuery({
    queryKey: taxQueryKeys.registrations(legalEntityId, schemeCode),
    queryFn: () => taxApi.listRegistrations(legalEntityId, schemeCode),
    enabled: Boolean(legalEntityId),
    staleTime: 30_000
  });
}

export function useTaxRegistration(id: string, legalEntityId: string) {
  return useQuery({
    queryKey: taxQueryKeys.registration(id, legalEntityId),
    queryFn: () => taxApi.getRegistration(id, legalEntityId),
    enabled: Boolean(id && legalEntityId)
  });
}

export function useTaxRegistrationHistory(id: string, legalEntityId: string) {
  return useQuery({
    queryKey: taxQueryKeys.registrationHistory(id, legalEntityId),
    queryFn: () => taxApi.getRegistrationHistory(id, legalEntityId),
    enabled: Boolean(id && legalEntityId)
  });
}

export function useTaxRegistrationMembers(id: string, legalEntityId: string, at?: string) {
  return useQuery({
    queryKey: taxQueryKeys.registrationMembers(id, legalEntityId, at),
    queryFn: () => taxApi.listRegistrationMembers(id, legalEntityId, at),
    enabled: Boolean(id && legalEntityId)
  });
}

export function useTaxProductProfiles(params: {
  legalEntityId: string;
  productTemplateId?: string;
  productVariantId?: string;
  at?: string;
}) {
  return useQuery({
    queryKey: taxQueryKeys.productProfiles(params.legalEntityId, params.productTemplateId, params.productVariantId, params.at),
    queryFn: () => taxApi.listProductProfiles(params),
    enabled: Boolean(params.legalEntityId),
    staleTime: 30_000
  });
}

export function useTaxProductProfileHistory(id: string, legalEntityId: string) {
  return useQuery({
    queryKey: taxQueryKeys.productProfileHistory(id, legalEntityId),
    queryFn: () => taxApi.getProductProfileHistory(id, legalEntityId),
    enabled: Boolean(id && legalEntityId)
  });
}

export function useTaxPartyProfiles(params: {
  legalEntityId: string;
  partyId?: string;
  at?: string;
}) {
  return useQuery({
    queryKey: taxQueryKeys.partyProfiles(params.legalEntityId, params.partyId, params.at),
    queryFn: () => taxApi.listPartyProfiles(params),
    enabled: Boolean(params.legalEntityId),
    staleTime: 30_000
  });
}

export function useTaxPartyProfileHistory(id: string, legalEntityId: string) {
  return useQuery({
    queryKey: taxQueryKeys.partyProfileHistory(id, legalEntityId),
    queryFn: () => taxApi.getPartyProfileHistory(id, legalEntityId),
    enabled: Boolean(id && legalEntityId)
  });
}

export function useTaxReferenceData(legalEntityId: string) {
  return useQuery({
    queryKey: taxQueryKeys.referenceData(legalEntityId),
    queryFn: () => taxApi.getReferenceData(legalEntityId),
    enabled: Boolean(legalEntityId),
    staleTime: 120_000
  });
}

export function useTaxRuleSets(legalEntityId: string) {
  return useQuery({
    queryKey: taxQueryKeys.ruleSets(legalEntityId),
    queryFn: () => taxApi.listRuleSets(legalEntityId),
    enabled: Boolean(legalEntityId),
    staleTime: 30_000
  });
}

export function useTaxRuleSet(id: string, legalEntityId: string) {
  return useQuery({
    queryKey: taxQueryKeys.ruleSet(id, legalEntityId),
    queryFn: () => taxApi.getRuleSet(id, legalEntityId),
    enabled: Boolean(id && legalEntityId)
  });
}

export function useTaxRules(ruleSetId: string, legalEntityId: string) {
  return useQuery({
    queryKey: taxQueryKeys.rules(ruleSetId, legalEntityId),
    queryFn: () => taxApi.listRules(ruleSetId, legalEntityId),
    enabled: Boolean(ruleSetId && legalEntityId),
    staleTime: 30_000
  });
}

export function useTaxCountryPacks() {
  return useQuery({
    queryKey: taxQueryKeys.countryPacks(),
    queryFn: () => taxApi.listCountryPacks(),
    staleTime: 60_000
  });
}

export function useTaxCountryPack(id: string) {
  return useQuery({
    queryKey: taxQueryKeys.countryPack(id),
    queryFn: () => taxApi.getCountryPack(id),
    enabled: Boolean(id)
  });
}

// Mutations
export function useSetTaxCapabilityMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ legalEntityId, request }: { legalEntityId: string; request: SetTaxCapabilityRequest }) =>
      taxApi.setCapability(legalEntityId, request),
    onSuccess: (_, vars) => {
      queryClient.invalidateQueries({ queryKey: ["tax", "capability", vars.legalEntityId] });
      queryClient.invalidateQueries({ queryKey: ["tax", "coverage", vars.legalEntityId] });
    }
  });
}

export function useCreateTaxRegistrationMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: TaxRegistrationRequest) => taxApi.createRegistration(request),
    onSuccess: (_, vars) => {
      queryClient.invalidateQueries({ queryKey: ["tax", "registrations", vars.legalEntityId] });
      queryClient.invalidateQueries({ queryKey: ["tax", "coverage", vars.legalEntityId] });
    }
  });
}

export function useUpdateTaxRegistrationMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, legalEntityId, request }: { id: string; legalEntityId: string; request: TaxRegistrationChangeRequest }) =>
      taxApi.updateRegistration(id, legalEntityId, request),
    onSuccess: (_, vars) => {
      queryClient.invalidateQueries({ queryKey: ["tax", "registrations", vars.legalEntityId] });
      queryClient.invalidateQueries({ queryKey: ["tax", "registration", vars.id, vars.legalEntityId] });
      queryClient.invalidateQueries({ queryKey: ["tax", "registration-history", vars.id, vars.legalEntityId] });
      queryClient.invalidateQueries({ queryKey: ["tax", "coverage", vars.legalEntityId] });
    }
  });
}

export function useDeregisterMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, legalEntityId, request }: { id: string; legalEntityId: string; request: TaxRegistrationChangeRequest }) =>
      taxApi.deregister(id, legalEntityId, request),
    onSuccess: (_, vars) => {
      queryClient.invalidateQueries({ queryKey: ["tax", "registrations", vars.legalEntityId] });
      queryClient.invalidateQueries({ queryKey: ["tax", "registration", vars.id, vars.legalEntityId] });
      queryClient.invalidateQueries({ queryKey: ["tax", "registration-history", vars.id, vars.legalEntityId] });
      queryClient.invalidateQueries({ queryKey: ["tax", "coverage", vars.legalEntityId] });
    }
  });
}

export function useSaveProductProfileMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, request }: { id?: string | undefined; request: TaxProductProfileRequest }) =>
      id ? taxApi.updateProductProfile(id, request) : taxApi.createProductProfile(request),
    onSuccess: (_, vars) => {
      const entityId = vars.request.legalEntityId ?? "";
      queryClient.invalidateQueries({ queryKey: ["tax", "product-profiles", entityId] });
      queryClient.invalidateQueries({ queryKey: ["tax", "coverage", entityId] });
      if (vars.id) {
        queryClient.invalidateQueries({ queryKey: ["tax", "product-profile-history", vars.id, entityId] });
      }
    }
  });
}

export function useSavePartyProfileMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, request }: { id?: string | undefined; request: TaxPartyProfileRequest }) =>
      id ? taxApi.updatePartyProfile(id, request) : taxApi.createPartyProfile(request),
    onSuccess: (_, vars) => {
      const entityId = vars.request.legalEntityId ?? "";
      queryClient.invalidateQueries({ queryKey: ["tax", "party-profiles", entityId] });
      queryClient.invalidateQueries({ queryKey: ["tax", "coverage", entityId] });
      if (vars.id) {
        queryClient.invalidateQueries({ queryKey: ["tax", "party-profile-history", vars.id, entityId] });
      }
    }
  });
}

export function useCreateRuleSetMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: TaxRuleSetRequest) => taxApi.createRuleSet(request),
    onSuccess: (_, vars) => {
      const entityId = vars.legalEntityId ?? "";
      queryClient.invalidateQueries({ queryKey: ["tax", "rule-sets", entityId] });
      queryClient.invalidateQueries({ queryKey: ["tax", "coverage", entityId] });
    }
  });
}

export function useTransitionRuleSetMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({
      ruleSetId,
      legalEntityId,
      transition
    }: {
      ruleSetId: string;
      legalEntityId: string;
      transition: "schedule" | "approve" | "activate" | "supersede" | "retire";
    }) => {
      switch (transition) {
        case "schedule":
          return taxApi.scheduleRuleSet(ruleSetId, legalEntityId);
        case "approve":
          return taxApi.approveRuleSet(ruleSetId, legalEntityId);
        case "activate":
          return taxApi.activateRuleSet(ruleSetId, legalEntityId);
        case "supersede":
          return taxApi.supersedeRuleSet(ruleSetId, legalEntityId);
        case "retire":
          return taxApi.retireRuleSet(ruleSetId, legalEntityId);
      }
    },
    onSuccess: (_, vars) => {
      queryClient.invalidateQueries({ queryKey: ["tax", "rule-sets", vars.legalEntityId] });
      queryClient.invalidateQueries({ queryKey: ["tax", "rule-set", vars.ruleSetId, vars.legalEntityId] });
      queryClient.invalidateQueries({ queryKey: ["tax", "coverage", vars.legalEntityId] });
    }
  });
}

export function useCountryPackActionMutation() {
  const queryClient = useQueryClient();
  return useMutation<
    TaxCountryPackDto | TaxCountryPackValidationDto,
    Error,
    {
      action: "validate" | "schedule" | "activate" | "retire" | "rollback";
      id: string;
      request?: TaxCountryPackActionRequest;
    }
  >({
    mutationFn: ({
      action,
      id,
      request
    }) => {
      switch (action) {
        case "validate":
          return taxApi.validateCountryPack(id);
        case "schedule":
          return taxApi.scheduleCountryPack(id, request ?? {});
        case "activate":
          return taxApi.activateCountryPack(id, request ?? {});
        case "retire":
          return taxApi.retireCountryPack(id, request ?? {});
        case "rollback":
          return taxApi.rollbackCountryPackForFuture(id, request ?? {});
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: taxQueryKeys.countryPacks() });
      queryClient.invalidateQueries({ queryKey: ["tax", "coverage"] });
    }
  });
}

import React, { createContext, useContext, useMemo, useState, useCallback, useEffect, type ReactNode } from "react";
import type { RuntimeContext, BrandProfile, ResolvedTheme } from "./runtime-context.ts";
import type { TenantContext, UserContext, LocaleContext } from "./tenant-context.ts";
import type { LegalEntityContext, BranchContext } from "./entity-context.ts";
import type { UserPreferences } from "./preferences.ts";
import { CapabilitySet, type CapabilityState, type CapabilityDetail } from "./capabilities.ts";
import { PermissionSet } from "./permissions.ts";
import { changeLanguage, currentLanguage, i18n } from "../i18n/i18n.ts";

export interface RuntimeContextActions {
  readonly switchTenant: (tenant: TenantContext) => Promise<void>;
  readonly switchLegalEntity: (legalEntity: LegalEntityContext) => Promise<void>;
  readonly switchBranch: (branch: BranchContext | undefined) => Promise<void>;
  readonly updatePreferences: (prefs: Partial<UserPreferences>) => void;
  readonly updateCapabilities: (caps: Record<string, CapabilityState | CapabilityDetail>) => void;
  readonly updatePermissions: (perms: string[]) => void;
  readonly updateBrand: (brand: BrandProfile) => void;
  readonly updateTheme: (theme: ResolvedTheme) => void;
}

export interface QueryCacheManager {
  readonly clearAll: () => void;
  readonly invalidateEntityScope: (legalEntityId: string) => void;
}

export interface RuntimeContextValue {
  readonly context: RuntimeContext;
  readonly actions: RuntimeContextActions;
}

const RuntimeContextState = createContext<RuntimeContextValue | null>(null);

export interface RuntimeProviderProps {
  readonly initialContext: RuntimeContext;
  readonly queryCacheManager?: QueryCacheManager | undefined;
  readonly children: ReactNode;
}

export function RuntimeProvider({
  initialContext,
  queryCacheManager,
  children
}: RuntimeProviderProps): React.JSX.Element {
  const [tenant, setTenant] = useState<TenantContext>(initialContext.tenant);
  const [legalEntity, setLegalEntity] = useState<LegalEntityContext>(initialContext.legalEntity);
  const [branch, setBranch] = useState<BranchContext | undefined>(initialContext.branch);
  const [user] = useState<UserContext>(initialContext.user);
  const [capabilities, setCapabilities] = useState<CapabilitySet>(initialContext.capabilities);
  const [permissions, setPermissions] = useState<PermissionSet>(initialContext.permissions);
  const [preferences, setPreferences] = useState<UserPreferences>(() => ({ ...initialContext.preferences, language: currentLanguage() }));
  const [brand, setBrand] = useState<BrandProfile>(initialContext.brand);
  const [theme, setTheme] = useState<ResolvedTheme>(initialContext.theme);

  const locale = useMemo<LocaleContext>(() => ({
    language: preferences.language,
    direction: preferences.language === "ar" ? "rtl" : "ltr",
    numeralSystem: preferences.numeralSystem
  }), [preferences.language, preferences.numeralSystem]);

  useEffect(() => {
    const synchronizeLanguage = (language: string) => {
      setPreferences((current) => ({ ...current, language: language.startsWith("en") ? "en" : "ar" }));
    };
    i18n.on("languageChanged", synchronizeLanguage);
    return () => { i18n.off("languageChanged", synchronizeLanguage); };
  }, []);

  const switchTenant = useCallback(async (nextTenant: TenantContext) => {
    if (nextTenant.tenantId === tenant.tenantId) {
      return;
    }
    // Tenant context switch must invalidate and clear all cached server state
    queryCacheManager?.clearAll();
    setTenant(nextTenant);
    setBranch(undefined);
  }, [tenant.tenantId, queryCacheManager]);

  const switchLegalEntity = useCallback(async (nextEntity: LegalEntityContext) => {
    if (nextEntity.legalEntityId === legalEntity.legalEntityId) {
      return;
    }
    // Legal entity switch invalidates entity-scoped queries
    queryCacheManager?.invalidateEntityScope(legalEntity.legalEntityId);
    setLegalEntity(nextEntity);
    setBranch(undefined);
  }, [legalEntity.legalEntityId, queryCacheManager]);

  const switchBranch = useCallback(async (nextBranch: BranchContext | undefined) => {
    setBranch(nextBranch);
  }, []);

  const updatePreferences = useCallback((partial: Partial<UserPreferences>) => {
    const { language, ...nonLanguagePreferences } = partial;
    setPreferences(current => ({ ...current, ...nonLanguagePreferences }));
    if (language) void changeLanguage(language);
  }, []);

  const updateCapabilities = useCallback((caps: Record<string, CapabilityState | CapabilityDetail>) => {
    setCapabilities(new CapabilitySet(caps));
  }, []);

  const updatePermissions = useCallback((perms: string[]) => {
    setPermissions(new PermissionSet(perms));
  }, []);

  const updateBrand = useCallback((newBrand: BrandProfile) => {
    setBrand(newBrand);
  }, []);

  const updateTheme = useCallback((newTheme: ResolvedTheme) => {
    setTheme(newTheme);
  }, []);

  const runtimeContext = useMemo<RuntimeContext>(() => ({
    tenant,
    legalEntity,
    branch,
    user,
    locale,
    capabilities,
    permissions,
    preferences,
    brand,
    theme
  }), [tenant, legalEntity, branch, user, locale, capabilities, permissions, preferences, brand, theme]);

  const actions = useMemo<RuntimeContextActions>(() => ({
    switchTenant,
    switchLegalEntity,
    switchBranch,
    updatePreferences,
    updateCapabilities,
    updatePermissions,
    updateBrand,
    updateTheme
  }), [switchTenant, switchLegalEntity, switchBranch, updatePreferences, updateCapabilities, updatePermissions, updateBrand, updateTheme]);

  const value = useMemo<RuntimeContextValue>(() => ({
    context: runtimeContext,
    actions
  }), [runtimeContext, actions]);

  return (
    <RuntimeContextState.Provider value={value}>
      {children}
    </RuntimeContextState.Provider>
  );
}

export function useRuntime(): RuntimeContextValue {
  const ctx = useContext(RuntimeContextState);
  if (!ctx) {
    throw new Error("useRuntime must be used within a <RuntimeProvider>");
  }
  return ctx;
}

export function useRuntimeContext(): RuntimeContext {
  return useRuntime().context;
}

export function useTenantContext(): TenantContext {
  return useRuntimeContext().tenant;
}

export function useLegalEntityContext(): LegalEntityContext {
  return useRuntimeContext().legalEntity;
}

export function useBranchContext(): BranchContext | undefined {
  return useRuntimeContext().branch;
}

export function useCapabilities(): CapabilitySet {
  return useRuntimeContext().capabilities;
}

export function usePermissions(): PermissionSet {
  return useRuntimeContext().permissions;
}

/** Returns permissions when a screen is rendered outside the full runtime shell. */
export function useOptionalPermissions(): PermissionSet | undefined {
  return useContext(RuntimeContextState)?.context.permissions;
}

export function useUserPreferences(): UserPreferences {
  return useRuntimeContext().preferences;
}

export function useLocaleContext(): LocaleContext {
  return useRuntimeContext().locale;
}

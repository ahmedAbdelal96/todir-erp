import type { CapabilitySet } from "./capabilities.js";
import type { PermissionSet } from "./permissions.js";
import type { UserPreferences } from "./preferences.js";
import type { TenantContext, UserContext, LocaleContext } from "./tenant-context.js";
import type { LegalEntityContext, BranchContext } from "./entity-context.js";

export interface BrandProfile {
  readonly productDisplayName: string;
  readonly logoLightRef?: string | undefined;
  readonly logoDarkRef?: string | undefined;
  readonly faviconRef?: string | undefined;
  readonly primaryBrandColor?: string | undefined;
  readonly accentBrandColor?: string | undefined;
}

export interface ResolvedTheme {
  readonly presetKey: string;
  readonly appearance: "light" | "dark";
  readonly density: "comfortable" | "compact";
  readonly cssVariables: Readonly<Record<string, string>>;
}

export interface RuntimeContext {
  readonly tenant: TenantContext;
  readonly legalEntity: LegalEntityContext;
  readonly branch?: BranchContext | undefined;
  readonly user: UserContext;
  readonly locale: LocaleContext;
  readonly capabilities: CapabilitySet;
  readonly permissions: PermissionSet;
  readonly preferences: UserPreferences;
  readonly brand: BrandProfile;
  readonly theme: ResolvedTheme;
}

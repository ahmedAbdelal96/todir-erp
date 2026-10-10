import {
  themeDefinitionSchema,
  defaultThemeDefinition,
  type ThemeDefinition
} from "./theme-definition.ts";
import { deriveSemanticTokens } from "./color-derivation.ts";
import { compileDensityCssVariables } from "./density.ts";
import {
  referenceRadiiSharp,
  referenceRadiiSoft,
  referenceRadiiStandard,
  referenceTypography,
  referenceZIndex
} from "../tokens/reference-tokens.ts";
import type { ResolvedTheme } from "../../core/runtime/runtime-context.ts";
import type { DensityMode, AppearanceMode } from "../../core/runtime/preferences.ts";

export interface CompileThemeOptions {
  readonly density?: DensityMode | undefined;
  readonly appearance?: AppearanceMode | undefined;
}

export interface ThemeCompilationResult {
  readonly theme: ResolvedTheme;
  readonly isValid: boolean;
  readonly validationErrors?: string[] | undefined;
}

/**
 * Compiles a ThemeDefinition into a production-safe ResolvedTheme
 * strictly enforcing fallbacks on invalid data.
 */
export function compileTheme(
  input: unknown,
  options: CompileThemeOptions = {}
): ThemeCompilationResult {
  const parseResult = themeDefinitionSchema.safeParse(input);

  let themeDef: ThemeDefinition;
  let isValid = true;
  let validationErrors: string[] | undefined;

  if (parseResult.success) {
    themeDef = parseResult.data;
  } else {
    isValid = false;
    validationErrors = parseResult.error.issues.map(i => `${i.path.join(".")}: ${i.message}`);
    // Safe fallback to default theme definition
    themeDef = defaultThemeDefinition;
  }

  const density = options.density ?? themeDef.defaultDensity ?? "comfortable";
  const appearance = options.appearance === "dark" ? "dark" : "light";

  // Derive semantic tokens
  const semantic = deriveSemanticTokens(themeDef);

  // Select radius scale
  const radiusPreset = themeDef.shape?.radiusPreset ?? "standard";
  const radii = radiusPreset === "sharp"
    ? referenceRadiiSharp
    : radiusPreset === "soft"
      ? referenceRadiiSoft
      : referenceRadiiStandard;

  // Compile CSS Custom Properties
  const cssVariables: Record<string, string> = {
    // Surfaces
    "--color-surface-page": semantic.surface.page,
    "--color-surface-card": semantic.surface.card,
    "--color-surface-elevated": semantic.surface.elevated,
    "--color-surface-subtle": semantic.surface.subtle,
    "--color-surface-inset": semantic.surface.inset,
    "--color-surface-overlay": semantic.surface.overlay,

    // Content
    "--color-content-primary": semantic.content.primary,
    "--color-content-secondary": semantic.content.secondary,
    "--color-content-muted": semantic.content.muted,
    "--color-content-inverse": semantic.content.inverse,
    "--color-content-disabled": semantic.content.disabled,

    // Borders
    "--color-border-default": semantic.border.default,
    "--color-border-subtle": semantic.border.subtle,
    "--color-border-strong": semantic.border.strong,
    "--color-border-focus": semantic.border.focus,
    "--color-border-interactive": semantic.border.interactive,

    // Actions
    "--color-action-primary": semantic.action.primary,
    "--color-action-primary-hover": semantic.action.primaryHover,
    "--color-action-primary-active": semantic.action.primaryActive,
    "--color-action-primary-content": semantic.action.primaryContent,
    "--color-action-secondary": semantic.action.secondary,
    "--color-action-secondary-hover": semantic.action.secondaryHover,
    "--color-action-secondary-active": semantic.action.secondaryActive,
    "--color-action-secondary-content": semantic.action.secondaryContent,

    // Statuses (Semantically protected)
    "--color-status-success": semantic.status.success.base,
    "--color-status-success-subtle": semantic.status.success.subtle,
    "--color-status-success-content": semantic.status.success.content,
    "--color-status-success-border": semantic.status.success.border,

    "--color-status-warning": semantic.status.warning.base,
    "--color-status-warning-subtle": semantic.status.warning.subtle,
    "--color-status-warning-content": semantic.status.warning.content,
    "--color-status-warning-border": semantic.status.warning.border,

    "--color-status-danger": semantic.status.danger.base,
    "--color-status-danger-subtle": semantic.status.danger.subtle,
    "--color-status-danger-content": semantic.status.danger.content,
    "--color-status-danger-border": semantic.status.danger.border,

    "--color-status-info": semantic.status.info.base,
    "--color-status-info-subtle": semantic.status.info.subtle,
    "--color-status-info-content": semantic.status.info.content,
    "--color-status-info-border": semantic.status.info.border,

    // Radii
    "--radius-none": radii.none,
    "--radius-sm": radii.sm,
    "--radius-md": radii.md,
    "--radius-lg": radii.lg,
    "--radius-xl": radii.xl,
    "--radius-full": radii.full,

    // Typography
    "--font-sans": referenceTypography.fontFamily.sans,
    "--font-mono": referenceTypography.fontFamily.mono,

    // Z-Indices
    "--z-dropdown": referenceZIndex.dropdown,
    "--z-sticky": referenceZIndex.sticky,
    "--z-modal": referenceZIndex.modal,
    "--z-popover": referenceZIndex.popover,
    "--z-tooltip": referenceZIndex.tooltip,

    // Density variables
    ...compileDensityCssVariables(density)
  };

  const theme: ResolvedTheme = {
    presetKey: themeDef.preset,
    appearance,
    density,
    cssVariables
  };

  return {
    theme,
    isValid,
    validationErrors
  };
}

/**
 * Serializes CSS variables into a standard CSS block string
 */
export function cssVariablesToStyleString(variables: Readonly<Record<string, string>>): string {
  return Object.entries(variables)
    .map(([prop, val]) => `${prop}: ${val};`)
    .join("\n");
}

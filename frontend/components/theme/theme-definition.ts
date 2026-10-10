import { z } from "zod";

/**
 * Bounded ThemeDefinition Schema
 * Strictly typed and validated with Zod to prevent arbitrary CSS injection.
 */

// Validates 3 or 6 hex digits, or oklch(...) formatted color string
const colorStringSchema = z.string().trim().regex(
  /^(#[0-9a-fA-F]{3}|#[0-9a-fA-F]{6}|oklch\(\s*[\d.]+\s+[\d.]+\s+[\d.]+(\s*\/\s*[\d.]+)?\s*\))$/,
  { message: "Color must be a valid 3/6-digit hex string or oklch(...) format" }
);

export const themeDefinitionSchema = z.object({
  preset: z.string().min(1).max(50).default("default"),
  brand: z.object({
    primary: colorStringSchema,
    accent: colorStringSchema.optional(),
    neutralTone: z.enum(["slate", "stone", "zinc", "neutral"]).optional().default("slate")
  }),
  typography: z.object({
    familyKey: z.enum(["sans", "ibm-plex", "tajawal", "cairo"]).optional().default("sans")
  }).optional(),
  shape: z.object({
    radiusPreset: z.enum(["sharp", "standard", "soft"]).optional().default("standard")
  }).optional(),
  defaultDensity: z.enum(["comfortable", "compact"]).optional().default("comfortable")
}).strict(); // strict rejects unexpected/arbitrary CSS fields

export type ThemeDefinition = z.infer<typeof themeDefinitionSchema>;
export type ThemeDefinitionInput = z.input<typeof themeDefinitionSchema>;

export const defaultThemeDefinition: ThemeDefinition = {
  preset: "default",
  brand: {
    primary: "#4f46e5", // Indigo 600
    accent: "#06b6d4",  // Cyan 500
    neutralTone: "slate"
  },
  typography: {
    familyKey: "sans"
  },
  shape: {
    radiusPreset: "standard"
  },
  defaultDensity: "comfortable"
};

export const alternateThemeDefinition: ThemeDefinition = {
  preset: "alternate",
  brand: {
    primary: "#059669", // Emerald 600
    accent: "#d97706",  // Amber 600
    neutralTone: "stone"
  },
  typography: {
    familyKey: "sans"
  },
  shape: {
    radiusPreset: "soft"
  },
  defaultDensity: "compact"
};

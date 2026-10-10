import { describe, it } from "node:test";
import assert from "node:assert/strict";
import {
  defaultThemeDefinition,
  alternateThemeDefinition,
  themeDefinitionSchema
} from "./theme-definition.ts";
import { compileTheme } from "./theme-compiler.ts";
import { sRgbToOklch } from "./color-derivation.ts";

describe("ThemeDefinition Schema Validation", () => {
  it("validates standard default and alternate theme definitions", () => {
    const validDefault = themeDefinitionSchema.safeParse(defaultThemeDefinition);
    assert.equal(validDefault.success, true);

    const validAlt = themeDefinitionSchema.safeParse(alternateThemeDefinition);
    assert.equal(validAlt.success, true);
  });

  it("strictly rejects arbitrary CSS injection or extra fields", () => {
    const maliciousInput = {
      preset: "malicious",
      brand: {
        primary: "#ff0000",
        customCss: "body { background: red !important; }"
      },
      dangerouslySetInnerHTML: "<script>alert(1)</script>"
    };

    const result = themeDefinitionSchema.safeParse(maliciousInput);
    assert.equal(result.success, false);
  });

  it("strictly rejects invalid color strings", () => {
    const badColor = {
      preset: "bad-color",
      brand: {
        primary: "not-a-color"
      }
    };
    const result = themeDefinitionSchema.safeParse(badColor);
    assert.equal(result.success, false);
  });
});

describe("Color Derivation (OKLCH)", () => {
  it("converts hex to valid OKLCH coordinates", () => {
    const oklch = sRgbToOklch("#4f46e5");
    assert.ok(oklch.l > 0 && oklch.l <= 1);
    assert.ok(oklch.c > 0 && oklch.c <= 0.4);
    assert.ok(oklch.h >= 0 && oklch.h <= 360);
  });

  it("preserves protected status colors across arbitrary brand themes", () => {
    const customBrandTheme = {
      preset: "custom-red-brand",
      brand: {
        primary: "#dc2626", // Red brand
        neutralTone: "slate" as const
      }
    };

    const { theme } = compileTheme(customBrandTheme);

    // Success must remain in green/emerald hue (~145), never changed to red
    assert.ok(theme.cssVariables["--color-status-success"]?.includes("145"));
    // Danger must remain in red/rose hue (~25)
    assert.ok(theme.cssVariables["--color-status-danger"]?.includes("25"));
  });
});

describe("Theme Compiler & Safe Fallbacks", () => {
  it("compiles default theme with all required CSS variables", () => {
    const { theme, isValid } = compileTheme(defaultThemeDefinition);

    assert.equal(isValid, true);
    assert.equal(theme.presetKey, "default");
    assert.ok(theme.cssVariables["--color-surface-page"]);
    assert.ok(theme.cssVariables["--color-content-primary"]);
    assert.ok(theme.cssVariables["--color-action-primary"]);
    assert.ok(theme.cssVariables["--radius-md"]);
    assert.ok(theme.cssVariables["--density-field-height"]);
  });

  it("compiles distinct alternate theme verifying no hardcoded styles", () => {
    const defaultResult = compileTheme(defaultThemeDefinition);
    const altResult = compileTheme(alternateThemeDefinition);

    assert.notEqual(
      defaultResult.theme.cssVariables["--color-action-primary"],
      altResult.theme.cssVariables["--color-action-primary"]
    );
    assert.notEqual(
      defaultResult.theme.cssVariables["--color-surface-page"],
      altResult.theme.cssVariables["--color-surface-page"]
    );
  });

  it("safely falls back to default theme on invalid input without crashing", () => {
    const invalidInput = { invalid: "data", brand: { primary: "xyz" } };
    const { theme, isValid, validationErrors } = compileTheme(invalidInput);

    assert.equal(isValid, false);
    assert.ok(validationErrors && validationErrors.length > 0);
    // Theme is still safely returned as default theme
    assert.equal(theme.presetKey, "default");
    assert.ok(theme.cssVariables["--color-action-primary"]);
  });
});

describe("Density Engine Independence", () => {
  it("changes density metrics independently of color theme", () => {
    const comfortable = compileTheme(defaultThemeDefinition, { density: "comfortable" });
    const compact = compileTheme(defaultThemeDefinition, { density: "compact" });

    // Colors remain identical
    assert.equal(
      comfortable.theme.cssVariables["--color-action-primary"],
      compact.theme.cssVariables["--color-action-primary"]
    );

    // Height & row metrics differ
    assert.equal(comfortable.theme.cssVariables["--density-field-height"], "2.5rem");
    assert.equal(compact.theme.cssVariables["--density-field-height"], "2rem");
    assert.equal(comfortable.theme.cssVariables["--density-grid-row-height"], "3rem");
    assert.equal(compact.theme.cssVariables["--density-grid-row-height"], "2.25rem");
  });
});

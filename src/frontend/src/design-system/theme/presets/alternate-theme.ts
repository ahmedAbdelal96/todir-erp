import type { SemanticTokens } from "../../tokens/semantic-tokens.ts";

/**
 * Strong Alternate Development Theme: Warm Stone Neutral, Emerald Primary, Amber Accent
 * Purpose: Switching between Default and Alternate exposes any hard-coded colors/styles.
 */
export const alternateSemanticTokensLight: SemanticTokens = {
  surface: {
    page: "oklch(0.975 0.015 65)",       // Warm stone page
    card: "oklch(0.995 0.005 65)",      // Warm white card
    elevated: "oklch(1.0 0 0)",
    subtle: "oklch(0.95 0.02 65)",       // Subdued warm tone
    inset: "oklch(0.92 0.025 65)",
    overlay: "oklch(0.2 0.02 65 / 0.55)"
  },
  content: {
    primary: "oklch(0.18 0.025 50)",     // Warm dark brown/stone
    secondary: "oklch(0.42 0.035 50)",
    muted: "oklch(0.58 0.03 50)",
    inverse: "oklch(0.99 0 0)",
    disabled: "oklch(0.72 0.015 50)"
  },
  border: {
    default: "oklch(0.86 0.02 60)",      // Warm stone border
    subtle: "oklch(0.92 0.015 60)",
    strong: "oklch(0.7 0.03 60)",
    focus: "oklch(0.55 0.18 155)",       // Emerald focus ring
    interactive: "oklch(0.6 0.15 155)"
  },
  action: {
    primary: "oklch(0.52 0.18 155)",         // Emerald 700
    primaryHover: "oklch(0.45 0.2 155)",     // Emerald 800
    primaryActive: "oklch(0.38 0.2 155)",
    primaryContent: "oklch(0.99 0 0)",
    secondary: "oklch(0.94 0.02 65)",
    secondaryHover: "oklch(0.9 0.03 65)",
    secondaryActive: "oklch(0.86 0.035 65)",
    secondaryContent: "oklch(0.2 0.03 50)",
    ghost: "transparent",
    ghostHover: "oklch(0.93 0.02 65)",
    ghostActive: "oklch(0.88 0.03 65)",
    ghostContent: "oklch(0.25 0.03 50)"
  },
  status: {
    success: {
      base: "oklch(0.62 0.17 145)",
      subtle: "oklch(0.96 0.04 145)",
      content: "oklch(0.25 0.09 145)",
      border: "oklch(0.85 0.08 145)"
    },
    warning: {
      base: "oklch(0.75 0.16 75)",
      subtle: "oklch(0.97 0.05 75)",
      content: "oklch(0.35 0.12 75)",
      border: "oklch(0.88 0.1 75)"
    },
    danger: {
      base: "oklch(0.58 0.22 25)",
      subtle: "oklch(0.96 0.05 25)",
      content: "oklch(0.3 0.15 25)",
      border: "oklch(0.86 0.1 25)"
    },
    info: {
      base: "oklch(0.6 0.15 230)",
      subtle: "oklch(0.96 0.04 230)",
      content: "oklch(0.28 0.1 230)",
      border: "oklch(0.86 0.08 230)"
    }
  }
};

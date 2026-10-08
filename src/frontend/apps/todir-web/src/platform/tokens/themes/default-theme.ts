import type { SemanticTokens } from "../semantic-tokens.ts";

/**
 * Default Development Theme: Enterprise Slate & Indigo
 */
export const defaultSemanticTokensLight: SemanticTokens = {
  surface: {
    page: "oklch(0.985 0.005 240)",      // Clean cool surface
    card: "oklch(1.0 0 0)",              // Pure white card
    elevated: "oklch(1.0 0 0)",          // Elevated card/modal
    subtle: "oklch(0.965 0.008 240)",    // Subdued background
    inset: "oklch(0.94 0.01 240)",       // Inset area
    overlay: "oklch(0.2 0.02 240 / 0.5)" // Backdrop overlay
  },
  content: {
    primary: "oklch(0.18 0.02 240)",     // Slate 900
    secondary: "oklch(0.42 0.03 240)",   // Slate 600
    muted: "oklch(0.58 0.02 240)",       // Slate 400
    inverse: "oklch(0.99 0 0)",          // White text
    disabled: "oklch(0.72 0.01 240)"     // Disabled text
  },
  border: {
    default: "oklch(0.88 0.01 240)",     // Slate 200
    subtle: "oklch(0.93 0.008 240)",     // Subtle divider
    strong: "oklch(0.75 0.02 240)",      // Stronger container border
    focus: "oklch(0.55 0.18 260)",       // Indigo focus ring
    interactive: "oklch(0.65 0.15 260)"  // Interactive border
  },
  action: {
    primary: "oklch(0.52 0.2 260)",          // Indigo 600
    primaryHover: "oklch(0.46 0.22 260)",     // Indigo 700
    primaryActive: "oklch(0.4 0.22 260)",     // Indigo 800
    primaryContent: "oklch(0.99 0 0)",       // White
    secondary: "oklch(0.96 0.01 240)",       // Light button
    secondaryHover: "oklch(0.92 0.015 240)",
    secondaryActive: "oklch(0.88 0.02 240)",
    secondaryContent: "oklch(0.2 0.02 240)",
    ghost: "transparent",
    ghostHover: "oklch(0.95 0.01 240)",
    ghostActive: "oklch(0.9 0.015 240)",
    ghostContent: "oklch(0.25 0.02 240)"
  },
  status: {
    success: {
      base: "oklch(0.62 0.17 145)",        // Emerald
      subtle: "oklch(0.96 0.04 145)",
      content: "oklch(0.25 0.09 145)",
      border: "oklch(0.85 0.08 145)"
    },
    warning: {
      base: "oklch(0.75 0.16 75)",         // Amber
      subtle: "oklch(0.97 0.05 75)",
      content: "oklch(0.35 0.12 75)",
      border: "oklch(0.88 0.1 75)"
    },
    danger: {
      base: "oklch(0.58 0.22 25)",         // Rose/Red
      subtle: "oklch(0.96 0.05 25)",
      content: "oklch(0.3 0.15 25)",
      border: "oklch(0.86 0.1 25)"
    },
    info: {
      base: "oklch(0.6 0.15 230)",         // Sky
      subtle: "oklch(0.96 0.04 230)",
      content: "oklch(0.28 0.1 230)",
      border: "oklch(0.86 0.08 230)"
    }
  }
};

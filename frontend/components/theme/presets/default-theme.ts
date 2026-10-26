import type { SemanticTokens } from "../../tokens/semantic-tokens.ts";

/**
 * Default Development Theme: Enterprise Slate & Indigo
 */
export const defaultSemanticTokensLight: SemanticTokens = {
  surface: {
    page: "oklch(0.965 0.01 240)",       // Crisp Slate-100 canvas (distinct from white cards)
    card: "oklch(1.0 0 0)",              // Pure crisp white card
    elevated: "oklch(1.0 0 0)",          // Elevated card/modal
    subtle: "oklch(0.945 0.012 240)",    // Subdued background / card header tint
    inset: "oklch(0.925 0.015 240)",     // Inset area
    overlay: "oklch(0.15 0.02 240 / 0.6)"// Deep modern backdrop overlay
  },
  content: {
    primary: "oklch(0.15 0.025 240)",    // Deep slate 950 (high contrast, crisp text)
    secondary: "oklch(0.35 0.035 240)",  // Slate 700
    muted: "oklch(0.50 0.03 240)",       // Slate 500 (not washed out)
    inverse: "oklch(0.99 0 0)",          // White text
    disabled: "oklch(0.70 0.015 240)"    // Disabled text
  },
  border: {
    default: "oklch(0.84 0.018 240)",    // Crisp Slate 250 border (distinct card perimeter)
    subtle: "oklch(0.90 0.012 240)",     // Subtle divider
    strong: "oklch(0.68 0.025 240)",     // Stronger container border
    focus: "oklch(0.52 0.22 260)",       // Vibrant Indigo focus ring
    interactive: "oklch(0.60 0.18 260)"  // Interactive border
  },
  action: {
    primary: "oklch(0.50 0.22 260)",     // Vibrant Royal Indigo 600
    primaryHover: "oklch(0.44 0.24 260)",// Indigo 700
    primaryActive: "oklch(0.38 0.24 260)",// Indigo 800
    primaryContent: "oklch(0.99 0 0)",   // White
    secondary: "oklch(1.0 0 0)",         // Crisp white button with border
    secondaryHover: "oklch(0.96 0.01 240)",
    secondaryActive: "oklch(0.92 0.015 240)",
    secondaryContent: "oklch(0.18 0.025 240)",
    ghost: "transparent",
    ghostHover: "oklch(0.94 0.012 240)",
    ghostActive: "oklch(0.88 0.018 240)",
    ghostContent: "oklch(0.22 0.025 240)"
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

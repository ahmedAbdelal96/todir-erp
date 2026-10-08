import type { ThemeDefinition } from "./theme-definition.ts";
import type { SemanticTokens } from "../tokens/semantic-tokens.ts";
import { defaultSemanticTokensLight } from "../tokens/themes/default-theme.ts";
import { alternateSemanticTokensLight } from "../tokens/themes/alternate-theme.ts";

export interface OklchColor {
  readonly l: number; // 0..1
  readonly c: number; // 0..0.4
  readonly h: number; // 0..360
}

/**
 * Converts a hex string (#RGB or #RRGGBB) to sRGB [0..1]
 */
function hexToRgb(hex: string): [number, number, number] {
  let clean = hex.replace("#", "");
  if (clean.length === 3) {
    clean = clean.split("").map(c => c + c).join("");
  }
  const num = parseInt(clean, 16);
  const r = ((num >> 16) & 255) / 255;
  const g = ((num >> 8) & 255) / 255;
  const b = (num & 255) / 255;
  return [r, g, b];
}

/**
 * Converts linear RGB to OKLCH color coordinates
 */
export function sRgbToOklch(hex: string): OklchColor {
  if (hex.startsWith("oklch")) {
    const match = hex.match(/oklch\(\s*([\d.]+)\s+([\d.]+)\s+([\d.]+)/);
    if (match && match[1] && match[2] && match[3]) {
      return {
        l: parseFloat(match[1]),
        c: parseFloat(match[2]),
        h: parseFloat(match[3])
      };
    }
  }

  const [r, g, b] = hexToRgb(hex);

  // Approximate sRGB to OKLCH conversion
  // Gamma expansion
  const rLin = r > 0.04045 ? Math.pow((r + 0.055) / 1.055, 2.4) : r / 12.92;
  const gLin = g > 0.04045 ? Math.pow((g + 0.055) / 1.055, 2.4) : g / 12.92;
  const bLin = b > 0.04045 ? Math.pow((b + 0.055) / 1.055, 2.4) : b / 12.92;

  // LMS matrix
  const l_ = Math.cbrt(0.4122214708 * rLin + 0.5363325363 * gLin + 0.0514459929 * bLin);
  const m_ = Math.cbrt(0.2119034982 * rLin + 0.6806995451 * gLin + 0.1073969566 * bLin);
  const s_ = Math.cbrt(0.0883024619 * rLin + 0.2817188376 * gLin + 0.6299787005 * bLin);

  const L = 0.2104542553 * l_ + 0.7936177850 * m_ - 0.0040720468 * s_;
  const a = 1.9779984951 * l_ - 2.4285922050 * m_ + 0.4505937099 * s_;
  const b_val = 0.0259040371 * l_ + 0.7827717662 * m_ - 0.8086757660 * s_;

  const C = Math.sqrt(a * a + b_val * b_val);
  let h = (Math.atan2(b_val, a) * 180) / Math.PI;
  if (h < 0) {
    h += 360;
  }

  return {
    l: Math.round(L * 1000) / 1000,
    c: Math.round(C * 1000) / 1000,
    h: Math.round(h * 10) / 10
  };
}

export function formatOklch(color: OklchColor, alpha?: number): string {
  const l = Math.max(0, Math.min(1, color.l));
  const c = Math.max(0, color.c);
  const h = (color.h + 360) % 360;
  if (alpha !== undefined) {
    return `oklch(${l} ${c} ${h} / ${alpha})`;
  }
  return `oklch(${l} ${c} ${h})`;
}

/**
 * Derives accessible semantic tokens from a validated ThemeDefinition
 */
export function deriveSemanticTokens(themeDef: ThemeDefinition): SemanticTokens {
  if (themeDef.preset === "alternate") {
    return alternateSemanticTokensLight;
  }
  if (themeDef.preset === "default" && themeDef.brand.primary === "#4f46e5" && themeDef.brand.neutralTone === "slate") {
    return defaultSemanticTokensLight;
  }

  const primaryOklch = sRgbToOklch(themeDef.brand.primary);
  const neutralHue = themeDef.brand.neutralTone === "stone" ? 65 : themeDef.brand.neutralTone === "zinc" ? 260 : 240;
  const neutralChroma = themeDef.brand.neutralTone === "neutral" ? 0 : 0.015;

  // Primary action progression
  const primaryBase = formatOklch({ l: Math.max(0.4, Math.min(0.65, primaryOklch.l)), c: Math.max(0.12, primaryOklch.c), h: primaryOklch.h });
  const primaryHover = formatOklch({ l: Math.max(0.32, primaryOklch.l - 0.07), c: Math.max(0.14, primaryOklch.c + 0.02), h: primaryOklch.h });
  const primaryActive = formatOklch({ l: Math.max(0.26, primaryOklch.l - 0.14), c: Math.max(0.14, primaryOklch.c + 0.02), h: primaryOklch.h });
  const primaryContent = primaryOklch.l < 0.68 ? "oklch(0.99 0 0)" : "oklch(0.15 0.02 240)";
  const focusRing = formatOklch({ l: 0.55, c: Math.max(0.16, primaryOklch.c), h: primaryOklch.h });

  return {
    surface: {
      page: formatOklch({ l: 0.985, c: neutralChroma * 0.4, h: neutralHue }),
      card: "oklch(1.0 0 0)",
      elevated: "oklch(1.0 0 0)",
      subtle: formatOklch({ l: 0.965, c: neutralChroma * 0.6, h: neutralHue }),
      inset: formatOklch({ l: 0.935, c: neutralChroma * 0.8, h: neutralHue }),
      overlay: formatOklch({ l: 0.2, c: neutralChroma, h: neutralHue }, 0.5)
    },
    content: {
      primary: formatOklch({ l: 0.18, c: neutralChroma * 1.5, h: neutralHue }),
      secondary: formatOklch({ l: 0.42, c: neutralChroma * 2, h: neutralHue }),
      muted: formatOklch({ l: 0.58, c: neutralChroma * 1.5, h: neutralHue }),
      inverse: "oklch(0.99 0 0)",
      disabled: formatOklch({ l: 0.72, c: neutralChroma, h: neutralHue })
    },
    border: {
      default: formatOklch({ l: 0.88, c: neutralChroma, h: neutralHue }),
      subtle: formatOklch({ l: 0.93, c: neutralChroma * 0.8, h: neutralHue }),
      strong: formatOklch({ l: 0.74, c: neutralChroma * 1.5, h: neutralHue }),
      focus: focusRing,
      interactive: formatOklch({ l: 0.65, c: Math.max(0.12, primaryOklch.c * 0.8), h: primaryOklch.h })
    },
    action: {
      primary: primaryBase,
      primaryHover,
      primaryActive,
      primaryContent,
      secondary: formatOklch({ l: 0.96, c: neutralChroma * 0.7, h: neutralHue }),
      secondaryHover: formatOklch({ l: 0.92, c: neutralChroma * 1.1, h: neutralHue }),
      secondaryActive: formatOklch({ l: 0.88, c: neutralChroma * 1.4, h: neutralHue }),
      secondaryContent: formatOklch({ l: 0.2, c: neutralChroma * 1.5, h: neutralHue }),
      ghost: "transparent",
      ghostHover: formatOklch({ l: 0.95, c: neutralChroma * 0.8, h: neutralHue }),
      ghostActive: formatOklch({ l: 0.9, c: neutralChroma * 1.2, h: neutralHue }),
      ghostContent: formatOklch({ l: 0.25, c: neutralChroma * 1.5, h: neutralHue })
    },
    // Status semantics are ALWAYS protected from tenant recoloring
    status: defaultSemanticTokensLight.status
  };
}

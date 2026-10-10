/**
 * Level 1: Foundation / Reference Tokens
 * Bounded scales for colors, typography, spacing, radius, elevation, and motion.
 */

export interface ReferenceColorScale {
  readonly 50: string;
  readonly 100: string;
  readonly 200: string;
  readonly 300: string;
  readonly 400: string;
  readonly 500: string;
  readonly 600: string;
  readonly 700: string;
  readonly 800: string;
  readonly 900: string;
  readonly 950: string;
}

export interface ReferenceTypography {
  readonly fontFamily: {
    readonly sans: string;
    readonly mono: string;
  };
  readonly fontSize: {
    readonly xs: string;
    readonly sm: string;
    readonly base: string;
    readonly lg: string;
    readonly xl: string;
    readonly "2xl": string;
    readonly "3xl": string;
  };
  readonly lineHeight: {
    readonly tight: string;
    readonly normal: string;
    readonly relaxed: string;
  };
  readonly fontWeight: {
    readonly normal: string;
    readonly medium: string;
    readonly semibold: string;
    readonly bold: string;
  };
}

export interface ReferenceSpacing {
  readonly 0: string;
  readonly 1: string;
  readonly 2: string;
  readonly 3: string;
  readonly 4: string;
  readonly 5: string;
  readonly 6: string;
  readonly 8: string;
  readonly 10: string;
  readonly 12: string;
  readonly 16: string;
  readonly 20: string;
  readonly 24: string;
}

export interface ReferenceRadii {
  readonly none: string;
  readonly sm: string;
  readonly md: string;
  readonly lg: string;
  readonly xl: string;
  readonly full: string;
}

export interface ReferenceShadows {
  readonly none: string;
  readonly sm: string;
  readonly md: string;
  readonly lg: string;
  readonly xl: string;
}

export interface ReferenceZIndex {
  readonly base: string;
  readonly dropdown: string;
  readonly sticky: string;
  readonly fixed: string;
  readonly backdrop: string;
  readonly modal: string;
  readonly popover: string;
  readonly tooltip: string;
}

export const referenceSpacing: ReferenceSpacing = {
  0: "0rem",
  1: "0.25rem",  // 4px
  2: "0.5rem",   // 8px
  3: "0.75rem",  // 12px
  4: "1rem",     // 16px
  5: "1.25rem",  // 20px
  6: "1.5rem",   // 24px
  8: "2rem",     // 32px
  10: "2.5rem",  // 40px
  12: "3rem",    // 48px
  16: "4rem",    // 64px
  20: "5rem",    // 80px
  24: "6rem"     // 96px
};

export const referenceRadiiStandard: ReferenceRadii = {
  none: "0px",
  sm: "0.25rem",  // 4px
  md: "0.375rem", // 6px
  lg: "0.5rem",   // 8px
  xl: "0.75rem",  // 12px
  full: "9999px"
};

export const referenceShadows: ReferenceShadows = {
  none: "none",
  sm: "0 1px 2px 0 rgba(0, 0, 0, 0.05)",
  md: "0 4px 6px -1px rgba(0, 0, 0, 0.07), 0 2px 4px -2px rgba(0, 0, 0, 0.04)",
  lg: "0 10px 15px -3px rgba(0, 0, 0, 0.08), 0 4px 6px -4px rgba(0, 0, 0, 0.03)",
  xl: "0 20px 25px -5px rgba(0, 0, 0, 0.1), 0 8px 10px -6px rgba(0, 0, 0, 0.04)"
};

export const referenceRadiiSharp: ReferenceRadii = {
  none: "0px",
  sm: "0px",
  md: "2px",
  lg: "3px",
  xl: "4px",
  full: "9999px"
};

export const referenceRadiiSoft: ReferenceRadii = {
  none: "0px",
  sm: "0.375rem",
  md: "0.5rem",
  lg: "0.75rem",
  xl: "1rem",
  full: "9999px"
};

export const referenceTypography: ReferenceTypography = {
  fontFamily: {
    // Approved high-quality Arabic + English fallback stack
    sans: 'system-ui, -apple-system, "Segoe UI", Roboto, "Noto Sans Arabic", "IBM Plex Sans Arabic", "Tajawal", "Cairo", sans-serif',
    mono: 'ui-monospace, "Cascadia Code", "Roboto Mono", "Courier New", monospace'
  },
  fontSize: {
    xs: "0.75rem",   // 12px
    sm: "0.875rem",  // 14px
    base: "1rem",    // 16px
    lg: "1.125rem",  // 18px
    xl: "1.25rem",   // 20px
    "2xl": "1.5rem", // 24px
    "3xl": "1.875rem"// 30px
  },
  lineHeight: {
    tight: "1.25",
    normal: "1.5",
    relaxed: "1.75"
  },
  fontWeight: {
    normal: "400",
    medium: "500",
    semibold: "600",
    bold: "700"
  }
};

export const referenceZIndex: ReferenceZIndex = {
  base: "0",
  dropdown: "1000",
  sticky: "1100",
  fixed: "1200",
  backdrop: "1300",
  modal: "1400",
  popover: "1500",
  tooltip: "1600"
};

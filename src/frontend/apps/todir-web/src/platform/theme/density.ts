import type { DensityMode } from "../runtime/preferences.ts";

export interface DensityMetrics {
  readonly fieldHeight: string;
  readonly buttonPaddingInline: string;
  readonly gridRowHeight: string;
  readonly gridCellPaddingInline: string;
  readonly toolbarHeight: string;
  readonly formGap: string;
}

export const comfortableDensityMetrics: DensityMetrics = {
  fieldHeight: "2.5rem",        // 40px
  buttonPaddingInline: "1rem",  // 16px
  gridRowHeight: "3rem",        // 48px
  gridCellPaddingInline: "1rem",// 16px
  toolbarHeight: "3.5rem",      // 56px
  formGap: "1.25rem"            // 20px
};

export const compactDensityMetrics: DensityMetrics = {
  fieldHeight: "2rem",           // 32px
  buttonPaddingInline: "0.75rem",// 12px
  gridRowHeight: "2.25rem",      // 36px
  gridCellPaddingInline: "0.75rem",// 12px
  toolbarHeight: "2.75rem",      // 44px
  formGap: "0.875rem"            // 14px
};

export function getDensityMetrics(mode: DensityMode): DensityMetrics {
  return mode === "compact" ? compactDensityMetrics : comfortableDensityMetrics;
}

export function compileDensityCssVariables(mode: DensityMode): Record<string, string> {
  const metrics = getDensityMetrics(mode);
  return {
    "--density-field-height": metrics.fieldHeight,
    "--density-button-padding-inline": metrics.buttonPaddingInline,
    "--density-grid-row-height": metrics.gridRowHeight,
    "--density-grid-cell-padding-inline": metrics.gridCellPaddingInline,
    "--density-toolbar-height": metrics.toolbarHeight,
    "--density-form-gap": metrics.formGap
  };
}

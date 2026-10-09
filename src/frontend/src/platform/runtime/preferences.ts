export type DensityMode = "comfortable" | "compact";
export type AppearanceMode = "light" | "dark" | "system";
export type NumeralSystem = "latin" | "arabic-indic";
export type SupportedLanguage = "ar" | "en";
export type TextDirection = "rtl" | "ltr";

export interface UserPreferences {
  readonly density: DensityMode;
  readonly appearance: AppearanceMode;
  readonly numeralSystem: NumeralSystem;
  readonly language: SupportedLanguage;
}

export const defaultUserPreferences: UserPreferences = {
  density: "comfortable",
  appearance: "light",
  numeralSystem: "latin",
  language: "ar"
};

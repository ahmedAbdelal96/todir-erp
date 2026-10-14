export type SupportedLanguage = "ar" | "en";
export type TextDirection = "rtl" | "ltr";
export type NumeralSystem = "latin" | "arabic-indic";

export const DEFAULT_LANGUAGE: SupportedLanguage = "ar";
export const LANGUAGE_STORAGE_KEY = "todir.ui.language";

export function normalizeLanguage(value: string | undefined): SupportedLanguage {
  return value?.toLowerCase().startsWith("en") ? "en" : "ar";
}

export function directionForLanguage(language: SupportedLanguage): TextDirection {
  return language === "ar" ? "rtl" : "ltr";
}

export function intlLocaleFor(language: SupportedLanguage, numeralSystem: NumeralSystem = "latin"): string {
  if (language === "en") return "en";
  return numeralSystem === "arabic-indic" ? "ar-u-nu-arab" : "ar-u-nu-latn";
}

export function readSavedLanguage(): SupportedLanguage {
  if (typeof window === "undefined") return DEFAULT_LANGUAGE;
  try {
    const saved = window.localStorage.getItem(LANGUAGE_STORAGE_KEY);
    return saved ? normalizeLanguage(saved) : DEFAULT_LANGUAGE;
  } catch {
    return DEFAULT_LANGUAGE;
  }
}

export function synchronizeDocumentLanguage(language: SupportedLanguage): void {
  if (typeof document === "undefined") return;
  document.documentElement.lang = language;
  document.documentElement.dir = directionForLanguage(language);
}

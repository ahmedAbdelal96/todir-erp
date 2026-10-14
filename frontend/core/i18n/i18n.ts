import i18n from "i18next";
import { initReactI18next } from "react-i18next";
import { ar as arCommon } from "./resources/ar/common.ts";
import { en as enCommon } from "./resources/en/common.ts";
import { ar as arNavigation } from "./resources/ar/navigation.ts";
import { en as enNavigation } from "./resources/en/navigation.ts";
import { ar as arValidation } from "./resources/ar/validation.ts";
import { en as enValidation } from "./resources/en/validation.ts";
import { ar as arWorkspace } from "./resources/ar/workspace.ts";
import { en as enWorkspace } from "./resources/en/workspace.ts";
import {
  DEFAULT_LANGUAGE,
  directionForLanguage,
  normalizeLanguage,
  readSavedLanguage,
  synchronizeDocumentLanguage,
  type SupportedLanguage
} from "./locale.ts";

const sharedResources = {
  ar: {
    common: arCommon,
    navigation: arNavigation,
    validation: arValidation,
    workspace: arWorkspace
  },
  en: {
    common: enCommon,
    navigation: enNavigation,
    validation: enValidation,
    workspace: enWorkspace
  }
} as const;

const initialLanguage = readSavedLanguage();
synchronizeDocumentLanguage(initialLanguage);

export type ModuleLocalizationResources = {
  readonly ar: Readonly<Record<string, unknown>>;
  readonly en: Readonly<Record<string, unknown>>;
};

export let i18nReady: Promise<unknown> | undefined;

export function initializeLocalization(moduleResources: ModuleLocalizationResources): Promise<unknown> {
  const resources = {
    ar: { ...sharedResources.ar, ...moduleResources.ar },
    en: { ...sharedResources.en, ...moduleResources.en }
  };
  const namespaces = Object.keys(resources.ar);
  i18nReady = i18n.use(initReactI18next).init({
    resources,
    lng: initialLanguage,
    supportedLngs: ["ar", "en"],
    fallbackLng: false,
    load: "languageOnly",
    ns: namespaces,
    defaultNS: "common",
    returnNull: false,
    returnEmptyString: false,
    interpolation: { escapeValue: false },
    react: { useSuspense: true },
    missingKeyHandler(languages, namespace, key) {
      if (import.meta.env.DEV) console.error(`Missing localization resource: ${languages.join(",")} / ${namespace} / ${key}`);
    },
    parseMissingKeyHandler() {
      return i18n.resolvedLanguage === "en" ? "Text unavailable" : "النص غير متاح";
    }
  });
  return i18nReady;
}

i18n.on("initialized", () => synchronizeDocumentLanguage(normalizeLanguage(i18n.resolvedLanguage ?? i18n.language)));
i18n.on("languageChanged", (language) => synchronizeDocumentLanguage(normalizeLanguage(language)));

export async function changeLanguage(language: SupportedLanguage): Promise<void> {
  const nextLanguage = normalizeLanguage(language);
  try {
    window.localStorage.setItem("todir.ui.language", nextLanguage);
  } catch {
    // The current page still switches when browser storage is unavailable.
  }
  await i18n.changeLanguage(nextLanguage);
}

export function currentLanguage(): SupportedLanguage {
  return normalizeLanguage(i18n.resolvedLanguage ?? i18n.language ?? DEFAULT_LANGUAGE);
}

export function currentDirection() {
  return directionForLanguage(currentLanguage());
}

export function currentIntlLocale(): string {
  return currentLanguage() === "ar" ? "ar-u-nu-latn" : "en";
}

export { i18n };

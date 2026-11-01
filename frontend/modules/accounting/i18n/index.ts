import { useTranslation } from "react-i18next";
export { ar } from "./ar.ts";
export { en } from "./en.ts";

export function useAccountingI18n() {
  const { t, i18n } = useTranslation("accounting");
  const language = i18n.language?.startsWith("ar") ? "ar" : "en";
  return { t, i18n, language };
}

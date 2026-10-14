import test from "node:test";
import assert from "node:assert/strict";
import { checkResourceParity, findHardcodedCopy } from "./verify-localization.mjs";
import { DEFAULT_LANGUAGE, directionForLanguage, intlLocaleFor, normalizeLanguage, readSavedLanguage, synchronizeDocumentLanguage } from "../../core/i18n/locale.ts";

test("Arabic is the default and language determines document direction and number locale", () => {
  assert.equal(DEFAULT_LANGUAGE, "ar");
  assert.equal(normalizeLanguage("en-US"), "en");
  assert.equal(normalizeLanguage("unknown"), "ar");
  assert.equal(directionForLanguage("ar"), "rtl");
  assert.equal(directionForLanguage("en"), "ltr");
  assert.equal(intlLocaleFor("ar"), "ar-u-nu-latn");
  assert.equal(intlLocaleFor("ar", "arabic-indic"), "ar-u-nu-arab");
  assert.equal(intlLocaleFor("en"), "en");
});

test("i18next is the single live language state and persists selection while syncing document metadata", async () => {
  const values = new Map();
  globalThis.window = { localStorage: { getItem: (key) => values.get(key) ?? null, setItem: (key, value) => values.set(key, value) } };
  globalThis.document = { documentElement: { lang: "ar", dir: "rtl" } };
  await import("../../app/localization.ts");
  const locale = await import("../../core/i18n/i18n.ts");
  await locale.i18nReady;
  await locale.changeLanguage("en");
  assert.equal(locale.currentLanguage(), "en");
  assert.equal(values.get("todir.ui.language"), "en");
  assert.equal(readSavedLanguage(), "en");
  assert.equal(document.documentElement.lang, "en");
  assert.equal(document.documentElement.dir, "ltr");
  assert.equal(locale.i18n.t("common:__missing_test_key__"), "Text unavailable");
  await locale.changeLanguage("ar");
  assert.equal(locale.currentLanguage(), "ar");
  assert.equal(readSavedLanguage(), "ar");
  assert.equal(document.documentElement.lang, "ar");
  assert.equal(document.documentElement.dir, "rtl");
  assert.equal(locale.i18n.t("common:__missing_test_key__"), "النص غير متاح");
  synchronizeDocumentLanguage("ar");
});

test("every shared and module localization resource has complete Arabic and English key parity", async () => {
  assert.deepEqual(await checkResourceParity(), []);
});

test("governed application source contains no hardcoded user-facing copy", async () => {
  assert.deepEqual(await findHardcodedCopy(), []);
});

/**
 * Normalizes Arabic characters, tashkeel, and Latin diacritics/case
 * for lightning-fast, forgiving Arabic-first search matching.
 */
export function normalizeSearchQuery(query: string): string {
  if (!query) return "";
  return query
    .toLowerCase()
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "") // remove Latin diacritics
    .replace(/[\u064B-\u065F\u0670]/g, "") // remove Arabic tashkeel / harakat
    .replace(/[\u0622\u0623\u0625\u0671]/g, "\u0627") // normalize all Alef variants to bare Alef
    .replace(/\u0629/g, "\u0647") // normalize Taa Marbuta to Haa
    .replace(/[\u0649\u064A]/g, "\u064A") // normalize Alef Maksura to Yaa
    .trim();
}

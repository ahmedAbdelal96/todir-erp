/**
 * Keeps the strict TypeScript baseline executable before frontend implementation begins.
 */
export type StrictTypecheckSentinel = Readonly<Record<never, never>>;

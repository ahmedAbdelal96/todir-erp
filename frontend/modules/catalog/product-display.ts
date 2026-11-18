/**
 * Product codes are business-facing identifiers. Internal UUIDs must never be
 * presented as an SKU when a product was created without a real code.
 */
export function getHumanProductCode(value?: string | null): string | undefined {
  const code = value?.trim();
  if (!code || containsGuid(code) || /^[0-9a-f]{32}$/i.test(code)) return undefined;
  return code;
}

function containsGuid(value: string): boolean {
  return /[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/i.test(value);
}

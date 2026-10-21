export const PricingReasonKind = {
  Customer: 0,
  PriceGroup: 1,
  QuantityTier: 2,
  ExactUnit: 3,
  Standard: 4,
} as const;

export type PricingReasonKind = (typeof PricingReasonKind)[keyof typeof PricingReasonKind];

export interface PricingReasonSummary {
  readonly kind: PricingReasonKind;
  readonly minimumQuantity: number | null;
}

/** Maps the resolver's diagnostic summary to business facts without displaying it. */
export function summarizePricingReason(reason: string): PricingReasonSummary {
  const minimumMatch = reason.match(/minimum quantity\s+([\d.]+)/i);
  const parsedMinimum = minimumMatch ? Number(minimumMatch[1]) : null;
  const minimumQuantity = parsedMinimum !== null && Number.isFinite(parsedMinimum) ? parsedMinimum : null;

  if (/SpecificParty/i.test(reason)) return { kind: PricingReasonKind.Customer, minimumQuantity };
  if (/PriceGroup/i.test(reason)) return { kind: PricingReasonKind.PriceGroup, minimumQuantity };
  if (minimumQuantity !== null && minimumQuantity > 1) return { kind: PricingReasonKind.QuantityTier, minimumQuantity };
  if (/unit specificity\s+3/i.test(reason)) return { kind: PricingReasonKind.ExactUnit, minimumQuantity };
  return { kind: PricingReasonKind.Standard, minimumQuantity };
}

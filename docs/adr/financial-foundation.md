# Financial Foundation ADR

Status: Accepted — codifies the decisions in the canonical FinBase Module Pack.

## Decision

- Persist monetary values and exchange rates with PostgreSQL `numeric`; perform backend calculations with .NET `decimal` only.
- Expose every authoritative financial decimal in JSON as an invariant decimal string. Never serialize financial values as binary floating-point JSON numbers.
- Keep `platform.legal_entity.base_currency_code` as the single Base Currency source. FinBase owns entity currency enablement, but does not duplicate the base-currency fact.
- Store exchange rates as an explicit `from_currency` → `to_currency` multiplier. Resolve only approved rates whose effective date is on or before the business date, then enforce the entity's maximum age.
- Convert with an explicit immutable approved-rate snapshot and a versioned rounding-policy snapshot. Same-currency conversion returns the original amount at rate one without resolving a master rate.
- Store an explicit rounding mode for each supported financial context and an explicit line/document aggregation strategy. Tax-specific strategies remain owned by the Tax Pack.
- Represent payment percentages as ratios from zero through one. A final `Balance` rule receives the exact residual so schedule lines sum to the document total.
- Correct financial master data by adding a superseding or new version. Do not rewrite approved rates, captured snapshots, activated rounding policies, or active/retired payment-term versions.

## Consequences

Currency, conversion, rounding, and payment-schedule behavior remains deterministic across API and module boundaries. Downstream modules consume contracts and snapshots; they do not query FinBase tables directly. Country-specific legal choices and Tax rounding rules remain in their approved country or Tax ownership boundaries.

## Evidence

The canonical source is [FinBase Module Pack](../modules/06_ERP_SAAS_FINBASE_CURRENCY_PAYMENT_TERMS_MONEY_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md), sections 35–48, 49–122, 148–189, and F00–F19. Backend contracts, migrations, golden vectors, and security tests implement these decisions.

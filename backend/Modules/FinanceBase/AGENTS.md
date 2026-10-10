# Finance base module rules

- `foundation_finbase` owns shared currency, legal-entity currency, exchange-rate, rounding-policy, financial-policy and payment-term foundations.
- Keep exact decimal-string and rounding semantics authoritative in the backend; do not introduce Ledger, Tax, Pricing, inventory or posting behavior here.
- Tenant and legal-entity scope, authorization, RLS, audit and Outbox behavior remain aligned with the approved Platform Kernel contracts.
- Other modules consume public contracts and capabilities; they do not read FinanceBase tables or entities directly.

# ADR — Subledger open-item and settlement foundation

Status: Accepted for Pass 1 (SL00-SL09)

## Decision

Ledger remains the source of account-level accounting truth. Subledger is the source of party, open-item, application, and settlement truth. It owns the `finance_subledger` schema and a unified `PartyOpenItem` model with explicit Receivable/Payable direction and Debit/Credit polarity. Amounts are non-negative; direction and polarity carry economic meaning.

Payments owns movement of money. A payment may produce an unapplied Subledger credit item after the payment's own posting. Applying that credit later never creates another payment, bank movement, or journal entry. Subledger source contracts are in-process typed capabilities; source creation is not an arbitrary public REST endpoint.

Confirmed applications and reversals are append-only evidence. Application and reversal commands update both open items in one PostgreSQL transaction. Same-currency applications require matching tenant, legal entity, party, direction, control-account context, and opposite polarity. They are protected with sorted row locks, version checks, database uniqueness, and idempotency keys. Reversal records link to the original application and preserve actor, effective date, reason, and creation time.

PartyFinancialProfile is keyed by Party × Legal Entity and stores references to FinBase payment terms without copying Party identity or payment-term definitions. Historical source amounts, dates, currency, base-carrying snapshot, and control-account context are immutable. Cached remaining amounts are transactionally maintained and derivable from applications and reversal evidence.

## Consequences

- The module uses one settlement engine for AR and AP; automatic AR/AP netting is prohibited.
- Tenant-owned records use FORCE RLS and composite tenant/legal-entity foreign keys for Subledger-owned references. Legal-entity and active-party checks remain in the application boundary.
- The module does not write Ledger tables. Both sides' base-carrying amounts are preserved so any settlement difference is derivable; Ledger posting remains a separate future integration.
- Cross-currency settlement, installments beyond stable source-schedule references, advances/credit-note product workflows, write-offs, exposure, aging, statements, control reconciliation, public API/UX, full Outbox hardening, and scale certification are deferred.

## Evidence

The canonical Subledger Module Pack, `docs/modules/10_ERP_SAAS_SUBLEDGER_OPEN_ITEMS_SETTLEMENT_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`, sections 0-18, 211-260, 325-345, 443-453. Runtime boundaries follow `docs/platform/transactions.md`, `docs/platform/rls.md`, ADR-005, ADR-007, and the accepted Ledger posting foundation ADR.

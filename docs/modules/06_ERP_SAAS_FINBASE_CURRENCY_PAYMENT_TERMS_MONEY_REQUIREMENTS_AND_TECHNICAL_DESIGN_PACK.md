# 06 — ERP SaaS Financial Foundation / FinBase / Currency / Money / Payment Terms Requirements & Technical Design Pack

**File:** `06_ERP_SAAS_FINBASE_CURRENCY_PAYMENT_TERMS_MONEY_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`  
**Status:** PRODUCT + FINANCIAL INTEGRITY + DOMAIN + TECHNICAL BASELINE  
**Version:** v1.0  
**Date:** 2026-10-05  
**Audience:** Product Owner, Senior Product Designer, Product Architect, Finance Domain Lead, Accountant Reviewer, Backend/Frontend Leads, Performance Engineer, QA, Implementation Agent  
**Risk Class:** CRITICAL — affects every monetary number in the ERP

**Depends on**
- `01_ERP_SAAS_MASTER_MODULE_AND_DEPENDENCY_BLUEPRINT.md`
- `02_ERP_SAAS_PRODUCTION_READY_TECH_STACK_AND_PROJECT_STRUCTURE.md`
- `03_ERP_SAAS_PLATFORM_KERNEL_AND_MODULE_SYSTEM_TECHNICAL_DESIGN_PACK.md`
- `04_ERP_SAAS_DIRECTORY_PARTY_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `05_ERP_SAAS_CATALOG_PRODUCT_UOM_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`

---

# 0. Executive Decision

This pack establishes the **single financial language** used by the entire ERP.

The non-negotiable principle is:

> **One financial truth. One set of deterministic calculation primitives. Backend authoritative. Historical financial snapshots immutable. UX simple.**

The user must not need to understand:

- decimal precision,
- exchange-rate direction,
- rounding residuals,
- currency conversion math,
- schedule allocation,
- rate sourcing,
- effective-date lookup,
- base/transaction currency mechanics,

to perform normal business work.

The system handles that complexity correctly in the backend.

---

# 1. Why This Pack Is High Risk

A one-cent / one-piaster / one-halalah discrepancy can appear harmless.

At scale it causes:

- invoice totals not matching tax totals;
- receivable schedules not matching invoice total;
- payments not fully closing invoices;
- ledger debit/credit differences;
- reconciliation problems;
- external e-invoice validation failures;
- month-end differences;
- customer statement disputes;
- FX gains/losses errors;
- audit problems.

Therefore monetary arithmetic is architecture, not UI formatting.

---

# 2. What FinBase Is

FinBase is a Foundation domain that provides shared financial definitions and deterministic services.

It owns or coordinates:

```text
Currency reference/use
Legal Entity financial policy
Rounding policies
Exchange-rate records and resolution
Currency conversion
Payment Term definitions
Payment schedule calculation
Shared financial date/rate semantics
```

---

# 3. What FinBase Is NOT

FinBase is not:

```text
Ledger
Accounting
Invoicing
Payments
Pricing
Tax
Banking
Subledger
```

It does not own:

```text
Product Price          → Pricing
Tax Calculation        → Tax
Journal Entries        → Ledger
Customer Balance       → Subledger
Cash/Bank Movement     → Payments
Invoice                → Invoicing
FX Gain/Loss Posting   → Ledger/Subledger
```

It supplies the primitives those modules use.

---

# 4. Most Important Source-of-Truth Rule

There must never be independent copies like:

```text
Sales.BaseCurrency
Invoicing.BaseCurrency
Payments.BaseCurrency
Ledger.BaseCurrency
```

This is forbidden.

---

# 5. Canonical Base Currency Ownership

The canonical base/functional currency belongs to:

```text
Organization.LegalEntity.base_currency_code
```

There is exactly one base currency for one Legal Entity at a time.

FinBase validates and consumes that value.

FinBase MUST NOT store another competing `base_currency_code`.

---

# 6. Why Base Currency Belongs to Legal Entity

A SaaS Tenant can contain:

```text
Egypt Legal Entity → EGP
Saudi Legal Entity → SAR
UAE Legal Entity   → AED
```

Therefore:

```text
Tenant.BaseCurrency ❌
LegalEntity.BaseCurrency ✅
```

---

# 7. Functional Currency Terminology

In the ERP product:

```text
Base Currency
```

means the Legal Entity's primary accounting/functional currency.

Accounting standards use the concept of a **functional currency**: the currency of the primary economic environment in which the entity operates.

We use user-friendly product wording while keeping the accounting meaning explicit.

---

# 8. Presentation / Reporting Currency Is Different

Future Ledger/Reporting may support:

```text
Reporting Currency
Presentation Currency
Additional Reporting Currency
```

This is NOT a second Base Currency.

The accounting Source of Truth remains the Legal Entity Base Currency.

---

# 9. Base Currency Lifecycle

Before financial go-live:

```text
Base Currency may be configured.
```

After first posted financial transaction / financial go-live:

```text
Normal Settings change = FORBIDDEN.
```

---

# 10. Base Currency Change After Go-Live

A genuine functional-currency change is not a dropdown edit.

It becomes a controlled project:

```text
BaseCurrencyChange / Financial Migration
```

requiring:

- accounting review;
- effective date;
- opening/remeasured balances;
- historical policy;
- reconciliation;
- auditor/accountant sign-off;
- country/legal review where applicable.

MVP does not support casual post-go-live base currency change.

---

# 11. Financial Truth Layers

There are several truths with different purposes.

## Current Master Truth

Current Legal Entity Base Currency and active financial policy.

## Current Rate Truth

Approved rate used as candidate for a new transaction.

## Transaction Snapshot Truth

The rate/policy actually used by a finalized transaction.

## Posted Accounting Truth

Values posted to Ledger.

A historical posted document never recalculates using today's master data.

---

# 12. Core Financial Invariants

1. One Legal Entity has one Base Currency.
2. A Money value always knows its Currency.
3. No implicit arithmetic across currencies.
4. No financial calculation uses binary floating point.
5. Every rounding operation is explicit.
6. No module invents its own rounding policy.
7. No module calls a live FX provider inside a posting transaction.
8. Historical rates/policies are snapshotted.
9. Final schedules sum exactly to document amount.
10. Backend is authoritative.
11. Posted values are immutable except via reversal/adjustment.
12. Same inputs + same policy snapshot = same output.

---

# 13. Money Is a Value Object

Money is not a database entity.

Concept:

```text
Money
{
    Amount: decimal
    Currency: CurrencyCode
}
```

Money belongs in the shared financial primitive library / Shared Kernel contract as already approved by the Master Architecture.

FinBase supplies policy/context around it.

---

# 14. Money Is Not Just Decimal

Forbidden conceptual API:

```text
CalculateTotal(decimal amount)
```

when currency matters.

Preferred:

```text
CalculateTotal(Money amount)
```

or explicit high-precision price/amount types.

---

# 15. Currency Mismatch Must Fail

This is invalid:

```text
100 USD + 500 EGP
```

without explicit conversion.

Money addition/subtraction requires same Currency.

---

# 16. No Implicit Conversion

This is forbidden:

```text
usdMoney.ToEgp()
```

using hidden current rate.

Conversion requires an explicit:

```text
ExchangeRateSnapshot / ConversionContext
```

---

# 17. Money Sign

Money can technically hold:

```text
positive
zero
negative
```

because credit/reversal calculations need signs.

Domain modules decide when negative amounts are allowed.

---

# 18. Money Equality

Equality includes:

```text
Amount
Currency
```

`100 USD != 100 EUR`.

---

# 19. Price Is Not Exactly Money

A Unit Price may require more decimal precision than a payable currency amount.

Example:

```text
0.00375 USD per unit
```

Therefore define separate concepts:

```text
Money
UnitPrice
Percentage
ExchangeRate
Quantity
```

Do not force all into one `decimal`.

---

# 20. Approved Numeric Types

Backend:

```text
C# decimal
```

Database:

```text
PostgreSQL numeric/decimal
```

Forbidden for authoritative monetary calculations:

```text
float
double
real
double precision
JavaScript Number as authoritative financial value
```

---

# 21. Why PostgreSQL NUMERIC

PostgreSQL documents `numeric/decimal` as exact and specifically recommends it for monetary amounts and quantities requiring exactness.

Do not use binary floating-point for financial truth.

---

# 22. Do Not Use PostgreSQL `money` as Canonical Type

We use explicit:

```text
numeric + currency_code
```

because the ERP must carry currency semantics explicitly and behave consistently across locales/integrations.

---

# 23. Database Precision Baseline

Lock the initial technical standard:

```text
Money stored amount      → numeric(28,8)
Unit Price               → numeric(28,10)
Exchange Rate            → numeric(28,12)
Percentage / Ratio       → numeric(18,8)
Payment allocation       → numeric(28,8)
```

These are storage ceilings, not automatic display precision.

---

# 24. Why Money Storage Has More Scale Than Typical Currency

Currency display/settlement may use:

```text
0 decimals
2 decimals
3 decimals
```

but database schema should not need migration when currency rules vary.

Application policy enforces the committed monetary scale.

---

# 25. Currency Minor Unit

Currency metadata includes the ISO minor-unit relationship.

Examples are not hardcoded in business logic.

ISO 4217 is the reference system for:

- three-letter codes;
- numeric codes;
- minor-unit relationship.

---

# 26. Currency Reference Data

System-level reference entity:

```text
CurrencyReference
```

Concept:

```text
code
numeric_code
name
minor_unit
status
valid_from?
valid_to?
dataset_version
```

---

# 27. Currency Reference Data Is Controlled

Do not query ISO/SIX at runtime for every request.

Ship/update currency reference data through controlled versioned platform data updates.

---

# 28. Enabled Currency vs Currency Exists

Currency exists in global reference data.

A Legal Entity may enable only some currencies for use.

Example:

```text
ISO list contains many currencies.
Entity enables:
EGP
USD
EUR
```

---

# 29. Legal Entity Currency Setup

FinBase owns:

```text
LegalEntityCurrency
```

Concept:

```text
tenant_id
legal_entity_id
currency_code
enabled
allow_sales
allow_purchase
allow_payments
allow_manual_rate
rate_policy_id?
status
```

Base Currency must always be enabled.

---

# 30. No Duplicate Currency Definition Per Module

Sales, Purchasing, Pricing, Invoicing and Payments consume FinBase Currency contracts.

They do not create their own Currency tables.

---

# 31. Currency Display

Storage:

```text
EGP
USD
EUR
```

UI formatting is locale-aware.

But ambiguous symbols should not be the only identifier.

Prefer:

```text
1,000.00 USD
```

in sensitive financial screens.

---

# 32. Currency Formatting Is Presentation

Never persist:

```text
"$1,000.00"
```

as monetary truth.

Persist amount + code.

---

# 33. JSON Financial Serialization

Authoritative financial decimal values should be serialized to frontend as decimal strings.

Example:

```json
{
  "amount": "1187.50",
  "currency": "EGP"
}
```

Reason:

JavaScript Number is binary floating point.

---

# 34. Frontend Financial Input

Frontend treats financial values as:

```text
string / decimal-library value
```

until sent to backend.

Avoid converting authoritative amounts through JS Number.

---

# 35. Frontend Decimal Preview

For instant UX, frontend may use a stable decimal arithmetic library for **preview only**.

This requires a small technology ADR and pinned package.

Backend recalculation remains authoritative.

---

# 36. Frontend Must Not Reimplement Financial Domain Rules

Frontend may preview:

```text
quantity × unit price
```

with decimal math.

Frontend must not independently own:

- tax rules;
- exchange-rate policy;
- payment-term legal rules;
- financial posting rules.

---

# 37. Backend Calculation Result Wins

If frontend preview shows:

```text
1,187.50
```

and backend canonical calculation returns:

```text
1,187.49
```

the backend result is truth.

The UI updates to backend result and surfaces any meaningful correction.

---

# 38. Rounding Is an Explicit Domain Operation

No direct uncontrolled calls to:

```text
Math.Round
decimal.Round
SQL round()
```

inside business modules.

All financial rounding goes through approved policy service.

---

# 39. Why Explicit Rounding Matters

.NET defaults to `MidpointRounding.ToEven` in common `Math.Round` overloads.

PostgreSQL `numeric` tie rounding behavior differs.

Therefore relying on framework/database defaults can cause cross-layer differences.

---

# 40. Rounding Engine

Contract concept:

```csharp
IMoneyRounding
```

Inputs:

```text
value
currency
rounding_context
financial_policy_version
```

Output:

```text
rounded value
rounding delta
policy metadata
```

---

# 41. Rounding Contexts

Initial explicit contexts:

```text
MoneySettlement
LineNetAmount
DocumentSubtotal
DocumentTotal
TaxLine
TaxTotal
FXBaseAmount
PaymentScheduleLine
PaymentAllocation
CashRounding
```

Tax-specific contexts are finalized in Tax Pack.

---

# 42. No Hidden Global Midpoint Default

Every Financial Rounding Policy stores its rounding mode explicitly.

Supported engine modes include:

```text
ToEven
AwayFromZero
ToZero
ToPositiveInfinity
ToNegativeInfinity
```

Commercial/legal policy determines which is used.

---

# 43. Country Pack and Rounding

Country Pack can:

- seed recommended/legal policy;
- constrain allowed choices;
- define statutory Tax rounding requirements.

Global Core does not hardcode country law.

---

# 44. Financial Rounding Policy

Versioned entity:

```text
FinancialRoundingPolicy
```

Fields conceptually:

```text
id
tenant_id
legal_entity_id
version
effective_from
effective_to?
money_rounding_mode
line_rounding_mode
document_rounding_mode
fx_rounding_mode
tax_rounding_strategy?
cash_rounding_rule?
status
```

---

# 45. Policy Snapshot

Posted/final documents preserve:

```text
rounding_policy_id
rounding_policy_version
relevant resolved values
```

Changing policy later does not change old documents.

---

# 46. Per-Line vs Per-Document

This is a real accounting/compliance difference.

Example:

```text
Round every line then sum
```

can differ from:

```text
Sum exact lines then round total
```

Therefore the strategy is explicit and versioned.

---

# 47. Rounding Residual

When component rounding creates a residual:

```text
0.01
```

the system uses deterministic allocation rules.

Never silently lose it.

The relevant downstream module defines where the residual is assigned.

---

# 48. Database Must Not Be Accidental Rounding Engine

Persist values after backend has explicitly applied the required policy.

Database column scale is a safety/storage rule, not business rounding logic.

---

# 49. Exchange Rate Business Model

An exchange rate means:

```text
TO amount = FROM amount × Rate
```

Canonical stored direction is explicit.

Example:

```text
FROM USD
TO EGP
Rate 52.40
```

means:

```text
1 USD = 52.40 EGP
```

---

# 50. Never Store Ambiguous "Rate"

Every rate carries:

```text
from_currency
to_currency
rate
```

No field simply called `exchange_rate` without direction in domain contracts.

---

# 51. Base Conversion Convention

For normal foreign-currency posting:

```text
Transaction Currency → Legal Entity Base Currency
```

Example:

```text
USD → EGP
```

---

# 52. Base Amount Calculation

Canonical raw conversion:

```text
base_amount_raw = transaction_amount × rate
```

Then explicit FX rounding policy applies.

---

# 53. Same Currency

If:

```text
Transaction Currency == Base Currency
```

then:

```text
rate = 1
```

No rate lookup.

Multicurrency UI should disappear for simple single-currency businesses.

---

# 54. Rate Is Not Universal Across All Purposes

A business may legitimately have different rate contexts:

```text
Accounting rate
Bank actual execution rate
Contractual rate
Tax-authority rate
Settlement rate
```

This is NOT multiple hidden sources of truth if modeled explicitly.

---

# 55. Exchange Rate Purpose

Every rate has an explicit purpose/type.

Initial concepts:

```text
Accounting
Commercial
Contractual
BankActual
Settlement
Tax
```

Not all need to be exposed in MVP UX.

---

# 56. Purpose Ownership

FinBase owns rate storage/resolution infrastructure.

Tax can request a Tax-purpose rate according to Country Pack rules.

Payments can provide BankActual rate from bank/payment data.

Ledger uses Accounting/Settlement snapshots produced by the relevant flow.

---

# 57. No Module Has Its Own FX Table

Forbidden:

```text
sales_exchange_rate
invoice_exchange_rate
payment_exchange_rate
ledger_exchange_rate
```

All use FinBase contracts and explicit snapshots.

---

# 58. Exchange Rate Record

Concept:

```text
ExchangeRate
{
  id
  tenant_id
  legal_entity_id

  from_currency
  to_currency
  purpose

  rate

  effective_date
  effective_at?

  source_type
  source_ref?
  provider_code?

  acquired_at
  approved_at?
  approved_by?

  status
  version
}
```

---

# 59. Rate Status

```text
Draft
Approved
Superseded
Rejected
```

Only approved/current-eligible rates are used in authoritative financial finalization.

---

# 60. Rate Source Types

```text
Manual
ExternalProvider
Bank
Contract
Government/TaxProvider
Imported
```

---

# 61. External Provider Is Not Source of Historical Truth

Provider gives data.

Once approved/accepted for a transaction:

the persisted local Rate Snapshot is authoritative for that transaction.

---

# 62. Never Call Live FX Provider During Posting

Posting/finalization must not depend on:

```text
internet request → external FX API
```

Reasons:

- latency;
- outage;
- rate changes mid-request;
- reproducibility;
- audit.

---

# 63. Exchange Rate Sync

Provider rates are fetched asynchronously:

```text
Quartz schedule
→ Worker
→ Provider adapter
→ Validate
→ Persist Rate
→ Audit
```

RabbitMQ can distribute refresh/import work.

---

# 64. Rate Provider Failure

If provider is down:

- existing approved rate may be used if policy says it is still valid;
- otherwise transaction finalization blocks with clear error;
- no silent rate=1;
- no random fallback provider without configured priority.

---

# 65. Rate Staleness

Financial policy defines:

```text
max_rate_age
missing_weekend_policy
allow_previous_business_day
```

Example:

use latest approved rate on or before document date within allowed staleness.

Never use a future rate.

---

# 66. Weekend / Holiday Gaps

Providers may not publish every calendar day.

Rate resolver can select:

```text
latest approved rate <= required date
```

subject to policy.

The selected effective date is snapshotted.

---

# 67. Manual Rate Override

Allowed only when Entity policy permits.

Requires:

```text
permission
reason
audit
```

Potential additional approval later.

---

# 68. Manual Override UX

User should not type a rate on every document by default.

Normal UX:

```text
Currency: USD
Rate: 52.40 [system]
```

Advanced authorized user can:

```text
Override
Reason
```

---

# 69. Rate Reciprocal

Do not fetch/store an unrelated reverse rate for the same snapshot.

Given:

```text
USD → EGP = 52.40
```

reverse for display can be derived from that exact snapshot.

Avoid inconsistent pair:

```text
USD→EGP = 52.40
EGP→USD = 0.020
```

from two unrelated sources.

---

# 70. Cross-Currency Conversion

If transaction requires A → B and neither is Base:

preferred controlled triangulation:

```text
A → Base → B
```

only if policy allows.

Snapshot stores both component rates/path.

---

# 71. No Arbitrary Rate Chaining

Do not chain:

```text
A→B→C→D
```

through whatever rates exist.

This creates hidden error/rounding.

---

# 72. Rate Snapshot

Immutable value embedded/stored with finalized financial operation:

```text
ExchangeRateSnapshot
{
  from_currency
  to_currency
  rate
  purpose
  effective_date
  source_type
  source_ref
  approved_rate_id?
  manual_override
  captured_at
}
```

---

# 73. Snapshot Is Historical Truth

Later editing/deleting the master rate cannot change:

```text
posted invoice
posted payment
posted ledger fact
```

---

# 74. Exchange Rate Corrections

Approved/used rate rows should not be destructively overwritten.

Correction creates:

- superseding record/version;
- explicit effective metadata;
- audit.

Historical snapshots stay unchanged.

---

# 75. Realized FX Is Not FinBase Posting

FinBase calculates/converts values.

Subledger/Settlement determines settlement difference.

Ledger posts realized FX gain/loss.

---

# 76. Unrealized FX Is Not FinBase Posting

Ledger later owns foreign-balance revaluation and postings.

FinBase supplies rate resolution.

---

# 77. Transaction vs Base Amount

Financial documents/facts that need multicurrency preserve both where relevant:

```text
TransactionAmount
TransactionCurrency

BaseAmount
BaseCurrency

ExchangeRateSnapshot
```

---

# 78. Third Currency / Reporting Currency

Later reporting currency is separate.

Do not overload:

```text
BaseAmount
```

to mean multiple things.

---

# 79. Payment Terms Business Definition

Payment Terms answer:

> **When is one financial document amount due?**

They do not mean:

- Payment Method.
- Credit Limit.
- Bank Account.
- Down-payment invoice policy.
- Collection strategy.

---

# 80. Payment Term vs Payment Method

Payment Term:

```text
Net 30
50% now / 50% after 30 days
```

Payment Method:

```text
Cash
Bank Transfer
Card
Cheque
```

Different domains.

---

# 81. Payment Term vs Down Payment

Example:

```text
30% down-payment invoice
70% final invoice
```

may be an invoicing policy producing multiple invoices.

That is not necessarily the same as one invoice with two due installments.

Do not conflate.

---

# 82. Payment Term Ownership

FinBase owns:

```text
PaymentTermDefinition
```

Default assignment belongs to business/financial profile.

Examples:

```text
Customer Billing Profile → Net 30
Supplier Profile → Net 60
```

---

# 83. Document Snapshot

When document is finalized, it stores:

```text
payment_term_id/version
resolved payment schedule
```

Changing Customer default later does not change old invoice.

---

# 84. Payment Term Versioning

Editing a used Payment Term must not rewrite history.

Use versioned/effective definitions.

---

# 85. Initial Payment Term Examples

```text
Due on Receipt
Net 7
Net 15
Net 30
Net 45
Net 60
End of Month
30% Immediate + 70% Net 30
30/60/90 installments
```

---

# 86. Payment Term Rule Types

MVP rule allocation:

```text
Percentage
Balance
```

FixedAmount can be designed but may be deferred if not needed.

---

# 87. Why `Balance` Rule Matters

For:

```text
33.33%
33.33%
33.34%
```

we should not rely on manually matching percentages forever.

Preferred:

```text
33.33%
33.33%
Balance
```

Final line absorbs deterministic residual.

---

# 88. Payment Schedule Exact Sum

Invariant:

```text
SUM(schedule.amount) == document.total
```

always.

---

# 89. Installment Allocation Algorithm

1. Work from authoritative document Money total.
2. Calculate ordered percentage lines using explicit PaymentSchedule rounding.
3. Sum rounded lines.
4. Final Balance line = total - prior lines.
5. Validate no invalid sign/overshoot.
6. Omit meaningless zero-value lines if policy allows.
7. Return immutable schedule.

---

# 90. Tiny Amount Example

Invoice:

```text
0.01
```

Term:

```text
50%
50%
```

Cannot produce:

```text
0.01 + 0.01
```

The final Balance algorithm guarantees exact total.

---

# 91. Negative Document / Credit Note

Payment schedule engine supports signed amounts deterministically.

Consuming module decides how credit notes become settlement/open items.

---

# 92. Due Date Basis

A Payment Term rule declares its date basis.

Initial supported basis:

```text
DocumentDate
```

Extensible to:

```text
InvoiceDate
DeliveryDate
Custom supplied business date
```

Consumers must explicitly provide required date context.

---

# 93. Financial Dates Are Different Concepts

Never use one generic `Date`.

Potential financial dates:

```text
DocumentDate
PostingDate
TaxDate
RateDate
DueDate
PaymentDate
```

Modules own relevant dates.

FinBase contracts make the distinction explicit.

---

# 94. Due Date Rules

Initial rule types:

```text
Immediate
DaysAfterDate
EndOfMonth
DaysAfterEndOfMonth
DayOfFollowingMonth
```

Keep deterministic.

---

# 95. Business-Day Adjustment

Do not silently move a due date because it falls on weekend.

MVP:

calendar due date.

Future optional:

```text
BusinessDayConvention + CalendarProvider
```

when actual business/legal requirement is confirmed.

---

# 96. Early Payment Discount

Design the contract now.

Potential:

```text
2% if paid within 10 days; otherwise Net 30
```

But actual tax/accounting impact can differ by country.

Therefore:

- FinBase can define discount eligibility structure.
- Tax Pack defines tax impact.
- Settlement applies the discount.
- Ledger posts effects.

---

# 97. MVP Early Discount Decision

Core Payment Schedule supports the extension seam.

Full early-discount accounting implementation is **Build Later unless launch requirements demand it**.

Do not fake it with generic discount logic.

---

# 98. Payment Term Calculation Must Be Pure

Given:

```text
Total
Currency
TermVersion
DateContext
RoundingPolicy
```

result must be deterministic.

No database lookup inside the pure calculation after the context is resolved.

---

# 99. Payment Schedule Result

```text
PaymentSchedule
{
  term_id
  term_version
  basis_date
  currency
  lines[]
}
```

Line:

```text
sequence
amount
due_date
rule_snapshot
```

---

# 100. Payment Term UI

Normal business user sees:

```text
Payment Terms: Net 30
Due: 04 Nov 2026
```

or:

```text
30,000 due today
40,000 due 04 Nov
30,000 due 04 Dec
```

No formula syntax.

---

# 101. Payment Term Admin UX

Finance admin can edit structured rules with live preview.

Example:

```text
30%  → Today
40%  → +30 days
Balance → +60 days
```

Preview on example amount.

---

# 102. No Free-Text Payment Formula

Do not store:

```text
"30% now and the rest later"
```

as execution truth.

Labels/descriptions are display only.

Rules are structured.

---

# 103. Financial Policy Per Legal Entity

FinBase owns a policy record referencing:

```text
rounding policy
exchange-rate policy
allowed currencies
payment term defaults/templates where appropriate
```

It MUST NOT duplicate LegalEntity Base Currency.

---

# 104. Financial Policy Versioning

Policy changes are:

```text
Draft
Approved
Effective
Superseded
```

where risk requires.

Simple non-critical settings can use standard versioning.

---

# 105. Financial Go-Live Gate

Before first real financial posting, must be locked:

- Legal Entity Base Currency.
- currency dataset.
- rounding policy.
- FX policy.
- accounting rate source.
- payment-term baseline.
- Money numeric contract.

---

# 106. Single-Currency Company UX

If entity only uses Base Currency:

User should not see:

```text
Exchange Rate
Base Amount
Rate Source
```

Normal flows show only:

```text
EGP amounts
```

Internally:

```text
rate = 1
```

---

# 107. Multi-Currency UX

When foreign currency selected:

show only useful information.

Sales user:

```text
Currency: USD
Total: 1,000 USD
```

Maybe base equivalent in secondary text.

Finance user:

```text
USD 1,000
Rate 52.40
Base EGP 52,400
Source / Date
```

Permissions and role shape experience.

---

# 108. Advanced Details Are Collapsible

Do not put FX audit details in every invoice form.

Advanced Finance panel can expose:

- Rate Purpose.
- Source.
- Effective Date.
- Override reason.
- Base equivalent.

---

# 109. Backend Authoritative Calculation Boundary

Frontend sends inputs.

Backend returns:

```text
CalculationResult
```

containing canonical:

- transaction amount;
- base amount;
- rate snapshot;
- rounding results;
- payment schedule.

---

# 110. Recalculation UX

For interactive document editing:

- local decimal preview provides instant feedback;
- backend calculation request is debounced/on meaningful changes;
- final Save/Confirm/Post always recalculates server-side.

---

# 111. No Stale Confirmation

Confirm/Post command must validate calculation version/input hash.

If inputs changed after preview:

server recalculates.

---

# 112. Calculation Fingerprint

Financial calculation result can include:

```text
calculation_version
input_hash
policy_version
```

Useful for detecting stale frontend results.

---

# 113. Deterministic Calculation Version

Each calculation engine contract has version.

Example:

```text
money_math_v1
payment_schedule_v1
currency_conversion_v1
```

Breaking algorithm change requires explicit version/ADR.

---

# 114. No Time-Dependent Hidden Inputs

Pure calculation must not call:

```text
DateTime.Now
latest rate
current settings
```

mid-calculation.

All context is resolved first and passed explicitly.

---

# 115. Currency Conversion Service

Concept:

```csharp
ICurrencyConverter
```

Input:

```text
Money source
target currency
ExchangeRateSnapshot
RoundingContext
```

Output:

```text
Money converted
raw amount optional
rounding delta
```

---

# 116. Exchange Rate Resolver

Concept:

```csharp
IExchangeRateResolver
```

Input:

```text
tenant
legal entity
from currency
to currency
purpose
required business date
```

Output:

```text
Approved ExchangeRateSnapshot
```

or explicit failure.

---

# 117. Financial Policy Provider

```csharp
IFinancialPolicyProvider
```

returns cached resolved policy by:

```text
tenant + legal entity + effective date
```

---

# 118. Currency Catalog Contract

```csharp
ICurrencyCatalog
```

supports:

- get metadata;
- validate code;
- enabled currencies;
- minor unit;
- display metadata.

---

# 119. Payment Term Calculator

```csharp
IPaymentTermCalculator
```

pure after definition/context resolution.

---

# 120. No God Financial Service

Do NOT create:

```text
FinancialService.DoEverything()
```

Keep focused primitives.

---

# 121. Database Schema

Use:

```text
foundation_finbase
```

except Money value object which is embedded in consuming schemas.

---

# 122. Core Tables

```text
currency_reference          -- system/reference scope
legal_entity_currency

financial_rounding_policy
financial_policy

exchange_rate
exchange_rate_source_config

payment_term
payment_term_version
payment_term_rule
```

Optional operational:

```text
exchange_rate_import_batch
```

---

# 123. `currency_reference`

System-controlled, not tenant-owned business data.

```text
code
numeric_code
name
minor_unit
status
valid_from?
valid_to?
dataset_version
```

---

# 124. `legal_entity_currency`

Tenant-owned / RLS:

```text
tenant_id
legal_entity_id
currency_code
enabled
allow_sales
allow_purchase
allow_payment
allow_manual_rate
rate_policy_ref?
status
version
```

---

# 125. Base Currency Is NOT In This Table as a Second Truth

The table may indicate:

```text
is_base derived/read-only
```

in UI/read model.

But the canonical field is:

```text
LegalEntity.base_currency_code
```

---

# 126. `financial_rounding_policy`

```text
id
tenant_id
legal_entity_id
version
effective_from
effective_to

money_mode
line_strategy
document_strategy
fx_mode
cash_rounding_increment?
status

created_by
approved_by?
created_at
```

---

# 127. `financial_policy`

```text
id
tenant_id
legal_entity_id
rounding_policy_id
default_exchange_rate_policy
max_rate_age_days?
allow_manual_override
status
version
```

No duplicate base currency.

---

# 128. `exchange_rate`

```text
id
tenant_id
legal_entity_id

from_currency
to_currency
purpose
rate

effective_date
effective_at?

source_type
provider_code?
source_reference?

status
approved_by?
approved_at?
acquired_at

version
created_at
```

---

# 129. Exchange Rate Indexes

Critical:

```text
(tenant_id, legal_entity_id, from_currency, to_currency, purpose, effective_date desc)
```

and approval/status filtering strategy.

---

# 130. Exchange Rate Uniqueness

Prevent ambiguous approved duplicates for same:

```text
Entity
Pair
Purpose
Effective timestamp/date
Source priority context
```

Exact constraint finalized with provider policy.

---

# 131. `payment_term`

Stable identity:

```text
id
tenant_id
code
display_name
status
```

---

# 132. `payment_term_version`

```text
id
tenant_id
payment_term_id
version
effective_from
effective_to?
status
description?
```

---

# 133. `payment_term_rule`

```text
id
tenant_id
payment_term_version_id
sequence

allocation_type
percentage?
fixed_amount?
is_balance

date_rule_type
days?
day_of_month?
month_offset?

discount_definition?
```

---

# 134. Payment Term Validation

Before activation:

- at most one Balance final rule;
- percentage valid;
- no impossible date config;
- allocations cannot structurally exceed total;
- rule ordering deterministic.

---

# 135. RLS

All tenant/entity-owned FinBase tables:

```text
ENABLE RLS
FORCE RLS
```

Runtime role non-owner.

`currency_reference` is intentional system/reference data and uses separate protection model.

---

# 136. Cross-Entity Isolation

Entity A's manual/approved rate must not silently be used for Entity B unless policy explicitly uses shared system/provider reference.

Resolver always receives LegalEntityId.

---

# 137. Shared Provider Data Optimization

A provider feed may be stored as system market data separately later.

But an Entity's authoritative financial selection still goes through its policy and creates its own snapshot.

Do not let optimization erase entity policy.

---

# 138. Exchange Rate Caching

Currency/rate resolution is hot.

Use:

- local memory cache;
- version-aware keys;
- bounded TTL;
- invalidation on approved rate/policy change.

Cache key includes:

```text
tenant
legal entity
pair
purpose
effective date
policy version
```

---

# 139. Do Not Introduce Redis Just for FX

Reference/config data is small.

In-process cache + DB indexes first.

Distributed cache later only with measured need.

---

# 140. Currency Metadata Cache

System currency metadata can be cached aggressively by dataset version.

---

# 141. Payment Term Cache

Active immutable/versioned Term definitions can be cached.

Cache by:

```text
tenant + term_version_id
```

---

# 142. Calculation Context Pre-Resolution

For a document:

resolve once:

```text
Base Currency
Transaction Currency
Currency metadata
Rounding Policy
Exchange Rate Snapshot
Payment Term Version
```

Then calculate all lines in memory.

Do not query FinBase per line.

---

# 143. Performance Rule

A 500-line invoice must not execute 500 currency/rate/policy queries.

Financial context is resolved once per calculation scope.

---

# 144. Batch Currency Conversion

For reporting/import workflows, expose batch conversion.

Avoid one DB rate lookup per row.

---

# 145. Rate Provider Adapter

Interface:

```csharp
IExchangeRateProvider
```

External adapters live behind Ports.

Provider returns normalized candidate rates.

FinBase validates/persists.

---

# 146. Provider Credentials

Secrets go to Platform secret/credential vault.

Never in `exchange_rate_source_config` plaintext.

---

# 147. Provider Configuration

Stores:

```text
provider code
schedule
supported purpose
priority
staleness policy
credential reference
enabled
```

---

# 148. Provider Priority

Explicit ordered policy.

Example:

```text
1 Contract Rate
2 Approved Manual Rate
3 Official/Configured Provider
```

Do not silently select any available rate.

---

# 149. Provider Refresh Frequency

Daily/periodic according to business need.

Do not assume real-time market FX is necessary for accounting.

---

# 150. Rate Audit

Must record:

- source;
- acquired time;
- effective date;
- who approved/overrode;
- previous value;
- reason.

---

# 151. Permissions

Initial:

```text
finbase.currency.read
finbase.currency.enable

finbase.rate.read
finbase.rate.manage
finbase.rate.approve
finbase.rate.override

finbase.rounding_policy.read
finbase.rounding_policy.manage

finbase.payment_term.read
finbase.payment_term.manage

finbase.financial_policy.manage
finbase.audit.view
```

---

# 152. Base Currency Permission

Base currency configuration before go-live requires elevated:

```text
finbase.base_currency.configure
```

After go-live API blocks even privileged normal user.

---

# 153. Break-Glass Is Not Base Currency Editor

Support/admin impersonation cannot casually bypass financial invariants.

A post-go-live base-currency migration is a separate controlled process.

---

# 154. Role-Based UX

Small-business Owner:

- sees simple currency setting at company setup;
- common payment terms;
- no FX administration if single-currency.

Finance Admin:

- currencies;
- approved rates;
- payment terms;
- rounding policy;
- audit.

Sales/Purchasing:

- choose allowed currency/term as permitted;
- no policy internals.

---

# 155. Company Setup Wizard

At Legal Entity setup ask:

```text
Country
Base Currency
```

If country strongly suggests a currency, prefill but require confirmation.

Show warning:

> Base Currency becomes locked after financial go-live.

---

# 156. Do Not Hide Meaning of Base Currency

Setup text should explain:

> "العملة الأساسية التي ستُقاس بها الحسابات والتقارير المحاسبية لهذا الكيان."

Not technical jargon.

---

# 157. Single Currency Default

For most small companies:

- Base Currency selected.
- other currencies disabled.
- no FX screens in daily workflow.
- default Payment Term templates simple.

---

# 158. Enabling Foreign Currency

Admin action:

```text
Enable USD
```

may ask:

- Rate source.
- Manual override allowed?
- refresh policy.

Advanced defaults can be preconfigured.

---

# 159. Transaction Currency

One commercial document normally has one transaction currency.

Do not allow arbitrary currency per line in standard invoice/order.

Special domains require explicit design.

---

# 160. Currency Change on Draft

Changing document currency:

- rerun Pricing later;
- rerun FX resolution;
- rerun Tax context if relevant;
- recalculate totals.

Never simply relabel amount.

---

# 161. Currency Change After Finalization

Blocked.

Correction via reversal/new document according to module rules.

---

# 162. Rate Change on Draft

Draft may refresh to newer rate according to policy.

UI shows if rate changed materially.

---

# 163. Rate Freeze Point

Each consuming module defines freeze moment.

Financial finalization/posting uses a frozen snapshot.

Typical:

```text
Invoice finalization/posting
```

---

# 164. Sales Order Rate Snapshot

Sales Pack later decides commercial exposure behavior.

Do not assume Sales Order accounting rate = Invoice accounting rate.

Catalog/FinBase supports explicit snapshots.

---

# 165. Pricing Currency vs Document Currency

Pricing later may have price list currency.

If it differs from document currency:

Pricing requests explicit FinBase conversion context.

No hidden conversion.

---

# 166. Tax Currency

Tax Pack may require conversion into statutory/tax currency using specific rate policy.

FinBase provides the infrastructure.

---

# 167. Payment Currency

Payment may be in a different currency from invoice.

Payments/Subledger Pack handles settlement semantics.

FinBase supplies rates.

---

# 168. Bank Actual Rate

If bank settlement reveals actual rate/fees:

store as explicit BankActual/Settlement context.

Do not retroactively change invoice snapshot.

---

# 169. FX Difference Concept

Invoice:

```text
1,000 USD
historic base = 52,400 EGP
```

Payment date:

```text
1,000 USD
payment/base equivalent = 53,000 EGP
```

Difference:

```text
600 EGP
```

FinBase supplies conversion data.

Subledger/Ledger decides realized FX posting.

---

# 170. Revaluation Concept

Open foreign balances may need period-end revaluation.

Ledger owns the job and entries.

FinBase resolves approved closing rate.

---



# 171. Reporting Currency Later

Additional reporting currency is a Ledger/Reporting feature.

FinBase can provide conversion primitives but does not own financial statement translation.

---

# 172. Currency Rate Date Policy

Each use case asks FinBase for a rate using an explicit business date.

Examples:

```text
Quotation Date
Invoice Date
Posting Date
Settlement Date
Period End Date
Tax Date
```

The consuming domain defines which date applies.

---

# 173. No `DateTime.Now` in Financial Rate Selection

Use explicit NodaTime values:

```text
LocalDate businessDate
Instant acquiredAt
DateTimeZone legalEntityZone
```

---

# 174. Legal Entity Timezone

Business date interpretation uses Legal Entity timezone where a date derives from timestamp.

Do not use server timezone.

---

# 175. Financial Date Value Objects

Recommended explicit types/contracts where useful:

```text
DocumentDate
PostingDate
RateDate
DueDate
```

At minimum avoid generic ambiguous parameters.

---

# 176. Calculation Pipeline Principle

Each financial operation follows:

```text
Validate Inputs
→ Resolve Immutable Context
→ Calculate High Precision
→ Apply Explicit Rounding
→ Reconcile Residuals
→ Produce Result
→ Snapshot
```

---

# 177. High Precision First

Intermediate calculations remain high precision.

Do not round after every multiplication unless policy requires line-level rounding.

---

# 178. Round at Defined Boundaries

Examples:

```text
Line Net Boundary
Tax Boundary
Document Total Boundary
Base Currency Boundary
Payment Schedule Line Boundary
```

Tax Pack finalizes tax-specific sequence.

---

# 179. No Repeated Round-Convert-Round Chains

Avoid:

```text
round
→ convert
→ round
→ convert back
→ round
```

unless a business/legal process explicitly requires it.

Every conversion path must be documented.

---

# 180. Financial Calculation Context

Concept:

```text
FinancialCalculationContext
{
  TenantId
  LegalEntityId

  BaseCurrency
  TransactionCurrency

  CurrencyMetadata
  RoundingPolicySnapshot
  ExchangeRateSnapshot?

  BusinessDate
  CalculationVersion
}
```

---

# 181. Immutable Context During Calculation

Once calculation starts:

policy/rate cannot change mid-loop.

Context is immutable.

---

# 182. Calculation Result

Concept:

```text
FinancialCalculationResult
{
  TransactionAmount
  BaseAmount
  RateSnapshot
  RoundingSummary
  PolicyVersion
  CalculationVersion
}
```

Consumer adds domain-specific totals.

---

# 183. Rounding Summary

Useful audit/debug metadata:

```text
raw_amount
rounded_amount
rounding_delta
context
mode
scale
```

Do not necessarily persist every intermediate in all documents.

Persist what is needed to reproduce/justify financial result.

---

# 184. Calculation Trace — Debug Only

For finance-support/test environments, calculation engine can produce structured trace:

```text
Input
Formula step
Raw value
Rounding
Output
```

Sensitive/business data controls apply.

---

# 185. Production Calculation Trace

Do not store huge traces for every document by default.

Store:

- final snapshots;
- versions;
- required audit.

Enable detailed trace selectively.

---

# 186. Calculation Determinism Test

Given serialized:

```text
inputs
policy snapshot
rate snapshot
engine version
```

replay must return exact same canonical result.

---

# 187. Calculation Golden Vectors

Create version-controlled test vectors.

Example categories:

```text
Basic Money
Rounding midpoint
Negative values
Zero-decimal currency
Three-decimal currency
FX
Payment schedule
Tiny amounts
Large amounts
```

Every financial module must run against shared vectors.

---

# 188. Financial Math Library

Approved primitives can include:

```text
Money.Add
Money.Subtract
Money.Multiply(decimal)
Money.Negate
Money.CompareSameCurrency

UnitPrice × Quantity
Percentage.Apply

RoundMoney
ConvertCurrency
```

No broad invoice-specific logic here.

---

# 189. Unit Price × Quantity

Input:

```text
UnitPrice(currency, high precision)
Quantity(decimal)
```

Output before policy rounding:

```text
high-precision monetary decimal
```

Then consuming calculation context chooses rounding boundary.

---

# 190. Percentage

Store percentage as decimal ratio with explicit convention.

Recommended contract:

```text
0.15 = 15%
```

or explicit Percent value object.

Do not mix:

```text
15
0.15
```

between modules.

---

# 191. Percentage Value Object

Preferred:

```text
Percentage.FromPercent(15)
Percentage.FromRatio(0.15)
```

Internally canonical ratio.

---

# 192. Division by Zero

Financial primitives fail explicitly.

No Infinity / NaN financial state.

---

# 193. Numeric Overflow

Backend detects decimal overflow and returns controlled error.

Database precision overflow must not be normal business validation.

---

# 194. Maximum Amount Policy

Define application limits appropriate to product scale.

Example not hardcoded globally until validated.

But request validation must reject absurd values before DB errors.

---

# 195. Currency Code Value Object

```text
CurrencyCode
```

validates:

- canonical uppercase format;
- exists in Currency Catalog where business use requires.

Avoid arbitrary string throughout code.

---

# 196. Money Serialization Version

Public Contracts use stable:

```json
{
  "amount": "100.00",
  "currency": "USD"
}
```

No localized commas in API.

---

# 197. ExchangeRate Serialization

```json
{
  "fromCurrency": "USD",
  "toCurrency": "EGP",
  "rate": "52.400000000000",
  "effectiveDate": "2026-10-05",
  "purpose": "Accounting"
}
```

---

# 198. Payment Schedule Serialization

```json
{
  "currency": "EGP",
  "total": "100000.00",
  "lines": [
    {"sequence":1,"amount":"30000.00","dueDate":"2026-10-05"},
    {"sequence":2,"amount":"40000.00","dueDate":"2026-11-04"},
    {"sequence":3,"amount":"30000.00","dueDate":"2026-12-04"}
  ]
}
```

---

# 199. REST API Surface

Base:

```text
/api/v1/finbase
```

Admin endpoints may include:

```text
GET  /currencies
GET  /legal-entities/{entityId}/currencies
POST /legal-entities/{entityId}/currencies/{code}/enable

GET  /legal-entities/{entityId}/exchange-rates
POST /legal-entities/{entityId}/exchange-rates
POST /exchange-rates/{id}/approve

GET  /payment-terms
POST /payment-terms
GET  /payment-terms/{id}

GET  /legal-entities/{entityId}/financial-policy
PUT  /legal-entities/{entityId}/financial-policy
```

---

# 200. Calculation APIs

Normal modules should use in-process contracts.

Public/internal preview endpoints may include:

```text
POST /conversion/preview
POST /payment-terms/{id}/preview
```

Do not turn internal hot-path financial arithmetic into HTTP calls inside Modular Monolith.

---

# 201. Commands

```text
EnableCurrency
DisableCurrency

CreateExchangeRate
ApproveExchangeRate
RejectExchangeRate
OverrideExchangeRate

CreatePaymentTerm
CreatePaymentTermVersion
ActivatePaymentTermVersion
ArchivePaymentTerm

ConfigureFinancialPolicy
ActivateRoundingPolicy
```

---

# 202. Queries

```text
GetCurrency
ListEnabledCurrencies

ResolveExchangeRate
ListExchangeRates

GetFinancialPolicy
GetRoundingPolicy

GetPaymentTerm
ListPaymentTerms
PreviewPaymentSchedule
```

---

# 203. Contracts Project

```text
Todir.Foundation.FinBase.Contracts
```

contains:

```text
Currency DTOs
ExchangeRateSnapshot
FinancialPolicySnapshot
PaymentTermSnapshot
PaymentSchedule
Ports / Queries
```

Money primitives remain in approved shared primitive library.

---

# 204. Cross-Module Dependency Rule

Pricing/Tax/Ledger/Invoicing/Payments depend on FinBase Contracts.

FinBase never depends on their implementation.

---

# 205. FinBase Manifest

Concept:

```json
{
  "id": "finbase",
  "type": "foundation",
  "purchasable": false,
  "dependencies": {
    "hard": ["platform-kernel", "organization"]
  },
  "owns": {
    "schema": "foundation_finbase"
  }
}
```

Exact IDs follow repository conventions.

---

# 206. Organization Dependency

FinBase needs LegalEntity identity and canonical base currency.

Organization does not depend on FinBase implementation.

Base-currency configuration workflow may be composed at application layer.

---

# 207. Base Currency Validation Hook

When user sets:

```text
LegalEntity.base_currency_code
```

system validates against Currency Catalog.

This does not create second ownership.

---

# 208. Go-Live Lock Hook

Organization update pipeline asks:

```text
IFinancialGoLiveGuard
```

before changing base currency.

FinBase/Ledger can block based on financial state.

---

# 209. No Circular Dependency

Use capability/contract boundary so:

```text
Organization → FinBase → Organization
```

does not become compile-time cycle.

---

# 210. Payment Term Assignment Ownership

Directory Party does not own payment term.

Stable financial/business profile later owns default assignment.

FinBase only owns definition.

---

# 211. Sales Payment Term

Sales may select a commercial default.

Invoice snapshot becomes financial truth for that invoice.

---

# 212. Supplier Payment Term

Purchasing/financial profile may define supplier default.

Same FinBase definitions can be reused.

---

# 213. Term Override

Authorized user can override Payment Term on a document if domain permits.

Override is snapshot to document and audit by consuming module.

Does not mutate customer's default.

---

# 214. Payment Term Currency

Term definition is normally currency-agnostic for percentage schedules.

Fixed-amount rules, if implemented, require currency semantics.

This is one reason Percentage + Balance is preferred MVP.

---

# 215. Early Discount Currency

Discount is calculated in document currency.

Tax treatment is delegated to Tax Pack.

---

# 216. Payment Term Archive

Archived term:

- unavailable for new documents;
- historical snapshots remain;
- existing drafts may warn/re-resolve according to consuming module.

---

# 217. Exchange Rate Archive/Delete

Used approved rates are not hard-deleted.

Status superseded/invalid with audit.

---

# 218. Currency Disable

Disabling foreign currency blocks new usage.

Existing documents/history stay valid.

Cannot disable Base Currency.

---

# 219. Currency Historical Status

If ISO currency becomes obsolete, historical records remain readable.

Currency reference supports status/effective history.

---

# 220. Importing Exchange Rates

Support controlled CSV/provider import.

Validation:

- known currency;
- positive rate;
- correct pair;
- effective date;
- duplicate policy;
- decimal precision.

---

# 221. Rate Import Preview

Show:

```text
Create
Supersede
Duplicate
Invalid
Outside tolerance
```

---

# 222. Rate Tolerance

Optional policy can warn if new rate differs unusually from previous/provider rate.

Do not automatically reject legitimate market moves without explicit policy.

---

# 223. Rate Approval Workflow

For many SMEs:

provider rates may auto-approve under configured trusted source.

Manual rates may require explicit approval.

Enterprise can enable four-eyes later.

---

# 224. Four-Eyes Design

Future/optional:

```text
Creator != Approver
```

via approval policy.

Do not force every small business through approval bureaucracy.

---

# 225. Rounding Policy Admin Safety

Changing Rounding Policy affects future calculations.

UI must show:

```text
Effective from date
Affected new documents only
Historical documents unchanged
```

---

# 226. Rounding Policy Comparison

Before activation, preview same sample values under old/new policy.

Useful for finance admin.

---

# 227. Rounding Policy Country Lock

Country Pack can mark parts:

```text
required / non-editable
```

when legally verified.

---

# 228. Currency Precision vs Display Precision

Currency minor unit influences monetary settlement/display.

Unit Price can show more precision if needed.

Do not truncate a unit price just because currency uses two decimal payment units.

---

# 229. Example — High Precision Unit Price

```text
Unit Price = 0.333333 USD
Qty = 300
Raw = 99.999900 USD
Document policy → 100.00 USD
```

Do not store unit price as 0.33 and create 99.00.

---

# 230. Example — Installment Residual

Total:

```text
100.00
```

Term:

```text
33.33%
33.33%
Balance
```

Possible result:

```text
33.33
33.33
33.34
```

Sum exactly 100.00.

---

# 231. Example — Three-Decimal Currency

System gets minor-unit scale from CurrencyReference.

No assumption:

```text
all money has 2 decimals
```

---

# 232. Example — Zero-Decimal Currency

Same.

Money rounding policy uses currency metadata.

---

# 233. Example — Foreign Invoice

Entity:

```text
Base = EGP
```

Invoice:

```text
1,000 USD
```

Resolved:

```text
USD→EGP
52.40
```

Snapshot:

```text
Transaction = 1,000 USD
Base = 52,400 EGP
```

---

# 234. Example — Rate Changes Tomorrow

Tomorrow rate:

```text
52.80
```

Yesterday invoice stays:

```text
52.40 snapshot
```

New eligible transaction may use 52.80.

---

# 235. Example — Bank Actual Rate

Invoice historic base:

```text
52,400 EGP
```

Payment settles at:

```text
53,000 EGP equivalent
```

Do NOT rewrite invoice.

Settlement/Ledger records FX difference.

---

# 236. Example — Single-Currency SME

Entity:

```text
Base EGP
Enabled EGP only
```

User sees no FX setup in Sales.

All calculations use:

```text
currency=EGP
rate=1
```

Complexity hidden.

---

# 237. Example — Group Tenant

```text
Egypt Entity → EGP
UAE Entity → AED
```

Same user may have permissions in both.

When switching Entity, financial context changes.

Caches are Entity-keyed.

---

# 238. Entity Switch UX

Frontend clears/reloads:

```text
enabled currencies
financial policy
payment terms defaults where scoped
```

Never reuse Entity A rate in Entity B.

---

# 239. Financial Context in Background Jobs

Every financial job carries:

```text
tenant_id
legal_entity_id
business_date/purpose
```

before resolving FinBase.

---

# 240. Messaging

FinBase publishes meaningful events:

```text
CurrencyEnabledV1
ExchangeRateApprovedV1
FinancialPolicyChangedV1
RoundingPolicyActivatedV1
PaymentTermActivatedV1
```

Do not publish every read/calculation.

---

# 241. Rate Refresh Messaging

Provider sync may use:

```text
RefreshExchangeRatesV1
```

worker persists candidate/approved rates according to policy.

---

# 242. Outbox

FinBase state changes + integration events use Platform Outbox.

---

# 243. Idempotency

Provider imports use external/provider event key or:

```text
provider + pair + purpose + effective timestamp + source version
```

to avoid duplicates.

---

# 244. Rate Provider Retry

Transient provider failures:

- retry/backoff;
- no duplicate persisted rate;
- alert after threshold.

---

# 245. Provider Circuit/Failure Handling

Do not block API threads waiting on bad provider.

Provider jobs run worker-side.

---

# 246. Rate Freshness Alert

Alert/finance dashboard when required currency has no rate within policy.

---

# 247. Rate Missing User Error

Business-friendly:

> "لا يوجد سعر صرف معتمد لـ USD بتاريخ 5 أكتوبر 2026. راجع المالية أو حدّث أسعار الصرف."

Not:

```text
NullReferenceException
```

---

# 248. Financial Error Codes

Stable:

```text
finbase.currency_not_enabled
finbase.currency_mismatch
finbase.base_currency_locked

finbase.rate_missing
finbase.rate_stale
finbase.rate_not_approved
finbase.rate_override_not_allowed

finbase.rounding_policy_missing
finbase.rounding_policy_invalid

finbase.payment_term_invalid
finbase.payment_schedule_mismatch

finbase.amount_overflow
finbase.invalid_decimal
```

---

# 249. Logging

Never log sensitive full documents unnecessarily.

Log:

- entity;
- currencies;
- rate ID/version;
- policy version;
- correlation.

Amounts may be business-sensitive; structured logging classification applies.

---

# 250. Audit vs Log

Exchange-rate override and rounding policy change are Audit events.

Not merely logs.

---

# 251. Observability Metrics

Safe-cardinality:

```text
finbase_rate_resolution_duration
finbase_rate_missing_total
finbase_rate_stale_total
finbase_conversion_duration
finbase_payment_schedule_duration
finbase_calculation_error_total
finbase_provider_sync_failures_total
```

Do not use Tenant ID as Prometheus label.

---

# 252. Performance Target — Pure Calculation

Money rounding/conversion/schedule calculation should be negligible relative to DB/API overhead.

Measure in microbenchmarks but optimize readability/correctness first.

---

# 253. Performance Target — Rate Resolution

Normal local rate resolution should be indexed/cached and fast.

Initial service target:

```text
p95 < 50ms
```

inside backend for cache/DB local resolution under realistic load.

Not contractual until measured.

---

# 254. No Per-Line FX Resolution

Document with 1,000 lines:

resolve rate once.

---

# 255. No Per-Line Currency Metadata Lookup

Resolve/cached metadata once.

---

# 256. Payment Schedule Complexity

Number of rules is small.

Pure O(n) in terms of schedule lines.

---

# 257. Financial Context Cache Invalidation

Invalidate on:

- Base Currency pre-go-live change;
- policy activation;
- currency enable/disable;
- approved new rate relevant to latest cache.

---

# 258. Posted Document Read Performance

Posted document reads its own snapshots.

It does not need current FX lookup to display original totals.

This improves correctness and speed.

---

# 259. Historical Report Conversion

If a report asks current revalued equivalent, that's a different explicit query.

Do not overwrite/display as if original base amount changed.

---

# 260. DB Aggregation

Summing already-posted same-currency/base amounts in SQL is allowed.

Do not recreate line pricing/tax/FX business algorithms in SQL reports.

---

# 261. Financial Reporting Precision

Ledger Pack will define reporting aggregation/rounding.

FinBase supplies currency metadata.

---

# 262. No Duplicate Calculation Implementations

Code search/architecture test should detect prohibited direct rounding in financial modules where practical.

---

# 263. Static Analysis Rule

Consider analyzer / architecture test:

flag:

```text
Math.Round
decimal.Round
```

in financial domain projects outside approved rounding library.

Allow exceptions only with explicit suppressions/reason.

---

# 264. Static Analysis for `double`

Financial contracts/domain should reject `double`/`float` properties.

Architecture test can inspect public DTO/domain types.

---

# 265. Type Mapping Test

Integration test confirms:

```text
C# decimal ↔ PostgreSQL numeric
```

round-trip exact for selected precision/scale boundaries.

---

# 266. JSON Round-Trip Test

Financial decimal string:

backend → JSON → frontend parser → API → backend

must preserve exact value.

---

# 267. Frontend Decimal ADR

Create:

```text
ADR-FIN-001 Frontend Decimal Handling
```

Decision:

- financial API decimals as strings;
- stable decimal arithmetic library for previews;
- JavaScript Number forbidden for authoritative financial calculations.

---

# 268. Additional ADRs

```text
ADR-FIN-002 Legal Entity Base Currency Source of Truth
ADR-FIN-003 Money and Numeric Precision
ADR-FIN-004 Explicit Rounding Engine
ADR-FIN-005 Exchange Rate Direction & Purpose
ADR-FIN-006 Rate Snapshot & No Live Provider in Posting
ADR-FIN-007 Payment Term Versioning & Schedule Algorithm
ADR-FIN-008 Financial Calculation Determinism
ADR-FIN-009 Base Currency Go-Live Lock
```

---

# 269. Unit Test Matrix — Money

Test:

```text
same currency addition
same currency subtraction
currency mismatch
negative money
zero
large amount
decimal multiplication
comparison
serialization
```

---

# 270. Unit Test Matrix — Rounding

Include exact midpoint vectors:

```text
1.005
-1.005
2.675
tiny fractions
large fractions
```

for every supported rounding mode/context.

Do not rely on intuition.

---

# 271. Unit Test Matrix — Currency Minor Units

Use test/reference currencies covering:

```text
0
2
3
```

minor-unit scales.

Reference data comes from ISO dataset.

---

# 272. Unit Test Matrix — FX

Test:

```text
base-to-base = 1
foreign-to-base
negative money
zero
very small rate
large rate
reciprocal display
triangulation
future rate rejected
stale rate
manual override
```

---

# 273. Unit Test Matrix — Payment Terms

Test:

```text
Immediate
Net 30
EOM
30/60/90
30% + 70%
33.33/33.33/Balance
tiny total
negative credit
month boundary
year boundary
leap year
```

---

# 274. Property-Based Test — Schedule Sum

For broad generated valid inputs:

```text
SUM(lines.amount) == total
```

always.

---

# 275. Property-Based Test — Same Currency

Conversion where:

```text
from == to
```

must return same amount with rate 1 and no external lookup.

---

# 276. Property-Based Test — Determinism

Same serialized input/context repeated thousands of times returns identical decimal result.

---

# 277. Property-Based Test — No Future Rate

Resolver never chooses:

```text
effective_date > required_date
```

unless explicit future-contract rate mode is requested.

---

# 278. Integration Test — RLS

Tenant A cannot read/modify:

- B rates;
- B payment terms;
- B policies.

---

# 279. Integration Test — Entity Scope

Same Tenant, Entity A cannot accidentally resolve Entity B manual rate.

---

# 280. Integration Test — Base Currency Lock

Before go-live:

change allowed with permission.

After financial go-live:

blocked.

---

# 281. Integration Test — Rate Approval

Draft rate is not used by finalization resolver.

Approved rate is.

---

# 282. Integration Test — Outbox

Rate approval event published only after DB commit.

---

# 283. Integration Test — Provider Failure

External provider outage does not corrupt local approved rate data.

---

# 284. Integration Test — Concurrent Rate Approval

Two admins cannot create ambiguous same-priority approved truth without policy resolution.

---

# 285. Integration Test — Concurrent Policy Change

Optimistic concurrency prevents silent overwrite.

---

# 286. Golden Cross-Module Contract Tests

Later modules run same FinBase vectors:

```text
Pricing
Tax
Ledger
Invoicing
Payments
Inventory Valuation
Payroll
```

---

# 287. Finance Red-Team Scenario — Two Sources of Base Currency

Architecture test/document review must reject any new field like:

```text
invoice_company_currency
sales_base_currency_setting
ledger_default_currency
```

if it attempts to become independent master truth.

Snapshots are allowed; competing master settings are not.

---

# 288. Finance Red-Team Scenario — Wrong Rate Direction

Given:

```text
1 USD = 52.40 EGP
```

system must never interpret:

```text
1 EGP = 52.40 USD
```

Contracts carry explicit `from/to`.

---

# 289. Finance Red-Team Scenario — Stale Rate

If policy max age = 2 days and only 10-day-old rate exists:

finalization blocks.

No silent stale conversion.

---

# 290. Finance Red-Team Scenario — Provider Changes History

Provider later corrects historical rate.

Old posted document remains on original snapshot.

---

# 291. Finance Red-Team Scenario — 0.01 Installments

Tiny amount split across several terms must never sum to > total.

---

# 292. Finance Red-Team Scenario — Frontend Float

Test values like:

```text
0.1 + 0.2
```

must never make authoritative amount through JS Number path.

---

# 293. Finance Red-Team Scenario — Different Rounding Layers

Backend and DB should not independently round same value using different midpoint conventions.

Backend explicitly rounds before persist.

---

# 294. Finance Red-Team Scenario — Same Tenant, Two Entities

Entity A EGP and Entity B AED.

Switching entity must not reuse:

- rate;
- base currency;
- rounding policy;
- payment default context.

---

# 295. Finance Red-Team Scenario — Currency Relabel

Changing USD document to EUR cannot leave numeric amounts untouched as if equivalent.

Must trigger recalculation.

---

# 296. Finance Red-Team Scenario — Rate = Zero

Reject.

Rate must be > 0.

---

# 297. Finance Red-Team Scenario — Negative Rate

Reject.

---

# 298. Finance Red-Team Scenario — NaN/Infinity

Impossible in approved financial types/storage.

---

# 299. Finance Red-Team Scenario — Unauthorized Override

Sales user cannot manually override exchange rate unless explicitly granted.

---

# 300. Finance Red-Team Scenario — Payment Term Changed

Invoice created/finalized with Term v1.

Admin changes Term to v2.

Old invoice schedule unchanged.

---

# 301. Finance Red-Team Scenario — Rounding Policy Changed

Posted invoice remains on policy snapshot v1.

Future documents use v2.

---

# 302. Business Acceptance Scenario — Single Currency

Small Egyptian company:

```text
Base Currency EGP
```

User creates Sales invoice in EGP.

User does not see exchange rate.

Backend uses:

```text
EGP → EGP = 1
```

---

# 303. Business Acceptance Scenario — Foreign Purchase

Entity EGP.

Supplier invoice USD.

Finance sees:

- USD total;
- approved rate;
- EGP base amount.

Normal Purchasing user sees only needed fields.

---

# 304. Business Acceptance Scenario — Net 30

Invoice date:

```text
2026-10-05
```

Term:

```text
Net 30
```

Schedule returns deterministic due date:

```text
2026-11-04
```

based on agreed date formula implementation.

---

# 305. Business Acceptance Scenario — Installments

Total:

```text
100,000 EGP
```

Term:

```text
30% immediate
40% +30 days
Balance +60 days
```

Schedule:

```text
30,000
40,000
30,000
```

Exact sum.

---

# 306. Business Acceptance Scenario — Rate Missing

USD document cannot finalize.

User sees actionable message.

Finance admin can refresh/add/approve according to permissions.

---

# 307. Business Acceptance Scenario — Manual Contract Rate

Customer contract says fixed rate.

Authorized finance user selects/creates Contractual rate.

Snapshot identifies:

```text
purpose=Contractual
source=Contract
```

No ambiguity.

---

# 308. Business Acceptance Scenario — Company Group

Tenant has EGP and AED entities.

Same Payment Term template may be reusable.

Rates/policies remain Entity-scoped.

---

# 309. Business Acceptance Scenario — User Simplicity

Salesperson inputs:

```text
Qty
Unit Price
Discount
```

sees:

```text
Total
```

Does not see rounding engine internals.

---

# 310. Accountant Experience

Finance user can inspect:

```text
Transaction Currency
Base Currency
Rate
Rate Date
Rate Source
Policy Version
Base Amount
Payment Schedule
```

without exposing technical class names.

---

# 311. Audit Experience

Finance admin can answer:

> "ليه الفاتورة دي اتحسبت على 52.40؟"

System shows:

```text
Rate 52.40
Effective 2026-10-05
Source X
Approved by Y
Snapshot ID/version
```

---

# 312. Support Experience

Support can inspect calculation metadata with authorization.

No direct production DB patching.

---

# 313. FinBase Import

Importable:

```text
Payment Terms
Exchange Rates
Enabled currencies config where appropriate
```

Base currency import only during tenant/legal-entity onboarding before go-live.

---

# 314. Currency Dataset Update

Controlled platform update can:

- add currency;
- mark obsolete;
- update metadata.

Must not mutate transaction snapshots.

---

# 315. Exchange Rate Source Configuration Import

Enterprise setup can import config templates, excluding secrets.

Secrets bound separately.

---

# 316. Backup / Restore

FinBase rates/policies/terms are critical DB data covered by PostgreSQL PITR.

External provider can never be the only recoverable source.

---

# 317. Disaster Recovery

After restore:

- approved rate history remains;
- payment-term versions remain;
- snapshots in documents remain;
- provider sync resumes.

---

# 318. Financial Data Retention

Rates/policies used by financial documents are retained according to financial/legal retention requirements.

Do not purge merely because provider data is old.

---

# 319. Privacy

FinBase contains limited personal data.

Audit actor IDs may be personal references.

Normal privacy/access policy applies.

---

# 320. Security

Key controls:

- RLS;
- elevated policy permissions;
- audit overrides;
- MFA/step-up hooks for sensitive changes;
- secrets vault;
- no provider credentials in logs.

---

# 321. MFA / Step-Up

Consider step-up for:

- manual exchange-rate override above policy;
- rounding-policy activation;
- post-go-live financial migration operations.

Not every small admin edit requires friction.

---

# 322. Separation of Duties

Optional policy:

```text
Rate creator != approver
```

for larger companies.

Default SME flow can be simpler.

---

# 323. Financial Policy Change Effective Dating

Prefer:

```text
future effective date
```

rather than changing active policy in middle of historical period unexpectedly.

---

# 324. Backdated Document

If user creates document dated in past:

resolver uses policy/rate effective for that date according to rules.

Do not automatically use today's rate.

---

# 325. Locked Period Interaction

Ledger later may block backdated posting even if FinBase can resolve rate.

FinBase does not own fiscal locks.

---

# 326. Tax Date Interaction

Tax Pack may request rate using TaxDate instead of PostingDate.

Explicit purpose/date prevents hidden divergence.

---

# 327. Preview vs Posting

Draft Preview can use:

```text
provisional approved/latest eligible rate
```

Finalization resolves/fixes snapshot again.

UI warns if rate changed since preview.

---

# 328. Rate Change Warning

If draft was previewed at:

```text
52.40
```

and final rate now:

```text
52.60
```

server returns changed calculation.

UI shows concise warning when material.

---

# 329. Price Quote Contract Rate

Pricing/Sales may intentionally lock commercial FX for quote validity.

That becomes explicit Contractual/Commercial snapshot.

It does not silently become accounting posting rate unless policy says so.

---

# 330. Different Commercial and Accounting Rates

If business requires:

- customer quoted at contractual FX;
- accounting conversion at official accounting FX;

system can preserve both contexts explicitly.

Do not overload one field.

---

# 331. Why This Does Not Violate Single Source of Truth

Single Source of Truth means:

> One canonical definition for each concept.

It does not mean pretending two different economic concepts are the same.

We have:

```text
AccountingRate
BankActualRate
TaxRateConversion
ContractualRate
```

as explicit purposes inside one FinBase rate framework.

No hidden duplicate tables.

---

# 332. FX Fees

Bank spread/fees are not exchange rate master.

Payments/Bank module records fees.

Do not bake fees into FinBase accounting rate unless provider explicitly returns executable BankActual context.

---

# 333. Crypto / Non-Fiat

Do not build crypto currency accounting now.

ISO currency reference focuses supported fiat/funds as approved.

Future alternative assets require explicit domain design.

---

# 334. Precious Metals / Funds

ISO can include fund/metal codes, but enabling them as transaction currencies requires product/accounting decision.

Existence in ISO reference ≠ automatically enabled business currency.

---

# 335. Currency Symbol Ambiguity

`$` can mean multiple currencies.

Sensitive screens show code.

---

# 336. Locale Decimal Separators

UI can accept localized input carefully.

API canonical decimal representation uses dot in invariant format strings.

---

# 337. Arabic Digits Input

Frontend can normalize Arabic-Indic digits for input.

Preserve exact numeric meaning.

---

# 338. Financial Input Parsing

One central frontend/backend parsing contract.

Do not let every screen parse `"1,234.50"` differently.

---

# 339. Thousands Separator

Presentation only.

Never include thousands separators in canonical API decimal string.

---

# 340. Negative Display

UI can use:

```text
-100.00
(100.00)
```

according to locale/report preference.

Stored amount remains signed decimal.

---

# 341. Zero Display

Reports can show dash/zero based formatting.

Not storage semantics.

---

# 342. Payment Term Labels

Human names can be localized:

```text
Net 30
صافي 30 يوم
```

Definition code/id stable.

---

# 343. Rate Source Labels

Human-facing:

```text
Central/Official Provider
Bank
Manual
Contract
```

Backend uses stable codes.

---

# 344. Rounding Explanation UX

If user asks why total differs by 0.01:

advanced tooltip can explain:

> "تم تقريب الإجمالي وفق سياسة الشركة المالية."

No technical midpoint lecture unless finance admin opens details.

---

# 345. Calculation Support Tool

Internal support tool can run:

```text
Recalculate using stored snapshots
Compare stored result
```

Any mismatch is incident-level.

---

# 346. Reconciliation Invariant

For finalized financial document:

```text
Stored canonical totals
==
replay using stored snapshot + engine version
```

subject to preserved engine compatibility.

---

# 347. Engine Version Retention

Do not delete old calculation implementation if posted documents need exact replay, OR persist enough final/intermediate values that replay isn't legally required.

Decision per engine documented.

---

# 348. Preferred Strategy

For invoices/tax later:

persist full legally relevant calculation snapshots.

Engine replay is a verification tool, not the only way to understand history.

---

# 349. API Versioning

Financial contract breaking change requires:

- new contract version;
- compatibility period;
- ADR.

No casual rename of Money fields.

---

# 350. Database Migration Strategy

Financial columns use expand/contract.

Never narrow precision in-place without full data audit.

---

# 351. Precision Migration

Increasing scale can be safe after tests.

Reducing scale risks data loss and requires explicit migration/reconciliation.

---

# 352. Numeric Constraints

Where appropriate:

```text
rate > 0
percentage >= 0
```

Business upper bounds applied in domain validation.

---

# 353. Payment Term Percent Constraint

Individual percentages:

```text
0 < percent <= 1
```

canonical ratio if stored that way.

Definition validator ensures coherent sequence.

---

# 354. Payment Schedule Amount Constraint

Schedule line sign should align with document sign unless specific credit semantics.

---

# 355. Rate Source Priority Conflict

If two same-priority sources provide rate:

policy must define deterministic resolution or fail.

Never random first-row DB selection.

---

# 356. Database Ordering

Queries specify deterministic ordering:

```text
effective_date desc
priority asc
approved_at desc
id
```

according to policy.

---

# 357. Exchange Rate Time Granularity

MVP accounting rates primarily daily/effective-date.

Schema supports optional `effective_at` for intraday contexts.

Do not force real-time rate infrastructure without need.

---

# 358. Intraday Bank Rate

BankActual/payment execution can carry exact timestamp.

Accounting daily rate can remain date-based.

---

# 359. Historical Provider Timestamp

Store:

```text
provider_timestamp
acquired_at
```

when available.

---

# 360. Rate Source Raw Evidence

Optional:

store provider response hash/raw evidence reference where compliance/audit requires.

Do not store huge provider payload in main rate row.

---

# 361. Rate Evidence File

Platform Files/object storage can hold evidence export if required.

---

# 362. Payment Term Version UX

Admin sees:

```text
Net 30
Current Version v3
Effective from ...
```

Normal document user simply sees `Net 30`.

---

# 363. Default Payment Terms Seed

On activation, FinBase may seed basic templates:

```text
Due on Receipt
Net 15
Net 30
Net 45
Net 60
```

Tenant can add more.

Seed must not overwrite edited tenant definitions.

---

# 364. Country-Specific Terms

Country Pack may add recommended templates.

Not hardcoded in global FinBase.

---

# 365. Payment Terms and Collections

Collections uses resolved due dates.

It does not recalculate payment terms itself.

---

# 366. Payment Terms and Subledger

Subledger converts finalized schedule lines into Open Items / due buckets according to its model.

Source schedule is document snapshot.

---

# 367. Payment Terms and Aging

Aging depends on due dates from Subledger.

Not FinBase report.

---

# 368. Payment Terms and Partial Payment

A partial payment does not change original schedule definition.

Settlement reduces open-item balances.

---

# 369. Payment Terms and Rescheduling

Renegotiating debt after invoice is a Collections/Subledger operation.

Do not mutate original invoice term snapshot.

---

# 370. Payment Terms and Credit Notes

Credit application handled by Invoicing/Subledger.

FinBase just can calculate signed schedule if asked.

---

# 371. Payment Terms and Prepayment

Prepayment / advance invoice belongs Sales/Invoicing.

Keep clear.

---

# 372. Payment Terms and Milestones

Project/milestone invoicing later can produce invoice dates/amounts.

Once invoice exists, its due schedule uses FinBase rules.

---

# 373. Payment Terms and Installments

One invoice can produce multiple due schedule lines.

Subledger should preserve line-level due dates.

---

# 374. Financial Context Snapshot

Recommended standardized value:

```text
FinancialContextSnapshot
{
  legal_entity_id
  base_currency
  transaction_currency
  rate_snapshot?
  rounding_policy_version
  calculation_version
}
```

Used by financial domains where relevant.

---

# 375. No Shared Mutable Financial Context

Never store context in static/global thread state.

Scoped request/command object.

---

# 376. Background Worker Context

Message carries entity + explicit financial reference/date.

Worker resolves or uses existing snapshot.

---

# 377. Retry Safety

Retrying calculation with same persisted context returns same result.

---

# 378. Event Consumers

Do not resolve "latest rate" while handling historical posted event if event already carries snapshot/reference.

Use source transaction snapshot.

---

# 379. AI Restrictions

AI can:

- explain payment term;
- suggest mapping;
- flag suspicious rate;
- draft payment-term setup.

AI cannot:

- invent rate;
- override approved rate;
- change base currency;
- choose rounding policy autonomously;
- post financial result.

---

# 380. AI Explanation

AI explanations are advisory.

Canonical numbers come from deterministic engine.

---

# 381. Performance Engineering Review

Before FinBase gate:

measure:

- policy resolve;
- cached currency lookup;
- rate resolve;
- 1,000 conversion batch;
- 1,000 payment schedule calculations;
- concurrent rate reads.

---

# 382. Load Scenario

Simulate many tenants/entities concurrently.

Ensure cache key isolation.

---

# 383. Cache Poisoning Test

Entity A cached USD→EGP must never answer Entity B request if B policy differs.

---

# 384. Memory Cache Size

Currency/policy/rates are small.

Bound caches and version invalidation.

---

# 385. No Unbounded Historical Rate Cache

Historical rates stay DB indexed.

Cache recent/hot ranges.

---

# 386. Rate Table Scale

Even years of daily rates × currencies × entities is manageable with correct indexes.

No time-series database needed.

---

# 387. Partitioning

Do not partition FinBase tables Day 1.

Measure first.

---

# 388. Search

FinBase is not search-heavy.

Standard indexed admin lists enough.

No OpenSearch.

---

# 389. UI Responsiveness

Currency dropdown reads cached enabled list.

Payment term dropdown lightweight.

No heavy FX history query in normal document load.

---

# 390. Rate History Screen

Finance-only screen with pagination/date filters.

---

# 391. Rate Chart

Optional later.

Not required to run financial workflows.

---

# 392. Provider Status Screen

Finance/Admin can see:

```text
Last successful sync
Next sync
Errors
Currencies covered
```

---

# 393. Setup Health Check

FinBase health:

```text
Base Currency set?
Rounding Policy active?
Enabled currencies valid?
Required rates fresh?
Payment terms active?
```

Useful before go-live.

---

# 394. Financial Go-Live Checklist

- [ ] Base Currency reviewed
- [ ] Currency metadata loaded
- [ ] Rounding policy approved
- [ ] FX provider/manual policy configured
- [ ] Rate staleness configured
- [ ] Manual override permissions
- [ ] Payment terms approved
- [ ] Precision test suite passing
- [ ] accountant sign-off
- [ ] multi-currency scenarios tested if enabled

---

# 395. Build Scope — MUST BUILD NOW

- Money/Currency primitive contracts.
- exact numeric conventions.
- CurrencyReference.
- LegalEntity currency enablement.
- Base Currency guard.
- Rounding engine + versioned policy.
- ExchangeRate storage/resolution.
- Rate snapshots.
- Manual rate + audit.
- provider adapter framework.
- at least one provider/manual import path appropriate to launch.
- PaymentTerm definition/version/rules.
- Payment Schedule calculator.
- FinBase Contracts.
- caching.
- APIs/admin UI.
- tests/golden vectors.
- observability.

---

# 396. DESIGN NOW / BUILD LATER

- early payment discount full accounting/tax.
- business-day calendars.
- additional reporting currency.
- four-eyes approval configurable workflow.
- complex cross-rate triangulation beyond base.
- intraday market-rate UI.
- base currency migration project.
- cash denomination rounding for POS where required.
- sophisticated rate tolerance analytics.

---

# 397. DO NOT BUILD HERE

- Ledger entries.
- FX gain/loss postings.
- Accounts.
- Tax engine.
- Pricing engine.
- Customer balance.
- Payment settlement.
- Bank reconciliation.
- Down-payment invoice engine.
- Collections rescheduling.
- Financial reporting.

---

# 398. Implementation Order

```text
F00 Financial ADRs + Numeric Standards
F01 CurrencyReference + CurrencyCode
F02 Money / UnitPrice / Percentage Contracts
F03 Legal Entity Base Currency Guard
F04 LegalEntityCurrency Setup
F05 Rounding Engine + Policies
F06 Exchange Rate Model
F07 Exchange Rate Resolver
F08 Manual Rate + Audit
F09 Provider Adapter + Sync
F10 Exchange Rate Snapshot + Converter
F11 Payment Term Definitions
F12 Payment Schedule Calculator
F13 FinBase APIs + Admin UX
F14 Frontend Decimal Handling
F15 Caching + Performance
F16 Messaging/Outbox
F17 Golden Test Vectors
F18 Cross-Module Contract Tests
F19 Security/Red-Team
F20 Financial Product Acceptance
```

---

# 399. F00 Exit Gate

Approved ADRs for:

- numeric types;
- JSON decimals;
- base currency ownership;
- rounding;
- rate direction;
- snapshots;
- payment terms.

---

# 400. F01 Exit Gate

- ISO currency dataset versioned.
- CurrencyCode stable.
- minor-unit metadata.
- obsolete currency handling.

---

# 401. F02 Exit Gate

- Money mismatch fails.
- UnitPrice precision retained.
- Percentage convention locked.
- no public financial `double/float`.

---

# 402. F03 Exit Gate

- Base Currency canonical field only in LegalEntity.
- pre-go-live change allowed.
- post-go-live normal change blocked.

---

# 403. F04 Exit Gate

- currencies enabled per Entity.
- base always enabled.
- disabled currency unavailable to new transaction context.

---

# 404. F05 Exit Gate

- explicit rounding modes.
- per-context rules.
- line/document strategy.
- policy version snapshot.
- .NET/Postgres behavior not relied on implicitly.

---

# 405. F06 Exit Gate

- explicit from/to.
- purpose.
- effective date.
- approval.
- indexes.
- RLS.

---

# 406. F07 Exit Gate

- deterministic eligible rate.
- no future rate.
- staleness.
- missing-rate error.
- entity isolation.

---

# 407. F08 Exit Gate

- manual override permission.
- reason.
- audit.
- immutable history.

---

# 408. F09 Exit Gate

- provider worker.
- no live provider in posting.
- retry.
- secrets safe.
- provider outage test.

---

# 409. F10 Exit Gate

- conversion exact.
- same currency=1.
- snapshot.
- batch performance.

---

# 410. F11 Exit Gate

- versioned terms.
- Percentage + Balance.
- due rules.
- archive.

---

# 411. F12 Exit Gate

- exact schedule sum.
- tiny amount.
- negative.
- date edges.
- deterministic.

---

# 412. F13 Exit Gate

- simple SME UX.
- advanced finance UX.
- no duplicate Source of Truth.

---

# 413. F14 Exit Gate

- API decimals string.
- frontend decimal preview ADR.
- no JS Number authoritative path.

---

# 414. F15 Exit Gate

- no per-line DB calls.
- policy/rate cache.
- concurrent load test.
- entity cache isolation.

---

# 415. F16 Exit Gate

- rate/policy events via Outbox.
- provider jobs idempotent.

---

# 416. F17 Exit Gate

Shared financial golden vectors versioned in repository.

---

# 417. F18 Exit Gate

At minimum test consumers / sample modules prove:

```text
Pricing-ready
Tax-ready
Ledger-ready
Invoicing-ready
```

without accessing FinBase DB directly.

---

# 418. F19 Exit Gate

Red-team scenarios all pass.

---

# 419. F20 Product Acceptance

Demo:

1. Single-currency small company.
2. Foreign-currency invoice preview.
3. Rate provider/manual rate.
4. Rate change after historical invoice.
5. Net 30.
6. Three installments with residual.
7. Multi-entity different base currencies.
8. unauthorized FX override.
9. post-go-live Base Currency lock.
10. rounding edge cases.

Finance/accounting reviewer signs off.

---

# 420. Financial Foundation Definition of Done

Before Pricing/Tax/Ledger production implementation:

- [ ] One Base Currency source of truth.
- [ ] Money primitive locked.
- [ ] Numeric precision locked.
- [ ] Explicit rounding engine.
- [ ] Currency dataset/versioning.
- [ ] Entity currency setup.
- [ ] Exchange rate resolver.
- [ ] Rate snapshots.
- [ ] No live provider in posting.
- [ ] Payment terms versioned.
- [ ] Schedule sum invariant.
- [ ] RLS.
- [ ] permissions/audit.
- [ ] frontend decimal safety.
- [ ] golden tests.
- [ ] load tests.
- [ ] accountant review.

---

# 421. Architecture Rule — No New Financial Math Without Review

Any module adding:

```text
rounding
currency conversion
payment due calculation
financial percentage allocation
```

must first prove the capability does not already belong to FinBase/Tax/Pricing/Ledger.

Architecture Test + PR review.

---

# 422. Senior Product Rule — Complexity Belongs Behind the Screen

The normal user should think:

```text
"I selected USD."
"I selected Net 30."
"I see the total."
```

Not:

```text
"Which quote convention?"
"Which scale?"
"Which midpoint rule?"
"Which source?"
```

Finance specialists can inspect these details when needed.

---

# 423. Senior Architecture Rule — Correctness Before Convenience

We will not simplify UI by hiding a calculation error.

We simplify UI by automating the correct backend process.

---

# 424. Senior Engineering Rule — Speed Without Duplicating Truth

Speed comes from:

- in-memory pure calculation;
- small cached policies;
- indexed local rates;
- context resolved once;
- immutable snapshots;
- no remote provider in hot path.

Not from:

- copying base currency into every module;
- storing duplicate rate settings;
- skipping checks.

---

# 425. Senior Finance Rule — History Is Frozen

After financial finalization:

Current master changes do not rewrite:

- currency;
- rate;
- rounding policy;
- payment schedule;
- base amount.

Corrections happen through domain-approved corrective flows.

---

# 426. Comparison With Established ERP Patterns

Business Central explicitly maintains local/base currency equivalents and dated exchange rates; it also separates rate adjustment/revaluation behavior for open balances.

Odoo records transactions in company main currency while preserving foreign currency context and creates exchange-difference accounting.

ERPNext supports stored dated Currency Exchange records and Payment Terms with deposits/installments/credit periods.

Our architecture adopts those proven business requirements while making ownership/snapshots/determinism explicit.

---

# 427. Accounting Standards Note

IAS 21 distinguishes functional currency from foreign currencies and focuses on which rates are used and how currency changes affect reporting.

Our Base Currency model aligns conceptually with functional currency.

Final statutory implementation and presentation-currency behavior must be reviewed by accounting/legal specialists for each market.

---

# 428. Source Register — Checked 2026-10-05

## ISO 4217 — Currency Codes

Official ISO:

https://www.iso.org/iso-4217-currency-codes.html

Used for:

- currency codes;
- numeric codes;
- minor-unit reference.

## PostgreSQL 18 — Numeric Types

Official PostgreSQL:

https://www.postgresql.org/docs/18/datatype-numeric.html

Used to validate:

- `numeric/decimal` exact arithmetic;
- floating-point inexactness;
- explicit precision/scale behavior.

## .NET — MidpointRounding / Math.Round

Microsoft Learn:

https://learn.microsoft.com/en-us/dotnet/api/system.midpointrounding

https://learn.microsoft.com/en-us/dotnet/api/system.math.round

Used to validate:

- .NET rounding modes;
- default `Math.Round` behavior;
- why financial rounding must be explicit.

## Microsoft Dynamics 365 Business Central — Currencies

https://learn.microsoft.com/en-us/dynamics365/business-central/finance-currencies

## Business Central — Exchange Rates

https://learn.microsoft.com/en-gb/dynamics365/business-central/finance-how-update-currencies

Used for real-world patterns:

- dated exchange rates;
- local currency equivalents;
- exchange-rate adjustment;
- realized/unrealized differences.

## Business Central — Payment Terms

https://learn.microsoft.com/en-gb/dynamics365/business-central/finance-payment-terms

Used for:

- default customer/vendor terms;
- document override;
- due-date formulas;
- payment discounts.

## Odoo — Multi-currency

https://www.odoo.com/documentation/20.0/applications/finance/accounting/get_started/multi_currency.html

## Odoo — Foreign Currency Bank

https://www.odoo.com/documentation/20.0/applications/finance/accounting/bank/foreign_currency.html

Used for:

- company main currency;
- foreign currency values;
- exchange differences;
- unrealized adjustment pattern.

## Odoo — Payment Terms / Installments

https://www.odoo.com/documentation/17.0/applications/finance/accounting/customer_invoices/payment_terms.html

Used for:

- due dates;
- installment plans;
- immediate / Net terms;
- advance percentage schedules;
- distinction from down-payment invoices.

## ERPNext — Currency Exchange

https://docs.frappe.io/erpnext/currency-exchange

Used for:

- approved dated rate records;
- stored rates before online fallback;
- controlled finance rate behavior.

## ERPNext — Payment Terms

https://docs.frappe.io/erpnext/payment-terms

Used for:

- deposits;
- installments;
- credit periods;
- schedule flow.

## IFRS — IAS 21

https://www.ifrs.org/issued-standards/list-of-standards/ias-21-the-effects-of-changes-in-foreign-exchange-rates/

Used for conceptual distinction:

- functional currency;
- foreign currency;
- currency translation issues.

---

# 429. Final Product + Architecture Verdict

This Financial Foundation is one of the most important contracts in the ERP.

The final model is:

```text
Legal Entity
    └── One Base / Functional Currency (single source of truth)

FinBase
    ├── Currency Reference
    ├── Enabled Entity Currencies
    ├── Financial/Rounding Policies
    ├── Exchange Rates
    ├── Rate Resolver
    ├── Immutable Rate Snapshots
    ├── Currency Converter
    ├── Payment Terms
    └── Payment Schedule Calculator

Shared Financial Primitives
    ├── Money
    ├── UnitPrice
    ├── Percentage
    └── CurrencyCode
```

Downstream:

```text
Pricing      → uses Money/Currency
Tax          → uses Money/Rounding/FX contexts
Ledger       → uses Base Currency/Facts/Rates
Subledger    → uses schedules and FX settlement context
Invoicing    → snapshots calculations
Payments     → supplies payment/BankActual context
Inventory    → uses exact monetary valuation primitives
Payroll      → uses exact money primitives
```

The product promise is:

> **The employee sees the number they need. The backend owns the complexity required to make that number correct.**

The architecture promise is:

> **No second Base Currency. No hidden rate. No implicit rounding. No floating-point financial truth. No historical recalculation from current settings.**

The engineering promise is:

> **Financial correctness and speed are achieved together through deterministic in-memory calculation, cached immutable configuration, local indexed rate data, and stored snapshots—not by duplicating Source of Truth.**

---

# 430. Next Pack

After FinBase is approved, the next file should be:

> **`07_ERP_SAAS_PRICING_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`**

Its business question:

> **إزاي الشركة تحدد سعر البيع الصحيح لكل منتج/عميل/كمية/عملة/تاريخ من غير ما المستخدم يتوه في Pricelists معقدة، ومن غير ما Product يبقى عنده Price Source of Truth تاني؟**

After Pricing:

```text
08 Tax
09 Ledger
10 Subledger
11 Invoicing
12 Payments
13 Inventory
14 Sales
15 Purchasing
...
```

---

## Local implementation status — 2026-10-09

The FinBase foundation is implemented locally under `src/backend/modules/FinBase` and `src/frontend/src/modules/finbase`. The implementation keeps `platform_org.legal_entity.base_currency_code` as the only base-currency source, uses decimal-string money contracts, explicit rounding, versioned payment-term rules, approved dated exchange-rate records, tenant/entity RLS, authorization, audit and Outbox publication.

The implementation intentionally does not create Chart of Accounts, fiscal-year or fiscal-period tables. The Accounting requirements pack assigns canonical CoA and Fiscal Period ownership to Ledger; FinBase provides the currency, conversion and payment-term contracts those later modules consume.

**END OF FILE — 06_ERP_SAAS_FINBASE_CURRENCY_PAYMENT_TERMS_MONEY_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md**

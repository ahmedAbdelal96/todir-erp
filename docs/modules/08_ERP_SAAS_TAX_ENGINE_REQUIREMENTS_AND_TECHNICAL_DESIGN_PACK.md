# 08 — TODIR ERP SaaS Tax Engine Requirements & Technical Design Pack

**File:** `08_ERP_SAAS_TAX_ENGINE_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`  
**Product:** **TODIR**  
**Status:** PRODUCT + TAX INTEGRITY + DOMAIN + TECHNICAL BASELINE  
**Version:** v1.0  
**Date:** 2026-10-05  
**Audience:** Product Owner, Product Architect, Tax/Accounting Reviewer, Country-Pack Engineers, Backend/Frontend Leads, Performance Engineer, QA, Implementation Agent  
**Risk Class:** CRITICAL — tax errors can affect legal compliance, customer invoices, payable/receivable balances, inventory valuation, accounting and government reporting.

**Depends on**
- `01_ERP_SAAS_MASTER_MODULE_AND_DEPENDENCY_BLUEPRINT.md`
- `02_ERP_SAAS_PRODUCTION_READY_TECH_STACK_AND_PROJECT_STRUCTURE.md`
- `03_ERP_SAAS_PLATFORM_KERNEL_AND_MODULE_SYSTEM_TECHNICAL_DESIGN_PACK.md`
- `04_ERP_SAAS_DIRECTORY_PARTY_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `05_ERP_SAAS_CATALOG_PRODUCT_UOM_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `06_ERP_SAAS_FINBASE_CURRENCY_PAYMENT_TERMS_MONEY_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `07_ERP_SAAS_PRICING_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`

---

# 0. Executive Decision

TODIR uses **one deterministic Tax Engine** for the platform.

Sales, Purchasing, Invoicing, POS, E-commerce and future business modules MUST NOT independently calculate tax.

The architecture is:

```text
Pricing
    ↓ commercial price

Tax Engine
    ↓ legally interpreted tax result

Transaction
    ↓ immutable Tax Snapshot

Ledger
    ↓ accounting posting

Compliance
    ↓ government/e-invoicing submission
```

Country law/content comes from **Country Packs**, not hardcoded business-module code.

The user should see only the tax information needed for their job.

---

# 1. Central Business Question

The engine must answer:

> **For this Legal Entity, transaction, party, product/service, location, date, currency and document type, what tax treatment legally applies, how is it calculated, and why?**

---

# 2. Product Promise

For a normal user:

```text
Subtotal   1,000.00
Tax          140.00
Total      1,140.00
```

or, for a company not using tax tracking:

```text
Total      1,000.00
```

The user does not need to understand:

- tax-rule priority;
- jurisdiction resolution;
- rate-version lookup;
- tax-base dependencies;
- inclusive extraction;
- country-pack internals;
- reporting codes;
- legal evidence versions.

---

# 3. The First Critical Distinction — Tax Capability vs Legal Status

TODIR must NOT model tax as:

```text
company.tax_enabled = true/false
```

alone.

Why?

Because:

- "Tax feature disabled in TODIR" is a product configuration state.
- "Not registered" is a legal/registration state.
- "Exempt supply" is a transaction treatment.
- "Zero-rated" is a taxable treatment with a zero rate.
- "Out of scope" is another legal treatment.
- "No tax due" can arise for several different reasons.

These are not interchangeable.

---

# 4. Tax Capability State

Per **Legal Entity**, not Tenant:

```text
TaxCapabilityState
{
    Disabled
    Enabled
}
```

Meaning:

### Disabled
TODIR does not perform indirect-tax calculation for normal workflows for this Legal Entity.

### Enabled
TODIR resolves and calculates tax according to registrations, Country Pack and rules.

This is a product capability state, not a legal claim.

---

# 5. Why Tax Capability Is Per Legal Entity

One Tenant may have:

```text
Egypt Legal Entity  → Tax enabled
UAE Legal Entity    → Tax enabled
Other Legal Entity  → Tax tracking disabled
```

Therefore:

```text
Tenant.TaxEnabled ❌
LegalEntity Tax Capability ✅
```

---

# 6. Tax-Disabled Fast Path

When Tax Capability is Disabled:

```text
Tax Engine entry
    ↓
Capability check
    ↓
Tax calculation bypassed
```

The system does NOT:

- resolve jurisdiction;
- load product tax categories;
- load tax rules;
- execute line tax algorithms;
- create fake 0% tax lines.

Normal total:

```text
Gross = Commercial Net
```

unless another non-tax charge exists in its owning domain.

---

# 7. Tax Disabled Is Not "0%"

Forbidden simplification:

```text
Tax Disabled → VAT 0%
```

because a 0% taxable supply can have different legal/reporting consequences.

---

# 8. Tax Disabled Is Not "Exempt"

Also forbidden:

```text
Tax Disabled → Exempt
```

Exemption is a statutory classification.

---

# 9. Internal Snapshot When Tax Capability Is Disabled

A transaction that needs historical clarity can preserve:

```text
TaxEvaluationState = NotEvaluatedBecauseCapabilityDisabled
```

or equivalent typed value.

Do not create:

```text
TaxRate = 0
```

as false legal evidence.

---

# 10. Country Pack Can Restrict Tax Disablement

Tax Capability is simple UX, but Country Pack may:

- warn that registration/configuration appears required;
- require privileged confirmation;
- prohibit disabling after tax go-live;
- expose registration-monitor recommendations.

TODIR must not present a simple switch as legal advice.

---

# 11. Tax Registration Is a Separate Concept

When Tax Capability is Enabled, the Legal Entity may have registration state.

Possible general lifecycle:

```text
NotRegistered
PendingRegistration
Registered
RegistrationException
Deregistered
```

Country Pack may extend/map legal statuses without changing Core semantics.

---

# 12. Tax Registration Is Effective-Dated

Example:

```text
Before 2027-01-01 → NotRegistered
From   2027-01-01 → Registered
```

Old documents do not become taxable merely because registration becomes active later.

---

# 13. Tax Registration Record

Concept:

```text
TaxRegistration
{
    Id
    TenantId

    TaxScheme
    CountryCode
    RegistrationNumber?

    Status

    EffectiveFrom
    EffectiveTo?

    IssuedAt?
    DeregisteredAt?

    VerificationState
    Source

    Version
}
```

---

# 14. Legal Entity Membership in Tax Registration

A tax registration can normally belong to one Legal Entity, but some jurisdictions support tax groups.

Therefore design:

```text
TaxRegistration
    ↓
TaxRegistrationMember
        └── LegalEntityId
```

UI for ordinary companies hides group complexity.

---

# 15. Why Tax Group Readiness Matters

Official tax regimes can permit multiple legal persons to be treated as one taxable group.

If we hard-code:

```text
LegalEntity has exactly one isolated tax registration
```

we may require painful future schema redesign.

So data model supports membership from Day 1, while group-management UX can be built only when a Country Pack enables it.

---

# 16. Tax Registration Does Not Own Legal Entity

Organization remains canonical Legal Entity owner.

Tax references LegalEntity IDs.

---

# 17. Tax Scheme

Examples of scheme concepts:

```text
VAT
GST
SalesTax
Excise
Withholding
```

Do not assume every country uses identical tax models.

---

# 18. MVP Tax Focus

First Tax Engine is focused on transactional indirect taxes such as:

```text
VAT / GST / Sales-type taxes
```

Architecture must leave seams for:

- excise/fixed-per-unit taxes;
- withholding;
- additional levies.

Do not mix all of them into one giant formula engine.

---

# 19. Tax Domain Ownership

Tax owns:

```text
TaxScheme
TaxType
TaxTreatment
TaxJurisdiction
TaxCategory
TaxRegistration
TaxRule
TaxRuleVersion
TaxRateDefinition
TaxStack/Component Definition
PartyTaxProfile
TaxPolicy
TaxCalculation Engine
Tax Snapshot Contract
```

---

# 20. Tax Does NOT Own

```text
Product identity               → Catalog
Party identity                 → Directory
Commercial price               → Pricing
Currency / FX / rounding base  → FinBase
Invoice                         → Invoicing
Payment                         → Payments
Journal Entry                   → Ledger
Government submission           → Compliance
```

---

# 21. Country Pack Ownership

Country Pack contributes authoritative/localized legal content:

```text
Tax schemes
Tax treatment definitions
Tax rates
Effective dates
Jurisdiction rules
Place-of-supply rules
Registration validation
Legal reason codes
Statutory rounding constraints
Document tax requirements
Tax reporting mappings
```

The generic Tax Engine executes them.

---

# 22. Country Pack Is Not a Fork

Forbidden:

```text
EgyptTaxEngine
SaudiTaxEngine
UaeTaxEngine
```

as separately maintained complete engines.

Correct:

```text
Generic TODIR Tax Engine
    +
Country Rule Provider / Country Pack
```

---

# 23. Why This Separation Matters

Benefits:

- one tested calculation engine;
- common API/contracts;
- consistent snapshots;
- country law can evolve independently;
- no duplicated bugs;
- easier MENA expansion.

---

# 24. Statutory Content Rule

Every statutory Country Pack rule must carry:

```text
official source reference
effective date
version
review status
```

No tax-law content based only on:

- memory;
- blog posts;
- forum answers;
- copied competitor configuration.

---

# 25. Official-Source-First Policy

Examples:

```text
Egypt → Egyptian Tax Authority / official legislation
KSA   → ZATCA
UAE   → Federal Tax Authority
```

Country Pack CI/release process must maintain evidence.

---

# 26. Legal Changes Are Expected

Tax content is versioned because law changes.

The Egyptian Tax Authority currently lists VAT amendments in 2025 and 2026, including later amendments to the 2016 VAT law.

The UAE FTA also publishes current amendments and public clarifications.

Therefore hardcoded permanent rates/rules are unacceptable.

---

# 27. Tax Treatment Is Not Just Rate

Transaction treatment examples:

```text
StandardRated
ReducedRated
ZeroRated
Exempt
OutOfScope
ReverseCharge
Import
Export
SpecialScheme
```

Country Pack can define country-specific subtypes.

---

# 28. Zero Rated vs Exempt

Both may produce `TaxAmount = 0`.

But they are different legal treatments.

The engine preserves treatment code/reason.

---

# 29. Out of Scope vs Zero Rated

Also different.

Never reduce all zero amounts to:

```text
rate = 0
```

---

# 30. Tax Category vs Tax Rate

A Product should normally store/classify:

```text
ProductTaxCategory = StandardGoods
```

not:

```text
VAT = 14%
```

The rate can depend on:

- country;
- jurisdiction;
- date;
- party;
- supply type;
- legal amendments.

---

# 31. Product Tax Classification

Catalog Product identity remains clean.

Tax owns or references a separate profile:

```text
ProductTaxProfile
{
    ProductVariantId / ProductTemplateId
    LegalEntity/Country scope
    TaxCategoryId
    Effective dates
}
```

---

# 32. Template vs Variant Tax Category

Default tax category may be on Product Template.

Variant override only when legally meaningful.

Do not duplicate same profile per Variant unnecessarily.

---

# 33. Service Tax Classification

Catalog `Nature = Service` is not enough to calculate tax.

Tax profile can classify service type where local law requires.

---

# 34. Party Tax Profile

Party identity does not contain tax-business logic.

Tax profile concept:

```text
PartyTaxProfile
{
    PartyId
    LegalEntityId / RegistrationContext

    RegistrationStatus?
    RegistrationNumber?
    TaxTreatmentCategory?

    Country/Jurisdiction attributes
    ReverseChargeEligibility?
    ExemptionCertificateRef?

    Effective dates
}
```

---

# 35. Do Not Duplicate Party Identity

Tax profile references Directory Party.

Names/addresses/contacts remain Directory truth.

---

# 36. Address and Tax

Tax may need:

- billing country;
- shipping country;
- ship-from;
- ship-to;
- establishment/location;
- place of supply.

Tax consumes immutable address/location snapshots or Directory location context.

It does not own addresses.

---

# 37. Transaction Address Snapshot

If jurisdiction depends on address, consuming document should snapshot the relevant legal address/location data used at finalization.

Changing Customer address later cannot rewrite historical tax treatment.

---

# 38. Tax Context

Canonical request:

```text
TaxContext
{
    TenantId
    LegalEntityId

    Direction                // Sale / Purchase
    DocumentKind
    TransactionType

    DocumentDate
    TaxDate?
    SupplyDate?
    PostingDate?

    PartyContext
    AddressContext
    JurisdictionContext?

    ProductTaxContext

    Quantity
    Uom

    PriceMode                // TaxExclusive / TaxInclusive

    LineNetOrGrossAmount
    Discounts
    TaxableCharges

    TransactionCurrency
    BaseCurrency

    RegistrationContext
}
```

Not every jurisdiction uses every field.

---

# 39. Tax Calculation Must Be Explicitly Contextual

Forbidden:

```text
CalculateTax(productId, amount)
```

This is not enough for real tax.

---

# 40. Direction

At minimum:

```text
Sale
Purchase
```

because output/input tax treatment can differ.

---

# 41. Document Kind

Examples:

```text
Invoice
CreditNote
DebitNote
Receipt
OrderPreview
PurchaseInvoice
PurchaseCredit
```

Tax Pack decides which kinds matter legally.

---

# 42. Preview vs Final Calculation

Tax calculation may occur for:

```text
Quotation preview
Sales order preview
Invoice draft
Invoice finalization
```

Only finalized document snapshot becomes historical financial/legal truth.

---

# 43. Tax Date

Tax Date is its own concept.

Do not assume:

```text
TaxDate = PostingDate
```

or:

```text
TaxDate = DocumentDate
```

globally.

---

# 44. Why Tax Date Is Separate

Established ERP systems explicitly support a VAT/tax date because reporting period and legal tax point can differ from document/posting dates.

Country Pack decides default and editability.

---

# 45. Tax Date Resolver

Contract:

```text
ITaxDateResolver
```

Input:

- document type;
- relevant business dates;
- country;
- transaction context.

Output:

```text
TaxDate
Reason
RuleVersion
```

---

# 46. No Hidden `DateTime.Now`

Tax calculation uses explicit NodaTime dates.

---

# 47. Legal Entity Timezone

Date derivation from timestamps uses Legal Entity business timezone.

Never server timezone.

---

# 48. Jurisdiction Resolution

Before rate selection, determine where tax rules apply.

Concept:

```text
TaxJurisdictionResult
{
    JurisdictionId
    Country
    Region/Zone?
    PlaceOfSupplyCode
    Reason
    RuleVersion
}
```

---

# 49. Jurisdiction Is Not Always Country Only

Future markets may require:

- state/province;
- emirate/zone;
- city/local levy;
- special economic zone;
- free/designated zone.

Data model must not assume one global country rate table is always enough.

---

# 50. Place of Supply

Place-of-supply can depend on:

- product/service type;
- seller establishment;
- customer establishment;
- ship-from/to;
- B2B/B2C;
- transaction nature.

Country Pack owns the legal logic.

---

# 51. Country Pack Resolver Port

Concept:

```text
ITaxJurisdictionProvider
```

The generic engine asks the active Country Pack.

---

# 52. Tax Category

Tax Category describes treatment/classification context, not necessarily a rate.

Examples:

```text
Standard
Export
Exempt
ZeroRated
Special
```

This pattern is validated in established ERPs.

---

# 53. Tax Rule

Tax Rule decides which Tax Definition/Stack applies for the resolved context.

Inputs can include:

```text
Registration
Jurisdiction
Direction
Party Tax Category
Product Tax Category
Document Kind
Tax Date
Place of Supply
```

---

# 54. Tax Rule Version

Rules are effective-dated/versioned.

Never update historical law by overwriting a used row.

---

# 55. Rule State

```text
Draft
Approved
Scheduled
Active
Superseded
Retired
```

Country Pack content usually ships Approved/Active with signed release/version metadata.

---

# 56. Rule Provenance

Every rule has provenance:

```text
CountryPack
TenantConfiguration
GovernmentIntegration
SystemReference
```

---

# 57. Rule Mutability Classification

A rule/config field can be:

```text
LockedByCountryPack
ConfigurableWithinAllowedValues
TenantDefined
```

This prevents tenant admins from accidentally changing statutory rates that should not be editable.

---

# 58. Tenant Custom Tax Rules

Some businesses need non-statutory/custom charges or special treatment.

Do NOT let users override law silently.

Custom rules must:

- use allowed rule types;
- be clearly labeled;
- be audit-tracked;
- not supersede locked Country Pack rule unless Country Pack permits.

---

# 59. Tax Rule Conflict

If two rules equally match and produce conflicting treatment:

```text
TaxConfigurationConflict
```

The engine MUST NOT choose by:

```text
created_at
database ID
row order
```

---

# 60. Rule Precedence

Country Pack can define deterministic precedence by named dimensions.

Generic framework supports:

```text
Registration specificity
Jurisdiction specificity
Party treatment specificity
Product category specificity
Document type specificity
Effective date/version
Explicit legal priority
```

---

# 61. Explainability

Tax Resolution returns why a rule was selected.

Admin can answer:

> Why did this line get this tax treatment?

---

# 62. Tax Result

Canonical result:

```text
TaxResult
{
    EvaluationState
    Treatment

    NetAmount
    TaxTotal
    GrossAmount

    TaxLines[]

    JurisdictionSnapshot
    RegistrationSnapshot
    RuleSetVersion
    CalculationVersion

    Explanation
}
```

---

# 63. Tax Line

```text
TaxLine
{
    TaxTypeCode
    TaxCode
    TreatmentCode

    Rate?
    FixedAmountPerUnit?

    TaxableBase
    TaxAmount

    Currency

    Jurisdiction
    LegalReasonCode?
    LegalReasonText?

    Recoverability?

    ReportingCode?

    RuleId
    RuleVersion
}
```

---

# 64. Why `TaxLines[]`

One transaction line can have:

- VAT;
- excise;
- local levy;
- environmental charge;
- other statutory components.

A single:

```text
tax_amount
```

does not scale.

---

# 65. Tax Computation Method

Structured enum/provider strategy, not arbitrary code:

```text
PercentageOfNet
PercentageExtractedFromGross
FixedPerUnit
FixedPerLine
PercentageOfModifiedBase
```

Country Pack decides allowed methods.

---

# 66. No Arbitrary Tax Formula Day 1

Forbidden tenant formula:

```text
eval("price * foo + whatever")
```

Tax correctness/security requires typed calculation methods.

---

# 67. Tax Exclusive Price

Given:

```text
Net = 100
Rate = 10%
```

result:

```text
Tax = 10
Gross = 110
```

before policy-specific rounding.

---

# 68. Tax Inclusive Price

Given gross 110 and rate 10%:

```text
Net = 100
Tax = 10
Gross = 110
```

Tax is extracted from gross; it is not simply `110 * 10%`.

---

# 69. Inclusive Extraction Formula

For simple percentage tax:

```text
Tax = Gross × rate / (1 + rate)
Net = Gross - Tax
```

then explicit rounding policy applies.

---

# 70. Price Mode Comes From Pricing/Transaction

Pricing may return:

```text
TaxExclusive
TaxInclusive
```

Tax consumes that context.

Pricing does not calculate VAT.

---

# 71. Tax Included Does Not Mean One Universal Formula

Multiple/compound taxes can require different extraction.

Country Pack/Tax Stack definition controls.

---

# 72. Tax Base

Tax base can depend on:

- unit price;
- quantity;
- discounts;
- taxable charges;
- previous taxes;
- exemptions;
- valuation rules.

The Tax Engine needs structured inputs, not just final total.

---

# 73. Discounts

The consuming document provides discount facts.

Tax Pack decides how they affect taxable base.

Do not hardcode:

```text
all discounts always reduce tax base
```

globally.

---

# 74. Charges

Shipping/fees may be:

- taxable;
- non-taxable;
- separately classified.

The owning commercial module provides charge lines; Tax classifies them.

---

# 75. Quantity/UOM

Fixed-per-unit/excise-style taxes may require Quantity and UOM.

Tax validates UOM through Catalog context.

---

# 76. UOM Conversion

Tax does not own quantity conversions.

Catalog provides base/entered quantity snapshot.

---

# 77. Multiple Taxes

Tax Stack:

```text
Tax Component A
Tax Component B
Tax Component C
```

Each component has explicit order/dependency.

---

# 78. Parallel Taxes

Two taxes can be calculated on the same base.

---

# 79. Compound Taxes

A later tax may include previous tax in its base.

This must be explicit.

---

# 80. Tax Dependency Graph

For complex cases, Tax Stack forms an acyclic dependency graph/order.

Activation validates no cycles.

---

# 81. No Tax Cycles

Forbidden:

```text
Tax A base includes B
Tax B base includes A
```

---

# 82. Fixed Tax

Some levies can be:

```text
fixed amount × quantity
```

Engine supports typed method.

---

# 83. Excise Boundary

Excise can require:

- product classification;
- quantity;
- retail selling price;
- special tax bases.

Core model supports tax components, but detailed Excise Engine/Pack is built only for launch countries requiring it.

---

# 84. Reverse Charge

Reverse charge is a treatment, not merely a zero tax amount.

Tax Result can indicate:

```text
ReverseCharge = true
TaxDueBy = Recipient
```

and produce tax components needed for downstream accounting/reporting.

---

# 85. Reverse Charge Country Logic

Country Pack determines:

- eligible supplies;
- party requirements;
- place of supply;
- reporting codes;
- document wording.

---

# 86. Import Tax

Import treatment can differ from domestic purchase.

Tax context includes transaction type/place-of-supply/customs references where relevant.

---

# 87. Export Treatment

Export can be zero-rated or otherwise treated depending on jurisdiction/evidence.

Never hardcode "foreign customer = 0%".

---

# 88. Evidence Requirements

Country Pack can declare evidence requirements:

```text
Export evidence
Exemption certificate
Tax registration verification
Reverse charge evidence
```

Tax Engine can return:

```text
MissingEvidenceWarning / Blocker
```

according to policy.

---

# 89. Exemption Certificate

Tax Party Profile can reference Platform File evidence/metadata.

Tax does not store document bytes.

---

# 90. Product Exemption

Product category may cause exemption/zero rate, but the rule is Country Pack effective-dated.

No permanent `product.is_tax_exempt` global boolean.

---

# 91. Party Exemption

Likewise no naive:

```text
customer.tax_exempt = true
```

without scope/effective dates/evidence.

---

# 92. Tax Registration Verification

Country Pack/integration can verify tax registration numbers when official service exists.

Verification metadata:

```text
Verified
Unverified
Failed
Expired
```

Never assume format validation = legal verification.

---

# 93. Tax Number Storage

Registration number is tax-domain data associated with Party/Legal Entity tax profile.

Sensitive access/audit policies apply.

---

# 94. Registration Number Snapshot

Final legal document snapshots the tax IDs legally printed/reported.

Changing master number later does not alter old document.

---

# 95. Purchase Tax Recoverability

On purchases, tax may be:

```text
Recoverable
NonRecoverable
PartiallyRecoverable
```

This affects accounting/valuation.

---

# 96. Recoverability Ownership

Tax Engine determines/returns tax recoverability classification based on Country Pack/config context.

Ledger/Inventory decides accounting posting/valuation according to Accounting Fact contracts.

---

# 97. Partial Recoverability

Result can include:

```text
RecoverablePercentage
RecoverableAmount
NonRecoverableAmount
```

No rounding ambiguity.

---

# 98. Inventory Valuation Boundary

Non-recoverable purchase tax may need to become cost/expense.

Tax produces structured facts.

Inventory/Ledger owns final valuation/posting.

---

# 99. Withholding Tax Boundary

Withholding is not the same as VAT/indirect tax.

Do not inject withholding into the normal VAT line calculator blindly.

---

# 100. Withholding Engine

Tax domain can later own a separate:

```text
IWithholdingTaxCalculator
```

because withholding can depend on:

- supplier/customer class;
- thresholds;
- cumulative amounts;
- payment events;
- certificate/reporting behavior.

---

# 101. Withholding Calculation Timing

May occur:

```text
Invoice
Payment
Settlement
```

depending on country.

Therefore it must not be forced into invoice VAT pipeline.

---

# 102. Design Now / Build by Country Need

Withholding contracts and data seam are designed now.

Full Egypt/KSA/UAE withholding behavior is implemented only from verified Country Pack requirements.

---

# 103. Tax Rounding Uses FinBase

Tax does NOT call random `Math.Round`.

It requests explicit FinBase/Financial Rounding policy context.

---

# 104. Tax-Specific Rounding Policy

Country Pack can contribute statutory tax rounding rules:

```text
PerLine
PerTaxComponent
PerDocument
```

and rounding mode/scale.

---

# 105. Line vs Document Rounding

These can produce different totals.

The selected strategy must be explicit and snapshotted.

---

# 106. Tax Precision

Tax calculation may use high precision internally.

Round only at legally/business-defined boundaries.

---

# 107. Rounding Residual

If line rounded tax sum differs from document rounded tax:

apply explicit residual allocation policy.

Never silently drop 0.01.

---

# 108. Residual Allocation

Potential deterministic strategies:

```text
LargestTaxableBase
LastEligibleLine
Proportional
CountrySpecific
```

Country Pack chooses allowed behavior.

---

# 109. Negative Documents

Credit notes/returns use signed or reversal semantics according to document type and Country Pack.

Do not simply run a positive invoice formula with random negative signs.

---

# 110. Credit Note Tax Snapshot

Should reference/reproduce original tax context where legally required.

Invoicing Pack finalizes correction document workflow.

---

# 111. Tax Rate Versioning

Example:

```text
Rule v1 valid until date X
Rule v2 effective from date Y
```

Old finalized documents keep v1 snapshot.

---

# 112. No Destructive Tax Rate Edit

Used active statutory rate cannot be overwritten.

Correction = new version/superseding rule.

---

# 113. Backdated Document

Tax Engine resolves rule effective at TaxDate, not today's rule.

Whether backdating is allowed is document/Ledger policy.

---

# 114. Future-Dated Document

Draft preview can resolve scheduled rules if business date is future.

Finalization validates again.

---

# 115. Finalization Freeze Point

Each legal document defines when Tax Snapshot becomes frozen.

Typical Invoice:

```text
Issue/Post/Legal Finalization
```

Country Pack/Compliance can require regulatory clearance before legal release.

---

# 116. Posted ≠ Issued

Already locked architecture rule:

```text
Accounting State
Regulatory State
Document Lifecycle
Settlement State
```

are separate.

Tax calculation must not collapse them.

---

# 117. Tax Snapshot

Immutable snapshot concept:

```text
TaxSnapshot
{
    CapabilityState
    TaxRegistrationSnapshot

    TaxDate
    JurisdictionSnapshot

    PartyTaxSnapshot
    ProductTaxSnapshot

    TaxRuleSetVersion
    RoundingPolicyVersion

    PriceMode

    NetAmount
    TaxLines[]
    TaxTotal
    GrossAmount

    CalculationVersion
}
```

---

# 118. Why Snapshot Is Mandatory

Without snapshot:

- changing rate changes old invoice;
- product category change changes old invoice;
- party status change changes old invoice;
- address change changes jurisdiction;
- Country Pack upgrade rewrites history.

All unacceptable.

---

# 119. Snapshot Is Not Live Foreign Keys Only

References alone are not enough.

Store legally relevant resolved values.

---

# 120. Snapshot Size

Do not dump full Country Pack rule JSON into each line.

Persist compact legal/calculation evidence.

---

# 121. Tax Explanation Snapshot

Optional compact explanation:

```text
Treatment code
Rule code/version
Legal reason code
Jurisdiction
```

Enough for support/audit.

---

# 122. Tax Calculation Version

Example:

```text
tax_engine_v1
```

Breaking algorithm semantics require new version/ADR.

---

# 123. Determinism

Same:

```text
Tax Context Snapshot
Country Rule Set Version
Rounding Policy
Calculation Version
```

must produce exactly the same result.

---

# 124. No Hidden Current Settings

Pure calculation stage never reads:

```text
latest tax rate
current product category
today
current party address
```

after context is frozen.

---

# 125. Tax Calculation Pipeline

```text
1. Capability Check
2. Resolve Registration
3. Resolve Tax Date
4. Resolve Jurisdiction / Place of Supply
5. Resolve Party Tax Profile
6. Resolve Product Tax Profile
7. Resolve Tax Rule Set
8. Build Tax Base
9. Calculate Components
10. Apply Explicit Rounding
11. Reconcile Residual
12. Validate Legal Requirements
13. Produce Result + Explanation
14. Snapshot on Finalization
```

---

# 126. Tax Engine Pure Core

After all context/rules are loaded:

```text
TaxCalculator.Calculate(context, resolvedRules)
```

should be pure deterministic in-memory logic.

---

# 127. No DB Calls Per Tax Component

Data resolution happens before calculation.

---

# 128. Batch Tax Calculation

Mandatory:

```text
CalculateTaxesBatch()
```

for documents with many lines.

---

# 129. Batch Context Optimization

Document-level context resolved once:

```text
Legal Entity
Registration
Tax Date
Jurisdiction base context
Party profile
Country Rule Set
Rounding Policy
Currency policy
```

Product tax profiles resolved in batch.

---

# 130. No 500-Line N+1

A 500-line invoice MUST NOT create:

```text
500 registration queries
500 party queries
500 country-pack queries
500 rate queries
```

---

# 131. Product Tax Context Batch

Catalog IDs → Tax profile map in one/few bounded queries.

---

# 132. Party Context Once

One invoice normally has one primary Party.

Resolve Party tax profile once unless line-specific counterparty exists.

---

# 133. Document Charges

Charges passed as typed taxable components.

No repeated document re-query.

---

# 134. Performance Fast Path — Tax Disabled

When capability disabled, tax calculation path should be near-zero overhead.

---

# 135. Performance Fast Path — Simple VAT

Most SME invoices:

```text
one registration
one jurisdiction
few product tax categories
one standard rule
```

Engine must optimize common case.

---

# 136. Safe Caching

Good cache candidates:

```text
Country Pack immutable rule set version
Tax categories
Registration configuration
Product tax classification
Party tax profile version
Tax policy
```

---

# 137. Cache Is Not Source of Truth

Versioned DB/Country Pack data remains authoritative.

---

# 138. No Redis Requirement Day 1

In-memory immutable rule cache + PostgreSQL indexes first.

Use Redis only with measured evidence.

---

# 139. Cache Key

Must include relevant:

```text
Tenant
Legal Entity
Country Pack Version
Tax Rule Set Version
Effective Date bucket where needed
```

No cross-tenant leakage.

---

# 140. Country Pack Compilation

On activation/update, Country Pack tax rules can compile into optimized in-memory representation.

---

# 141. Tax Rule Update

Version activation creates clean cache invalidation boundary.

---

# 142. Tax Country Pack Upgrade

Process:

```text
Install new Pack version
Validate rules
Run golden vectors
Review effective dates
Activate
Publish event
```

Old documents remain on old snapshots.

---

# 143. Country Pack Rollback

Do not rewrite historical transactions.

Rollback affects future resolution only, using explicit version activation.

---

# 144. Tax Currency

Tax calculation occurs in transaction/document currency unless local rules require another tax/reporting currency.

---

# 145. Tax FX

If statutory conversion is required, Tax asks FinBase using explicit:

```text
Purpose = Tax
RateDate
Currency Pair
```

---

# 146. Tax Does Not Own FX

No:

```text
tax_exchange_rate_table
```

unless it stores a snapshot/reference from FinBase.

---

# 147. Tax FX Snapshot

If conversion used:

```text
TaxFxSnapshot
```

is preserved in Tax Snapshot.

---

# 148. Accounting FX Can Differ

Tax statutory FX and Accounting FX can legitimately differ by purpose.

This is modeled explicitly, not hidden.

---

# 149. Tax Base Currency

Do not assume Tax reporting currency is always Legal Entity Base Currency.

Country Pack decides.

---

# 150. Pricing Integration

Pricing returns:

```text
UnitPrice
Currency
PriceMode
Discount facts
```

Tax calculates tax.

---

# 151. Catalog Integration

Catalog provides:

```text
ProductVariantId
TemplateId
UOM/quantity snapshot
Nature
```

Tax profile adds tax classification.

---

# 152. Directory Integration

Directory provides Party and Address snapshots/context.

Tax profile adds tax registration/treatment data.

---

# 153. Invoicing Integration

Invoicing:

- builds line context;
- requests tax result;
- stores Tax Snapshot;
- displays totals;
- never independently recomputes tax.

---

# 154. Purchasing Integration

Same engine, Direction=Purchase.

Purchase-specific recoverability/import logic comes through context/Country Pack.

---

# 155. Sales Integration

Quotation/order previews can call Tax.

Final invoice legal tax snapshot belongs final legal document workflow.

---

# 156. POS Integration

Same Tax Engine rules.

Offline POS requires separate Pack/snapshot sync design later.

---

# 157. E-Commerce Integration

Website can show tax-included/excluded prices per market.

Backend Tax Engine remains authority.

---

# 158. Ledger Integration

Tax Engine outputs **Tax Facts**, not account IDs.

---

# 159. Tax Accounting Fact

Concept:

```text
TaxAccountingFact
{
    TaxType
    Treatment
    TaxableBase
    TaxAmount
    RecoverableAmount?
    NonRecoverableAmount?
    Currency
    Jurisdiction
    PartyRef?
    ProductTaxCategory?
    Dimensions
    SourceDocumentRef
}
```

---

# 160. Ledger Account Determination

Ledger maps Tax Facts to accounts.

Tax module does not own:

```text
VAT Payable Account ID
Input VAT Account ID
```

as universal posting truth.

Country Pack/Accounting configuration can contribute account determination metadata/templates.

---

# 161. Compliance Boundary

Tax calculation:

> how much tax and why.

Compliance:

> how to submit/clear/report document to government.

Different domains.

---

# 162. Compliance Example

Tax result:

```text
Tax = 140
```

Compliance may then need:

```text
XML/JSON schema
UUID
signature
certificate
hash
submission
clearance/rejection
QR
```

Tax Engine does not own these.

---

# 163. Tax Reporting Boundary

Tax Report/Return preparation can consume Tax Snapshots/Facts/read models.

Do not make calculator itself a filing engine.

---

# 164. Regulatory State

Compliance owns:

```text
Pending
Submitted
Cleared
Rejected
Cancelled
```

Tax owns calculation status.

---

# 165. Tax Calculation Status

Potential:

```text
NotRequired
NotEvaluated
Resolved
Warning
Blocked
```

Separate from regulatory status.

---

# 166. Legal Release Gate

Country Pack/Compliance can say:

```text
Document may not legally issue until compliance clearance.
```

Tax Engine only returns tax readiness.

---

# 167. Tax Readiness

Result can contain:

```text
Ready
MissingProductClassification
MissingPartyTaxData
MissingRegistration
MissingEvidence
NoApplicableRule
Conflict
```

---

# 168. Progressive Disclosure

Normal salesperson should not see Tax Admin complexity.

---

# 169. UX — Tax Disabled Company

Invoice:

```text
Item        1,000
Total       1,000
```

No:

```text
Tax 0%
Tax Total 0
```

unless document format legally requires it.

---

# 170. UX — Tax Enabled Simple Company

Invoice:

```text
Subtotal  1,000
Tax         140
Total     1,140
```

---

# 171. UX — Advanced Accountant

Can inspect:

```text
Tax Treatment
Rate
Taxable Base
Tax Amount
Jurisdiction
Tax Date
Rule Version
Registration
Reason Code
```

---

# 172. UX — "Why This Tax?"

Finance/Tax admin action:

```text
Why this tax?
```

Example explanation:

```text
Country Pack: ...
Tax Date: ...
Party Treatment: ...
Product Category: ...
Matched Rule: ...
Result: ...
```

---

# 173. UX — Missing Tax Configuration

Business-friendly:

> "هذا المنتج يحتاج تصنيفًا ضريبيًا قبل إصدار المستند."

Not:

```text
TaxRuleNotFoundException
```

---

# 174. UX — Tax Configuration Setup

Legal Entity setup:

```text
Does this entity need tax tracking in TODIR?
[No] [Yes]
```

If No:

hide advanced tax setup.

If Yes:

Country Pack guides registration/configuration.

---

# 175. Setup Wording

Avoid:

> "Are you legally exempt?"

unless official legal classification is known.

Use:

> "هل تريد تفعيل إدارة وحساب الضرائب لهذا الكيان؟"

Then legal registration details are separate.

---

# 176. Country Pack Setup Wizard

When Tax enabled:

- tax scheme;
- registration status;
- registration number;
- effective date;
- required profile fields;
- default price tax mode where relevant.

---

# 177. Setup Validation

Before Tax Go-Live:

- Country Pack installed.
- registration configured.
- tax policies active.
- product tax coverage.
- party/profile defaults.
- rounding rules.
- test invoice passes.

---

# 178. Tax Go-Live Date

Per Legal Entity / Tax Scheme:

```text
TaxGoLiveDate
```

helps migration boundaries.

---

# 179. Historical No-Tax Data

Documents before Tax Go-Live remain unchanged.

---

# 180. Enabling Tax Later

Company can start with capability disabled, then activate from effective date.

Activation does not rewrite past transactions.

---



# 181. Disabling Tax After Go-Live

After finalized taxable history exists:

normal toggle-off is blocked or requires controlled Wind-Down.

Do not let admin erase active tax behavior from ongoing obligations.

---

# 182. Tax Wind-Down

Potential lifecycle:

```text
Active
WindDown
ReadOnly
DisabledForNewTransactions
```

Used for deregistration/migration according to Country Pack.

---

# 183. Deregistration

Deregistration has effective date.

Transactions before/after follow their applicable context.

---

# 184. Registration Change Snapshot

Changing registration number/status does not alter old legal documents.

---

# 185. Tax Group Membership Change

Effective-dated.

Never retroactively regroup historical transactions silently.

---

# 186. Tax Registration Threshold Monitoring

This is advisory/compliance assistance, not the core calculator.

Future capability can monitor turnover and warn:

```text
"Registration threshold may be approaching."
```

---

# 187. Threshold Monitor Is Country Pack Driven

Thresholds can change and have legal qualifications.

Must be official-source versioned.

---

# 188. No Automatic Legal Registration Decision

TODIR can alert.

It does not autonomously declare:

```text
"You are legally required to register"
```

without clearly defined statutory logic and appropriate disclaimer/review.

---

# 189. Registration Threshold Evidence

Country Packs can attach official authority references.

Example official UAE FTA pages currently specify mandatory and voluntary VAT thresholds and were updated in 2026.

Such values belong Pack content, not hardcoded Core.

---

# 190. Tax Master Data Ownership Table

| Concept | Owner |
|---|---|
| Legal Entity | Organization |
| Party identity/address | Directory |
| Product identity/UOM | Catalog |
| Commercial Price | Pricing |
| Currency/FX/Rounding | FinBase |
| Tax Registration | Tax |
| Party Tax Profile | Tax |
| Product Tax Profile | Tax |
| Tax Rules/Rates | Tax + Country Pack |
| Tax Snapshot | Consuming legal document |
| Tax Accounting Fact | Contract consumed by Ledger |
| Tax Accounts | Ledger/Accounting |
| Government submission | Compliance |

---

# 191. Tax Type

Stable conceptual type:

```text
VAT
Excise
LocalLevy
Environmental
CustomsRelated?
Withholding
```

Country Pack determines which are active.

---

# 192. Tax Code

Human/business code:

```text
VAT_STD
VAT_ZERO
VAT_EXEMPT
```

but code naming comes from Pack/config.

Do not encode legal rate in identity if it changes.

---

# 193. Avoid Rate in Permanent Identity

Bad permanent identity:

```text
VAT14
```

if rate could later change.

Better:

```text
VAT_STANDARD
```

with effective-dated rate definition.

Legacy/export/report codes may still include percentage if authority requires.

---

# 194. Tax Category

Reusable classification such as:

```text
StandardGoods
MedicalGoods
ExportService
ExemptFinancialService
```

Pack-specific.

---

# 195. Category Localization

Arabic/English display names.

Stable code remains language-neutral.

---

# 196. Product Classification Migration

Import mapping can assign tax categories by:

- SKU;
- product category;
- external legacy tax code.

Human review for ambiguous items.

---

# 197. Product Tax Coverage

Tax-enabled Legal Entity can run coverage report:

```text
Products requiring tax classification
Products using fallback
Products with expired classification
```

---

# 198. Default Product Tax Category

Country Pack may allow Entity default for simple businesses.

Example:

most products Standard.

This reduces user work.

---

# 199. Explicit Product Override

Products with special treatment override default.

---

# 200. Default Does Not Mean Hardcoded

Default is configuration/rule, still versioned.

---

# 201. Party Tax Category Defaults

Similarly, normal domestic registered customer may use default profile.

Only exceptions need manual configuration.

---

# 202. Progressive Complexity

SME:

```text
Enable Tax
Enter Registration Number
Most products use Standard
```

Advanced company:

- special products;
- exports;
- reverse charge;
- multi-registration;
- tax groups.

Same architecture.

---

# 203. Tax Rule Matching

Resolution must identify exactly one applicable Tax Stack/treatment for each taxable unit/line context.

---

# 204. Rule Match Inputs

Possible:

```text
Country
Tax Registration
Direction
Jurisdiction
Party Type/Profile
Party Registration State
Product Tax Category
Document Kind
Transaction Type
Place of Supply
Tax Date
Price Mode
```

---

# 205. Rule Inputs Must Be Typed

No arbitrary string-expression DSL.

---

# 206. Rule Activation Validation

Check:

- required references;
- effective dates;
- rate validity;
- jurisdiction validity;
- no ambiguous overlap;
- no component cycle;
- legal source metadata for Pack rules.

---

# 207. Tax Stack

Concept:

```text
TaxStackDefinition
{
  Components[]
  RoundingStrategy
  PriceModeSupport
}
```

---

# 208. Tax Component

```text
TaxComponentDefinition
{
  TaxType
  TaxCode
  Method
  Rate/Amount
  Sequence
  BaseRule
  Treatment
  Effective dates
}
```

---

# 209. Base Rule

Structured:

```text
OriginalNet
NetAfterDiscount
NetPlusPriorTaxComponents
QuantityBased
CustomPackStrategy
```

`CustomPackStrategy` requires code shipped/reviewed with trusted Country Pack, not tenant code.

---

# 210. Trusted Country Pack Code

Some legal regimes may need algorithms too complex for generic data rules.

Country Pack may supply signed/reviewed implementation of a stable interface.

This is controlled product code, not arbitrary customer plugin code.

---

# 211. Country Pack Algorithm Version

Any code-based tax strategy exposes:

```text
strategy_id
version
```

and has golden legal test vectors.

---

# 212. Fixed-Per-Unit Tax

Needs quantity basis and UOM normalization.

---

# 213. Quantity Precision

Catalog Quantity rules apply.

Tax calculation uses exact quantity snapshot.

---

# 214. Mixed Tax Items

Invoice may contain:

```text
Standard
Zero Rated
Exempt
```

lines simultaneously.

Document TaxResult aggregates by tax treatment/code without losing line detail.

---

# 215. Document Tax Summary

Output:

```text
TaxSummary[]
{
  TaxCode
  Rate/Treatment
  TaxableBase
  TaxAmount
}
```

Used for UI/printing/reporting.

---

# 216. Summary Is Derived From Line Tax Snapshots

Do not calculate summary separately using a different algorithm.

---

# 217. Document-Level Taxes

Some regimes/charges may apply at document level.

Engine supports typed document component if Country Pack needs it.

Do not fake it as a product line.

---

# 218. Header Discount Allocation

If document-level discount affects tax bases:

consuming document calculation allocates discount using approved strategy, then Tax Engine calculates on allocated bases OR Pack strategy explicitly handles it.

One owner per step.

---

# 219. Charge Allocation

Same principle.

---

# 220. Tax Base Reconciliation

Invariant:

```text
Document Tax Summary
==
aggregation of authoritative tax components
```

subject to explicit document rounding/residual policy.

---

# 221. Net/Gross Reconciliation

For simple taxes:

```text
Net + Tax = Gross
```

For withheld taxes/charges, present separate semantics; do not force everything into this identity.

---

# 222. Withholding Presentation

Withholding may reduce amount payable without reducing invoice gross in the same way as VAT.

Separate totals:

```text
Invoice Gross
Withheld Amount
Amount Payable
```

when applicable.

---

# 223. Purchase Recoverability Presentation

Normal Purchasing user can see:

```text
Tax
Total
```

Finance user can inspect:

```text
Recoverable
Non-recoverable
```

---

# 224. Tax Source of Truth

Current statutory rule source:

```text
Country Pack version + Tax rule version
```

Transaction truth:

```text
Tax Snapshot
```

Do not confuse them.

---

# 225. Country Pack Manifest

Concept:

```yaml
country_pack:
  country: EG
  version: 2026.x
  capabilities:
    vat: true
    excise: optional
    withholding: optional
    tax_grouping: false_or_supported
    e_invoice: separate
  tax_rule_sets:
    - ...
  official_sources:
    - ...
```

---

# 226. Country Pack Compatibility

Manifest declares required Tax Engine contract version.

---

# 227. Pack Upgrade Gate

CI:

- schema validation;
- legal source metadata;
- golden vectors;
- backward compatibility;
- overlapping effective dates;
- rule conflict;
- performance.

---

# 228. Pack Release Review

Requires domain reviewer/accountant/tax specialist sign-off before Production.

---

# 229. Pack Legal Disclaimer

TODIR configuration/tooling supports compliance but does not replace professional legal/tax advice.

Documentation should say this without making UX hostile.

---

# 230. Egypt Pack

Initial Egyptian Pack should be based on current Egyptian Tax Authority official law/regulation pages.

Important: ETA currently lists multiple VAT-law/regulation amendments, including 2025 and 2026 changes.

Therefore implementation must use the current consolidated interpretation reviewed by a qualified Egyptian tax expert.

---

# 231. KSA Pack

Initial Saudi Pack uses ZATCA official VAT Law, Implementing Regulations and current guidelines.

ZATCA maintains amendments/guidance; Pack release must capture effective versions.

---

# 232. UAE Pack

Initial UAE Pack uses Federal Tax Authority official VAT legislation, executive regulation and public clarifications.

FTA had current 2026 VAT legislative/public clarification updates, so Pack cannot be treated as static.

---

# 233. Do Not Copy Competitor Rates

Competitor documentation helps identify product patterns.

It is not statutory source.

---

# 234. Country Pack Test Evidence

Every legal rule test links to:

```text
source document
article/section/reference
effective date
reviewer
```

where practical.

---

# 235. Legal Evidence Registry

Concept:

```text
LegalRuleEvidence
{
  SourceAuthority
  SourceTitle
  URL/Reference
  PublicationDate?
  EffectiveDate?
  PackVersion
  Notes
}
```

Can live in source repository metadata rather than transactional DB.

---

# 236. Runtime Does Not Depend on Web Source

Country Pack ships validated local rules.

No live scraping government websites during invoice calculation.

---

# 237. Government API Outage

Tax calculation continues from locally approved rules.

Government submission belongs Compliance and can queue/retry.

---

# 238. Tax Engine Availability

Tax-enabled invoice finalization requires local Tax Engine/data.

No fallback to frontend or remembered tax rate.

---

# 239. Draft Offline Behavior

Frontend may show last backend preview, but final confirmation requires server-authoritative recalculation.

---

# 240. Frontend Tax Preview

Backend endpoint/debounced calculation.

Frontend can do cosmetic subtotal preview only; statutory TaxResult remains backend authority.

---

# 241. Tax Calculation Fingerprint

Result can include:

```text
input_hash
rule_set_version
calculation_version
rounding_policy_version
```

Detect stale draft preview.

---

# 242. Finalization Recalculation

On Confirm/Post/Issue:

rebuild current eligible context and verify/freeze.

If rule changed since preview:

return updated result and require appropriate user flow.

---

# 243. Tax Rule Changed During Draft

Draft can be recalculated.

Finalized snapshot never changes.

---

# 244. Tax Date Lock

After legal finalization, Tax Date changes only through permitted correction mechanism.

---

# 245. Error Codes

Stable:

```text
tax.capability_disabled
tax.registration_missing
tax.registration_invalid
tax.product_classification_missing
tax.party_profile_missing
tax.jurisdiction_unresolved
tax.rule_missing
tax.rule_conflict
tax.stack_cycle
tax.evidence_missing
tax.rounding_policy_missing
tax.fx_rate_missing
tax.calculation_mismatch
tax.country_pack_missing
tax.country_pack_incompatible
```

---

# 246. Tax Disabled Error Semantics

Normal no-tax workflow should not throw an error.

Returns:

```text
NotEvaluatedBecauseCapabilityDisabled
```

with zero TaxLines and commercial total passthrough.

---

# 247. Tax-Enabled Missing Rule

Do NOT silently return zero.

Block legal finalization.

---

# 248. Missing Product Classification

If default category safely applies, use default and record it.

If no safe default:

block or warn according to Pack.

---

# 249. Missing Party Registration

Country Pack can:

- treat as unregistered;
- block;
- request data;
- use default.

Not generic hardcoding.

---

# 250. Tax Rule Conflict Is Critical Configuration Error

Runtime should:

- fail safely;
- trace config IDs;
- alert admin/support.

---

# 251. Permissions

Initial:

```text
tax.read
tax.registration.read
tax.registration.manage

tax.party_profile.read
tax.party_profile.manage

tax.product_profile.read
tax.product_profile.manage

tax.policy.read
tax.policy.manage

tax.rule.read
tax.rule.manage
tax.rule.activate

tax.simulate
tax.audit.view
tax.import
tax.export
```

Country Pack statutory rule editing may be blocked entirely.

---

# 252. Tax Capability Permission

```text
tax.capability.configure
```

Elevated.

After Tax Go-Live, disable/wind-down guarded.

---

# 253. Sensitive Registration Number Access

Mask/limit according to business need.

Legal invoice output still includes what law requires.

---

# 254. Approval Hooks

Sensitive operations:

```text
BeforeTaxRuleActivate
BeforeTaxRegistrationChange
BeforeTaxCapabilityDisable
```

can require approval.

---

# 255. Audit

Mandatory audit:

- Tax capability changes.
- Registration create/change/deregister.
- Party/Product tax classification change.
- Tenant-defined tax rule change.
- Country Pack activation.
- Legal override.
- Tax Date override where allowed.

---

# 256. Audit Is Not Every Calculation

Do not create audit row for each tax preview.

Final transaction snapshot provides history.

---

# 257. Tax Calculation Trace

On-demand/debug:

```text
Context
Jurisdiction resolution
Registration resolution
Rule candidates
Winning rule
Base calculation
Tax components
Rounding
Result
```

---

# 258. Production Trace

Do not persist full verbose trace for every line by default.

Structured tracing sampled/on-demand.

---

# 259. "Why This Tax?" Uses Structured Explanation

Not raw trace.

---

# 260. Observability Metrics

Safe-cardinality:

```text
tax_calculation_duration
tax_batch_duration
tax_batch_lines
tax_rule_missing_total
tax_rule_conflict_total
tax_classification_missing_total
tax_rounding_residual_total
tax_country_pack_version
tax_cache_hit_ratio
```

Do not label Tenant/Product IDs.

---

# 261. Alerting

Alert on:

- runtime rule conflict;
- systematic missing classification;
- Country Pack activation failure;
- unusual tax calculation error spike.

---

# 262. Performance Target

Simple cached document-tax context:

Tax pure calculation should be negligible compared with DB/API.

Initial service target for ordinary batch calculation:

```text
p95 document tax resolution/calculation < 50–100ms
```

for typical SME document sizes after Product contexts are batch-resolved.

Validate, do not promise blindly.

---

# 263. Large Document Target

500 lines must remain bounded/no N+1.

Benchmark realistic mixed tax categories.

---

# 264. Tax Disabled Target

Near-zero incremental overhead.

---

# 265. Database Schema

Use:

```text
foundation_tax
```

---

# 266. Core Tables

```text
tax_capability_state
tax_registration
tax_registration_member

tax_scheme
tax_type
tax_jurisdiction
tax_category

tax_policy

party_tax_profile
product_tax_profile

tax_rule_set
tax_rule
tax_rule_version

tax_stack_definition
tax_component_definition

tax_pack_activation
```

Some reference definitions can be pack/system scope rather than tenant rows.

---

# 267. Tax Capability State Table

Concept:

```text
tenant_id
legal_entity_id
state
effective_from
go_live_date?
country_pack_id?
version
```

---

# 268. Tax Registration Table

```text
id
tenant_id
scheme
country
registration_number
status
effective_from
effective_to
verification_state
version
```

---

# 269. Tax Registration Member

```text
tenant_id
tax_registration_id
legal_entity_id
effective_from
effective_to
```

supports group readiness.

---

# 270. Party Tax Profile

```text
tenant_id
party_id
legal_entity_id
tax_scheme
tax_category_id?
registration_number?
registration_country?
status?
effective dates
evidence_ref?
version
```

---

# 271. Product Tax Profile

```text
tenant_id
product_template_id
variant_id?
legal_entity_id?
country/tax_scheme scope
tax_category_id
effective dates
version
```

---

# 272. Tax Rule Set

```text
id
country_pack_id
tenant_id?  // null/system for pack rule sets where architecture permits
scheme
version
effective_from
effective_to
status
provenance
```

---

# 273. Tax Rule

Typed conditions + result reference.

Do not use arbitrary EAV for hot legal dimensions.

---

# 274. Tax Component Definition

```text
tax_type
tax_code
method
rate_or_amount
base_rule
sequence
affects_subsequent_base
reporting_code
treatment
```

exact fields refined per engine contracts.

---

# 275. RLS

All tenant-owned Tax tables:

```text
ENABLE ROW LEVEL SECURITY
FORCE ROW LEVEL SECURITY
```

---

# 276. Country Pack System Reference Data

System pack tables/read-only data use separate controlled DB role/schema strategy.

Tenant cannot edit Pack definitions.

---

# 277. Cross-Tenant IDs

APIs validate tenant/entity scope even when RLS exists.

Defense in depth.

---

# 278. Indexing

Critical:

```text
tenant_id + legal_entity_id + effective dates
party_id + entity
product_template/variant + entity
tax_rule_set + status/effective dates
registration + scheme + dates
```

---

# 279. Effective Date Query Index

Tax history is read often by specific TaxDate.

Use indexes supporting:

```text
effective_from <= date
effective_to is null or >= date
```

and active version selection.

---

# 280. Range Types

PostgreSQL range/exclusion constraints may be useful for non-overlapping effective periods.

Use only when they simplify correctness.

---

# 281. No Giant Tax DbContext Graph

`TaxDbContext` owns Tax only.

---

# 282. Cross-Module Contracts

Project:

```text
Todir.Foundation.Tax.Contracts
```

---

# 283. Core Contracts

```text
ITaxCalculator
ITaxBatchCalculator
ITaxContextResolver
ITaxDateResolver
ITaxJurisdictionResolver
ITaxPolicyProvider
ITaxExplanationProvider
```

---

# 284. Country Pack Ports

```text
ICountryTaxRuleProvider
ICountryTaxJurisdictionProvider
ICountryTaxRegistrationValidator
ICountryTaxDocumentRequirementProvider
ICountryTaxRoundingProvider
```

Names can be simplified in implementation, but responsibilities remain separate.

---

# 285. No God Tax Service

Forbidden:

```text
TaxService.DoEverything()
```

---

# 286. API Surface

Base:

```text
/api/v1/tax
```

Admin:

```text
GET/PUT /legal-entities/{id}/capability
GET/POST /registrations
GET/POST /party-profiles
GET/POST /product-profiles
GET /country-pack
POST /rules/validate
POST /simulate
GET /coverage
```

---

# 287. Calculation Endpoint

Internal/hot path should use in-process contract.

Optional API:

```text
POST /calculate/preview
POST /calculate/batch-preview
```

for frontend/admin.

---

# 288. No Internal HTTP in Modular Monolith

Invoicing → Tax via in-process Contracts, not network call.

---

# 289. Commands

```text
EnableTaxCapability
BeginTaxWindDown

CreateTaxRegistration
UpdateTaxRegistration
DeregisterTaxRegistration

AssignPartyTaxProfile
AssignProductTaxProfile

ActivateCountryTaxPack
CreateTenantTaxRule
ActivateTenantTaxRule
```

---

# 290. Queries

```text
GetTaxCapability
GetTaxRegistration
ResolvePartyTaxProfile
ResolveProductTaxProfilesBatch
CalculateTax
CalculateTaxBatch
SimulateTax
ExplainTax
GetTaxCoverage
```

---

# 291. Events

Meaningful:

```text
TaxCapabilityEnabledV1
TaxRegistrationChangedV1
TaxCountryPackActivatedV1
TaxRuleSetActivatedV1
ProductTaxProfileChangedV1
PartyTaxProfileChangedV1
```

---

# 292. Event Volume

Product profile mass import should not flood broker with one event per row if batch/version event suffices.

---

# 293. Outbox

All durable integration events via Transactional Outbox.

---

# 294. Pack Activation Event

Other modules can invalidate cached tax display/read models.

---

# 295. Country Pack Update and Existing Drafts

Drafts may be marked:

```text
TaxRecalculationRequired
```

if their relevant TaxDate/context uses new active rules.

---

# 296. Finalized Documents Ignore Pack Update

Snapshots remain.

---

# 297. Import

Tax onboarding import:

- Party tax IDs/categories.
- Product tax categories.
- tenant-defined allowed mappings.

Do not import/edit locked Country Pack rates via spreadsheet.

---

# 298. Product Tax Import Template

```text
SKU
Tax Category
Legal Entity/Country Scope
Effective From
```

---

# 299. Party Tax Import Template

```text
Party Code
Registration Country
Tax Registration Number
Tax Treatment Category
Effective From
```

---

# 300. Import Matching

Catalog/Directory external refs.

No fuzzy auto-update of legal tax registrations without confirmation.

---

# 301. Import Preview

Show:

```text
Create
Update Draft
Invalid tax number format
Unknown product
Unknown party
Ambiguous
Effective-date overlap
```

---

# 302. Bulk Product Classification

Common SME onboarding:

select Category/Product group → assign default Tax Category.

Preview affected products.

---

# 303. Bulk Change Versioning

Changes have effective date and audit.

---

# 304. Tax Coverage Dashboard

Tax-enabled Entity:

```text
Registration configured?
Products classified?
Parties requiring registration data?
Rules active?
Country Pack current?
Missing rates?
```

---

# 305. Tax Health

Green/Warning/Blocked.

Normal employees do not see tax admin dashboard.

---

# 306. Legal Update Notification

When TODIR publishes Country Pack update:

Tax Admin receives:

```text
What changed
Effective date
Affected rule areas
Required action
```

---

# 307. Automatic Pack Activation

Do not auto-activate material statutory change blindly in every tenant unless release policy guarantees correctness/effective date.

Use controlled deployment/activation semantics.

---

# 308. Mandatory Legal Update

Some Pack updates may need platform-forced activation by effective date.

This requires Product/Legal release policy and customer notification.

---

# 309. Country Pack Data Migration

If classification codes change:

provide migration mapping/preview.

Do not destroy old snapshots.

---

# 310. Tax Reports Read Model

Tax Reporting later consumes finalized Tax Snapshots/Facts into read models.

Calculator DB is not reporting warehouse.

---

# 311. Tax Reporting Code

Each TaxLine can include Pack reporting code for future VAT returns.

---

# 312. Reporting Code Versioning

Effective-dated with Pack.

---

# 313. Tax Return Filing

Separate capability/module later.

Do not block Calculator architecture waiting for full filing engine.

---

# 314. E-Invoice

Separate Compliance Pack.

Tax Snapshot supplies needed tax breakdown.

---

# 315. E-Receipt

Same separation.

---

# 316. Tax Invoice Print

Invoicing/Template engine prints fields required by Country Pack/Compliance.

Tax provides legal data snapshot.

---

# 317. Legal Mentions

Country Pack can return:

```text
legal_reason_code
legal_reason_text
```

for exemption/zero/reverse-charge wording.

---

# 318. Translation

Country Pack provides Arabic/English legal display labels.

Store stable codes.

---

# 319. Printed Legal Text Snapshot

If legal wording must be frozen, document snapshot keeps version/text or template reference/version.

---

# 320. Tax Number Format Validation

Country Pack validates syntax.

External verification is separate.

---

# 321. Tax Number Uniqueness

Not globally assumed.

Country Pack decides scope/uniqueness rules.

---

# 322. Multiple Registrations

One Legal Entity can potentially have multiple scheme/jurisdiction registrations.

Schema must not assume one `tax_number` field.

---

# 323. Branch Registrations

Some jurisdictions may require branch/location-specific registration/reporting.

Design registration scope/member/location seam.

Do not implement speculative UI unless needed.

---

# 324. Tax Group

Country Pack capability flag enables group UI/rules.

---

# 325. Tax Unit vs Accounting Entity

Tax registration/group can span entities, but accounting remains per Legal Entity.

Tax calculation returns registration/tax-unit context without merging Ledgers.

---

# 326. Intercompany

Intercompany transaction may still be taxable depending on rules/group status.

Do not automatically set tax 0 because both entities share Tenant.

---

# 327. Cross-Border

Party country alone is insufficient.

Use place-of-supply/legal context.

---

# 328. Free/Designated Zones

Country Pack can model special jurisdictions/zones.

UAE FTA publishes zone-specific guidance, validating need for this seam.

---

# 329. Address Normalization

Tax location matching should use structured Country/Region codes, not free-text city comparisons where avoidable.

---

# 330. Geocoding

Not required for initial MENA VAT.

Do not add external geospatial tax service unless market need.

---

# 331. US-Style Sales Tax

Future market may require far more granular jurisdiction resolution.

Current architecture permits provider adapter/external tax engine if needed.

Do not overbuild US nexus logic now.

---

# 332. External Tax Provider

Future optional:

```text
IExternalTaxProvider
```

for jurisdictions where maintaining rules internally is impractical.

---

# 333. External Tax Provider Is Not Automatically Truth

Persist response snapshot/evidence.

Provider outage/fallback policy explicit.

---

# 334. MENA First

Egypt/KSA/UAE Country Packs are primary validation targets.

---

# 335. Security — Mass Assignment

Tax admin APIs whitelist fields.

Cannot post `status=Approved` through generic DTO.

---

# 336. Security — IDOR

Party/Product/Registration references tenant/entity validated.

---

# 337. Security — Support Access

Support cannot silently modify statutory data.

All privileged support changes audited and permission-controlled.

---

# 338. Security — AI

AI cannot:

- change tax rate;
- activate Country Pack;
- set registration status;
- issue legal tax decision;
- override TaxResult.

AI can suggest/draft only.

---

# 339. Architecture Test

Business modules must not define their own Tax Calculator classes/rate tables.

---

# 340. Architecture Test — No Hardcoded Rates

Consider static analysis/grep rules for obvious tax-rate constants in Sales/Invoicing/Purchasing/POS projects.

Exceptions require review.

---

# 341. Architecture Test — No Direct Tax Tables

Other Modules cannot reference Tax implementation DbContext/entities.

---

# 342. Architecture Test — Money/Rounding

Tax uses FinBase numeric primitives.

---

# 343. Unit Test Matrix — Basic

```text
Tax disabled
Standard exclusive
Standard inclusive
Zero-rated
Exempt
Out-of-scope
Missing classification
Missing rule
Conflict
```

---

# 344. Unit Test Matrix — Math

```text
small decimals
large amounts
negative credit
quantity
fixed-per-unit
multiple taxes
compound base
inclusive extraction
rounding midpoint
```

---

# 345. Unit Test Matrix — Date

```text
before effective date
first effective date
last date
after expiry
backdated
future draft
```

---

# 346. Unit Test Matrix — Registration

```text
not registered
registered
pending
deregistered
tax group member
registration number change
```

---

# 347. Unit Test Matrix — Jurisdiction

```text
domestic
export
import
cross-border
special zone
```

using Country Pack test scenarios.

---

# 348. Unit Test Matrix — Recoverability

```text
fully recoverable
non-recoverable
partial
```

---

# 349. Property Test — Determinism

Candidate input order cannot change TaxResult.

---

# 350. Property Test — Totals

For simple tax stack:

```text
Net + Sum(TaxLines) == Gross
```

within explicit rounding semantics.

---

# 351. Property Test — Disabled Capability

No TaxLines, no rule lookup.

---

# 352. Property Test — No Future Rule

Resolver does not select rule effective after TaxDate.

---

# 353. Golden Legal Vectors

Each Country Pack ships official/reviewer-approved scenarios.

---

# 354. Golden Vector Structure

```text
Scenario
Source reference
Effective date
Inputs
Expected treatment
Expected base
Expected tax
Expected legal code
```

---

# 355. Egypt Pack Tests

Representative:

- domestic standard transaction;
- zero/exempt cases from current law;
- relevant construction/special amendments where in scope;
- credit/correction;
- tax date;
- e-invoice snapshot compatibility.

Exact rates/rules come from verified current ETA sources.

---

# 356. KSA Pack Tests

Representative:

- standard VAT;
- zero/exempt;
- reverse charge/import where applicable;
- transitional/effective rule;
- credit note;
- ZATCA tax breakdown compatibility.

---

# 357. UAE Pack Tests

Representative:

- standard;
- zero-rated;
- exempt;
- reverse charge;
- designated/free zone where relevant;
- tax-group registration;
- credit note.

---

# 358. Test Sources Must Be Current

Do not keep golden vector after legal rule changes without reviewing Pack version.

---

# 359. Integration Test — RLS

Tenant A cannot read/modify B tax registrations/profiles/config.

---

# 360. Integration Test — Multi-Entity

Entity A rules/registration cannot leak to Entity B.

---

# 361. Integration Test — Tax Group

When Pack supports group, membership effective dates resolve correctly.

---

# 362. Integration Test — FinBase

Inclusive/exclusive and tax rounding exact decimal behavior.

---

# 363. Integration Test — Pricing

PriceMode + discount facts produce expected Tax Context.

---

# 364. Integration Test — Invoicing Sample

Draft preview → final snapshot → master rule change → old invoice unchanged.

---

# 365. Integration Test — Ledger Fact

TaxResult maps to TaxAccountingFact without Tax owning accounts.

---

# 366. Integration Test — Country Pack Upgrade

Old final documents unchanged; new eligible docs use new rule version.

---

# 367. Integration Test — Broker Outage

Tax Pack activation commits + Outbox; cache invalidation event resumes later.

---

# 368. Load Test

500-line invoice with mixed tax categories.

No N+1.

---

# 369. Load Test — Many Tenants

Small/large tenant mix.

Cache isolation.

---

# 370. Load Test — Tax Disabled

Verify minimal overhead.

---

# 371. Red-Team — `tax_rate` on Product

Reject as reusable statutory source of truth.

Product has Tax Category/Profile.

---

# 372. Red-Team — Customer `is_tax_exempt` Boolean

Reject as sole legal truth.

Needs scoped/effective tax profile/evidence.

---

# 373. Red-Team — `company.tax_enabled`

Reject as complete model.

Capability + registration/treatment are separate.

---

# 374. Red-Team — Tax Disabled Becomes 0%

Reject.

---

# 375. Red-Team — Zero Rate Becomes Exempt

Reject.

---

# 376. Red-Team — Sales Calculates VAT

Reject.

---

# 377. Red-Team — Frontend Final Tax

Reject.

---

# 378. Red-Team — Tax Rule Edited In Place

Reject after use.

New version.

---

# 379. Red-Team — Current Address Recalculates Old Invoice

Reject.

Snapshot.

---

# 380. Red-Team — Country Pack Upgrade Changes History

Reject.

---

# 381. Red-Team — Missing Rule Defaults Zero

Reject.

---

# 382. Red-Team — Floating Point

Reject.

---

# 383. Red-Team — Per-Line DB Tax Queries

Fail performance gate.

---

# 384. Red-Team — Government API Called During Tax Calculation

Reject unless specific architecture requires external provider; even then explicit resiliency/snapshot.

---

# 385. Red-Team — Ledger Account in Tax Rule

Tax calculation rule should not directly become Ledger account determination truth.

Keep Accounting ownership.

---

# 386. Red-Team — Tax and Compliance Same State

Reject.

---

# 387. Red-Team — Withholding Mixed Into VAT Total

Reject unless explicitly modeled separate component semantics.

---

# 388. Business Acceptance — No Tax Company

Tax tracking disabled.

User creates:

```text
Qty 1
Price 1,000
Total 1,000
```

No Tax UI.

No rule queries.

---

# 389. Business Acceptance — Enable Tax Later

Entity tax tracking activated effective date.

Old docs unchanged.

New docs use Pack.

---

# 390. Business Acceptance — Simple Registered SME

Setup:

- choose Country Pack;
- enter registration;
- default standard classification.

Sales user sees simple subtotal/tax/total.

---

# 391. Business Acceptance — Special Product

Most products standard.

One product has special Tax Category.

Engine resolves different treatment automatically.

---

# 392. Business Acceptance — Export

Party/location/product context resolves export treatment and legal reason.

---

# 393. Business Acceptance — Tax Inclusive Retail

Displayed price includes tax.

Engine extracts correct base/tax.

---

# 394. Business Acceptance — Mixed Invoice

Standard + zero + exempt lines.

Correct summary groups.

---

# 395. Business Acceptance — Purchase Recoverability

Tax total split recoverable/non-recoverable.

Purchasing user simple; Finance can inspect.

---

# 396. Business Acceptance — Rate Change

New effective rule.

Old finalized invoice unchanged.

---

# 397. Business Acceptance — Missing Classification

Finalization blocked with actionable message.

---

# 398. Business Acceptance — Country Pack Update

Admin sees update/effective date.

Golden tests pass.

Future docs use update.

---

# 399. Business Acceptance — Why This Tax?

Tax Admin receives human explanation.

---

# 400. Product Acceptance — UX Simplicity

If normal user needs to understand rule IDs to issue standard invoice, design fails.

---

# 401. Implementation Order

```text
TX00 ADRs + Tax Ownership
TX01 Tax Capability State
TX02 Tax Registration + Membership
TX03 Country Pack Tax Contracts
TX04 Tax Scheme/Type/Category Reference Model
TX05 Party Tax Profile
TX06 Product Tax Profile
TX07 Tax Date Resolver
TX08 Jurisdiction / Place of Supply Resolver
TX09 Rule Set + Versioning
TX10 Tax Stack / Component Model
TX11 Deterministic Tax Calculator
TX12 Inclusive / Exclusive Calculation
TX13 Multiple / Compound / Fixed Tax Support
TX14 FinBase Rounding + Tax FX
TX15 Purchase Recoverability
TX16 Snapshot + Explanation
TX17 Batch Calculator + Performance
TX18 Admin Setup + Coverage UX
TX19 Import / Migration
TX20 Country Pack Activation Pipeline
TX21 Egypt/KSA/UAE Pack Golden Vectors
TX22 Invoicing/Purchasing/Ledger Contracts
TX23 Security / RLS / Audit
TX24 Load / Red-Team
TX25 Product + Tax Specialist Acceptance
```

---

# 402. TX00 Exit Gate

Approved ADRs:

- one Tax Engine;
- Country Pack separation;
- Tax disabled semantics;
- registration model;
- tax treatment taxonomy;
- Tax/Compliance/Ledger boundaries.

---

# 403. TX01 Exit Gate

- per Legal Entity capability.
- Disabled fast path.
- no fake 0%.
- effective activation/go-live.

---

# 404. TX02 Exit Gate

- registration history.
- group-ready membership.
- no single tax-number column assumption.

---

# 405. TX03 Exit Gate

- Country Pack ports.
- source/effective-date metadata.
- compatibility versioning.

---

# 406. TX04 Exit Gate

- schemes/types/categories.
- stable codes.
- no rate encoded into permanent category identity.

---

# 407. TX05 Exit Gate

- Party profile scoped/effective.
- Directory identity remains canonical.
- evidence references.

---

# 408. TX06 Exit Gate

- Product classification.
- Template default + Variant override.
- Catalog remains tax-rate free.

---

# 409. TX07 Exit Gate

Explicit TaxDate; no hidden current date.

---

# 410. TX08 Exit Gate

Jurisdiction resolver deterministic and Pack-driven.

---

# 411. TX09 Exit Gate

- effective rule versions.
- conflict validation.
- provenance/mutability.

---

# 412. TX10 Exit Gate

Structured tax components with no arbitrary tenant code.

---

# 413. TX11 Exit Gate

Pure deterministic calculation + explanation.

---

# 414. TX12 Exit Gate

Inclusive/exclusive golden math tests.

---

# 415. TX13 Exit Gate

Multiple/fixed/compound support only through explicit acyclic definitions.

---

# 416. TX14 Exit Gate

- FinBase rounding.
- Tax-purpose FX.
- snapshots.

---

# 417. TX15 Exit Gate

Recoverable/non-recoverable/partial purchase tax contract.

---

# 418. TX16 Exit Gate

Finalized tax snapshot survives master/profile/Pack changes.

---

# 419. TX17 Exit Gate

500 lines, batch, no N+1, disabled fast path.

---

# 420. TX18 Exit Gate

Simple SME UX + advanced Tax Admin UX.

---

# 421. TX19 Exit Gate

Safe product/party classification imports.

---

# 422. TX20 Exit Gate

Pack validate/test/activate/rollback-for-future pipeline.

---

# 423. TX21 Exit Gate

Country packs have reviewed official-source golden vectors.

---

# 424. TX22 Exit Gate

No cross-module DbContext; Tax Facts not account IDs.

---

# 425. TX23 Exit Gate

RLS/permissions/audit/pass.

---

# 426. TX24 Exit Gate

All red-team/performance scenarios pass.

---

# 427. TX25 Exit Gate

Product Architect + Tax/Accounting specialist sign-off before financial pilot.

---

# 428. Build Now — MUST

- Tax Capability per Legal Entity.
- Disabled fast path.
- effective Tax Registration.
- Country Pack interfaces.
- VAT/indirect tax generic categories/rules.
- Party/Product Tax Profiles.
- Tax Date.
- basic jurisdiction/place-of-supply seam.
- effective-dated Tax Rule Sets.
- percentage tax.
- inclusive/exclusive.
- zero/exempt/out-of-scope treatments.
- reverse-charge treatment seam.
- explicit rounding.
- Tax Snapshot.
- purchase recoverability structure.
- batch calculation.
- explanation.
- RLS.
- official-source Country Pack test system.
- Egypt/KSA/UAE initial launch Pack support according to product launch order.

---

# 429. Design Now / Build Later or By Country Need

- full Excise engines.
- withholding engine implementation.
- tax group advanced UX.
- tax registration threshold monitoring.
- external tax provider.
- complex local-jurisdiction tax.
- offline POS tax package.
- tax return filing.
- tax audit analytics.
- automated registration verification integrations.

---

# 430. Explicitly NOT Owned by This Pack

- government e-invoice protocol;
- ZATCA/ETA/FTA submission transport;
- journal posting;
- tax accounts;
- invoicing lifecycle;
- payment settlement;
- price selection;
- product/UOM identity;
- full withholding if payment-based;
- corporate income tax.

---

# 431. Senior Product Rule

> **Tax complexity must be invisible when the company does not need it, and automatic when it does.**

---

# 432. Senior Architecture Rule

> **Tax treatment is resolved from context and versioned law, never from a hardcoded percentage on Product or Invoice.**

---

# 433. Senior Legal-Integrity Rule

> **Zero-rated, exempt, out-of-scope and tax-disabled are different states even when the monetary tax amount is zero.**

---

# 434. Senior Engineering Rule

> **Tax calculation is deterministic backend code using exact decimals, explicit rounding and immutable context snapshots.**

---

# 435. Senior Performance Rule

> **Resolve document-level tax context once, resolve product classifications in batch, calculate in memory, and bypass the entire rule engine when tax capability is disabled.**

---

# 436. Senior Country-Pack Rule

> **Statutory content must have official authority evidence, effective dates and reviewed Pack versions.**

---

# 437. Senior History Rule

> **A finalized legal document is never recalculated from today's tax law or today's master data.**

---

# 438. Market Pattern Validation — Odoo

Odoo 20 supports:

- tax-excluded and tax-included computation;
- fixed/percentage tax methods;
- groups/multiple tax components;
- taxes affecting subsequent tax bases;
- fiscal positions/localization.

Its localization documentation explicitly distinguishes multiple 0%-amount meanings such as exempt, 0% and not-subject categories.

TODIR adopts the real-world requirements but keeps statutory country content in controlled Country Packs and keeps tax accounting ownership separated.

---

# 439. Market Pattern Validation — Business Central

Business Central supports:

- VAT Business Posting Groups;
- VAT Product Posting Groups;
- combinations that determine VAT treatment;
- VAT Date;
- import VAT;
- reverse-charge VAT;
- VAT rounding/reporting.

This validates separate Party/business classification + Product classification + Tax Date.

TODIR avoids coupling tax calculation directly to accounting account IDs.

---

# 440. Market Pattern Validation — ERPNext

ERPNext uses:

- Tax Categories;
- Tax Rules;
- sales/purchase tax templates;
- item tax templates;
- validity dates;
- party/location/item context;
- separate Tax Withholding Category.

This validates our classification/rule separation and the decision that withholding has distinct semantics.

---

# 441. Official Source Register — Egypt

**Egyptian Tax Authority — VAT Laws**

https://eta.gov.eg/ar/content/qwanyn-aldrybt-ly-alqymt-almdaft

Current official page reviewed 2026-10-05 lists the base VAT law and subsequent amendments/regulations, including 2025 and 2026 updates.

**ETA Document Center**

https://www.eta.gov.eg/ar/mrkz-almstndat

Country Pack engineers must use current official consolidated law/regulations and specialist review.

---

# 442. Official Source Register — Saudi Arabia

**ZATCA — VAT Law**

https://zatca.gov.sa/ar/RulesRegulations/Taxes/Pages/VATLaw.aspx

**ZATCA — VAT Implementing Regulations**

https://zatca.gov.sa/ar/RulesRegulations/Taxes/Pages/VATImplementingRegulations.aspx

**ZATCA Guides**

https://zatca.gov.sa/ar/HelpCenter/guidelines/Pages/default.aspx

Current ZATCA material includes VAT amendments/guidance. Pack must be versioned.

---

# 443. Official Source Register — UAE

**Federal Tax Authority — VAT Legislation**

https://tax.gov.ae/en/legislation/vat/vat-decree-law.aspx

**FTA — VAT Guides, References & Public Clarifications**

https://www.tax.gov.ae/en/taxes/Vat/guides.references.aspx

**FTA — VAT Registration**

https://tax.gov.ae/en/taxes/Vat/vat.topics/registration.for.vat.aspx

FTA pages reviewed in 2026 contain current legislative amendments/clarifications and registration guidance.

---

# 444. Product/ERP Reference Register

**Odoo 20 Tax Computation**

https://www.odoo.com/documentation/20.0/applications/finance/accounting/taxes/tax_computation.html

**Odoo Accounting Localization**

https://www.odoo.com/documentation/20.0/developer/howtos/accounting_localization.html

**Microsoft Business Central VAT Setup**

https://learn.microsoft.com/en-us/dynamics365/business-central/finance-setup-vat

**ERPNext Taxes**

https://docs.frappe.io/erpnext/taxes

**ERPNext Tax Category**

https://docs.frappe.io/erpnext/tax-category

**ERPNext Tax Rule**

https://docs.frappe.io/erpnext/tax-rule

**ERPNext Tax Withholding Category**

https://docs.frappe.io/erpnext/tax-withholding-category

These are product-pattern references, not statutory legal authority.

---

# 445. Final Architecture

```text
Organization
  └── Legal Entity

Tax
  ├── Capability State
  ├── Registration / Registration Membership
  ├── Party Tax Profile
  ├── Product Tax Profile
  ├── Tax Date
  ├── Jurisdiction Resolver
  ├── Country Rule Set
  ├── Tax Stack
  ├── Deterministic Calculator
  ├── Rounding / FX via FinBase
  └── Explanation

Country Pack
  └── Legal rules + effective dates + source evidence

Pricing
  └── Commercial price / price mode

Document
  └── Immutable Tax Snapshot

Ledger
  └── Tax Facts → accounts

Compliance
  └── Government format/submission/state
```

---

# 446. Final Business Verdict

For a company that does not use tax tracking:

```text
TODIR feels like there is no Tax module.
```

For a normal registered SME:

```text
TODIR calculates the right tax automatically with minimal setup.
```

For an advanced/multi-country company:

```text
TODIR can resolve registration, party/product classifications, jurisdictions, multiple treatments, tax-inclusive pricing, recoverability and Country Pack versions without changing the core architecture.
```

This is the desired product shape:

> **Simple outside. Legally and mathematically rigorous inside.**

---

# 447. Next Pack

After Tax is approved:

> **`09_ERP_SAAS_LEDGER_POSTING_ENGINE_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`**

The central question:

> **How does TODIR turn immutable business facts from Invoicing, Payments, Inventory, Payroll and other modules into correct, balanced, auditable and reversible accounting entries without letting each module write journal entries directly?**

This pack will lock:

- Chart of Accounts ownership.
- Journal model.
- Fiscal periods.
- Accounting Facts Catalog.
- Posting Engine.
- Account Determination.
- Dimensions.
- Debit = Credit invariant.
- Idempotency.
- Reversal.
- Multi-currency posting.
- tax facts.
- managed ledger vs full accounting.
- opening balances.
- performance.
- immutable posted entries.

---

**END OF FILE — 08_ERP_SAAS_TAX_ENGINE_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md**

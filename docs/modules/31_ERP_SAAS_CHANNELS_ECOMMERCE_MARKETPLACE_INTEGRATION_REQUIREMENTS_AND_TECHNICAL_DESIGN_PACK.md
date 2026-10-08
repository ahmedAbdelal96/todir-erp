# 31 — TODIR ERP SaaS Channels, E-commerce & Marketplace Integration Requirements & Technical Design Pack

**File:** `31_ERP_SAAS_CHANNELS_ECOMMERCE_MARKETPLACE_INTEGRATION_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`  
**Product:** **TODIR**  
**Status:** PRODUCT + OMNICHANNEL COMMERCE + CHANNEL CONNECTIONS + LISTINGS + ASSORTMENTS + PRICE/STOCK SYNC + ORDER INGESTION + MARKETPLACE OPERATIONS + SETTLEMENT EVIDENCE + RECONCILIATION + IMPLEMENTATION BASELINE  
**Version:** v1.0  
**Date:** 2026-10-08  
**Audience:** Product Owner, Product Architect, Commerce Operations Lead, E-commerce Manager, Marketplace Manager, Sales Operations Lead, Catalog/Master Data Lead, Inventory Lead, Finance Lead, Payments/Banking Lead, Shipping/Fulfillment Lead, Backend/Frontend Leads, Integration Architect, Security Engineer, Privacy Lead, Performance Engineer, QA, Implementation Agent  
**Risk Class:** **CRITICAL — REVENUE + OVERSelling + ORDER DUPLICATION + CUSTOMER PRIVACY + PRICE/INVENTORY DRIFT + MARKETPLACE SETTLEMENT + EXTERNAL API FAILURE + OPERATIONAL TRUST**

---

## Canonical TODIR dependencies

Primary design sources:

- `01_ERP_SAAS_MASTER_MODULE_AND_DEPENDENCY_BLUEPRINT.md`
- `02_ERP_SAAS_PRODUCTION_READY_TECH_STACK_AND_PROJECT_STRUCTURE.md`
- `03_ERP_SAAS_PLATFORM_KERNEL_AND_MODULE_SYSTEM_TECHNICAL_DESIGN_PACK.md`
- `04_ERP_SAAS_DIRECTORY_PARTY_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `05_ERP_SAAS_CATALOG_PRODUCT_UOM_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `06_ERP_SAAS_FINBASE_CURRENCY_PAYMENT_TERMS_MONEY_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `07_ERP_SAAS_PRICING_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `08_ERP_SAAS_TAX_ENGINE_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `11_ERP_SAAS_INVOICING_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `12_ERP_SAAS_PAYMENTS_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `13_ERP_SAAS_INVENTORY_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `14_ERP_SAAS_SALES_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `16_ERP_SAAS_ACCOUNTING_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `25_ERP_SAAS_BANKING_CASH_MANAGEMENT_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `29_ERP_SAAS_HELPDESK_CUSTOMER_SUPPORT_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `30_ERP_SAAS_SUBSCRIPTIONS_RECURRING_BILLING_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`

### Architecture corrections locked by this pack

The master blueprint initially described:

```text
channels -> sales + catalog
optional -> inventory + payments + invoicing
```

This pack locks the production boundary as:

```text
Hard business dependencies
--------------------------
Sales
Catalog
Pricing
Directory

Optional capability providers
-----------------------------
Inventory
Payments
Invoicing
Shipping
Compliance
Banking / Bank Reconciliation
Accounting
Portal
Studio
```

Reasons:

1. Channels publishes commercial prices and therefore consumes **Pricing** directly; it never creates an independent e-commerce price master.
2. Channels must resolve/create/link customer identity under explicit policies and therefore consumes **Directory** directly; it must never obtain Party identity transitively through Sales.
3. Inventory, Payments, Invoicing and future Shipping remain optional because a channel can operate in partial modes, but when active their domains remain authoritative.

### Locked upstream truths

- Catalog owns canonical ProductTemplate/ProductVariant/UOM/SKU/barcode identity.
- Pricing owns reusable price lists, versions and commercial price-resolution rules.
- Directory owns canonical Party/Customer/Contact identity.
- Sales owns the internal commercial order after external order ingestion succeeds.
- Inventory owns physical on-hand, reserved, available/salable truth, locations, movements and valuation.
- Payments owns payment attempts, gateway references, refunds and money-movement truth.
- Invoicing owns invoices/credit notes/debit notes; marketplace order documents are not automatically legal invoices.
- Tax owns tax calculation; Channels stores external tax evidence but does not become a tax engine.
- Banking/Bank Reconciliation owns actual bank receipt/payout reconciliation.
- Accounting/Ledger owns journal truth and account mapping for marketplace fees/adjustments.
- Shipping will own carrier shipment/label/tracking orchestration; Channels only maps external fulfillment observations and commands.
- Sales already distinguishes an internal `SalesChannel` dimension from the external integration described here.
- Platform owns Outbox/Inbox, Jobs, Notifications, Files, Audit, Secrets/Credential Vault, Integration messaging infrastructure and tenant isolation primitives.
- External writes enter approved application Commands; no connector writes another module database directly.
- Every externally triggered write is idempotent.
- AI follows `AI -> Tool Layer -> Permissions -> Application Commands/Queries -> Domain`; never AI -> Database or raw provider credentials.

---

# 0. Executive Decision

TODIR `31 Channels, E-commerce & Marketplace Integration` is the canonical **commerce-channel orchestration layer** between TODIR business truth and external selling platforms.

It answers:

> **Where do we sell, which TODIR products are offered there, how are they represented remotely, what price and availability should each channel see, which external orders or changes arrived, how do we import them exactly once into Sales, what did the marketplace report about fulfillment/payment/settlement, where has local and remote state drifted, and can operations reconcile every discrepancy without duplicating Product, Price, Stock or Finance truth?**

The module is not “Shopify integration.” It is a provider-neutral commerce hub with provider adapters.

---

# 1. Central Product Question

> **Can a merchant sell through Salla, Zid, Shopify, WooCommerce, Amazon, noon and future storefronts while TODIR remains the single operational ERP truth for Product, Pricing, Stock, Sales and Finance—and while every sync failure is visible, retryable, explainable and safe?**

---

# 2. Product Law — Channel Is A Selling Context, Not A Second ERP

Channels owns publication, mapping, synchronization and external-commerce orchestration. It never becomes a second Catalog, Pricing, Inventory, Sales, Payments, Invoicing, Shipping or Accounting implementation.

---

# 3. Product Law — One Product Identity, Many Listings

```text
Catalog ProductVariant
    -> Salla Listing
    -> Zid Listing
    -> Shopify Variant
    -> Amazon Seller SKU / ASIN relation
    -> noon partner SKU
```

Those are channel representations of one canonical TODIR ProductVariant, not independent ERP products.

---

# 4. Product Law — SalesChannel != ChannelConnection

`SalesChannel` is an internal Sales classification such as Direct, Wholesale, Retail, Ecommerce, Marketplace or POS.

`ChannelConnection` is one concrete external account/store/market connection such as `Amazon Egypt - Seller 1`, `Salla KSA - Brand Store` or `Shopify UAE`.

One ChannelConnection maps to one internal SalesChannel, but the entities have different ownership and lifecycle.

---

# 5. Product Law — Remote Order Is Source Input, Sales Order Is Internal Truth

A remote platform may originate a customer order. After successful ingestion, TODIR Sales owns the internal commercial order state. Later remote changes enter through Sales amendment/cancellation/return commands; they never directly mutate Sales tables.

---

# 6. Product Law — Webhook Is A Signal, Not Complete Truth

Webhooks provide low-latency change signals but can be delayed, duplicated, missing or out of order. Every provider integration must also support checkpoint/poll/reconciliation where the provider APIs allow it.

---

# 7. Product Law — Provider Acceptance != Remote Business Success

A successful HTTP response can mean only “request accepted.” Listing publication, content QC, buyability, stock visibility or order acknowledgment may complete later. TODIR stores desired state separately from observed remote state.

---

# 8. Product Law — Small Merchant First

A small company should be able to:

```text
Connect Store
-> map products
-> choose price list
-> choose warehouse/availability policy
-> enable orders
-> enable stock/price sync
```

without configuring enterprise mapping/routing concepts unless needed.

---

# 9. Product Law — Enterprise Depth Is Progressive

Advanced tenants may enable multiple legal entities, markets, warehouses, marketplace content schemas, location mapping, reconciliation rules, settlement evidence, approval hooks, custom policies and high-volume batch operations without replacing the simple Channel Core.

---

# 10. Product Law — No Generic Bidirectional Sync Switch

There is no unsafe global `TwoWaySync = true`. Authority is decided by data class: Product, Price, Availability, Orders, Customer identity, Fulfillment and Finance each have explicit ownership and direction policies.

---

# 11. Product Law — No Silent Conflict Resolution

When remote and TODIR state disagree in a way that affects an invariant—e.g. remote cancellation after shipment—Channels raises a typed `ChannelConflict`. It never silently overwrites the losing side.

---

# 12. Product Law — External Buyer Is Not Automatically A Party

Marketplace accounts can provide guest, relay, temporary or masked identity. TODIR may create/link a canonical Party only under an explicit identity policy with sufficient trustworthy data. Masked marketplace PII must not pollute Directory.

---

# 13. Product Law — Gross Sale != Marketplace Settlement != Bank Payout

An external order gross total, marketplace settlement net amount and bank deposit are three different facts. Channels may capture settlement evidence; Payments/Banking/Accounting own the corresponding financial truth.

---

# 14. Product Law — Disabling A Channel Never Deletes History

Channel disconnection stops new synchronization according to policy. It never deletes mappings, imported Sales Orders, financial evidence, audit history or external references needed for traceability.

---

# 15. Core Distinction — Catalog Product != Channel Listing

`ProductVariant` is canonical identity. `ChannelListingVariant` describes how that variant exists on one external selling context.

---

# 16. Core Distinction — Assortment != Catalog

Catalog contains what the company sells. `ChannelAssortment` declares which subset is intended for a specific channel/market.

---

# 17. Core Distinction — Desired State != Observed State

Desired state is what TODIR wants remotely. Observed state is what the provider most recently reports. Drift between them is first-class.

---

# 18. Core Distinction — Remote SKU != TODIR SKU

Remote identifiers can differ. Explicit mappings are stored; never overwrite the canonical TODIR SKU to match one marketplace.

---

# 19. Core Distinction — Remote Category != TODIR Category

Provider taxonomies/product types/browse nodes are channel-specific mappings, not Catalog category truth.

---

# 20. Core Distinction — Remote Attribute != Catalog Attribute

Provider field schemas can change independently. Attribute mappings are provider/market/version-aware.

---

# 21. Core Distinction — External Price != Pricing Master

A published channel price is an output/evidence of a TODIR Pricing decision, not a second reusable master price.

---

# 22. Core Distinction — OnHand != Publishable Availability

Inventory owns stock truth. Channels can apply exposure/safety/max-publish policy to compute what should be advertised externally.

---

# 23. Core Distinction — External Location != Inventory Location

Provider warehouse/location IDs are mapped explicitly to TODIR Inventory locations or fulfillment scopes.

---

# 24. Core Distinction — Order Payment Status != TODIR Payment

Remote `paid` is evidence from the commerce platform. Payments decides whether/how that creates a TODIR payment/money movement.

---

# 25. Core Distinction — Marketplace Invoice != TODIR Legal Invoice

A provider-generated receipt/invoice may be evidence. Invoicing/Compliance determines TODIR legal document obligations per country/process.

---

# 26. Core Distinction — Fulfillment Observation != Shipment Truth

Channels can observe `shipped/delivered/FBA` externally; Shipping/Inventory own internal shipment/movement truth.

---

# 27. Core Distinction — Return Request != Inventory Return

Remote return/cancellation is an input to Sales/Shipping/Inventory/Payments workflows, not a stock mutation.

---

# 28. Core Distinction — Marketplace Fee != Product Discount

Commissions, fulfillment fees, storage, penalties and marketplace-funded promotions remain typed financial evidence, not Pricing master data.

---

# 29. Core Distinction — Connector != Provider Core

Provider SDK/API behavior belongs adapter infrastructure. The Channels domain remains provider-neutral.

---

# 30. Core Distinction — Synchronization != Migration

Initial discovery/mapping/go-live is a controlled migration/onboarding workflow; steady-state sync has different guarantees and metrics.

---

# 31. Core Distinction — Channel Health != Provider Status

TODIR channel health combines auth, webhook, API, queue, reconciliation and mapping health; a provider status alone is insufficient.

---

# 32. Core Distinction — Retry != Reprocess Business Action

Transport retry can repeat delivery; inbox/idempotency ensures the business command executes at most once for the same semantic event.

---

# 33. Core Distinction — Storefront Cart != Sales Order

A future TODIR-native cart is an ephemeral commerce object. SalesOrder begins only when commercial order placement succeeds.

---

# 34. Core Distinction — Publication != Fulfillment

Publishing an item for sale and deciding which warehouse/courier fulfills the resulting order are separate responsibilities.

---

# 35. Supported Commerce Contexts

Initial architecture supports:

```text
Owned Web Store
Hosted Store Platform
Marketplace
B2B Commerce Channel
Social/Feed Publication
External Custom Commerce API
```

POS remains Pack 20. Direct/phone/wholesale sales remain Sales.

---

# 36. Initial Provider Priority — Arabic Market

Commercial implementation priority:

```text
P1: Salla, Zid, Shopify, WooCommerce
P2: Amazon EG/SA/UAE, noon
P3: Adobe Commerce/Magento, Custom Commerce API
P4: feed/social/catalog syndication where validated
```

Architecture must tolerate Amazon/noon-level listing complexity from Day 1 even if connectors ship later.

---

# 37. Module Name

Technical module key:

```text
channels
```

Projects:

```text
Todir.Modules.Channels.Domain
Todir.Modules.Channels.Application
Todir.Modules.Channels.Infrastructure
Todir.Channels.Contracts
```

---

# 38. Provider Adapter Projects

Recommended adapter packages when implemented:

```text
Todir.Channels.Providers.Salla
Todir.Channels.Providers.Zid
Todir.Channels.Providers.Shopify
Todir.Channels.Providers.WooCommerce
Todir.Channels.Providers.Amazon
Todir.Channels.Providers.Noon
```

Provider packages depend on stable Channels contracts; Channels Domain never depends on provider packages.

---

# 39. Database Schema

Recommended module schema: `business_channels`.

---

# 40. DbContext

`ChannelsDbContext` owns only `business_channels` tables. No direct DbSet for Catalog, Pricing, Sales, Inventory, Payments, Invoicing, Shipping or Finance tables.

---

# 41. Channel Provider

`ChannelProvider` is a stable provider type/key such as `salla`, `zid`, `shopify`, `woocommerce`, `amazon`, `noon`, `custom`.
Provider registration supplies metadata/capabilities but secrets never live in the domain entity.

---

# 42. Channel Connection

Canonical aggregate root:

```text
ChannelConnection
{
    Id
    TenantId
    ProviderKey
    Name
    LegalEntityId
    BranchId?
    MarketCode?
    DefaultCurrencyCode?
    DefaultSalesChannelId
    CredentialRef
    State
    PolicyVersion
    LastHealthState?
    CreatedAt
    RowVersion
}
```

---

# 43. One Remote Account May Have Multiple Connections

Separate connection records are valid when one provider account exposes materially different marketplaces/stores/markets/legal entities. Do not force all provider markets into one opaque connection if routing or compliance differs.

---

# 44. Connection State Machine

```text
Draft
-> Authorizing
-> Connected
-> Discovering
-> MappingRequired
-> ReadyForGoLive
-> Active
-> Degraded
-> Suspended
-> Disconnected
-> Archived
```

Auth loss may move Active -> Degraded/Suspended without deleting mappings.

---

# 45. Connection Go-Live Contract

A connection cannot become Active until required mappings/policies pass validation:

```text
credentials valid
provider account identity confirmed
legal entity scope valid
sales channel mapping valid
market/currency compatible
order import policy valid
required product mapping threshold satisfied
inventory policy valid if stock push enabled
pricing policy valid if price push enabled
webhook or polling strategy configured
reconciliation enabled
```

---

# 46. Provider Capability Manifest

Every provider adapter exposes a versioned capability manifest. Core behavior branches on capabilities, not provider-name `if` statements.

---

# 47. Provider Capability — CatalogRead

`CatalogRead` indicates: Can discover remote products/listings. Capability absence must have an explicit fallback or disabled feature; the UI never promises unsupported behavior.

---

# 48. Provider Capability — CatalogPublish

`CatalogPublish` indicates: Can create/update remote products/listings. Capability absence must have an explicit fallback or disabled feature; the UI never promises unsupported behavior.

---

# 49. Provider Capability — ContentPublish

`ContentPublish` indicates: Can publish listing-specific content. Capability absence must have an explicit fallback or disabled feature; the UI never promises unsupported behavior.

---

# 50. Provider Capability — BilingualContent

`BilingualContent` indicates: Can manage separate Arabic/English listing content. Capability absence must have an explicit fallback or disabled feature; the UI never promises unsupported behavior.

---

# 51. Provider Capability — CategoryDiscovery

`CategoryDiscovery` indicates: Can discover remote taxonomies/product types. Capability absence must have an explicit fallback or disabled feature; the UI never promises unsupported behavior.

---

# 52. Provider Capability — AttributeSchemaDiscovery

`AttributeSchemaDiscovery` indicates: Can fetch required/allowed attributes. Capability absence must have an explicit fallback or disabled feature; the UI never promises unsupported behavior.

---

# 53. Provider Capability — PricePush

`PricePush` indicates: Can update remote item/offer price. Capability absence must have an explicit fallback or disabled feature; the UI never promises unsupported behavior.

---

# 54. Provider Capability — InventoryPush

`InventoryPush` indicates: Can update remote availability. Capability absence must have an explicit fallback or disabled feature; the UI never promises unsupported behavior.

---

# 55. Provider Capability — MultiLocation

`MultiLocation` indicates: Provider exposes inventory locations/warehouses. Capability absence must have an explicit fallback or disabled feature; the UI never promises unsupported behavior.

---

# 56. Provider Capability — OrderPull

`OrderPull` indicates: Orders can be fetched by checkpoint/date/status. Capability absence must have an explicit fallback or disabled feature; the UI never promises unsupported behavior.

---

# 57. Provider Capability — OrderWebhook

`OrderWebhook` indicates: Provider can notify order changes. Capability absence must have an explicit fallback or disabled feature; the UI never promises unsupported behavior.

---

# 58. Provider Capability — OrderAcknowledge

`OrderAcknowledge` indicates: TODIR can acknowledge/accept an order remotely. Capability absence must have an explicit fallback or disabled feature; the UI never promises unsupported behavior.

---

# 59. Provider Capability — OrderCancelPush

`OrderCancelPush` indicates: TODIR can request remote cancellation. Capability absence must have an explicit fallback or disabled feature; the UI never promises unsupported behavior.

---

# 60. Provider Capability — ReturnPull

`ReturnPull` indicates: Returns can be retrieved/received. Capability absence must have an explicit fallback or disabled feature; the UI never promises unsupported behavior.

---

# 61. Provider Capability — RefundPush

`RefundPush` indicates: Refund can be initiated/acknowledged remotely. Capability absence must have an explicit fallback or disabled feature; the UI never promises unsupported behavior.

---

# 62. Provider Capability — FulfillmentPush

`FulfillmentPush` indicates: Shipment/fulfillment status can be sent remotely. Capability absence must have an explicit fallback or disabled feature; the UI never promises unsupported behavior.

---

# 63. Provider Capability — SettlementPull

`SettlementPull` indicates: Financial settlement/payout evidence can be fetched. Capability absence must have an explicit fallback or disabled feature; the UI never promises unsupported behavior.

---

# 64. Provider Capability — FeeBreakdown

`FeeBreakdown` indicates: Provider exposes typed fee/adjustment components. Capability absence must have an explicit fallback or disabled feature; the UI never promises unsupported behavior.

---

# 65. Provider Capability — WebhookSignature

`WebhookSignature` indicates: Provider supplies signed/verifiable webhook mechanism. Capability absence must have an explicit fallback or disabled feature; the UI never promises unsupported behavior.

---

# 66. Provider Capability — ApiVersioning

`ApiVersioning` indicates: Provider API has explicit version contract. Capability absence must have an explicit fallback or disabled feature; the UI never promises unsupported behavior.

---

# 67. Provider Capability — BulkOperations

`BulkOperations` indicates: Provider supports batch/feed/report operations. Capability absence must have an explicit fallback or disabled feature; the UI never promises unsupported behavior.

---

# 68. Capability Versioning

Capability manifests include adapter version and provider API version/effective date. A provider upgrade that changes semantics is a controlled deployment, not a silent configuration flip.

---

# 69. Provider Contract Certification

Each adapter must have sandbox/contract tests for authentication, pagination, rate limits, retryable errors, idempotency, ordering, mapping and breaking-schema detection where provider facilities permit.

---

# 70. Channel Market

`ChannelMarket` represents provider selling scope such as Amazon marketplace, noon country, Shopify market/store context or a hosted-store market. It can influence currency, content locale, listing schema and fulfillment behavior.

---

# 71. Market vs Legal Entity

One Market does not imply one Legal Entity. ChannelConnection explicitly maps commercial ownership to TODIR LegalEntity; country/provider settings cannot infer accounting ownership silently.

---

# 72. Market Currency

Channel market/order currency is explicit. FinBase validates currency and conversion inputs; Channels never invents exchange rates.

---

# 73. Channel Policy

Versioned `ChannelPolicy` groups operational choices such as:

```text
ProductAuthorityPolicy
ContentAuthorityPolicy
PricePublicationPolicy
AvailabilityPublicationPolicy
OrderImportPolicy
CustomerIdentityPolicy
PaymentEvidencePolicy
FulfillmentPolicy
TaxEvidencePolicy
ReconciliationPolicy
```

---

# 74. Authority Matrix

Default authority:

```text
Product identity        -> Catalog
Canonical content       -> Catalog
Channel content overlay -> Channels
Reusable price          -> Pricing
Inventory truth         -> Inventory
External listing state  -> Remote provider observation
External order source   -> Remote provider
Internal commercial order -> Sales
Canonical customer      -> Directory
Payment movement        -> Payments
Invoice                 -> Invoicing
Shipment/movement       -> Shipping/Inventory
Marketplace settlement  -> external evidence; Finance owns financial truth
```

---

# 75. Authority Policy Must Be Explicit

A connection cannot enable inbound product overwrite, inbound price overwrite or inbound stock overwrite merely because the provider API exposes those fields. Any reverse-import feature needs a deliberate, reviewable policy.

---

# 76. Channel Assortment

`ChannelAssortment` defines which Catalog products/variants are intended for one ChannelConnection/Market. It can be built from explicit variants, categories or controlled rules.

---

# 77. Assortment Is Publishable Intent

Assortment membership means “eligible to publish,” not “successfully live.” Listing readiness, provider validation and observed state remain separate.

---

# 78. Assortment Inclusion Modes

Initial:

```text
Explicit Products/Variants
Catalog Category include/exclude
All sellable products with exclusions
```

Avoid an unrestricted arbitrary expression engine in V1.

---

# 79. Assortment Versioning

Material assortment changes that trigger mass publication use a version/revision with preview and audit so operators can explain why thousands of listings changed.

---

# 80. Assortment Effective Dates

Future start/end dates are optional advanced behavior. If supported, scheduler activation is idempotent and timezone-aware to the LegalEntity/channel policy.

---

# 81. Channel Listing

Core entity:

```text
ChannelListing
{
    Id
    TenantId
    ChannelConnectionId
    MarketCode?
    ProductTemplateId
    ExternalListingId?
    ExternalProductId?
    DesiredPublicationState
    ObservedPublicationState
    ObservedBuyabilityState?
    MappingReadiness
    IssueCount
    LastPublishedAt?
    LastObservedAt?
    ProviderRevision?
    RowVersion
}
```

---

# 82. Channel Listing Variant

```text
ChannelListingVariant
{
    Id
    ChannelListingId
    ProductVariantId
    ExternalVariantId?
    ExternalSellerSku?
    ExternalOfferId?
    DesiredState
    ObservedState
    LastObservedHash?
    RowVersion
}
```

---

# 83. Listing Lifecycle

```text
Unmapped
-> Mapped
-> Ready
-> Publishing
-> Published
```

Exception states can include `ActionRequired`, `Rejected`, `Suppressed`, `Suspended`, `Paused`, `Delisted`, `Archived` while raw provider status is retained separately.

---

# 84. Desired State

TODIR desired state is controlled by approved assortment/listing commands. It never gets overwritten solely because a stale remote response arrived.

---

# 85. Observed Remote State

Observed state records the last verified remote condition with provider timestamp/revision/evidence. It is evidence, not authority for Catalog/Pricing/Inventory master data.

---

# 86. Listing Issue

`ChannelListingIssue` stores typed provider validation/QC/buyability issues with provider code, severity, field/attribute reference, observed time, raw evidence reference and resolved state.

---

# 87. Listing Issue Lifecycle

Open -> Acknowledged -> Resolved/Obsolete. If provider reports the issue gone during reconciliation, close it with evidence; never delete issue history.

---

# 88. Listing Suppression

A remotely suppressed listing remains mapped. TODIR surfaces the reason and required action; it does not create a new listing identity to bypass provider state.

---

# 89. Listing Delist

Delisting/removing from assortment does not delete Catalog Product or historical Sales Orders. Remote delete/unpublish behavior follows provider capability/policy.

---

# 90. External Reference Map

Channels owns provider-specific references:

```text
(tenant_id, connection_id, object_type, external_id) -> internal ref
```

Use unique constraints appropriate to provider identity scope.

---

# 91. Product Mapping

Primary mapping is `ExternalVariantId/ExternalSellerSku -> ProductVariantId`. Template/listing-level mappings are separate from variant/offer mappings.

---

# 92. Product Matching Order

Recommended onboarding matching:

```text
Existing channel external ref
-> exact SKU
-> GTIN/barcode where semantics allow
-> approved legacy mapping
-> candidate by name/brand/attributes
-> manual review
```

Never fuzzy-overwrite an existing mapping automatically.

---

# 93. Mapping Confidence

Candidate matching may calculate confidence and reasons, but only deterministic/explicit rules auto-link. Ambiguous candidates remain review tasks.

---

# 94. Mapping Immutability After Orders

Changing a variant mapping after imported orders exist is sensitive. Require permission, impact preview and audit; historical orders retain original external snapshots/references.

---

# 95. Category Mapping

`ChannelCategoryMapping` maps TODIR Catalog category/context to remote category/product type/browse node. It does not copy provider taxonomy into Catalog as canonical categories.

---

# 96. Category Mapping Scope

Mappings are scoped by provider, connection/market and schema version where provider semantics differ. One TODIR category may map differently across Amazon EG, Amazon SA, noon and Salla.

---

# 97. Attribute Mapping

`ChannelAttributeMapping` maps canonical Catalog attributes/values or derived values to provider-specific fields/enums. Mapping can include transformation only through constrained typed mapping operations—not arbitrary code/SQL.

---

# 98. Attribute Mapping Version

Store provider schema/product-type version and mapping version so a later provider definition change does not silently reinterpret old configuration.

---

# 99. Provider Schema Discovery

Where supported, adapters discover category/product-type requirements and cache versioned metadata. The domain stores references/version/evidence, not provider-specific JSON as canonical business model.

---

# 100. Required Attribute Readiness

Before publication, listing readiness checks required remote attributes, values, media, identifiers, locale completeness and policy constraints. Missing requirements block publish with actionable messages.

---

# 101. Remote Enum Mapping

Provider enum values are mapped explicitly. Unknown new enum values produce `UnsupportedProviderValue`/degraded mapping instead of defaulting to a random internal value.

---

# 102. Location Mapping

`ChannelLocationMapping` maps provider warehouse/location/fulfillment identifiers to TODIR Inventory location or an external-fulfillment scope. Mapping is explicit and auditable.

---

# 103. Payment Method Mapping

External payment method/status codes map to normalized evidence categories. They do not create TODIR payment records without Payments policy/application command.

---

# 104. Shipping Method Mapping

External shipping/fulfillment methods map to future Shipping service levels/provider references or Sales fulfillment policy. Channels does not own carrier master truth.

---

# 105. Canonical Product Content

Catalog owns canonical identity, names/descriptions/media references/translations defined by Catalog policy. Channels consumes them and may add channel-specific overlays.

---

# 106. Channel Content Overlay

Channels may own listing-specific fields such as:

```text
Title override AR/EN
Description override AR/EN
Bullet points
Marketplace keywords
Channel-specific media selection/order
Remote SEO/metadata where provider requires it
```

This never overwrites Catalog canonical content automatically.

---

# 107. Arabic + English First-Class

For Arabic markets, listing content tracks `ar` and `en` separately with completeness and provider/QC state. UI must support RTL content editing and market-specific required language rules.

---

# 108. Content Fallback

If channel override is absent, renderer may fall back to canonical Catalog translation according to explicit locale policy. Fallback source is shown in preview.

---

# 109. Content Snapshot On Publish

Publication attempts record the content/version/hash sent so operations can compare what TODIR sent versus what the provider later shows.

---

# 110. Media Selection

Catalog/Platform Files own canonical media. Channels stores channel selection/order/cropping metadata only where needed. No binary provider images are stored in PostgreSQL.

---

# 111. Media Upload Workflow

Heavy image upload/processing is asynchronous with per-asset status, retry and provider issue feedback. Listing publish can be partial/pending depending on provider semantics.

---

# 112. Content QC

Provider QC approval/rejection is observed state. TODIR preserves provider reason/code and language/field scope so the operator can fix the correct content.

---

# 113. Content Drift

If a merchant edits remote content directly and TODIR is authoritative, reconciliation flags drift. Policy decides whether to re-publish automatically or require review.

---

# 114. Price Publication Source

Every outbound reusable selling price comes from Pricing resolution/export context. No `channel_listing.master_price` source of truth is created.

---

# 115. Channel Price Context

ChannelConnection/Market can select:

```text
PriceList
PriceGroup/context
Currency
PricingDate policy
Customer/segment context where supported
```

---

# 116. Channel Price Snapshot

Store last desired/sent/observed price evidence with Pricing list/version/rule evidence where material; do not copy all Pricing rule data into Channels.

---

# 117. Price Publication Lifecycle

Pending -> Calculated -> Queued -> Sent -> Observed/Confirmed, with Failed/ActionRequired/Drift states. Provider acceptance is not automatically Confirmed.

---

# 118. Price Rounding

Pricing/FinBase produce correct price/precision. Provider adapter validates provider precision/minimum constraints. Channels must never silently round with ad-hoc decimal logic.

---

# 119. Price Currency Mismatch

If selected price list currency is incompatible with channel market requirements and no approved conversion policy exists, activation/publication blocks rather than guessing FX.

---

# 120. Promotional Price Evidence

External provider may have marketplace campaigns/discounts. Channels records externally observed price/promotional evidence but does not mutate Pricing master unless a deliberate import workflow is approved.

---

# 121. Marketplace Fees Are Not Price

Referral, fulfillment, storage, payment, advertising, penalty and settlement adjustments are financial evidence separate from item selling price and discount calculation.

---

# 122. Price Drift

Reconciliation compares desired published price to provider-observed offer price when retrievable. Difference beyond policy tolerance creates `PriceDrift` issue.

---

# 123. Bulk Price Push

Large price publication uses chunked/bulk provider operations when available, with preview, rate-limit aware scheduling, per-item results and resumability.

---

# 124. Inventory Authority

Inventory remains the only owner of on-hand/reserved/available stock. Channels never adjusts inventory quantity because an external platform says a number changed unless an explicit inbound reconciliation workflow requests Inventory action.

---

# 125. Publishable Availability

Conceptually:

```text
Inventory availability from selected scope
- channel safety buffer
- policy reserve/holdback
capped by maximum publish quantity if configured
= desired external availability
```

Exact formula is typed/versioned policy and never changes Inventory truth.

---

# 126. Availability Policy

`ChannelAvailabilityPolicy` can define:

```text
InventoryScope
SafetyBuffer
MaximumPublishedQuantity?
OutOfStockBehavior
BackorderExposurePolicy
AllocationStrategy
StaleDataFailSafe
```

---

# 127. Safety Buffer

A channel can hold back units to reduce overselling risk. Buffer is a publication policy, not an Inventory reservation unless an explicit reservation command exists.

---

# 128. Maximum Published Quantity

Useful where merchant does not want full ERP availability exposed to a marketplace. Cap is not stock; when inventory falls below cap, desired quantity follows real availability.

---

# 129. Multi-Location

If provider supports locations, publish per mapped location only under an explicit mapping/policy. Otherwise aggregate the approved TODIR inventory scope deterministically.

---

# 130. Availability Change Feed

Prefer Inventory integration events/read model for changed variants and batch availability query; do not full-scan millions of SKUs on every small stock movement.

---

# 131. Availability Debounce/Coalescing

High-frequency stock movements may coalesce outbound updates per variant/connection for a short safe window. Coalescing must never skip a materially newer state.

---

# 132. Overselling Guard

When inventory freshness is uncertain beyond configured threshold, policy may publish zero, freeze last safe value or stop sync; default for high-risk channels should fail safe rather than advertise arbitrary stale stock.

---

# 133. Backorder Policy

Channels can expose backorder capability only if both Inventory/Sales policy and provider behavior support it. External backorder state never overrides internal negative-stock policy.

---

# 134. Inventory Drift

When provider observed stock differs from TODIR desired stock, raise drift and attempt safe correction. If repeated drift suggests another system is authoritative, surface configuration conflict.

---

# 135. Fulfillment Model

Normalize at least:

```text
SellerFulfilled
MarketplaceFulfilled
ThirdPartyFulfilled
StoreFulfilled
Pickup
Unknown
```

---

# 136. Marketplace-Fulfilled Inventory

FBA/noon-fulfilled/etc. stock may be external inventory evidence. Do not merge it blindly with TODIR on-hand; model it as mapped external fulfillment/inventory scope until Inventory pack defines canonical external-stock ownership behavior.

---

# 137. Fulfillment Handoff

External order normalized by Channels includes fulfillment intent. Sales/Inventory/future Shipping decide reservation/source/shipment based on approved contracts.

---

# 138. External Fulfillment Observation

`ExternalFulfillmentObservation` records provider status/tracking/package evidence without becoming the shipment aggregate.

---

# 139. Tracking Number

Remote tracking number is evidence. Future Shipping owns canonical Shipment/TrackingRef when imported/linked.

---

# 140. Partial Fulfillment

Provider order/item/package fulfillment can be partial. Never collapse multiple remote fulfillment units into one boolean `shipped` flag.

---

# 141. Pickup

Pickup orders preserve pickup location/time/customer instructions as order/fulfillment snapshot. Store pickup execution belongs future Shipping/Inventory/POS service boundaries as appropriate.

---

# 142. Order Ingestion Pipeline

```text
Webhook / Poll / Report
-> Authenticate/Fetch
-> ProviderEvent Inbox
-> Normalize
-> ExternalOrderEnvelope
-> Validate Mapping/Policy
-> Identity Resolution
-> Sales.CreateExternalOrder
-> Persist Link + Result
-> Acknowledge provider if required
```

---

# 143. External Order Envelope

Evidence boundary:

```text
ExternalOrderEnvelope
{
    ChannelConnectionId
    ExternalOrderId
    ExternalRevision?
    ProviderOccurredAt?
    ReceivedAt
    CurrencyCode
    BuyerSnapshot
    ShippingSnapshot
    LineSnapshots[]
    DiscountSnapshots[]
    TaxSnapshots[]
    PaymentEvidence
    FulfillmentEvidence
    RawPayloadRef?
    NormalizerVersion
}
```

---

# 144. Raw Payload Evidence

Raw provider payload can be retained only under privacy/retention policy using Platform Files/object storage/evidence storage. Never log full sensitive payloads by default or treat raw JSON as domain schema.

---

# 145. Order Normalization

Provider adapter maps remote fields/statuses to a versioned normalized envelope. Unknown/unsupported values remain explicit rather than silently coerced.

---

# 146. Order Idempotency Key

Unique semantic key should include at least:

```text
TenantId + ChannelConnectionId + ExternalOrderId
```

A provider-specific revision/event key can separately deduplicate changes.

---

# 147. Duplicate Order Rule

The same external order key must never create a second internal SalesOrder. Repeat ingestion returns existing link/result or processes a newer allowed revision.

---

# 148. Order External Link

`ChannelOrderLink` maps external order to SalesOrder with import status, source revision, timestamps and trace/correlation identifiers.

---

# 149. Order Import Failure

Mapping/customer/currency/product/tax/fulfillment failures create an actionable import exception. The order remains in an operations queue and can be retried after correction without creating duplicates.

---

# 150. Missing Product Mapping

Do not auto-create Product during live order ingestion unless an explicitly approved emergency workflow exists. Default: hold order import, surface missing mapping, then retry.

---

# 151. Unknown Remote SKU

Capture external line snapshot and issue. Operator can map to existing ProductVariant; mapping resolution retries the same order idempotently.

---

# 152. Deleted/Archived Catalog Product

Historical remote order may refer to archived Catalog item. Import can use historical mapping/snapshot if Sales policy permits; it must not silently reactivate master data.

---

# 153. Order Currency Validation

FinBase/Sales validates currency. If provider gives conversion rate, preserve evidence; never substitute provider rate as canonical FX for unrelated accounting calculations.

---

# 154. Order Total Reconciliation

Before import, normalized lines/discount/tax/shipping totals reconcile to provider order total within configured precision/tolerance. Mismatch is explicit.

---

# 155. External Discounts

Normalize seller-funded, marketplace-funded, coupon, shipping and other discount evidence separately when provider exposes them. Sales/Invoicing decide internal representation.

---

# 156. External Tax Evidence

Preserve tax-inclusive/exclusive totals, provider tax lines/codes if available. Tax/Invoicing/Compliance determine authoritative legal calculation/document behavior.

---

# 157. External Shipping Charge

Shipping charge is part of commercial order snapshot but carrier cost is separate. Do not conflate customer shipping revenue with provider/courier expense.

---

# 158. Gift/Marketplace Credits

Marketplace credits/vouchers/subsidies are typed evidence. Their accounting and customer price impact must not be guessed by Channels.

---

# 159. Order Time Semantics

Store provider created/updated timestamps with source timezone/offset where supplied and `ReceivedAtUtc`. Never compare naive local timestamps across providers.

---

# 160. Backfill Orders

Initial connection can import open/recent historical orders according to cutover policy. Cutover boundary is explicit to prevent duplicate processing with legacy systems.

---

# 161. Order Acknowledgment

If provider requires acknowledgement/acceptance, send only after configured internal readiness point. Retry acknowledgement idempotently; failure does not create another SalesOrder.

---

# 162. Buyer Identity Policy

Supported strategies:

```text
LinkVerifiedParty
MatchOrCreateForOwnedStore
GuestPartyWithSnapshot
MarketplaceSharedPartyWithSnapshot
ManualReview
```

---

# 163. Owned Store Customer

For Salla/Zid/Shopify/WooCommerce first-party store customers, policy may search Directory by stable external mapping and trustworthy identifiers, then link or create through Directory commands.

---

# 164. Guest Checkout

Guest checkout does not force permanent Party creation. Use configured guest policy and immutable order buyer snapshot; create Party later only if business process justifies it.

---

# 165. Marketplace Masked Buyer

Masked/relay identity must not be normalized into canonical phone/email or used for duplicate matching. Default to marketplace guest/shared Party plus immutable BuyerSnapshot.

---

# 166. External Customer Reference

`ExternalBuyerRef` is scoped to provider/connection and can link to Party only when stable and authorized. It is not globally unique identity.

---

# 167. Buyer Snapshot

Sales/Channels preserve only data needed for order execution/legal history according to privacy policy:

```text
DisplayName?
Email/relay?
Phone?
ShipTo snapshot
BillTo snapshot?
ExternalBuyerRef?
Masking/quality flags
```

---

# 168. Identity Quality Flag

Normalized buyer data records quality such as Verified, ProviderAccount, Guest, Masked, Relay, Unknown. Directory match logic must respect it.

---

# 169. PII Minimization

Do not persist every provider payload field. Keep only operational/legal data required by the workflow and configured retention policy.

---

# 170. External Order Change

Later remote changes are normalized into `ExternalOrderChange` with external revision/event ID, type, occurred time and payload/evidence reference.

---

# 171. Change Types

At least:

```text
CancellationRequested
QuantityChanged
AddressChanged
LineAdded/Removed if provider supports
PaymentStatusChanged
FulfillmentChanged
ReturnRequested
RefundObserved
OtherProviderChange
```

---

# 172. Sales Amendment Handoff

Commercial changes call Sales application contracts. Channels never updates `sales_order` directly.

---

# 173. Cancellation Before Fulfillment

If Sales policy allows, external cancellation can invoke Sales cancel command and then downstream release/correction workflows. Outcome is linked back to external event.

---

# 174. Cancellation After Fulfillment Started

Create ChannelConflict/exception; never silently cancel completed/reserved/shipped internal work. Operator or configured workflow resolves safely.

---

# 175. Address Change

If provider allows address changes, Sales/Shipping policy decides whether change is accepted based on fulfillment state. Historical snapshots remain preserved.

---

# 176. Stale Change

Provider revision/timestamp/sequence can indicate event older than already observed state. Stale event is recorded/deduplicated but does not regress business state.

---

# 177. Out-of-Order Event

Consumers cannot assume transport order. Provider-specific comparator/revision strategy decides if event is new, duplicate, stale or conflicting.

---

# 178. Channel Conflict

```text
ChannelConflict
{
    Type
    ChannelConnectionId
    ExternalObjectRef
    InternalRef?
    DetectedAt
    LocalStateSnapshot
    RemoteStateSnapshot
    Severity
    ResolutionState
    ResolvedBy?
    ResolutionReason?
}
```

---

# 179. Conflict States

Open -> Investigating -> Resolved/AcceptedRemote/KeptLocal/Compensated/Obsolete. Resolution never erases original evidence.

---

# 180. Automatic Conflict Resolution

Only safe, explicitly modeled cases can auto-resolve. Financial, fulfillment or identity conflicts default to review/compensating workflow.

---

# 181. Return Intake

Remote return requests/events normalize into a return handoff. Sales owns return authorization/commercial decision; Inventory owns physical return; Invoicing owns credit documents; Payments owns refund.

---

# 182. Return Item Mapping

Return lines must link via external order-line reference to imported SalesOrderLine/ProductVariant. Unknown line creates exception, never arbitrary SKU matching.

---

# 183. Return Reason Mapping

Remote reasons map to typed TODIR reason categories with raw provider reason preserved. Provider-specific reason list does not become global Sales truth.

---

# 184. Refund Observation

Provider-reported refund is evidence. Payments/Subledger/Invoicing workflows determine actual internal refund/credit/open-item effects.

---

# 185. Marketplace-Funded Refund/Adjustment

If marketplace absorbs/refunds an amount itself, model financial evidence separately from seller refund obligation; do not assume merchant cash left.

---

# 186. Partial Return

Quantities are explicit per line/package; no boolean returned flag for multi-quantity orders.

---

# 187. Payment Evidence Categories

Normalize evidence such as:

```text
PrepaidByCustomer
MarketplaceCollected
CashOnDelivery
BankTransferExternal
PayLater
Unpaid/Pending
Unknown
```

Payments owns canonical movement.

---

# 188. Marketplace-Collected Money

Marketplace-collected amount is not automatically a TODIR bank receipt. Payment/settlement flow waits for provider financial evidence and actual payout reconciliation according to finance design.

---

# 189. COD Distinction

```text
Order Delivered
!= Cash Collected
!= Courier/Marketplace Remitted
!= Bank Deposit Received
```

Track observations separately; Payments/Banking own final money movement/reconciliation.

---

# 190. Marketplace Settlement Evidence

`MarketplaceSettlementEvidence` captures provider statement/payout identifiers, period, gross amounts, fee/adjustment components, currency, net amount and source document/evidence reference.

---

# 191. Marketplace Fee Evidence

Typed examples:

```text
ReferralCommission
FulfillmentFee
StorageFee
ShippingFee
PaymentFee
AdvertisingFee
Penalty
RefundAdjustment
MarketplaceSubsidy
TaxOnFee
Other
```

Accounting mapping is external to Channels.

---

# 192. Payout Link

Channels can link external settlement/payout ID to Payments/Banking reconciliation reference after Finance processes it. Link is an opaque cross-module ref, not a foreign key into bank tables.

---

# 193. Settlement Reconciliation

Compare provider statement components to imported orders/refunds/fees and later bank payout. Unmatched components are exception cases; do not force-balance by “other fee” automatically.

---

# 194. Settlement Currency

Preserve source currency and provider conversion evidence. Finance determines accounting FX treatment; Channels never invents base-currency journal amounts.

---

# 195. Settlement Cutoff

Provider settlement periods/cutoffs are external evidence. Bank posting date and accounting period remain Finance decisions.

---

# 196. Invoice Boundary

Channels may import external invoice/receipt identifiers/evidence but cannot mark TODIR Invoicing document as issued/posted by direct write. Use Invoicing/Compliance contracts.

---

# 197. Country Compliance

Country-specific rules deciding whether a marketplace document satisfies/affects legal invoice obligation belong Compliance/Country Provider. Channels carries source facts only.

---

# 198. Shipping Boundary

Pack 32 will own carrier selection, rate shopping, labels, shipment lifecycle, delivery tracking, COD remittance relationships and logistics integrations. Pack 31 owns provider-facing commerce/fulfillment mappings only.

---

# 199. Shipping Address Snapshot

External order preserves exact ship-to snapshot used for fulfillment. Later Directory address edits do not rewrite historical order/shipment address.

---

# 200. Provider Shipping Status

Map provider status into normalized observation; do not conflate platform status with courier truth if they differ.

---

# 201. Sync Direction Types

```text
OutboundPublication
InboundObservation
InboundBusinessEvent
BidirectionalReferenceReconciliation
```

Each operation has explicit authority and retry semantics.

---

# 202. Channel Sync Checkpoint

```text
ChannelSyncCheckpoint
{
    ConnectionId
    StreamType
    Cursor/Watermark
    LastObservedAt
    LastSuccessfulRunAt
    ProviderVersion?
    RowVersion
}
```

---

# 203. Checkpoint Atomicity

Advance checkpoint only after all records in the committed unit are safely processed/persisted. Crash before commit repeats safely through idempotency.

---

# 204. Channel Sync Run

Every batch/reconciliation run records start/end, provider, connection, stream, request counts, successes, failures, throttling, cursor and correlation ID.

---

# 205. Sync Item Result

Large operations store per-item or chunk result/evidence enough to retry only failures without replaying successful millions of items unnecessarily.

---

# 206. Webhook Inbox

Persist verified webhook/event receipt with provider event ID/hash/type/timestamps before heavy business processing. ACK provider quickly after durable receipt where protocol allows.

---

# 207. Webhook Deduplication

Unique provider event ID when reliable; otherwise provider-specific semantic key/hash. Duplicate event returns success without repeating business command.

---

# 208. Webhook Signature Verification

Provider adapter verifies signature/secret/token/timestamp/replay window according to provider protocol before event becomes trusted input.

---

# 209. Webhook Replay Protection

Reject/flag expired timestamps, invalid signatures and known event IDs. Audit suspicious failures without logging secrets/body PII unsafely.

---

# 210. Webhook Routing

Endpoint resolves tenant/connection using trusted provider routing data; user-supplied tenant IDs in body are never blindly trusted.

---

# 211. Polling

Use provider incremental endpoints/cursors/date windows when webhooks are unavailable/incomplete. Poller is checkpointed, throttled and idempotent.

---

# 212. Reconciliation Worker

Periodic reconciliation compares remote and TODIR desired/linked state for orders, listings, price, stock, fulfillment and settlements according to enabled capabilities.

---

# 213. Reconciliation Does Not Rewrite History

Reconciliation identifies/repairs drift through normal commands. It never runs direct UPDATEs across business schemas.

---

# 214. Anti-Echo Loop

Track outbound mutation correlation, observed provider revision/hash and last desired state to prevent TODIR push -> provider webhook -> identical push loops.

---

# 215. Provider Event Ordering Strategy

Adapter declares ordering support: sequence/revision/time/no-guarantee. Core never assumes universal monotonically increasing IDs.

---

# 216. Retry Classification

Errors classify at least:

```text
TransientNetwork
RateLimited
ProviderUnavailable
AuthenticationExpired
ValidationRejected
MappingRequired
ConcurrencyConflict
UnknownOutcome
PermanentUnsupported
```

---

# 217. Unknown Outcome

Timeout after outbound request can leave remote mutation uncertain. Do not blindly retry non-idempotent provider mutation; query/reconcile first or use provider idempotency key when supported.

---

# 218. Dead Letter / Recovery

Repeated poison/unsupported events go to controlled recovery queue with tenant/connection/type/error/evidence. Operators can correct mapping/config and replay safely.

---

# 219. Rate Limit Budget

Throttle per provider/account/connection/operation. Respect provider headers/backoff where available; one noisy tenant must not starve all channels.

---

# 220. Adaptive Throttling

Workers reduce concurrency on rate-limit/provider pressure and recover gradually. Business queues remain visible with delay metrics.

---

# 221. Provider Outage

Local TODIR business continues where safe. Outbound publications queue; inbound order visibility may degrade. Health dashboard clearly communicates freshness and recovery status.

---

# 222. Credential Ownership

ChannelConnection stores only `CredentialRef`. Actual access/refresh tokens, API keys, client secrets and webhook secrets live in approved encrypted Platform Credential Vault.

---

# 223. Credential Rotation

Adapters support refresh/rotation without changing ChannelConnection identity/mappings. Failed rotation moves connection health to actionable degraded state.

---

# 224. Credential Least Privilege

Request only scopes needed by enabled capabilities. Read-only connector mode must not request write scopes unnecessarily.

---

# 225. Provider Account Verification

At authorization, record verified remote merchant/account/store identity from provider API—not user-entered label only—to prevent connecting wrong production account.

---

# 226. Secret Logging Rule

Tokens, authorization headers, webhook secrets and raw credentials never enter normal logs, traces, audit payloads or AI context.

---

# 227. PII Logging Rule

Buyer names/emails/phones/addresses and raw order payloads are excluded/masked in operational logs and metrics. Debug evidence requires privileged controlled access.

---

# 228. Data Retention

Provider raw payloads/events have explicit retention policy independent of durable business snapshots. Do not retain unlimited third-party PII “just in case.”

---

# 229. Tenant Isolation

All Channels tables include tenant boundary, RLS/FORCE RLS per platform standards, tenant-scoped unique keys, cache/job/object-storage namespacing and cross-tenant security tests.

---

# 230. Legal Entity Isolation

Connection and imported order routing enforce approved LegalEntity scope; a provider account cannot route an order to another entity through client-supplied IDs.

---

# 231. Core Tables

Recommended initial tables:

```text
channel_provider_registration
channel_connection
channel_market
channel_policy
channel_provider_capability

channel_assortment
channel_assortment_version
channel_assortment_member

channel_listing
channel_listing_variant
channel_listing_content_overlay
channel_listing_issue

channel_category_mapping
channel_attribute_mapping
channel_value_mapping
channel_location_mapping
channel_shipping_mapping
channel_payment_mapping
channel_external_reference

channel_sync_checkpoint
channel_sync_run
channel_sync_item_result
channel_provider_event_inbox
channel_outbound_operation

external_order_envelope
external_order_line_snapshot
external_order_change
channel_order_link
external_buyer_snapshot
external_fulfillment_observation

marketplace_settlement_evidence
marketplace_fee_evidence

channel_conflict
channel_reconciliation_run
```

---

# 232. No Giant Generic Mapping Table

Use typed mappings for high-risk/hot concepts such as product variant, category, attribute, location and order references. A generic external-ref table can supplement but not replace all typed constraints.

---

# 233. channel_connection — Keys

Unique remote account/store identity per tenant/provider where provider semantics allow. Multiple market rows can hang off one connection if account scope is shared safely.

---

# 234. channel_listing — Uniqueness

Prevent one external listing identity from silently mapping to multiple canonical ProductTemplates in the same connection/market. Provider-specific identity scopes are documented.

---

# 235. channel_listing_variant — Uniqueness

Prevent one external seller SKU/variant identity from mapping to multiple ProductVariants within the same remote scope unless provider explicitly permits aliasing and design models it.

---

# 236. channel_order_link — Uniqueness

Unique `(tenant_id, channel_connection_id, external_order_id)`; SalesOrder link becomes stable after successful import.

---

# 237. provider_event_inbox — Uniqueness

Unique trustworthy provider event ID per connection, or fallback deterministic hash/key strategy. Store processing state and attempt metadata.

---

# 238. sync_checkpoint — Uniqueness

One active checkpoint per `(tenant, connection, stream_type, market?)` with optimistic concurrency.

---

# 239. RLS

Enable and force RLS on tenant-owned Channels tables; runtime application role is not table owner and cannot bypass policy casually.

---

# 240. Indexes — Orders

Indexes on connection + external_order_id, import_state + received_at, provider_event processing state, SalesOrderRef, and exception queues. Avoid JSON scans for hot routing keys.

---

# 241. Indexes — Listings

Indexes on connection/market + desired/observed state, ProductVariantId, external SKU/listing ID, issue count/state, last observed/published timestamps.

---

# 242. Indexes — Sync

Indexes for due retries, connection health, stream/checkpoint, reconciliation due time and provider-specific operation state.

---

# 243. JSONB Usage

Use JSONB only for provider-specific non-canonical evidence/metadata where typed columns are impractical. Hot/invariant/query fields remain typed and indexed.

---

# 244. Raw Payload Storage

Large raw provider JSON belongs object/file evidence storage with encrypted access and retention; database stores hash/ref/metadata rather than unbounded blobs when practical.

---

# 245. Optimistic Concurrency

Configuration/mapping/listing desired state use row version/concurrency token. Sensitive mapping changes fail stale update with 409/412-style semantics.

---

# 246. Internal Contract Rule

Inside Modular Monolith, Channels calls Sales/Catalog/Pricing/Directory/Inventory capabilities in-process through contracts where immediate answer/invariant is required. Do not make internal HTTP calls by default.

---

# 247. External Provider IO Rule

Provider network IO belongs infrastructure/jobs and is never held inside a multi-module database transaction.

---

# 248. API Surface

Base admin/application route:

```text
/api/v1/channels
```

Exact endpoints stay use-case oriented rather than generic table CRUD.

---

# 249. Connection APIs

Examples:

```text
POST /connections
POST /connections/{id}/authorize
POST /connections/{id}/validate
POST /connections/{id}/discover
POST /connections/{id}/go-live
POST /connections/{id}/suspend
POST /connections/{id}/disconnect
GET  /connections/{id}/health
```

---

# 250. Mapping APIs

```text
GET/POST /connections/{id}/product-mappings
POST     /connections/{id}/mapping-discovery
POST     /connections/{id}/mapping-preview
POST     /connections/{id}/mapping-approve
GET/POST /connections/{id}/category-mappings
GET/POST /connections/{id}/attribute-mappings
GET/POST /connections/{id}/location-mappings
```

---

# 251. Listing APIs

```text
GET  /listings
GET  /listings/{id}
POST /listings/{id}/validate
POST /listings/{id}/publish
POST /listings/{id}/unpublish
POST /listings/bulk-preview
POST /listings/bulk-publish
```

---

# 252. Order APIs

```text
GET  /orders/inbox
GET  /orders/exceptions
POST /orders/{externalRef}/retry-import
POST /orders/{externalRef}/resolve-mapping
GET  /orders/{externalRef}/trace
```

---

# 253. Reconciliation APIs

```text
POST /connections/{id}/reconcile
GET  /connections/{id}/reconciliation-runs
GET  /connections/{id}/drift
POST /conflicts/{id}/resolve
```

---

# 254. Settlement Evidence APIs

Finance-facing read/use-case endpoints expose normalized settlement evidence and reconciliation state, never raw provider credentials.

---

# 255. Commands

Application commands include:

```text
CreateChannelConnection
AuthorizeChannelConnection
ValidateChannelConnection
DiscoverRemoteCommerceState
SetChannelPolicy
ActivateChannelConnection
SuspendChannelConnection
DisconnectChannelConnection
CreateChannelAssortment
CreateAssortmentVersion
PreviewAssortment
ActivateAssortmentVersion
LinkExternalVariant
UnlinkExternalVariant
MapChannelCategory
MapChannelAttribute
MapChannelValue
MapChannelLocation
CreateListing
ValidateListing
PublishListing
PauseListing
UnpublishListing
ResolveListingIssue
QueuePricePublication
QueueAvailabilityPublication
ProcessProviderEvent
ImportExternalOrder
RetryExternalOrderImport
ProcessExternalOrderChange
RecordExternalFulfillmentObservation
RecordMarketplaceSettlementEvidence
OpenChannelConflict
ResolveChannelConflict
RunChannelReconciliation
```

---

# 256. Queries

Queries include:

```text
ListChannelConnections
GetChannelConnection
GetChannelHealth
ListProviderCapabilities
GetMappingCoverage
FindUnmappedRemoteProducts
ListChannelListings
GetListing
GetListingIssues
PreviewListingPublication
GetPriceDrift
GetAvailabilityDrift
ListOrderInbox
GetExternalOrderTrace
ListOrderExceptions
GetChannelConflict
ListChannelConflicts
GetSyncRun
GetReconciliationReport
ListMarketplaceSettlements
GetChannelOperationalDashboard
```

---

# 257. Catalog Contracts Consumed

Consume stable Product/Variant/UOM/translation/media snapshots and batch lookup/matching contracts. No Catalog DbContext access.

---

# 258. Pricing Contracts Consumed

Consume batch price resolution/export/version evidence appropriate to connection/market. Batch is mandatory for high-volume publication.

---

# 259. Directory Contracts Consumed

Consume Party search/snapshot and controlled create/link commands for approved customer identity policy. Never create Directory rows directly.

---

# 260. Sales Contracts Consumed

Need idempotent `CreateExternalOrder`, amendment/cancellation/return handoff and order snapshot/status queries. Sales returns stable refs/outcomes.

---

# 261. Inventory Contracts Consumed

Consume batch availability by mapped scope/location and events/change feed. If future reservation is required, call explicit Inventory command; never decrement stock here.

---

# 262. Payments Contracts Consumed

Record/resolve payment/refund evidence through Payments application contracts when enabled. Channels never posts money movement directly.

---

# 263. Invoicing Contracts Consumed

Request/link invoice/credit document workflows only through approved Invoicing contracts when channel process requires it.

---

# 264. Shipping Contracts — Future

Pack 32 should expose shipment creation/status/tracking/label contracts. Channels integrates through those contracts and retains provider fulfillment refs.

---

# 265. Domain Events

Meaningful domain events may include:

```text
ChannelConnectionActivated
ChannelConnectionDegraded
ChannelAssortmentActivated
ChannelListingDesiredStateChanged
ChannelListingIssueOpened
ExternalOrderReceived
ExternalOrderImported
ExternalOrderImportFailed
ChannelConflictOpened
ChannelConflictResolved
MarketplaceSettlementEvidenceRecorded
```

---

# 266. Integration Events

Potential versioned events:

```text
channels.connection-activated.v1
channels.listing-published.v1
channels.order-imported.v1
channels.order-import-failed.v1
channels.conflict-opened.v1
channels.settlement-evidence-recorded.v1
```

Do not emit one broad event for every transient provider retry.

---

# 267. Outbox

Local domain state + outgoing integration event/outbound work intent commits with transactional Outbox when they belong to same module transaction. Provider HTTP call happens after commit.

---

# 268. Inbox

Provider/broker events are processed with inbox/idempotency semantics. At-least-once transport cannot become at-least-once order creation.

---

# 269. Message Envelope

Use platform message envelope with message_id, schema_version, tenant_id, legal_entity_id, correlation_id, causation_id, actor, occurred_at, trace_id, source module/ref.

---

# 270. Webhook Processing Worker

Process durable verified provider events; normalize and dispatch idempotent commands.

---

# 271. Order Poll Worker

Fetch changed/new orders by checkpoint for providers where polling is needed.

---

# 272. Listing Publication Worker

Push create/update/unpublish requests and capture per-item outcomes.

---

# 273. Price Publication Worker

Batch desired price updates from Pricing evidence.

---

# 274. Availability Publication Worker

Coalesce/batch inventory availability changes safely.

---

# 275. Remote Listing Observation Worker

Refresh listing/buyability/issues where provider push notifications are incomplete.

---

# 276. Reconciliation Worker

Compare remote vs desired state on a schedule and open/repair drift.

---

# 277. Settlement Pull Worker

Fetch statements/payout evidence when provider supports finance API.

---

# 278. Credential Health Worker

Detect/refresh expiring credentials and surface failures.

---

# 279. Provider Metadata Refresh Worker

Refresh category/product-type/attribute schemas with version change detection.

---

# 280. Dead Letter Recovery Worker

Controlled replay only after cause/configuration correction.

---

# 281. Job Tenant Context

Every job carries trusted tenant + connection + market context. Job payload never selects arbitrary tenant from untrusted provider body.

---

# 282. Job Idempotency

Every job operation has stable semantic key/version so scheduler retries cannot double-publish destructive operations or double-import orders.

---

# 283. Job Chunking

Large catalog/price/stock operations chunk by provider limits and bounded memory. Checkpoint per safe batch and expose progress.

---

# 284. Job Fairness

Queue partitioning/concurrency prevents one giant tenant/account from monopolizing workers. Provider/global rate limits remain respected.

---

# 285. Performance Product Requirement

Channels must handle high-volume catalogs/orders without slowing interactive Sales/Inventory. External API latency is isolated in asynchronous workers; local mapping/order queues remain indexed and paginated.

---

# 286. Batch Everything That Is Naturally Batch

Catalog lookup, Pricing resolution, Inventory availability and provider publication use batch APIs/read models. N+1 internal queries across thousands of listings are prohibited.

---

# 287. Change-Driven Publication

Prefer publish changed variants/prices/availability instead of full channel scan after every event. Periodic full reconciliation remains a safety net.

---

# 288. Hash/Diff Optimization

Compute normalized desired-state hashes/version refs to avoid outbound calls when nothing material changed. Hash is optimization/evidence, not substitute for authoritative fields.

---

# 289. Cache Policy

Cache immutable/provider metadata and safe reference data with version keys. Never use stale cache as hidden source of Product/Price/Stock truth.

---

# 290. Pagination

All listing/mapping/order/issue/sync screens and APIs are cursor/keyset paginated where high-volume. Avoid unbounded OFFSET on large hot tables.

---

# 291. Provider Timeout Budget

HTTP client has bounded connect/read timeout, retry policy by error class and circuit breaker. UI/API requests do not wait synchronously for marketplace publication.

---

# 292. Connection Isolation

Circuit breaker/rate limit queues isolate one failing provider/connection from others.

---

# 293. Freshness SLO

Define observable freshness goals per stream, e.g. order ingestion lag, stock publication lag, price publication lag and reconciliation age. Exact targets are performance testing/release decisions.

---

# 294. Order Ingestion Priority

Orders/cancellations generally outrank low-priority catalog enrichment during backlog because delayed orders can cause SLA/overselling/customer-impact risk.

---

# 295. Observability Dimensions

Metrics/logs/traces include tenant-safe connection/provider/operation/status dimensions. Never use customer email/order PII as metric labels.

---

# 296. Core Metrics

At least:

```text
webhook_received_total / invalid_total
provider_api_calls / latency / errors / rate_limited
orders_received/imported/failed/duplicate
order_ingestion_lag
listings_desired/published/issues
price_drift_count
availability_drift_count
sync_queue_age
reconciliation_age
credentials_expiring/invalid
settlements_unmatched
conflicts_open
```

---

# 297. Distributed Trace

Correlation flows provider event -> inbox -> Channels command -> Sales/Directory/etc -> result, with provider request IDs captured where safe.

---

# 298. Health Classification

Connection health can be:

```text
Healthy
Warning
Degraded
ActionRequired
Suspended
Unknown/Stale
```

Health is computed from auth, provider availability, backlog, reconciliation freshness and critical mapping/drift failures.

---

# 299. No Green From Last HTTP 200

A connection with valid auth but 5,000 stuck order imports is not Healthy. Health reflects business processing, not only network success.

---

# 300. Commerce Home

Primary workspace summarizes:

```text
Connections
Orders needing attention
Listings/issues
Price/stock drift
Sync health
Open conflicts
Settlement exceptions
```

---

# 301. Connections List UX

Show provider icon/name, store/account, market, state, last successful sync/reconciliation, order backlog and action-required count—without exposing credentials.

---

# 302. Connection Setup Wizard

Recommended flow:

```text
1 Choose provider
2 Authorize/credentials
3 Confirm remote account/store
4 Select legal entity/market
5 Map Sales/price/inventory policies
6 Discover remote data
7 Review product mappings
8 Preview go-live impact
9 Enable selected sync directions
```

---

# 303. Mapping Workspace

Side-by-side remote item and TODIR ProductVariant, match reasons, SKU/barcode, image thumbnail, category/variant info, create-link/skip/exception actions. Bulk approve only deterministic high-confidence matches.

---

# 304. Listing Workspace

One Product can display status across all connections/markets: Published, Pending, Issue, Unmapped, Suppressed, Paused, Drift. User never has to open five duplicated Product masters.

---

# 305. Listing Issue UX

Show provider human-readable reason plus normalized TODIR guidance, affected field/language/variant, last attempt, provider evidence and retry after correction.

---

# 306. Publication Preview

Before bulk go-live/publication show:

```text
Will create
Will update
Will unpublish
Unmapped
Missing category
Missing required attributes
Invalid identifiers
Currency/price conflicts
Inventory scope conflicts
```

---

# 307. Order Inbox UX

Operators see external order, import state, linked Sales Order, identity mode, mapping warnings, payment/fulfillment evidence and retry/resolve actions. Avoid dumping raw JSON as the main experience.

---

# 308. Conflict UX

Side-by-side local vs remote state, business impact, recommended safe actions and audit reason. High-risk resolution requires permission/approval as configured.

---

# 309. Health Dashboard UX

Explain the cause: token expired, provider throttling, order backlog, missing mapping, stale stock sync, listing rejection, reconciliation overdue—not generic red dot.

---

# 310. Arabic UX

All product/business labels use translation keys; provider error text can be preserved but TODIR normalized message/guidance should support Arabic/English. RTL is first-class.

---

# 311. Progressive Disclosure

Small merchants see Connect/Products/Orders/Issues. Advanced tabs for mappings, reconciliation, settlement evidence, schemas, raw provider diagnostics appear by role/need.

---

# 312. Permissions

Initial permissions:

```text
channels.read
channels.connection.manage
channels.connection.authorize
channels.connection.go_live
channels.connection.suspend
channels.policy.manage
channels.mapping.read
channels.mapping.manage
channels.mapping.override
channels.assortment.manage
channels.listing.manage
channels.listing.publish
channels.listing.bulk_publish
channels.order.read
channels.order.retry
channels.order.resolve
channels.conflict.read
channels.conflict.resolve
channels.settlement.read
channels.reconciliation.run
channels.provider_diagnostics.read
channels.audit.read
```

---

# 313. Credential Permission

Ordinary users never view secrets/tokens. Connection authorization/rotation uses restricted operations and vault integration.

---

# 314. Bulk Publish Permission

`channels.listing.bulk_publish` is separate from ordinary listing edit because a mistaken mass publication can affect revenue/customer experience across thousands of SKUs.

---

# 315. Mapping Override Permission

Changing an established external->internal product mapping after order history requires elevated permission and reason.

---

# 316. Conflict Resolution Permission

High-impact cancellation/payment/fulfillment conflict resolution uses specific permission and optional Approval hook; not every e-commerce operator can force it.

---

# 317. Audit — Must Capture

Audit at least:

```text
connect/disconnect/suspend/go-live
credential authorization result metadata (never secret)
policy changes
product/category/attribute/location mapping changes
assortment version activation
listing publish/unpublish intent
manual retry/override
mapping override after history
conflict resolution
settlement evidence acceptance/linking
provider diagnostic access if sensitive
```

---

# 318. Audit vs Operational Log

Audit records user/business decisions. Sync logs record technical attempts. Do not flood immutable audit with every harmless polling request; do not omit human overrides from audit.

---

# 319. Error — `channels.connection.auth_failed`

Provider authorization/credential invalid. Error response includes correlation/connection/external ref where safe and actionable remediation; never includes token or unnecessary PII.

---

# 320. Error — `channels.connection.scope_missing`

Required provider permission/scope missing. Error response includes correlation/connection/external ref where safe and actionable remediation; never includes token or unnecessary PII.

---

# 321. Error — `channels.connection.remote_account_mismatch`

Authorized remote account differs from configured identity. Error response includes correlation/connection/external ref where safe and actionable remediation; never includes token or unnecessary PII.

---

# 322. Error — `channels.mapping.product_missing`

Remote product/variant has no approved TODIR mapping. Error response includes correlation/connection/external ref where safe and actionable remediation; never includes token or unnecessary PII.

---

# 323. Error — `channels.mapping.ambiguous`

Multiple TODIR candidates; manual review required. Error response includes correlation/connection/external ref where safe and actionable remediation; never includes token or unnecessary PII.

---

# 324. Error — `channels.mapping.conflict`

External identity already mapped inconsistently. Error response includes correlation/connection/external ref where safe and actionable remediation; never includes token or unnecessary PII.

---

# 325. Error — `channels.listing.not_ready`

Required category/attribute/content/identifier missing. Error response includes correlation/connection/external ref where safe and actionable remediation; never includes token or unnecessary PII.

---

# 326. Error — `channels.listing.provider_rejected`

Provider rejected publication. Error response includes correlation/connection/external ref where safe and actionable remediation; never includes token or unnecessary PII.

---

# 327. Error — `channels.listing.suppressed`

Provider reports listing not buyable/suppressed. Error response includes correlation/connection/external ref where safe and actionable remediation; never includes token or unnecessary PII.

---

# 328. Error — `channels.price.currency_mismatch`

Channel/pricing currency policy invalid. Error response includes correlation/connection/external ref where safe and actionable remediation; never includes token or unnecessary PII.

---

# 329. Error — `channels.price.drift`

Remote observed price differs from desired price. Error response includes correlation/connection/external ref where safe and actionable remediation; never includes token or unnecessary PII.

---

# 330. Error — `channels.inventory.scope_invalid`

Inventory source/location policy invalid. Error response includes correlation/connection/external ref where safe and actionable remediation; never includes token or unnecessary PII.

---

# 331. Error — `channels.inventory.drift`

Remote observed quantity differs from desired availability. Error response includes correlation/connection/external ref where safe and actionable remediation; never includes token or unnecessary PII.

---

# 332. Error — `channels.order.duplicate`

External order already linked/imported. Error response includes correlation/connection/external ref where safe and actionable remediation; never includes token or unnecessary PII.

---

# 333. Error — `channels.order.total_mismatch`

Normalized order total cannot be reconciled. Error response includes correlation/connection/external ref where safe and actionable remediation; never includes token or unnecessary PII.

---

# 334. Error — `channels.order.unknown_product`

Order line has no safe ProductVariant mapping. Error response includes correlation/connection/external ref where safe and actionable remediation; never includes token or unnecessary PII.

---

# 335. Error — `channels.order.identity_insufficient`

Buyer identity policy cannot safely resolve customer. Error response includes correlation/connection/external ref where safe and actionable remediation; never includes token or unnecessary PII.

---

# 336. Error — `channels.order.change_conflict`

Remote change conflicts with internal order/fulfillment state. Error response includes correlation/connection/external ref where safe and actionable remediation; never includes token or unnecessary PII.

---

# 337. Error — `channels.webhook.signature_invalid`

Webhook authentication failed. Error response includes correlation/connection/external ref where safe and actionable remediation; never includes token or unnecessary PII.

---

# 338. Error — `channels.webhook.duplicate`

Provider event already processed/recorded. Error response includes correlation/connection/external ref where safe and actionable remediation; never includes token or unnecessary PII.

---

# 339. Error — `channels.provider.rate_limited`

Provider rate limit reached. Error response includes correlation/connection/external ref where safe and actionable remediation; never includes token or unnecessary PII.

---

# 340. Error — `channels.provider.timeout_unknown`

Provider operation outcome unknown after timeout. Error response includes correlation/connection/external ref where safe and actionable remediation; never includes token or unnecessary PII.

---

# 341. Error — `channels.provider.schema_changed`

Provider metadata/schema changed and mapping requires revalidation. Error response includes correlation/connection/external ref where safe and actionable remediation; never includes token or unnecessary PII.

---

# 342. Error — `channels.reconciliation.overdue`

Remote reconciliation stale beyond policy. Error response includes correlation/connection/external ref where safe and actionable remediation; never includes token or unnecessary PII.

---

# 343. Error — `channels.settlement.unmatched`

Settlement component cannot be matched safely. Error response includes correlation/connection/external ref where safe and actionable remediation; never includes token or unnecessary PII.

---

# 344. Error — `channels.permission_denied`

Actor lacks required permission. Error response includes correlation/connection/external ref where safe and actionable remediation; never includes token or unnecessary PII.

---

# 345. Error — `channels.concurrency_conflict`

Configuration changed since user loaded it. Error response includes correlation/connection/external ref where safe and actionable remediation; never includes token or unnecessary PII.

---

# 346. Report — Channel Sales

Sales by connection/market/provider from Sales-linked orders; Channels does not recompute revenue truth from raw provider payload.

---

# 347. Report — Order Import Reliability

received/imported/failed/duplicate, lag percentiles and top failure reasons.

---

# 348. Report — Listing Coverage

assortment eligible vs mapped vs ready vs published vs issue/suppressed.

---

# 349. Report — Mapping Coverage

product/variant/category/attribute/location mapping completeness.

---

# 350. Report — Price Drift

desired vs observed discrepancies and age.

---

# 351. Report — Availability Drift

desired vs observed stock discrepancies and age.

---

# 352. Report — Connection Health

auth/API/webhook/backlog/reconciliation health over time.

---

# 353. Report — Marketplace Settlement Exceptions

unmatched payouts/fees/adjustments awaiting finance resolution.

---

# 354. Report — Provider Error Trends

validation/throttling/schema/auth failures by provider/operation without PII.

---

# 355. Report — Operational SLA

order ingestion lag, publication lag, reconciliation freshness.

---

# 356. Analytics Ownership

Operational channel analytics can use Channels read models. Cross-module profitability/revenue/customer analytics belongs Reporting/Insights and consumes canonical projections, not raw provider payload as financial truth.

---

# 357. Existing Store Onboarding Is A Product Feature

Connecting an established store with thousands of products/orders requires discovery, matching, preview and controlled cutover. “Connect API and sync everything” is not acceptable.

---

# 358. Discovery Phase

Pull remote store/account identity, markets, products/variants, categories, locations, open/recent orders and provider metadata according to selected scope without mutating TODIR business masters.

---

# 359. Mapping Preview

Show auto-matched, candidate, missing, conflict and intentionally skipped records before enabling write sync.

---

# 360. Cutover Policy

Explicitly define:

```text
order_import_from
which system owns stock until time X
which system owns price until time X
existing fulfillment responsibility
historical order backfill depth
open order handling
```

---

# 361. Dual-Run Warning

Running legacy OMS and TODIR as simultaneous writers to the same store can cause stock/price/order races. Go-live plan must state writer authority per data class.

---

# 362. Go-Live Dry Run

Before activation, perform listing/price/availability/order mapping validation and report counts/errors. Activation requires operator sign-off for unresolved high-risk conflicts.

---

# 363. Reconnect Same Store

If credential is reauthorized for the same verified remote account, reuse connection/mappings/history. Do not create duplicate connection solely because token changed.

---

# 364. Move Store To Another Legal Entity

Changing LegalEntity on active connection with historical orders/settlements is a governed migration, not simple edit. Prefer new effective connection/routing boundary with explicit cutover.

---

# 365. Provider Migration

Moving from Shopify to Salla/noon/etc. maps both to same Catalog/Pricing/Inventory truth; it never copies old channel listing records as new canonical products.

---

# 366. Testing Strategy

Test domain laws, provider adapters, database constraints, integration contracts, workers, idempotency, reconciliation, security, performance and end-to-end business scenarios. Mock-only happy-path tests are insufficient.

---

# 367. Unit Test — Connection State Transitions

Deterministic unit tests cover connection state transitions; test success and invalid transition/configuration paths, not only happy path.

---

# 368. Unit Test — Provider Capability Gating

Deterministic unit tests cover provider capability gating; test success and invalid transition/configuration paths, not only happy path.

---

# 369. Unit Test — Assortment Include/Exclude

Deterministic unit tests cover assortment include/exclude; test success and invalid transition/configuration paths, not only happy path.

---

# 370. Unit Test — Listing Desired-State Transitions

Deterministic unit tests cover listing desired-state transitions; test success and invalid transition/configuration paths, not only happy path.

---

# 371. Unit Test — Mapping Uniqueness/Override Rules

Deterministic unit tests cover mapping uniqueness/override rules; test success and invalid transition/configuration paths, not only happy path.

---

# 372. Unit Test — Category/Attribute Mapping Version

Deterministic unit tests cover category/attribute mapping version; test success and invalid transition/configuration paths, not only happy path.

---

# 373. Unit Test — Buyer Identity Policy

Deterministic unit tests cover buyer identity policy; test success and invalid transition/configuration paths, not only happy path.

---

# 374. Unit Test — Order Total Normalization/Reconciliation

Deterministic unit tests cover order total normalization/reconciliation; test success and invalid transition/configuration paths, not only happy path.

---

# 375. Unit Test — External Event Stale/Duplicate Comparator

Deterministic unit tests cover external event stale/duplicate comparator; test success and invalid transition/configuration paths, not only happy path.

---

# 376. Unit Test — Availability Policy Formula

Deterministic unit tests cover availability policy formula; test success and invalid transition/configuration paths, not only happy path.

---

# 377. Unit Test — Price Publication Evidence

Deterministic unit tests cover price publication evidence; test success and invalid transition/configuration paths, not only happy path.

---

# 378. Unit Test — Conflict State Machine

Deterministic unit tests cover conflict state machine; test success and invalid transition/configuration paths, not only happy path.

---

# 379. Unit Test — Settlement Component Normalization

Deterministic unit tests cover settlement component normalization; test success and invalid transition/configuration paths, not only happy path.

---

# 380. Unit Test — Error Classification/Retry Policy

Deterministic unit tests cover error classification/retry policy; test success and invalid transition/configuration paths, not only happy path.

---

# 381. Unit Test — Anti-Echo Decision Logic

Deterministic unit tests cover anti-echo decision logic; test success and invalid transition/configuration paths, not only happy path.

---

# 382. Integration Test — PostgreSQL RLS and cross-tenant isolation

Run with real PostgreSQL where database behavior matters and deterministic fake/provider server for external network semantics. Verify PostgreSQL RLS and cross-tenant isolation.

---

# 383. Integration Test — unique external order id prevents duplicate Sales link

Run with real PostgreSQL where database behavior matters and deterministic fake/provider server for external network semantics. Verify unique external order id prevents duplicate Sales link.

---

# 384. Integration Test — mapping FK/reference validation through application layer

Run with real PostgreSQL where database behavior matters and deterministic fake/provider server for external network semantics. Verify mapping FK/reference validation through application layer.

---

# 385. Integration Test — inbox duplicate provider event

Run with real PostgreSQL where database behavior matters and deterministic fake/provider server for external network semantics. Verify inbox duplicate provider event.

---

# 386. Integration Test — outbox publication after commit

Run with real PostgreSQL where database behavior matters and deterministic fake/provider server for external network semantics. Verify outbox publication after commit.

---

# 387. Integration Test — checkpoint crash and replay

Run with real PostgreSQL where database behavior matters and deterministic fake/provider server for external network semantics. Verify checkpoint crash and replay.

---

# 388. Integration Test — worker retry after transient failure

Run with real PostgreSQL where database behavior matters and deterministic fake/provider server for external network semantics. Verify worker retry after transient failure.

---

# 389. Integration Test — provider timeout unknown recovery

Run with real PostgreSQL where database behavior matters and deterministic fake/provider server for external network semantics. Verify provider timeout unknown recovery.

---

# 390. Integration Test — Sales CreateExternalOrder idempotency

Run with real PostgreSQL where database behavior matters and deterministic fake/provider server for external network semantics. Verify Sales CreateExternalOrder idempotency.

---

# 391. Integration Test — Directory guest/masked identity policy

Run with real PostgreSQL where database behavior matters and deterministic fake/provider server for external network semantics. Verify Directory guest/masked identity policy.

---

# 392. Integration Test — Pricing batch publication

Run with real PostgreSQL where database behavior matters and deterministic fake/provider server for external network semantics. Verify Pricing batch publication.

---

# 393. Integration Test — Inventory batch availability

Run with real PostgreSQL where database behavior matters and deterministic fake/provider server for external network semantics. Verify Inventory batch availability.

---

# 394. Integration Test — concurrency on mapping change

Run with real PostgreSQL where database behavior matters and deterministic fake/provider server for external network semantics. Verify concurrency on mapping change.

---

# 395. Integration Test — raw payload evidence retention/ref access

Run with real PostgreSQL where database behavior matters and deterministic fake/provider server for external network semantics. Verify raw payload evidence retention/ref access.

---

# 396. Integration Test — reconciliation creates drift/conflict without direct cross-schema writes

Run with real PostgreSQL where database behavior matters and deterministic fake/provider server for external network semantics. Verify reconciliation creates drift/conflict without direct cross-schema writes.

---

# 397. Security Test — invalid webhook signature

Negative/red-team test: invalid webhook signature. Request must fail safely, be observable and avoid sensitive data disclosure.

---

# 398. Security Test — replay webhook

Negative/red-team test: replay webhook. Request must fail safely, be observable and avoid sensitive data disclosure.

---

# 399. Security Test — tenant spoof in webhook body

Negative/red-team test: tenant spoof in webhook body. Request must fail safely, be observable and avoid sensitive data disclosure.

---

# 400. Security Test — cross-tenant external ref lookup

Negative/red-team test: cross-tenant external ref lookup. Request must fail safely, be observable and avoid sensitive data disclosure.

---

# 401. Security Test — credential leakage in log/error

Negative/red-team test: credential leakage in log/error. Request must fail safely, be observable and avoid sensitive data disclosure.

---

# 402. Security Test — PII leakage in metric label

Negative/red-team test: PII leakage in metric label. Request must fail safely, be observable and avoid sensitive data disclosure.

---

# 403. Security Test — ordinary user tries mapping override

Negative/red-team test: ordinary user tries mapping override. Request must fail safely, be observable and avoid sensitive data disclosure.

---

# 404. Security Test — provider callback references wrong connection

Negative/red-team test: provider callback references wrong connection. Request must fail safely, be observable and avoid sensitive data disclosure.

---

# 405. Security Test — malicious oversized payload

Negative/red-team test: malicious oversized payload. Request must fail safely, be observable and avoid sensitive data disclosure.

---

# 406. Security Test — SSRF/unsafe remote URL if provider payload contains URLs

Negative/red-team test: SSRF/unsafe remote URL if provider payload contains URLs. Request must fail safely, be observable and avoid sensitive data disclosure.

---

# 407. Performance Test — 100k and 1m listing query/pagination

Benchmark 100k and 1m listing query/pagination with representative indices, memory, queue depth and provider latency simulation. Pass criteria set before production based on target customer sizes.

---

# 408. Performance Test — 10k product bulk publication preview

Benchmark 10k product bulk publication preview with representative indices, memory, queue depth and provider latency simulation. Pass criteria set before production based on target customer sizes.

---

# 409. Performance Test — high-frequency inventory updates coalescing

Benchmark high-frequency inventory updates coalescing with representative indices, memory, queue depth and provider latency simulation. Pass criteria set before production based on target customer sizes.

---

# 410. Performance Test — order burst ingestion

Benchmark order burst ingestion with representative indices, memory, queue depth and provider latency simulation. Pass criteria set before production based on target customer sizes.

---

# 411. Performance Test — provider rate-limit backlog recovery

Benchmark provider rate-limit backlog recovery with representative indices, memory, queue depth and provider latency simulation. Pass criteria set before production based on target customer sizes.

---

# 412. Performance Test — reconciliation millions of mapped variants by chunks

Benchmark reconciliation millions of mapped variants by chunks with representative indices, memory, queue depth and provider latency simulation. Pass criteria set before production based on target customer sizes.

---

# 413. Performance Test — settlement import with large statement

Benchmark settlement import with large statement with representative indices, memory, queue depth and provider latency simulation. Pass criteria set before production based on target customer sizes.

---

# 414. Performance Test — mapping lookup hot path

Benchmark mapping lookup hot path with representative indices, memory, queue depth and provider latency simulation. Pass criteria set before production based on target customer sizes.

---

# 415. Performance Test — channel dashboard read model under backlog

Benchmark channel dashboard read model under backlog with representative indices, memory, queue depth and provider latency simulation. Pass criteria set before production based on target customer sizes.

---

# 416. Acceptance Scenario — Salla New Order

Salla order arrives twice by webhook/poll; exactly one SalesOrder is created and second delivery resolves idempotently.

---

# 417. Acceptance Scenario — Zid Marketplace Masked Buyer

Zid marketplace order has masked name/email/phone; no fake Party is created; configured marketplace guest Party + BuyerSnapshot is used.

---

# 418. Acceptance Scenario — Shopify Price Change

Pricing version activates; only changed mapped variants queue outbound price publication; observed price later reconciles.

---

# 419. Acceptance Scenario — WooCommerce Custom Status

Plugin exposes unfamiliar status; adapter preserves raw value and maps to Unknown/Review rather than treating as Completed.

---

# 420. Acceptance Scenario — Amazon Listing Requirement Change

Product type definition version changes; affected mappings/listings become RevalidationRequired without corrupting Catalog.

---

# 421. Acceptance Scenario — noon Arabic QC Failure

English content passes but Arabic content fails QC; listing issue points to Arabic field/language while Catalog canonical product remains intact.

---

# 422. Acceptance Scenario — Unknown SKU Order

External order contains unmapped SKU; import waits in exception queue; operator maps variant; retry creates one SalesOrder.

---

# 423. Acceptance Scenario — Duplicate External Order

Same connection + external order ID cannot create second SalesOrder even across crash/retry.

---

# 424. Acceptance Scenario — Stock Safety Buffer

Inventory availability 100, safety buffer 10; desired publish = 90 according to policy without reducing Inventory on-hand.

---

# 425. Acceptance Scenario — Stock Falls During Provider Outage

Provider unavailable while inventory drops; outbound queue stores latest desired state; after recovery provider receives safe current state, not stale intermediate values.

---

# 426. Acceptance Scenario — Remote Stock Drift

Marketplace stock edited manually; reconciliation flags/corrects according to TODIR-authoritative policy.

---

# 427. Acceptance Scenario — Price Drift

Marketplace price differs from Pricing-derived desired value; issue visible and correction is queued; Pricing master is unchanged.

---

# 428. Acceptance Scenario — Remote Cancellation Before Pick

Cancellation enters Sales cancel command and releases downstream commitments through owners.

---

# 429. Acceptance Scenario — Remote Cancellation After Shipment

Conflict opens; Channels does not roll back shipment/order silently.

---

# 430. Acceptance Scenario — Address Change After Label

Remote address update after shipment preparation becomes conflict/review per Shipping/Sales policy.

---

# 431. Acceptance Scenario — Marketplace Fulfilled

Amazon/noon fulfillment is observed without pretending TODIR warehouse shipped the goods.

---

# 432. Acceptance Scenario — Partial Fulfillment

Two packages/statuses map to multiple observations; order is not reduced to one shipped boolean.

---

# 433. Acceptance Scenario — COD Delivered Not Remitted

Order delivered but COD remittance not received; Payment/Bank state remains outstanding; Channels shows provider observation only.

---

# 434. Acceptance Scenario — Marketplace Settlement

Gross 1000 with fees/refund/net payout is recorded as typed evidence; no Product price/accounting mutation occurs in Channels.

---

# 435. Acceptance Scenario — Bank Payout Match

Finance later links marketplace payout evidence to Banking/Payments reconciliation via opaque reference.

---

# 436. Acceptance Scenario — Connection Token Expires

Channel becomes ActionRequired/Degraded; mappings/history remain; reauthorization resumes sync.

---

# 437. Acceptance Scenario — Reconnect Same Store

Same verified remote store reuses mappings rather than duplicating connection data.

---

# 438. Acceptance Scenario — Provider Rate Limit

Workers throttle/backoff; order priority and backlog metrics remain visible; no request storm.

---

# 439. Acceptance Scenario — Webhook Replay Attack

Old/duplicate signed payload fails replay/dedup policy without business re-execution.

---

# 440. Acceptance Scenario — Provider Timeout Unknown

Outbound listing mutation times out; worker queries/reconciles before unsafe retry.

---

# 441. Acceptance Scenario — Go-Live Existing 8000 Products

Discovery/matching preview shows mapped/missing/conflicts; no products are overwritten until approval.

---

# 442. Acceptance Scenario — One Product Five Channels

Same ProductVariant has five listings with independent statuses/content overlays and one Catalog identity.

---

# 443. Acceptance Scenario — Channel Unpublish

Removing from assortment unpublishes/pauses remote listing per policy but keeps Product and historical orders.

---

# 444. Acceptance Scenario — Legal Entity Routing

Order from connection is always routed to configured LegalEntity; provider body cannot override it.

---

# 445. Acceptance Scenario — Multi-Currency Order

Remote currency/rate evidence is retained; FinBase/Sales validate; Channels does not create accounting FX entries.

---

# 446. Acceptance Scenario — External Tax Totals

Provider tax evidence is preserved but Invoicing/Tax/Compliance determine legal TODIR invoice behavior.

---

# 447. Acceptance Scenario — Remote Refund

Refund event invokes/links proper Payments/Invoicing return workflows; no direct cash mutation.

---

# 448. Acceptance Scenario — Provider Schema Adds Enum

Unknown new remote enum becomes explicit unmapped/provider-schema issue instead of silent default.

---

# 449. Acceptance Scenario — Bulk Publish Partial Failure

900 succeed, 100 fail; retry addresses only failures/current desired state without recreating successful listings.

---

# 450. Acceptance Scenario — Cross-Tenant Attack

Tenant A cannot resolve/update Tenant B connection, mapping, event, order link, evidence or settlement ref by guessed ID.

---

# 451. Provider Design Note — Salla

Arabic-market P1. Support OAuth2/scopes, orders/products/customers/shipping/financial/localization APIs and store webhooks. Handle API deprecations as versioned adapter changes.

---

# 452. Provider Design Note — Zid

Arabic-market P1. Support orders/products/inventories/locations/webhooks. Explicitly respect marketplace customer data masking and multi-currency evidence.

---

# 453. Provider Design Note — Shopify

P1 global/region merchant platform. Model Catalog/Publication/Market concepts, locations/inventory, orders/webhooks and versioned GraphQL Admin API.

---

# 454. Provider Design Note — WooCommerce

P1 open ecosystem. REST/webhooks, but extensions can add order statuses/fields; adapter must be defensive and configurable.

---

# 455. Provider Design Note — Amazon

P2 high-complexity marketplace. Product Type Definitions, Listings, Catalog Items, Orders, Notifications, inventory/fulfillment and Finances evidence; marketplace/product schema version changes are normal.

---

# 456. Provider Design Note — noon

P2 regional marketplace. Catalog/Content/Pricing/Stock/Offer/Orders/Fulfillment/Returns/Events; bilingual content/QC is a first-class design reference.

---

# 457. Provider Design Note — Adobe Commerce / Magento

P3. Multi-source inventory/salable quantity concepts are useful integration evidence; TODIR remains Inventory authority according to configured policy.

---

# 458. Provider Design Note — Custom Commerce API

P3 generic integration must use a documented canonical contract and connector implementation, not database access.

---

# 459. Build Now — V1 Core

Production V1 should include:

```text
Channel connections + credential references
Provider capability model
Remote discovery
Product/variant mapping
Category/attribute/location mapping foundations
Assortments/publication intent
Listing state + issues
Arabic/English content overlay
Price publication from Pricing
Availability publication from Inventory
Order ingestion + idempotency
Buyer identity policy/snapshots
Order change/cancellation handling
Fulfillment observation/handoff
Return/refund handoff
Webhook + polling + checkpoint
Reconciliation + drift/conflict
Operational health dashboard
Marketplace settlement/fee evidence foundation
Audit/security/performance/runbooks
```

---

# 460. Design Now / Build Later — Native TODIR Storefront

Design seams for `Provider = TodirStorefront` but defer full storefront experience:

```text
Cart
Checkout
SEO/CMS
Theme/page builder
Customer account UX
Storefront search
Merchandising experience
```

Future Storefront consumes Catalog/Pricing/Inventory/Tax/Payments/Sales/Shipping/Portal/Studio rather than duplicating them.

---

# 461. Design Now / Build Later — Advanced Marketplace Finance

Full automated fee accrual/account mapping, settlement-to-ledger orchestration and profitability analytics belong Finance/Reporting integration after marketplace evidence is stable.

---

# 462. Design Now / Build Later — Feed/Social Commerce

Google Merchant/social catalog syndication can reuse publication/mapping model but should be added after priority commerce connectors unless market validation moves it earlier.

---

# 463. Do Not Build In 31

Do not build here:

```text
Second Product master
Second Pricing engine
Second Inventory ledger
Generic Promotions engine
Carrier/label/routing engine
Legal invoice engine
Payment gateway engine
Accounting posting engine
Full CMS/site builder
Advertising campaign manager
Customer-support ticketing
```

---

# 464. Implementation Phase CH00 — Governance & Architecture Contracts

Create module skeleton, AGENTS scope, manifest, hard/optional dependencies, architecture tests and no-cross-schema rules.

---

# 465. CH00 Exit Gate

Phase `CH00` is complete only when its domain/API/database tests pass, architecture boundaries remain intact, observability exists for its critical failures, and no unresolved CRITICAL correctness/security defect remains.

---

# 466. Implementation Phase CH01 — Connection Core

ChannelProvider/Connection/Market/Policy lifecycle, vault references, permissions and go-live contract.

---

# 467. CH01 Exit Gate

Phase `CH01` is complete only when its domain/API/database tests pass, architecture boundaries remain intact, observability exists for its critical failures, and no unresolved CRITICAL correctness/security defect remains.

---

# 468. Implementation Phase CH02 — Provider Adapter SPI

Capability manifest, auth client boundaries, errors, rate limits, version metadata and test harness.

---

# 469. CH02 Exit Gate

Phase `CH02` is complete only when its domain/API/database tests pass, architecture boundaries remain intact, observability exists for its critical failures, and no unresolved CRITICAL correctness/security defect remains.

---

# 470. Implementation Phase CH03 — Discovery & Mapping

Remote discovery, external references, product/variant match workflow and mapping UX.

---

# 471. CH03 Exit Gate

Phase `CH03` is complete only when its domain/API/database tests pass, architecture boundaries remain intact, observability exists for its critical failures, and no unresolved CRITICAL correctness/security defect remains.

---

# 472. Implementation Phase CH04 — Category/Attribute/Location Mapping

Typed provider taxonomy/schema mapping with versioning and validation.

---

# 473. CH04 Exit Gate

Phase `CH04` is complete only when its domain/API/database tests pass, architecture boundaries remain intact, observability exists for its critical failures, and no unresolved CRITICAL correctness/security defect remains.

---

# 474. Implementation Phase CH05 — Assortments & Listings

Assortment/version, desired/observed listing states, issues and publication preview.

---

# 475. CH05 Exit Gate

Phase `CH05` is complete only when its domain/API/database tests pass, architecture boundaries remain intact, observability exists for its critical failures, and no unresolved CRITICAL correctness/security defect remains.

---

# 476. Implementation Phase CH06 — Content & Localization

Channel content overlays, AR/EN, media selection and provider QC evidence.

---

# 477. CH06 Exit Gate

Phase `CH06` is complete only when its domain/API/database tests pass, architecture boundaries remain intact, observability exists for its critical failures, and no unresolved CRITICAL correctness/security defect remains.

---

# 478. Implementation Phase CH07 — Price Publication

Pricing batch contracts, evidence, drift detection and bulk worker.

---

# 479. CH07 Exit Gate

Phase `CH07` is complete only when its domain/API/database tests pass, architecture boundaries remain intact, observability exists for its critical failures, and no unresolved CRITICAL correctness/security defect remains.

---

# 480. Implementation Phase CH08 — Availability Publication

Inventory batch availability, channel exposure policy, location mapping and safe coalescing.

---

# 481. CH08 Exit Gate

Phase `CH08` is complete only when its domain/API/database tests pass, architecture boundaries remain intact, observability exists for its critical failures, and no unresolved CRITICAL correctness/security defect remains.

---

# 482. Implementation Phase CH09 — Webhook/Inbox Infrastructure

Verified durable provider event receipt, dedup, replay protection and processing worker.

---

# 483. CH09 Exit Gate

Phase `CH09` is complete only when its domain/API/database tests pass, architecture boundaries remain intact, observability exists for its critical failures, and no unresolved CRITICAL correctness/security defect remains.

---

# 484. Implementation Phase CH10 — Order Ingestion

ExternalOrderEnvelope, total validation, external order link and Sales idempotent creation.

---

# 485. CH10 Exit Gate

Phase `CH10` is complete only when its domain/API/database tests pass, architecture boundaries remain intact, observability exists for its critical failures, and no unresolved CRITICAL correctness/security defect remains.

---

# 486. Implementation Phase CH11 — Buyer Identity

Owned-store, guest and masked-marketplace identity policies with Directory integration.

---

# 487. CH11 Exit Gate

Phase `CH11` is complete only when its domain/API/database tests pass, architecture boundaries remain intact, observability exists for its critical failures, and no unresolved CRITICAL correctness/security defect remains.

---

# 488. Implementation Phase CH12 — Order Changes & Conflicts

Cancellation/amendment flows, stale/out-of-order handling and conflict workbench.

---

# 489. CH12 Exit Gate

Phase `CH12` is complete only when its domain/API/database tests pass, architecture boundaries remain intact, observability exists for its critical failures, and no unresolved CRITICAL correctness/security defect remains.

---

# 490. Implementation Phase CH13 — Fulfillment/Returns Handoffs

External fulfillment observations, return/refund input and future Shipping contracts.

---

# 491. CH13 Exit Gate

Phase `CH13` is complete only when its domain/API/database tests pass, architecture boundaries remain intact, observability exists for its critical failures, and no unresolved CRITICAL correctness/security defect remains.

---

# 492. Implementation Phase CH14 — Settlement Evidence

Marketplace payout/fee/adjustment evidence and Finance-facing read contracts.

---

# 493. CH14 Exit Gate

Phase `CH14` is complete only when its domain/API/database tests pass, architecture boundaries remain intact, observability exists for its critical failures, and no unresolved CRITICAL correctness/security defect remains.

---

# 494. Implementation Phase CH15 — Polling/Checkpoints/Reconciliation

Incremental fetch, checkpoint correctness, drift repair and anti-echo.

---

# 495. CH15 Exit Gate

Phase `CH15` is complete only when its domain/API/database tests pass, architecture boundaries remain intact, observability exists for its critical failures, and no unresolved CRITICAL correctness/security defect remains.

---

# 496. Implementation Phase CH16 — Operations UX & Reporting

Health dashboard, order exceptions, listing issues, mapping coverage and reports.

---

# 497. CH16 Exit Gate

Phase `CH16` is complete only when its domain/API/database tests pass, architecture boundaries remain intact, observability exists for its critical failures, and no unresolved CRITICAL correctness/security defect remains.

---

# 498. Implementation Phase CH17 — Security/Privacy/Observability

Credential/PII hardening, RLS, tracing, metrics, runbooks and red-team tests.

---

# 499. CH17 Exit Gate

Phase `CH17` is complete only when its domain/API/database tests pass, architecture boundaries remain intact, observability exists for its critical failures, and no unresolved CRITICAL correctness/security defect remains.

---

# 500. Implementation Phase CH18 — Provider P1 Adapters

Salla, Zid, Shopify, WooCommerce adapters certified against contracts.

---

# 501. CH18 Exit Gate

Phase `CH18` is complete only when its domain/API/database tests pass, architecture boundaries remain intact, observability exists for its critical failures, and no unresolved CRITICAL correctness/security defect remains.

---

# 502. Implementation Phase CH19 — Provider P2 Adapters

Amazon EG/SA/UAE and noon with advanced listing/schema/settlement behavior.

---

# 503. CH19 Exit Gate

Phase `CH19` is complete only when its domain/API/database tests pass, architecture boundaries remain intact, observability exists for its critical failures, and no unresolved CRITICAL correctness/security defect remains.

---

# 504. Implementation Phase CH20 — Scale/Performance

Large catalog/order benchmarks, fair queues, rate-limit stress and recovery drills.

---

# 505. CH20 Exit Gate

Phase `CH20` is complete only when its domain/API/database tests pass, architecture boundaries remain intact, observability exists for its critical failures, and no unresolved CRITICAL correctness/security defect remains.

---

# 506. Implementation Phase CH21 — Migration & Go-Live

Existing merchant discovery/mapping/cutover tooling and reconciliation sign-off.

---

# 507. CH21 Exit Gate

Phase `CH21` is complete only when its domain/API/database tests pass, architecture boundaries remain intact, observability exists for its critical failures, and no unresolved CRITICAL correctness/security defect remains.

---

# 508. Implementation Phase CH22 — Release Gate

End-to-end acceptance, provider certification, operational readiness and architecture review.

---

# 509. CH22 Exit Gate

Phase `CH22` is complete only when its domain/API/database tests pass, architecture boundaries remain intact, observability exists for its critical failures, and no unresolved CRITICAL correctness/security defect remains.

---

# 510. Runbook — Provider credentials expired

Suspend unsafe writes, surface action required, reauthorize, verify remote account identity, replay/reconcile backlog.

---

# 511. Runbook — Orders missing

Check webhook receipt, poll checkpoint, provider API, queue/inbox, mapping/import errors; backfill by safe window; verify no duplicates.

---

# 512. Runbook — Duplicate order suspected

Search channel order unique key/link and Sales idempotency; do not manually create another order; reconcile provider vs internal refs.

---

# 513. Runbook — Stock drift/oversell risk

Pause/zero outbound availability if needed, check Inventory scope/mapping/backlog, reconcile current desired quantity, resume after confidence restored.

---

# 514. Runbook — Wrong product mapping

Freeze affected listing/order automation, assess historical orders, correct mapping with elevated permission, do not rewrite historical snapshots, compensate operationally.

---

# 515. Runbook — Price published wrong

Pause/correct listing, inspect Pricing context/mapping/currency/version, republish correct price, review affected orders separately—never rewrite accepted orders.

---

# 516. Runbook — Provider API schema changed

Mark adapter/connection degraded as needed, capture unknown fields/status, update versioned adapter/mapping, certification tests, then resume.

---

# 517. Runbook — Rate limit storm

Reduce concurrency, honor backoff, prioritize orders/cancellations, let low-priority catalog queue age visibly, recover gradually.

---

# 518. Runbook — Unknown outbound mutation outcome

Do not blind retry if provider operation is non-idempotent; query observed state/reconcile, then decide corrective action.

---

# 519. Runbook — Marketplace payout mismatch

Freeze auto-match for statement, inspect fee/adjustment/order refs, compare bank payout, send unmatched items to Finance review.

---

# 520. Runbook — Webhook security incident

Reject invalid traffic, rotate secret if compromised, inspect replay/tenant routing, review audit, do not expose raw credentials/PII.

---

# 521. Runbook — Connection reauthorized

Verify same remote merchant/store ID, preserve mappings/history, refresh capabilities/webhooks/checkpoints, reconcile before clearing degraded state.

---

# 522. Architecture Invariant

No external order can create two Sales Orders for the same connection/order ID.

---

# 523. Architecture Invariant

No remote variant/listing identity silently maps to two ProductVariants in one identity scope.

---

# 524. Architecture Invariant

Channels never writes Catalog/Pricing/Sales/Inventory/Payments/Invoicing/Shipping/Accounting tables directly.

---

# 525. Architecture Invariant

No masked marketplace identity is promoted automatically to canonical Party contact data.

---

# 526. Architecture Invariant

No provider HTTP 200 alone marks a listing Published unless provider semantics prove it.

---

# 527. Architecture Invariant

No webhook retry repeats a completed business action.

---

# 528. Architecture Invariant

No checkpoint advances past uncommitted processing.

---

# 529. Architecture Invariant

No external cancellation rewrites a fulfilled internal order silently.

---

# 530. Architecture Invariant

No external refund directly mutates cash/open item/invoice state.

---

# 531. Architecture Invariant

No marketplace fee changes Product Price master.

---

# 532. Architecture Invariant

No raw access token is persisted in Channels domain tables or logs.

---

# 533. Architecture Invariant

No cross-tenant connection/mapping/order/evidence access by guessed ID.

---

# 534. Architecture Invariant

No bulk publication starts without validated configuration and preview where material risk exists.

---

# 535. Architecture Invariant

No stock publication uses arbitrary stale data beyond configured fail-safe policy.

---

# 536. Architecture Invariant

No provider schema/enum change is coerced to a misleading known state.

---

# 537. Architecture Invariant

No disabling/disconnecting channel deletes historical evidence/mappings/orders.

---

# 538. Architecture Invariant

No remote direct edit silently overwrites canonical Catalog/Pricing/Inventory truth.

---

# 539. Architecture Invariant

No reconciliation performs cross-module direct SQL fixes.

---

# 540. Final Agent Rule — Role

Implementation agents act as Senior Product Architect + Senior Software/Systems Architect + Senior Engineer and protect business truth before feature speed.

---

# 541. Final Agent Rule — Governance

Read repository root/scoped AGENTS.md and canonical packs before implementation; do not improvise conflicting ownership.

---

# 542. Final Agent Rule — Provider Neutrality

Core domain may not contain provider-name branches for business meaning; provider variance belongs adapters/capability metadata.

---

# 543. Final Agent Rule — Product Identity

Never create channel-specific canonical product masters.

---

# 544. Final Agent Rule — Pricing

Never introduce a second reusable e-commerce/marketplace price truth.

---

# 545. Final Agent Rule — Inventory

Never treat remote quantity as permission to mutate Inventory directly.

---

# 546. Final Agent Rule — Orders

External order import is idempotent and produces/links Sales-owned order exactly once.

---

# 547. Final Agent Rule — Customer Identity

Guest/masked/relay identity follows explicit quality-aware policy; no master-data pollution.

---

# 548. Final Agent Rule — Finance

Gross sale, fee, settlement and bank payout remain separate facts.

---

# 549. Final Agent Rule — History

External changes create commands/adjustments/conflicts; never rewrite history silently.

---

# 550. Final Agent Rule — Webhooks

Verify, persist, ACK, then process; do not run long multi-module work inside webhook request.

---

# 551. Final Agent Rule — Polling

Use checkpointed incremental fetch/reconciliation as safety net where provider supports it.

---

# 552. Final Agent Rule — Rate Limits

Backoff/throttle fairly; never turn provider throttling into request storm.

---

# 553. Final Agent Rule — Unknown Outcome

Query/reconcile uncertain remote mutations before unsafe retry.

---

# 554. Final Agent Rule — Secrets

CredentialRef only in domain; secret vault owns secrets; no secret in logs/AI.

---

# 555. Final Agent Rule — Privacy

Minimize PII and protect raw provider payloads with retention/access policy.

---

# 556. Final Agent Rule — Performance

Batch internal reads and external writes; avoid N+1 and full scans on every change.

---

# 557. Final Agent Rule — Observability

Every business-impacting sync failure must be discoverable by operator without reading server logs.

---

# 558. Final Agent Rule — Testing

Provider happy path is insufficient; duplicate/out-of-order/retry/schema/rate-limit/security cases are release blockers.

---

# 559. Final Agent Rule — Build Later

Future storefront/feed/advanced finance seams are preserved without prematurely building those products.

---

# 560. Release Gate — Required Evidence

Before production release of Pack 31 implementation, provide evidence for:

```text
Architecture tests: no forbidden module references/db writes
RLS/cross-tenant security
Credential vault/no-secret logging
Webhook signature/replay tests
Order idempotency duplicate/crash tests
Out-of-order/stale event tests
Provider rate-limit/backoff tests
Mapping uniqueness/override tests
Price/availability publication correctness
Provider timeout-unknown recovery
Reconciliation/drift tests
Buyer masked/guest identity tests
Sales/Directory/Pricing/Inventory integration contracts
Settlement evidence traceability
Large-catalog/order performance tests
Operations dashboard/runbooks
Provider adapter certification for enabled providers
Go-live migration/cutover dry run
```

---

# 561. Release Sign-Off

Recommended sign-off roles:

```text
Product Owner / Commerce Lead
Product/Systems Architect
Sales/Catalog/Inventory domain owners
Finance/Payments reviewer for settlement evidence
Security/Privacy reviewer
Backend/Frontend leads
QA/Performance
Operations/Implementation lead
```

---

# 562. No Separate Channel Subfeature Packs

Keep Channels customer-facing commerce integration requirements in this comprehensive Pack 31. Split only true shared/platform or future standalone domains such as Shipping, Portal/Storefront Experience or enterprise Integrations Hub when justified.

---

# 563. Official Market / Platform Source Register

Verified/reviewed during design on **2026-10-08**. Sources provide market/API behavior evidence; they do not authorize copying competitor architecture blindly.

1. **Salla Developer Platform** — Merchant APIs across orders, products, customers, shipment integration, financial management, localization; OAuth2 and webhooks.  
   `https://docs.salla.dev/get-started`  
   `https://docs.salla.dev/421119m0`  
   `https://docs.salla.dev/5394146e0`

2. **Zid Developer Platform** — Orders, Products, Inventories, Locations and Webhooks. Marketplace order buyer fields can be masked for privacy.  
   `https://docs.zid.sa/start-here`  
   `https://docs.zid.sa/webhooks`  
   `https://docs.zid.sa/list-of-orders`

3. **Shopify Admin GraphQL / Catalog** — Catalog can carry publication and price-list context; versioned Admin API and webhook model support channel integration.  
   `https://shopify.dev/docs/api/admin-graphql/latest/interfaces/catalog`  
   `https://shopify.dev/docs/apps/build/markets/catalogs`  
   `https://shopify.dev/docs/apps/build/webhooks`

4. **WooCommerce REST API / Webhooks** — Orders/Products/Customers APIs and HMAC-based webhook delivery; open extension ecosystem requires defensive adapters.  
   `https://developer.woocommerce.com/docs/apis/rest-api/v3/orders/`  
   `https://developer.woocommerce.com/docs/apis/rest-api/v2/webhooks`

5. **Amazon Selling Partner API** — Product Type Definitions, Listings Items, Catalog Items and Notifications for listing lifecycle/issues/product-type changes; Orders/Finances/fulfillment APIs are separate domains.  
   `https://developer-docs.amazon.com/sp-api/lang-en_EN/docs/manage-product-listings-guide`  
   `https://developer-docs.amazon.com/sp-api/docs/notification-type-values`

6. **noon Partners API** — Catalog, Content, Marketplace Pricing, Stock, Offers, Event Notifications, Warehouse/Fulfillment/Returns. Content API supports bilingual Arabic/English submission and independent QC/completeness.  
   `https://noon-docs.noonpartners.dev/`  
   `https://noon-docs.noonpartners.dev/docs/overview/api-domains`  
   `https://noon-docs.noonpartners.dev/docs/content/content-api`

7. **Microsoft Dynamics 365 Commerce** — Channels have product/payment/price-group context; assortments map products/product groups to channels and are published/processed.  
   `https://learn.microsoft.com/en-us/dynamics365/commerce/channels-overview`  
   `https://learn.microsoft.com/en-us/dynamics365/commerce/assortments`

8. **Adobe Commerce Inventory Management** — Sources represent physical inventory locations; Stocks aggregate sources for sales channels; Salable Quantity considers reservations/thresholds and is distinct from physical quantity.  
   `https://experienceleague.adobe.com/en/docs/commerce-admin/inventory/basics/sources-stocks`  
   `https://experienceleague.adobe.com/en/docs/commerce-admin/inventory/basics/selection-reservations`

Provider API versions, scopes, commercial availability, certification requirements, regional marketplace programs and country legal/tax obligations are **MARKET/LEGAL/PROVIDER VERIFICATION at implementation and release time**.

---

# 564. Market Positioning

TODIR should position Channels as an **ERP-native multi-channel commerce control center** for Arabic-region merchants: simpler to operate than specialist integration middleware for common ERP flows, but architecturally strict enough for Amazon/noon-grade listing complexity, Salla/Zid regional commerce, Shopify/WooCommerce owned stores and future channels.

---

# 565. Differentiator

The differentiator is not “we connect to Shopify.” It is:

> **One canonical Product, one Pricing engine, one Inventory truth, one Sales domain and one financial truth—operated coherently across many external stores/marketplaces with visible drift, safe idempotency and Arabic-first commerce operations.**

---

# 566. Architecture Closure

Pack 31 architecture is approved only if implementation preserves these non-negotiables:

```text
1. SalesChannel and ChannelConnection remain separate concepts.
2. Product identity remains Catalog-owned; listings are channel representations.
3. Pricing remains the reusable price source; Channels only publishes/evidences it.
4. Inventory remains stock authority; Channels only exposes computed availability.
5. External orders create/link Sales Orders exactly once.
6. Masked/guest buyers do not pollute Directory.
7. Webhook + checkpoint/reconciliation protect against missing/duplicate/out-of-order events.
8. Desired and observed remote state remain separate.
9. Marketplace settlement evidence does not become accounting truth in Channels.
10. Provider adapters are isolated behind capabilities/contracts.
11. Credentials/PII are protected and tenant boundaries enforced.
12. Every drift/failure/conflict is operationally visible and recoverable without direct cross-module DB edits.
```

---

# 567. Next Pack

After Channels & E-commerce, the recommended next domain pack is:

> **`32_ERP_SAAS_SHIPPING_LOGISTICS_FULFILLMENT_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`**

Central question:

> **How does TODIR turn fulfillment intent into correct shipments, carrier/service selection, labels, pickup/handover, tracking, delivery exceptions, COD/remittance and returns across internal warehouses, carriers, 3PLs and marketplace fulfillment—without letting Shipping own Sales or Inventory truth?**

---

# 568. Final Product Verdict

TODIR should build Pack 31 as a **Commerce Channel Hub**, not as a collection of one-off connectors and not as a second e-commerce ERP.

The approved product stance is:

> **Connect many selling surfaces, map them to one internal business truth, ingest every external order exactly once, publish only approved price/availability, preserve provider evidence, surface every drift/conflict, and make failures recoverable without corrupting Sales, Inventory, Customer or Finance history.**

For a small merchant, the experience should feel like “connect store and sync.” For a larger merchant, the same model must safely support many accounts, markets, warehouses, listing schemas, bulk catalogs, marketplace fulfillment and settlement evidence without replacement architecture.

---


<!-- Section count: 569 -->

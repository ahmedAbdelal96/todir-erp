# 32 — TODIR ERP SaaS Shipping, Logistics, Carrier & Delivery Requirements & Technical Design Pack

**File:** `32_ERP_SAAS_SHIPPING_LOGISTICS_CARRIER_DELIVERY_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`  
**Product:** **TODIR**  
**Status:** PRODUCT + TRANSPORTATION EXECUTION + MULTI-CARRIER + RATES + BOOKING + PACKAGES + LABELS + PICKUP + MANIFEST + TRACKING + DELIVERY EXCEPTIONS + POD + RETURNS/RTO + COD EVIDENCE + FREIGHT COST EVIDENCE + IMPLEMENTATION BASELINE  
**Version:** v1.0  
**Date:** 2026-10-08  
**Audience:** Product Owner, Product Architect, Logistics/Shipping Lead, Warehouse Lead, Sales Operations Lead, Purchasing Lead, E-commerce/Marketplace Lead, Finance/Payments/Banking Lead, Customer Support Lead, Backend/Frontend Leads, Integration Architect, Security Engineer, Privacy Lead, Performance Engineer, QA, Implementation Agent  
**Risk Class:** **CRITICAL — DELIVERY PROMISE + DUPLICATE BOOKING/AWB + COD CASH + CUSTOMER PII + FREIGHT COST + RETURNS/RTO + EXTERNAL CARRIER FAILURE + OPERATIONAL TRUST**

---

## Canonical TODIR dependencies

Primary design sources:

- `01_ERP_SAAS_MASTER_MODULE_AND_DEPENDENCY_BLUEPRINT.md`
- `02_ERP_SAAS_PRODUCTION_READY_TECH_STACK_AND_PROJECT_STRUCTURE.md`
- `03_ERP_SAAS_PLATFORM_KERNEL_AND_MODULE_SYSTEM_TECHNICAL_DESIGN_PACK.md`
- `04_ERP_SAAS_DIRECTORY_PARTY_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `05_ERP_SAAS_CATALOG_PRODUCT_UOM_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `06_ERP_SAAS_FINBASE_CURRENCY_PAYMENT_TERMS_MONEY_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `11_ERP_SAAS_INVOICING_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `12_ERP_SAAS_PAYMENTS_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `13_ERP_SAAS_INVENTORY_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `14_ERP_SAAS_SALES_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `15_ERP_SAAS_PURCHASING_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `16_ERP_SAAS_ACCOUNTING_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `25_ERP_SAAS_BANKING_CASH_MANAGEMENT_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `29_ERP_SAAS_HELPDESK_CUSTOMER_SUPPORT_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `31_ERP_SAAS_CHANNELS_ECOMMERCE_MARKETPLACE_INTEGRATION_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`

### Architecture correction locked by this pack

The original master blueprint left Shipping as either a hard Inventory dependency or a Sales fulfillment dependency. This pack resolves that ambiguity.

```text
Shipping hard business dependencies
-----------------------------------
Directory
FinBase

Optional capability providers / business sources
-----------------------------------------------
Inventory
Sales
Purchasing
Channels
Payments
Invoicing
Accounting
Banking / Bank Reconciliation
Approvals
Compliance / Country Providers
HR / Fleet capability
Portal
Support
```

**Reason:** transportation execution exists in valid workflows where TODIR does not own inventory movement: dropship, supplier pickup, marketplace/3PL fulfillment, document/package transport, direct supplier-to-customer shipment and manual/internal delivery. Inventory remains authoritative whenever stock is involved, but Shipping must not require fake stock flows to exist.

### Locked upstream truths

- Directory owns canonical Party/Contact/Address identity; Shipping stores transaction snapshots for booked shipments.
- FinBase owns Money/Currency/rounding primitives.
- Catalog owns Product/UOM identity and reusable physical product data.
- Sales owns commercial customer order commitment, requested delivery promise and Sales Return Authorization.
- Purchasing owns supplier commercial commitment and purchase return authorization.
- Inventory owns Warehouse/Location/Reservation/StockMove/physical receipt/issue/transfer/valuation truth.
- Payments owns Receipt/Payout/refund/money-movement truth. A carrier COD observation is not a Payment.
- Banking/Bank Reconciliation owns actual bank settlement/match truth.
- Invoicing owns customer/vendor legal financial documents.
- Accounting/Ledger owns journals and account determination.
- Channels owns external commerce/channel observations; it must not duplicate TODIR Shipping when TODIR actually controls the shipment.
- Support may consume Shipping status and request actions through approved contracts, but does not mutate Shipping state.
- Platform owns Jobs, Outbox/Inbox, Files, Notifications, Audit, Secrets/Credential Vault, tenant isolation and generic integration infrastructure.
- External carrier APIs are never part of a local DB transaction.
- Every external mutation is idempotent or reconciled when provider idempotency is unavailable.
- AI follows `AI -> Tool Layer -> Permissions -> Application Commands/Queries -> Domain`; never AI -> carrier credentials/database.

---


# 0. Executive Decision

TODIR `32 Shipping & Logistics` is the canonical **transportation-execution domain**. It receives transport demand from business/fulfillment owners, determines or accepts the transport service, manages carrier accounts and services, obtains rates, books transport, creates labels/AWBs, groups packages, schedules pickup/handover, normalizes tracking events, records delivery attempts/POD, manages reverse transport and RTO, and preserves COD/freight-cost evidence.

It does **not** own the commercial order, inventory movement, invoice, customer payment, bank receipt or journal entry.

> **The module must make physical transportation reliable and explainable without becoming a second Sales, Inventory or Finance system.**

---

# 1. Central Product Question

> **Can TODIR move a parcel or shipment from an origin to a destination through the right carrier/service, know exactly what has been booked and physically observed, survive carrier/API failures safely, handle delivery exceptions/returns/COD, and reconcile carrier evidence without corrupting Sales, Inventory or Finance truth?**

---

# 2. Product Law — Transportation Is Its Own Domain

Shipping is not a column on a Delivery Order and not a helper inside Inventory. Carrier/service selection, booking, labels, pickup, transport tracking, delivery attempts, POD and reverse transport have independent lifecycle, failure modes and evidence.

---

# 3. Product Law — Inventory != Shipping

Inventory answers **what quantity exists, where it exists, what is reserved and which stock movement happened**. Shipping answers **how packed goods travel, with whom, under which service/AWB, and what happened during transportation**.

A carrier event never directly changes stock.

---

# 4. Product Law — Sales Order != Shipment

One Sales Order can produce zero, one or many Shipments; one Shipment may consolidate eligible source lines where policy permits. Sales owns ordered/cancelled commercial quantity. Shipping owns transport execution only.

---

# 5. Product Law — Label Created != Shipped

Never show `Shipped` merely because an AWB or label exists.

```text
LabelCreated
!=
Booked
!=
AwaitingPickup
!=
HandedOver
!=
InTransit
!=
Delivered
```

---

# 6. Product Law — Carrier Quote != Actual Carrier Charge

A rate quote is an estimate/offer used for selection. Actual billed freight may later include fuel, remote-area, address-correction, weight/dimension, return or other adjustments. Preserve both; never silently replace one with the other.

---

# 7. Product Law — Customer Delivery Charge != Carrier Cost

Sales/Pricing may charge the customer free shipping, a flat fee, a subsidy or a markup. Shipping owns carrier transport cost evidence; it does not decide the customer selling price for delivery.

---

# 8. Product Law — COD Observation != Payment

Carrier-reported `COD Collected` means the carrier claims it collected money. Actual company cash/bank receipt remains Payments/Banking truth.

```text
COD Requested
-> COD Collected Observation
-> Carrier Remittance Evidence
-> Payments/Banking Actual Receipt
-> Bank Reconciliation
```

---

# 9. Product Law — RTO != Customer Return

Return-to-Origin occurs because the outbound delivery failed or was refused before successful completion. A Customer Return normally starts after commercial return authorization. They may use reverse transport, but they are different business meanings.

---

# 10. Product Law — Provider Raw Evidence Is Preserved

Canonical Shipping states simplify UX, while provider codes/status text/references remain stored for audit and troubleshooting. Never discard the remote evidence merely because a canonical state exists.

---

# 11. Product Law — Small Company First

A small company must be able to configure one carrier, enter/select package data, click **Create Label**, print it, track the shipment and record delivery without learning TMS concepts such as freight units, lanes, tendering or load building.

---

# 12. Product Law — Enterprise Depth Is Progressive

Larger organizations can progressively enable multi-carrier rate shopping, multi-warehouse origins, pickups/manifests, carrier performance, freight-cost reconciliation, internal delivery, international shipping and later route/load/tendering capabilities without replacing Shipping Core.

---

# 13. Absolute Domain Distinctions

```text
Shipment != SalesOrder
Shipment != Inventory StockMove
Shipment != Delivery Invoice
Shipment != Payment
Shipment != Carrier Vendor Bill
Shipment != Channel Fulfillment Observation
Shipment != Load (future)
Shipment != Route Plan (future)
Shipment != Return Authorization
Shipment != RTO reason
Carrier != CarrierAccount
CarrierAccount != CarrierConnection
BookingProvider != ExecutingCarrier
RateQuote != FreightChargeActual
CODObservation != Receipt
POD != Customer commercial acceptance
```

---

# 14. Shipment Source Types

Shipping must accept typed source links instead of a mandatory `sales_order_id`.

```text
SalesFulfillment
PurchaseInbound
PurchaseReturn
WarehouseTransfer
CustomerReturn
ChannelFulfillment
ServiceSparePart
Dropship
ManualShipment
InternalDelivery
OtherApprovedSource
```

The source owner remains authoritative for its business document.

---

# 15. Canonical Simple Outbound Flow

```text
Sales Order / fulfillment intent
-> Inventory pick/pack if Inventory active
-> Shipping shipment draft
-> Rate / select carrier
-> Book
-> Generate label
-> Pickup / Handover
-> Track
-> Delivered / Exception / RTO
-> downstream owners react through typed contracts/events
```

---

# 16. Canonical Dropship Flow

```text
Sales Order
-> Purchasing/Supplier arrangement
-> Shipping origin = Supplier
-> Carrier pickup at Supplier
-> Customer delivery
```

No fake TODIR warehouse receipt/issue is required.

---

# 17. Canonical Purchase Inbound Flow

```text
Supplier ready
-> Shipping books collection
-> In transit
-> Carrier delivers to warehouse
-> Warehouse physically verifies
-> Inventory Receipt
```

Carrier delivery does not auto-create Inventory receipt by default.

---

# 18. Canonical Warehouse Transfer Flow

Inventory owns the transfer and in-transit quantity. Shipping can own the transport booking/tracking between warehouses. Partial receipt/discrepancy remains Inventory truth; Shipping contributes transport evidence.

---

# 19. Canonical Customer Return Transport Flow

```text
Sales Return Authorization
-> Shipping reverse shipment / return label / pickup
-> transport
-> Inventory physical receipt / inspection
-> Invoicing/Payments correction if applicable
```

---

# 20. Canonical RTO Flow

```text
Outbound shipment
-> failed/refused delivery
-> RTO decision/observation
-> ReturningToOrigin
-> ReturnedToOrigin
-> source owner + Inventory decide business/stock consequence
```

---

# 21. Module Key

Recommended technical module key:

```text
shipping
```

---

# 22. Project Names

```text
Todir.Modules.Shipping.Domain
Todir.Modules.Shipping.Application
Todir.Modules.Shipping.Infrastructure
Todir.Shipping.Contracts
```

Provider adapters belong behind Shipping ports, for example:

```text
Todir.Shipping.Providers.OTO
Todir.Shipping.Providers.Aramex
Todir.Shipping.Providers.DHL
```

---

# 23. Database Schema

Recommended schema:

```text
business_shipping
```

Shipping tables only. No cross-module ORM navigation.

---

# 24. DbContext

```text
ShippingDbContext
```

It maps only `business_shipping` data plus platform-owned shared infrastructure contracts where approved.

---

# 25. Hard Dependencies

```text
Directory
FinBase
```

Directory provides Party/Contact/Address identity and carrier-party linkage. FinBase provides Money/Currency/precision primitives. Kernel infrastructure is implicit.

---

# 26. Optional Capability Providers

```text
Inventory
Sales
Purchasing
Channels
Payments
Invoicing
Accounting
Banking/BankRec
Approvals
Compliance/Country Providers
HR/Fleet
Portal
Support
```

Absence of any optional provider has an explicit fallback; no hidden hard dependency.

---

# 27. Carrier Identity

A Carrier represents the transport company/business relationship, for example Aramex, DHL, FedEx, SMSA or a local courier. Canonical legal identity remains a Directory Party; Shipping owns the carrier-operational profile.

---

# 28. CarrierProfile Concept

```text
CarrierProfile
{
    Id
    TenantId
    PartyId
    Code
    DisplayNameSnapshot
    Status
    DefaultMode?
    Notes?
    Version
}
```

---

# 29. CarrierAccount

`CarrierAccount` represents one commercial shipping account/contract context, not the carrier itself.

Examples: `Aramex Egypt Contract A`, `DHL Export Account`, `OTO Aggregated Account`, `Local Courier Cash Account`.

---

# 30. CarrierAccount Concept

```text
CarrierAccount
{
    Id
    TenantId
    LegalEntityId
    CarrierProfileId?
    Name
    AccountNumberMasked?
    BillingCurrency?
    ProviderType
    ContractRef?
    Status
    EffectiveFrom?
    EffectiveTo?
    Version
}
```

---

# 31. CarrierConnection

`CarrierConnection` represents technical integration state for one account/provider. It stores `CredentialRef`, provider/API version, environment and capability snapshot; never raw secrets.

---

# 32. CarrierConnection Concept

```text
CarrierConnection
{
    Id
    TenantId
    CarrierAccountId
    ProviderKey
    CredentialRef
    Environment
    ProviderApiVersion?
    CapabilitySnapshotVersion
    Status
    LastVerifiedAt?
    LastErrorCode?
}
```

---

# 33. Executing Carrier vs Booking Provider

Aggregators require two separate concepts.

```text
BookingProvider = OTO
ExecutingCarrier = SMSA
```

Never overwrite the executing carrier with the aggregator name or vice versa.

---

# 34. Carrier Service

A `CarrierService` is a provider/account-specific delivery product such as Express, Economy, Same Day, Cold Delivery or Heavy/Bulky. Core does not use one permanent enum for every provider offering.

---

# 35. Carrier Service Concept

```text
CarrierService
{
    Id
    CarrierAccountId
    ProviderServiceCode
    DisplayName
    Mode
    Method?
    CapabilityFlags
    Active
    ObservedAt
}
```

---

# 36. Mode and Method

Keep transport mode/method extensible.

```text
Mode examples: Parcel, LTL, FTL
Method examples: Ground, Air, Ocean, Rail
```

V1 product focuses on Parcel/Courier while retaining future-safe contracts.

---

# 37. Carrier Capability Model

Provider adapters expose typed capabilities rather than forcing every carrier into the same feature set.

```text
Serviceability
Rate
BookShipment
CancelShipment
ModifyShipment
MultiPiece
LabelPdf
LabelZpl
Pickup
Manifest
TrackingPull
TrackingWebhook
ProofOfDelivery
ReturnShipment
COD
AddressValidation
International
CustomsDocuments
DeclaredValue
Insurance
ColdChain
HeavyBulky
```

---

# 38. Capability Snapshot

Capabilities are versioned/observed because providers change products/APIs. A previously booked shipment keeps the provider/service evidence used at booking time.

---

# 39. Manual Carrier Mode

Shipping must work without API integration. A manual carrier can support user-entered carrier/service, quoted/actual cost, AWB/tracking number, status updates and POD evidence with audit.

---

# 40. Internal Delivery Mode

V1 may support lightweight internal delivery with optional Employee/DriverRef and VehicleRef, manual/scan status progression and POD. Fleet maintenance, fuel, telematics and payroll remain outside Shipping Core.

---

# 41. Shipment Aggregate

`Shipment` is the central transportation aggregate. It describes one transport execution arrangement from an origin snapshot to a destination snapshot under one coherent carrier/booking context.

---

# 42. Shipment Concept

```text
Shipment
{
    Id
    Number
    TenantId
    LegalEntityId
    BranchId?

    Direction
    ShipmentKind

    OriginSnapshot
    DestinationSnapshot

    CarrierAccountId?
    CarrierServiceRef?
    BookingProviderRef?
    ExecutingCarrierRef?

    BookingState
    ExecutionState
    ReturnState?

    RequestedShipDate?
    PromisedDeliveryAt?
    CarrierEstimatedDeliveryAt?
    LatestObservedEta?
    DeliveredAt?

    CurrencyCode
    RowVersion
}
```

---

# 43. Shipment Numbering

Use Platform Numbering, e.g. `SHP-2026-000123`. Shipment number is a TODIR business reference, not the carrier AWB/tracking number.

---

# 44. Shipment ID != Tracking Number

Technical ID is UUIDv7. Carrier tracking/AWB is an external reference that may change, be replaced, be piece-specific or be missing for manual delivery.

---

# 45. Shipment Direction

Suggested initial values:

```text
Outbound
Inbound
Reverse
InternalTransferTransport
```

Direction describes transport, not stock/accounting effect.

---

# 46. Shipment Kind

Suggested product-facing semantics:

```text
CustomerDelivery
SupplierPickup
CustomerReturnTransport
SupplierReturnTransport
WarehouseTransferTransport
DropshipTransport
ManualParcel
InternalDelivery
```

---

# 47. Source Links

A Shipment stores typed source links instead of cross-module FKs.

```text
ShipmentSourceLink
{
    ShipmentId
    SourceRef { Module, EntityType, Id }
    Purpose
    SourceLineRef?
    QuantitySnapshot?
    UomSnapshot?
    LabelSnapshot?
}
```

---

# 48. Multiple Source Links

A Shipment may consolidate compatible source lines/orders only if policy permits and commercial/customer/address/legal constraints are satisfied. Never consolidate merely for technical convenience.

---

# 49. Origin Snapshot

Shipment origin is immutable evidence after booking: name, contact, phone, address components, geo if supplied, country and operational instructions. It can reference a Warehouse/Party for navigation, but live master edits never rewrite booked history.

---

# 50. Destination Snapshot

Same rule as origin. Destination is a transaction snapshot at booking. Later Directory address corrections affect future shipments, not this one unless an explicit amendment/rebook workflow succeeds.

---

# 51. Address Roles

Shipping uses operational origin/destination snapshots. Directory remains identity owner; Warehouse/Inventory may provide origin address; Sales/Purchasing may provide chosen ship-to/from context.

---

# 52. Address Validation State

```text
NotChecked
Valid
ValidWithSuggestion
Ambiguous
Undeliverable
ProviderUnavailable
ManualConfirmed
```

Validation outcome is evidence; it does not silently edit Directory.

---

# 53. Address Correction UX

When carrier/provider suggests an address, show original vs suggested. User can apply only to this Shipment or separately request master-data correction with proper permission.

---

# 54. Saudi National Address Provider Seam

For Saudi Arabia, Country/Address provider integration may validate/search National Address, Short Address, regions/cities/districts/geocode. Shipping Core calls a versioned provider; no `if country == SA` legal/format logic is embedded in the domain.

---

# 55. Egypt Address Practicality

Support optional district/area, landmark, phone, alternate phone, building/floor/apartment and geolocation through canonical address/extension contracts. Do not make postal code or geolocation globally mandatory.

---

# 56. Package Aggregate Boundary

Warehouse packing and transport packages are related but distinct. Inventory/WMS may provide packed-unit evidence; Shipping creates `ShipmentPackage` transport representation. If Inventory is absent, packages can be entered manually.

---

# 57. ShipmentPackage Concept

```text
ShipmentPackage
{
    Id
    ShipmentId
    Sequence
    PackageTypeRef?
    Weight
    Dimensions?
    DeclaredValue?
    ContentsDescription?
    ExternalPieceRef?
    TrackingNumber?
    Status
}
```

---

# 58. Multi-Package Shipment

One Shipment may contain many packages/pieces. Provider adapter decides whether one master AWB plus piece IDs, one tracking per package or another provider structure applies.

---

# 59. Package Content Snapshot

Store only the transport-relevant content snapshot needed for trace/customs/POD/reference. Canonical product and actual stock movement remain outside Shipping.

---

# 60. Weight

Use explicit quantity value objects and unit. Avoid double precision. Package and shipment totals must be deterministic and explainable.

---

# 61. Dimensions

Length/width/height have explicit units. Missing dimensions are permitted only when the selected service/provider allows them; do not fabricate zeros for rating.

---

# 62. Actual Weight vs Chargeable Weight

Preserve actual entered/measured weight separately from provider-calculated chargeable/volumetric weight. Different carriers/contracts can use different divisors/rules, so no universal divisor belongs in Shipping Core.

---

# 63. Dimensional Weight Evidence

Rate/charge evidence may include provider chargeable weight, divisor/rule label if exposed and package-level computed details. This supports later freight variance investigation.

---

# 64. Package Type

Reusable package types such as Box Small, Satchel, Pallet may store dimensions/tare/defaults, but provider-specific packaging codes are mapped per adapter/account.

---

# 65. Packing Does Not Transfer Stock

Creating or editing a ShipmentPackage never issues or receives inventory. Inventory movement happens only through Inventory contracts.

---

# 66. Rate Request

`RateRequest` snapshots the rating inputs used to query one or more eligible carrier accounts/services.

---

# 67. Rate Request Concept

```text
RateRequest
{
    Id
    ShipmentId?
    OriginSnapshot
    DestinationSnapshot
    Packages[]
    ShipDate
    ServiceRequirements
    CODInstruction?
    DeclaredValue?
    CurrencyPreference?
    RequestedAt
}
```

---

# 68. Serviceability Before Rating

Where provider capabilities support it, validate origin/destination/service restrictions before or together with rates. An ineligible service is not shown as a valid quote.

---

# 69. Rate Quote

`RateQuote` is immutable provider evidence for a specific request and timestamp. It is not the final carrier invoice.

---

# 70. Rate Quote Concept

```text
RateQuote
{
    Id
    RateRequestId
    CarrierAccountId
    ServiceRef
    ProviderRateRef?
    Currency
    BaseCharge?
    Surcharges[]
    TaxEvidence?
    TotalEstimatedCharge
    EstimatedDeliveryAt?
    TransitDays?
    ChargeableWeight?
    QuotedAt
    ExpiresAt?
    RawEvidenceRef?
}
```

---

# 71. Rate Estimate vs Rate Quote

If a provider exposes estimate-only endpoints that may omit fuel/insurance/customs/other charges, mark the evidence as `Estimate` and display that limitation. Do not present it as a guaranteed carrier charge.

---

# 72. Rate Expiry

At booking, revalidate/refresh quote when provider requires or when expired. If price changes materially, policy decides whether to auto-accept within tolerance or require user review.

---

# 73. Rate Shopping

Return eligible quotes with cost, service, delivery estimate, provider/carrier, restrictions and warnings. UI can sort by Recommended, Cheapest or Fastest but must show what is being optimized.

---

# 74. Carrier Selection Policy

Deterministic policy may consider serviceability, destination, package weight/dimensions, COD, declared value, requested delivery promise, cutoff, cost, transit estimate, carrier performance, return support and tenant preferences.

---

# 75. Carrier Selection Is Explainable

Store or compute reasons such as `CheapestEligible`, `MeetsPromise`, `PreferredCarrier`, `CODRequired`, `ManualOverride`. Avoid opaque black-box selection.

---

# 76. Manual Carrier Override

Authorized user may select another eligible carrier/service. High-risk/expensive override can require reason and Approval capability. Never silently ignore serviceability failure.

---

# 77. Carrier Performance Input

Past delivery KPIs can influence advanced scoring later, but V1 selection remains deterministic/configurable. AI may recommend, never finalize sensitive carrier/financial decisions without policy.

---

# 78. Shipping Policy

`ShippingPolicy` controls defaults and progressive complexity per Legal Entity/Branch/context: default account/service, rating behavior, COD rules, label format, pickup behavior, allowed internal/manual shipping and exception thresholds.

---

# 79. Small Company Shipping Policy

Simple defaults:

```text
Default Ship-From
Default Carrier Account
Default Service
Default Label Format
COD Enabled?
```

Advanced fields stay hidden unless required.

---

# 80. Booking

Booking is the external mutation that asks a carrier/provider to create the transport shipment/AWB/label arrangement. It is a high-risk idempotent/reconcilable action.

---

# 81. ShipmentBooking Concept

```text
ShipmentBooking
{
    Id
    ShipmentId
    AttemptNo
    ProviderRequestKey
    RequestedRateQuoteId?
    RequestedAt
    State
    ProviderShipmentRef?
    MasterTrackingNumber?
    ConfirmedChargeEstimate?
    ConfirmedAt?
    FailureCode?
    UnknownOutcomeSince?
}
```

---

# 82. Booking States

```text
NotRequested
BookingPending
Booked
BookingFailedRetryable
BookingFailedTerminal
TimeoutUnknown
CancellationPending
Cancelled
```

Do not overload Shipment execution state with provider-request state.

---

# 83. External Mutation Idempotency

Use provider-supported idempotency/client reference wherever available. TODIR also protects locally with operation idempotency and `ProviderRequestKey`.

---

# 84. TimeoutUnknown

If request may have succeeded remotely but response is lost, never blindly issue another booking. Enter `TimeoutUnknown`, query/reconcile by client/provider reference and only retry when absence is proven or provider contract guarantees idempotency.

---

# 85. Duplicate AWB Prevention

The system must be able to prove that repeated UI clicks, job retries, HTTP timeouts, worker redelivery and failover cannot create two unintended carrier bookings for the same booking purpose.

---

# 86. Same Idempotency Key Different Payload

Conflict. Never reinterpret an old booking key for changed destination/package/COD/service data.

---

# 87. Booking Evidence

Persist request fingerprint, provider refs, response evidence, selected rate/service and generated artifacts references. Secrets and excessive PII are redacted from logs/evidence.

---

# 88. Booking Amendment

Provider adapter advertises whether address/package/COD/service can be modified after booking. If unsupported, use explicit cancel/void + rebook workflow when allowed; never edit local state only.

---

# 89. Booking Cancellation

Cancellation semantics depend on stage/provider. Before handover a carrier booking/label may be voidable; after handover the correct action may be intercept/RTO request rather than cancellation.

---

# 90. Local Cancellation vs Provider Cancellation

A local request is not considered provider-cancelled until carrier confirms or the policy/evidence establishes cancellation. Keep `CancellationPending`/failure visible.

---

# 91. Label

A ShippingLabel is an artifact generated or registered for a booking/package. It may be PDF, PNG or ZPL and is stored through Platform Files.

---

# 92. ShippingLabel Concept

```text
ShippingLabel
{
    Id
    ShipmentId
    ShipmentPackageId?
    BookingId
    ProviderLabelRef?
    Format
    Layout?
    FileRef
    CreatedAt
    VoidedAt?
    SupersededBy?
}
```

---

# 93. Label Lifecycle

```text
Created
Printed
Reprinted
Voided
Superseded
```

Reprint does not create a second Shipment. Void does not imply provider cancellation unless explicitly confirmed.

---

# 94. Label Formats

Support PDF and ZPL first; PNG optional where provider/operations require. Printing configuration belongs to workstation/platform print integration, not carrier business truth.

---

# 95. Bulk Label Creation

Bulk shipping is first-class. Process large batches in chunks with per-shipment results. `197 success / 3 failed` is valid; do not roll back 197 remote bookings because three independent carrier calls failed.

---

# 96. Bulk Booking Safety

Each Shipment booking keeps its own idempotency/unknown-outcome state. Batch job is orchestration, never the only source of truth.

---

# 97. Pickup Request

Carrier pickup is a separate business object because shipments may be booked before collection and many shipments can share one pickup.

---

# 98. PickupRequest Concept

```text
PickupRequest
{
    Id
    CarrierAccountId
    OriginSnapshot
    ReadyFrom
    ReadyUntil
    PackageCount
    TotalWeight?
    ProviderPickupRef?
    State
    Instructions?
}
```

---

# 99. Pickup States

```text
Draft
Requested
Confirmed
Updated
Cancelled
Completed
Failed
```

---

# 100. Manifest

`CarrierManifest` groups booked labels/shipments for carrier handover/closeout where the provider/service requires it. It is not merely an attached PDF.

---

# 101. CarrierManifest Concept

```text
CarrierManifest
{
    Id
    CarrierAccountId
    OriginRef?
    ShipDate
    State
    ShipmentRefs[]
    ProviderManifestRef?
    DocumentFileRef?
    CreatedAt
}
```

---

# 102. Manifest Membership Integrity

A Shipment/label cannot be silently attached to incompatible carrier/account/date manifest. Manifest changes after closeout require provider-supported correction or new manifest, with audit.

---

# 103. Handover

Carrier handover is a material transport milestone distinct from label generation and pickup scheduling. Capture handover time, manifest/pickup reference, carrier scan/evidence and actor where available.

---

# 104. Execution States

```text
Preparing
ReadyForCarrier
AwaitingPickup
PickupScheduled
HandedOver
InTransit
AtDestinationHub
OutForDelivery
Delivered
DeliveryException
ReturningToOrigin
ReturnedToOrigin
Lost
Closed
```

Canonical state is intentionally smaller than every provider code.

---

# 105. State Monotonicity Is Contextual

Tracking generally should not move backward due to stale events, but valid exception/recovery transitions exist. Implement a state transition policy using event occurred-time/provider sequence/evidence, not simple ordinal integers.

---

# 106. Tracking Event

Tracking is an append-only event timeline plus current-state projection.

---

# 107. TrackingEvent Concept

```text
TrackingEvent
{
    Id
    ShipmentId
    PackageId?
    ProviderEventId?
    ProviderSequence?
    CanonicalEventType
    ProviderStatusCode?
    ProviderStatusText?
    LocationSnapshot?
    OccurredAt
    ReceivedAt
    EvidenceRef?
}
```

---

# 108. Tracking Event Dedupe

Prefer provider event ID. Otherwise use a stable fingerprint of connection/shipment/piece/status/location/occurred-time with conservative collision handling. Duplicate delivery webhook must not repeat downstream action.

---

# 109. Out-of-Order Tracking

Persist every authentic event but update current projection only if transition policy accepts it. A late `InTransit` event must not overwrite an already-proven `Delivered` state.

---

# 110. Webhook + Polling

Webhook provides low latency; scheduled polling/reconciliation protects against missed callbacks, provider outages and subscription misconfiguration. Both paths feed the same idempotent normalization pipeline.

---

# 111. Tracking Reconciliation

Periodically identify active shipments whose last event is stale or whose provider status differs from local projection. Reconciliation records findings/actions; it never performs direct cross-domain correction.

---

# 112. Tracking Staleness

Policy can flag `No carrier event for X hours/days` by service/state. Stale tracking is an operational alert, not automatic declaration of loss.

---

# 113. Estimated Delivery Dates

Keep separate:

```text
CustomerPromisedDeliveryAt
CarrierEstimatedDeliveryAt
LatestObservedETA
ActualDeliveredAt
```

This enables promise-vs-carrier-vs-actual analytics.

---

# 114. Delivery Attempt

A failed or successful last-mile attempt is first-class evidence, not just a string status.

---

# 115. DeliveryAttempt Concept

```text
DeliveryAttempt
{
    Id
    ShipmentId
    AttemptNo
    AttemptedAt
    Outcome
    CanonicalFailureReason?
    ProviderReasonCode?
    ProviderReasonText?
    NextAttemptAt?
    EvidenceRef?
}
```

---

# 116. Delivery Failure Reasons

Initial normalized reasons can include `CustomerUnavailable`, `PhoneUnreachable`, `WrongOrIncompleteAddress`, `CustomerRequestedReschedule`, `Refused`, `CODUnavailable`, `OutsideServiceArea`, `CarrierCapacity`, `WeatherOperational`, `Damaged`, `Other`. Preserve raw provider reason.

---

# 117. NDR Workflow

For e-commerce operations, non-delivery may need human action: contact customer, correct shipment-only address, reschedule/re-attempt or authorize RTO. Model a task/exception workflow without turning Shipping into Helpdesk.

---

# 118. Shipment Exception

`ShipmentException` is typed operational work/evidence for issues such as delay, address problem, damage, suspected loss, provider mismatch, COD issue, booking ambiguity or stuck return.

---

# 119. Exception Severity

Severity reflects transport/business impact; priority reflects work ordering. Do not conflate them. High-value/VIP/COD/temperature-sensitive shipments may escalate differently.

---

# 120. Exception Ownership

Shipping owns transport resolution steps. If commercial refund/replacement, quality investigation or support communication is required, request/link the owning module rather than implement it inside Shipping.

---

# 121. Proof Of Delivery

POD records provider/internal-delivery evidence that transport was completed. Possible evidence: recipient name, signature, OTP confirmation, image, geolocation, delivered timestamp and carrier POD document.

---

# 122. ProofOfDelivery Concept

```text
ProofOfDelivery
{
    Id
    ShipmentId
    DeliveredAt
    RecipientNameMasked?
    DeliveryMethod?
    SignatureFileRef?
    PhotoFileRef?
    ProviderPodRef?
    GeoPoint?
    EvidenceSource
}
```

---

# 123. POD Privacy

POD can contain personal data/photos/signatures. Apply least privilege, retention, tenant isolation, secure file access and masking. Never expose POD publicly via predictable IDs.

---

# 124. Delivered != Commercial Acceptance

POD proves a transport event, not that goods are defect-free/correct or that the customer waives return rights. Post-delivery product disputes belong to Support/Sales/Quality flows.

---

# 125. RTO Decision

RTO can be provider-observed or explicitly requested. Preserve reason and request/confirmation state. Do not automatically cancel/return commercial documents.

---

# 126. RTO Inventory Consequence

When goods physically return, Inventory performs receipt/inspection/hold/restock according to its policy. Shipping's `ReturnedToOrigin` status is evidence and trigger context, not stock truth.

---

# 127. Return Shipment

A reverse Shipment may be created from Sales Return Authorization, Purchase Return or other approved source. It has its own booking/tracking lifecycle and can link to the original outbound Shipment.

---

# 128. ReturnShipmentLink

```text
ReturnShipmentLink
{
    OriginalShipmentId?
    ReverseShipmentId
    AuthorizationSourceRef?
    ReasonSnapshot
}
```

---

# 129. Return Label

Where provider supports it, create return label directly or from original label/shipment. Otherwise book a normal reverse shipment. Capability model hides provider differences.

---

# 130. Return Pickup

Reverse pickup scheduling follows the same PickupRequest principle but origin/destination swap according to authorized return context.

---

# 131. No Return Without Authorization When Required

For customer commercial returns, default policy requires a Sales return authorization/ref. RTO is the explicit exception because it is a failed outbound transport, not a post-sale customer return.

---

# 132. COD Instruction

COD is an instruction sent to the carrier, not money ownership.

---

# 133. CODInstruction Concept

```text
CODInstruction
{
    ShipmentId
    Amount
    Currency
    Reference
    CollectionPolicy?
}
```

---

# 134. COD Observation

```text
CODObservation
{
    ShipmentId
    State
    ReportedAmount?
    Currency?
    ReportedAt
    ProviderReference?
    EvidenceRef?
}
```

Possible states: `Requested`, `CollectedReported`, `NotCollectedReported`, `RemittedReported`, `AdjustedReported`.

---

# 135. COD Amount Change

After booking, changing COD may be unsupported or financially risky. Adapter capability and policy decide amend vs cancel/rebook. Any change is audited and cannot silently diverge from Sales/Invoice amount.

---

# 136. COD Mismatch

If carrier reports amount different from instruction, create a reconciliation/exception fact. Do not auto-write the difference to accounting or customer balance.

---

# 137. Carrier Remittance Evidence

Store carrier/aggregator statement evidence that links collections, shipping fees, returns/adjustments and net remittance. Payments/Banking own actual receipt; Accounting owns posting.

---

# 138. Freight Charge Estimate

Preserve expected transport cost from selected quote/booking so operations can compare with later provider statements/vendor bills.

---

# 139. Carrier Charge Evidence

```text
CarrierChargeEvidence
{
    Id
    ShipmentId?
    CarrierAccountId
    ExternalStatementRef?
    ChargeType
    Amount
    Currency
    TaxEvidence?
    Period?
    ReportedAt
    RawEvidenceRef?
}
```

---

# 140. Charge Types

Examples include base freight, fuel surcharge, remote area, COD fee, address correction, overweight/remeasurement, return/RTO, insurance, storage, customs handling and other provider adjustments. Keep provider raw code + normalized meaning.

---

# 141. Freight Reconciliation

Shipping can match selected/estimated charges to provider charge evidence and surface variances. Vendor Bill creation/posting remains Purchasing/Invoicing/Accounting depending TODIR policy.

---

# 142. Freight Variance

Calculate estimated vs observed amount/currency and typed variance reason where known. Tolerance policy can classify `WithinTolerance`, `ReviewRequired`, `Disputed` without auto-posting GL.

---

# 143. Carrier Vendor Bill Boundary

Carrier invoice/vendor bill is Invoicing/Purchasing financial truth. Shipping supplies shipment/charge matching evidence and may request/link a bill workflow.

---

# 144. Landed Cost Boundary

Inbound freight can later feed Inventory `ValuationAdjustment`/landed-cost allocation through typed facts. Shipping does not allocate inventory value itself.

---

# 145. International Shipping Scope

Architecture supports declared value, contents, origin country, HS/classification refs, Incoterm snapshot, duties payer, customs documents and carrier paperless trade payloads. V1 implementation depth is provider/customer driven.

---

# 146. Customs Document Boundary

Shipping assembles carrier transport/customs payload from authorized sources. Legal invoice remains Invoicing; product classification belongs Catalog/Compliance; trade/legal rules belong Country/Compliance providers.

---

# 147. Commercial Invoice Upload Boundary

If carrier API requires commercial invoice data/image, Shipping transmits a snapshot/reference supplied by Invoicing/Compliance. It does not create a second legal invoice.

---

# 148. Declared Value

Declared value for transport/insurance is explicit and can differ from selling price. Source/policy determines it; store currency and evidence, never infer silently.

---

# 149. Insurance

Carrier insurance/declared-value service is a transport option/evidence. Insurance accounting/claims may later have separate domain treatment.

---

# 150. Dangerous Goods

Design capability seam only. Dangerous-goods rules are high-risk regulatory content and must be implemented with specialist/legal/provider validation, not generic free-text flags pretending to be compliance.

---

# 151. Cold Chain

Carrier service capability can mark temperature-controlled transport. V1 does not build sensor telemetry/temperature chain-of-custody unless a validated sector requires it.

---

# 152. Heavy and Bulky

Provider capability/service can support heavy/bulky shipping. Core packages retain dimensions/weight; advanced load/freight planning is later.

---

# 153. Same Day

Same-day service is a CarrierService/capability with cutoff/serviceability/ETA, not a separate shipping engine.

---

# 154. Own Fleet Boundary

Shipping may dispatch internal deliveries lightly, but Vehicle master/maintenance, fuel, telematics and driver HR/payroll belong Fleet/Maintenance/HR capabilities.

---

# 155. DriverRef

If internal delivery uses an employee/contractor driver, Shipping stores an opaque DriverRef/identity snapshot needed for execution. It does not duplicate employee master.

---

# 156. VehicleRef

Same for vehicle. Shipping can store assignment/snapshot; vehicle lifecycle remains owning domain.

---

# 157. Internal Delivery States

Internal/manual delivery can reuse Shipping execution states and POD/attempt model, even when no external CarrierConnection exists.

---

# 158. Route Planning — Build Later

Future capability can plan multi-stop routes/segments based on origins, destinations, capacity, time windows and carrier/fleet. V1 keeps one shipment transport movement and does not build a route optimizer.

---

# 159. Load Building — Build Later

Future Load groups shipments into truck/container capacity. Do not add a fake Load aggregate to V1 merely because enterprise TMS products have it.

---

# 160. Freight Unit — Build Later

If enterprise TMS demand appears, introduce FreightUnit/TransportationRequest as a planning object separate from Shipment execution. Preserve source links now so this can be added without rewriting Sales/Inventory ownership.

---

# 161. Freight Order — Build Later

Future FreightOrder/Booking can represent road/ocean/air contracted transport, legs and carrier commitments. Parcel booking remains the V1 center.

---

# 162. Tendering — Build Later

Future enterprise flow may issue transport RFQs to carriers, collect quotations and award based on price/service. Do not build carrier tendering in V1.

---

# 163. Multi-Modal — Build Later

Air/Ocean/Rail/road leg orchestration is a future advanced TMS concern. Keep `Mode/Method` extensible, but avoid premature objects/screens.

---

# 164. Dock Appointment — Build Later

Dock/yard appointment scheduling can be added when warehouse/transport operations require it. It is not a prerequisite for courier/parcel shipping.

---

# 165. Carrier Claim — Design Now / Build Later

Preserve evidence for loss, damage, late-delivery guarantee or COD discrepancy. A future CarrierClaim aggregate may manage claim amount/status/documents; V1 can track exception + evidence/ref.

---

# 166. Lost Shipment

`Lost` requires provider/operational evidence or authorized decision. Shipping records transport outcome; stock write-off, customer refund/replacement and accounting effects stay with owning modules.

---

# 167. Damaged Shipment

Damage can trigger Shipping exception, Quality investigation, Inventory disposition and Sales/Support action. Shipping does not auto-decide product quality or financial compensation.

---

# 168. Partial Loss/Damage

For multi-package/line evidence, record affected package/content snapshot when known. Do not assume whole Shipment failed if provider reports one piece damaged/lost.

---

# 169. Carrier Account Deactivation

Blocks new bookings/rating use. Historical Shipments keep carrier/account snapshot/references. Open shipments remain trackable/reconcilable until wind-down completes.

---

# 170. Connection Credential Expiry

Connection can become `ActionRequired`; new bookings may stop while existing shipment tracking can fall back to alternate provider endpoints/manual updates if policy permits. Never delete mappings/history.

---

# 171. Provider API Versioning

Store adapter/provider API version/certification information. Upgrade adapters behind stable Shipping contracts; version external payload translators and golden contract fixtures.

---

# 172. Provider Adapter SPI

Core depends on a provider-neutral port such as:

```text
IShippingProvider
  GetCapabilities()
  ValidateAddress()
  CheckServiceability()
  GetRates()
  CreateShipment()
  CancelShipment()
  UpdateShipment()? 
  CreateReturnShipment()? 
  CreatePickup()? 
  CreateManifest()? 
  GetTracking()
  GetProofOfDelivery()? 
  ReconcileByClientReference()
```

Exact interface may be split into capability-specific ports to avoid a god interface.

---

# 173. No God Shipping Provider Interface

Prefer small capability interfaces (`IRatingProvider`, `IBookingProvider`, `ITrackingProvider`, etc.) resolved by connection capability. This prevents every adapter implementing meaningless methods.

---

# 174. Provider DTO Isolation

Provider request/response DTOs remain in adapter projects. Domain/Application sees normalized contracts; raw provider enums never leak through public TODIR APIs.

---

# 175. Provider Error Normalization

Map errors into stable classes such as authentication, validation, unsupported service, rate limit, retryable provider outage, timeout unknown, terminal booking rejection and reconciliation-required. Preserve raw provider code separately.

---

# 176. Rate Limits

Provider scheduler respects account/endpoint limits. Partition queues by tenant/provider/connection/operation and apply backoff/jitter without starving unrelated carriers/tenants.

---

# 177. Provider Circuit Breaker

Short-lived circuit breaking may protect operations during provider outage, but state stays visible. Do not convert circuit-open into successful booking/tracking.

---

# 178. Provider Sandbox

Adapters should support official sandbox/test environments where available. Production credentials are never used in CI tests.

---

# 179. Provider Contract Tests

Each adapter maintains recorded/synthetic contract fixtures and sandbox smoke tests for rates, booking, labels, cancel, tracking and provider-specific edge cases. Sensitive real payloads are sanitized.

---

# 180. External API Is Outside Transaction

Never hold TODIR database transaction open while calling carrier. Use local intent/state + durable job/outbox/inbox and reconcile external outcomes.

---

# 181. Transactional Outbox

Local Shipping state change and outgoing internal integration event commit atomically with Outbox. Provider calls are executed after commit where appropriate.

---

# 182. Provider Inbox

Incoming webhook/callback is authenticated, persisted/deduped rapidly, acknowledged and processed asynchronously. Never execute long cross-module work before responding to provider.

---

# 183. Webhook Signature

Provider-specific authentication/signature/timestamp/replay-window verification happens before trusted event processing. Failed verification is security telemetry, not a business event.

---

# 184. Webhook Tenant Resolution

Resolve tenant/connection from trusted endpoint/secret/account mapping, never from an untrusted `tenant_id` in provider payload.

---

# 185. Replay Protection

Use provider event IDs/signature timestamp/nonces where available and durable Inbox dedupe. Replayed authentic webhooks cannot re-run booking/delivery/COD consequences.

---

# 186. Raw Payload Retention

Keep raw payload evidence only when operational/legal value justifies it, encrypted/access-controlled/retention-bounded. Prefer sanitized normalized evidence to indefinite PII-heavy blobs.

---

# 187. PII in Logs

Never log full phone/address/signature/photo/credentials. Structured logs use ShipmentId, connection, provider code, correlation/trace and masked identifiers.

---

# 188. Provider File Downloads

Labels/POD/customs documents fetched from provider are validated for content type/size, stored in Platform Files and never trusted as arbitrary executable content.

---

# 189. SSRF Protection

If provider payload includes document/image URLs, do not let workers fetch arbitrary URLs blindly. Use allowlisted provider hosts/signed connector flow or platform-safe fetch service.

---

# 190. Tenant Isolation

Every Shipping-owned table is tenant-scoped with RLS/FORCE RLS according to platform standards. Provider/webhook/background jobs establish trusted tenant context before DB access.

---

# 191. Legal Entity Isolation

Carrier accounts/policies/shipments are Legal Entity scoped where financial/commercial responsibility differs. Cross-entity transport requires explicit policy; one entity must not consume another private carrier account accidentally.

---

# 192. Branch Scope

Branches can influence origin/default account/pickup schedule/permissions while Legal Entity remains the primary financial responsibility boundary.

---

# 193. Optimistic Concurrency

Use row version/concurrency tokens for Shipment/policy/exception updates. Critical transitions validate latest booking/execution state.

---

# 194. Distributed Locking Is Not Default

Do not use global locks for every Shipment. Prefer unique constraints/idempotency/concurrency plus job partitioning. Use narrowly scoped advisory/distributed locks only for proven races such as one active booking mutation.

---

# 195. Unique Constraints

Examples:

```text
(tenant_id, shipment_number)
(tenant_id, provider_connection_id, provider_shipment_ref) when present
(tenant_id, provider_connection_id, provider_event_id) when present
(tenant_id, provider_request_key)
```

Add partial/conditional uniqueness as PostgreSQL semantics require.

---

# 196. Indexes — Shipment Work Queues

Index tenant/entity + execution/booking state + requested ship date/updated time for operational queues. Avoid giant unbounded scans.

---

# 197. Indexes — Tracking

Index shipment + occurred_at, provider connection + external refs, and stale active shipments. Keep provider raw JSON out of hot indexes.

---

# 198. Indexes — Charges

Index carrier account/statement ref/shipment/currency/date for reconciliation workbench. Financial evidence queries remain paginated.

---

# 199. Database Tables — Core

```text
carrier_profile
carrier_account
carrier_service
carrier_connection
shipping_policy
carrier_selection_policy
shipment
shipment_source_link
shipment_package
shipment_package_content_snapshot
```

---

# 200. Database Tables — Rating/Booking

```text
rate_request
rate_quote
shipment_booking
shipping_label
pickup_request
pickup_shipment_link
carrier_manifest
carrier_manifest_shipment
```

---

# 201. Database Tables — Execution

```text
tracking_event
delivery_attempt
shipment_exception
proof_of_delivery
return_shipment_link
cod_instruction
cod_observation
```

---

# 202. Database Tables — Financial Evidence/Integration

```text
freight_charge_estimate
carrier_charge_evidence
carrier_external_reference
carrier_sync_checkpoint
provider_inbox_event
shipping_reconciliation_run
shipping_reconciliation_issue
```

---

# 203. No Cross-Module Foreign Keys

Business source refs to Sales/Inventory/Purchasing/Channels/Payments/etc. are opaque typed refs/contracts, not cross-schema ORM relationships. Foundation IDs may use approved stable FKs only under architecture rules.

---

# 204. Audit — Must Capture

Create/cancel/rebook shipment; destination/address override; carrier/service override; COD change; rate selection; booking unknown-outcome resolution; manual tracking override; RTO/return request; POD manual entry; charge reconciliation/dispute; credential/account config changes; high-risk permission actions.

---

# 205. Business History != Audit

Tracking events, bookings, attempts, POD and return links are domain history. Generic audit additionally records who changed configuration/overrode decisions. Keep both.

---

# 206. API Base

Recommended admin/operations base:

```text
/api/v1/shipping
```

Internal modular-monolith hot paths use published in-process contracts, not HTTP to self.

---

# 207. Shipment APIs

```text
GET    /shipments
POST   /shipments
GET    /shipments/{id}
POST   /shipments/{id}/packages
POST   /shipments/{id}/rate
POST   /shipments/{id}/select-rate
POST   /shipments/{id}/book
POST   /shipments/{id}/cancel-booking
POST   /shipments/{id}/request-rto
POST   /shipments/{id}/close
```

---

# 208. Label APIs

```text
GET  /shipments/{id}/labels
POST /shipments/{id}/labels/reprint
POST /labels/{id}/void
```

Reprint/void semantics call domain commands, not raw file operations.

---

# 209. Pickup / Manifest APIs

```text
POST /pickups
PATCH /pickups/{id}
POST /pickups/{id}/cancel
POST /manifests
GET  /manifests/{id}
```

---

# 210. Tracking APIs

```text
GET  /shipments/{id}/tracking
POST /shipments/{id}/refresh-tracking
GET  /shipments/{id}/pod
```

Public customer tracking uses a separate safe tokenized surface later.

---

# 211. Carrier Configuration APIs

```text
GET/POST /carriers
GET/POST /carrier-accounts
POST     /carrier-connections/{id}/verify
GET      /carrier-connections/{id}/capabilities
GET      /carrier-services
```

---

# 212. Reconciliation APIs

```text
POST /reconciliation/runs
GET  /reconciliation/issues
POST /reconciliation/issues/{id}/resolve
GET  /freight-charges
```

---

# 213. Public Tracking Boundary

Portal/public tracking returns a minimal projection using opaque/signed token with throttling/expiry as appropriate. Never expose tenant internal IDs, carrier credentials, cost, or private POD by default.

---

# 214. Commands

```text
CreateShipment
AddShipmentPackage
RequestRates
SelectRate
BookShipment
ResolveUnknownBooking
CancelShipmentBooking
SchedulePickup
CreateCarrierManifest
RefreshTracking
RecordManualTrackingEvent
RecordDeliveryAttempt
AttachProofOfDelivery
RequestReturnToOrigin
CreateReturnShipment
RecordCODObservation
ImportCarrierChargeEvidence
ResolveShippingReconciliationIssue
```

---

# 215. Queries

```text
GetShipment
ListShipmentWorkQueue
GetShipmentTimeline
GetRates
GetLabels
GetTracking
GetProofOfDelivery
GetCarrierAccounts
GetCarrierServices
GetCarrierHealth
GetShippingExceptions
GetFreightVariance
GetCarrierPerformance
```

---

# 216. Domain Events

Meaningful internal events may include:

```text
ShipmentCreated
ShipmentBooked
ShipmentBookingUnknown
ShipmentBookingCancelled
ShipmentHandedOver
ShipmentOutForDelivery
ShipmentDelivered
ShipmentDeliveryFailed
ShipmentRtoRequested
ShipmentReturnedToOrigin
ReturnShipmentCreated
CODCollectionReported
CarrierChargeEvidenceRecorded
```

---

# 217. Integration Events

Publish selected versioned events after commit, e.g. `shipping.shipment-delivered.v1`, `shipping.shipment-returned-to-origin.v1`, `shipping.delivery-exception.v1`. Avoid event per every harmless tracking scan unless consumers truly need it.

---

# 218. No Event Storm From Tracking

Persist detailed TrackingEvent history locally. Publish coarse business milestones/exceptions externally; analytics/read models can consume batched/projection data without flooding RabbitMQ with every scan.

---

# 219. Inventory Contract Boundary

Shipping can receive packed/source snapshots and emit transport milestones. Inventory alone executes stock issue/receipt/return/transfer. No Shipping `UPDATE inventory.*` ever.

---

# 220. Sales Contract Boundary

Sales can request/attach Shipment and consume delivered/RTO status projections. Shipping cannot cancel or amend Sales Order. Commercial promise remains Sales-owned.

---

# 221. Purchasing Contract Boundary

Purchasing can request supplier pickup/inbound/return transport; Shipping never confirms PO receipt or vendor bill.

---

# 222. Channels Contract Boundary

Channels external fulfillment observations do not automatically create duplicate Shipping records. Create TODIR Shipment only when TODIR manages/needs unified transport execution under explicit source/provider mode.

---

# 223. Payments Contract Boundary

Shipping may send COD/remittance evidence. Payments decides/records actual receipt/refund. Shipping cannot mark invoice paid or create cash/bank movement.

---

# 224. Banking Boundary

Carrier remittance to bank is reconciled by Payments/Banking/BankRec. Shipping links statement/evidence refs where useful.

---

# 225. Invoicing Boundary

Shipping can provide freight/transport source references and carrier vendor bill matching evidence. It neither issues customer invoices nor vendor bills.

---

# 226. Accounting Boundary

Shipping never chooses GL accounts or posts journals. Accounting maps normalized charge meanings/evidence to financial treatment.

---

# 227. Compliance Boundary

International/customs/country-specific shipping requirements come from versioned provider/country capability. Shipping Core avoids legal constants and false global assumptions.

---

# 228. Support Boundary

Support can answer “Where is my order?” using Shipping read contract and request escalation/return handoff. It cannot manually mutate carrier states.

---

# 229. Portal Boundary

Portal later displays tracking/ETA/POD availability/return logistics permitted by policy. Portal remains presentation/access layer, not Shipping truth owner.

---

# 230. Notifications Boundary

Shipping emits milestones; Platform Notifications sends email/SMS/WhatsApp/push. Shipping never embeds provider-specific messaging credentials/templates.

---

# 231. Reporting Boundary

Shipping owns operational transport reports/read models. Cross-module profitability/customer analytics use Reporting projections, not SQL joins across module tables.

---

# 232. Operational Work Queue

Primary Shipping UX is a work queue, not generic CRUD:

```text
Ready to Ship
Need Carrier
Rating/Booking Problem
Labels Ready
Awaiting Pickup
In Transit
Delivery Exceptions
RTO / Returns
COD Attention
Freight Variance
```

---

# 233. Shipment Workspace

Header: shipment number, source/customer/destination summary, carrier/service, booking/execution state, ETA. Tabs/sections: Packages, Labels, Tracking, Attempts/Exceptions, COD, Charges, Source Links, Files, Audit. Progressive disclosure hides irrelevant enterprise fields.

---

# 234. Ready-To-Ship UX

Warehouse/operator can select source, confirm package weight/dimensions, compare recommended rates and book. If defaults uniquely resolve, one-click `Create Label` is allowed with preview/error recovery.

---

# 235. Bulk Shipping UX

Select many ready shipments -> validate missing data -> assign/rate -> book in safe chunks -> generate labels -> print -> manifest. Results show exact successes/failures and never hide unknown provider outcomes.

---

# 236. Rate Comparison UX

Show carrier/service, total estimated charge, currency, ETA/transit, cutoff/warnings and recommendation reason. Do not bury surcharge/estimate limitations behind invisible tooltips.

---

# 237. Booking Error UX

Errors are actionable: invalid address, missing dimensions, unsupported COD, provider unavailable, credentials expired, service unavailable, timeout unknown. For unknown outcome, disable unsafe `Try again` and offer `Reconcile booking`.

---

# 238. Tracking Timeline UX

Human-readable canonical milestone plus carrier raw label/code details in expandable diagnostics. Show event time and received time when useful; avoid displaying raw JSON to normal users.

---

# 239. Exception Queue UX

Filter by exception type/severity/carrier/warehouse/age/customer promise breach. Actions are typed: contact customer through Support/notification handoff, correct shipment-only address, reschedule, request RTO, investigate carrier, link claim later.

---

# 240. POD UX

Authorized user sees delivery time and allowed POD evidence. Sensitive signature/photo/recipient data is masked/permissioned and not shown in list views.

---

# 241. COD UX

Display separately: requested amount, carrier collection observation, remittance evidence, actual payment/bank match projection when available. Never one ambiguous `COD Paid` badge.

---

# 242. Freight Variance UX

Show quoted/expected vs provider actual evidence, variance amount/%, normalized reasons and linked vendor bill/settlement when available. Support bulk review with tolerances.

---

# 243. Carrier Health Dashboard

Show connection/credential status, recent API errors, booking success, unknown outcomes, tracking staleness, rate latency, webhook age, backlog/rate-limit pressure and reconciliation issues per carrier/account.

---

# 244. Carrier Performance Dashboard

Operational KPIs can include on-time %, first-attempt delivery %, average transit, exception/RTO/damage/loss rates, pickup SLA, average freight cost, quote-vs-actual variance, COD remittance delay and service-level distribution.

---

# 245. KPI Provenance

Each KPI has explicit definitions/time basis/filter/source. `On-time delivery` compares agreed promise/selected carrier commitment to actual delivery according to documented policy; no vague dashboard number.

---

# 246. Performance Product Requirement

Warehouse shipping must remain responsive at high daily volume. External provider latency must not freeze lists/forms. Use async jobs, pagination, bulk operations, batching/coalescing and compact projections while preserving correctness.

---

# 247. Performance Targets — Interactive

Initial engineering targets under representative load: Shipment work queue/filter p95 <= 400 ms from local projections; Shipment detail p95 <= 500 ms excluding explicit external refresh; local commands <= 500 ms before external async work when possible. Provider SLA is measured separately.

---

# 248. Performance Targets — Rating

Multi-carrier rating is external-latency bound. Execute eligible provider calls concurrently within limits, stream/return partial results when product UX supports, and show provider timeouts explicitly rather than waiting indefinitely for the slowest carrier.

---

# 249. Performance Targets — Bulk

Bulk booking/labels process asynchronously in bounded chunks with progress. Design for thousands of shipments/day per tenant and bursty e-commerce peaks; benchmark higher volumes before claiming limits.

---

# 250. Performance — Tracking

Do not poll every delivered/closed shipment forever. Active-state adaptive polling, webhook preference, provider batch tracking where available and staleness priorities reduce cost/load.

---

# 251. Performance — Read Models

Work queues/KPIs use Shipping-owned read projections rather than joining tracking events/packages/charges on every request. Projections are rebuildable from owned canonical data.

---

# 252. Caching

Cache stable carrier services/capabilities/address reference metadata with explicit expiry/version. Never cache booking state or COD/payment truth in a way that overrides DB/provider evidence.

---

# 253. Observability Dimensions

Metrics/traces include tenant-safe dimensions: provider, carrier account alias/id, operation, result class, latency bucket, queue/backlog, retry count, unknown-outcome count, reconciliation age. Never use phone/address/tracking full value as metric label.

---

# 254. Distributed Tracing

Propagate trace/correlation/causation through source request -> Shipping command -> outbox/job -> provider HTTP -> webhook/inbox -> downstream event. Redact auth headers and PII.

---

# 255. Operational Alerts

Alert on sustained booking failure, credential expiry, webhook silence where expected, provider error spike, unknown booking outcomes, tracking reconciliation backlog, stale shipments, COD mismatch backlog and freight variance anomalies.

---

# 256. SLO Separation

Measure TODIR internal availability separately from carrier/provider availability. Dashboard should make clear when TODIR is healthy but external carrier is degraded.

---

# 257. Error Codes

Use stable application codes such as `shipping.address_invalid`, `shipping.no_service`, `shipping.booking_timeout_unknown`, `shipping.provider_rate_limited`, `shipping.cod_not_supported`, `shipping.stale_event`, `shipping.permission_denied`, `shipping.concurrency_conflict`.

---

# 258. Safe User Errors

User errors explain what can be fixed without leaking provider secrets/raw stack traces. Diagnostics link correlation ID/provider code for authorized support/admin users.

---

# 259. Permissions

Initial permission families:

```text
shipping.read
shipping.create
shipping.package.manage
shipping.rate
shipping.book
shipping.booking.cancel
shipping.booking.resolve_unknown
shipping.label.print
shipping.pickup.manage
shipping.manifest.manage
shipping.tracking.refresh
shipping.tracking.manual_update
shipping.exception.manage
shipping.rto.request
shipping.return.create
shipping.pod.view
shipping.pod.manage_manual
shipping.cod.view
shipping.cod.manage_evidence
shipping.freight_cost.view
shipping.freight_reconcile
shipping.carrier.manage
shipping.carrier_connection.manage
shipping.policy.manage
shipping.audit.view
```

---

# 260. Cost Visibility Permission

Warehouse workers may need label/tracking without seeing negotiated carrier cost/financial settlement. Keep `shipping.freight_cost.view` separate from operational booking permissions where product roles require.

---

# 261. POD Permission

POD/signature/photo access can be more sensitive than normal tracking. Separate read permission and signed file authorization.

---

# 262. Credential Management Permission

Carrier credentials/connection changes require strong permission and audit. Normal shipping operator never sees secret values.

---

# 263. Separation of Duties

Advanced tenant may require separate approval for high-cost manual carrier override, high-value COD changes, freight variance dispute/write-off decision or carrier connection credential changes. Approvals capability supplies decision path; Shipping owns state transitions.

---

# 264. Data Retention

Retain business shipment/tracking/charge evidence according to country/contract/tenant policy. POD/raw provider payloads can have stricter/shorter privacy retention than shipment commercial history.

---

# 265. Deactivation

Shipping module deactivation is wind-down, not delete: block new bookings, finish/cancel open jobs, preserve tracking/history, export if required and keep dependent read references resolvable.

---

# 266. Reactivation

Reuses retained data/accounts subject to re-verification of credentials/capabilities. Never recreate historical Shipments as new.

---

# 267. Go-Live Contract

Shipping declares effective-from, open-shipment transition and historical policy. Old delivered sales need no synthetic Shipment. Open deliveries may be explicitly imported/mapped if business needs active tracking.

---

# 268. Migration — Carrier Accounts

Import carrier profiles/accounts/service mappings and masked account references; credentials are entered securely, not imported from spreadsheets in plaintext.

---

# 269. Migration — Open Shipments

Support import of active external tracking/AWB with origin/destination snapshot, current canonical/raw state, packages if known, source ref and provider connection. Do not fabricate missing event history.

---

# 270. Migration — Historical Shipments

Optional. Import summary evidence only if reporting/support value justifies. Do not create fake booking/tracking events to make history look complete.

---

# 271. Migration Preview

Show create/link/conflict/unknown source/duplicate tracking/provider mismatch/invalid address/state mapping before commit. Large imports async with downloadable errors.

---

# 272. Reconciliation After Migration

For imported active shipments, poll provider/current status and mark differences for review. Never overwrite imported/source history silently.

---

# 273. Market Review — Odoo

Odoo 19 demonstrates the SME baseline: third-party carriers can verify service/address context, calculate costs, create shipment/labels, use tracking references and support multi-package labels. TODIR should match this simple flow while keeping transportation state independent from Inventory.

---

# 274. Market Review — ShipStation / ShipEngine

ShipStation APIs demonstrate specialist parcel primitives: rate shopping, labels, multi-package labels, batches, return labels, manifests and pickups. This validates TODIR's parcel-execution scope and the separation of rates, labels, return transport and manifests.

---

# 275. Market Review — OTO

OTO is strategically relevant to Arabic/MENA operations: multi-carrier API, rates, real-time tracking/webhooks, returns, shipping transactions/cost reconciliation, address validation and 450+ carrier integrations. TODIR should support an aggregator provider separately from the executing carrier.

---

# 276. Market Review — Dynamics 365 Transportation Management

Dynamics validates inbound/outbound transport, external providers or own fleet, fastest/least-cost rating, load building, multi-stop routing and freight reconciliation. TODIR uses these as future-safe seams but keeps V1 parcel/courier focused.

---

# 277. Market Review — SAP Transportation Management

SAP TM validates the advanced end-state: freight units/orders/bookings, carrier selection, transportation planning, tendering, charge calculation and regulations. These are explicit Build-Later domains, not reasons to burden TODIR V1.

---

# 278. Market Review — DHL MyDHL API

DHL MyDHL exposes account rates, multi-piece rating, shipment creation, labels, pickup create/update/cancel, tracking, address capability validation, proof of delivery, landed cost and customs invoice upload. This strongly validates the provider capability model rather than a one-method carrier connector.

---

# 279. Market Review — Saudi SPL National Address

SPL National Address API supports address verification/search using short address/geolocation and region/city/district lookups. TODIR should integrate through Country/Address provider contracts for Saudi shipping readiness.

---

# 280. Recommended Initial Provider Strategy

For Arabic-market launch, prefer:

```text
P1: Manual/Internal + OTO aggregator + Aramex direct foundation
P2: DHL + FedEx + SMSA/SPL according to first customers
P3: Bosta/Mylerz/J&T/iMile/local/specialist carriers based on demand
```

Do not build dozens of adapters without real customer demand; use the provider SPI and aggregator coverage to move faster.

---

# 281. Provider Strategy — Direct vs Aggregator

Use aggregators for broad carrier coverage and direct adapters where strategic account rates/features/reliability justify them. Same Shipment model works with both; `BookingProvider` and `ExecutingCarrier` keep evidence correct.

---

# 282. Provider Strategy — Manual Fallback

Manual shipping must remain operationally usable during unsupported carrier/provider outage. It can record external AWB/status/cost with audit, but must not pretend API-confirmed evidence exists.

---

# 283. Testing Strategy

Testing spans pure domain transitions, PostgreSQL integration/RLS/constraints, provider adapter contract tests, job/outbox/inbox idempotency, end-to-end business flows, security/privacy red-team tests, concurrency, performance and operational recovery drills.

---

# 284. Unit Test — Shipment state machine

Valid/invalid booking/execution/RTO/return transitions; no impossible backward mutation.

---

# 285. Unit Test — Rate selection

Eligibility, manual override, recommendation reason, expired quote behavior.

---

# 286. Unit Test — Multi-package totals

Weight/dimensions/declared values and no floating-point errors.

---

# 287. Unit Test — COD distinction

Instruction/observation/remittance evidence never changes Payment truth.

---

# 288. Unit Test — RTO vs Return

Failed outbound RTO remains distinct from authorized customer return.

---

# 289. Unit Test — Delivery attempt reasons

Provider raw reason maps to canonical reason without loss of raw evidence.

---

# 290. Unit Test — Stale tracking event

Old event persists but cannot incorrectly regress current state.

---

# 291. Unit Test — Address snapshot

Master address update does not alter booked shipment snapshot.

---

# 292. Unit Test — Rate vs actual charge

Variance calculated from immutable quote/evidence.

---

# 293. Unit Test — Carrier capability

Unsupported operations fail explicitly instead of fake success.

---

# 294. Integration Test — RLS

Tenant A cannot read/create/link CarrierAccount, Shipment, label/POD or provider event for Tenant B.

---

# 295. Integration Test — Booking idempotency

Repeated local command/key creates one booking intent and one confirmed external ref in fixture/sandbox.

---

# 296. Integration Test — Provider timeout unknown

Simulated response loss enters TimeoutUnknown; reconciliation finds remote booking; no second AWB.

---

# 297. Integration Test — Inbox dedupe

Same tracking webhook repeated 20 times produces one normalized business effect.

---

# 298. Integration Test — Out-of-order webhook

Delivered followed by stale InTransit does not regress projection.

---

# 299. Integration Test — Outbox

Shipment milestone + outbox commit atomically; broker outage later replays safely.

---

# 300. Integration Test — Package uniqueness

Cross-tenant/source package references rejected by DB/application rules.

---

# 301. Integration Test — Label files

Generated label saved to Platform Files; DB stores ref; authorization checked.

---

# 302. Integration Test — Freight evidence

Provider statement evidence imports idempotently and links without posting accounting.

---

# 303. Integration Test — Go-live import

Open shipment import + provider reconciliation preserves supplied history/current evidence.

---

# 304. Security Test — Invalid webhook signature

Reject before trusted Inbox processing; record security telemetry.

---

# 305. Security Test — Replay webhook

Old/replayed valid event cannot repeat domain effect.

---

# 306. Security Test — Tenant spoof in payload

Ignore untrusted tenant field; resolve tenant from trusted connection.

---

# 307. Security Test — Credential leakage

No secrets in logs, exception text, DTOs, exports or audit payload.

---

# 308. Security Test — POD cross-tenant

Signed file authorization prevents access using guessed ID/token.

---

# 309. Security Test — Public tracking enumeration

Opaque tokens/rate limits prevent sequential shipment discovery.

---

# 310. Security Test — SSRF document URL

Malicious provider URL cannot make worker access arbitrary internal network.

---

# 311. Security Test — Oversized webhook

Payload/body limits and safe parse reject abuse without worker crash.

---

# 312. Security Test — Unauthorized cost view

User who can print labels cannot automatically see carrier rates/actual costs.

---

# 313. Security Test — Manual state override

Ordinary user cannot forge Delivered/COD/POD states.

---

# 314. Performance Test — Shipment work queue

Benchmark 100k/1m shipment history with active-state queue indexes/projections.

---

# 315. Performance Test — Tracking events

Millions of tracking events with paginated timeline and current-state projection.

---

# 316. Performance Test — Bulk 1k labels

Chunked booking/label job progress, per-item recovery and bounded memory.

---

# 317. Performance Test — Rating fan-out

Concurrent multi-carrier rate calls respect timeouts/rate limits and partial results.

---

# 318. Performance Test — Webhook burst

Large delivery-status burst drains Inbox without duplicate effects or tenant starvation.

---

# 319. Performance Test — Polling recovery

Provider outage backlog resumes gradually without thundering herd.

---

# 320. Performance Test — COD/freight reconciliation

Large provider statement/charge import paginated and indexed.

---

# 321. Performance Test — Dashboard

Carrier KPI/read-model query remains bounded under long history.

---

# 322. Acceptance — Simple local carrier

Small company creates manual shipment, enters AWB, prints internal document, tracks manually and marks delivered with audit.

---

# 323. Acceptance — API label

Operator rates, selects carrier, books once, receives label/tracking and prints it.

---

# 324. Acceptance — Multi-package

One order ships in three packages with piece refs/labels and one coherent shipment timeline.

---

# 325. Acceptance — Partial fulfillment

One Sales Order produces multiple shipments over time without duplicate commercial fulfillment.

---

# 326. Acceptance — Dropship

Supplier origin ships direct to customer without fake TODIR inventory movements.

---

# 327. Acceptance — Purchase inbound

Carrier delivers supplier pickup to warehouse; Inventory receipt remains manual/verified owner action.

---

# 328. Acceptance — Warehouse transfer

Inventory in-transit transfer links to carrier shipment; transport delivery does not auto-close Inventory discrepancy.

---

# 329. Acceptance — Carrier timeout

Create booking times out after carrier creation; TODIR reconciles and recovers existing AWB without duplicate.

---

# 330. Acceptance — Booking rejection

Invalid address/service shows actionable failure; no false label/shipped state.

---

# 331. Acceptance — Pickup/manifest

Multiple booked shipments share pickup/manifest and handover evidence.

---

# 332. Acceptance — Tracking webhook

Carrier events normalize to timeline/current state with raw codes retained.

---

# 333. Acceptance — Out-of-order tracking

Stale event arrives after Delivered; timeline records it but state stays correct.

---

# 334. Acceptance — Failed attempt

Customer unavailable creates attempt/exception and optional next attempt without changing payment/stock.

---

# 335. Acceptance — RTO

Refused outbound becomes Returning/ReturnedToOrigin; Sales/Inventory consequences remain separate.

---

# 336. Acceptance — Customer return

Approved Sales return creates reverse shipment and warehouse later receives through Inventory.

---

# 337. Acceptance — POD

Delivered shipment stores protected POD evidence visible only to authorized users.

---

# 338. Acceptance — COD collected

Carrier reports COD collected; Finance still sees actual Payment/Bank receipt separately.

---

# 339. Acceptance — COD mismatch

Carrier reports wrong collected amount; reconciliation issue is visible and no auto-posting occurs.

---

# 340. Acceptance — Freight variance

Quoted 80, observed charge 123 with surcharges; workbench shows variance and evidence.

---

# 341. Acceptance — Customer shipping charge

Customer pays 40 while carrier costs 80; both values remain separate and explainable.

---

# 342. Acceptance — Carrier credentials expire

New booking blocked/actionable; historical shipment/tracking data remains available.

---

# 343. Acceptance — Provider outage

Queue remains usable; manual/alternate carrier path possible by policy; no fake success.

---

# 344. Acceptance — Bulk shipping

200 shipments produce per-item results and labels; three failures do not lose 197 successes.

---

# 345. Acceptance — Saudi address validation

Short/National Address provider validates shipment without hardcoded KSA domain logic.

---

# 346. Acceptance — International DHL-like flow

Shipment carries customs/document snapshot refs and provider creates international booking without Shipping owning invoice.

---

# 347. Red Team — Double click Create Label

Two concurrent requests must converge to one booking or safe conflict.

---

# 348. Red Team — Network cut after provider commit

Unknown outcome reconciles; no blind retry.

---

# 349. Red Team — Provider sends Delivered twice

Only one downstream delivered milestone effect.

---

# 350. Red Team — Provider sends RTO after Delivered

Conflict/exception according to provider semantics; no silent rewrite.

---

# 351. Red Team — Wrong tenant tracking number

Lookup cannot cross tenant/connection ownership.

---

# 352. Red Team — Address edited after booking

Booked snapshot remains unchanged; explicit amend/rebook required.

---

# 353. Red Team — User changes COD after handover

Blocked or provider-supported explicit workflow; never local-only.

---

# 354. Red Team — Manual Delivered without permission

Rejected and audited.

---

# 355. Red Team — Carrier charge in wrong currency

No arithmetic across currencies without explicit FinBase conversion context.

---

# 356. Red Team — Label URL points internal IP

Safe file-fetch policy blocks SSRF.

---

# 357. Red Team — One package missing from provider response

Booking enters reconciliation/error state; do not pretend complete.

---

# 358. Red Team — Carrier returns 200 with error body

Adapter validates semantic success, not HTTP code only.

---

# 359. Red Team — Shipment source cancelled after booking

Shipping does not silently delete; owning-domain cancellation handoff determines intercept/cancel/RTO action.

---

# 360. Red Team — Inventory disabled

Manual/dropship shipment still works without fake stock records.

---

# 361. Red Team — Payments disabled

COD instruction can be transported but no fake receipt is created.

---

# 362. Build Now — V1 Core

Build production-grade:

- Carrier/Account/Connection/Service + capability model.
- Manual/internal carrier baseline.
- Package + multi-piece model.
- Rate request/quotes + deterministic selection basics.
- Booking with idempotency/TimeoutUnknown reconciliation.
- Labels PDF/ZPL + bulk processing.
- Pickup + manifest.
- Tracking webhook/poll/reconciliation + normalized/raw states.
- Delivery attempts/exceptions + POD.
- RTO + reverse shipment/return label capability.
- COD instruction/observation + remittance evidence boundary.
- Freight estimate/charge evidence + variance workbench basics.
- Address validation provider seam including Saudi National Address capability.
- Security/RLS/privacy/audit/observability/runbooks.
- Initial aggregator/direct carrier adapters according to launch customers.

---

# 363. Design Now / Build Later — Advanced TMS

Do not build yet unless validated customer need:

- FreightUnit/FreightOrder/FreightBooking planning.
- LTL/FTL/air/ocean/rail orchestration.
- Multi-stop route optimization and scheduled routes.
- Load building / vehicle/container capacity.
- Carrier tendering/RFQ/quotation award.
- Dock/yard appointment management.
- Advanced own-fleet telematics/GPS/fuel.
- Cold-chain sensor telemetry.
- Dangerous goods compliance engine.
- Carrier claims automation.
- Full customs/trade compliance.
- Advanced freight invoice auto-reconciliation/posting automation.
- AI autonomous carrier optimization.

---

# 364. Do Not Build

Do not create: separate Product/Customer/Invoice/Payment/Stock truth in Shipping; carrier-specific columns all over core tables; arbitrary scripts for carrier rules; universal volumetric divisor; provider API calls inside DB transactions; one overloaded `shipment_status`; direct GL/account posting; raw credentials in business tables.

---

# 365. Implementation Phase SH00 — Governance & Architecture Contracts

Lock ownership, hard/optional dependency correction, module manifest, source refs, provider SPI, RLS and architecture tests.

---

# 366. SH00 — Governance & Architecture Contracts Exit Gate

Phase passes only when its required behavior is implemented, automated tests pass, architecture boundaries remain clean, operational failure/recovery is demonstrated, and unresolved product/legal/provider assumptions are explicitly documented.

---

# 367. Implementation Phase SH01 — Carrier Core

CarrierProfile/Account/Service/Connection + credentials refs, activation/deactivation and admin UX.

---

# 368. SH01 — Carrier Core Exit Gate

Phase passes only when its required behavior is implemented, automated tests pass, architecture boundaries remain clean, operational failure/recovery is demonstrated, and unresolved product/legal/provider assumptions are explicitly documented.

---

# 369. Implementation Phase SH02 — Shipment Core

Shipment/source links/address snapshots/package model/state dimensions/numbering/concurrency.

---

# 370. SH02 — Shipment Core Exit Gate

Phase passes only when its required behavior is implemented, automated tests pass, architecture boundaries remain clean, operational failure/recovery is demonstrated, and unresolved product/legal/provider assumptions are explicitly documented.

---

# 371. Implementation Phase SH03 — Provider Adapter SPI

Capability-specific provider ports, normalized errors, provider DTO isolation and contract fixtures.

---

# 372. SH03 — Provider Adapter SPI Exit Gate

Phase passes only when its required behavior is implemented, automated tests pass, architecture boundaries remain clean, operational failure/recovery is demonstrated, and unresolved product/legal/provider assumptions are explicitly documented.

---

# 373. Implementation Phase SH04 — Manual/Internal Shipping

Manual carrier/AWB/tracking/POD baseline so Shipping works before external adapters.

---

# 374. SH04 — Manual/Internal Shipping Exit Gate

Phase passes only when its required behavior is implemented, automated tests pass, architecture boundaries remain clean, operational failure/recovery is demonstrated, and unresolved product/legal/provider assumptions are explicitly documented.

---

# 375. Implementation Phase SH05 — Rating & Serviceability

Rate request/quotes, eligibility, selection policy, rate expiry/evidence and UX.

---

# 376. SH05 — Rating & Serviceability Exit Gate

Phase passes only when its required behavior is implemented, automated tests pass, architecture boundaries remain clean, operational failure/recovery is demonstrated, and unresolved product/legal/provider assumptions are explicitly documented.

---

# 377. Implementation Phase SH06 — Booking & Idempotency

Booking state machine, provider request keys, timeout-unknown recovery and cancellation.

---

# 378. SH06 — Booking & Idempotency Exit Gate

Phase passes only when its required behavior is implemented, automated tests pass, architecture boundaries remain clean, operational failure/recovery is demonstrated, and unresolved product/legal/provider assumptions are explicitly documented.

---

# 379. Implementation Phase SH07 — Labels & Printing

File-backed labels, PDF/ZPL, multi-piece, reprint/void, bulk label jobs.

---

# 380. SH07 — Labels & Printing Exit Gate

Phase passes only when its required behavior is implemented, automated tests pass, architecture boundaries remain clean, operational failure/recovery is demonstrated, and unresolved product/legal/provider assumptions are explicitly documented.

---

# 381. Implementation Phase SH08 — Pickup & Manifest

Pickup lifecycle, manifest grouping/documents, handover evidence.

---

# 382. SH08 — Pickup & Manifest Exit Gate

Phase passes only when its required behavior is implemented, automated tests pass, architecture boundaries remain clean, operational failure/recovery is demonstrated, and unresolved product/legal/provider assumptions are explicitly documented.

---

# 383. Implementation Phase SH09 — Tracking Pipeline

Webhook Inbox, pull polling, event normalization/dedupe/out-of-order policy/current projection.

---

# 384. SH09 — Tracking Pipeline Exit Gate

Phase passes only when its required behavior is implemented, automated tests pass, architecture boundaries remain clean, operational failure/recovery is demonstrated, and unresolved product/legal/provider assumptions are explicitly documented.

---

# 385. Implementation Phase SH10 — Delivery Attempts & Exceptions

Attempts, failure reasons, NDR/reschedule/RTO operations queue.

---

# 386. SH10 — Delivery Attempts & Exceptions Exit Gate

Phase passes only when its required behavior is implemented, automated tests pass, architecture boundaries remain clean, operational failure/recovery is demonstrated, and unresolved product/legal/provider assumptions are explicitly documented.

---

# 387. Implementation Phase SH11 — POD

Secure POD evidence/files/permissions/public projection boundary.

---

# 388. SH11 — POD Exit Gate

Phase passes only when its required behavior is implemented, automated tests pass, architecture boundaries remain clean, operational failure/recovery is demonstrated, and unresolved product/legal/provider assumptions are explicitly documented.

---

# 389. Implementation Phase SH12 — Returns & RTO

Reverse Shipment, return label/pickup, original link, RTO distinction and owner handoffs.

---

# 390. SH12 — Returns & RTO Exit Gate

Phase passes only when its required behavior is implemented, automated tests pass, architecture boundaries remain clean, operational failure/recovery is demonstrated, and unresolved product/legal/provider assumptions are explicitly documented.

---

# 391. Implementation Phase SH13 — COD

Instructions, carrier observations, remittance evidence and Payments/Banking integration boundary.

---

# 392. SH13 — COD Exit Gate

Phase passes only when its required behavior is implemented, automated tests pass, architecture boundaries remain clean, operational failure/recovery is demonstrated, and unresolved product/legal/provider assumptions are explicitly documented.

---

# 393. Implementation Phase SH14 — Freight Charges

Expected/actual evidence, normalized charge types, variance/reconciliation workbench.

---

# 394. SH14 — Freight Charges Exit Gate

Phase passes only when its required behavior is implemented, automated tests pass, architecture boundaries remain clean, operational failure/recovery is demonstrated, and unresolved product/legal/provider assumptions are explicitly documented.

---

# 395. Implementation Phase SH15 — Address/Country Providers

Address validation seam, Saudi National Address provider, practical localization/RTL.

---

# 396. SH15 — Address/Country Providers Exit Gate

Phase passes only when its required behavior is implemented, automated tests pass, architecture boundaries remain clean, operational failure/recovery is demonstrated, and unresolved product/legal/provider assumptions are explicitly documented.

---

# 397. Implementation Phase SH16 — Provider P1

OTO aggregator + Aramex/direct foundation or exact launch providers; sandbox/certification tests.

---

# 398. SH16 — Provider P1 Exit Gate

Phase passes only when its required behavior is implemented, automated tests pass, architecture boundaries remain clean, operational failure/recovery is demonstrated, and unresolved product/legal/provider assumptions are explicitly documented.

---

# 399. Implementation Phase SH17 — Provider P2

DHL/FedEx/SMSA/SPL as validated demand requires.

---

# 400. SH17 — Provider P2 Exit Gate

Phase passes only when its required behavior is implemented, automated tests pass, architecture boundaries remain clean, operational failure/recovery is demonstrated, and unresolved product/legal/provider assumptions are explicitly documented.

---

# 401. Implementation Phase SH18 — Work Queues & Bulk Ops

Operational queues, bulk rating/booking/labels, carrier health dashboard.

---

# 402. SH18 — Work Queues & Bulk Ops Exit Gate

Phase passes only when its required behavior is implemented, automated tests pass, architecture boundaries remain clean, operational failure/recovery is demonstrated, and unresolved product/legal/provider assumptions are explicitly documented.

---

# 403. Implementation Phase SH19 — Reporting/KPIs

Carrier performance, cost/variance, attempt/RTO/COD operational metrics with provenance.

---

# 404. SH19 — Reporting/KPIs Exit Gate

Phase passes only when its required behavior is implemented, automated tests pass, architecture boundaries remain clean, operational failure/recovery is demonstrated, and unresolved product/legal/provider assumptions are explicitly documented.

---

# 405. Implementation Phase SH20 — Security/Privacy/Observability

RLS, webhook security, secret handling, POD/PII controls, traces/metrics/alerts.

---

# 406. SH20 — Security/Privacy/Observability Exit Gate

Phase passes only when its required behavior is implemented, automated tests pass, architecture boundaries remain clean, operational failure/recovery is demonstrated, and unresolved product/legal/provider assumptions are explicitly documented.

---

# 407. Implementation Phase SH21 — Performance/Scale

Indexes/read models, webhook/rating/bulk benchmarks, provider outage backlog recovery.

---

# 408. SH21 — Performance/Scale Exit Gate

Phase passes only when its required behavior is implemented, automated tests pass, architecture boundaries remain clean, operational failure/recovery is demonstrated, and unresolved product/legal/provider assumptions are explicitly documented.

---

# 409. Implementation Phase SH22 — Migration & Go-Live

Carrier config, active shipment import, dry run/reconciliation, open-workflow transition.

---

# 410. SH22 — Migration & Go-Live Exit Gate

Phase passes only when its required behavior is implemented, automated tests pass, architecture boundaries remain clean, operational failure/recovery is demonstrated, and unresolved product/legal/provider assumptions are explicitly documented.

---

# 411. Implementation Phase SH23 — Cross-Module Acceptance

Sales/Inventory/Purchasing/Channels/Payments/Support handoff E2E tests.

---

# 412. SH23 — Cross-Module Acceptance Exit Gate

Phase passes only when its required behavior is implemented, automated tests pass, architecture boundaries remain clean, operational failure/recovery is demonstrated, and unresolved product/legal/provider assumptions are explicitly documented.

---

# 413. Implementation Phase SH24 — Release Gate

Production evidence, provider certification, security/performance/runbook drills and sign-off.

---

# 414. SH24 — Release Gate Exit Gate

Phase passes only when its required behavior is implemented, automated tests pass, architecture boundaries remain clean, operational failure/recovery is demonstrated, and unresolved product/legal/provider assumptions are explicitly documented.

---

# 415. Runbook — Carrier credentials expired

Disable/mark affected connection ActionRequired; stop unsafe new calls; alert admins; reauthorize/verify; replay safe pending jobs; never delete shipments/history.

---

# 416. Runbook — Provider outage

Circuit/backoff; keep jobs durable; offer alternate/manual carrier by policy; display provider degradation; recover backlog gradually; reconcile unknown outcomes first.

---

# 417. Runbook — Booking timeout unknown

Freeze blind retry; query/reconcile by client/provider refs; if found link remote booking; if proven absent retry with safe idempotency; record decision/audit.

---

# 418. Runbook — Duplicate AWB suspected

Stop further booking actions; compare request keys/provider refs/timestamps; void duplicate only through provider-supported action; preserve evidence/cost implications.

---

# 419. Runbook — Wrong address after booking

Determine provider amend support and handover state; amend if supported or cancel/rebook before handover; otherwise exception/intercept/RTO flow; never local-only edit.

---

# 420. Runbook — Tracking stopped

Check provider health/webhook, poll manually, compare refs, create stale-tracking exception, escalate provider if threshold exceeded.

---

# 421. Runbook — Delivered but customer disputes

Preserve POD; create/link Support case/return/quality flow; do not rewrite Delivered without transport evidence correction.

---

# 422. Runbook — RTO stuck

Refresh tracking/provider case, verify last scan/handover, flag exception; do not restock until Inventory physical receipt.

---

# 423. Runbook — COD mismatch

Compare instruction/provider collection/remittance evidence; link payment/bank data; raise reconciliation issue; no manual GL mutation from Shipping.

---

# 424. Runbook — Freight charge variance

Compare selected quote/package weight/provider remeasurement/surcharge evidence/vendor bill; classify/dispute/link finance workflow; never overwrite quote.

---

# 425. Runbook — Provider webhook signature failures

Treat as security incident when sustained/unexpected; reject, inspect secret rotation/provider config, avoid disabling verification to restore service.

---

# 426. Runbook — Label generation failed after booking

Re-fetch/regenerate provider label if supported using existing booking; never create second shipment just to obtain a label.

---

# 427. Runbook — Manifest mismatch

Freeze closeout changes, compare label membership/provider manifest refs, create corrective/new manifest as provider permits, preserve original evidence.

---

# 428. Runbook — Bulk job partial failure

Keep successful shipment results; retry only failed/unknown items according to state; do not rerun whole batch blindly.

---

# 429. Runbook — Wrong carrier/service selected

If not handed over, follow cancel/rebook policy; if handed over, exception/intercept/RTO depending provider; record override/root cause.

---

# 430. Report — Shipment Volume

By date/entity/branch/origin/destination/carrier/service/source/channel.

---

# 431. Report — Delivery Performance

On-time, first-attempt, average transit, promise breach and delivery exceptions.

---

# 432. Report — RTO / Return Transport

RTO count/rate/reasons/cost; customer-return reverse transport separately.

---

# 433. Report — Carrier Cost

Expected vs observed freight by carrier/service/zone/weight/source.

---

# 434. Report — Freight Variance

Quote/booking estimate vs provider actual evidence, reason/tolerance/status.

---

# 435. Report — COD Operations

Requested/collected-reported/remitted-reported/actual payment projection delays and mismatches.

---

# 436. Report — Provider Reliability

Rate/booking latency, error/timeout/unknown rates, webhook/poll health.

---

# 437. Report — Exceptions Aging

Open transport exceptions by type/severity/age/owner/carrier.

---

# 438. Report — Pickup SLA

Scheduled/confirmed/completed pickups and delays.

---

# 439. Report — Shipping by Source

Sales/Purchase/Transfer/Return/Dropship/Manual source mix.

---

# 440. Final Source-of-Truth Matrix

```text
Party / Address master                    -> Directory
Money / Currency primitives              -> FinBase
Product / UOM identity                   -> Catalog
Customer commercial order                -> Sales
Supplier commercial order                -> Purchasing
Stock / Reservation / StockMove          -> Inventory
External commerce channel state          -> Channels
Shipment / Carrier booking / Tracking    -> Shipping
Customer/Vendor Invoice                  -> Invoicing
Payment / Receipt / Refund               -> Payments
Bank actual / reconciliation             -> Banking / BankRec
Journal Entry                             -> Ledger
Transport legal/country rules            -> Compliance/Country Provider
Notification delivery                    -> Platform Notifications
```

---

# 441. Final Shipping Ownership

Shipping owns:

- Carrier operational profile/account/service/connection references.
- Shipment and package transportation representation.
- Rate requests/quotes and carrier-selection evidence.
- Booking/AWB/labels/pickup/manifest/handover.
- Tracking events/current transport state.
- Delivery attempts/exceptions/POD.
- Reverse transport/RTO execution links.
- COD transport observations/remittance evidence.
- Freight expected/actual carrier charge evidence and reconciliation status.

---

# 442. Final Shipping Non-Ownership

Shipping never owns:

- Sales/Purchase commercial commitment.
- Canonical Party/Product.
- Warehouse stock/valuation/receipt/issue.
- Customer/vendor legal invoice.
- Actual cash/bank payment or settlement.
- GL/account determination.
- Customer support case.
- Product quality disposition.
- Fleet maintenance/driver HR master.
- Country legal constants outside provider contracts.

---

# 443. Final Agent Rule — Business First

Implement real shipping workflows and failure states before decorative UI or generic abstractions.

---

# 444. Final Agent Rule — Ownership

Never cross-write another module or make Shipping source of stock/payment/invoice/accounting truth.

---

# 445. Final Agent Rule — Idempotency

Every booking/cancel/return/pickup/provider callback path must prove duplicate safety and unknown-outcome recovery.

---

# 446. Final Agent Rule — Evidence

Preserve provider raw codes/refs and immutable transaction snapshots required to explain what happened.

---

# 447. Final Agent Rule — Provider Isolation

No carrier-specific enums/DTOs/credentials leak into Shipping Domain/public contracts.

---

# 448. Final Agent Rule — Financial Safety

COD and freight evidence never become Payments/Ledger truth directly.

---

# 449. Final Agent Rule — Privacy

Phone/address/POD/signatures/photos/credentials are least-privilege data, not debug convenience.

---

# 450. Final Agent Rule — Performance

Use projections/batching/async external work; never sacrifice correctness for faster fake state.

---

# 451. Final Agent Rule — Progressive Complexity

Small courier shipping remains simple; advanced TMS concepts appear only when enabled/needed.

---

# 452. Final Agent Rule — Future Safety

Protect seams for freight/load/tendering without prematurely building them.

---

# 453. Release Gate — Required Evidence

Production release requires:

- Architecture tests prove dependency/implementation boundaries.
- RLS/cross-tenant security suite passes.
- Booking idempotency + TimeoutUnknown drill passes.
- At least one production-target provider adapter passes sandbox/certification/smoke evidence.
- Webhook authentication/replay tests pass.
- Bulk booking/label and tracking burst performance targets validated on representative dataset.
- POD/PII/credential security review complete.
- COD/payment boundary and freight/accounting boundary reviewed by Finance.
- RTO/customer-return/inventory handoff E2E accepted.
- Runbooks exercised for provider outage, duplicate/unknown booking and tracking recovery.
- Go-live/migration/backout plan approved.
- All MARKET/LEGAL/PROVIDER verification items for enabled countries/carriers resolved.

---

# 454. Release Sign-Off

Required sign-off: Product/Operations, Architecture, Warehouse/Logistics, Finance/Payments for COD/cost evidence, Security/Privacy, Backend/Frontend, QA/Performance and Implementation/Operations. Country/legal specialist sign-off where international/customs/regulatory behavior is enabled.

---

# 455. No Separate Shipping Subfeature Packs

Keep customer-facing Shipping requirements in this comprehensive Pack 32. Split only genuinely standalone future domains such as enterprise TMS planning/tendering, Fleet/Telematics or Trade/Customs Compliance when market need proves they deserve independent ownership.

---

# 456. Official Market / Platform Source Register

Verified/reviewed during design on **2026-10-08**. These sources provide current product/API behavior evidence; they do not authorize copying competitor architecture blindly.

1. **Odoo 19 Shipping** — third-party carrier integration, rates, shipment/labels, tracking and multi-package operations.  
   `https://www.odoo.com/documentation/19.0/applications/inventory_and_mrp/inventory/shipping_receiving/setup_configuration/third_party_shipper.html`  
   `https://www.odoo.com/documentation/19.0/applications/inventory_and_mrp/inventory/shipping_receiving/setup_configuration/labels.html`  
   `https://www.odoo.com/documentation/19.0/applications/inventory_and_mrp/inventory/shipping_receiving/setup_configuration/multipack.html`

2. **ShipStation / ShipEngine APIs** — rate shopping, labels, multi-package/batch/return labels, manifests and pickups.  
   `https://docs.shipstation.com/api-overview`  
   `https://docs.shipstation.com/getting-started`  
   `https://docs.shipstation.com/apis/shipengine/docs/rates/get-shipment-rates`  
   `https://docs.shipstation.com/apis/shipengine/docs/shipping/manifests/create-a-manifest`

3. **OTO** — MENA multi-carrier API, rates, tracking/webhooks, returns, shipping transactions/reconciliation, address validation and broad carrier connectivity.  
   `https://www.tryoto.com/api-integration`

4. **Microsoft Dynamics 365 Supply Chain Management — Transportation Management** — inbound/outbound transport, own fleet/external providers, fastest/least-cost selection, load building, modes/methods, multi-stop routes and freight reconciliation.  
   `https://learn.microsoft.com/en-us/dynamics365/supply-chain/transportation/transportation-management-overview`  
   `https://learn.microsoft.com/en-us/dynamics365/supply-chain/transportation/transportation-management-modes-methods`  
   `https://learn.microsoft.com/en-us/dynamics365/supply-chain/transportation/plan-freight-transportation-routes-multiple-stops`  
   `https://learn.microsoft.com/en-us/dynamics365/supply-chain/transportation/reconcile-freight-transportation-management`

5. **SAP Transportation Management** — freight units/orders/bookings, carrier selection, transportation planning, charges and tendering/RFQ award.  
   `https://help.sap.com/docs/SAP_S4HANA_ON-PREMISE/e3dc5400c1cc41d1bc0ae0e7fd9aa5a2/9c27c9535f267414e10000000a174cb4.html`  
   `https://help.sap.com/docs/SAP_S4HANA_ON-PREMISE/e3dc5400c1cc41d1bc0ae0e7fd9aa5a2/6c921d2b67584ec2a2a7c0bcc8a0b8dc.html`

6. **DHL Express MyDHL API** — account rates, multi-piece rating, shipment creation, labels, pickup management, tracking, address capability validation, electronic POD, landed cost and customs invoice data.  
   `https://developer.dhl.com/api-reference/dhl-express-mydhl-api`

7. **Saudi Post / SPL National Address API** — National Address verification/search using short address/geolocation and updated region/city/district lookups.  
   `https://splonline.com.sa/en/national-address-api/`

Carrier API versions, services, regional availability, account contracts, pricing, webhook/signature behavior, customs requirements, COD/remittance programs and legal obligations are **MARKET/LEGAL/PROVIDER VERIFICATION at implementation and release time**.

---

# 457. Market Positioning

TODIR should position Shipping as an **ERP-native Arabic-market transportation execution hub**: simpler than enterprise TMS for everyday courier shipping, but materially safer/deeper than a basic “print AWB” connector through idempotency, multi-carrier evidence, tracking reconciliation, RTO/COD and finance boundaries.

---

# 458. Differentiator

> **One operational Shipping workspace that can book and track many carriers while stock remains Inventory truth, orders remain Sales/Purchasing truth and money remains Payments/Banking truth.**

That boundary discipline is the differentiator, not merely the number of carrier logos.

---

# 459. Architecture Closure

Pack 32 is approved only if implementation preserves these non-negotiables:

```text
1. Shipping is transport execution, not Inventory/Sales/Finance duplication.
2. Directory + FinBase are hard; Inventory/Sales/Purchasing/etc. are optional capabilities/sources.
3. LabelCreated never means Shipped/HandedOver.
4. RateQuote never silently becomes ActualCarrierCharge.
5. COD observation never becomes Payment/Bank receipt.
6. RTO and Customer Return remain distinct.
7. External booking timeout cannot create blind duplicate AWBs.
8. Tracking is append-only evidence + normalized projection; stale events do not corrupt state.
9. Booked origin/destination/package evidence remains historically stable.
10. Provider adapters are capability-based and isolated.
11. POD/PII/credentials obey least privilege and tenant isolation.
12. Advanced TMS seams are preserved without building enterprise complexity prematurely.
```

---

# 460. Next Pack

After Shipping & Logistics, the recommended next customer-facing pack is:

> **`33_ERP_SAAS_COMMISSIONS_SALES_INCENTIVES_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`**

Central question:

> **How does TODIR calculate, explain, accrue, reverse and settle salesperson/team commissions from real commercial outcomes—across orders, invoices, collections, returns and cancellations—without making Sales or Payroll duplicate financial truth?**

---

# 461. Final Product Verdict

TODIR should build Pack 32 as a **Shipping & Transportation Execution Hub**, not as an Inventory sub-screen and not as an enterprise Transportation Management suite on day one.

For the small company, it should feel like: select carrier -> create label -> hand over -> track -> delivered.

For the larger merchant/distributor, the same model must safely support many carriers/accounts, multi-package shipments, bulk labels, pickups/manifests, delivery exceptions, RTO/returns, COD/remittance evidence, freight variance and future TMS expansion without replacement architecture.

> **Simple outside. Powerful inside. Transport truth is explicit; stock, commerce and finance stay with their owners.**

---


<!-- Section count: 462 -->

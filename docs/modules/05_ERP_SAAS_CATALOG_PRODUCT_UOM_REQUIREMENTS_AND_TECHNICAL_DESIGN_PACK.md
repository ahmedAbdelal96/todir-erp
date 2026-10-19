# 05 — ERP SaaS Catalog / Product / UOM Requirements & Technical Design Pack

**Status:** PRODUCT + DOMAIN + TECHNICAL BASELINE  
**Version:** v1.0  
**Date:** 2026-10-05  
**Audience:** Product Owner, Senior Product Designer, Product Architect, Backend/Frontend Engineers, Implementation Agent, QA, Migration Team, Performance Engineer  

**Depends on**
- `01_ERP_SAAS_MASTER_MODULE_AND_DEPENDENCY_BLUEPRINT.md`
- `02_ERP_SAAS_PRODUCTION_READY_TECH_STACK_AND_PROJECT_STRUCTURE.md`
- `03_ERP_SAAS_PLATFORM_KERNEL_AND_MODULE_SYSTEM_TECHNICAL_DESIGN_PACK.md`
- `04_ERP_SAAS_DIRECTORY_PARTY_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`

---

# 0. Business Question

هذا الملف يجيب على سؤال:

> **ما الشيء الذي تبيعه الشركة أو تشتريه أو تخزنه أو تنتجه أو تقدمه كخدمة، وكيف نمثله مرة واحدة بحيث كل الموديولات تستخدم نفس الحقيقة بدون تكرار؟**

لو Catalog اتبنى غلط، المشاكل ستظهر لاحقًا في Sales وPurchasing وInventory وPOS وManufacturing وPricing وTax وE-commerce وReporting.

---

# 1. Product Vision

نريد Catalog يشعر المستخدم أنه:

> **المكان الواحد الذي يعرف كل ما تتعامل به الشركة كمنتج أو خدمة.**

وليس شاشة Item Master فيها عشرات الحقول غير المفهومة.

يجب أن يخدم بنفس التصميم:

- شركة خدمات عندها 20 خدمة.
- تاجر جملة عنده 10,000 صنف.
- موزع يشتري بالكرتونة ويبيع بالقطعة.
- متجر ملابس عنده ألوان ومقاسات.
- شركة أغذية تبيع بالوزن.
- شركة إلكترونيات عندها Variants كثيرة.
- مستقبل Manufacturing وPOS وE-commerce.

---

# 2. Locked Architecture Decisions

## 2.1 One Canonical Product Identity

لا يوجد:

```text
Sales Product
Purchase Product
Inventory Product
```

كثلاث هويات منفصلة.

يوجد Catalog واحد.

---

## 2.2 Product Template + Product Variant

الهوية تعتمد:

```text
ProductTemplate
    └── ProductVariant
```

مثال:

```text
Classic T-Shirt
    ├── Black / M
    ├── Black / L
    ├── Blue / M
    └── Blue / L
```

---

## 2.3 Every Product Has a Variant

حتى المنتج البسيط:

```text
Consulting Service
```

داخليًا:

```text
ProductTemplate
    └── DefaultVariant
```

المستخدم لا يرى كلمة Variant لو مش محتاجها.

لكن كل المعاملات المستقبلية تشير لنفس النوع:

```text
ProductVariantId
```

وده يمنع وجود مسارين مختلفين في الكود.

---

## 2.4 UOM First-Class

وحدة القياس ليست Text بجانب الكمية.

المنتج قد:

- يُخزن قطعة.
- يُشترى كرتونة.
- يُباع Pack.
- يُباع بالكيلو.
- يُقدم بالساعة.

---

## 2.5 Catalog Does Not Own Price

```text
Catalog → What is it?
Pricing → How much?
```

Price ليس عمودًا في الهوية الأساسية للمنتج.

---

## 2.6 Catalog Does Not Own Cost

Average Cost / FIFO / Stock Value ملك Inventory Valuation.

---

## 2.7 Catalog Does Not Own Stock

Stock Quantity وAvailability ملك Inventory.

Catalog screen يمكنه عرضهم من Provider سريع إذا Inventory فعال.

---

## 2.8 Catalog Does Not Own Tax Calculation

Tax module يملك Tax Engine.

Catalog يمكن أن يوفر Product Classification reference، لكن لا يخزن Rate كحقيقة عامة.

---

## 2.9 Product UX Principle

> **Simple Outside. Powerful Inside.**

Service بسيط:

```text
Name
Type = Service
UOM = Hour
```

تاجر جملة يمكنه فتح:

```text
Variants
Multiple UOM
Barcodes
References
Brand
Category
Aliases
```

---

# 3. Real Business Scenarios

## 3.1 Service Company

```text
Accounting Consultation
Type: Service
UOM: Hour
```

لا نطلب:

- Warehouse.
- Barcode.
- Inventory tracking.
- Weight.

---

## 3.2 Simple Goods

```text
A4 Paper
SKU: PAP-A4
Base UOM: Box
Barcode: ...
```

Template + Default Variant.

---

## 3.3 Wholesale Packaging

```text
Water Bottle 500 ml
```

الوحدات:

```text
Piece = 1
Pack = 6 Pieces
Carton = 24 Pieces
```

الشركة قد:

```text
Buy = Carton
Sell = Pack / Piece
Store internally = Piece
```

---

## 3.4 Grocery / Weight

```text
Cheese
Base UOM = Gram
Selling UOM = Kilogram
```

يدعم fractional quantity.

---

## 3.5 Fashion

```text
Polo Shirt
Attributes:
- Color
- Size
```

Variants:

```text
Black / M
Black / L
Blue / M
Blue / L
```

كل Variant يمكنه SKU وBarcode مستقل.

---

## 3.6 Electronics

```text
Laptop X
16GB / 512GB
32GB / 1TB
```

Serial Tracking لاحقًا في Inventory، وليس Catalog.

---

## 3.7 Customer and Supplier Codes

Our SKU:

```text
ABC-001
```

Supplier code:

```text
SUP-X13
```

Customer code:

```text
CUST-7782
```

كلهم لنفس Variant.

---

## 3.8 Arabic / English

```text
مياه معدنية 500 مل
Mineral Water 500ml
```

Search بالاسمين.

---

## 3.9 Obsolete Product

نوقف المنتج للمعاملات الجديدة لكن التاريخ يظل.

```text
Active → Archived
```

لا حذف.

---

# 4. Competitor Lessons

## Odoo

Odoo يدعم Multiple UOM لنفس المنتج، ومن أمثلته شراء العبوات الكبيرة والبيع بالوحدات الأصغر.

الدرس:

- UOM conversion جزء أساسي من المعاملات.
- Sales/Purchase units قد تختلف.

---

## Microsoft Business Central

يدعم:

- Base UOM.
- Alternate UOM.
- Qty per UOM.
- Product Variants.
- Variant attributes.
- Customer/Vendor Item References.
- Reference مرتبطة بVariant وUOM.

الدرس:

> **Product, Variant, UOM, External Reference هي مفاهيم منفصلة.**

---

## ERPNext

Item يمكن أن يكون:

- Product.
- Service.
- Raw Material.
- Component.
- Finished Good.

ويدعم:

- Item Groups.
- Multiple UOM.
- Barcodes.
- UOM-specific barcodes.
- Variants.

لكن نحن لن نجمع Inventory Valuation/Reorder داخل Catalog لمجرد أن بعض الأنظمة تفعل ذلك.

---

## Daftra

يدعم منتجات وخدمات وBrands وCategories وMultiple UOM وBarcodes متعددة.

وده يؤكد أن:

> Multiple UOM وPackaging مش Enterprise-only feature في السوق العربي.

---

# 5. Core Domain Model

```text
ProductTemplate
ProductVariant

ProductCategory
Brand

UomDimension
UnitOfMeasure
ProductUom

ProductBarcode
ProductAlias
ProductTranslation
ProductReference

ProductAttribute
ProductAttributeValue
ProductTemplateAttribute
ProductVariantAttribute

ProductVisibility
```

Downstream Profiles:

```text
SalesProductProfile
PurchaseProductProfile
InventoryProductProfile
TaxProductProfile
ManufacturingProfile
```

هذه ليست ملك Catalog.

---

# 6. Product Template

يمثل الفكرة المشتركة للمنتج:

```text
Classic T-Shirt
Water 500ml
Consulting Service
Laptop Model X
```

---

# 7. Product Variant

هو الـSKU الفعلي الذي تستخدمه المعاملات.

الـVariant يمكن أن يختلف في:

- SKU.
- Barcode.
- Attributes.
- Image.
- Status.

---

# 8. Default Variant

لو المستخدم لم يفعّل Variants:

```text
ProductTemplate
    └── DefaultVariant
```

الواجهة تعرضه كمنتج عادي.

---

# 9. Product Nature

MVP:

```text
Goods
Service
```

لا نضيف من الآن:

```text
RawMaterial
FinishedGood
Consumable
Digital
Stockable
```

كـenum عالمي دائم.

دي سياسات Profiles حسب الموديولات.

---

# 10. Goods != Stocked

Goods لا يعني أن Inventory لازم يكون فعال.

Catalog يعرف أن الشيء Good.

Inventory يقرر:

```text
Tracked?
Valued?
Lot/Serial?
Negative allowed?
```

---

# 11. Service

Service يمكن:

- يباع.
- يشترى.
- يُقاس Hour/Day/Visit/Session.

لا نفترض Service = Sales only.

---

# 12. SKU

SKU موجود على Variant.

```text
TSH-BLK-M
```

- ليس Primary Key.
- UUIDv7 هو Technical ID.
- SKU business-friendly.
- tenant-scoped unique.

---

# 13. SKU Change

تغييره ممكن بصلاحية.

Old SKU يتحول Alias عند الحاجة.

لا نكسر integrations silently.

---

# 14. Product Names

Template:

```text
display_name
name_ar?
name_en?
```

Variant display يمكن يكون:

```text
Classic T-Shirt — Black / M
```

---

# 15. Translations

Underlying model يدعم:

```text
ProductTranslation
language_code
name
description
```

ولا نفترض العربية والإنجليزية هما آخر لغتين للأبد.

---

# 16. Aliases

Examples:

```text
Old SKU
Old Name
Arabic alternative spelling
English abbreviation
Legacy system name
```

مفيدة للبحث والهجرة.

---

# 17. Category

Hierarchy:

```text
Food
  └── Beverages
      └── Water
```

Category للتنظيم.

ليست Tax Category تلقائيًا.

وليست GL Account.

---

# 18. Brand

Brand = classification.

Brand لا يساوي Supplier.

مثال:

```text
Brand: Nike
Supplier: Distributor X
```

---

# 19. Manufacturer

لو احتجنا Manufacturer كجهة قانونية:

نربطه لاحقًا بـParty/Directory.

لا ننشئ نسخة شركة داخل Catalog.

---

# 20. UOM — Business Model

Examples:

```text
Piece
Pack
Carton
Gram
Kilogram
Meter
Square Meter
Liter
Hour
Day
Visit
```

---

# 21. UOM Dimension

Examples:

```text
Count
Mass
Length
Area
Volume
Time
BusinessUnit
```

Generic conversion valid:

```text
Gram ↔ Kilogram
```

غير صالح بدون Product context:

```text
Carton ↔ Piece
```

---

# 22. Base UOM

كل Variant له Base UOM.

مثال:

```text
Water Bottle
Base = Piece
```

Alternate:

```text
Pack = 6 Pieces
Carton = 24 Pieces
```

---

# 23. Quantity Truth

Canonical:

```text
base_qty = entered_qty × factor_to_base
```

Example:

```text
10 Cartons × 24 + 5 Pieces = 245 Pieces
```

---

# 24. Product-specific Conversion

`Carton` ليس له Factor عالمي.

Product A:

```text
Carton = 24
```

Product B:

```text
Carton = 12
```

---

# 25. Decimal Only

Quantity:

```text
decimal / numeric
```

لا:

```text
float
double
```

Baseline:

```text
numeric(19,6)
```

والـprecision النهائي يتقفل مع FinBase.

---

# 26. Fraction Rules

UOM يمكن تحدد:

```text
allow_fraction
precision
```

مثال:

```text
Piece → غالبًا whole
kg → fractional
Hour → fractional
```

لكن لا نفرض افتراضًا عالميًا غير قابل للتغيير.

---

# 27. Conversion Snapshot

أي Transaction line مستقبلًا تحفظ:

```text
entered_qty
entered_uom_id
conversion_factor_snapshot
base_qty
```

لو Conversion تغير غدًا، المستند القديم لا يتغير.

---

# 28. Effective-Dated Conversion

لو:

```text
Carton = 24
```

ثم المورد غير العبوة إلى:

```text
Carton = 20
```

لا نعدل التاريخ.

إما:

- version/effective date.
- أو علاقة UOM جديدة effective-dated.

---

# 29. Dangerous Base UOM Change

بعد وجود Inventory/Transactions:

تغيير Base UOM عملية حساسة جدًا.

MVP rule:

- قبل الاستخدام → Allowed.
- بعد الاستخدام → Block/Controlled migration.

Inventory Pack يقفل السيناريو النهائي.

---

# 30. Barcode

Barcode على Variant.

يمكن ربطه بـUOM.

مثال:

```text
Piece Barcode → Piece
Carton Barcode → Carton
```

---

# 31. Multiple Barcodes

Variant يمكن يكون عنده:

- manufacturer barcode.
- internal barcode.
- Piece barcode.
- Pack barcode.
- Carton barcode.

لا نضع `barcode` واحد فقط على Product.

---

# 32. Barcode Uniqueness

Normal rule:

```text
UNIQUE (tenant_id, normalized_barcode)
```

لأن Scan لازم يحل إلى نتيجة واضحة.

---

# 33. Product References

نحتاج:

```text
ProductReference
```

لأكواد العملاء والموردين.

Fields conceptually:

```text
variant_id
party_id
reference_type
reference_code
uom_id?
valid_from?
valid_to?
```

---

# 34. Reference Example

```text
Our SKU = WTR-500

Supplier A:
X500-C
UOM Carton

Customer B:
DRINK-500
UOM Pack
```

كلهم نفس Variant.

---

# 35. Attributes

Examples:

```text
Color
Size
Memory
Storage
Material
```

---

# 36. Variant-driving Attributes

Template chooses:

```text
Color
Size
```

Variant:

```text
Color=Black
Size=M
```

---

# 37. Variant Combination Uniqueness

لا نسمح:

```text
Black / M
Black / M
```

مرتين في نفس Template.

نعمل canonical combination signature/hash.

---

# 38. Variant Explosion

لا نولد كل combinations تلقائيًا بدون Preview.

```text
10 × 10 × 5 = 500 variants
```

المستخدم يختار المطلوب.

---

# 39. Variant UX

لو لا يوجد Variants:

المستخدم لا يرى Matrix.

لو فعّلها:

```text
1 Basics
2 Attributes
3 Combinations
4 SKU / Barcode
```

---

# 40. Product vs Packaging

Variant:

```text
Red / M
```

Packaging/UOM:

```text
Carton of 20 Red/M
```

إلا لو Carton له commercial identity مستقل يستحق Variant/Product منفصل.

---

# 41. Packaging Decision UX

السؤال للمستخدم:

> هل دي نفس السلعة بكمية/عبوة مختلفة، أم SKU مستقل يُباع كمنتج مختلف؟

Same → UOM/Packaging.

Independent SKU → Variant/Product.

---

# 42. Physical Dimensions

Design hook:

```text
weight
height
width
length
volume
```

لكن لا نبنيها كلها الآن قبل Shipping/Inventory requirements.

غالبًا ستكون على Variant/UOM packaging level.

---

# 43. Lots / Serial / Expiry

Catalog لا يملك Lot/Serial instances.

Inventory يملكهم.

Catalog فقط يتيح Stable Variant identity.

---

# 44. Price Ownership

ممنوع:

```text
product.price
```

كحقيقة تجارية عامة.

Pricing module يملك Pricelists/Rules.

Catalog لا يملك Amount.

---

# 45. Cost Ownership

ممنوع:

```text
product.cost
```

كـcanonical valuation.

Inventory owns cost.

---

# 46. Stock Ownership

ممنوع Catalog يحسب Available Qty.

Inventory provider.

---

# 47. Tax Ownership

Tax module يملك:

- Classification rules.
- Rates.
- computation.

Catalog يمكنه reference إلى Tax Classification حسب contract.

---

# 48. Sales Profile

Later:

```text
default sales UOM
sales description
sellable policy
```

Sales owns.

---

# 49. Purchase Profile

Later:

```text
default purchase UOM
purchasable policy
supplier preferences
```

Purchasing owns.

---

# 50. Inventory Profile

Later:

```text
tracked
stock UOM policy
valuation method
negative stock
lot/serial
replenishment
```

Inventory owns.

---

# 51. Manufacturing Profile

Later:

```text
BOM
make/buy
yield
```

Manufacturing owns.

---

# 52. Multi-Legal-Entity

Product identity tenant-level.

Entity behavior can differ.

Example:

```text
Entity A → Sells + Stocks
Entity B → Purchases
Entity C → Does not use
```

---

# 53. Product Visibility

Support:

```text
TenantWide
SelectedLegalEntities
SingleLegalEntity
```

لا نكرر Product per company.

---

# 54. Multi-Entity Price

Pricing per Entity/context.

---

# 55. Multi-Entity Cost

Inventory valuation per Entity.

---

# 56. Product Quick Create

Simple:

```text
Name *
Type *
Base UOM *
SKU optional/generated
```

Advanced collapsed.

---

# 57. Service Quick Create

```text
Name
UOM
```

ولا نطلب Stock/Barcode.

---

# 58. Goods Quick Create

لو Inventory inactive:

يمكن إنشاء Product identity.

لو المستخدم يريد Stock transaction:

Readiness تطلب Inventory Profile.

---

# 59. Product Readiness

Capabilities:

```text
CanSell
CanPurchase
CanStock
CanManufacture
CanUseInPOS
CanPublishOnline
```

Example:

```text
CanSell = Yes
CanStock = No — Inventory Profile missing
```

---

# 60. Required Fields Philosophy

لا نجعل كل downstream requirements Required على Create Product.

Contextual validation.

---

# 61. Product Workspace

Header:

```text
Name
SKU
Goods/Service
Status
Category
Brand
```

Derived badges:

```text
Sellable
Purchasable
Stocked
Service
```

---

# 62. Core Tabs

```text
Overview
Variants
Units & Barcodes
Aliases & References
Files
Audit
```

Conditional:

```text
Sales
Purchasing
Inventory
Pricing
Tax
Manufacturing
```

---

# 63. Small Company UX

صاحب شركة صغير عنده Sales/Purchase/Inventory Roles.

نفس Account.

Catalog لا يفرض Persona واحدة.

Experience =

```text
Active Modules
+ Effective Permissions
+ Entity Scope
+ User Preferences
```

---

# 64. Permissions Are Authority

Backend permission controls actions.

Roles مجرد Bundles.

مثال:

```text
catalog.product.read
catalog.product.create
catalog.product.edit
catalog.product.archive
catalog.variant.manage
catalog.product_uom.manage
catalog.barcode.manage
catalog.reference.manage
catalog.attribute.manage
catalog.import
catalog.export
```

---

# 65. Sales UX

Sales user يحتاج:

- Product Picker.
- SKU/name.
- Variant.
- Sales UOM.
- price from Pricing.
- availability from Inventory optionally.

لا يرى Inventory Cost إلا بصلاحية Inventory.

---

# 66. Purchasing UX

Purchasing user يحتاج:

- supplier reference.
- purchase UOM.
- supplier product code.

---

# 67. Warehouse UX

Warehouse user يحتاج:

- SKU.
- barcode.
- UOM.
- Inventory data.

لا يحتاج Pricing configuration.

---

# 68. Master Data Admin UX

يحتاج:

- Variant.
- UOM conversion.
- barcode.
- import.
- archive.
- audit.
- duplicate issues.

---

# 69. Search — Critical Product Capability

Search by:

```text
SKU
Name AR
Name EN
Alias
Barcode
Customer reference
Supplier reference
Brand
Variant label
```

---

# 70. Product Picker

Return lightweight:

```text
VariantId
TemplateId
SKU
DisplayName
VariantLabel
BaseUom
PrimaryBarcode?
Status
```

ولا يحمل:

- كل الصور.
- كل UOMs.
- كل Attributes.
- Stock history.
- Price rules.

---

# 71. Product Picker Speed

Initial engineering target:

```text
Catalog-only p95 <= 250–300ms
```

على datasets واقعية.

ده Target يجب قياسه، مش Claim.

---

# 72. Exact SKU / Barcode

يجب يكونوا أسرع من fuzzy name search.

Dedicated indexed lookup.

---

# 73. Arabic Search

Search-only normalization:

- Alef variants.
- diacritics optionally.
- Arabic/Latin digits.
- whitespace/punctuation.

لا نغير الاسم الأصلي.

---

# 74. PostgreSQL First

Use:

```text
B-tree exact SKU/barcode
pg_trgm for names/aliases
GIN/GiST where justified
```

لا OpenSearch Day 1.

---

# 75. Catalog Scale

Design must remain healthy around:

```text
100k variants medium/large tenant
500k variants stress
1m variants enterprise stress
```

مع tenant-scoped indexed queries.

These are target operating scales, not a gate for the current Catalog completion mission. Product Owner decision (2026-10-19): benchmark certification at these volumes is deferred to a future Performance Hardening mission.

---

# 76. Global SaaS Scale

لو عندنا 20 مليون Product across SaaS:

User query لا يرى 20 مليون.

دائمًا:

```text
tenant_id = CurrentTenant
```

ثم Filter + Pagination.

Later cells distribute tenants.

---

# 77. Pagination

Server-side.

No:

```text
GET all products
```

Default page:

```text
50
```

---

# 78. Read Model / Search Projection

Create optimized projection:

```text
catalog_search_projection
```

Fields:

```text
tenant_id
variant_id
template_id
sku
display_name
normalized_ar
normalized_en
search_text
category
brand
base_uom
status
```

Projection ليست Source of Truth.

---

# 79. Performance Rule

> **Write Model for correctness. Read Model for speed.**

لكن لا نكرر ownership.

---

# 80. Search Projection RLS

Projection نفسها Tenant protected.

ممنوع "fast table" بدون RLS.

---

# 81. Immediate Searchability

بعد Create Product:

لا نريد user يستنى RabbitMQ لكي يجد المنتج.

Core searchable identity updates synchronously.

Heavy enrichment async ممكن.

---

# 82. Lazy Loading

فتح Product:

Initial summary فقط.

Tabs load when opened.

---

# 83. N+1 Is Forbidden

ممنوع:

```text
50 products
→ 50 Pricing calls
→ 50 Inventory calls
```

Use batch:

```text
GetPrices(variantIds)
GetAvailability(variantIds)
```

أو purpose-built read model.

---

# 84. Composite Sales Picker

Later can return:

```text
Catalog identity
+ Price
+ Availability
```

في Query optimized للـSales.

بدون كسر ownership.

---

# 85. Query Count Budget

Critical screen يجب يكون له bounded query count.

No per-row query.

---

# 86. Cache

Safe:

```text
UOM definitions
Category tree
Brand list
```

Potential hot summaries later.

Never cache Stock/Price inside Catalog as authority.

---

# 87. Redis

Not required Day 1.

Postgres + proper indexes first.

---

# 88. Product List

Default columns:

```text
SKU
Name
Type
Category
Base UOM
Status
```

Optional:

```text
Brand
Barcode
Variants count
```

---

# 89. Filters

```text
Active
Goods
Service
Category
Brand
Has Variants
Missing Barcode
```

Derived module filters conditional.

---

# 90. Bulk Actions

Safe:

```text
Archive
Category
Brand
Export
```

Dangerous actions like Base UOM change get dedicated workflow.

---

# 91. Database Schema

```text
foundation_catalog
```

---

# 92. Core Tables

```text
product_template
product_variant
product_category
brand

uom_dimension
uom
product_uom

product_barcode
product_alias
product_translation
product_reference

product_attribute
product_attribute_value
product_template_attribute
product_variant_attribute

product_visibility
external_reference
catalog_search_projection
```

---

# 93. product_template

Concept:

```text
id
tenant_id
display_name
name_ar?
name_en?
product_nature
category_id?
brand_id?
status
created_at
updated_at
version
```

---

# 94. product_variant

```text
id
tenant_id
product_template_id
sku
normalized_sku
display_suffix?
is_default_variant
status
version
```

Unique:

```text
tenant_id + normalized_sku
```

---

# 95. uom

```text
id
scope/system_or_tenant
code
name_ar
name_en
dimension_id?
allow_fraction
precision
status
```

---

# 96. product_uom

```text
id
tenant_id
variant_id
uom_id
factor_to_base
is_base
usage_flags
effective_from
effective_to?
rounding_precision?
status
version
```

---

# 97. Base UOM Constraint

Exactly one current base per Variant.

```text
factor_to_base = 1
```

---

# 98. product_barcode

```text
id
tenant_id
variant_id
uom_id?
barcode_type
value
normalized_value
is_primary
status
```

---

# 99. product_alias

```text
id
tenant_id
template_id/variant_id
alias
normalized_alias
alias_type
language?
```

---

# 100. product_reference

```text
id
tenant_id
variant_id
party_id?
reference_type
reference_code
uom_id?
valid_from?
valid_to?
status
```

---

# 101. product_attribute

```text
id
tenant_id
code
name
data_type
status
```

---

# 102. product_attribute_value

```text
id
tenant_id
attribute_id
code
display_value
sort_order
status
```

---

# 103. product_template_attribute

```text
template_id
attribute_id
drives_variant
required
sort_order
```

---

# 104. product_variant_attribute

```text
variant_id
attribute_id
value_id
```

---

# 105. Product Visibility

```text
tenant_id
template_id
scope_type
legal_entity_id?
```

Template-level default.

Variant override only if real need.

---

# 106. RLS

Every tenant-owned table:

```text
ENABLE RLS
FORCE RLS
```

runtime app role non-owner.

---

# 107. Indexing

Critical examples:

```text
(tenant_id, id)
(tenant_id, normalized_sku)
(tenant_id, normalized_barcode)
(tenant_id, status, display_name)
(tenant_id, category_id, status)
(tenant_id, brand_id, status)
```

---

# 108. Avoid Index Explosion

لا نعمل Index لكل Custom Attribute.

Attribute search يحتاج measured strategy.

---

# 109. EF Core

```text
CatalogDbContext
```

لا يحتوي Inventory/Sales/Pricing entities.

---

# 110. Cross-Module Rule

لا Module يستخدم CatalogDbContext.

يستخدم Contracts.

---

# 111. API

Base:

```text
/api/v1/catalog
```

Examples:

```text
GET /products
POST /products
GET /products/{id}
PATCH /products/{id}

GET /variants
GET /search

POST /products/{id}/archive
POST /products/{id}/restore

POST /products/{id}/variants
POST /variants/{id}/uoms
POST /variants/{id}/barcodes
```

---

# 112. Commands

```text
CreateProduct
UpdateProductIdentity
ArchiveProduct
RestoreProduct

CreateVariant
UpdateVariant
DeactivateVariant

AddProductUom
ChangeProductUomConversion

AddBarcode
RemoveBarcode

AddProductReference
AddAlias

DefineAttribute
GenerateVariants
```

---

# 113. Queries

```text
GetProduct
GetVariant
SearchProducts
ResolveSku
ResolveBarcode
GetAllowedUoms
ConvertQuantity
GetProductReadiness
```

---

# 114. Contracts

`Todir.Foundation.Catalog.Contracts`

Stable:

```text
GetProductSummary
GetVariantSummary
GetVariantSnapshot

ResolveVariantBySku
ResolveVariantByBarcode

GetAllowedUoms
ValidateUom
ConvertToBaseQuantity

SearchCatalog
```

---

# 115. Product Snapshot

```text
ProductVariantSnapshot
{
  VariantId
  TemplateId
  SKU
  DisplayName
  VariantLabel
  EnteredUom
  BaseUom
  ConversionFactor
}
```

No price/cost/tax truth inside.

---

# 116. Quantity Conversion Service

```text
IQuantityConversion
```

Responsibilities:

- validate Variant.
- validate UOM.
- effective date.
- decimal conversion.
- return factor snapshot.

---

# 117. Transaction Boundary

Create Product:

```text
Template
+ Default Variant
+ Base UOM
+ Audit
+ Outbox
```

same transaction.

---

# 118. Events

Meaningful:

```text
ProductCreatedV1
ProductIdentityChangedV1
ProductArchivedV1
ProductRestoredV1
VariantCreatedV1
VariantChangedV1
ProductUomChangedV1
ProductBarcodeChangedV1
```

---

# 119. RabbitMQ

Use for:

- external sync.
- search enrichment/rebuild.
- image processing.
- imports.
- downstream integration.

Do not use for immediate Product lookup/conversion.

---

# 120. Import Is Mandatory

Product migration is part of product onboarding.

Sources:

- Excel.
- old ERP.
- POS system.
- Odoo.
- ERPNext.
- custom software.

---

# 121. Simple Import

Columns:

```text
SKU
Name
Type
Category
Brand
Base UOM
Barcode
```

---

# 122. Variant Import

```text
Template
Variant SKU
Color
Size
Base UOM
Barcode
```

---

# 123. Multiple UOM Import

```text
Variant SKU
UOM
Factor
Usage
Effective Date
Barcode?
```

---

# 124. Matching Order

1. External reference.
2. SKU.
3. Barcode.
4. Approved legacy mapping.
5. Candidate by name/brand.
6. Create.

No fuzzy automatic overwrite.

---

# 125. Import Preview

```text
Create
Update
Duplicate SKU
Duplicate Barcode
Potential Duplicate
Unknown UOM
Unknown Category
Invalid Conversion
```

---

# 126. Import Performance

No one-request-per-row.

Batch/cache reference validation.

Worker + chunking for large imports.

---

# 127. External References

```text
system_key
external_id
variant_id
```

Use for Shopify/Salla/Zid/Legacy systems later.

---

# 128. Files

Images/Datasheets via Platform Files.

No binary in PostgreSQL.

---

# 129. Image Processing

RabbitMQ worker can:

- resize.
- thumbnail.
- validate.

Save product first; don't block UI on heavy image work.

---

# 130. Localization

Arabic/English names and UOM labels.

RTL.

Documents may snapshot translated description.

---

# 131. Audit

Must capture:

- SKU change.
- Base UOM.
- Conversion change.
- Barcode reassignment.
- Variant deactivation.
- Archive/Restore.
- visibility.

---

# 132. Concurrency

Optimistic versioning.

Two admins don't silently overwrite conversion/SKU.

---

# 133. Barcode Conflict UX

Show:

```text
Barcode already belongs to:
SKU ABC-001
```

No generic DB error.

---

# 134. SKU Conflict UX

Same.

Import reports conflict before commit.

---

# 135. Archive

Archived Variant/Product not selectable in new transaction by default.

History remains.

---

# 136. Hard Delete

Only if never used/referenced.

Otherwise archive.

---

# 137. Product Merge

Do not build broad Merge MVP.

Need Sales/Inventory/Pricing/Purchase impact contracts first.

---

# 138. Product Replacement

Future concept separate from Merge.

Discontinued Product can point to replacement.

Old history unchanged.

---

# 139. Kits / Bundles

Design later.

Not Catalog MVP behavior.

---

# 140. Manufacturing

Catalog provides Variant/UOM.

Manufacturing owns BOM/yield/make-buy.

---

# 141. POS

Catalog already provides:

- Barcode.
- Variant.
- UOM.
- localized name.

POS pricing/tax/stock remains separate.

---

# 142. E-commerce

Catalog provides stable identity/media/translations.

Channel mapping belongs Channels module.

---

# 143. Shipping

Physical dimensions hook designed, build after Shipping requirements.

---

# 144. Product Data Quality

MVP checks:

- Duplicate SKU.
- Duplicate barcode.
- Missing base UOM.
- invalid conversion.
- variant combination conflict.

Later dashboard adds richer checks.

---

# 145. Observability

Metrics:

```text
catalog_search_duration
catalog_barcode_lookup_duration
catalog_import_rows_processed
catalog_import_errors
catalog_variant_generation_duration
catalog_uom_conversion_errors
```

No tenant/product ID as Prometheus label.

---

# 146. Tracing

Critical path later:

```text
Sales UI
→ Product Search
→ Catalog
→ Pricing Batch
→ Inventory Availability Batch
```

---

# 147. Speed Is a Product Requirement

Catalog is one of the most repeated interactions in ERP.

Bad Product Picker = whole ERP feels slow.

Therefore:

- lightweight response.
- proper indexes.
- no N+1.
- pagination.
- lazy detail tabs.
- batch enrichments.
- no huge JSON.

---

# 148. Speed Cannot Sacrifice Integrity

We will not:

- duplicate Products across modules.
- skip RLS.
- trust stale stock/price.
- ignore UOM conversion.
- use unsafe cache.

---

# 149. Speed Cannot Sacrifice UX

We won't force users to know SKU.

Search by:

- name.
- Arabic.
- English.
- barcode.
- references.

---

# 150. Speed Cannot Sacrifice Maintainability

Read models only for real hot workflows.

Not projection for every possible screen.

---

# 151. Architecture Tests

CI rejects:

- Catalog → Inventory implementation.
- Catalog → Sales implementation.
- Catalog → Pricing implementation.
- Domain → Infrastructure.
- cross-module DbContext.

---

# 152. Unit Tests

- SKU normalization.
- UOM conversion.
- effective date.
- variant combination.
- barcode validation.
- archive.
- readiness.

---

# 153. Integration Tests

Real PostgreSQL:

- RLS.
- uniqueness.
- search.
- concurrency.
- UOM effective periods.
- outbox.

---

# 154. Cross-Tenant Tests

Tenant A cannot:

- search B Product.
- resolve B Barcode.
- use B UOM.
- reference B Party.
- load B Variant.

---

# 155. Performance Tests

The following benchmark datasets are deferred to the future Performance Hardening mission by Product Owner decision (2026-10-19). Current Catalog verification uses canonical Development Seed data and moderate realistic database tests.

Datasets:

```text
100k
500k
1m variants
```

Test:

- exact SKU.
- exact barcode.
- Arabic name.
- English name.
- category.
- pagination.
- attributes.

---

# 156. Product Picker Rule

Frontend uses server-side pagination.

No full product download.

MUI Data Grid/List virtualization where needed.

---

# 157. UOM and Pricing

Catalog provides valid UOM + conversion.

Pricing decides whether:

```text
Carton price = 120
```

Price doesn't have to equal Piece price × 24.

---

# 158. UOM and Inventory

Inventory stores base quantity.

Catalog owns conversion contract.

---

# 159. UOM and Purchasing

PurchaseProfile selects default Purchase UOM.

---

# 160. UOM and Sales

SalesProfile selects default Sales UOM.

---

# 161. UOM and Manufacturing

Manufacturing can consume alternate unit.

Waste/yield is manufacturing logic.

---

# 162. Month Is Not 30 Days

Commercial UOM:

```text
Month
```

cannot be globally converted to fixed `30 days`.

Support non-convertible Business Units.

---

# 163. Category != Tax Category

Keep separate.

---

# 164. Category != GL Account

Account determination may later use Category as input, but Catalog doesn't own accounts.

---

# 165. Brand != Supplier

Keep separate.

---

# 166. Goods != Manufactured

A Good may be purchased or manufactured later.

---

# 167. Service != Non-Purchasable

Service may be outsourced/purchased.

---

# 168. Product Usage Summary

Workspace may show:

```text
Sales activity
Purchase activity
Stock
```

through read providers.

No raw cross-schema queries.

---

# 169. Frontend Folder Structure

```text
modules/catalog/
├── api/
├── routes/
├── pages/
│   ├── ProductListPage/
│   ├── ProductWorkspacePage/
│   ├── CategoryPage/
│   ├── UomPage/
│   └── ImportPage/
├── features/
│   ├── create-product/
│   ├── manage-variants/
│   ├── manage-uoms/
│   ├── manage-barcodes/
│   ├── manage-references/
│   └── import-products/
├── components/
│   ├── ProductPicker/
│   ├── VariantPicker/
│   ├── UomSelector/
│   ├── BarcodeInput/
│   └── ProductSummary/
├── permissions/
├── i18n/
└── public.ts
```

---

# 170. Backend Structure

```text
foundation/Catalog/
├── Todir.Foundation.Catalog/
│   ├── Domain/
│   │   ├── Products/
│   │   ├── Variants/
│   │   ├── Units/
│   │   ├── Attributes/
│   │   ├── Barcodes/
│   │   └── Categories/
│   ├── Application/
│   ├── Infrastructure/
│   │   ├── Persistence/
│   │   ├── Search/
│   │   ├── Import/
│   │   └── Messaging/
│   ├── Api/
│   └── ReadModels/
└── Todir.Foundation.Catalog.Contracts/
```



# 171. Product Domain Services

Possible focused services:

```text
SkuNormalizer
BarcodeResolver
QuantityConversionService
VariantCombinationService
ProductReadinessService
CatalogSearchService
```

Avoid one giant `ProductManager`.

---

# 172. Repository Rule

No generic repository.

Use focused repositories only when they add domain value.

Examples:

```text
IProductRepository
IUomRepository
```

or direct `CatalogDbContext` from application handlers where appropriate.

---

# 173. Read Model Rule

Product List/Picker query does not load aggregate.

Use projection.

---

# 174. Base Quantity Contract

Downstream modules should never each invent quantity conversion.

Catalog contract returns:

```text
BaseQuantity
BaseUomId
EnteredUomId
FactorSnapshot
```

---

# 175. Effective-Date Contract

All conversion resolution that may change over time accepts:

```text
effectiveDate
```

not only "current now."

This matters for backdated documents/migrations.

---

# 176. UOM Compatibility

Generic dimensions can enforce compatible conversion.

Custom Product UOM can bridge packaging to Base.

Do not permit arbitrary chain loops.

---

# 177. Conversion Graph

Prefer direct relation to Base:

```text
Piece = 1
Pack = 6
Carton = 24
```

rather than storing:

```text
Carton → Pack → Piece
```

as the only truth.

Direct factor-to-base simplifies correctness and speed.

---

# 178. Conversion Precision

Conversion factor uses Decimal with enough precision.

Example:

```text
1 lb = 0.45359237 kg
```

Do not truncate too early.

Document/business quantity rounding happens according to UOM/module rules.

---

# 179. UOM Code vs Label

Stable internal code:

```text
kg
g
EA
h
```

Localized label:

```text
Kilogram / كيلوجرام
```

Label can change.

Code stability protects integrations.

---

# 180. Custom UOM

Tenant can define:

```text
Tray
Drum
Bundle
Roll
```

If Product-specific packaging, factor belongs `ProductUom`.

---

# 181. Standard UOM Seed

Catalog activation seeds standard units/dimensions.

Seeds versioned.

Never overwrite tenant custom definitions.

---

# 182. Product Activation

Catalog is Foundation, non-purchasable.

Dependent modules can auto-activate it.

Activation:

- validate Kernel.
- apply schema.
- seed standard UOM.
- build search indexes/projection infrastructure.

No fake/demo products in production.

---

# 183. Deactivation

Catalog cannot be disabled while active modules depend on it.

Policy:

```text
BlockedIfDependents
```

Historical data always retained.

---

# 184. Product Go-Live

Directory had party migration; Catalog has product migration.

Go-Live inputs may include:

- current products.
- legacy SKU mapping.
- UOM mapping.
- barcode mapping.
- variant decisions.
- category cleanup.

Opening Stock itself belongs Inventory pack.

Catalog import happens before Opening Stock.

---

# 185. Migration Order Relation

Typical:

```text
UOM
→ Category / Brand
→ Product Templates / Variants
→ Barcodes
→ Product References
→ Inventory Opening Stock later
→ Pricing later
```

---

# 186. Import Reconciliation

Report:

```text
Rows input
Templates created
Variants created
Updated
Skipped
Errors
Duplicate SKU
Duplicate barcode
UOM issues
```

---

# 187. Data Quality — Real World

Common Excel problems:

- SKU with spaces/case differences.
- barcode stored as scientific number.
- leading zero lost.
- same Product repeated per warehouse.
- same Product repeated per UOM.
- Arabic/English duplicated as separate items.
- one row per size/color without template relation.

Importer must not blindly preserve bad structure.

---

# 188. Migration Suggestion vs Automation

System may suggest:

```text
Rows appear to be variants of same product
```

but human confirms.

No AI automatic restructuring of commercial Master Data.

---

# 189. Barcode Storage

Store as text, never numeric.

Why:

- leading zero.
- values can exceed integer range.
- not used in arithmetic.

---

# 190. SKU Storage

Text.

Normalization separate.

Never parse SKU as number.

---

# 191. Product Name Storage

Unicode text.

No forced transliteration.

---

# 192. Category Code

Optional stable code useful in imports.

Do not require it for small company.

---

# 193. Brand Code

Optional.

---

# 194. Product Notes

General internal note okay.

Critical behavior should be structured.

Do not put:

```text
"don't sell below X"
```

as a Note instead of Pricing/Approval rule.

---

# 195. Product Tags

Lightweight tags may be useful later.

Do not use tags instead of Category, Brand, Tax Classification or Profile.

---

# 196. Variant Attributes vs Custom Fields

Variant-driving:

```text
Color
Size
```

→ ProductAttribute.

Informational tenant field:

```text
Season Code
Internal Collection
```

→ Custom Field if appropriate.

---

# 197. Variant Attributes and Localization

Attribute names/values should support translations.

Example:

```text
Color / اللون
Black / أسود
```

---

# 198. Attribute Ordering

Store sort order for user-friendly matrices.

---

# 199. Variant Display Name

Generated from:

```text
Template Name + ordered attribute values
```

But tenant/user may override short display suffix if necessary.

---

# 200. Product Reference and Directory

Supplier/customer references require Party.

Catalog can depend on stable Directory contract/ID according to approved Foundation dependency rules.

Avoid fetching full Party during every Product Picker.

---

# 201. Supplier-specific Product Data

Do not put:

```text
supplier lead time
supplier price
minimum order
```

inside ProductReference.

Purchasing owns Supplier Product Profile/Info.

Reference is identity/mapping only.

---

# 202. Customer-specific Product Data

Customer code/reference can live mapping.

Customer negotiated price belongs Pricing/Sales.

---

# 203. Barcodes and External References

Barcode means machine-readable product identity.

External reference means another business/system's code.

Do not mix both in one "code" table without type/semantics.

---

# 204. Product Category Defaults

Category may provide suggested defaults:

- UOM.
- tax classification later.
- profile templates.

But user must know inherited settings.

No invisible magic that changes posted business behavior.

---

# 205. Defaults vs Ownership

A Category can suggest:

```text
default UOM = Piece
```

but Variant owns resolved product configuration.

Tax still owns tax rule.

---

# 206. Product Readiness Provider

Interface concept:

```csharp
IProductReadinessContributor
```

Modules contribute:

```text
Sales
Purchase
Inventory
POS
Manufacturing
```

Catalog aggregates.

---

# 207. Product Readiness UX

Example:

```text
Ready for Sales       ✓
Ready for Purchase    ✓
Ready for Inventory   ✕ Base inventory profile missing
Ready for POS         ✕ Barcode recommended
```

Warnings vs blockers clearly separated.

---

# 208. POS Barcode Requirement

Not every POS product legally/operationally must have barcode.

POS pack decides whether barcode required.

Catalog only supports it.

---

# 209. Product Code Generation

Tenant setting:

```text
Manual
Auto
Pattern
```

Example:

```text
PRD-000001
```

Use Numbering engine where helpful.

SKU numbering does not require legal gapless semantics.

---

# 210. Variant SKU Pattern

Example:

```text
{TEMPLATE}-{COLOR}-{SIZE}
```

Preview generated SKUs before save.

If generated values collide, block.

---

# 211. Product Reference Search

Contextual search.

In Sales with selected Customer:

customer's reference codes become searchable.

In Purchasing:

supplier reference codes.

Do not add all party references into every result payload unnecessarily.

---

# 212. Product Search Endpoint Context

Request can include:

```text
context = sales
party_id?
legal_entity_id
query
cursor
```

Backend still verifies visibility/permission.

---

# 213. Product Picker with No Inventory

Should work perfectly.

Do not show empty "Stock = 0" if Inventory module inactive.

---

# 214. Product Picker with Inventory

Can show:

```text
Available
```

from Inventory provider.

If Inventory temporarily unavailable internally, Sales pack defines fallback/blocking depending operation.

---

# 215. Product Picker with Pricing

Can show appropriate customer/entity price.

Pricing owns.

---

# 216. Product Picker Complexity

Don't make Catalog endpoint know every module.

Use application composition/read-model orchestrator at consuming module level.

---

# 217. API Response DTOs

Keep explicit.

Do not serialize EF entities.

---

# 218. API Errors

Stable:

```text
catalog.product_not_found
catalog.sku_exists
catalog.barcode_exists
catalog.uom_invalid
catalog.uom_conversion_conflict
catalog.variant_combination_exists
catalog.product_archived
```

---

# 219. Idempotency

Integration/import Product create:

```text
Idempotency-Key
```

plus ExternalReference/SKU uniqueness.

---

# 220. Concurrency Error

Admin edits old version:

```text
409 / 412
catalog.concurrency_conflict
```

UI offers refresh/review.

---

# 221. Audit Reason

High-risk changes require reason:

- Base UOM after use.
- Conversion factor.
- barcode reassignment.
- SKU replacement after integrations.

---

# 222. Product Images UX

Do not load full-resolution images in list.

Thumbnails.

Lazy full image.

---

# 223. Product Image Variants

Template image default.

Variant image override.

---

# 224. Product Attachments

Datasheet/manual/certificate.

Platform Files.

---

# 225. Search Projection Consistency

Exact lookup authoritative from canonical tables.

Fuzzy text projection may be eventually updated but should update core name/SKU synchronously where feasible.

---

# 226. Projection Rebuild

Provide background command:

```text
catalog search rebuild
```

idempotent.

Useful after index/search algorithm updates.

---

# 227. Read Model Version

Track projection version/schema.

Deployment can rebuild safely.

---

# 228. OpenSearch Decision Gate

Only introduce if Postgres search no longer meets validated UX requirements due:

- scale.
- advanced Arabic relevance.
- fuzzy multi-field needs.
- operational load.

Not because "ERP should have search engine."

---

# 229. Product History

Posted document displays snapshot.

Click can navigate current Product.

Name change does not rewrite history.

---

# 230. Product Availability History

Inventory owns.

---

# 231. Price History

Pricing owns.

---

# 232. Cost History

Inventory valuation owns.

---

# 233. Tax History

Transaction tax snapshot owned by transaction module.

---

# 234. Custom Product Fields and Search

Not every Custom Field becomes searchable.

Definition can mark:

```text
Searchable = true
```

with limits.

Indexing strategy controlled by platform/catalog.

Avoid tenant causing hundreds of indexes.

---

# 235. Custom Field Limits

Set sensible per-entity limit.

Exact limit later based on product tier/performance.

---

# 236. Product Data API for AI

AI tools can:

```text
SearchProducts
GetProductSummary
SuggestClassification
DraftProductFromDocument
```

No direct DB.

---

# 237. AI Restrictions

AI cannot autonomously:

- change UOM factor.
- reassign barcode.
- archive heavily used product.
- merge products.
- create 1,000 variants without review.

---

# 238. OCR Use

Possible later:

- supplier catalog.
- barcode.
- product label.

Creates Draft suggestions.

---

# 239. Product Duplicate Detection

Strong signals:

```text
same barcode
same GTIN
same SKU
```

Medium:

```text
same brand/model
same supplier ref
name similarity
```

Warnings with reasons.

---

# 240. No AI Duplicate Score Alone

Display:

```text
Possible duplicate because:
✓ Same barcode
✓ Similar name
```

not only:

```text
93% match
```

---

# 241. Product Merge Later Gate

Before merge capability:

Sales, Inventory, Purchasing, Pricing must define:

- mutable refs.
- posted snapshots.
- stock balance.
- open documents.
- barcodes.
- price rules.

Until then:

Archive + Alias/Replacement.

---

# 242. Product Archive Impact

Before archive show:

```text
Open Sales Orders?
Open Purchase Orders?
Current Stock?
Active Price Rules?
BOM use later?
```

Contributors from modules.

MVP without these modules simply archives.

---

# 243. Product Restore

Restore existing product rather than create duplicate.

Run duplicate checks.

---

# 244. Category Archive

Cannot orphan Products invisibly.

Options:

- block until reassigned.
- archive while retaining products/category path historical.

Prefer block/reassign for active products.

---

# 245. UOM Archive

Cannot archive if current Base UOM or active conversion.

---

# 246. Attribute Archive

Cannot break active Variant identity.

May deactivate from future templates while retaining existing values.

---

# 247. Brand Archive

Existing Products preserve relation/display.

Brand hidden from new assignment.

---

# 248. Technical Data Ownership

Catalog owns only its schema.

No direct SQL write from other modules.

---

# 249. Stable Foundation FK

According to Master Architecture, business modules may keep stable:

```text
product_variant_id
```

while still not using ORM navigation across modules.

---

# 250. Product Variant ID in Transactions

Sales line:

```text
variant_id
```

Purchase line:

```text
variant_id
```

Stock move:

```text
variant_id
```

Same canonical ID.

---

# 251. Product Template ID Usage

Reporting/grouping/configuration.

Transactions normally reference Variant.

---

# 252. Service Variant

Same rule.

No special ServiceLine ID path.

---

# 253. Quantity Value Object

Shared/Catalog contract should use a value shape:

```text
quantity
uom_id
```

and resolved base quantity.

Do not pass raw decimal with no UOM when business semantics require quantity.

---

# 254. Money Separation

Catalog Quantity is not Money.

FinBase later defines Money.

No price calculation inside quantity converter.

---

# 255. Time Separation

Effective-dated UOM uses business date via NodaTime.

---

# 256. Product Search and Entity Scope

Filter before returning results.

Do not return then hide in frontend.

---

# 257. Product Search and Permission

If user lacks product read, deny.

If user can Sales-select but not Catalog-admin, expose safe picker data only.

---

# 258. Scoped Data DTO

Catalog admin DTO can include more fields than Sales picker DTO.

Avoid overexposure.

---

# 259. Small Business Defaults

For small tenant:

- auto SKU optional.
- Default category optional.
- default UOM = Piece maybe configurable.
- variants hidden by default.

Keep onboarding simple.

---

# 260. Distributor Defaults

Can enable:

- multiple UOM.
- barcodes.
- brand/category.
- product references.

---

# 261. Fashion Defaults

Variants/attributes prominent.

---

# 262. Service Company Defaults

Goods inventory fields hidden.

---

# 263. UX Personalization

User can save:

- preferred columns.
- filters.
- recent products.

Does not change permission.

---

# 264. Favorites/Recent Products

Safe optimization for pickers.

Keep tenant/user scoped.

---

# 265. Product List Counts

Do not run expensive exact total count on every search if table grows huge.

Use:

- next cursor.
- approximate/optional total where UX allows.

---

# 266. Keyset Pagination

Prefer keyset for deep browsing large catalogs.

Offset acceptable for small admin views if measured.

---

# 267. Sorting

Indexed sorts:

```text
Name
SKU
Recently Updated
```

Avoid arbitrary expensive sort on unindexed dynamic fields by default.

---

# 268. Export Sorting

Large export background job can do heavier work separately.

---

# 269. Product Picker Network Payload

Keep compact.

Images thumbnail optional.

No embedded binary.

---

# 270. Compression

HTTP compression can be used, but don't use it as excuse for oversized DTO.

---

# 271. Frontend Query Cache

TanStack Query.

Keys include:

```text
tenant
entity
search/filter
```

On tenant/entity switch clear relevant caches.

---

# 272. Mutation Invalidation

Product update invalidates:

- detail.
- list.
- picker/search relevant entries.

---

# 273. Optimistic UI

Avoid optimistic update for:

- UOM conversion.
- barcode.
- SKU.
- variant generation.

Server confirmation first.

---

# 274. Accessible Product Picker

- keyboard.
- scanner input.
- clear labels.
- RTL.
- screen-reader result context.

---

# 275. Scanner Handling

Barcode scanner behaves as keyboard.

Frontend can detect fast input pattern only if reliable.

Server resolver remains authority.

---

# 276. Barcode Resolver Response

```text
VariantId
SKU
Name
UomId
UomCode
factor
```

Context modules enrich price/stock.

---

# 277. Unknown Barcode UX

Offer if permission:

```text
No product found.
[Search manually]
[Add barcode to existing product]
[Create product]
```

Context-sensitive.

---

# 278. Duplicate Barcode UX

Should never normally happen due constraint.

Migration/admin conflict surfaced explicitly.

---

# 279. Product Import UX

Wizard:

```text
Upload
→ Map
→ Normalize
→ Validate
→ Variant grouping suggestions
→ UOM mapping
→ Duplicate review
→ Preview
→ Import
→ Reconcile
```

---

# 280. Import Dry Run

Must not mutate canonical data.

---

# 281. Import Rollback

Before products are referenced, safe batch rollback may be possible.

After use, use archive/corrective process.

---

# 282. Import Audit

Batch ID attached to created/updated entities where useful.

---

# 283. Product Source Tracking

Created by:

```text
Manual
Import
API
Integration
```

Useful metadata/audit, not business status.

---

# 284. Supplier Catalog Import Later

Purchasing integration can map supplier feed to existing Catalog via ProductReference.

Don't let supplier feed become canonical automatically.

---

# 285. E-commerce Sync Later

Channels module maps:

```text
ExternalVariantId ↔ ProductVariantId
```

Catalog provides identity.

---

# 286. POS Offline Future

Stable UUID/SKU/barcode supports sync.

Do not implement offline now.

---

# 287. Performance Testing Harness

Create seeded datasets with realistic distributions:

- 80% simple products.
- 10% multiple UOM.
- 10% variants.
- Arabic/English mix.
- category hierarchy.
- multiple barcodes.

Not only synthetic sequential names.

---

# 288. Search Test Queries

Include:

```text
exact SKU
prefix SKU
Arabic partial
English partial
barcode
alias
brand
misspelling
```

Record p50/p95/p99.

---

# 289. Database Query Plans

Capture `EXPLAIN ANALYZE` for critical searches during performance gate.

---

# 290. Slow Query Regression

CI/nightly performance can compare baseline.

Don't make every PR run million-row load test if too expensive; use scheduled performance pipeline.

---

# 291. Catalog Operational Runbook

Need:

- rebuild search projection.
- resolve duplicate barcode.
- repair broken category.
- resume failed import.
- investigate slow picker.

---

# 292. Security Runbook

Cross-tenant search issue = severity critical.

Immediate disable/incident procedure.

---

# 293. Product Change Webhooks Later

Integrations module can subscribe to Catalog events and dispatch signed webhooks.

Catalog doesn't call external webhooks directly.

---

# 294. Product Event Schema Versioning

Breaking event → new version.

No changing consumers silently.

---

# 295. Message Idempotency

Projection/integration consumers track message IDs.

Replay safe.

---

# 296. Product Search Projection Consumer

If async enrichment consumer receives duplicate:

upsert by Variant/version.

No duplicate rows.

---

# 297. Product Event Ordering

Consumers should use entity version where stale events could arrive later.

Don't assume global broker ordering.

---

# 298. Catalog Version Field

Optimistic concurrency/version also helps projection ordering.

---

# 299. Performance and Audit

Audit writes should not make every Product Picker slow.

Picker is read-only and doesn't write Audit.

Mutations write Audit transactionally/appropriately.

---

# 300. Performance and RLS

Never remove RLS for speed.

Optimize indexes/query shape.

Security is non-negotiable.

---

# 301. Performance and Normalization

Do not denormalize canonical Product across modules.

Denormalize read projections only.

---

# 302. Performance and Images

Thumbnails + CDN/signed URLs.

No original full image in list query.

---

# 303. Performance and Attributes

Do not render every attribute in list.

Variant label includes selected identity attributes.

---

# 304. Performance and Categories

Cache tree/reference metadata.

---

# 305. Performance and UOM

UOM reference set small enough for controlled cache.

Invalidate by version.

---

# 306. Testing — Unit

Required:

- UOM factor.
- decimal rounding.
- effective date.
- combination signature.
- SKU normalization.
- barcode normalization.
- Readiness.
- archive.

---

# 307. Testing — DB Integration

Required:

- RLS.
- unique SKU.
- unique barcode.
- variant combination.
- UOM interval constraints.
- search index.
- concurrency.
- import.

---

# 308. Testing — Messaging

- event only after commit.
- broker outage retained Outbox.
- projection consumer duplicate safe.

---

# 309. Testing — E2E Service

Create Service:

```text
Site Inspection
Visit
```

Fast path should not show inventory settings.

---

# 310. Testing — E2E Distributor

Create:

```text
Water 500ml
Piece
Pack 6
Carton 24
```

Scan Carton.

Conversion correct.

---

# 311. Testing — E2E Fashion

Create Template + 4 Variants.

Search one SKU.

Group under same Template.

---

# 312. Testing — E2E References

Supplier and Customer codes resolve canonical Variant.

---

# 313. Testing — E2E Arabic

Arabic alias/name search.

RTL workspace.

---

# 314. Testing — E2E Permission

Sales user can search/select but cannot change UOM factor.

---

# 315. Testing — E2E Multi-role

Small-company owner with Sales + Purchase + Inventory permissions sees combined useful experience from one account.

---

# 316. Testing — E2E Multi-Entity

Product available A, hidden B.

---

# 317. Testing — E2E Import

Import thousands, duplicate SKU prevented, UOM mapped.

---

# 318. Testing — E2E Archive

Archive product.

Old record remains readable.

New selection excludes it.

---

# 319. MVP Scope — LOCKED

Build:

- Template.
- Default Variant.
- Explicit Variants.
- Goods/Service.
- SKU.
- Category.
- Brand.
- UOM dimensions/definitions.
- Product UOM conversion.
- effective dates.
- multiple/UOM-specific barcodes.
- aliases.
- translations.
- customer/vendor references.
- visibility.
- search projection.
- Product Picker.
- Quick Create.
- Workspace.
- Import/Export.
- Archive/Restore.
- readiness.
- audit.
- events/contracts.
- performance tests.

---

# 320. Design Now / Build Later

- physical packaging dimensions.
- replacement/substitution.
- advanced PIM metadata.
- richer image workflows.
- product duplicate merge.
- GS1 integrations.
- advanced bulk category transforms.

---

# 321. Do Not Build Yet

- Pricing engine.
- Inventory stock ledger.
- cost.
- tax engine.
- BOM.
- lot/serial.
- Warehouse.
- CPQ.
- recommendation engine.
- Marketplace sync implementation.
- OpenSearch.
- Redis dependency.
- Product microservice.

---

# 322. Product Risk Register

## Risk — God Product

Mitigation: module profiles.

## Risk — Slow Catalog

Mitigation: projection/index/pagination/lazy loading.

## Risk — UOM corruption

Mitigation: base quantity + effective dating + snapshots.

## Risk — Variant explosion

Mitigation: preview/controlled generation.

## Risk — Dirty imports

Mitigation: mapping/preview/reconciliation.

## Risk — Duplicates

Mitigation: SKU/barcode uniqueness + candidate detection.

---

# 323. Senior Product Review Questions

Before approval ask:

1. هل Service user شايف شاشة بسيطة؟
2. هل distributor قادر يتعامل carton/pack/piece؟
3. هل fashion variants سهلة؟
4. هل Product Picker سريع؟
5. هل السعر منفصل؟
6. هل cost منفصل؟
7. هل stock منفصل؟
8. هل UOM history محفوظ؟
9. هل supplier/customer codes مدعومة؟
10. هل multi-company لا يكرر Products؟
11. هل import واقعي؟
12. هل أي screen بتعمل N+1؟

---

# 324. Product Gate Before Next Pack

لا نقفل Catalog قبل الموافقة على:

- Template/Variant.
- default Variant.
- Goods/Service.
- SKU.
- UOM model.
- effective conversion.
- Barcode/UOM relation.
- Product References.
- Entity visibility.
- Search strategy.
- Performance budgets.
- Import.
- boundaries with Pricing/Tax/Inventory.

---

# 325. Implementation Order

```text
C00 Manifest + Module Shell
C01 Schema + RLS
C02 UOM Master
C03 Template + Default Variant
C04 SKU + Translation + Alias
C05 Category + Brand
C06 Product UOM + Conversion
C07 Barcode
C08 Attributes + Variants
C09 Product References
C10 Entity Visibility
C11 Search Projection
C12 Product Picker
C13 Product Workspace
C14 Quick Create
C15 Readiness
C16 Import/Export
C17 Outbox + Events
C18 Cross-Module Contracts
C19 Performance/Security Hardening
C20 Product Acceptance
```

---

# 326. C00 Exit Gate

- Manifest.
- permissions.
- backend/frontend shell.
- architecture CI.

---

# 327. C01 Exit Gate

- RLS forced.
- runtime role.
- cross-tenant tests.

---

# 328. C02 Exit Gate

- standard/custom UOM.
- dimensions.
- fractional behavior.

---

# 329. C03 Exit Gate

Create simple Product atomically produces:

```text
Template
Default Variant
Base UOM
```

---

# 330. C04 Exit Gate

- unique SKU.
- translations.
- alias search.

---

# 331. C05 Exit Gate

- category hierarchy.
- brand.
- no recursive query explosion.

---

# 332. C06 Exit Gate

```text
Piece
Pack=6
Carton=24
```

converts correctly.

Future factor works by effective date.

---

# 333. C07 Exit Gate

UOM-specific barcode resolves correct UOM.

---

# 334. C08 Exit Gate

Variant matrix works.

Duplicates impossible.

---

# 335. C09 Exit Gate

Customer/vendor references work.

---

# 336. C10 Exit Gate

Entity visibility enforced backend/DB/application.

---

# 337. C11 Exit Gate

Realistic Development Seed search and picker checks at moderate data volume.

Tenant isolation, bounded pages, and query behavior are verified on PostgreSQL.

Product Owner decision (2026-10-19): certification at 100k, 500k, and 1m variants is deferred to a future Performance Hardening mission. Do not generate scale fixtures or run large-scale benchmarks as part of Catalog backend completion. Preserve pagination, tenant-scoped queries, exact identifier lookups, absence of N+1 behavior, and query-plan review as normal performance requirements.

---

# 338. C12 Exit Gate

Picker:

- keyboard.
- pagination.
- search.
- scanner.
- no N+1.
- lightweight payload.

---

# 339. C13 Exit Gate

Workspace initial load doesn't fetch every tab/module profile.

---

# 340. C14 Exit Gate

Simple Service creation target:

```text
< 30–60 sec product UX target
```

validated with usability, not just code.

---

# 341. C15 Exit Gate

Readiness shows actionable missing data.

---

# 342. C16 Exit Gate

Large import rehearsal + reconciliation.

---

# 343. C17 Exit Gate

Outbox/replay/idempotent projection.

---

# 344. C18 Exit Gate

Stable Catalog Contracts published.

No downstream implementation coupling.

---

# 345. C19 Exit Gate

- Moderate Development Seed runtime checks for search, picker, query bounds, and query plans.
- Security, cross-tenant isolation, and ordinary concurrency checks.
- 100k/500k/1m scale certification is explicitly deferred by Product Owner to Performance Hardening; it is outside the current Catalog completion gate.

---

# 346. C20 Product Acceptance Demo

Demo must include:

```text
1 simple Service
1 simple Good
1 distributor item with Piece/Pack/Carton
1 fashion Product with Variants
Arabic/English names
customer/supplier references
import
multi-entity visibility
```

If simple use case feels complicated, not approved.

---

# 347. Senior Product Verdict

The Catalog is **not an Inventory Item table**.

It is:

> **The canonical identity and quantity-definition layer for anything the business sells, buys, stocks, manufactures, or delivers as a service.**

The final ownership is:

```text
Catalog
  Product identity
  Variant identity
  SKU
  Category
  Brand
  UOM + conversion
  Barcode
  Alias / Translation
  External product reference
```

Not Catalog:

```text
Price       → Pricing
Stock       → Inventory
Cost        → Inventory Valuation
Tax         → Tax
Sales policy→ Sales
Supplier terms → Purchasing
BOM         → Manufacturing
```

Final transaction reference:

> **ProductVariantId**

even for simple products.

Final UX rule:

> **Hide complexity until the business actually needs it.**

Final quantity rule:

> **Normalize to Base UOM, but preserve the user's entered UOM and factor snapshot.**

Final performance rule:

> **A list/picker never loads the full Product graph.**

Final scale rule:

> **Tenant-scoped + indexed + paginated + no N+1 + batch enrichment.**

Final product/architecture balance:

> **We gain speed through read models and query design, never by duplicating source-of-truth data or weakening integrity/security.**

---

# 348. Current Competitor Evidence — Checked 2026-10-05

## Odoo

Official UOM documentation:

https://www.odoo.com/documentation/20.0/applications/inventory_and_mrp/inventory/product_management/configure/uom.html

Supports multiple UOM for product and conversion use cases.

## Microsoft Business Central

Variants:

https://learn.microsoft.com/en-gb/dynamics365/business-central/inventory-item-variants

Units:

https://learn.microsoft.com/en-gb/dynamics365/business-central/inventory-how-setup-units-of-measure

Item references:

https://learn.microsoft.com/en-ca/dynamics365/business-central/inventory-how-use-item-cross-refs

Relevant patterns:

- variants under a common Item.
- variant-specific attributes.
- Base UOM + alternate UOM.
- customer/vendor references with UOM/variant.

## ERPNext

Item:

https://docs.frappe.io/erpnext/item

UOM:

https://docs.frappe.io/erpnext/uom

Barcode:

https://docs.frappe.io/erpnext/track-items-using-barcode

Relevant patterns:

- items/services.
- groups.
- alternate UOM.
- barcode/UOM-specific barcode.
- variants.

## Daftra

Product/Service:

https://docs.daftra.com/en/user_manual/adding-a-new-product-or-a-new-service/

Multiple UOM:

https://docs.daftra.com/en/user_manual/multi-measurement-units-in-the-system-and-linking-them-to-products/

Relevant patterns:

- products/services.
- category/brand.
- multiple measurement units.
- different selling/purchasing units.
- multiple barcodes.

---

# 349. Next File

The next pack should be:

> **`06_ERP_SAAS_FINBASE_CURRENCY_PAYMENT_TERMS_MONEY_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`**

Business question:

> **إزاي النظام يمثل الفلوس والعملات وأسعار الصرف وشروط الدفع والقواعد المالية المشتركة بشكل واحد صحيح قبل ما نبني Pricing وTax وLedger وInvoicing؟**

Expected sequence after that:

```text
06 FinBase / Money / Currency / Payment Terms
07 Pricing
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

The numbering remains subject only to an explicit approved architecture ADR, not ad-hoc renaming during implementation.

---

**END OF FILE — 05_ERP_SAAS_CATALOG_PRODUCT_UOM_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md**

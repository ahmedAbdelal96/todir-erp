# 04 — ERP SaaS Directory / Party Requirements & Technical Design Pack

**File:** `04_ERP_SAAS_DIRECTORY_PARTY_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`  
**Status:** PRODUCT + DOMAIN + TECHNICAL BASELINE  
**Version:** v1.0  
**Date:** 2026-10-05  
**Audience:** Product Owner, Senior Product Designer, Business Analyst, Backend Agent, Frontend Agent, QA, Data Migration Team  
**Depends on:**  
- `01_ERP_SAAS_MASTER_MODULE_AND_DEPENDENCY_BLUEPRINT.md`
- `02_ERP_SAAS_PRODUCTION_READY_TECH_STACK_AND_PROJECT_STRUCTURE.md`
- `03_ERP_SAAS_PLATFORM_KERNEL_AND_MODULE_SYSTEM_TECHNICAL_DESIGN_PACK.md`

---

# 0. لماذا هذا الموديول موجود أصلًا؟

أي ERP ناجح يحتاج إجابة صحيحة على سؤال بسيط جدًا:

> **"إحنا بنتعامل مع مين؟"**

الشركة لا تتعامل فقط مع "عميل" و"مورد".

في الواقع قد تتعامل مع:

- شركة تشتري منها.
- شركة تبيع لها.
- شركة تفعل الاثنين.
- فرع من فروع عميل.
- شخص مسؤول مشتريات داخل شركة.
- شخص مسؤول حسابات.
- شخص مسؤول استلام.
- شركة شحن.
- موزع.
- وكيل.
- مقاول.
- جهة حكومية.
- مكتب محاسبة.
- شركة شقيقة.
- شخص فردي وليس شركة.
- Lead محتمل لم يتحول بعد إلى عميل رسمي.
- عميل توقف عن التعامل ولكن يجب الاحتفاظ بتاريخه.

لو صممنا النظام على أساس:

```text
Customers Table
Suppliers Table
Contacts Table
```

بشكل منفصل ومن غير نموذج موحد، هنخلق مشاكل طويلة المدى:

- نفس الشركة تتكرر أكثر من مرة.
- الرقم الضريبي يختلف بين Sales وPurchasing.
- عنوان العميل في Sales يختلف عن Accounting.
- لو العميل أصبح موردًا نكرر نفس البيانات.
- التقارير لا تعرف أن السجلين لنفس الجهة.
- التكاملات تعاني من Duplicate IDs.
- الـMigration من Excel ينتج آلاف السجلات المكررة.
- المستخدم لا يعرف أي Card هي الصحيحة.
- تغيير اسم الشركة يتطلب تحديث أكثر من مكان.

لهذا القرار الأساسي للمشروع هو:

> **Directory = الهوية الموحدة لكل طرف خارجي.**

و:

> **Party = الكيان الأساسي الذي يمثل شخصًا أو مؤسسة نتعامل معها.**

ثم نضيف له أدوارًا:

```text
Customer
Supplier
Carrier
Contractor
Partner
Other
```

ولا ننشئ هوية جديدة لنفس الجهة لمجرد أن دورها تغير.

---

# 1. Product Vision

نريد Directory يشعر المستخدم أنه:

> "دفتر علاقات الشركة الذكي"

وليس:

> "شاشة Master Data ثقيلة."

الهدف:

- أسرع إنشاء عميل/مورد ممكن.
- أقل إدخال بيانات يدوي ممكن.
- منع التكرار قبل أن يحدث.
- نفس الجهة تستخدمها كل الموديولات.
- دعم شركات وفروع وأشخاص اتصال متعددين.
- دعم عربي/إنجليزي.
- دعم اختلاف العنوان القانوني/الفوترة/الشحن.
- تهيئة ممتازة للهجرة من Excel.
- عدم إجبار مندوب المبيعات على إدخال حقول محاسبية لا يعرفها.
- عدم السماح بعملية قانونية ببيانات ناقصة.
- دعم النمو من شركة صغيرة إلى مجموعة شركات بدون إعادة تصميم.

---

# 2. Business Principle — الهوية شيء والسياسة شيء آخر

أهم قاعدة في الموديول:

> **Directory owns identity, not every business policy.**

Directory يملك:

```text
من هو الطرف؟
كيف نتواصل معه؟
أين يقع؟
ما هويته القانونية الأساسية؟
```

لكن لا يملك:

```text
كيف نبيع له؟
كيف نشتري منه؟
ما حد الائتمان؟
ما قائمة الأسعار؟
ما حسابه المحاسبي؟
ما شروط المورد؟
```

هذه مملوكة للموديولات المتخصصة.

مثال:

```text
Party: ABC Trading
```

Directory يملك:

```text
Legal Name
Trade Name
Tax Identifier
Contacts
Addresses
Phone
Email
Registration Identifiers
```

Sales يملك:

```text
Salesperson
Default Pricelist
Commercial Segment
Sales Terms
Customer Product Codes
```

Purchasing يملك:

```text
Supplier Lead Time
Supplier Product Codes
Preferred Buying Currency
Supplier Terms
```

Subledger / Finance profile يملك:

```text
Credit Limit
Payment Terms
Receivable / Payable relationship policy
Credit Hold
```

هذه القاعدة تمنع "God Customer".

---

# 3. Real-World Business Scenarios

الموديول يجب أن ينجح في السيناريوهات التالية.

## Scenario A — محل صغير / فرد

```text
Ahmed Hassan
Mobile: ...
Customer
No Tax ID
One delivery address
```

المستخدم يجب أن ينشئه في ثوانٍ.

لا نعرض له:

- VAT setup معقد.
- Supplier settings.
- Accounting mapping.
- 20 Tabs.

---

## Scenario B — شركة B2B

```text
Al Noor Trading LLC
Customer
Tax ID
Commercial Registration
HQ Address
Billing Address
2 Delivery Addresses
Purchasing Contact
Accounts Payable Contact
Warehouse Contact
```

المندوب يستخدم Purchasing Contact.

الفاتورة تذهب إلى Accounts Payable.

الشحن يذهب إلى Warehouse Address.

---

## Scenario C — عميل ومورد في نفس الوقت

شركة تقدم لنا خدمة، وفي نفس الوقت تشتري منتجاتنا.

الصحيح:

```text
Party
  Roles:
    Customer
    Supplier
```

وليس:

```text
Customer ABC
Supplier ABC
```

بسجلين منفصلين.

---

## Scenario D — مجموعة فروع للعميل

مثال:

```text
Cairo HQ
Alex Branch
Mansoura Branch
10th of Ramadan Warehouse
```

كلها قد تشترك في نفس Legal Customer.

بعض الطلبات:

```text
Bill To = Cairo HQ
Ship To = 10th Ramadan Warehouse
```

لا ننشئ Customer منفصل لكل عنوان إلا إذا كان هناك **كيان فوترة قانوني مستقل**.

---

## Scenario E — شركة بها عدة أشخاص اتصال

```text
Mohamed — Purchasing
Sara — Finance
Omar — Receiving
Mona — General Manager
```

لكل شخص:

- Phone.
- Email.
- Job title.
- Department.
- Preferred communication.
- Active/Inactive.

المستند يختار Contact المناسب حسب Workflow.

---

## Scenario F — تغيير موظف داخل العميل

مسؤول المشتريات ترك الشركة.

الصحيح:

```text
Contact = Inactive
```

وليس حذف الشخص من التاريخ.

المستندات القديمة تظل تشير إلى Snapshot أو Historical Reference.

---

## Scenario G — العميل غير اسمه التجاري

الشركة:

```text
Old Trade Name → New Trade Name
```

لكن نفس الكيان القانوني.

نحدث الاسم.

المستندات المرحّلة لا تتغير بأثر رجعي.

---

## Scenario H — Duplicate

موظف 1 أنشأ:

```text
النور للتجارة
```

موظف 2 أنشأ:

```text
Al Noor Trading
```

ونفس الرقم الضريبي.

النظام يجب أن يوقف/يحذر قبل الإنشاء.

---

## Scenario I — Excel Migration

العميل عنده:

```text
7,000 customers
2,500 suppliers
```

في Excel.

قد نجد:

- أرقام ضريبية ناقصة.
- نفس الشركة 3 مرات.
- أسماء عربية/إنجليزية مختلفة.
- هواتف بصيغ مختلفة.
- عناوين داخل خلية واحدة.
- مورد وعميل منفصلين لنفس الشركة.

Directory يجب أن يكون جاهزًا للمشكلة دي من البداية.

---

## Scenario J — Multi-Legal-Entity Tenant

Tenant فيه:

```text
Company A
Company B
```

نفس Party قد يتعامل مع الاثنين.

لكن:

- Company A تبيع له.
- Company B تشتري منه.
- شروط الدفع تختلف.
- الرصيد المالي مختلف.
- بعض المستخدمين لا يجب أن يروا العلاقة مع Company B.

لذلك:

> Party identity can be tenant-level, while business profiles are entity-scoped.

---

# 4. Competitor Lessons

## 4.1 Odoo

Odoo يعامل Contacts كمستودع مركزي للجهات، ويدعم Person/Company وربط الأشخاص بالشركات. كما يدعم اختلاف Invoice Address عن Delivery Address.

الدرس المفيد:

- Contact/Party مركزي.
- تعدد العناوين طبيعي.
- Person مرتبط بـCompany.
- Sales/Invoice/Delivery يمكن أن يستخدموا عناوين مختلفة.

ما لا نريد نسخه:

- شاشة عامة ضخمة تجمع حقول من Apps كثيرة.
- ظهور إعدادات لا تخص المستخدم.
- Coupling يجعل Contact Form تكبر كلما أضفنا Module.

قرارنا:

> نفس القوة، لكن Module Profiles والـProgressive Disclosure تمنع تضخم الشاشة.

---

## 4.2 Microsoft Dynamics 365 Business Central

Business Central يفرق بين Contact وبين Customer/Vendor، ويمكن تحويل Contact إلى علاقة رسمية أو ربطه بها، ويحتوي على duplicate handling.

الدرس:

- Prospect/Contact ليس بالضرورة Customer جاهز للمعاملات.
- التحويل من علاقة أولية إلى Business Relation عملية مهمة.
- Duplicate merge ضروري في Master Data.

قرارنا:

> CRM Lead لا يصبح Party تلقائيًا حتى التأهيل، ولكن Directory Party نفسه يمكن أن يأخذ Customer/Supplier Role بدون إنشاء هوية جديدة.

---

## 4.3 ERPNext

ERPNext يفصل:

- Customer.
- Supplier.
- Address.
- Contact.

العناوين والأشخاص يمكن ربطهم بالأطراف، والعنوان الواحد قد يكون Billing/Shipping/Office/Branch.

الدرس:

- لا تخزن Address في Customer row فقط.
- Contacts متعددة لكل جهة.
- Primary Contact/Address مفيد كـdefault.

قرارنا:

> نحتفظ بفصل Address/Contact ككيانات، لكن Customer/Supplier يصبحان Roles/Profiles فوق Party بدل هويتين مستقلتين.

---

## 4.4 Zoho Books

Zoho يفصل Customers/Vendors لكنه يدعم ربط Customer مع Vendor عندما يكون نفس الطرف يؤدي الدورين.

الدرس:

> السوق يحتاج هذه الحالة فعلًا، وليست Edge Case نظرية.

قرارنا:

> لا نحتاج Link بين سجلين لأننا لن ننشئ سجلين من الأصل.

---

## 4.5 Daftra

دفترة يعكس احتياجات سوق عربي عملية:

- المورد لديه Business Name.
- Representative.
- Phone/Mobile.
- Address.
- Country.
- Commercial Registration.
- VAT Number.
- Secondary Address.
- Additional Contacts.
- Currency.
- Import.

الدرس:

> الحقول القانونية والعناوين المتعددة والاستيراد ليست "Enterprise extras"؛ هي احتياجات حقيقية حتى للشركات المتوسطة.

قرارنا:

- دعمها.
- لكن لا نحشرها كلها في Quick Create.
- نظهرها عند الحاجة.

---

# 5. Final Domain Decision

الكيانات الأساسية:

```text
Party
PartyRole
PartyIdentifier
ContactPerson
ContactPoint
Address
PartyAddressLink
PartyContactLink
PartyRelationship
PartyMergeRedirect
PartyAlias
PartyVisibilityScope
```

لاحقًا Module-specific:

```text
SalesCustomerProfile
SupplierProfile
BillingProfile
CreditProfile
CRM relationship
Portal association
```

---

# 6. Party Types

الـParty يمثل:

```text
Organization
Individual
```

لا نضع:

```text
Customer
Supplier
```

في `party_type`.

Customer/Supplier = Roles.

---

# 7. Party Entity

## Core Fields

```text
id
tenant_id

party_type
display_name

legal_name?
trade_name?
name_ar?
name_en?

status

country_code?
primary_language?
preferred_communication_language?

created_at
updated_at
version
```

## Individual Fields

عندما Party = Individual:

```text
first_name?
middle_name?
last_name?
```

لا نطلب كل الاسم المفصل إجباريًا في الأسواق التي تستخدم اسمًا كاملًا حرًا.

`display_name` هو ما يظهر للمستخدم.

---

# 8. Party Status

```text
Draft
Active
Blocked
Archived
Merged
```

## Draft

تم إنشاؤه Quick Create ولم تكتمل بياناته.

## Active

صالح للاستخدام.

## Blocked

الهوية موجودة لكن استخدامه في معاملات جديدة مقيد.

Block ليس حذفًا.

سبب الحظر غالبًا يأتي من Module Policy.

Directory يحفظ حالة عامة فقط عند الحاجة الإدارية.

## Archived

لا يظهر افتراضيًا في الاختيار الجديد.

التاريخ محفوظ.

## Merged

السجل Redirect لسجل Master.

---

# 9. Party Roles

Initial roles:

```text
Customer
Supplier
Carrier
Contractor
BusinessPartner
GovernmentEntity
Other
```

لا نحاول بناء 30 Role من البداية.

## Role record

```text
party_id
role_code
active_from
active_to?
status
created_at
```

## Rule

إضافة Role لا تنشئ Party جديد.

---

# 10. Role Ownership

Directory owns:

```text
PartyRole flag / relationship identity
```

لكن Module owns:

```text
role-specific commercial profile
```

مثال:

Customer Role يقول:

> هذا الطرف يمكن استخدامه كعميل.

Sales Profile يقول:

> كيف نبيع له؟

---

# 11. Party Identifier

الأرقام القانونية لا توضع كأعمدة ثابتة لكل دولة.

استخدم:

```text
PartyIdentifier
```

Fields:

```text
id
tenant_id
party_id
identifier_type
country_code
value
normalized_value
issuing_authority?
valid_from?
valid_to?
is_primary
verification_status?
```

Types أمثلة:

```text
TaxId
VATRegistration
CommercialRegistration
NationalId
Passport
GovernmentVendorNumber
Custom
```

لكن Country Pack يحدد:

- ما الأنواع الموجودة.
- ما المطلوب.
- Validation format.
- هل uniqueness mandatory أم warning.
- متى يصبح Required.

---

# 12. Tax ID Rule

لا نفرض رقم ضريبي على كل Party.

Product rule:

> **Collect legal data only when the workflow requires it.**

مثال:

Quick Customer:

```text
Name
Phone
```

ثم عند إصدار مستند B2B يحتاج Tax ID حسب البلد:

النظام يقول:

```text
"أكمل بيانات الفوترة القانونية قبل الإصدار."
```

هذا أفضل من إجبار المندوب على إدخال بيانات لا يعرفها وقت إنشاء Lead/Customer.

---

# 13. Identifier Uniqueness

Default:

```text
(tenant, country, identifier_type, normalized_value)
```

ينتج Duplicate Candidate.

لا نضع DB Unique Constraint عالمي لكل Identifier دون Country Policy.

السبب:

- بعض المعرفات قد تتكرر بطريقة قانونية حسب نوعها.
- شركات قد تستخدم أرقام فروع/مجموعات.
- بيانات الهجرة قد تكون dirty.

Policy:

```text
Hard Block
Warning
Allowed
```

تأتي من Identifier Type/Country Pack.

---

# 14. Contact Person

الشخص الذي نتواصل معه.

Fields:

```text
id
tenant_id
display_name
first_name?
last_name?

job_title?
department?

status
language?
notes?

created_at
version
```

## Important

Contact Person قد يرتبط بأكثر من Party في حالات محدودة.

مثال:

استشاري محاسبي يمثل شركتين.

لذلك العلاقة في Link table.

---

# 15. Contact Points

لا نحشر Email/Phone واحد فقط في Contact.

`ContactPoint`:

```text
id
tenant_id
contact_person_id? 
party_id?
type
value
normalized_value
label?
is_primary
is_verified?
status
```

Types:

```text
Phone
Mobile
Email
WhatsApp
Website
Other
```

Directory يدعم Party-level generic phone/email أيضًا حتى لو لا يوجد شخص محدد.

---

# 16. Phone Normalization

Store:

```text
raw_value
normalized_e164?
country_hint?
```

عرض للمستخدم يحافظ على صيغة مناسبة.

البحث يستخدم normalized representation.

لا نرفض رقمًا محليًا قابلًا للاستخدام فقط لأن E.164 غير قابل للاستنتاج بدون Country.

---

# 17. Email Normalization

Normalize cautiously:

```text
trim
lowercase domain
```

لا نطبق transformations عدوانية على local-part.

Email validation = syntax/confirmation where needed, not "email definitely exists."

---

# 18. Addresses

Address entity:

```text
id
tenant_id

label
address_type

line1
line2?
district?
city?
state_region?
postal_code?
country_code

building_number?
additional_number?
short_address?

latitude?
longitude?

status
created_at
version
```

Country Pack / Address Format determines optional region-specific fields.

---

# 19. Address Types

Initial:

```text
General
Legal
Billing
Shipping
Office
Warehouse
Other
```

Address Type does not mean one address can only have one purpose.

Use Link/Purpose where required.

---

# 20. Party Address Link

```text
party_id
address_id

usage:
  Billing
  Shipping
  Legal
  General

is_default
valid_from?
valid_to?
```

Allows:

- one address serving several usages.
- different defaults.

---

# 21. Why Billing and Shipping Must Be Separate

Real companies commonly:

- invoice Head Office.
- deliver warehouse.
- send correspondence branch.
- use registered legal address for compliance.

ERPNext and Odoo both explicitly support multiple/role-specific addresses.

Therefore this is Day-1.

---

# 22. Address Snapshot Rule

Posted/confirmed business documents store an immutable address snapshot.

Never render old invoice using today's Party address.

Example:

Customer moves in 2028.

Invoice from 2026 must preserve 2026 address.

---

# 23. Contact Snapshot Rule

Documents that legally/business-wise need the historical contact may snapshot:

```text
contact_name
email
phone
role
```

Do not depend exclusively on live Contact record.

Exact snapshot requirement is decided by each consuming module.

---

# 24. Party Hierarchy

We need controlled relationships without building a graph database.

`PartyRelationship`:

```text
parent_party_id
child_party_id
relationship_type
valid_from?
valid_to?
```

Initial types:

```text
ParentCompany
Subsidiary
BranchOrganization
Franchise
Other
```

But physical delivery locations should usually be Addresses, not separate Parties.

---

# 25. When to Create Another Party vs Address

Create another Party if:

- independently billed legal entity.
- separate tax/legal identity.
- separate receivable/payable relationship.
- legally signs its own contracts.

Use Address if:

- same legal entity.
- only shipping/office/warehouse location differs.

---

# 26. Individual Contact vs Individual Party

Important distinction.

## Contact Person

Person representing an organization.

Example:

```text
Mohamed — Purchasing Manager at ABC Trading
```

## Individual Party

The person themselves is the business counterpart.

Example:

```text
Ahmed Hassan buys as an individual.
```

A person may theoretically be both, but default UX should avoid unnecessary complexity.

---

# 27. Party Aliases

Store alternate searchable names:

```text
PartyAlias
```

Examples:

```text
شركة النور للتجارة
Al Noor Trading
El Nour
النور
```

Useful for:

- Arabic/English.
- old name.
- common abbreviation.
- migration matching.

Alias does not replace legal/display name.

---

# 28. Search UX

Global Party Search should match:

- display name.
- legal name.
- trade name.
- aliases.
- Tax ID.
- CR.
- phone.
- email.
- customer/supplier external code later.
- contact name.

Search result should show enough context:

```text
Al Noor Trading
Customer + Supplier
VAT: ***123
Cairo
Phone: ...
```

to avoid selecting wrong duplicate.

---

# 29. Arabic Search

Design for:

- Arabic/Latin mixed names.
- optional normalization of Alef forms.
- diacritics-insensitive search.
- Arabic/English aliases.
- phone digits normalization.

Do not silently rewrite source legal name.

Maintain searchable normalized columns/index expressions separately.

---

# 30. PostgreSQL Search Baseline

Start with:

- normalized searchable fields.
- `pg_trgm`.
- indexed exact identifiers.
- indexed normalized phone/email.

Do not add OpenSearch Day 1.

Add dedicated search engine only if measured search relevance/scale requires it.

---

# 31. Duplicate Detection — Business Goal

The goal is not "AI detects duplicates."

Goal:

> Prevent dirty Master Data before transactions depend on it.

Duplicate engine must work during:

- manual create.
- Quick Create.
- import.
- CRM conversion.
- API integration.

---

# 32. Duplicate Signals

Strong:

```text
Same Tax ID
Same Commercial Registration
Same verified email
Same normalized business phone
```

Medium:

```text
Similar legal name + same city
Similar name + same phone prefix
Same address + similar name
```

Weak:

```text
name similarity only
```

---

# 33. Duplicate Decision

System calculates candidate reasons.

Example:

```text
Possible duplicate:
Al Noor Trading

Reasons:
✓ Same VAT number
✓ Similar Arabic name
✓ Same phone

Actions:
[Use Existing]
[View]
[Create Anyway — permission required]
```

Do not show opaque score like "87% duplicate" without reasons.

---

# 34. Duplicate Policy

Levels:

```text
Block
Strong Warning
Suggest
Ignore
```

Tax ID exact match usually strong.

But exact policy may vary by Identifier/Country.

---

# 35. Merge

Merge is essential.

## Merge operation

```text
Source Party
→ Master Party
```

Source becomes:

```text
Merged
```

and stores redirect.

Never delete source ID immediately.

---

# 36. Merge Rules

Before merge:

- compare identifiers.
- addresses.
- contacts.
- roles.
- aliases.
- module profiles.
- open transactions.
- legal entity visibility.

Merge wizard displays conflicts.

Example:

```text
Phone:
Source: ...
Target: ...
Choose / keep both
```

---

# 37. Cross-Module Merge

Directory cannot directly update Sales/Invoices tables.

Modules register:

```text
IPartyMergeContributor
```

Responsibilities:

- validate merge.
- redirect mutable references if safe.
- preserve posted snapshot/history.
- update read models.
- reject impossible merge when legal identity conflicts.

Merge becomes coordinated operation.

---

# 38. Merge Audit

Must record:

```text
source_party_id
master_party_id
actor
reason
conflicts resolved
timestamp
```

Permission:

```text
directory.party.merge
```

High-risk action.

---

# 39. Unmerge

Do not promise generic Unmerge after transactions have been consolidated.

MVP:

> Merge is effectively irreversible through normal UI.

Support-only repair requires explicit technical procedure and audit.

---

# 40. Archive

Archive instead of delete once referenced.

Archived Party:

- hidden from new transaction search by default.
- historical transactions remain visible.
- can be restored by authorized user.

---

# 41. Hard Delete

Allowed only if:

- created by mistake.
- no transaction/module reference.
- no legal/audit requirement.
- no identifiers/relationships that require history.

Prefer cleanup before first use.

---

# 42. Blocking Party

"Blocked" needs reason/scope.

Possible sources later:

- credit hold.
- compliance.
- purchasing policy.
- admin blacklist.

Directory should not own business-specific reasons.

Use capability/policy provider.

Directory displays effective restrictions with source.

---

# 43. Customer/Supplier Role Activation

When Party gains Customer role:

- Directory records role.
- Sales/Subledger profile may be provisioned lazily/defaulted.
- role alone does not create journal accounts.

When Supplier role added:

- same identity remains.

---

# 44. No Separate Customer ID Requirement

Party ID is canonical.

Modules may expose friendly codes:

```text
Customer Code
Supplier Code
```

as module-owned aliases/sequences.

Do not make them primary IDs.

---

# 45. Legal Entity Profiles

Same Party can have entity-specific business behavior.

Key:

```text
(tenant_id, party_id, legal_entity_id)
```

Examples later:

```text
BillingProfile
CreditProfile
SupplierProfile
SalesProfile
```

---

# 46. Visibility Scope

Tenant-level Party identity does not mean every user sees every Party.

`PartyVisibilityScope`:

```text
TenantWide
SelectedLegalEntities
SingleLegalEntity
```

Initial data:

```text
party_id
scope_type
legal_entity_id?
```

Later can become many-to-many selected entities.

---

# 47. Why Visibility Matters

Tenant may be a group of companies.

Company A should not necessarily browse confidential supplier relationships of Company B.

But creating duplicate Parties per company damages canonical identity.

Solution:

> Shared identity + scoped visibility + entity profiles.

---

# 48. Access Rule

User can view Party if:

1. Membership scope allows relevant Legal Entity.
2. Party visibility includes that Entity.
3. Permission allows action.

Global Tenant Admin can have tenant-wide visibility.

---

# 49. Quick Create — Product Design

This is one of the most important UX features.

Salesperson creating quotation should not leave the workflow.

Quick Create:

```text
Name *
Mobile / Phone
Country
Type: Company / Individual
```

Optional:

```text
Email
Tax ID
```

Then:

```text
[Create and Continue]
```

Party becomes:

```text
Draft/Active-minimal
```

depending policy.

---

# 50. Contextual Completion

When user later performs a workflow requiring more data:

Example:

```text
Issue tax invoice
```

System checks:

```text
Missing:
- Tax ID
- Legal address
```

Then opens concise completion panel.

This is better than giant onboarding form.

---

# 51. Full Party Workspace

The full page should be role-aware.

## Header

```text
Party Name
Type
Roles
Status
Primary phone
Tax badge/status
Actions
```

## Business summary

Depending active modules:

```text
Sales
Purchasing
Receivables
Payables
Open Orders
Last Activity
```

These are read models from modules, not Directory ownership.

---

# 52. Party Workspace Tabs

Core tabs:

```text
Overview
Contacts
Addresses
Legal & Identifiers
Relationships
Activity
Files
Audit
```

Module-contributed sections later:

```text
Sales
Purchasing
Finance
CRM
Service
```

Do not hard-code empty Sales/Finance tabs if modules inactive.

---

# 53. Progressive Disclosure

Basic user sees:

```text
Name
Phone
Address
Contacts
```

Specialist/Admin can see:

```text
Legal identifiers
Merge
Visibility
Audit
Advanced relationships
```

---

# 54. Role-Based UX — Salesperson

Needs:

- search fast.
- quick create.
- main contact.
- shipping location.
- customer status.
- maybe credit warning from provider.

Does not need:

- merge mechanics.
- raw identifiers table.
- accounting mapping.
- sensitive bank info.

---

# 55. Role-Based UX — Purchasing

Needs:

- supplier contacts.
- supplier addresses.
- legal identity.
- default procurement contact.
- later Supplier Profile data.

---

# 56. Role-Based UX — Finance

Needs:

- legal name.
- tax identifiers.
- billing address.
- invoice contact.
- statement contact.
- entity relationship.
- duplicate certainty.
- audit.

---

# 57. Role-Based UX — Master Data Admin

Needs:

- merge.
- aliases.
- duplicate queue.
- import.
- visibility.
- identifier verification.
- bulk cleanup.

---

# 58. Required Fields Philosophy

At Party creation:

Hard minimum:

```text
display_name
party_type
```

Workflow may require more.

Do not mark 20 fields "required" at database form level because one downstream process may need them.

Use:

```text
Completeness Rules by Capability
```

---

# 59. Party Completeness Model

Instead of one global "Complete/Incomplete", calculate capability readiness:

```text
CanSell
CanPurchase
CanInvoice
CanPay
CanSubmitGovernmentDocument
CanPortalInvite
```

Example:

```text
CanSell = true
CanInvoice = false (Tax ID missing)
```

This is far more useful than red asterisk everywhere.

---

# 60. Completeness Provider Contract

Modules/country packs may contribute validators:

```text
IPartyReadinessRule
```

Result:

```text
capability
ready
missing_fields
warnings
blocking_reason
```

Directory aggregates and presents them.

---

# 61. Primary Contact

Do not allow only one universal primary contact.

We need contextual preference.

Examples:

```text
General
Sales
Purchasing
Billing
Collections
Shipping
```

PartyContactLink can store usage/preference.

---

# 62. Contact Usage

```text
General
Sales
Purchasing
Billing
AccountsPayable
Collections
Shipping
Technical
Executive
```

Keep extensible.

---

# 63. Contact Lifecycle

```text
Active
Inactive
```

Inactive contact remains historical.

Reason optional:

```text
LeftCompany
ChangedRole
Invalid
Other
```

---

# 64. Contact Privacy

Do not automatically expose all contact fields to every user.

Potential sensitive:

- personal mobile.
- private email.
- identification docs later.

Initial scope can be permission-based where justified.

Avoid overengineering field-level permission for ordinary business phone/email.

---

# 65. Portal User Relationship

Portal identity is not Contact itself.

Model later:

```text
Identity
  ↔ ContactPerson
  ↔ Party
```

Invitation is separate.

Deleting Contact does not automatically delete Identity without lifecycle checks.

---

# 66. CRM Lead Conversion

CRM Lead != Party by default.

Reason:

Leads can be:

- low-quality.
- duplicated.
- incomplete.
- scraped.
- unqualified.

Upon qualification:

```text
Search Directory
→ Link existing Party
or
→ Create Party
```

Duplicate check mandatory.

---

# 67. Import — Business Reality

Import is not optional tooling.

It is part of onboarding product.

First customers likely migrate from:

- Excel.
- legacy desktop accounting.
- Odoo.
- ERPNext.
- Daftra.
- custom software.

Directory importer should be one of first real uses of Platform Import Framework.

---

# 68. Customer/Supplier Import Templates

Provide templates:

```text
Parties
Contacts
Addresses
Identifiers
Roles
```

And a "Simple Customer Import" flattened template for common cases.

Example columns:

```text
Display Name
Type
Customer?
Supplier?
Phone
Email
Tax ID
Country
Billing Address
Shipping Address
Contact Name
Contact Phone
```

Importer expands to normalized entities.

---

# 69. Matching During Import

Matching order:

1. confirmed legal identifier.
2. explicit external source ID mapping.
3. exact normalized email/phone.
4. name + location similarity.
5. create new.

User sees:

```text
Create
Update Existing
Potential Duplicate
Skip
Error
```

before commit.

---

# 70. External IDs

Do not put every legacy ID as column.

Use:

```text
ExternalReference
```

Fields:

```text
tenant_id
party_id
system_key
external_id
```

Unique:

```text
(tenant_id, system_key, external_id)
```

Useful for integrations/import.

---

# 71. Data Quality Dashboard

Directory Admin should see:

```text
Potential duplicates
Missing legal identifiers
Invalid phone/email
No primary contact
No billing address
Archived with open relationships
Unverified import records
```

Later priority.

MVP should at least expose duplicate/import issues.

---

# 72. API Ownership

Base:

```text
/api/v1/directory/
```

Possible endpoints:

```text
GET    /parties
POST   /parties
GET    /parties/{id}
PATCH  /parties/{id}

POST   /parties/{id}/roles
DELETE /parties/{id}/roles/{role}

POST   /parties/{id}/contacts
POST   /parties/{id}/addresses
POST   /parties/{id}/identifiers

GET    /parties/{id}/duplicate-candidates
POST   /parties/{id}/merge

POST   /parties/{id}/archive
POST   /parties/{id}/restore

GET    /parties/search
GET    /parties/{id}/readiness
```

Exact API surface must stay use-case oriented.

---

# 73. Commands

Application commands:

```text
CreateParty
UpdatePartyIdentity
AddPartyRole
RemovePartyRole
AddIdentifier
UpdateIdentifier
AddContactPerson
LinkContactToParty
AddAddress
LinkAddressToParty
ArchiveParty
RestoreParty
MergeParty
SetPartyVisibility
```

---

# 74. Queries

```text
GetParty
SearchParties
ListParties
GetPartyContacts
GetPartyAddresses
GetPartyIdentifiers
FindDuplicateCandidates
GetPartyReadiness
GetPartyActivitySummary
```

Activity Summary may aggregate read models only from active modules.

---

# 75. Contracts Exposed to Other Modules

`Todir.Foundation.Directory.Contracts`

Core queries:

```text
GetPartySummary
GetPartySnapshot
SearchParty
ValidatePartyRole
GetPartyAddress
GetPartyContact
GetPartyIdentifiers
GetPartyReadiness
```

Commands exposed sparingly:

```text
EnsurePartyRole
CreatePartyFromQualifiedSource
```

Do not expose DbContext.

---

# 76. Party Snapshot Contract

For downstream documents:

```text
PartySnapshot
{
  PartyId
  DisplayName
  LegalName
  IdentifierSnapshot[]
  AddressSnapshot?
  ContactSnapshot?
}
```

Consuming module decides what to persist.

---

# 77. Domain Events

Directory domain events:

```text
PartyCreated
PartyUpdated
PartyArchived
PartyRestored
PartyRoleAdded
PartyRoleRemoved
PartyIdentifierChanged
ContactLinked
AddressLinked
PartyMerged
PartyVisibilityChanged
```

Domain events are internal first.

Only meaningful cross-module integration facts become broker events.

---

# 78. Integration Events

Potential:

```text
directory.party.created.v1
directory.party.identity-changed.v1
directory.party.merged.v1
directory.party.archived.v1
directory.party.role-added.v1
```

Do not publish event for every phone edit unless consumers need it.

---

# 79. Party Merge Event

`PartyMergedV1` payload:

```text
tenant_id
source_party_id
master_party_id
merged_at
```

Consumers update mutable references/read models.

Historical posted snapshots remain untouched.

---

# 80. Database Schema

Use:

```text
foundation_directory
```

Owned by Directory module.

---

# 81. Tables

Initial:

```text
party
party_role
party_identifier

contact_person
contact_point
party_contact

address
party_address

party_alias
party_relationship

party_merge_redirect
party_visibility

external_reference

duplicate_candidate
```

`duplicate_candidate` may be projection/cache rather than permanent truth.

---

# 82. `party`

Concept:

```sql
id uuid
tenant_id uuid

party_type smallint
display_name text

legal_name text null
trade_name text null
name_ar text null
name_en text null

status smallint

country_code char(2) null
primary_language text null

created_at timestamptz
created_by uuid
updated_at timestamptz
updated_by uuid
version bigint

primary key (tenant_id, id)
```

RLS mandatory.

---

# 83. `party_role`

```text
tenant_id
party_id
role_code
status
active_from
active_to
created_at
```

Unique active combination as appropriate.

---

# 84. `party_identifier`

```text
id
tenant_id
party_id
identifier_type
country_code
value
normalized_value
issuing_authority
valid_from
valid_to
is_primary
verification_status
created_at
version
```

Indexes:

```text
tenant + identifier_type + normalized_value
party
```

---

# 85. `contact_person`

```text
id
tenant_id
display_name
first_name
last_name
job_title
department
status
language
created_at
version
```

---

# 86. `contact_point`

```text
id
tenant_id
owner_type
owner_id
type
label
value
normalized_value
is_primary
verification_status
status
```

Owner can be Party or ContactPerson.

Alternative implementation with separate tables is allowed if it improves integrity.

Do not use polymorphic FK without considering DB integrity.

---

# 87. `party_contact`

```text
tenant_id
party_id
contact_person_id
usage
is_default
valid_from
valid_to
status
```

---

# 88. `address`

```text
id
tenant_id
label
line1
line2
district
city
state_region
postal_code
country_code
building_number
additional_number
short_address
latitude
longitude
status
version
```

Do not require every field globally.

---

# 89. `party_address`

```text
tenant_id
party_id
address_id
usage
is_default
valid_from
valid_to
```

---

# 90. `party_alias`

```text
id
tenant_id
party_id
alias
normalized_alias
alias_type
language
```

Types:

```text
AlternateName
FormerName
Abbreviation
ImportedName
```

---

# 91. `party_relationship`

```text
id
tenant_id
from_party_id
to_party_id
relationship_type
valid_from
valid_to
```

Prevent self-reference.

Guard cycles only for relationship types that require hierarchy.

---

# 92. `party_merge_redirect`

```text
tenant_id
source_party_id
master_party_id
merged_at
merged_by
reason
```

Source ID resolves to master.

---

# 93. `party_visibility`

```text
tenant_id
party_id
scope_type
legal_entity_id
```

Rules prevent referencing entity outside Tenant.

---

# 94. External Reference Table

```text
tenant_id
system_key
external_id
party_id
created_at
```

Unique:

```text
tenant_id + system_key + external_id
```

---

# 95. RLS

All tenant-owned Directory tables:

```text
ENABLE ROW LEVEL SECURITY
FORCE ROW LEVEL SECURITY
```

Runtime app role not owner.

Cross-tenant integration tests mandatory.

---

# 96. Foreign Keys

Directory internal tables use strong FK.

Examples:

```text
party_role → party
party_identifier → party
party_contact → party/contact
party_address → party/address
```

Include tenant in composite keys/FKs.

---

# 97. Cross-Module Reference

Business modules may store:

```text
party_id
```

as stable Foundation FK if Master Architecture permits.

They may not navigate Directory EF entity through ORM relationships.

Use ID + Directory contract/snapshot.

---

# 98. EF Core

Directory has:

```text
DirectoryDbContext
```

Only Directory entities.

No Sales/Invoice entities.

---

# 99. Aggregate Boundaries

Primary aggregate:

```text
Party
```

But do not load 5,000 contacts/addresses into one aggregate for every edit.

Use bounded operations.

Potential aggregates:

```text
PartyIdentity
ContactPerson
Address
```

with application service enforcing links.

DDD pragmatism over giant aggregate.

---

# 100. Concurrency

Use optimistic concurrency.

Sensitive operations:

- merge.
- identifier change.
- role change.

require fresh version.

---

# 101. Validation

Validation layers:

```text
Syntax
Domain
Duplicate
Capability readiness
Country pack
```

Do not mix them.

---

# 102. Legal Name vs Display Name

`display_name`:

what users search/see.

`legal_name`:

official business identity if available.

They may differ.

Example:

```text
Display: El Nour
Legal: Al Nour Trading & Distribution S.A.E.
```

---

# 103. Trade Name

Optional.

Used for commercial brand.

Do not overwrite Legal Name.

---

# 104. Country

Country belongs to Party identity as primary registration/context.

Addresses can be in different countries.

Legal/Tax profiles later may be entity-country specific.

---

# 105. Currency

Directory does **not** own default sales/purchase currency.

Because same Party may:

- buy in USD.
- sell in EGP.
- differ by Legal Entity.

Currency belongs to module/entity profiles.

This avoids hidden coupling.

---

# 106. Payment Terms

Directory does not own.

Finance/Sales/Purchase profile owns defaults.

---

# 107. Credit Limit

Directory does not own.

Subledger/Credit profile owns.

---

# 108. Price List

Directory does not own.

Sales/Pricing profile owns.

---

# 109. Bank Accounts

Party bank account is sensitive and has fraud implications.

Do not put generic bank account editing into MVP Directory unless Purchasing/Payments requires it.

When implemented:

- separate entity.
- masked display.
- strong permission.
- approval on changes.
- audit.
- possibly beneficiary verification.

Design-now, build with Payments/Purchasing requirements.

---

# 110. Notes

Avoid one giant free-text notes field used for critical rules.

Allow general notes, but:

- credit rules → structured.
- tax status → structured.
- shipping instructions → relevant module/profile.
- security warning → structured flag.

---

# 111. Tags

Tags useful for user classification.

Examples:

```text
VIP
Distributor
Government
Strategic
```

But do not use tags to replace structured role/policy.

MVP can include lightweight tags if Product needs segmentation.

---

# 112. Categories / Segments

Customer group/territory/segment belongs primarily to Sales/CRM, not Directory identity.

Directory may support generic classification later, but do not duplicate commercial taxonomy.

---

# 113. Activity Timeline

Party workspace can display:

```text
Created
Address changed
Contact added
Merged
Sales order
Invoice
Payment
Purchase order
```

But Directory should not own all those records.

Use federated read model/events.

---

# 114. Privacy & Retention

Directory contains personal/business data.

Need:

- least privilege.
- audit for sensitive access where justified.
- retention policy.
- tenant export.
- country legal review later.

Do not hardcode GDPR/PDPL behavior globally without country/legal verification.

---

# 115. Audit — Must Capture

```text
Create Party
Change Legal Name
Change Tax/Legal Identifier
Archive/Restore
Merge
Visibility change
Sensitive contact change
Role change
```

General phone typo edit can still be audited through standard entity audit if configured.

---

# 116. Permissions

Initial:

```text
directory.party.read
directory.party.create
directory.party.edit
directory.party.archive

directory.party.role.manage
directory.party.identifier.view
directory.party.identifier.manage

directory.contact.manage
directory.address.manage

directory.party.merge
directory.party.visibility.manage
directory.duplicate.override

directory.import
directory.export
directory.audit.view
```

---

# 117. Permission UX

If user lacks:

```text
directory.party.identifier.manage
```

do not show Edit button.

Backend still enforces.

---

# 118. Merge Permission

`directory.party.merge` should not be granted to ordinary sales users.

Recommended roles later:

- Master Data Admin.
- Tenant Admin.

---

# 119. Duplicate Override

Creating despite a strong duplicate requires:

```text
directory.duplicate.override
```

and reason.

Audit it.

---

# 120. Sensitive Identifier Masking

Some identifier types may be masked depending on policy.

Example:

```text
*******1234
```

Tax ID often operationally visible, while National ID may require stricter policy.

Country pack/security classification decides.

---

# 121. Quick Create Permission

Sales user may have:

```text
directory.party.quick_create
```

which allows basic Party creation but not legal identifiers/merge.

Consider separate permission if product needs control.

---

# 122. Approval

Directory generally should not require approvals for ordinary create/update.

Possible approvals:

- Merge.
- bank details change later.
- high-risk legal identifier change in mature deployment.

Don't overburden MVP.

---

# 123. Workflow — Create Customer From Sales

```text
Sales Quotation
→ Search Party
→ No match
→ Quick Create
→ Duplicate Check
→ Create Party(Customer Role)
→ Return PartyId
→ Continue Quotation
```

Target UX:

less than a minute for simple customer.

---

# 124. Workflow — Create Supplier From Purchasing

```text
Purchase workflow
→ Search Supplier
→ Quick Create Party(Supplier Role)
→ Add Legal/Contact minimum
→ Continue PO/RFQ
```

Full compliance data required only when Purchase/Bill workflow needs it.

---

# 125. Workflow — Add Supplier Role to Existing Customer

```text
Search by Tax ID
→ Existing Party found
→ "Already Customer"
→ Add Supplier Role
→ Create SupplierProfile defaults later
```

No duplicate identity.

---

# 126. Workflow — Lead Conversion

```text
CRM Lead Qualified
→ Search Directory
→ Match found?
    Yes → Link to Party
    No  → Create Party
→ Add Customer role if needed
→ Opportunity references Party
```

---

# 127. Workflow — Duplicate Merge

```text
Duplicate Queue
→ Compare
→ Validate identifiers
→ Inspect module impacts
→ Choose master
→ Resolve data conflicts
→ Confirm with reason
→ Transaction/operation
→ Source marked Merged
→ Redirect
→ Publish PartyMerged
```

---

# 128. Workflow — Archive

```text
Archive request
→ Check blocking rules
→ Warn if open module relationships
→ Confirm
→ status Archived
```

Archiving may not cancel outstanding invoices/orders.

Modules own those states.

---

# 129. Workflow — Reactivate

```text
Archived Party
→ Restore
→ Duplicate check
→ Active
```

Do not create new Party if same legal party returns.

---

# 130. Workflow — Change Legal Identifier

```text
Edit VAT/Tax ID
→ permission
→ duplicate search
→ country validation
→ conflict check
→ reason
→ update
→ audit
→ PartyIdentityChanged event
```

Posted historical documents unchanged.

---

# 131. Readiness UX

Example panel:

```text
Sales Order       Ready ✓
Tax Invoice       Missing VAT Number ✗
Purchase Order    Ready ✓
Portal Invite     Missing Email ✗
```

Very valuable business UX.

---

# 132. Mobile

Mobile Directory priorities:

- search.
- quick create.
- call/WhatsApp shortcut.
- view address.
- add contact.
- view role/status.

Advanced merge/import/admin not mobile priority.

---

# 133. Offline

Do not build offline Directory initially.

POS/mobile offline needs separate requirements later.

---

# 134. AI Opportunities

AI may assist:

- duplicate candidate explanation.
- Arabic/English name normalization suggestions.
- parse business card.
- parse customer registration document.
- map Excel columns.
- suggest address structure.

AI does not:

- merge automatically.
- overwrite Tax ID.
- create legal party automatically without review.
- change roles without human confirmation.

---

# 135. OCR

Future:

Upload:

```text
Commercial Registration
Tax Certificate
Business Card
```

OCR extracts Draft.

User confirms.

Never treat OCR as legal verification.

---

# 136. Data Migration Quality

Before first import:

produce report:

```text
Rows
Unique names
Exact duplicate Tax IDs
Duplicate phones
Missing country
Invalid email
Potential customer-supplier matches
```

This helps onboarding consultants clean data.

---

# 137. Import Commit Strategy

Do not commit row-by-row blindly.

Use:

```text
Validate all
Preview
Chunked commit
Error report
Reconciliation
```

Directory import may allow partial batch only if user explicitly chooses policy.

Default enterprise migration should prefer predictable all-valid batches/groups.

---

# 138. Export

Permissioned CSV/Excel export.

Sensitive identifiers obey permission.

Large export asynchronous.

---

# 139. Reporting — Directory

Core operational reports:

```text
Party list
Customers/Suppliers by Role
Potential duplicates
Missing identifiers
Archived parties
New parties by period
Incomplete readiness
```

Commercial Sales reports belong to Sales.

---

# 140. KPIs

Useful Product/Admin KPIs:

```text
Duplicate rate
Quick-create completion rate
Average creation time
Import error rate
Party records missing legal data
Merge count
Search-to-create ratio
```

Search-to-create ratio helps detect poor search causing duplicates.

---

# 141. Product Analytics Events

Internal product analytics, if enabled:

```text
party_search_used
party_quick_create_started
party_quick_create_completed
duplicate_warning_shown
duplicate_existing_selected
duplicate_override
party_merge_completed
```

No sensitive field values in analytics events.

---

# 142. Module Manifest

Conceptual:

```json
{
  "id": "directory",
  "version": "1.0.0",
  "contractsVersion": 1,
  "layer": "L1-foundation",
  "type": "foundation",
  "purchasable": false,
  "scope": "tenant",
  "dependencies": {
    "hard": ["platform-kernel"],
    "optionalCapabilities": ["country-pack"]
  },
  "owns": {
    "schema": "foundation_directory",
    "entities": [
      "Party",
      "PartyRole",
      "PartyIdentifier",
      "ContactPerson",
      "Address"
    ]
  }
}
```

---

# 143. Activation

Directory is a Foundation capability.

It is activated/provisioned automatically when first dependent module requires it.

Typical tenants effectively have it active early.

Activation seeds:

- base role types.
- identifier definitions from Country Pack when applicable.
- address metadata.

Do not seed fake parties.

---

# 144. Deactivation

Directory cannot be deactivated while any dependent active module uses Party.

In practice:

```text
BlockedIfDependents
```

Historical data retained.

---

# 145. Go-Live

Directory Go-Live mainly concerns migration boundary.

Inputs:

```text
existing parties
existing customer/vendor codes
external IDs
duplicate resolution strategy
```

No financial opening balance here.

---

# 146. API Idempotency

Critical create endpoints used by integrations/import support:

```text
Idempotency-Key
```

Particularly:

- CreateParty.
- CRM conversion.
- external connector sync.

---

# 147. Concurrency

If two users create same Party simultaneously:

- duplicate engine helps before.
- DB unique strong identifier may stop where applicable.
- post-create duplicate queue still catches races.

No system can rely only on pre-check.

---

# 148. Search Performance Target

Business goal:

Party lookup must feel instant to salesperson.

Initial engineering target to validate:

```text
p95 < 300ms
```

for normal tenant search at expected scale.

This is target, not guarantee.

Use realistic datasets:

```text
100k parties
1m contacts/address/search tokens
```

for stress tests later.

---

# 149. Large Tenant Consideration

At 1M Parties:

- cursor pagination.
- selective projections.
- no Include-all graph.
- optimized search indexes.
- async exports.
- duplicate scanning in background.

---

# 150. Caching

Do not cache entire Party records globally.

Possible short caching:

- identifier definitions.
- country metadata.
- small summaries.

Always tenant-keyed.

---

# 151. Eventual Read Models

Cross-module Party summary cards may be eventually consistent.

Identity data used for legal posting must be read synchronously/snapshotted at transaction time.

---

# 152. Failure Scenarios

## Directory unavailable in-process

In Modular Monolith this usually means DB/app issue, not network service.

Dependent command fails safely if Party validation is required.

## Duplicate worker delayed

Creation can still occur under configured warning policy; duplicate queue catches later.

## Country Pack unavailable

Use currently activated versioned metadata, not runtime remote dependency.

---

# 153. Data Invariants

1. Party belongs to exactly one Tenant.
2. Party cannot reference another Tenant.
3. Merged Party redirects to active/master Party.
4. Merge redirect cannot point to itself.
5. Active identifier normalization deterministic.
6. default address/contact link must belong to same Party.
7. role is unique per Party/role active period as designed.
8. tenant isolation mandatory.
9. posted document snapshots never depend on live update.
10. archived Party history remains readable.

---

# 154. Merge Invariants

- Cannot merge across Tenants.
- Cannot merge incompatible verified legal identities without explicit elevated process.
- Merge chains resolve to one root.
- Avoid cycles.
- Source cannot become active normally after merge.
- External refs are reassigned or mapped according to conflict rule.

---

# 155. Relationship Invariants

- Parent relationship cannot be self.
- For hierarchy types, prevent cycles.
- Address is not automatically a Branch Party.
- Contact person does not imply portal user.

---

# 156. Data Quality Severity

```text
Info
Warning
Blocking
```

Example:

No phone:

```text
Info/Warning
```

Exact duplicated VAT:

```text
Blocking / elevated override
```

depending Country rule.

---

# 157. Country Pack Integration

Country Pack contributes:

```text
Identifier definitions
Identifier validation
Address field metadata
Address rendering
Readiness rules
Legal label translations
```

Directory does not contain:

```text
if country == EG ...
```

in domain logic.

---

# 158. Document Rendering

Directory provides formatted Address/Party Snapshot input.

Invoice/Purchase document module owns final print layout.

---

# 159. Phone / WhatsApp

Store canonical contact info.

Notification/CRM integrations decide whether WhatsApp channel is available.

Directory does not call WhatsApp API.

---

# 160. Email

Directory owns email identity/contact.

Notification module owns sending.

---

# 161. Marketing Consent

Do not implement full marketing consent/CDP in Directory MVP.

If CRM/Marketing later needs consent:

separate consent domain/entity with legal research.

---

# 162. KYC / Verification

Do not build enterprise KYC engine initially.

Possible status:

```text
Unverified
UserConfirmed
SystemValidated
ExternallyVerified
```

only when actual use case exists.

Never label OCR result "verified."

---

# 163. Attachments

Party may attach:

```text
registration document
tax certificate
contract
other
```

Use Platform Files.

Directory stores link/classification.

Access permissions apply.

---

# 164. Document Expiry

Some identifiers/docs may expire.

Directory can expose:

```text
valid_to
```

Notification scheduler later alerts.

Do not make all identifiers require expiry date.

---

# 165. Address Geolocation

Latitude/longitude optional.

Useful later for:

- delivery.
- field service.
- route planning.

Do not require map/geocoding on Day 1.

---

# 166. Relationship to Shipping

Shipping module consumes:

- Party.
- Shipping Address.
- Contact.

Directory does not own carrier shipment logic.

---

# 167. Relationship to Sales

Sales consumes:

```text
Party(Customer Role)
Contact
Ship-to/Bill-to
Sales Profile (owned by Sales)
```

Directory never stores quotation/order.

---

# 168. Relationship to Purchasing

Purchasing consumes:

```text
Party(Supplier Role)
Contacts
Supplier addresses
SupplierProfile owned by Purchasing
```

---

# 169. Relationship to Invoicing

Invoicing consumes legal snapshot:

```text
Party identity
Tax identifiers
Billing address
billing contact
```

at posting/issue time.

---

# 170. Relationship to Subledger

Subledger uses:

```text
PartyId
```

for receivable/payable open items.

Credit policy lives there, not Directory.

---

# 171. Relationship to CRM

CRM Lead may be unqualified raw entity.

Qualified Opportunity links/creates Party.

---

# 172. Relationship to HR

Employee is not Party.

External contractor may be Party.

If Employee later needs reimbursement/payee relationship, use explicit link/profile rather than changing Employee into Party automatically.

---

# 173. Relationship to Portal

Portal Account maps Identity/Contact/Party.

Portal permission is separate from Directory role.

---

# 174. Relationship to Compliance

Compliance reads invoice/legal snapshots, not live Party directly unless validation pre-issue asks Directory/Country Pack.

---

# 175. Relationship to AI

AI gets Directory through approved tools:

```text
SearchParty
SuggestDuplicate
DraftPartyFromDocument
```

No direct DB.

---

# 176. Frontend Folder Structure

```text
src/frontend/src/modules/directory/

├── api/
├── routes/
├── pages/
│   ├── PartyListPage/
│   ├── PartyWorkspacePage/
│   ├── DuplicateReviewPage/
│   └── ImportPage/
│
├── features/
│   ├── quick-create-party/
│   ├── edit-party/
│   ├── manage-contacts/
│   ├── manage-addresses/
│   ├── manage-identifiers/
│   ├── merge-party/
│   └── party-readiness/
│
├── components/
│   ├── PartyPicker/
│   ├── PartySummary/
│   ├── ContactCard/
│   ├── AddressCard/
│   ├── IdentifierBadge/
│   └── DuplicateWarning/
│
├── queries/
├── mutations/
├── schemas/
├── permissions/
├── i18n/
├── public.ts
└── tests/
```

---

# 177. Backend Folder Structure

```text
src/backend/foundation/Directory/

├── Todir.Foundation.Directory/
│   ├── Module.cs
│   ├── module.manifest.json
│   │
│   ├── Domain/
│   │   ├── Parties/
│   │   ├── Contacts/
│   │   ├── Addresses/
│   │   ├── Identifiers/
│   │   ├── Relationships/
│   │   └── Duplicates/
│   │
│   ├── Application/
│   │   ├── Features/
│   │   ├── Ports/
│   │   ├── Permissions/
│   │   └── Policies/
│   │
│   ├── Infrastructure/
│   │   ├── Persistence/
│   │   ├── Search/
│   │   ├── Import/
│   │   └── Messaging/
│   │
│   ├── Api/
│   └── ReadModels/
│
├── Todir.Foundation.Directory.Contracts/
│   ├── Queries/
│   ├── Commands/
│   ├── DTOs/
│   ├── Events/
│   └── Capabilities/
│
└── README.md
```

---

# 178. Key Domain Services

Possible:

```text
PartyDuplicateDetector
PartyMergeService
PartyReadinessService
IdentifierNormalizationService
PartySnapshotService
```

Do not create `PartyManager` doing everything.

---

# 179. Repositories

No generic repository.

Possible:

```text
IPartyRepository
IContactRepository
```

or use DirectoryDbContext directly in Application handlers where appropriate.

---

# 180. Query Model

Party list should use projection, not aggregate load.

Projection:

```text
PartyId
DisplayName
Type
Roles
PrimaryPhone
PrimaryEmail
Country
TaxIdentifierDisplay
Status
DuplicateWarning
```

---

# 181. Party Picker

Critical shared UI component.

Requirements:

- keyboard friendly.
- search-as-you-type.
- roles filtering.
- entity visibility.
- quick create.
- duplicate hint.
- recent selections.
- barcode? not relevant.
- no huge modal.

---

# 182. Party Picker Search Flow

```text
User types "نور"
↓
300ms debounce or equivalent
↓
Server search
↓
Results
↓
Select
or
Quick Create
```

TanStack Query caches appropriately by tenant/context.

---

# 183. Empty State

When no parties:

```text
"أضف أول عميل أو مورد"
```

Context button:

```text
Add Customer
Add Supplier
Import from Excel
```

Not generic "No data."

---

# 184. Import UX

Wizard:

```text
1 Upload
2 Map Columns
3 Validate
4 Review Duplicates
5 Preview Changes
6 Import
7 Reconciliation Report
```

User can download error workbook.

---

# 185. Duplicate Review UX

Side-by-side.

Show:

```text
Names
Identifiers
Phones
Emails
Addresses
Roles
Module usage
```

Highlight conflict.

---

# 186. Merge UX Safety

Before confirm:

```text
This action cannot be easily undone.
Source record will redirect to Master.
Historical documents remain unchanged.
```

Require reason.

---

# 187. Audit UX

Timeline:

```text
Ahmed changed Tax ID
Old: ***
New: ***
Reason: ...
Date
```

Sensitive values masked based on permission.

---

# 188. Accessibility

- keyboard navigation.
- labels associated with inputs.
- focus management.
- contrast.
- RTL.
- screen-reader meaningful duplicate warnings.

---

# 189. Internationalization

Every label/error uses translation key.

Names remain user data.

Address rendering uses Country template.

---

# 190. Testing — Unit

Test:

- role add/remove.
- archive/restore.
- identifier normalization.
- relationship cycle.
- merge redirect.
- readiness aggregation.
- duplicate rule reasons.
- alias normalization.

---

# 191. Testing — Integration

Real PostgreSQL:

- RLS.
- composite FKs.
- unique constraints.
- trigram search.
- concurrency.
- merge transaction.
- import.
- event outbox.

---

# 192. Testing — Cross-Tenant

Tenant A cannot:

- search B.
- fetch by B ID.
- merge with B.
- link B Contact.
- use B Address.
- import using B external ref.

---

# 193. Testing — E2E

Flows:

1. Quick create Customer.
2. Add supplier role.
3. Add two addresses.
4. Add contacts.
5. duplicate warning.
6. merge.
7. archive/restore.
8. import.
9. Arabic search.
10. permissions.

---

# 194. Testing — Performance

Datasets:

```text
10k
100k
1m parties
```

Test:

- exact Tax ID.
- Arabic name fuzzy.
- phone.
- Party Picker.
- duplicate import.

---

# 195. Acceptance Scenario 1 — Simple Retail/Service Customer

Given:

Sales user.

When:

creates Individual with name + mobile.

Then:

- Party created.
- Customer role added.
- quotation can continue.
- no tax fields block.
- audit created.

---

# 196. Acceptance Scenario 2 — B2B Invoice

Given:

Customer missing required legal identifier.

When:

user attempts invoice workflow requiring it.

Then:

- readiness check blocks issue.
- user sees exact missing data.
- can complete in contextual panel.
- no need to edit unrelated fields.

---

# 197. Acceptance Scenario 3 — Customer Becomes Supplier

Given:

Party has Customer role.

When:

Purchasing finds same Tax ID.

Then:

- system presents existing Party.
- user adds Supplier role.
- no duplicate Party.
- existing contacts/addresses reusable.

---

# 198. Acceptance Scenario 4 — Different Shipping Address

Given:

Party with Billing + Warehouse addresses.

When:

Sales selects Party.

Then:

- default Billing and Shipping selected independently.
- user can choose other linked address.
- order snapshots selected addresses.

---

# 199. Acceptance Scenario 5 — Duplicate Import

Given:

Excel row matches existing VAT ID.

Then:

- importer marks matched/duplicate.
- does not silently create another Party.
- user chooses update/skip.

---

# 200. Acceptance Scenario 6 — Merge

Given:

two duplicate Parties with transactions.

Then:

- merge impact collected.
- master selected.
- source becomes redirect.
- old posted document snapshots unchanged.
- searches resolve master.

---

# 201. Acceptance Scenario 7 — Multi-Entity Visibility

Given:

Party visible only Entity A.

User scoped Entity B.

Then:

- Party not available in normal search.
- direct API access denied/not found according to security contract.

---

# 202. Acceptance Scenario 8 — Arabic/English Alias

Given:

Party aliases:

```text
النور للتجارة
Al Noor Trading
```

Searching either finds same Party.

---

# 203. Acceptance Scenario 9 — Contact Leaves

Given:

old AP contact linked to historical invoices.

When:

contact marked Inactive.

Then:

- not chosen by default for new docs.
- historical snapshots remain.
- audit preserved.

---

# 204. Acceptance Scenario 10 — Reactivation

Given:

archived Party returns after two years.

Then:

- user restores existing Party.
- system does not encourage new duplicate.
- historical identity remains continuous.

---

# 205. MVP Scope

Must build:

- Organization/Individual Party.
- Roles Customer/Supplier.
- Contacts.
- Contact points.
- Multiple Addresses.
- Billing/Shipping defaults.
- Legal identifiers.
- Arabic/English names/aliases.
- search.
- quick create.
- duplicate check.
- archive/restore.
- merge.
- visibility scope.
- import/export.
- audit.
- readiness API/framework.
- Contracts for downstream modules.

---

# 206. Build Soon After MVP

- Duplicate review queue enhancements.
- business card OCR draft.
- document OCR draft.
- richer hierarchy.
- portal contact linking.
- data-quality dashboard.
- bulk edit.

---

# 207. Do Not Build Yet

- full CRM.
- marketing automation.
- CDP.
- KYC engine.
- sanctions screening.
- graph relationship platform.
- arbitrary customer hierarchy rules.
- AI auto-merge.
- marketing consent engine.
- bank-account fraud workflow until Payments/Purchasing design.
- external company registry auto-enrichment unless market/provider validated.

---

# 208. Product Risks

## Risk 1 — God Party

Every module adds fields to Party.

Mitigation:

> module profiles + module UI contributions.

## Risk 2 — Too much data required

User avoids system or enters fake data.

Mitigation:

> contextual readiness + Quick Create.

## Risk 3 — Duplicate pollution

Mitigation:

> search first, duplicate detection, import matching, merge.

## Risk 4 — Over-shared group data

Mitigation:

> Party visibility scope + entity profile.

## Risk 5 — Legal fields hardcoded globally

Mitigation:

> Country Pack metadata.

---

# 209. Technical Risks

## Search quality Arabic

Start with Postgres normalization/trigram.

Measure.

## Merge cross-module complexity

Use contributor contract from first dependent modules.

## Dirty legacy data

Import preview/data-quality report.

## RLS leaks

Kernel RLS policy + integration fuzz tests.

---

# 210. Business Success Criteria

We consider Directory successful when:

- Sales can find/create customer without friction.
- Purchasing reuses same Party for Supplier.
- Finance trusts legal identity.
- data team can import thousands safely.
- users rarely create duplicates.
- same Party can support multi-entity relationships.
- future modules do not need their own Customer/Supplier identity table.

---

# 211. UX Success Criteria

Target user experience:

```text
Search existing Party: seconds.
Quick create simple Party: < 30–60 seconds target.
Add second address/contact: minimal clicks.
Duplicate warning: clear reason.
Readiness error: actionable, not technical.
```

These are product targets to validate, not contractual SLA.

---

# 212. Data Success Criteria

- duplicate rate measurable.
- Tax ID conflicts visible.
- Party merge never destroys posted history.
- no cross-tenant record access.
- import produces reconciliation report.
- search resolves Arabic/English aliases.

---

# 213. Technical Definition of Done

Before Directory is approved:

- [ ] Manifest valid.
- [ ] Schema migrations.
- [ ] RLS forced.
- [ ] Permissions.
- [ ] API.
- [ ] Contracts.
- [ ] Events through Outbox.
- [ ] Duplicate engine.
- [ ] Merge.
- [ ] Import.
- [ ] Search.
- [ ] Frontend.
- [ ] RTL.
- [ ] Architecture tests.
- [ ] Cross-tenant tests.
- [ ] Performance baseline.
- [ ] Observability.
- [ ] Runbook.
- [ ] API client regenerated.
- [ ] Module activation integration.

---

# 214. Product Gate Before Catalog

Before moving to `05 Catalog/Product/UOM`, Product Owner must approve:

1. Party definition.
2. Customer/Supplier role model.
3. Contact model.
4. Address model.
5. legal identifier model.
6. duplicate policy.
7. merge policy.
8. Quick Create UX.
9. readiness concept.
10. multi-entity visibility.
11. module profile boundary.
12. import workflow.

If any remains ambiguous, Catalog can technically start but Sales/Finance requirements must not lock against an unstable Party contract.

---

# 215. Senior Product Decision — What We Are Deliberately Doing Better

Compared with common ERP patterns, our differentiator is not "more fields."

It is:

### 1. One identity

Customer + Supplier = one Party.

### 2. Contextual complexity

Simple customer is simple.

Legal complexity appears only when needed.

### 3. No duplicated module truth

Sales, Purchasing, Finance all point to same Party identity.

### 4. Snapshot history

Changing master data does not rewrite history.

### 5. Migration-first thinking

Excel onboarding is part of product.

### 6. Multi-entity from architecture

But simple company UI remains simple.

### 7. Role-focused experience

Salesperson does not become master-data clerk.

---

# 216. Example Business Walkthrough — Wholesale Distributor

Company:

```text
Delta Distribution
```

Customer:

```text
Al Amal Stores
```

Directory:

```text
Party
  Al Amal Stores
  Organization

Roles
  Customer

Identifiers
  VAT / CR

Addresses
  HQ Billing
  Giza Store
  Nasr City Store

Contacts
  Mahmoud — Purchasing
  Hany — Accounts
  Ali — Receiving
```

Sales profile later:

```text
Salesperson: Ahmed
Pricelist: Wholesale A
```

Credit profile later:

```text
Credit Limit: ...
Payment Terms: ...
```

Order 1:

```text
Bill To: HQ
Ship To: Giza
Contact: Mahmoud
```

Invoice:

snapshots:

```text
Legal Name
VAT
Billing Address
```

Delivery:

snapshots:

```text
Giza Address
Ali / Receiving
```

This is how the system matches real company workflow without duplicating the customer.

---

# 217. Example Business Walkthrough — Customer + Supplier

Party:

```text
Tech Services Co.
```

Roles:

```text
Customer
Supplier
```

Sales sees:

```text
Sales Profile
Orders
Invoices
```

Purchasing sees:

```text
Supplier Profile
POs
Bills
```

Finance sees:

```text
Receivable
Payable
```

Directory sees:

```text
one legal identity
```

Future netting, if implemented, happens in Finance/Payments—not Directory.

---

# 218. Example Business Walkthrough — Accounting Office

External accountant Identity has Membership in:

```text
Tenant A
Tenant B
Tenant C
```

Each Tenant has its own Directory.

Same real-world customer name across tenants is NOT shared globally.

Tenant is privacy boundary.

Never create global cross-customer Party database across SaaS tenants.

---

# 219. Example Business Walkthrough — Group Companies

Tenant:

```text
Holding Group
```

Legal Entities:

```text
Company A
Company B
```

Party:

```text
Supplier X
```

Visibility:

```text
A + B
```

Supplier profile:

```text
Company A → Currency USD
Company B → Currency EUR
```

Identity shared.

Policies entity-specific.

---

# 220. Example Business Walkthrough — Branch Is Not Legal Entity

Customer:

```text
Retail Chain X
```

20 stores.

If stores are not separately invoiced legal entities:

Do not create 20 Customers.

Create:

```text
1 Party
20 Shipping Addresses
```

Optionally Contact per store.

This reduces duplication massively.

---

# 221. Example Business Walkthrough — Separately Billed Branch

If branch has:

- separate Tax ID.
- separate legal invoice recipient.
- separate contract/payment liability.

Then create separate Party, with hierarchy relation to parent.

This distinction must be taught in UI/help.

---

# 222. User Education

Inline guidance:

> **Add another address** if it is the same legal customer at another location.

> **Create another Party** if the branch is independently billed or has a different legal/tax identity.

This simple rule avoids years of bad master data.

---

# 223. Onboarding Wizard

When tenant first starts:

Offer:

```text
Add manually
Import Excel
Connect system later
```

For import:

show downloadable template.

---

# 224. Demo Data

Development/demo environment may have:

```text
Al Noor Trading
Customer+Supplier
multiple addresses/contacts
```

Production seed has none.

---

# 225. Documentation for Customer Admin

Need help articles:

```text
Customer vs Contact vs Address
When to create another Party
Customer who is also Supplier
How merge works
How import matching works
How legal data readiness works
```

---

# 226. Support Tooling

Support/admin can inspect:

```text
Party ID
Merge redirect
External refs
Duplicate reasons
Visibility
Audit
```

without raw DB.

---

# 227. Runbook — Duplicate Incident

If many duplicates imported:

1. stop further import.
2. identify batch.
3. generate candidate groups.
4. review strong IDs.
5. merge with contributor checks.
6. reconcile counts.
7. resume.

---

# 228. Runbook — Wrong Merge

Normal UI has no blind unmerge.

Escalate.

Evaluate affected module references.

Use controlled repair plan with audit.

---

# 229. Runbook — Tax ID Conflict

1. inspect Parties.
2. verify country/type.
3. check migration source.
4. correct identifier or merge.
5. audit reason.

---

# 230. Observability

Metrics with safe cardinality:

```text
directory_search_duration
directory_search_errors
directory_duplicate_candidates_total
directory_import_rows_processed
directory_import_errors_total
directory_merge_failures_total
```

Do not label by Party ID/Tenant ID in Prometheus.

Traces/logs can carry Tenant ID.

---

# 231. Alerts

Potential:

- import failure spike.
- duplicate detection job stuck.
- merge operation failure.
- search latency regression.

Directory does not need 24/7 pager for every duplicate warning.

---

# 232. Message Broker Use

Use RabbitMQ for:

- Party identity changed notifications to projections/integrations.
- merge event propagation.
- large import jobs.
- optional enrichment/OCR.
- external connectors.

Do not use RabbitMQ for immediate:

```text
SearchParty
GetPartySnapshot
ValidateRole
```

These are synchronous in-process.

---

# 233. Transaction Boundary — Create

Create Party + roles + contacts + audit + outbox event:

same DB transaction where appropriate.

---

# 234. Transaction Boundary — Merge

Merge core Directory state transactionally.

Cross-module mutable reference updates may use:

- synchronous contributors if invariant.
- post-commit events/read model update otherwise.

Do not hold one giant transaction over millions of rows.

---

# 235. Merge Strategy for Large Data

Mark canonical redirect first under safe transaction.

Then background consumers/projectors repair non-critical read models.

Critical writable module references must honor redirect resolver.

---

# 236. Party Resolver

Provide:

```text
ResolveCanonicalPartyId(partyId)
```

follows merge redirect.

Prevent long chains by path compression/background normalization.

---

# 237. Redirect Chain

Maximum logical behavior:

```text
A → B → C
```

resolver returns C.

Merge implementation should rewrite A→C when safe to avoid growing chain.

---

# 238. Search Merged Records

Default:

do not show source duplicates.

If user searches old name/external code:

result can show:

```text
Merged into Al Noor Trading
```

and redirect.

---

# 239. Historical Display

Old document may display original snapshotted name.

Navigation to Party opens current canonical Party.

---

# 240. Public API Behavior After Merge

GET old Party ID:

Option:

```text
200 canonical representation + canonicalId
```

or explicit redirect semantics.

For REST JSON, prefer stable response indicating:

```text
status: merged
canonicalPartyId
```

rather than HTTP 301 assumptions.

---

# 241. External Integration Mapping After Merge

External refs from source must not be lost.

Possible:

```text
both external refs → canonical Party
```

if no conflict.

---

# 242. Product Boundary — Directory vs CRM

Directory:

> known business identity.

CRM:

> relationship development/opportunity.

A Party can exist without CRM.

CRM cannot own canonical billing identity.

---

# 243. Product Boundary — Directory vs Sales

Directory:

> who.

Sales:

> how we sell.

---

# 244. Product Boundary — Directory vs Finance

Directory:

> legal/business identity.

Finance:

> balances, credit, payment terms, account determination.

---

# 245. Product Boundary — Directory vs Purchasing

Directory:

> supplier identity.

Purchasing:

> sourcing/procurement relationship.

---

# 246. Product Boundary — Directory vs HR

Directory external parties.

HR employees.

Do not collapse into universal Person table.

---

# 247. Product Boundary — Directory vs IAM

Directory Contact ≠ login Identity.

A Contact can exist without access.

Identity can exist without Party Contact (external accountant/internal admin).

---

# 248. ADR Candidates

Create:

```text
ADR-DIR-001 Party + Roles Model
ADR-DIR-002 Address/Contact Separation
ADR-DIR-003 Tenant-Level Party with Entity Visibility
ADR-DIR-004 Identifier Model via Country Pack
ADR-DIR-005 Duplicate Detection Policy
ADR-DIR-006 Merge + Redirect Strategy
ADR-DIR-007 Party Snapshot Contract
ADR-DIR-008 Search Strategy Postgres First
ADR-DIR-009 Custom Data Boundary
```

---

# 249. Implementation Order

```text
D00 Manifest + Permissions
D01 Database Schema + RLS
D02 Party Identity
D03 Roles
D04 Contact Points
D05 Contact Persons
D06 Addresses
D07 Identifiers + Country Hook
D08 Search
D09 Duplicate Detection
D10 Quick Create API/UI
D11 Full Party Workspace
D12 Visibility Scope
D13 Archive/Restore
D14 Merge/Redirect
D15 Readiness Framework
D16 Import/Export
D17 Integration Events
D18 Cross-Module Contracts
D19 Performance + Security Tests
D20 Product Acceptance
```

---

# 250. D00 — Manifest + Permissions

Deliver:

- module manifest.
- contracts project.
- permissions.
- route registration shell.
- frontend module shell.

Exit:

Architecture CI passes.

---

# 251. D01 — Database + RLS

Create schema/tables/migrations.

Every tenant table RLS.

Exit:

Cross-tenant tests pass with runtime DB role.

---

# 252. D02 — Party Identity

Implement:

- create.
- update.
- get/list.
- optimistic concurrency.
- audit.

Exit:

Organization + Individual flows work.

---

# 253. D03 — Roles

Implement:

- add/remove customer/supplier role.
- status.
- permissions.

Exit:

same Party can have both.

---

# 254. D04 — Contact Points

Phone/email/WhatsApp model.

Normalization.

Search.

---

# 255. D05 — Contact Person

Contacts and Party links.

Primary/context usage.

---

# 256. D06 — Addresses

Multiple addresses.

Usage/defaults.

Country formatting hook.

---

# 257. D07 — Identifiers

Identifier metadata.

Country Pack port.

duplicate strong signal.

---

# 258. D08 — Search

Party Picker backend.

Arabic/English.

Phone/ID search.

---

# 259. D09 — Duplicate Detection

Candidate service.

Reasons.

warning/block rules.

---

# 260. D10 — Quick Create

Sales/Purchasing-friendly UX.

Do not wait for Sales module; build reusable Directory component.

---

# 261. D11 — Party Workspace

Full role-aware page.

---

# 262. D12 — Visibility

Legal entity scope.

---

# 263. D13 — Archive

Archive/restore lifecycle.

---

# 264. D14 — Merge

Contributor framework.

Redirect.

Audit.

---

# 265. D15 — Readiness

Capability readiness rules.

Platform/Country/module contributor interfaces.

---

# 266. D16 — Import

Simple customer/supplier templates.

Advanced normalized templates.

Duplicate review.

---

# 267. D17 — Events

Outbox integration.

Integration event contracts.

---

# 268. D18 — Cross-Module Contracts

Stabilize:

```text
GetPartySummary
GetPartySnapshot
SearchParty
GetAddresses
GetContacts
Readiness
Canonical resolver
Merge contributor
```

---

# 269. D19 — Hardening

- load.
- security.
- concurrency.
- tenant isolation.
- import large batch.
- search.

---

# 270. D20 — Product Acceptance

Realistic demo:

```text
Distributor
Customer+Supplier
3 addresses
3 contacts
Arabic/English names
Duplicate
Merge
Excel import
Entity scope
```

If demo feels bureaucratic for a simple salesperson, UX is not approved.

---

# 271. Product Questions We Intentionally Leave to Later Module Packs

Do not finalize here:

- exact credit limit behavior.
- payment terms ownership details beyond boundary.
- salesperson assignment.
- purchase lead time.
- collections strategy.
- statement recipient rules.
- portal permissions.
- marketing consent.

Those are downstream domain decisions.

---

# 272. Evidence Classification

In this document:

**LOCKED**  
Architecture/product decision approved for our ERP.

**COMPETITOR EVIDENCE**  
Observed behavior in current competitor documentation.

**PRODUCT RECOMMENDATION**  
Our chosen design based on workflow/architecture.

**LEGAL VERIFICATION**  
Country-specific legal details that must be verified officially before release.

No competitor behavior is copied merely because it exists.

---

# 273. Source Register

Research checked on 2026-10-05.

## Odoo

Contacts:
https://www.odoo.com/documentation/20.0/applications/essentials/contacts.html

Delivery and Invoice Addresses:
https://www.odoo.com/documentation/17.0/applications/finance/accounting/customer_invoices/customer_addresses.html

Key evidence used:

- Person/Company contacts.
- central contact record.
- invoice vs delivery addresses.

## Microsoft Dynamics 365 Business Central

Manage Contacts:
https://learn.microsoft.com/en-us/dynamics365/business-central/marketing-contacts

Create Business Contacts:
https://learn.microsoft.com/en-us/dynamics365/business-central/marketing-create-contact-companies

Key evidence:

- Contact can precede formal business relation.
- conversion/linking to Customer/Vendor.
- duplicate management.
- synchronization concepts.

## ERPNext

Customer:
https://docs.frappe.io/erpnext/customer

Supplier:
https://docs.frappe.io/erpnext/supplier

Address:
https://docs.frappe.io/erpnext/address

Contact:
https://docs.frappe.io/erpnext/contact

Key evidence:

- multiple addresses/contacts.
- billing/shipping distinctions.
- Contact and Address modeled separately.
- customer/supplier defaults.

## Zoho Books

Customers & Vendors:
https://www.zoho.com/in/books/help/contacts/

Link Customer and Vendor:
https://www.zoho.com/us/books/help/contacts/link-customer-and-vendor.html

Key evidence:

- same real-world entity may be both customer and vendor.
- import and separate customer/vendor workflows.

## Daftra

Knowledge Base:
https://docs.daftra.com/en/

Adding Supplier:
https://docs.daftra.com/en/tutorial/adding-a-new-supplier/

Key evidence:

- Arabic-market practical supplier data.
- CR/VAT.
- multiple addresses/contacts.
- import.
- supplier numbering/currency.

---

# 274. Final Senior Product Verdict

The Directory is not "Customer Master."

It is:

> **The canonical identity layer for every external business relationship in the ERP.**

If designed correctly:

- Sales will never need a separate Customer identity database.
- Purchasing will never need a duplicate Supplier identity.
- Finance will trust one legal Party.
- CRM can convert leads safely.
- Imports can reconcile entities.
- Multi-company can scale.
- new modules can attach their own Profile without bloating Party.

The product rule is:

> **One Party identity. Many roles. Many contacts. Many addresses. Module-specific policies outside Directory.**

The UX rule is:

> **Ask the user only for what the current business action needs.**

The data-quality rule is:

> **Search before create. Match before import. Merge instead of duplicate. Snapshot history instead of rewriting the past.**

The technical rule is:

> **Directory owns identity and relationships; other modules consume stable contracts, never its DbContext.**

---

# 275. Next File

After this Directory pack is approved and implemented, the next domain pack should be:

> `05_ERP_SAAS_CATALOG_PRODUCT_UOM_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`

Its business question will be:

> **"What does the company sell, buy, stock, manufacture, or provide as a service—and how do we model it once so Sales, Purchasing, Inventory, POS, Manufacturing, Pricing and Accounting can all use it without duplication?"**

---

**END OF FILE**

# ToDir Business Analysis & Decision Recommendations — Consolidated Design Review

**Status:** Historical consolidated copy — non-normative reference only. The authoritative documents are `README.md` and `docs/00`–`18`; do not implement from this file.


---

# ToDir ERP — Business Analysis & Decision Recommendations | Design Pack

**Version:** 0.9 — Architecture/Product design baseline; **NOT approved for implementation**  
**Prepared:** 2026-11-22  
**Reference branch:** `dev`  
**Pinned source commit:** `8a06af00dd82ed8e6ee593c992993c25786200e1` (observed 2026-11-22 09:23:33 UTC)  
**Audience:** Product Owner, ERP Architect, Backend/DB, Frontend, Security, Performance, QA, implementation agents.

> **ما هذه الحزمة؟** مجموعة مواصفات مرتبة لتنفيذ تحليلات الأعمال والتوصيات داخل ToDir، مع الحفاظ على أداء ووظائف ERP الحالية. هذه وثائق تصميم لا تعني أن أي جداول أو APIs أو Worker جديد قد تم إنشاؤه. لم تُجر اختبارات runtime/benchmark في إعداد هذه الحزمة.

## القرارات التي نقلناها من متطلبات صاحب المنتج [USER-LOCKED]

1. صفحات تحليل مستقلة **داخل الموديول المعني** (مثل «تحليل المبيعات»، «تحليل تحصيلات العملاء»)؛ لا نضيف تحليلًا إلى صفحات الفواتير أو العمليات اليومية ولا نجبر المستخدم على قسم تقني اسمه «Intelligence & Analytics».
2. **Manual on-demand only:** المستخدم المصرّح له يختار نوع التحليل والمدى والتصفية ثم يضغط «بدء التحليل». فتح صفحة، تحديثها، تغيير الفلاتر، دخول شاشة أخرى، حدث أعمال، أو جدول دوري **لا ينشئ Analysis Run تلقائيًا** في النسخة المتفق عليها.
3. صفحة النتائج تقرأ **نتائج محفوظة**، وتعرض سجلًا تاريخيًا يمكن الرجوع إليه؛ لا إعادة حساب صامتة ولا تغيير للنسخ السابقة.
4. طبقة **اقتراحات قرارات** أساسية: أسباب، أدلة، أثر متوقع، مخاطر وحدود، مراجعة بشرية، ثم تنفيذ عبر Business Workflow الأصلي إن أُجيز؛ لا تنفيذ مباشر من AI ولا SQL في موديول آخر.
5. المسار اليومي في Sales/Accounting/Payments/Inventory/POS وغيرها لا ينتظر Analysis أو LLM؛ حماية الأداء وقابلية الصيانة **شرط قبول**، وليس مجرد هدف مرغوب.
6. استخدام النظام الحالي للصلاحيات والاشتراكات والـNavigation والـReusable Frontend Components؛ لا نظام موازي.
7. البدء بـ.NET؛ بنية قابلة لإضافة Python في المستقبل **بعد إثبات حاجة فعلية**، دون تشغيل خدمة Python جديدة حاليًا.
8. عدم الاكتفاء بقول Agent إن الموديول COMPLETE: القبول حسب المواصفات والاختبارات ونتائج القياس.

## ترتيب القراءة — لا تبدأ التنفيذ قبل مراجعة الوثائق بترتيبها

| الملف | المسؤولية |
|---|---|
| `docs/00_EVIDENCE_BASELINE_AND_DECISION_STATUS.md` | الكود الذي تمت مراجعته، الحقيقة مقابل الاقتراح وما لم يُثبت |
| `docs/01_PRODUCT_SCOPE_AND_USER_JOURNEYS.md` | تعريف المنتج، من يستخدمه، ما الذي لا نريده، User Journeys |
| `docs/02_BUSINESS_ANALYSIS_CATALOG.md` | أنواع التحليلات المرشحة في الموديولات؛ ترتيب الأولوية لا يعني موافقة على بناء الكل |
| `docs/03_ARABIC_UX_NAVIGATION_FILTERS.md` | أسماء الصفحات، الفلاتر، حالات الواجهة، الصلاحيات واكتشاف الروابط |
| `docs/04_TECHNICAL_ARCHITECTURE_AND_BOUNDARIES.md` | الشكل المعماري، الملكية، المخاطر، إعادة استخدام .NET والعقود |
| `docs/05_DOMAIN_FACTS_AND_READ_CONTRACTS.md` | الفصل بين الحقائق والتحليل، الـRead Models والـWatermarks وAs-of |
| `docs/06_JOBS_WORKERS_AND_ISOLATION.md` | API on-demand، Job Lifecycle، التنفيذ المعزول والفشل واستعادة التشغيل |
| `docs/07_SCHEMA_HISTORY_AND_VERSIONING.md` | مخطط تخزين مبدئي، ثبات التاريخ، Retention وMigration |
| `docs/08_RECOMMENDATIONS_AND_DECISION_WORKFLOW.md` | اقتراحات القرارات وتفسيرها وموافقتها ومتابعة نتائجها |
| `docs/09_ACCESS_SECURITY_AND_ENTITLEMENTS.md` | Tenant/RLS/Legal Entity/Branch/record scope والتفويض والتدقيق |
| `docs/10_PERFORMANCE_TESTS_AND_OBSERVABILITY.md` | خطة Baseline/Load/Chaos/Regression والـSLO/SLI |
| `docs/11_API_AND_CONTRACT_EXAMPLES.md` | أمثلة HTTP/JSON وأنواع البيانات؛ proposals وليست endpoints موجودة |
| `docs/12_COLLECTIONS_PILOT_DATA_TRUTH.md` | أول مرشح: تحليل تحصيلات العملاء، الفروق المالية وحالات الحواف |
| `docs/13_DELIVERY_ROADMAP_AND_ACCEPTANCE.md` | مراحل العمل، الاعتماديات، شروط المرور وDoD |
| `docs/14_ADRS_OPEN_QUESTIONS_AND_RISK_REGISTER.md` | القرارات المقترحة والبدائل والمخاطر والاعتراضات المفتوحة |
| `docs/15_TRACEABILITY_TEST_MATRIX.md` | ربط المتطلبات بالاختبارات والأدلة التي تُطلب من الـAgent |
| `docs/16_IMPLEMENTATION_AGENT_HANDOFF.md` | Contract إنجليزي للمراحل والـAgents، مع منع بدء كتابة الكود قبل الموافقة |
| `docs/17_PER_ANALYSIS_SPECIFICATION_TEMPLATE.md` | قالب تفصيلي إلزامي قبل تنفيذ تحليل جديد في أي موديول |
| `docs/18_IMPLEMENTATION_STRUCTURE_MODULE_MAP.md` | هيكل الكود والـDI وعقود التكامل وتسلسل تنفيذ أول Slice |

## وضع الملفات داخل مستودع ToDir لاحقًا

المسار المقترح: `D:\Web\full-projects\todir-erp\docs\enterprise-analysis\`؛ انسخ محتويات مجلد `TODIR_ENTERPRISE_ANALYSIS_DESIGN_PACK` بالكامل بداخله، مع الاحتفاظ بمجلدي `docs/` و`reference/` كما هما. **لم يتم تعديل GitHub أو الـworking tree في إعداد الحزمة.** بعد P0 يمكن تعديل هذا المسار أو دمج الملفات في توثيق المعمارية الرسمي بإجراء مراجعة منضبطة؛ لا تُعتمد الحزمة رسميًا لمجرد نسخها.

## قواعد استخدام الحزمة

- **ORDER:** ابدأ بمرحلة Discovery + baseline؛ ثم Architecture ADR/product sign-off؛ ثم Core foundation؛ ثم Pilot موثق؛ ثم التوسع. لا تقفز من الفكرة إلى تنفيذ كل الموديولات.
- **SOURCES:** روابط GitHub مربوطة بـCommit محدد. عند التنفيذ يجب إعادة مراجعة `origin/dev` الحالي ومقارنة الفروقات (لأن الفرع يتحرك يوميًا). الوثائق لا تدّعي أن اختبارًا تم تشغيله.
- **STATUS:** `USER-LOCKED` = متطلب صريح؛ `EXISTING-VERIFIED` = ظهر من قراءة ملفات repo؛ `PROPOSED-ADR` = قرار هندسي مرشح يحتاج اعتماد؛ `TO-VERIFY` = لا يجوز افتراضه قبل الاختبار؛ `FUTURE` = خارج النسخة الأولى.
- **OWNERSHIP:** الـERP يملك العمليات والحقائق؛ صاحب الموديول يملك عقود قراءته؛ Analysis يملك عمليات التحليل والنتائج والتوصيات فقط.
- **NAMING:** أسماء الـUI عربية من واقع العمل؛ أسماء الـAPIs التقنية تصف المورد/الوظيفة، ولا تُفرض تسمية «AI» في كل endpoint.
- **CHANGE CONTROL:** أي تغيير في متطلب `USER-LOCKED`، أو أي تعارض مع Master Blueprint، يحتاج ADR/موافقة صريحة وليس قرار Agent ضمني.

## Definition of Ready لهذه الوثائق

هذه حزمة **Design v0.9** وليست مواصفة تنفيذ نهائية، إلى أن يُنجز P0 ويُثبت: عقود القراءة الممكنة، صحة معاني البيانات، Baseline الأداء، فعالية الـRLS والاشتراكات، سياسة الصلاحيات الدقيقة، ونتيجة ADRs. الفجوات مذكورة صراحةً، لا يجوز ملؤها بالتخمين.

## References

- [Pinned ToDir repository](https://github.com/ahmedAbdelal96/todir-erp/tree/8a06af00dd82ed8e6ee593c992993c25786200e1)
- [Master Blueprint](https://github.com/ahmedAbdelal96/todir-erp/blob/8a06af00dd82ed8e6ee593c992993c25786200e1/docs/architecture/01_ERP_SAAS_MASTER_MODULE_AND_DEPENDENCY_BLUEPRINT.md)
- [Navigation and permissions architecture](https://github.com/ahmedAbdelal96/todir-erp/blob/8a06af00dd82ed8e6ee593c992993c25786200e1/docs/architecture/TODIR_NAVIGATION_ACCESS_UX_ARCHITECTURE_DECISION.md)
- [Backend solution boundaries](https://github.com/ahmedAbdelal96/todir-erp/blob/8a06af00dd82ed8e6ee593c992993c25786200e1/docs/architecture/solution-boundaries.md)
- [FP&A design pack](https://github.com/ahmedAbdelal96/todir-erp/blob/8a06af00dd82ed8e6ee593c992993c25786200e1/docs/modules/28_ERP_SAAS_BUDGETING_FINANCIAL_PLANNING_FPA_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md)
- The independent static review by Claude is included in `reference/CLAUDE_COLLECTIONS_AND_WORKLOAD_REVIEW_2026-11-19.md`; its conclusions are **review evidence**, not proven runtime behavior.


---

# ToDir — دليل مراجعة واعتماد وثائق تحليل الأعمال واقتراح القرارات

**هذه الحزمة ليست كودًا منفذًا ولا تصريحًا ببدء التنفيذ.** اتعملت علشان نمنع تكرار تصميم الوظائف والـAPIs، ونحمي الأداء والبيانات، ونحسم القرارات على أدلة.

## إيه اللي أصبح ثابتًا من متطلباتنا؟

- التحليل له صفحة مستقلة **داخل الموديول التجاري المناسب** باسم مفهوم للمستخدم، مش صفحة تقنية اسمها `Intelligence & Analytics`.
- المدير أو صاحب الصلاحية هو اللي يحدد **نوع التحليل، الفترة، الشركة/الفرع أو العميل/المخزن حسب الوظيفة**، وبعدها يضغط «بدء التحليل».
- مجرد فتح الصفحة أو عمل Refresh أو دخول صفحة فواتير أو تحديث الفلاتر لا يبدأ حسابات ولا ينشئ Jobs. مفيش تشغيل دوري تلقائي في النسخة الأولى.
- النتائج تحفظ كما نُشرت، بتاريخ بياناتها ووقت التنفيذ وإصدار الطريقة، مع تاريخ كامل قابل للعرض والصلاحيات.
- التوصيات جزء أصيل: السبب والأدلة والحلول المحتملة والمخاطر والقرار البشري والمتابعة. لا اعتماد مالي أو تنفيذ تشغيل تلقائي.
- Functions/Contracts الخاصة بالقراءة تضاف عند الحاجة، دون تكرار Business Logic أو وضع Analytics على مسار المعاملات اليومية.
- تنفيذ .NET أولًا، Python مستقبلًا فقط بدليل. الـWorker منفصل عن العمليات الثقيلة، لكن بدون ادعاء ضمان الأداء إلا بعد اختبار حقيقي.

## إيه اللي حددناه هندسيًا لكنه محتاج إثبات قبل التنفيذ؟

- `Analysis` كـBounded Context في Layer L6، منفصل المسؤولية وليس ERP ثانيًا.
- Existing owner contracts/read models بدل direct cross-module SQL.
- PostgreSQL job store + dedicated .NET worker؛ عدم إضافة broker جديد افتراضيًا.
- Schema خاص بالـruns/results/recommendations/evidence/history، مع History immutability.
- نظام صلاحيات/اشتراكات الحالي مع صلاحيات جديدة دقيقة للعرض والطلب ومراجعة التوصيات.
- Start with Collections Pilot 0 **مش شرط**؛ اللي هيحسمه جودة بيانات المصدر وإمكانية قياس قيمة المنتج في مصر.

## إيه اللي ممنوع على Agent يعمله بدون موافقة؟

- يبدأ بناء كل الموديولات من جدول التحليلات المرشحة.
- يعدل فواتير/قيود/Stock Posting أو يجعلها تنتظر AI.
- يضيف قواعد بيانات/Permissions/Subscriptions بديلة.
- يعمل scheduled/event-triggered analyses أو يطلب تحليل كل ما المستخدم يفتح صفحة.
- يستخدم `ApplicationDate` كأنه تاريخ التحصيل النقدي الحقيقي.
- يضيف Python، vector DB، DWH أو Microservice من غير Benchmark/ADR.
- يدّعي Performance PASS أو جاهزية إنتاج على أساس static review أو Tests Mock فقط.

## خطة اعتماد المراحل

| المرحلة | المطلوب قبل التحرك |
|---|---|
| **P0** | Actual code discovery + baseline performance + cash-date truth + owner contract map |
| **P1** | ADRs signed off + Product/UX filters and labels + approval of pilot definition + accepted performance budgets |
| **P2** | Foundation security/migrations/API/worker/history/recommendation flow built and tested end-to-end |
| **P3** | A real approved use case connected to canonical data and verified with financial fixtures and business reviewers |
| **P4** | Failure/permissions/mixed-load/regression and safe release/rollback tests |
| **P5** | Repeat per valuable module using shared engine, one verified use case at a time |

## المصدر مقابل الاقتراح

هذا التصميم قرأ فرع `dev` عند `8a06af00dd82ed8e6ee593c992993c25786200e1` واكتشف أن Manufacturing موجود بالفعل، وأن نظام الصلاحيات والـNavigation والـWorkers/RabbitMQ/Quartz والـSubledger/Payments موجودين. **ده فحص static وليس إثبات إن كل الموديولات انتهت أو إن أحمال الأداء مقبولة.** لا نعيد استخدام تقرير Claude الأقدم كأنه Snapshot أحدث.

اقرأ `README.md` وترتيب الملفات قبل إسناد مهام للـAgent. ابدأ P0 فقط. كل نقطة غير محسومة موجودة في `docs/14_ADRS_OPEN_QUESTIONS_AND_RISK_REGISTER.md`، وكل شروط القبول في `docs/15_TRACEABILITY_TEST_MATRIX.md`.


---



---

<!-- source: docs/00_EVIDENCE_BASELINE_AND_DECISION_STATUS.md -->

# 00 — Repo Evidence, Baseline & Decision Status

**Status:** Verified static inspection / discovery input only. **No runtime testing has been executed for this package.**

## Snapshot and review methodology

Source of evidence: GitHub connector reading `dev` pinned at `8a06af00dd82ed8e6ee593c992993c25786200e1` (branch observed on 2026-11-22). Examined master blueprints, selected module manifests, module contracts, selected backend service/repository code, actual frontend routes/navigation, Worker/Program and Messaging Hosting, Subledger/Payments schema excerpts, and uploaded independent Claude review (based on older `d07dd58`). **Not a codebase-wide audit**; source facts below are limited to examined files. Code evidence ≠ exercised behavior/production readiness.

| ID | Finding | Evidence | Class | Consequence |
|---|---|---|---|---|
| EV-01 | .NET Modular Monolith, PostgreSQL, React/TypeScript and shared business modules exist | `README.md`, `backend/`, `frontend/` | EXISTING-VERIFIED | No greenfield architecture; reuse patterns |
| EV-02 | 24 directories under `backend/Modules`, now including **Manufacturing**; Budgeting implementation directory absent | GitHub `backend/Modules` listing at pinned SHA | EXISTING-VERIFIED | Claude's `d07dd58` absence of Manufacturing is **outdated** |
| EV-03 | 29 packs under `docs/modules`, including Manufacturing and FP&A | GitHub `docs/modules` listing | EXISTING-VERIFIED | Spec existence does NOT imply ready module |
| EV-04 | Master Blueprint locks single source of truth, single writer, modular boundaries and forbids direct AI database access | `docs/architecture/01_...BLUEPRINT.md` sections 1,8,9,28.34,28.38 | EXISTING-VERIFIED | Analysis must use approved capabilities/projections, not direct cross-schema queries |
| EV-05 | Architecture tests enforce module implementation isolation | `docs/architecture/solution-boundaries.md`, `backend/Tests/Architecture` | EXISTING-VERIFIED | New module and host must preserve these gates |
| EV-06 | UI navigation uses destination registry/effective-access and separate task routes | `frontend/app/router/destination-registry.ts`, `frontend/modules/sales/routes.tsx`, `frontend/app/shell/app-shell.tsx` | EXISTING-VERIFIED | Add analysis destinations via established registry, not separate sidebars |
| EV-07 | `Worker/Program.cs` runs many MassTransit consumers and hosted services in one host | `backend/Worker/Program.cs` | EXISTING-VERIFIED | CPU/memory isolation requires a separate executable/deployment role |
| EV-08 | `MessagingHostingExtensions` configures RabbitMQ quorum endpoints and common prefetch 16 | `backend/Core/Messaging/MessagingHostingExtensions.cs` | EXISTING-VERIFIED | Do not indiscriminately append heavy analytic consumer to operational bus |
| EV-09 | Outbox dispatch runs every 5 seconds and calls `claim_pending_outbox(100)` | `backend/Worker/OutboxDispatchWorker.cs` | EXISTING-VERIFIED | Track broker and outbox lag in acceptance; not measured here |
| EV-10 | Quartz is configured for Postgres persistence and clustering | `backend/Core/Scheduling/SchedulingHostingExtensions.cs` | EXISTING-VERIFIED | Scheduling available, but user forbids auto-creation of analysis runs in v1 |
| EV-11 | `Subledger` has due dates, application and reversal records, as-of balance logic and scoped queries | `backend/DbMigrator/Database/bootstrap/subledger-schema.sql`; `SubledgerRepository.cs` | EXISTING-VERIFIED | Potential Collections descriptive data foundation; cannot infer true cash receipt date from application date |
| EV-12 | `PaymentsService.ApplyToPaymentAsync` forwards request `ApplicationDate` / `SettlementDate` | `backend/Modules/Payments/Services/PaymentsService.cs` | EXISTING-VERIFIED | Analyze allocations and provenance before predicting payment behavior |
| EV-13 | Payments table has `payment_date`, `value_date`, `posting_date`, `completed_at`, `reconciliation_state` | `backend/DbMigrator/Database/bootstrap/payments-schema.sql` | EXISTING-VERIFIED | Date semantics and bank-cash proof require investigation |
| EV-14 | Banking has existing `cash_forecast_version`, `cash_forecast_item`, read and create methods | `backend/Modules/Banking/Repositories/BankingRepository.cs` | EXISTING-VERIFIED | Prevent duplicate commercial forecast ownership |
| EV-15 | CRM forecast code exists; older static review found deterministic pipeline weighting, not ML | `backend/Modules/CRM/Repositories/CrmRepository.cs` and independent report | STATIC-REVIEW | Forecast label != ML guarantee; re-verify before implementation |
| EV-16 | FP&A specs explicitly separate Actual / Budget / Forecast / Scenario and `AI Suggestion != Approved Forecast` | `docs/modules/28_...FPA...md` §§4–30 | EXISTING-VERIFIED-DOC | Never publish a recommendation as an approved financial plan |
| EV-17 | Product needs authorized user-triggered analysis, filters, saved history and recommendations | Product-owner decisions in this conversation | USER-LOCKED | No background auto-run / implicit page-triggered computation |

## Source links (pin to SHA)

Base URL `https://github.com/ahmedAbdelal96/todir-erp/blob/8a06af00dd82ed8e6ee593c992993c25786200e1/`, followed by exact paths above. The running branch **must be reverified** before implementation, and any baseline drift documented.

## Open investigation ledger — these are NOT settled facts

| ID | Unknown | Cheapest reliable verification |
|---|---|---|
| U-01 | Actual performance P50/P95/P99 and DB/broker throughput | Local/staging baseline at realistic data size; no guessed numbers |
| U-02 | Complete payment-to-bank confirmation lifecycle, including method-specific value dates | Read code paths/triggers and real-stack test plus accounting owner review |
| U-03 | True as-of knowledge-time reconstruction under retroactive operations | Temporal truth tables + seeded controlled fixtures + matching historical reports |
| U-04 | Number of useful invoices per customer and quality of client historical data | Permissioned audit on sanitized client data; no unrestricted production extracts |
| U-05 | Whether adding an Analysis module requires new manifest type/permission layout | Build a minimal nonproduction architectural fixture; run architecture validators |
| U-06 | Whether all targeted analysis dimensions exist and are stable across modules | Read each owner pack + code + data contract before catalog promotion |
| U-07 | Tenant resource quotas and noisy-neighbor behavior in real hosting | Simulate concurrent workers/tenants under controlled workloads |
| U-08 | Current deploy/scale resource limits in production | Inspect actual environment, not Terraform alone |
| U-09 | Customer demand and willingness to pay for first analysis | Egyptian business interviews and observed manual decisions |
| U-10 | Exact module entitlements under current SaaS packaging | Review Kernel entitlement checks and live UX before choosing pricing |

## Governing conflict-resolution order

1. Business/user requirements expressly stated here, subject to hard security and correctness checks.
2. Approved existing repo Master Blueprint / ADRs and current module owner specifications.
3. Verified implementation; mismatches become documented drift, not assumed approval.
4. This package's `PROPOSED-ADR` sections, pending approval.
5. Independent reviewer suggestions / other AI opinions are evidence to assess, never authority.

If sources conflict: **stop the affected slice, record the exact conflict, propose a measured choice and ADR; do not silently rewrite production behavior.**


---



---

<!-- source: docs/01_PRODUCT_SCOPE_AND_USER_JOURNEYS.md -->

# 01 — Product Requirements and User Journeys

**Language:** Arabic-first for Egyptian businesses; natural terms, not literal technology translations. **Status:** USER-LOCKED experience + proposed refinement requiring acceptance.

## Problem and success criteria

ToDir already records and executes operational/financial transactions. The new feature lets an authorized manager **request an analysis of selected business facts**, examine results and evidence, see **business decision recommendations**, review them, and return to immutable historical run records. It must not distract the user or impair daily work.

Success is **not** number of charts or use of LLM. Success = trustworthy answers to operational decisions, appropriate scope, explainability, repeatable history, useful options, measurable impact, fast day-to-day ERP.

## User personas and their tasks

| Persona | Real questions | Needs and constraints |
|---|---|---|
| Company Owner / GM | Which areas are deteriorating? What options do I have? | Multi-domain summary of **saved authorized runs** only; no implicit computation; avoid financial jargon without explanation |
| Collections Manager | Which overdue customers require attention and why? | AR/paid vs noncash separation, scope by entity/team, drill to invoices, partial payments, proposed follow-up action |
| Sales Manager | Which products/branches/customers underperform during selected dates? | Revenue basis clearly named (orders / issued invoices / posted invoices), returns, taxes, currency |
| Inventory Manager | Which products show risk of shortage or overstock? | Warehouse, product, UOM, history/cutoff, reservations and inbound commitments if actually supported |
| Procurement Manager | Where are supplier delays or price deviations? | Supplier/PO/timeframe, approved vs received, currency/units |
| Finance Manager | What changed in cash exposure or working capital? | Legal entity, FX policy, ledger/subledger source and cutoff, no fake bank confirmations |
| Production Manager | Which completed orders had cost/quantity/capacity deviations? | Operational definitions and production evidence; do not mistake MRP planned data for actual |

No automatic suggestion execution; a recommendation may not exist when data is missing or analysis does not justify one.

## Core experience — 3 independent user operations

### Journey J-01: request new analysis

1. Authorized manager enters **the module's dedicated analysis page** (not its operational list).
2. GET obtains only **available analysis definitions, prior saved runs and permissions**. This must not start computation.
3. Manager selects analysis type (business label), legal entity/branch within effective scope, **date interval**, suitable filters/dimensions, comparison/metric basis if meaningful.
4. Client validates form; server repeats validation, checks authorization and entitlement; may return no-run if data availability prerequisite not met.
5. Manager confirms and clicks **«بدء التحليل»**. This is the **only permitted run-creation trigger** in v1 (a genuine authenticated POST). Request contains idempotency key.
6. Server creates durable job and returns run ID immediately; frontend shows Queued / Running / Succeeded / Failed / Canceled.
7. Results, warnings and any justified recommendations are saved and published atomically. Manager can leave page.
8. No automatic rerun or implicit update when open page, refresh, switch tenant, or view a prior run.

### Journey J-02: review past results

- Manager searches saved runs by period/filter/creator/status/type within authorized scope, sorted with cursor-based pagination.
- Opens run details and sees business period, scope, options, **data cutoff + job execution time**, input revision, metrics, quality warnings, recommendations and decisions; filter criteria may be read-only in details.
- Historical records remain versioned and **not silently overwritten**. If data changed, old run may display `بيانات أحدث متاحة` or `نتيجة قديمة` without editing original results. A new run requires a new explicit click.
- Access to old results is always re-evaluated against current permissions/tenant/entity/branch, not just the access at creation.

### Journey J-03: review recommendation and action

- Review recommendation tied to a specific run; see source facts, assumptions, options, expected benefit/risk, uncertainty, conflicts and freshness.
- Authorized person records `اعتماد التوصية للمراجعة التشغيلية`, `رفض`, or `تحتاج دراسة` with reason. In v1 this is not equivalent to executing a business transaction.
- If later enabling action handoff, module owner **re-validates current business state and current permissions**, then opens owner workflow/Draft only via approved command. Actual final approval remains within owner domain.
- Save follow-up and observed outcome separately; comparison does not rewrite historical recommendation.

## Explicit non-goals for v1

- No analysis when user opens or refreshes any page, **even the analysis page**.
- No scheduled/nightly/event-created analysis runs.
- No LLM interpretation of raw SQL or direct reads/edits to accounting tables.
- No auto-generated credit restriction, stock transfer, purchase order, journal, bank settlement, payroll change or production release.
- No user-managed arbitrary SQL/Python/rule scripting.
- No per-module independent AI engines, ACL engines or custom sidebars.
- No enterprise-wide all-modules rollout in a single release; no claim of statistically valid forecasting until real data and backtesting.
- No mandated Python service, model training pipeline, vector DB, DWH, microservices or GPU.

## Functional requirements (IDs are implementation-traceable)

| ID | Priority | Requirement / rationale |
|---|---|---|
| PR-001 | MUST | Dedicated business-named route inside each **enabled** module with a real approved use case; never embed analysis runtime inside daily operation routes |
| PR-002 | MUST | Reading analysis page, changing filters, reading history causes **zero AnalysisRun creation and zero compute jobs** |
| PR-003 | MUST | Explicit authorized POST + idempotency is the only v1 analysis trigger |
| PR-004 | MUST | Validated typed filters per analysis, start/end dates, legal entity scope, relevant dimensions and a clearly stated metric/time basis |
| PR-005 | MUST | Server enforces independent read/run/recommendation-review privileges, module availability and record/scope restrictions |
| PR-006 | MUST | Durable run status/progress and recovery; jobs survive user leaving UI |
| PR-007 | MUST | Historical result integrity: run input/scope/cutoff/model version/results/evidence visible and immutable after publish |
| PR-008 | MUST | Recommendations reference exact run and evidence, no unsupported claims or fictitious confidence |
| PR-009 | MUST | No automatic financial/stock/legal write or approval from the analysis engine |
| PR-010 | MUST | User can filter/paginate history and inspect exact prior output, including failed run metadata without sensitive traces |
| PR-011 | MUST | No regression of daily operational SLOs under mixed workloads |
| PR-012 | MUST | Data insufficiency yields explicit `لا توجد بيانات كافية` / partial result with warnings, not hallucinated totals |
| PR-013 | MUST | Existing shared frontend components, Arabic RTL, English parity, navigation/effective-access conventions |
| PR-014 | SHOULD | Explain why recommendation made, alternative action and possible downside; may expose `not enough evidence` |
| PR-015 | SHOULD | Scope-preserving export of completed run with provenance; prohibit historical result mutation |
| PR-016 | FUTURE | Cross-module company-wide saved-results view, only after ownership/permissions/tenant scope are settled |

## Product decision gates before release

- Does each selected analysis answer a measurable manager decision? If not, do not build it.
- Are source facts and date semantics documented and testable?
- Are output warnings and recommended actions natural to Egyptian business practice?
- Can the original source of every critical financial number be traced to owner modules?
- Can users with narrow access discover only their authorized analysis route and nothing else?
- Under large data, can run be canceled or queued without blocking normal transactions?

## First release recommendation

Candidate = «تحليل تحصيلات العملاء» **Pilot 0 descriptive/decision hygiene**. Not commercially committed: pass Data Readiness + market-fit gate before locking the pilot. If data truth fails, compare Inventory or Sales alternatives without changing the shared foundation architecture.


---



---

<!-- source: docs/02_BUSINESS_ANALYSIS_CATALOG.md -->

# 02 — Candidate Business Analysis Catalog (Not a Build-All Backlog)

**Purpose:** Useful Egyptian business questions first, then their filters/facts/outputs/recommendations. These are **candidate product requirements**, NOT claims that source contracts, data quality or model accuracy already exist. Promotion into committed backlog requires code evidence + source owner + business validation + test cases.

## Registry entry template — mandatory per use case

`analysis_code` (stable internal key), Arabic business label, owning module, target persona, decision question, data time basis, period applicability, eligible filters, source owner/contracts, calculation definitions/formulas, currencies/units, exception and return handling, data-readiness metrics, expected output shapes, candidate recommendations, permissions, performance limits, backtest requirements (when forecasting), UX states, precise acceptance fixtures.

## Prioritized candidates — domain-specific, not marketing terms

| Stage | Module / analysis page | Candidate analysis / real decision | Input scope and filters | Required authoritative facts | Potential recommendation; never auto-execute |
|---|---|---|---|---|---|
| P3 candidate | Subledger/Payments — **تحليل تحصيلات العملاء** | Overdue aging; payment allocation delays; customer collection history | Legal entity; as-of date; period for cash; customer/team; currency; overdue band | AR open items, applications, actual payment completion, credit notes/write-offs/reversals | Prioritize follow-up on customers whose exposure and historical delays justify it; flag unallocated receipts |
| P4 | Sales — **تحليل المبيعات** | Sales by products/customers/branches; growth/change from comparison period | From/to; invoice vs order basis; branch, product group, customer, currency | Sales actuals with returns/cancellations and posting basis | Review declining product group or unusually high returns; no unverified pricing advice |
| P4 | Inventory — **تحليل المخزون** | Slow movement, shortages and overstocks, physical vs available | Warehouse, product/category, date interval, UOM, as-of | Stock ledger, inventory balance/valuation, reserved and inbound evidence | Review reorder/stock-transfer candidate; never reserve/transfer automatically |
| P5 | Purchasing — **تحليل المشتريات** | Supplier lead-time and purchase-price deviation | Supplier, category, legal entity, date period, currency, receiving stage | Approved POs, receipts, agreed terms/pricing, reversals | Review supplier reliability, investigate cost changes |
| P5 | Banking — **تحليل السيولة** | Cash exposure from confirmed facts and what-if shortfalls | Cash accounts, legal entity, as-of, forecast horizon, currency | Bank/cash balances, statement match confidence, AR/AP obligations | Review collections/payments timing; not override Banking cash-forecast version |
| P5 | Accounting — **التحليل المالي** | Variances in margins/costs, period comparisons | Legal entity, financial period, reporting basis, currency | Posted ledger, P&L, TB, close state | Investigate anomaly with journal evidence; no fabricated causes |
| P5 | CRM — **تحليل فرص البيع** | Pipeline conversion and stale opportunities | Pipeline, salesperson, stage, period | Opportunities and stage history, activities, closed outcomes | Follow up with neglected opportunity; don't call weighted pipeline calculation ML |
| P5 | POS — **تحليل مبيعات نقاط البيع** | Sales mix, returns, session/cash reconciliation trends | Store, cashier if allowed, shifts, date period | Closed sessions, sales and refunds, settlement state | Investigate unusually high returns or till variance |
| P6 | Manufacturing — **تحليل الإنتاج** | Production cost/scrap/lead time deviation by work center/order | Plant, order, product, period, approved BOM version | Confirmed production events, actual costs, genealogy/quality gates | Investigate high scrap/downtime; never publish planned MRP as Actual |
| P6 | Projects — **تحليل تكاليف المشروعات** | Budget/actual progress and margin variance | Project, work package, fiscal period | Project costs, approved commitments and posted accounting facts | Review project cost overruns; ownership with Projects and Accounting |
| P6 | HR/Payroll — **تحليل تكاليف العاملين** | Payroll trends, overtime and department cost | Legal entity, department, payroll run, period | Authorized payroll and HR facts | Review workforce cost changes; sensitive aggregated access only |
| P6 | Service — **تحليل أعمال الصيانة** | Work order responsiveness, repeat incidents, cost | Team, category, service period | Approved work orders, timesheets, materials | Review repeat repairs and allocation of teams |
| P6 | Expenses — **تحليل المصروفات** | Expense policy exceptions and category growth | Policy/category, period, branch | Approved expense facts and reimbursements | Review recurring exceptions without bypassing approvals |
| P6 | Fixed Assets — **تحليل الأصول الثابتة** | Asset service/carrying value trends and disposal cases | Asset class, site, period | Owned asset book, posted depreciation/disposals | Review underutilized assets; no automatic disposal |

### Lower-priority domain owners, not automatic analysis projects

`Directory`, `Catalog`, `FinBase`, `Tax`, `Pricing`, `Ledger`, `Invoicing` and other financial kernels may provide source facts/analytics contracts or already own operational reports. **Do not invent separate standalone analysis pages for each.** The requirement is “for each justified business use case, inside its relevant owning module,” not “an analysis menu for every technical module.”

`Quality`, `Budgeting/FP&A`, `Helpdesk`, `Subscriptions`, `Channels`, `Logistics` have design packs in `docs/modules`, but no corresponding backend implementation directory was seen at pinned SHA. Their prospective analyses remain **FUTURE** pending implementation and runtime verification; no phantom menu entries.

## Definition and collision policy

- Analysis of current sales ≠ sales orders count; define `booked`, `invoiced`, `posted`, `net-of-returns`, `tax-inclusive/exclusive` as distinct metrics. No silently mixed bases.
- Treasury liquidity projection ≠ approved FP&A Forecast; don't create conflicting official forecasts.
- MRP planning is a domain planning capability; Analysis may interpret completed MRP runs, never create an alternative MRP engine under a generic AI layer.
- Existing `Subledger` application suggestions, Banking reconciliation suggestions and CRM pipeline snapshots are owner-domain features. New analysis must **consume** them if needed, not replace their rules.
- Money by currency must not be summed across currencies without explicit FX source/effective date, and FX-sensitive conclusions show exposure.
- HR/Payroll records must be aggregate/masked by role. No broad cross-entity people analysis via executive access alone.

## Promotion scorecard (0–5 each, proof required)

`Problem severity`, `decision frequency`, `source truth maturity`, `expected commercial value`, `measurability`, `performance cost`, `privacy impact`, `reusability`. A use case with unverified source truth or excessive permission ambiguity is **blocked** regardless of score. Score is a prioritization aid, not an automatic release decision.

## Detailed definition for first candidate: Collections Hygiene

- **Question:** Which money is genuinely overdue, which collections have been received but not allocated, and where should responsible staff focus follow-up?
- **Required form:** legal entity, overdue `as_of`, optional activity from/to, currency, customer(s), bucket/delay threshold, include/exclude opening balances, metric basis documented.
- **Metrics:** outstanding AR (by currency); overdue by due date; allocated cash receipts; noncash settlements; unapplied customer receipt aging; writeoff/reversal count; payment-date vs apply-date delay; insufficient-confidence count.
- **Recommendation rules candidate:** `review unallocated receipt`; `review persistent overdue exposure`; `investigate data anomaly`; not `block customer`, `change credit limit` or `write off` automatically.
- **Evidence:** exact open-item IDs / source reference and summarized payment/application linkage, as-of and knowledge cutoff; owner-scope checked at view-time.
- **Data gate:** classify payment methods and proof of received cash, verify partials and historical as-of. If not possible, restrict output to descriptive truth and warnings.
- **No invented ML metric:** all v1 calculations deterministic and evaluated against controlled expected outcomes.

## Analysis template approval checklist

- [ ] Relevant owner identifies canonical business facts and negative cases (void, reversal, backdated, partial).
- [ ] Arabic label tested with real target user; no AI jargon.
- [ ] Types, period semantics, filter dependencies and default values reviewed.
- [ ] Clear recommendation eligibility and prohibition conditions.
- [ ] Data readiness check and practical performance budget.
- [ ] Permissions include visibility/read/run/decision, tenant/entity/branch/record scope.
- [ ] Owner module docs and regression tests remain compatible.


---



---

<!-- source: docs/03_ARABIC_UX_NAVIGATION_FILTERS.md -->

# 03 — Arabic-first UX, Navigation, Forms & Historical Results

**Governed by:** `FRONTEND_PLATFORM_AND_DESIGN_SYSTEM_CONTRACT.md` and `TODIR_NAVIGATION_ACCESS_UX_ARCHITECTURE_DECISION.md`. No new navigation framework. Every route is an independent business task and respects effective access.

## Names users actually recognize

| Context | Approved candidate page label | Natural actions |
|---|---|---|
| Sales | تحليل المبيعات | «بدء التحليل» / «التحليلات السابقة» / «عرض النتائج» |
| Subledger/Collections | تحليل تحصيلات العملاء | «تحديد العملاء» / «بدء التحليل» / «مراجعة التوصية» |
| Inventory | تحليل المخزون | «اختيار المخزن» / «المنتجات» / «عرض التحليل» |
| Purchasing | تحليل المشتريات | «اختيار الموردين» / «الفترة» |
| Banking | تحليل السيولة النقدية | «الحسابات» / «الفترة» / «النتائج السابقة» |
| Accounting | التحليل المالي | «الفترة المالية» / «المقارنة» |
| Manufacturing | تحليل الإنتاج | «أوامر الإنتاج» / «مراكز العمل» |

Avoid UI phrases `AI Intelligence`, `Smart Decisions Engine`, `Run New Analysis`, `Decision Intelligence Workspace`, literal odd translations. **Technical type IDs and route names may be English**; visible text must reflect actual business job. Do not introduce names for modules lacking an approved analysis use case.

## Routing model, e.g. Sales (PROPOSED, not existing)

- `/sales/analysis`: analyze + saved history as one dedicated task page, no work started on GET.
- `/sales/analysis/:runId`: read-only result/decisions detail, deep-linkable; authorization rechecked.
- Other domains: `/inventory/analysis`, `/subledger/collections-analysis` (subject to existing route registry), `/banking/liquidity-analysis`.
- Canonical route entries go in `frontend/app/router/destination-registry.ts` plus module's `routes.tsx`, using the same `PermissionGuard` and module navigation registration currently present. Preserve search, recents, favorites, back/forward, translated route names, mobile behavior.
- If the module's existing `Reports` or `Forecast` page covers a different operational job, **do not silently rename or replace it**. Show a distinct business route only if approved as unique job; avoid duplicate navigation entries and horizontal tab proliferation.

## Main analysis page — content contract

1. **Header:** «تحليل تحصيلات العملاء» and short business explanation, as well as last saved run timestamp if visible.
2. **Form:** selected analysis category, user-accessible legal entity, business period (from/to), and analysis-specific filters. No global filter that assumes every module uses a date range the same way.
3. **Before starting:** explain `خلال الفترة` vs `حتى تاريخ` vs `التاريخ المتوقع`; show available data cutoff, source warnings, estimated amount of work only if calibrated.
4. **Primary action:** «بدء التحليل» (single deliberate action, disabled if scope invalid or permission absent); the button submits one POST with idempotency protection, and reports queued status. No automatic trigger on date change, onMount, route prefetch, retry handler, socket reconnect, download or preview.
5. **Previous runs:** paginated and permission-scoped list with analysis type, scope, start/end, requested by, created time, status and completion time; searching/filtering history performs **reads only**.
6. **Results:** summary KPIs with definitions, evidence table, comparison if requested, charts only where they add meaning, quality/warnings, data as-of and run version, recommendations only when justified.
7. **Recommendation detail:** problem, business impact, suggested action, supporting records and assumptions, alternative option, review status, next step, review actor/time.

## Required filter vocabulary and exact meaning

| Input | Semantics / server checks |
|---|---|
| `من تاريخ / إلى تاريخ` | Business measurement period. Server validates order, max allowed window by type, date system and financial timezone; avoid using run date for business basis |
| `حتى تاريخ` | As-of snapshot cutoff for stock, aging, balances; may be separate from activity period |
| `الشركة` | Legal Entity under tenant; only permitted selections, no arbitrary client-supplied tenant_id |
| `الفرع` | Only when source facts have reliable branch scope; all branches only with explicit all-branch permission |
| `العملاء / الموردون` | Scoped searchable picker, paginated; no unauthorized IDs and no cross-entity visibility |
| `المخزن / المنتجات / الفئة` | UOM and warehouse visibility validated against owner capabilities |
| `العملة` | Display separate currencies or show explicit FX conversion policy and rate date |
| `مقارنة مع` | Defined matched period: previous same length or same calendar period previous year; no ambiguous defaults |
| `نوع العملية` | Explicit bases: posted invoices, completed payments, received goods etc.; never implicit mixing |
| `المبالغ تشمل ضريبة؟` | Only show where the metric's owner supports a meaningful legal definition |

**Each analysis definition owns a versioned filter JSON schema and validation behavior.** Query parameters may preserve draft form state but must never trigger execution.

## Screen states and copy

| State | Arabic presentation | Behavior |
|---|---|---|
| No previous run | «لم يتم إجراء تحليل لهذه الفترة بعد» | Invite explicit user action, not autoplay |
| Queued | «طلب التحليل قيد الانتظار» | Show run ID and ability to navigate away |
| Running | «جارٍ إعداد النتائج» | Status polling/SSE is **status read**, not compute trigger |
| Succeeded | «التحليل جاهز» | Load saved results only |
| Insufficient data | «البيانات المتاحة لا تكفي للوصول إلى نتيجة موثوقة» | List missing evidence; avoid misleading charts/recommendations |
| Partial | «النتائج جزئية» | Clearly show coverage and omission; never publish as full success |
| Failed | «تعذر إكمال التحليل» | Correlation ID and controlled retry via new explicit action; no sensitive exception text |
| Stale | «توجد بيانات أحدث من المستخدمة في هذا التحليل» | Keep past report intact; optional user-requested new run |
| Unauthorized | «ليس لديك صلاحية عرض هذه البيانات» | Fail closed and hide sensitive previous content |
| Read-only entitlement | «يمكنك الاطلاع على التحليلات المحفوظة فقط» | Hide run/review controls according to backend effective access |

## Historical results and recommendation UX

- Two date labels always: **فترة البيانات** + **تم التحليل في**; include source-known-as-of / currency where relevant.
- Never show invented certainty (e.g., `90% confidence`) unless calibrated and appropriately validated.
- Explain negative measures with business labels: «متأخر عن موعد السداد»، «دفعات لم تُوزَّع على الفواتير».
- Record links open the originating business module if currently authorized; otherwise show aggregate evidence without record leakage.
- A recommendation is a **proposal**, not the same thing as manager approval or a journal/PO creation.

## Frontend implementation constraints

- Follow repo's route registry and `public.ts` module boundaries; reusable Table/DatePicker/Combobox/Filters, cards and state components from approved design system; no parallel design system.
- RTL Arabic + English LTR, dates, EGP and currency precision, accessible keyboard workflows, responsive mobile and desktop, loading/error boundaries.
- Cursor-pagination for run/record/evidence lists and server-side filters; avoid fetching all rows and frontend-only pagination.
- Query cache keys include tenant, legal entity, branch, analysis type, run ID, permission revision; invalidate and clear on switching context.
- Verify narrow permission users can discover the analysis page even without unrelated Sales overview permission; no parent permission false dependency.
- Default: **No analysis cards or background fetches on operational screens**, including Sales Orders or Invoices. Future contextual display is separate product change and requires explicit approval.

## UX acceptance scenarios

1. Open Sales Orders 20×, open analysis 20×, change filters 20× → 0 analysis create requests and 0 analytics compute jobs.
2. Run action once, double-click and HTTP retry → one effective run per idempotency key.
3. Switch legal entity while a run status is loading → previous entity's content disappears immediately.
4. View old report after new data posted → original values unchanged, source freshness label updated separately.
5. User allowed `analysis.read` but not `analysis.run` can browse history only; opposite role's behavior defined by permission matrix.
6. Arabic labels reviewed by accounting/operations users; no technical jargon required to complete the workflow.


---



---

<!-- source: docs/04_TECHNICAL_ARCHITECTURE_AND_BOUNDARIES.md -->

# 04 — Technical Architecture, Ownership & Integration Boundaries

**Architecture recommendation:** Incremental .NET-first Analysis capability inside the current Modular Monolith, with **separately deployed compute host** for demanding work, not a second ERP or microservice network. **Status:** PROPOSED-ADR, except existing boundaries called out as verified.

## 1. Nonnegotiable invariants

1. Daily operation API call graph **must not call Analysis** (synchronous or wait-on-queue) and must not perform new analytics calculations or heavy projection writes in the same financial transaction.
2. An analysis run begins **only** through an explicit authenticated, authorized request containing validated filters. Neither events nor schedules may create analysis runs in v1.
3. No Analysis-owned SQL over `business_sales`, `finance_subledger`, `finance_payments`, `finance_banking` or other module schemas by default; owner-provided capability/read projection is the entry point. No cross-module SQL writes. `AI`/LLM has no raw SQL access whatsoever.
4. Every operation is tenant-scoped with Legal Entity/Branch/record scope, entitlement and current permissions; service credentials are not blanket bypasses.
5. Financial truth, posting/stock movement, order approval, employee/payroll and other domain state transitions stay with original owner modules.
6. Historical published results immutable; corrections require a new run/version, not rewriting source/evidence/results.
7. New compute workloads isolate process CPU/RAM and DB pool; baseline comparison and real tests required before go-live.
8. Existing architecture validators remain enabled. All module integration uses published contracts/capabilities/events.

## 2. Proposed component view

```text
React/TypeScript — existing shell/navigation/permission runtime
  /sales/analysis   /inventory/analysis   /subledger/collections-analysis
        | GET saved runs/results                    | POST user-initiated request only
        v                                            v
   .NET API (existing authentication, effective access, tenant and scope guards)
        |                                            |
        v                                            v
   Analysis read services                      AnalysisRequestService
        |                                      validate typed filters,
        |                                      permission+entitlement,
        |                                      limits and idempotency
        |                                            |
        v                                            v
   Analysis-owned schema                         Durable Job Store
   (runs, results,                                (new run only)
    recommendations,                                    |
    evidence, reviews)                                  |
                                                          v
                                              Separate .NET Analysis Worker HOST
                                              (bounded CPU/RAM/pool/parallelism)
                                                          |
                                               approved owner Read Capabilities
                                            / bounded owner-built projections
                                                          |
                                               deterministic calculations / rules
                                                          |
                                            validate + publish run results atomically
                                                          |
                                                          v
                                              Analysis-owned history & evidence

Existing Sales/Payments/Inventory/Accounting transaction paths remain unchanged.
```

This is a logical flow, **not a claim that such hosts and tables already exist**. A reader should not mistake independent runtime host for a microservice with an independent business database and identity stack.

## 3. Domain separation and source-of-truth map

| Concern | Canonical owner | Analysis may do | Analysis must not do |
|---|---|---|---|
| Sales orders, invoices, refunds | Sales / Invoicing | Consume approved scoped facts | Recompute official invoice totals or alter state |
| AR/AP item and allocation history | Subledger | Aggregate validated fact exports | Write `party_open_item`, alter settlement date |
| Money movement and payment status | Payments | Explain receipt/payment patterns | Declare bank cash received solely from `application_date` |
| Bank/cash evidence and forecast versions | Banking | Analyze confidence and trends | Create rival official treasury forecast |
| P&L, TB, accounting periods | Accounting/Ledger | Read reconciled period facts | Re-post accounting entries, independently calculate tax |
| Stock movement/availability/valuation | Inventory | Analyze approved snapshots | Reserve, transfer or mutate inventory without owner workflow |
| MRP and production | Manufacturing | Analyze completed actual/planning versions distinctly | Run a replacement MRP engine or treat planned orders as actual |
| Budget, Actual, Forecast, Scenario | Future FP&A owner | Explain/report approved inputs; propose scenarios | Publish Analysis result as approved FP&A Forecast |
| Analysis job, derived result, recommendation, review | New Analysis capability | Own these artifacts | Become source of canonical ERP transactions |

## 4. Source contract integration design

Existing module implementation projects must not import other implementations. For new demand, use an owner-owned capability such as **illustrative** `ICollectionsAnalysisFactsReadCapability` in `backend/Shared/Contracts`, implemented in Subledger; the consumer obtains it via existing DI/module registration (verify actual contract pattern first). For cross-domain snapshots, the Analysis orchestrator requests **independent owner facts** with common authorization scope, cutoff and version, then joins *records identified by approved opaque references*, **never SQL joins across owner tables**.

**Read contract shape:** `Scope {tenant, legal_entity, optional branch/record}`, `BusinessPeriod / AsOf / KnownAt`, `Criteria`, `PageLimit + Cursor`, `SchemaVersion`, `SourceWatermark`, `CompletenessWarnings`, `Facts[]`. No raw entity models or arbitrary SQL expressions. Owner validates inputs and tenant scope. A module may materialize its own read model to make this efficient; Analysis stores only bounded/protected derived data required by each run.

If reading entire data set requires 1000 paginated calls, create a purpose-designed **owner-scope, bounded export/projection** after benchmarks. Do not loop per party (`N+1`) or return unbounded lists.

## 5. Component packaging — proposed

```text
backend/
  Modules/
    Analysis/                # proposed L6 capability; manifest and contracts review required
      Controllers/           # only if existing module pattern requires public route controllers
      Services/
      Repositories/
      Rules/
      Models/
      module.manifest.json
    Subledger/              # optional owner-scoped analytic read adapter ONLY
    Payments/               # optional owner-scoped analytic read adapter ONLY
    ...existing modules...
  Shared/Contracts/
    AnalysisContracts.cs    # candidate; keep contracts small/versioned
    SubledgerAnalysisReadContracts.cs
  AnalysisWorker/           # proposed separate host; validate architecture tooling before creation
    Program.cs
  Tests/
    Analysis...             # unit, integration, security, performance and E2E
frontend/
  modules/sales/            # add one independently permitted route/page using existing patterns
  modules/subledger/       # collections analysis route when pilot approved
  components/              # reusable analysis form / history / evidence UI, if genuinely common
  app/router/              # destination registry; no new sidebar or routing layer
docs/
  ...this design pack...
```

**Do not create these files just because a diagram lists them**. Verify existing module conventions, dependency graph, naming, shared UI contracts, deployment and build gates. Check `Module` manifest `layer=L6` compatibility and whether Analysis should be purchasable as an additive entitlement; reuse current subscription system. The technical internal module name is a proposal and is not the label shown to end users.

## 6. Explicit alternatives considered

| Option | Benefit | Cost/Failure mode | Recommendation |
|---|---|---|---|
| Put analysis loops into existing Sales/Inventory transaction service | Fewest files | Latency coupling; no resource isolation; risk of violating no-regression requirement | **Reject** |
| Duplicate each module's calculations in an AI subsystem | Fast prototype | Conflicting financial truth and hidden divergence | **Reject** |
| Direct read-only SQL from Analysis across all module schemas | Easy queries | Breaks ownership, RLS ambiguity, schema upgrade coupling | **Reject** |
| One new Python microservice immediately | ML ecosystem | Runtime/deployment/privacy/observability cost before need | **Defer** |
| .NET module + jobs inside operational Worker process | Simple deployment | Same CPU/RAM/pool; noisy neighbor | **Reject for heavy jobs** |
| .NET module + separately hosted Analysis Worker + owner read contracts | Single codebase, compatible contracts, controllable resource isolation | Extra host/test/runbook, DB still shared | **Recommend** |
| Independent queue/broker dedicated day 1 | Supports high throughput | Added operational complexity; duplicate messaging semantics | **Defer until benchmark** |

## 7. Availability and graceful degradation

- If analysis host down, operational APIs remain available. Queued analysis runs stay queued or time out with clear state, no blocked invoice/payment/stock operations.
- If run store inaccessible, **only** dedicated analysis pages degrade; no mandatory startup dependency in Sales/Payments host beyond standard module registration.
- Analysis schema migrations must be backward compatible with existing platform; forward-only migrations, safe rollout order, no destructive data operations.
- If data export slow, use bounded execution, `statement_timeout`, budgeted pool, cancel and defer instead of starving transactional load.
- If data quality insufficient, publish `InsufficientData` result with evidence, or a failed validation run as specified—not imaginary forecasts.

## 8. Python-ready boundary (not implemented in v1)

Introduce a **versioned algorithm interface** with deterministic input facts and result schema independent of programming language. .NET v1 runs deterministic algorithms. When a pilot demands statistical training/optimization, compare .NET libraries and Python against baseline correctness, performance, cost and maintenance, using a versioned job payload and egress/secret/privacy controls. Python **never becomes a second ERP owner**.

## 9. Where architecture decisions must be recorded

ADR-A: Layer/module boundary and host. ADR-B: owner facts/read projection. ADR-C: queue vs DB claim, separate CPU/DB pools. ADR-D: historical temporal semantics. ADR-E: entitlement/navigation. See doc 14. Existing approved Master Blueprint wins until amendment is formally approved.


---



---

<!-- source: docs/05_DOMAIN_FACTS_AND_READ_CONTRACTS.md -->

# 05 — Domain Truth, Analytical Read Contracts, Temporal Semantics

**Status:** proposed capabilities + verified sources. **Key decision:** Do not calculate business truth twice. Source module owns facts; Analysis owns derived conclusions.

## 1. Owner fact contract principles

1. **Explicit business meaning:** each numeric field has basis (gross/net, posted/unposted, allocated/cash, inc/ex tax), unit, currency, rounding, owner.
2. **Explicit temporal meaning:** `period_from`/`period_to` measure activity; `business_as_of` measures economic validity; `known_at_utc` measures knowledge availability; `run_started_at` measures processing time. Different columns cannot be treated as interchangeable.
3. **Versioned extraction:** `source_schema_version`, `source_projection_watermark`, `source_fingerprint`, extraction time, completeness count/coverage, and warnings.
4. **Scoped export:** tenant, legal entity, branch/party if supported, row-level permission, authorized fields and dimensions, bounded cursor pages, stable consistent ordering.
5. **No mutable source resurrection:** old analysis references a reproducible immutable fact-set or recorded approved source version; it is not silently rerun from current DB and presented as history.
6. **No arbitrary SQL from filter values.** Queries use typed filters/parameters and legal whitelists for sort/group/filter expressions.

## 2. Financial definition catalog — mandatory before writing code

| Term | Must specify | Invalid shortcut |
|---|---|---|
| Posted sales | Source status, tax basis, credit notes and reversals, legal entity, currency | Sum all orders as revenue |
| Outstanding receivable | Debits - valid applications + reversals - write-offs, as-of and knowledge cutoff | Sum current `open_amount` for past dates |
| Overdue | `due_date < business_as_of` with owner rules and exclusion of negative/credit items | Assume overdue = outstanding without due-date rules |
| Cash received | Payment state/method/value or bank confirmation basis; cash vs credit vs adjustment | Count every application as bank cash |
| Collection delay | Explicit anchor (invoice due/doc/payment value/confirmed) and weighting by amount | `settlement_date - due_date` blindly |
| Payment allocation latency | Payment completion evidence vs application confirmation system-time; may be negative due to backdating | Compare user-entered application_date alone |
| Stock available | Owner availability rule incl. reservations, lot/serial, warehouse and UOM | Use on-hand quantity as always available |
| Production output | Confirmed output, scrap, cost, closure and timing | Treat MRP plan quantity as actual production |
| Cash forecast | Owner's published treasury version versus Analysis what-if projection clearly marked | Publish Analysis projection as approved Banking/FP&A forecast |

## 3. Where facts actually live at inspected SHA

- `SubledgerRepository.GetPartyBalanceAsync` has an as-of-aware branch using applications/reversals/writeoffs, but **existing query is scoped to a party**, not a certified portfolio-wide analytics export.
- `SubledgerRepository.GetAgingAsync` and `GetStatementAsync` exist; no proof of a ready collections bulk contract or all-tenant quality baseline.
- `Subledger` immutable application/reversal records and `Payments` completed payment state exist in code. See `subledger-schema.sql` and `payments-schema.sql`.
- `BankingRepository` owns separate cash-forecast versions. Any analytical liquidity result must identify itself as **analysis suggestion** and not modify Banking's official version.
- `Manufacturing` code folder and manifest exist at pinned SHA. Future cost-analysis needs actual test-backed implementation review, not assumption that every spec workflow works.
- FP&A is specified but its backend directory is absent in inspected module listing; avoid live APIs or reports attributed to it.

## 4. Contract composition example (PROPOSED)

**`CollectionsFactWindowRequest`**: `LegalEntityId`, `BusinessAsOf`, `KnownAtUtc?`, `ActivityFrom`, `ActivityTo`, `CustomerFilter`, `CurrencyCode?`, `Limit`, `Cursor`, `ConsistencyToken?`.

**`CollectionsFactWindowPage`**: `SourceOwner`, `SchemaVersion`, `TenantBoundScope`, `AsOf`, `KnownAt`, `Watermark`, `Completeness`, `NextCursor`, `OpenItemFacts`, `PaymentEvidenceReferences`, `ApplicationFacts`, `ReversalFacts`, `NonCashSettlementFacts`, `QualityWarnings`.

**Never put account credentials, full payment details, personal phone lists, raw bank statement text or provider tokens into generalized AI payloads.** Each analysis requires a minimal purpose-specific projection.

There may be one or more read contracts owned by Subledger, Payments and Banking; the exact shape depends on their current capabilities and database volume. Analysis **may** own a derived cross-domain read model if populated via owner-approved contracts/events, versioned and never authoritative.

## 5. Two temporal axes — illustrative corrected handling

For a fact with business date 2026-10-03 but `created_at=2026-11-06`:

- Report answering **"balance effective on Oct 10, as known today"** may include the backdated fact according to owner accounting rules.
- Backtest asking **"what could manager know on Oct 10?"** must exclude the fact recorded Nov 6, even though business date is Oct 3.
- An event may have **three** timestamps (payment completed, value date, imported bank statement) and later state changes; using `created_at` alone is insufficient for every fact. Determine `knowledge_time` and finalization evidence **per fact type**.

No source count in this document proves complete bitemporal history. **Gate:** controlled fixtures including backdated invoices, partial receipts, delayed allocations, reversals and corrections; compare known-at snapshots and actual financial reports.

## 6. Provenance: snapshot and fingerprint

An AnalysisRun requires: `definition_version`, `normalized_filter_hash`, `scope_hash`, `source_watermark(s)` per owner, `fact_contract_versions`, `known_at_utc`, `source_data_quality`, `engine_version`, `started_at_utc` and `completed_at_utc`. Store hashes/opaque owner references and an **appropriately minimal retained evidence snapshot** when replay needs to remain possible. Keep financial/PII details under role-aware retention policy.

Important: a `watermark` alone does not guarantee multi-owner atomic consistency; snapshot assembly must specify isolation/consistency method. Alternatives: owner-published immutable cutoffs, bounded read snapshot with validation/refresh policy, or explicitly partial cross-owner consistency with warning. Do not falsely call a cross-module extract a single transaction snapshot without proving it.

## 7. Read-model implementation and load strategy

- First prefer existing owner read models **if they include correct dates and scopes**.
- If needed, the owner publishes an optimized analytics export (per-entity, cursor, validated projections, stable sort and indexes) not a public unrestricted table.
- For wide portfolio analysis use **set-based SQL inside the owning module**, not a loop over each customer, and collect only required columns.
- Use streaming/chunked writes to a bounded fact store; define dedupe identities and update strategy for corrections.
- Read replicas are future capacity option: replica lag and as-of cutoff must be visible, and security/routing enforced. Do not blindly assume read replica guarantees immediate consistency.
- Owner projection and analysis run store have separate retention/update semantics: read projection rebuildable, **published run outputs immutable**.

## 8. Completeness and eligibility gates

Every type declares: required factual fields, source states considered final, share of missing `value_date`, unallocated receipts, unknown cash settlement, cancellation/reversal ratios, missing party lineage, source-history coverage, currency/branch accuracy, and reconciliation variance. The gate outcome is `Ready`, `PartialWithWarnings`, `NotReady`, with reasons and affected metrics. Thresholds are **proposed after observing real data**, not fabricated in this document.

## 9. Required contract tests

- Cross-tenant, legal entity, branch, record scope and scoped role access.
- Cursor consistency with inserts/new transactions; explicit snapshot/knownAt semantics.
- Partial cash vs noncash receipts and refunds correctly classified.
- Idempotent rebuild and no duplicate fact IDs.
- Reversals before/after cutoff and backdated knowledge-time replay.
- No integer overflow, floating money values or FX mixing; amounts use decimal and owner rounding rules.
- Performance bound to realistic row counts and no N+1 calls.

**Source:** `backend/Shared/Contracts/SubledgerContracts.cs` currently declares per-party balance, aging, statement and list contracts; any new bulk facts capability is **proposed**, not existing.


---



---

<!-- source: docs/06_JOBS_WORKERS_AND_ISOLATION.md -->

# 06 — Manual Jobs, Isolated Execution, Reliability and Fairness

**Decision:** User-created on-demand AnalysisRun only. Job workers may run continuously to **service accepted user jobs** but must never schedule/create analyses on their own. **Architecture:** PROPOSED-ADR.

## 1. Request vs process separation

```text
User clicks «بدء التحليل» → POST /v1/analyses (illustrative)
    → auth/permission/entity/entitlement validation
    → definition/filter/cost limits
    → transactional create run + job + idempotency marker
    → HTTP 202 Accepted {runId,status:Queued}
    → dedicated Analysis Worker claims durable job
    → owner-approved fact extraction in bounded chunks
    → deterministic analysis + (eligible) recommendations
    → validate evidence and data quality
    → atomically publish saved, immutable result + complete job
    → GET /v1/analyses/{id} returns saved result / job state only
```

**GET never queues, creates or recomputes.** SSE/poll for status is a read-only channel. No page mount, prefetch, user search, auth refresh, form change, scheduled cron, event handler, or fallback retry causes POST automatically.

## 2. Separate worker process

Operational `backend/Worker/Program.cs` currently hosts many background tasks and RabbitMQ consumers. Heavy analytical execution therefore belongs to a **separately deployed .NET host or independently selectable process role**, with explicit CPU/memory/network/DB-pool limits. Distinct worker pool must not share operational process resource budget. The job store initially uses PostgreSQL scoped claiming. Need not create RabbitMQ queue in first iteration; Quartz, already present, **may** schedule job polling/maintenance but is not automatically the entire job framework.

**Runtime isolation is not only separate .NET classes.** Separate host/container instances + cgroup limits, small connection pool, bounded `MaxDegreeOfParallelism`, throttled DB statement timeout, CPU limits, queue-age monitoring and deploy kill switch. If DB is shared, protection remains incomplete until mixed-load tests pass.

## 3. State and resilience contract

`Queued → Claimed/Running → Completed | Failed | Canceled`.

Additional attributes (not replace terminal state): `result_freshness=Current|Stale|Superseded`, `data_quality=Ready|Partial|Insufficient`. A completed job can have a stale result later **without rewriting its immutable output**. Failed jobs have sanitized operational metadata, not a fabricated report.

Claim requires `job_id`, `tenant_id`, `legal_entity_id`, `lease_owner`, `lease_until`, `attempt`, `deadline`, heartbeat and `claim_epoch` fencing token. Only current lease holder can publish. Crash leaves run queued/retryable after lease expiry; completed publication idempotent. Workers check cancellation and deadlines at chunk boundaries. An attempt may repeat computation; **at-least-once processing + effectively-once publication**, not impossible exactly-once execution.

## 4. Idempotency is distinct from computation reuse

- **Submission idempotency key:** bound to requesting principal + tenant/scope + body hash, with bounded key lifetime; identical retry returns same run, different body with same key → 409. Never create second job on network retry/double click.
- **Analytic fingerprint/cache key:** definition + version + normalized parameters + authorized tenant/entity/branch + source watermarks + model/config version + privacy scope. May suggest reuse of a previous completed output **only if contract/business semantics permit and user still authorized**.
- **Explicit new run:** clicking again may intentionally produce a new execution, even with the same visible filters, especially after source data changes. No automatic reuse that violates the user's expectation of a new dated result.

## 5. Claiming and tenant context

Worker must discover pending jobs across tenants without using `erp_app` as a broad RLS bypass. Evaluate a **narrow fixed-parameter security-definer dispatcher** (pattern already exists for platform outbox) or an approved privileged broker; it returns only minimal claim routing identity and is audited. Once tenant is known, all fact/result queries use **transaction-local** `app.tenant_id`, current authorized legal entity, and effective scope; no connection re-use leakage. No generic tenant ID supplied by untrusted payload overrides verified job ownership.

A persistent background job does **not** retain perpetual user privilege. Decide permission semantics at both `request time` and `data fetch/publish/action time`, and honor revoked access/cancellation in a defined way. At minimum, no stale revoked user can view results; high-risk processing should cancel or fail safe per approved policy.

## 6. Resource budgets and fairness

- Per-instance bounded parallel runs, statement and total execution deadlines, bounded memory and response size.
- Per-tenant and per-legal-entity concurrency quotas to avoid a large tenant starving others; weighted/fair scheduling or fair claim ordering when metrics justify it.
- No unbounded tenant-wide joins, memory materialization or export; cursor/keyset or batch extraction.
- Bulk data extraction may use short transactions/owner snapshots with explicit cutoff; avoid long-running transactions holding MVCC bloat and locks.
- Backpressure: if workload too high, return `Queued` with meaningful status; enforce hard queue/job limits and reject oversized inputs **before** creating expensive work.
- Read replica only when measured; if used, record replication lag and do not claim newer sources were analyzed.

## 7. Retry and error policy

| Case | Policy |
|---|---|
| Worker crash | Lease expires; retry from checkpoint with fenced publication |
| Temporary DB disconnect | Limited jittered retries, no busy-loop; separate operational metrics |
| Invalid filters/no authorization | Synchronous 400/403; no job creation |
| Missing source data | Publish explicit insufficient-data result or validation rejection, depending definition |
| Financial invariant mismatch | Fail-closed; preserve diagnostic trace and source IDs for operator, not user-visible raw SQL |
| Model bug / results fail sanity bounds | Fail job, do not show unvalidated recommendations |
| Duplicate request | Same run ID returned; no duplicate charge or compute |
| User cancels | Best-effort cooperative cancellation; no partial published output silently shown as complete |
| Source changed during extraction | Consistency check; retry bounded or publish controlled warning; never silently combine inconsistent cutoffs |
| Worker disabled | Existing runs readable, new creation disabled or queued according to configured policy; daily ERP unaffected |

## 8. Queue choice — exact decision gate

**Initially prefer Postgres job store + dedicated worker**, subject to measured contention. Existing RabbitMQ for operational integration events remains unchanged. Dedicated Analysis queue becomes justified if job wait-time, throughput, multi-language dispatch or operational isolation test fails. Do not abuse `OutboxPublishedMessage` fanout for heavy compute; domain events can still inform **future owner projections** without creating new analysis runs, under a separately approved change.

## 9. Load-test failure assertions

- Same bank/customer invoice/stock transaction API path works when Analysis service is off.
- When 2–N large jobs run, sales order confirmation, invoice posting, stock availability and payment settlement stay under **approved baseline budgets**, no growing DB lock queue or excessive outbox lag.
- Kill Analysis Worker during extract, compute and result commit → no missing/duplicate final result, no orphan `Running` job.
- Kill/disable feature + rerun → historical saved results remain available if access policy allows.
- Tenant A job cannot use source rows or persisted output of tenant B, including after pooled-connection reuse.
- Simulate 429 or quota exhaustion → clear domain response and zero unbounded backlog.

## 10. Deployment and rollback

- Deploy schema and contracts forward-compatibly; keep operational hosts unaltered until safe release.
- Feature flag/entitlement gate rollout of analysis routes and creation; disable new jobs without editing operational workflows.
- Rollback worker/app binaries without deleting historical runs. Forward-only remedial migration for schema problems, never destructive rollback on live financial datasets.
- Runbook covers drain, pause, resume, retry, dead-letter diagnostics, tenant job concurrency and stale evidence remediation.


---



---

<!-- source: docs/07_SCHEMA_HISTORY_AND_VERSIONING.md -->

# 07 — Persistence, Historical Integrity, Lifecycle & Data Retention

**Status:** conceptual schema and invariants. **No SQL migration authorized yet.** Exact schema names, table shape and indexes require ADR approval and DB replay tests. Owner: new Analysis bounded context, **not** other business modules' canonical tables.

## Why not just save one JSON report?

We need queryable history, idempotency, exact run filters, reproducibility, reliable recommendations/decision status, tenant-scoped auth, pagination, audit and analytics quality metrics. A single opaque JSON blob fails lifecycle/query/versioning; excessive normalization of every chart cell adds overhead. Use **typed relational headers + carefully versioned JSONB for bounded domain-specific result payloads**, with separate controlled high-volume evidence as needed.

## Candidate normalized logical entities

| Entity | Purpose / critical fields | Write owner | Immutability |
|---|---|---|---|
| `analysis_definition` | stable code, module owner, display translation key, definition/version, input JSON schema, allowed dimensions, algorithm reference, max scope, state | Analysis admin/controlled deployments | Versioned definitions; never reinterpret an existing run |
| `analysis_run` | tenant/entity/optional branch, definition/version, request actor, submitted at, period/as-of, normalized filter hash, idempotency reference, job status, started/finished, consistency/cost metadata | Analysis | Mutable **operational state only** until terminal |
| `analysis_run_filter` (optional) | validated normalized business filters and UI rendering values | Analysis | Snapshot sealed once queued |
| `analysis_job_attempt` | attempt, lease, checkpoint, worker ID, error category, heartbeat, limits | Analysis worker | Append attempts; mutable lease with fencing |
| `analysis_result` | run ID, output schema version, metrics summaries, source watermarks, data quality, engine build, result artifact pointer/hash, published_at | Analysis | **Append/sealed on publish**; changes require new version/run |
| `analysis_evidence_ref` | run/result/metric/recommendation, source module/type/opaque ID, selected validated fields, snapshot hash, evidence access class | Analysis | Sealed; access checked at read time |
| `analysis_recommendation` | run ID, rule/model version, business issue, action option, support, expected impact/risk/uncertainty, evidence, expiry, current status | Analysis | Immutable proposal fields; review states separate |
| `recommendation_review` | recommendation ID, actor, decision (ReviewLater/AcceptedAsProposal/Rejected), reason, timestamp, concurrency version | Analysis | Append-only reviews; latest review projection derived |
| `recommendation_action_link` | optional later handoff, owner module, operation/draft ID, idempotency, status and actor | Analysis + owner-owned command contract | Append/controlled state; no autonomous execution |
| `recommendation_outcome` | measurable subsequent actual, source owner, evaluation period, result version, known-at, observed change and caveats | Analysis | Append evaluation versions; never rewrite original proposal |
| `analysis_audit_event` | who, what, when, scope, prior/new state, correlation and security event | Approved platform audit infrastructure | Append-only |

**Do not create all tables automatically.** Split actual schema only where usage, size, query patterns, ownership and audit requirements demand it; no EAV/unbounded flexible cube.

## Critical invariants

1. Composite tenant and legal entity keys; RLS FORCE on all tenant-owned tables, nonowner app role, no `BYPASSRLS`, exact access scope.
2. DB uniqueness for request idempotency and one final published result per run/engine version; owner transaction publishes final result and terminal job state atomically with fencing.
3. No float financial amounts; exact `numeric`/decimal and currency code + conversion source/rate date. Units always explicit.
4. Full run filter snapshot sealed once request accepted. Client changing later form fields does not affect queued job.
5. UTC instants for system events; separate business `date`/local calendar and legal entity timezone. Do not infer UTC timestamp cutoff as the same as fiscal period.
6. Historical output is **immutable even if source ERP data later corrected**; mark freshness separately, with linked new run. Changes to evidence confidentiality must preserve legal deletion/redaction policy without silent financial rewriting.
7. All collection/history/result list APIs use bounded pagination, stable sorting and filters. Large row-level result sets must have server pagination or artifact cursor, not giant JSON in GET.
8. PII and bank/employee data encrypted and access-controlled as required; never store payment tokens, credential secrets or open-ended raw transcripts.

## Run lifecycle vs recommendation lifecycle

**Analysis run:** `Queued → Running → Completed` / `Failed` / `Canceled`; `DataQuality: Ready / Partial / Insufficient` separate; `Freshness: Current / Stale / Superseded` separate projection. No `Superseded` job state.

**Recommendation:** `Proposed → Reviewed [AcceptedAsProposal | Rejected | NeedsMoreInformation]`; optional `HandoffRequested → DraftCreated` belongs to an **explicit later integration**, not the v1 auto-command. The review lifecycle records who and why; approval of analysis suggestion is not legal authorization to execute a payment or credit-limit adjustment.

## Historical report identity

```
RunIdentity = (tenant, legalEntity, optional branch/access policy,
               definitionCode, definitionVersion, normalizedParameters,
               sourceCutoffsAndWatermarks, engineVersion, requestedBy, submittedAt)
```

The user-facing report shows `الفترة التي تم تحليلها`, `البيانات متاحة حتى`, `التحليل أُنشئ في`, `الإصدار`, `المراجع`, `التحذيرات`. These are not redundant.

## Publication procedure (logical transaction)

- Verify lease/fencing token, run scope, permissions/entitlement policy, immutable filters, source watermark and result schema.
- Validate numerical invariants (totals, currencies, expected ranges), evidence sufficiency and no direct unauthorized data references.
- Insert results and approved recommendations as **one consistent unit**; atomically mark run `Completed` and record audit. Failed validation results in `Failed` or controlled `Insufficient`, never pseudo-success.
- Retries cannot append second official result or duplicate recommendation, even if compute repeats.

## Proposed index/query design to validate with EXPLAIN

- History: `(tenant_id, legal_entity_id, domain_code, requested_at DESC, id DESC)` plus active schema's RLS policy.
- Requested-by filter: `(tenant_id, legal_entity_id, requested_by, requested_at DESC, id DESC)` only if observed query patterns justify it.
- Jobs: partial index on `status IN ('Queued','Running')` + `available_at`, `lease_until`, `priority` with safe claim order and fair tenant scheduling; define uniqueness on scope/idempotency.
- Recommendations: `(tenant_id, legal_entity_id, run_id, review_status, id)`; don't add high-cardinality unneeded indexes until measured.
- Partition/history archiving when retention/size metrics cross measured thresholds; **not default**.

## Retention and deletion policy (must be agreed before production)

- Operational job attempt logs have short retention (proposal to configure by tier); published run records and decision audit may require longer retention subject to legal advice and commercial tiers.
- Deleting a run is **not automatically permitted** simply because a user wants to free storage; retention, legal holds and evidence must be defined with product/legal oversight.
- Avoid unbounded append-only accumulation; document archive/export/delete/replay with scope and audit.
- Keep read-model/temporary input chunks separately with shorter retention than published historical report; encrypt sensitive intermediate data, delete according to policy.
- Tenant offboarding/deactivation must respect contracted export and deletion, current entitlement/read-only history and audit obligations; do not assume one policy fits Egypt and Saudi Arabia.

## Schema migration approach

- Reversible application toggles do **not** imply destructive DB rollback. Follow current DbMigrator forward-only SQL conventions and seed/migration harness.
- New Analysis tables/schemas and RLS deployed first with no operational change; integration code behind feature gates; worker last; UI last.
- Tests: clean DB replay, incremental existing DB upgrade, RLS with `erp_app` and dispatcher scopes, concurrency/idempotency, fresh/old run reads, backups/restores, audit/retention.

## Deferred design decisions

`Exact schema name`, `Object Storage provider`, `Retention defaults`, `Whether to persist per-result tables or JSONB`, `Encryption keys`, `Result anonymization policies`, `Legal requirements by Country Pack` — **TO-VERIFY/ADR**. Never invent compliance retention requirements.


---



---

<!-- source: docs/08_RECOMMENDATIONS_AND_DECISION_WORKFLOW.md -->

# 08 — Decision Recommendations, Explanations, Review & Outcome

**Critical product requirement:** recommendations are first-class **evidence-based proposals** with optional human review and measurable outcomes, not decorative AI text and never a second command path bypassing ERP owners.

## 1. Separate analytical objects carefully

| Object | Business question | Owner | Example |
|---|---|---|---|
| Fact | What did official records say? | Domain module | Invoice due 2026-10-15; receipt completed 2026-10-20 |
| Analysis result | What patterns/variance were calculated? | Analysis | Past-due AR by customer and currency, as-of cutoff |
| Risk/opportunity signal | Which metric is outside validated threshold? | Analysis | Customer overdue exposure increased beyond a defined baseline |
| Recommendation | What specific action could the manager consider, why and with what tradeoff? | Analysis | Review collection contact for customer X first |
| Manager review | Did authorized person agree, reject, defer, and why? | Analysis/audit | NeedsMoreInformation reason |
| Business action | What operational draft/command did module execute? | **Owner module** | Create collection follow-up task or Sales draft if contract supports it |
| Outcome | Did action lead to expected measurable change? | Owner actual facts + Analysis evaluation | Balance dropped or remained overdue; external causes noted |

## 2. Recommendations may be absent

- Do **not** manufacture an action for every completed analysis. If enough evidence is unavailable, show `لا توجد توصيات موثوقة حاليًا` with reason.
- Suggestion text should be generated by **typed deterministic rules** initially; no unstated causal inference or fabricated probability.
- Future models (if approved) must return structured score/uncertainty/version and independently validated inputs; LLM can explain verified metrics in natural Arabic, not author financial source truth or official approval.

## 3. Recommendation data contract — conceptual

```
recommendation_id, run_id, business_area, business_issue_code,
subject_refs[] (opaque, scoped), title_ar, concise_reason_ar,
priority_class (proposed from policy, never arbitrary AI urgency),
source_fact_refs[], computation_rule_id, rule_version, input_cutoff,
impact {metric, amount?, currency?, unit?, range?, assumptions[]},
risks[], alternatives[], eligibility_warnings[], expires_at?,
requested_action {owner_module, action_kind, supported?},
review_status, reviewers[], evaluated_outcome_refs[]
```

All quantitative fields have explicit units/currencies and formulas; absent impact estimate is honest `not_estimated`, not zero. Avoid generic words such as «ثورة ذكية» or «تحليل متقدم» in visible business text.

## 4. Example, Collections Hygiene — conditional rule, not real data

**Signal:** Selected legal entity has a completed customer receipt remaining unallocated beyond a configurable, justified lag; receipt's cash receipt date classification is known.

**Recommendation:** «مراجعة دفعة عميل لم تُوزَّع على الفواتير».

**Evidence:** PaymentId, CompletedAt/value date basis, amount/currency, open payment credit, application references, elapsed time, last data cutoff.

**Action option:** Navigate to Payments' existing allocation workflow if user has original module permission. Do **not** create allocation automatically; reason codes and checks remain in Payments/Subledger.

**Block conditions:** payment reversed, provider not finalized, canceled, not in current legal entity, credit note mistaken for cash, missing trust in receipt date, or source invalid at cutoff.

**Outcome:** whether later allocation occurred (accounting hygiene), without pretending it caused actual bank cash receipt.

## 5. Approval and action safety — fixed protocol

1. Analysis result can be reviewed by person permitted for specific **run + business scope + data sensitivity**.
2. Review `AcceptAsProposal` is a decision about recommendation quality, **not business execution**.
3. If action handoff ever enabled, request is a distinct authorized POST; owner module re-reads **current** source facts/version, checks original business command authorization/entitlement/approval/credit/period lock and idempotency.
4. Owner may return `Draft`, `ReviewRequired`, `NotApplicable`, `Conflict`, `Forbidden`. Analysis stores opaque action reference/audit; never assumes success.
5. Financial/tax/payroll/legal sensitive finalization cannot be autonomous. A recommendation must never directly open a journal, change available stock, approve bill, run payroll or publish official forecast.

## 6. Recommendation review permission boundaries

| Permission | Scope |
|---|---|
| View analysis results | May read saved outputs only for authorized tenant/entity/record scope |
| Create new analysis | May request a new, bounded run; NOT imply ability to review/execute |
| Read recommendations | May inspect recommendation and supporting evidence, separately gated if sensitive |
| Review recommendations | Can accept/reject/defer suggested next steps, with audit and concurrency version |
| Execute owner action | Always **separate existing owner module permission** and any mandated approvals |

Permission codes naming to follow existing module manifest conventions, proposed in doc 09; user with `sales.analysis.run` must not acquire `sales.order.confirm` automatically.

## 7. Measuring outcome without false causality

- Track recommendation exposure/acceptance, linked action actually performed, observation period and matched source facts. An observed improvement does not prove the recommendation caused it.
- For forecasting, compare **model version and prediction issued before actual result**; validate against simple baselines and separate model confidence vs data quality.
- If comparison is statistically invalid or lacks a counterfactual, label it descriptive `بعد الإجراء` rather than `بسبب الإجراء`.
- Wrong or outdated recommendation → link a correction/contradiction assessment; do not silently alter original text or numbers.

## 8. High-risk recommendations fail-closed

Classify according to impact: informational / operational review / financial sensitive / statutory or payroll sensitive. Policies must map to **existing ERP approver and audit controls**, never separate AI-specific final-approval rules. Possible policy: v1 only provides informational and operational review suggestions; financial-sensitive action handoff is disabled pending formal acceptance and legal review.

## 9. Recommendation-specific testing

- No recommendation when required evidence missing.
- Credit-note settlement never counted as positive cash collection.
- Same run cannot create duplicate recommendation after job retry.
- Reviewer cannot modify someone else's entity/branch recommendation.
- Revoked role cannot view historical sensitive evidence or approve old suggestion.
- Expired or stale recommendation cannot be handed off without a source recheck.
- Owner rejects invalid purchase/payment action; Analysis records rejection without committing any change.
- No autonomous execution path; static architecture/test search confirms.

## 10. UX wording examples

**Use:** «إجراء مقترح»، «سبب الاقتراح»، «البيانات المستخدمة»، «الأثر المتوقع»، «مخاطر أو ملاحظات»، «مراجعة»، «نتيجة المتابعة».  
**Avoid:** «الذكاء قرر»، «الموديل متأكد 99%» بدون إثبات، «اعتماد تلقائي»، «حل سحري»، أو أسماء تقنية كعناوين عمليات تجارية.


---



---

<!-- source: docs/09_ACCESS_SECURITY_AND_ENTITLEMENTS.md -->

# 09 — Authorization, Tenant Security, Entitlements and Privacy

**Governing source:** existing ToDir `docs/platform/authorization.md`, `entitlements.md`, `rls.md`, Master Blueprint and Navigation ADR. **Rule:** do not create a separate AI admin/purchasing/role/entitlement system.

## 1. Scope and permission model

Effective access = authenticated identity + selected tenant + permitted legal entities + permitted branches/records + current role permissions + module entitlement + activation/read-only mode + data classification. Server independently checks every API call, worker extraction, result/evidence fetch, review and action handoff. UI hide/show is not an authorization decision.

**Proposed permission vocabulary** (not existing in repo; exact codes subject to manifest design):

- `sales.analysis.read`, `sales.analysis.run`, `sales.analysis.recommendation.read`, `sales.analysis.recommendation.review`
- `subledger.collections_analysis.read`, `.run`, `.recommendation.read`, `.recommendation.review` or a consistent manifest-approved alternative
- `inventory.analysis.read`, `.run` ... as needed per justified use case
- Sensitive result/evidence policy may require existing financial `audit.read`/`reports.read` or explicit data permission (do not infer privilege from generic `analysis.read`).

**Distinct capabilities**, no implied permission hierarchy: read history ≠ create analysis ≠ review recommendation ≠ execute business action. A `run`-only role needs a documented policy for viewing **its own created run** or add explicit `readOwn`; do not silently grant all read or expose another manager's results. Parent navigation visibility derives from permitted child destinations, not unrelated module overview permission.

## 2. End-to-end checks

- Request `POST`: server resolves authenticated actor/tenant, current entity and branch from trusted context; rejects cross-scope IDs, unauthorized dimensions, overly broad branch lists, hidden datasets.
- Worker: `tenant_id` in persisted job belongs to system-authored request, cannot be overridden by payload or selected result. Worker applies transaction-local RLS per scope and delegated authorization, use minimal narrow dispatcher for cross-tenant job claiming.
- Result `GET`: **re-evaluate current permissions**; user may lose access after creating run. Evidence links are also scoped by current owner permissions and data sensitivity.
- Recommendation review: role + entity + record scope + optimistic concurrency, mandatory audit actor/correlation; review does not grant original module command permission.
- Tenant switch: clear stale caches, pending network response surfaces, filters and selected run; late previous-tenant responses must never rehydrate UI.
- Legal entity deactivation: respect existing read-only history and entitlement policy; block new runs if module not operational/entitled, unless explicitly approved.

## 3. RLS and DB roles

- `erp_app` remains nonowner, no BYPASSRLS. Every new tenant-owned table uses FORCE RLS with `tenant_id` and composite tenant-sensitive keys.
- Job dispatcher requiring cross-tenant queue search uses **narrow security-definer** wrapper with fixed claims only (to evaluate against existing outbox dispatcher pattern), never unbounded query granted to normal app.
- Runtime credentials for Analysis Worker distinct where useful, least privilege for read/write of **its own** schema; cross-owner source projection read via approved owner contracts only.
- Set `app.tenant_id` as `SET LOCAL` in transaction; verify pooled connection switching tenants under malicious and accidental contexts. Stronger legal-entity/branch enforcement is required on top of tenant RLS.
- Do not send tenant private financial or payroll facts to external AI providers without separate consent, entitlement, retention, legal and data processor assessment.

## 4. Entitlement options — proposed ADR, not a new system

- A module's analysis route is available only if its source module is accessible and the tenant's effective **analysis add-on entitlement**, if commercially adopted, is active.
- Whether analysis is sold as separate add-on, included in tier or limited by monthly quota is **commercial policy to decide**. Implementation must use the **existing entitlement/lifecycle** infrastructure; no independent subscription state.
- Quotas, job size and retention should be enforced server-side and auditable, not just UI. Denied requests must not enqueue work or reveal data.

## 5. Data minimization and sensitive fields

- Analysis results should preferentially store aggregated amounts + scoped opaque references, not replicate bank statements, full customer contacts, employees' salaries, ID numbers or payment processor tokens.
- Salary and employee-level analysis requires separate HR/Payroll role-specific policy; do not expose individual pay through owner-wide generic dashboard.
- Preserve finance/audit traceability with permission-scoped evidence; hide record links/PII for viewers lacking source record access.
- Encrypt stored data and artifact links where required, audit download/external export, signed short-lived links scoped to current access.
- `model input`, debug logs, tracing, error payloads and events must not leak secrets or financial row payloads.
- External providers, consent and data residency are out of initial deterministic offline-only scope until product/legal review.

## 6. Threat scenarios and acceptance tests

| Scenario | Expected outcome |
|---|---|
| Tenant A passes Tenant B run UUID in GET | 404/403 without information disclosure |
| Same user switches branch/entity mid-fetch | Old data cleared; late responses ignored; new permissions enforced |
| User has sales read, not analysis run | No POST execution; allowed operational work unaffected |
| User has analysis run, not sensitive financial report | Data extractor omits/denies sensitive facts; no privilege escalation |
| Role revoked after queued job | Defined cancel/fail-safe policy; cannot view former restricted output |
| Malformed JSON filters contain SQL-like tokens | Typed validation; no dynamic SQL execution |
| Guessing evidence ref ID across another legal entity | Owner capability enforces record scope |
| Worker connection reused across two tenants | `SET LOCAL` scope verified; no cross-tenant rows |
| Unauthorized LLM prompt asks to run command | No command capability/SQL access; only approved read/description if enabled later |
| Module entitlement expires with history present | New runs blocked; old run access per explicit existing read-only policy |

## 7. Security release gate

Required: automated backend 401/403/404 matrix per route+action+role, PostgreSQL RLS tests with real `erp_app` role and transaction pool, authorization change during run, mixed-tenant worker stress, artifact access controls, audit correlation, no leaked data in logs, and proven fail-closed frontend access. Security green in mocked tests is **insufficient**.


---



---

<!-- source: docs/10_PERFORMANCE_TESTS_AND_OBSERVABILITY.md -->

# 10 — Performance Engineering, Operational Reliability & Test Program

**First principle:** do not accept an architecture because it is asynchronous or has separate code. **Measure** speed, CPU/RAM, DB pressure, broker/backlog and error rates under representative mixed workloads. No benchmarks have been executed for this design pack; every number below is a **target/proposal only**, never actual performance.

## 1. Baseline before any Analysis code (P0 mandatory)

Use reproducible isolated staging database / disposable DB with representative test data; document hardware, versions, connection-pool size, query plans, number of tenants, concurrency, network and background traffic. Avoid production-destructive tests or arbitrary changes to existing local DB.

Capture per scenario: throughput, p50/p95/p99, response error rate, DB CPU and I/O, slow queries, lock wait, buffer cache behavior, connection pool utilization, read replica lag (if present), outbox pending count/oldest age, RabbitMQ queue lag, worker memory/CPU, trace correlation. Save raw output in repo reports and script path so reruns are comparable.

**Operational flows to baseline:** Sales quote/order create-confirm, invoice post/credit note, Payments completion+application, Banking reconciliation, Subledger aging, Inventory availability/reservation/movement, POS cash/card checkout, Manufacturing release/close if its acceptance baseline is verifiable, list pagination/authorization. Choose representative sizes and actual current paths, not artificial no-op endpoints.

## 2. Performance SLO design

**No made-up guarantees.** Gate 1 records actual current baseline. Gate 2 proposes accepted p95/p99 absolute SLOs *and* allowed relative regression budget per critical flow, with statistical uncertainty and consistency across multiple runs. Draft recommendation: no meaningful regression; provisional alert threshold **5% relative p95** in comparable conditions, **not approved** and not a substitute for absolute SLO or p99/error/lock metrics. Owner and engineering must approve thresholds with baseline before implementation.

KPIs beyond API latency:

| Indicator | Why it matters |
|---|---|
| Transaction success/error rates | AI processing must not break financial correctness |
| p95/p99 for critical writes | Detect hidden tail-latency regression |
| PostgreSQL lock wait, CPU, I/O, temp space | Separate worker may still compete on same database |
| Pool saturation / timeouts | Heavy analytics can exhaust connections |
| RabbitMQ/outbox age and throughput | Protect operational event delivery |
| Analysis wait time / run duration | Jobs can be slow; user page remains responsive, transparent status |
| Data watermark age | Fast result with stale/partial facts is not trustworthy |
| Worker memory/CPU and cancel latency | Protect noisy-neighbor fairness and operations |
| Results storage growth / read query cost | Historical report volume can degrade DB and backup windows |

## 3. Benchmark matrix

| Case | Setup | Pass condition (to formalize after baseline) |
|---|---|---|
| B-01 Operational only | No analysis feature; identical baseline hardware/data | Baseline recorded with repeatability bounds |
| B-02 Analysis idle | New module, worker deployed but no run | Core API behavior and resource overhead within accepted budget |
| B-03 Analysis small | One short user-triggered job | Correct output; no regression in operational p95/p99 |
| B-04 Analysis large | Large company/time range, bounded extraction | No unbounded RAM/DB load; operational paths healthy |
| B-05 Concurrent tenants | Multiple tenants jobs + active transactional load | Fair quotas, no leak, no starvation, no exceeded budgets |
| B-06 Worker down | Stop/kill host during jobs | Operational ERP unchanged; job recoverable without duplicates |
| B-07 DB stress | Long/slow owner projection read, locks from concurrent writes | Bounded timeout/cancel/backpressure; no runaway connections |
| B-08 Large history | Many historical results and recommendations | Keyset/history pagination, bounded memory, indexed query plan |
| B-09 Cache context change | Switch tenant/legal entity/branch/role | No stale cross-scope display, no unfiltered query |
| B-10 Broker disturbance | Delay RabbitMQ operational events while analyses execute | Operational outbox lag within agreed tolerance; no accidental fanout storm |
| B-11 Migration upgrade | Upgrade nonempty versioned DB | No long exclusive lock on critical source tables; rollback app compatible |
| B-12 Failed user POST | Auth/limits/unknown type | No job/data recorded; no expensive extraction triggered |

## 4. Code and DB validation gates

- Backend: `dotnet restore/build`, architecture validator, module-specific unit/integration tests, real Postgres RLS and migration replay; formatter and `git diff --check`.
- Frontend: `corepack pnpm typecheck`, `corepack pnpm architecture:frontend`, existing Vitest and Playwright, localization/RTL/effective-access tests, controlled browser network check asserting **zero analysis POST on GET navigation**.
- API: OpenAPI shape/versioned schemas, invalid filter matrix, pagination, HTTP idempotency, concurrent requests/cancel/retry tests.
- Persistence: PostgreSQL `EXPLAIN (ANALYZE, BUFFERS)` on disposable data with safe query plans, RLS and tenant-pooled reuse, migration clean+incremental.
- Worker: lease fencing, job cancellation, dead-letter/retry, result publish atomicity, idempotency and failure injection.
- Financial: amount and currency rules, as-of/known-at, current vs historical, partial payments/credit notes/writeoff/reversal; reconciling source totals with owner reports.
- Data analysis: deterministic fixtures and independent calculations for v1; future model temporal backtesting and leakage checks before calling anything predictive.

## 5. Observability and alerts

Use existing OpenTelemetry and Core metrics conventions; add **low-cardinality, tenant-safe** metrics:

- `analysis_run_requested_total` by type/status (not tenant ID label), `analysis_run_completed_total`, `analysis_run_duration_seconds`, `analysis_job_queue_age_seconds`, `analysis_job_retries_total`, `analysis_data_quality_block_total`, `analysis_worker_active_jobs`, `analysis_result_bytes`, `analysis_recommendation_review_total` (aggregate only).
- Per-run trace includes correlation ID, sanitized run ID, definition version, source owners, waiting/extract/compute/publish stages; do **not** log raw customer/employee/finance payloads.
- Alert on stuck jobs/leases, queue age exceeding configured objective, DB pool saturation, excessive query time, p99 operational regression, outbox lag, cross-tenant policy violation and excessive storage growth.
- Admin kill switch **pauses creation and/or execution of Analysis**, preserving operating ERP, existing saved read views and audit. Clear policy whether already running jobs stop safely or finish within limits.

## 6. Release test evidence record

Each release report requires: exact commit SHA, env config (sanitized), DB dataset and size, command lines, timestamps, benchmark artifacts, pass/fail table, regressions against baseline, SQL query plans for top reads, security matrix, retry/crash verification and product acceptance. A test marked PASS without reproducible command/artifact or source scope is not sufficient.

## 7. Guard against overengineering

Do not provision DWH, cache cluster, Kafka, vector DB, GPU, Kubernetes node group, Python model-serving API or separate RabbitMQ vhost by default. They are allowed **only if one measured gate cannot be satisfied** within current architecture, and ADR records evidence, price, failure and rollback.


---



---

<!-- source: docs/11_API_AND_CONTRACT_EXAMPLES.md -->

# 11 — Proposed HTTP API & Cross-Module Contract Examples

**Important:** These are **illustrative designs**, **not existing ToDir endpoints**. Final routing prefix, URL semantics, module-scoped permission codes, DTOs and OpenAPI schema must conform to current API conventions and be approved in ADR. Public-facing API resources should be named for their real task, not `IntelligenceAndAnalytics` or `AIAnything`.

## API rule summary

- GET is read-only: never computes, enqueues, refreshes fact projections or modifies historical outputs.
- Only `POST` on explicit start action can create a run. No cron/event/action-on-navigation auto creation.
- Analysis filters are typed and whitelisted, not arbitrary SQL or user-supplied programming expressions.
- Creation requires `Idempotency-Key`, authenticated actor, current authorized scope/definition, entitlement and quotas.
- Results returned by source owner record/currency/access scope, not uncontrolled raw database records.
- Lists are cursor-paginated; payload sizes and search/sort bounded.
- `202 Accepted` is **not** final analysis success. All status and result reads are explicit.

## Proposed resource contract (subject to current `/v1` prefix)

| Method & route (example) | Action | Permission |
|---|---|---|
| `GET /v1/analysis-definitions?module=sales` | Allowed analysis types, input form schema, UX labels | relevant analysis read or run, effective access |
| `POST /v1/analyses` | Request run, typed input and `Idempotency-Key` | relevant `.analysis.run` + data/source scope |
| `GET /v1/analyses?module=sales&cursor=...` | Paginated saved runs | relevant `.analysis.read`; results scoped |
| `GET /v1/analyses/{runId}` | Status + saved metadata; no auto compute | scoped read/own-read policy |
| `GET /v1/analyses/{runId}/results?cursor=...` | Published output/paginated detailed rows | scoped read + evidence classification |
| `GET /v1/analyses/{runId}/recommendations?cursor=...` | Saved recommendations only | relevant recommendation read |
| `POST /v1/analyses/{runId}/cancel` | Explicit cancel (not rerun) | run owner or manager with permission |
| `POST /v1/recommendations/{id}/reviews` | Human accepts as proposal/rejects/defers | relevant `.recommendation.review` |
| `GET /v1/recommendations/{id}/reviews` | Paginated review history | scoped read |
| `POST /v1/recommendations/{id}/action-request` | **FUTURE/disabled by default:** owner-workflow draft handoff | reviewer + owner module action permission |

Route placement: a single service may handle analysis runs across modules, but `analysisDefinition.module` is explicit. Frontend URLs remain business-task routes like `/sales/analysis`. If existing API framework favors module-owned controllers for scoped permissions, expose a thin domain path wrapper **only when needed**, never duplicate compute engines.

## Example: Create Collections Hygiene run

```http
POST /v1/analyses
Authorization: Bearer <scoped-session-token>
Idempotency-Key: 7dcf6c32-4d61-4c12-8e40-26753a6cbd55
Content-Type: application/json
```

```json
{
  "definitionCode": "collections.hygiene",
  "definitionVersion": 1,
  "legalEntityId": "00000000-0000-0000-0000-000000000101",
  "scope": {"branchIds": [], "customerIds": []},
  "filters": {
    "asOfDate": "2026-10-31",
    "activityFromDate": "2026-10-01",
    "activityToDate": "2026-10-31",
    "currencyCode": "EGP",
    "includeOpeningBalances": true
  }
}
```

**All UUIDs above are placeholders, not real company IDs.** An empty `branchIds` / `customerIds` must have an explicitly defined meaning; **do not assume “all”** unless effective access and analysis definition authorize it. `tenantId` is **not accepted** from the user payload.

Example only:

```http
HTTP/1.1 202 Accepted
Location: /v1/analyses/00000000-0000-0000-0000-000000000201
```

```json
{
  "runId": "00000000-0000-0000-0000-000000000201",
  "status": "Queued",
  "createdAtUtc": "2026-11-22T09:30:00Z",
  "resultAvailable": false,
  "statusUrl": "/v1/analyses/00000000-0000-0000-0000-000000000201"
}
```

## Example: Completed run detail (sample fields, not fictitious business data)

```json
{
  "runId": "00000000-0000-0000-0000-000000000201",
  "definitionCode": "collections.hygiene",
  "definitionVersion": 1,
  "status": "Completed",
  "businessAsOf": "2026-10-31",
  "activityPeriod": {"from": "2026-10-01", "to": "2026-10-31"},
  "sourceKnownAtUtc": "2026-11-22T09:30:10Z",
  "publishedAtUtc": "2026-11-22T09:31:00Z",
  "resultVersion": 1,
  "quality": {"state": "Partial", "warnings": ["SOME_RECEIPTS_HAVE_UNKNOWN_BANK_CONFIRMATION"]},
  "resultsUrl": "/v1/analyses/00000000-0000-0000-0000-000000000201/results",
  "recommendationsUrl": "/v1/analyses/00000000-0000-0000-0000-000000000201/recommendations"
}
```

No invented figures. Actual amount metrics are retrieved in source-scoped result resources and must each specify currency, aggregation rule, evidence provenance, and coverage.

## Typed filter definitions

A versioned registry controls allowed definitions and filter schema:

```text
AnalysisDefinition {
  code: "collections.hygiene"
  moduleOwner: "subledger"
  filterSchemaVersion: 1
  businessLabelAr: "تحليل تحصيلات العملاء"
  scope: LegalEntity [plus optional allowed branch/customer]
  required: asOfDate
  optional: activityFromDate, activityToDate, currencyCode,
            customerIds, includeOpeningBalances
  rules: date horizon and customer-set size bounded by approved policies
  outputSchemaVersion: 1
  engineVersion: "collections-hygiene-v1"
  recommendationPolicyVersion: 1
}
```

The text above is a proposed contract catalog, **not a new database-driven arbitrary formula interpreter**. Version definitions via code or approved metadata, not tenant-uploaded scripts.

## Error contract (illustrative)

| HTTP | Domain issue | Behavior |
|---|---|---|
| 400 | Invalid date/unsupported filter/unknown type | No job created; machine error code and Arabic field message |
| 401/403 | Not logged in or unauthorized for action/scope | No job; no disclosive details |
| 404 | Run inaccessible or not found | Same concealment convention; no existence leak |
| 409 | Same idempotency key different payload / stale review version | No duplicate/new action; explicit conflict |
| 422 | Data readiness unsupported or contradictory period/metric | Controlled reason; no fabricated result |
| 429 | Quota/concurrency exceeded | Reason and retry policy; no queue storm |
| 503 | Analysis creation temporarily disabled | Operational ERP still available |

Exact use of 422 vs terminal `InsufficientData` must be chosen per definition and documented; don't mix inconsistent states.

## Cross-module internal capability example (conceptual C#, NOT paste-ready)

```csharp
public interface ICollectionsAnalysisFactsReadCapability
{
    Task<AnalysisFactPage<CollectionsFact>> ReadPageAsync(
        VerifiedScope scope, CollectionsFactQuery query,
        AnalysisSourceCutoff cutoff, string? cursor,
        int limit, CancellationToken cancellationToken);
}
```

- Define types in approved contracts project and map to owner-specific DTOs; avoid referencing `Modules.Subledger.Repositories` or internal entities from Analysis.
- `VerifiedScope` cannot be constructed from untrusted tenant string; backend obtains authorized actor context.
- Owner uses RLS, validates authorized source fields and returns `NextCursor`, `Watermark`, `KnownAt`, `Completeness`.
- No C# code emitted by an AI agent should assume this interface already exists.

## Functional contract tests

1. All GET endpoints are nonmutating and issue zero AnalysisWorker jobs, even repeated.
2. POST double-click or lost response is idempotent; mismatched key/payload conflict.
3. Same run ID with unauthorized tenant/entity returns concealment status; no side-channel data.
4. Large result tables bounded; pagination stable when new runs created.
5. Historical run `definitionVersion=1` is interpreted with its own v1 contract after v2 introduced.
6. Unknown dimension or invalid date basis is rejected rather than silently ignored.


---



---

<!-- source: docs/12_COLLECTIONS_PILOT_DATA_TRUTH.md -->

# 12 — Collections Pilot: Source-of-Truth Research, Rules & Acceptance

**Status:** leading PILOT CANDIDATE (not committed commercial priority). **Pilot 0** = deterministic AR/collection hygiene. **Pilot 1** = statistical forecast only if evidence and commercial value justify it. This plan is grounded in repo's `Subledger`, `Payments`, `Banking`, `Invoicing`, not in hypothetical already-trained AI.

## 1. Primary decision questions

1. At selected business as-of, which customers have overdue **collectible** balances, by currency and legal entity?
2. What amount has been actually **received** versus allocated vs credited/written off, without confusing accounting settlement with cash?
3. Which completed customer payments remain unallocated or partly allocated, and for how long (with reliable timestamps)?
4. Which source data are inconsistent (backdating, incomplete bank confirmation, missing relationship, reversal, unexpected date ordering)?
5. Which operational follow-up, if any, is justified based on transparent evidence—not a made-up probability?

## 2. Source discovery targets

| Repo source (pinned SHA) | Existing fields / behavior observed | Do not assume |
|---|---|---|
| `backend/DbMigrator/Database/bootstrap/subledger-schema.sql` | `party_open_item` with `document_date`, `posting_date`, `due_date`, original/settled/open; `application_batch` with `application_date`, `settlement_date`, `created_at`, `confirmed_at`; applications/reversals | `application_date` equals real cash date |
| `backend/Modules/Subledger/Repositories/SubledgerRepository.cs` | `GetPartyBalanceAsync`, `GetAgingAsync`, `GetStatementAsync`, as-of SQL, partial/reversal/write-off logic | Portfolio-wide historical export already exists, or true bi-temporal replay is proven |
| `backend/DbMigrator/Database/bootstrap/payments-schema.sql` | `payment_date`, `posting_date`, nullable `value_date`, `completed_at`, `reconciliation_state` | `value_date` universally populated or bank-confirmed |
| `backend/Modules/Payments/Services/PaymentsService.cs` | `ApplyToPaymentAsync` maps authorized target invoice IDs and user-provided application/settlement dates | allocation happens at same instant as receipt |
| `backend/Modules/Banking/Repositories/BankingRepository.cs` | reconciliation sessions, bank claims, forecast versions | Bank statement confirmed date mapped to payment value date in all paths |
| `docs/modules/10_...SUBLEDGER...md`, `12_...PAYMENTS...md`, `25_...BANKING...md` | domain specs | All semantics implemented/tested or live-quality |

## 3. Event timeline example — WHY the dates cannot be collapsed

**Illustrative only:** invoice due Oct 10; bank transfer appears Oct 21; payment completed Oct 22; user allocates receipt against invoice Nov 2 with `application_date=Oct 20`; analyst runs report Nov 3.

- **Due-date delay:** bank value/receipt date vs invoice due date (11 days if validated bank date Oct21); distinguish amount-weighted partials.
- **Allocation latency:** system completion-to-allocation confirmation (Oct22 to Nov2 = 11 days), **not customer paying delay**.
- **As-of (economic):** query valid-time can count backdated application at Oct20.
- **Known-at (historical forecast):** on Oct25 application wasn't known until Nov2; temporal backtest MUST NOT see it.
- `settlement_date` may be used for FX accounting context and must **not** be uncritically used as bank receipt date.

## 4. Cash and noncash taxonomy — separate before building metrics

| Event type | Cash receipt? | Effect on AR and scoring |
|---|---|---|
| Completed customer cash receipt with valid proof | Possibly yes, per payment method; needs policy | Reduce cash collection remaining when allocated; count as receipt with evidence/date confidence |
| Bank transfer imported with reconciliation evidence | Potentially yes after reconciliation lifecycle verified | Stronger evidence class than manually entered payment |
| Provider/card settlement pending | Not automatically bank cash | Separate provider/settlement states |
| Customer advance before invoice | Cash may exist; not invoice-specific yet | Track unapplied credit, avoid inventing invoice payment date |
| Credit note | **No cash** | AR reduction; exclude from cash performance numerator |
| Write-off / allowance | **No cash** | AR adjustment, separate negative collection outcome |
| Application reversal | Reverses prior allocation | Correct historical effective and known-at views, no double count |
| Refund | Negative movement / potential contra receipt | Not positive collection; owner lifecycle dictates sign |
| Opening balance imported | No pre-go-live receipt proof | Allow descriptive balance but label behavioral history insufficient |
| Backdated transaction | Accounting effect may precede entry | Preserve both effective and known times, no future info leakage |

**Taxonomy is an APPROVAL GATE**: source owners/accounting SME validate classification. One generic `payment_date` fallback should not silently stand in for proven bank cash.

## 5. Deterministic Pilot 0 outputs and formulas to certify

### 5.1 Outstanding and overdue by party

Use owner Subledger as-of calculation with credit/debit polarity and valid applications, reversals and write-offs. For each currency separately:

`outstanding(asOf,knownAt) = recognized original receivable - effective applied amounts + effective reversal amounts - approved write-offs`.

This is **conceptual**: validate owner polarity and write-off semantics before implementing. Never sum currencies without explicit conversion policy. Aging buckets respect due date and current partial balance; predefined buckets documented with boundary tests (due today vs overdue tomorrow).

### 5.2 Cash collection by customer and invoice

Sum eligible completed receipt allocations linked to payment source, explicitly excluding credit notes/writeoffs, with amount-weighting for partials. Track `UnverifiedReceiptDate` separately. **An unallocated payment is collected at customer level but cannot be attributed to a specific invoice without evidence.** Do not force allocation to a guessed invoice.

### 5.3 Allocation lag

`confirmation_time_of_application − payment_completion_time` where both trustworthy; negative or missing timestamps become warnings/data-quality cases. Not the same as client delay.

### 5.4 Recommendation rules (initial candidates)

- `COLLECTION_REVIEW_OVERDUE`: persistent overdue exposure beyond configurable, explainable policy; recommend human follow-up with due invoices.
- `COLLECTION_REVIEW_UNALLOCATED`: completed receipt still unallocated at known-at; review allocation workflow.
- `COLLECTION_REVIEW_DATE_ANOMALY`: date order inconsistent; request accountant validation, not credit restriction.
- No model-predicted collection date, automatic blocking or payment promise certainty in Pilot 0.

## 6. Data readiness audit — done BEFORE approving pilot

- Use **sanitized/approved actual client data** if available, else controlled fixtures and label readiness unknown for real deployment.
- Percentage of completed customer receipts with meaningful/verified value/booking dates, per payment method.
- Percentage with reconciliation evidence; classify unmatched vs reconciled and how status updates happen.
- Payment→open-item→application linkage completeness and count of unallocated credits.
- Date gaps: payment posting vs completion vs value vs allocation confirmation; histograms, missing/negative delays.
- Opening-balance share, imported historical coverage, number of closed invoices per customer, currency/branch consistency.
- Write-off/reversal/credit note rates; whether a source amount can move or be corrected after captured cutoff.
- Backdated transactions and exact known-at reconstruction; compare historical computed totals to period-end documented reports.
- Runtime performance of portfolio extraction on representative large entity under ongoing operational load.

**Data thresholds:** Do not invent. Report distribution/coverage and present explicit acceptance proposal to finance/product owner. Static schema existence is **not** evidence of customer data quality.

## 7. Correctness test fixtures — minimum

1. Invoice with due date, paid in full on time, allocated same day.
2. Partial receipts before/after due date, remaining balance and weighted delay.
3. Completed advance without invoice allocation: cash vs invoice collection separated.
4. Credit note offsets invoice: no cash counted.
5. Write-off and its approval: no cash counted.
6. Application reversed after cutoff; economic and known-at views differ.
7. Payment reversed/refunded and allocation rules obey owner lifecycle.
8. Backdated invoice entered later, source not known at earlier backtest cutoff.
9. Foreign currency and base currency with owner FX context; no cross-currency sum.
10. Opening balances without historical pre-go-live cash dates.
11. Completed payment with null value date, unmatched bank statement: marked unverified, not silently assumed cash verified.
12. Multiple legal entities/branches + narrow role cannot see other scope.
13. Customer receipt split across 2+ invoices, same receipt not counted twice in total.
14. Posted vs draft invoice/credit-note states; no false AR exposure.
15. Filter changes do not compute until deliberate user POST; two same POST retries create one run.
16. Worker restart during calculation restores job; results and recommendations exactly one published set.

## 8. Gate to statistical Pilot 1 (future)

- Temporal train/validation/test splits with no future-information leakage; compare against baseline simple policy (due dates / recent customer history); precision/recall/calibration or proper score as appropriate to outcome. Customer sample size and behavior variability verified.
- Define prediction **target** and truth timestamp unambiguously (e.g., probability receipt confirmed within 30 days, not `application_date` coincidence). Validate segmented by customer size, amount, season and method.
- Establish prediction robustness, false positive/negative business cost, model drift, explainability and fallback to deterministic signals.
- If Python materially outperforms a maintainable .NET approach given real metrics, propose ADR and isolated compute integration; otherwise remain .NET only.

## 9. Pilot completion criteria

- Data semantics signed off by finance/product owners.
- Independent case fixtures and reconciliations pass for all exceptions.
- Clear human-recognizable Arabic UX, typed filters and historical versioned reports.
- Useful recommendation rules with explicit source evidence and review states; no false causal claim.
- No material operational performance regression under mixed workload, plus security/RLS/permission gates.
- Customer review confirms decision value; failing pilot triggers alternative comparison, not inflated claims.


---



---

<!-- source: docs/13_DELIVERY_ROADMAP_AND_ACCEPTANCE.md -->

# 13 — Implementation Roadmap, Dependencies, Deliverables & Acceptance

**Process:** Technical design before implementation, measurable gates per phase, `dev` branch only, no blanket "complete" from agent report. **No fixed calendar estimates**: dates depend on baseline, actual code complexity and reproducible tests. Work is staged, but an implementation stage owns its slice end-to-end.

## Phase P0 — Repository Discovery and Operational Baseline (NO CODE CHANGE)

**Inputs:** latest `origin/dev`, current architecture/docs, owned services and DB, current frontend navigation/effective access, actual CI/local verification patterns.

**Tasks:**
1. Read root/backend/frontend `AGENTS.md`, current master architecture/navigation/permission/messaging/platform documents, owners' module packs.
2. Pin SHA and diff against this design pack's SHA; map existing APIs/business functions/DTOs/RLS/triggers/indexes/events/worker/host boundaries.
3. Generate source ownership map and definition of acceptable read capabilities for collections, sales and inventory reference use cases.
4. Run operational **baseline** on controlled realistic data; record p50/p95/p99, DB, outbox and relevant paths. If environment missing, record **NOT MEASURED** and setup steps; do not fabricate results.
5. Verify user roles and available effective access, tenant/legal entity/branch handling and enrollment/entitlement activation rules.
6. Read Claude static report and validate its date/messaging claims against the **current** code (not older `d07dd58`).
7. Produce gap/impact matrix including any likely existing critical performance blockers; do not implement unrelated fixes.

**Deliverables:** `CURRENT_STATE_EVIDENCE.md`, `BASELINE_METRICS.md` + scripts/artifacts, `SOURCE_OWNERSHIP_MATRIX.md`, changed-source register, risk gate and architecture questions. **Exit:** critical flows benchmarked or explicitly BLOCKED; no undocumented source truth assumptions.

## Phase P1 — Product & Architecture Approval (NO IMPLEMENTATION)

**Tasks:**
1. Validate natural Arabic page labels and two personas for pilot; finalize typed filter definitions & date semantics.
2. Sign off architecture ADRs in doc 14; decide module scope, worker isolation, DB job claiming, history storage, quota, permissions, subscription semantics.
3. Identify exactly which existing owner capabilities can be reused and what **new read-only public contracts** are necessary; use dependency graph/architecture fixture to prove feasibility.
4. Prepare SQL migration design (not execute), OpenAPI DTOs, frontend route registry addition, state machines, results and recommendation policy.
5. Set measurable SLO/performance budgets from P0 and runbook; choose security, data-readiness and failure gates.
6. Review product value evidence for Collections vs Sales/Inventory; select pilot or document that decision is BLOCKED pending data.

**Deliverables:** approved ADRs, signed spec per analysis, API/schema contract, detailed acceptance tests, test dataset plan, handoff plan. **Exit:** no unresolved P0 blocker or false completion claim.

## Phase P2 — Shared .NET Analysis Foundation (END-TO-END SLICE)

**Tasks:**
1. Add new bounded capability/manifest according to approved architecture, not unrestricted platform kernel expansion.
2. Create Analysis-owned schema, FORCE RLS, job lease/retry/fencing, idempotency, results/history/evidence/review base and audit integration.
3. Implement effective-access guarded APIs; **GET read only**; `POST` creates user-requested run; strict filter schemas and scope.
4. Create separately deployed .NET analysis worker (or independently configured process role proven equivalent); limits, kill switch, telemetry, cancellation/recovery.
5. Add minimal shared frontend components and domain route *for pilot only*, preserving current navigation and styling.
6. Include deterministic fixture engine **without reading cross-module tables directly** and test one typed read capability.

**Acceptance:** approved contracts/build/architecture/RLS/migrations/HTTP/UI, no unexpected run creation, failure recovery, no operational regression beyond approved SLO, one full flow from click → queued → saved result → history and recommendation review.

## Phase P3 — Collections Pilot 0 (CONDITIONAL)

**Prerequisite:** Collections Data Truth/Readiness audit approved. If not, select alternative with its own owner-fact contract.

**Tasks:** owner Subledger/Payments/Banking analytical read capabilities/projection, temporal and cash classification rules, deterministic metrics and recommendations, Arabic UI and typed filters, historical snapshots/provenance/quality, human review and outcome tracking.

**Acceptance:** reconcile actuals and reversals with owner module reports, all pilot fixtures, real-stack tests, multi-tenant RLS, data quality warnings, worker noise test, user verification. No ML claims.

## Phase P4 — Hardening and Release Candidate

- Replay clean and incremental DB upgrades; backup/restore; performance mixed-load/stress; rights/entitlement transitions; cancellation/retry/dead worker; huge history pagination; corrupted/stale input detection.
- Validate Arabic & English, RTL/LTR, accessibility, no hidden unauthorized destination, no operational API drift.
- Rollout canary and kill switch, monitor critical ERP SLO and outbox, keep deploy rollback and remediation runbook.
- Independent acceptance sign-off: product, finance domain owner, security, database, frontend, performance. Human UX approval required where visuals involved.

## Phase P5 — Catalog expansion by validated value

Promote one approved domain at a time using **the same** shared job/results/recommendation engine; no copied AI services. Candidates Sales/Inventory first, then Purchasing/Banking/Accounting and later Manufacturing/HR/Projects where validated. Every domain has separate source-fact accuracy/performance gate and UX review. No all-modules big-bang release.

## Phase P6 — Predictive/Optimization maturity (future proof, not speculative sprint)

Add statistical prediction or mathematical optimization only when specific decision improvement has measurable target, historical data/time leakage checks, out-of-sample gain over deterministic baseline, economic benefit, explainability and operational cost justification. Choose Python only by comparative evidence/ADR; preserve same analysis contract and job/result schema.

## Task card standard — every coding-agent assignment

| Field | Mandatory content |
|---|---|
| Task ID/title | Unique ID and use case, never vague "build AI" |
| Current code SHA | exact `dev` baseline, generated source diffs |
| Owner module | business owner; published capabilities required |
| Input/output contract | typed API, filters, scope, page/cursor, metrics/evidence and states |
| Forbidden changes | no operational path slowdown; no direct cross-schema reads/writes; no hidden auto-trigger |
| Files allowed | task-owned code/tests/docs only, existing conventions |
| Migration impact | forward-only, clean + upgrade replay, no destructive SQL |
| Acceptance tests | exact commands and real-stack evidence; performance, security, UX |
| Delivery | docs updated; commit SHA; push only `origin/dev` per repo governance |
| Completion decision | product spec + code + automated evidence + signed acceptance, not agent narration |

## Workstream dependencies

`P0 baseline → P1 ADRs+product → P2 foundation → P3 validated pilot → P4 release gate → P5 domain-by-domain expansion`. Future P6 can start a scoped research spike after P3 data truth, not a production Python deployment.

## STOP conditions

If a task touches customer financial truth without owner-defined semantics; adds AI to write transaction path; causes cross-tenant evidence leak; uses unknown scope; fails production DB migration replay; has unexplained performance regression; auto-starts analysis on GET; or invents a result during source failure — **STOP RELEASE**, fix cause and rerun affected acceptance. Do not bypass gates to close a task.


---



---

<!-- source: docs/14_ADRS_OPEN_QUESTIONS_AND_RISK_REGISTER.md -->

# 14 — Architecture Decision Records, Open Questions & Risk Register

**Status vocabulary:** `USER-LOCKED` (explicit product rule); `PROPOSED-ADR` (recommended pending code/metrics approval); `TO-VERIFY`; `FUTURE`.

## Existing binding platform decisions

- Modular Monolith, single writer/source of truth, no direct business module implementation imports or cross-schema writes.
- L6 scale/intelligence, L5 platform reporting and AI Gateway, versioned capabilities, contract-only cross-module integration.
- Tenant nonbypass isolation and current entitlement/activation/access model; navigation permission-driven.
- Source owner controls financial truth and statutory actuals; AI cannot directly access/modify DB.

## Decision register (ready to turn into ADR files after P0)

### ADR-AN-001 — Location of new capability

**Decision proposed:** analysis orchestration/results as bounded L6 module; owner facts via contracts; separately deployable .NET worker in one repo. **Why:** process isolation and ownership; avoid duplicating each module's AI. **Reject:** per-module engines, embedded compute in write paths, independent ERP microservice. **Verify:** graph/manifest/L6 conventions, DI and deployment hosting; no module cycles.

### ADR-AN-002 — User-triggered execution policy

**Decision:** `USER-LOCKED` user `POST` only; no scheduled, event-triggered, on-page-open analyses in v1. **Reason:** user's explicit control, predictable load, historical run semantics. **Nuance:** maintenance scheduler/polling processes already submitted jobs; this is not permission to create new business runs.

### ADR-AN-003 — Read contracts and projection ownership

**Decision proposed:** Subledger owns AR authoritative facts, Payments owns payment states, Banking owns reconciliation; Analysis owns cross-owner **derived** report/evidence snapshots only. **Why:** truth accuracy, no duplicate source ownership. **Verify:** owner APIs/capabilities, contract shape, efficient pagination, actual data quality and read-model performance.

### ADR-AN-004 — Job transport and runtime isolation

**Decision proposed:** PostgreSQL durable job+lease store with **separate bounded .NET worker process**, no new RabbitMQ analytic consumer or Python service at day one. **Reason:** existing infrastructure, no fanout/extra broker burden, testable isolation. **Fallback if performance fails:** separate job queue/DB replica; implement only after benchmark. **Verify:** RLS safe cross-tenant dispatcher, pool sizing, lease fencing, fairness, operational p95/p99 under load.

### ADR-AN-005 — Persistence and immutability

**Decision proposed:** typed run/status tables + versioned result/evidence + append review/outcome, strictly immutable published output. **Why:** auditability and reproducibility. **Verify:** retention, data size, encryption, storage layout, migrations; distinguish immutable result from separately mutable freshness label.

### ADR-AN-006 — Canonical financial ownership

**Decision proposed:** Analysis never owns official Banking forecast, FP&A budgets or Accounting posting. Output labeled scenario/analysis/recommendation; owner workflow required for business action. **Why:** legal/financial audit and spec conflicts. **Verify:** current Banking and FP&A future contract semantics.

### ADR-AN-007 — Permissions and commercial entitlement

**Decision proposed:** separate run/read/recommendation-review rights in module manifest, existing access bootstrap, existing subscription infrastructure. **TO-VERIFY:** whether analysis add-on sold separately or included; retention and quotas. **Never:** new hidden AI subscription per module.

### ADR-AN-008 — Python or ML admission

**Decision:** do not deploy Python or ML without validated business advantage, backtests, privacy/performance budget and ADR. **Reason:** long-term maintainability, evidence-first. **Preserve:** language-agnostic versioned algorithm request/response shape.

### ADR-AN-009 — Pilot domain

**Decision proposed:** Collections Hygiene candidate, provided real data/source truth/market fit. **Alternative:** Inventory or Sales descriptive pilot if Collection Data Readiness fails. **Not locked:** until data and buyer evidence.

### ADR-AN-010 — UI routes and terminology

**Decision:** analysis pages are independent within the relevant module, user-language labels, explicit filters and create button, history read-only. **No** auto-analysis triggers or injected cards in daily operational pages; any future contextual surfacing requires separate product change approval.

## Open questions blocking coding

| ID | Question | Proposed owner | Required evidence |
|---|---|---|---|
| Q-01 | What precisely constitutes cash received per payment method and bank reconciliation state? | Payments + Banking + finance SME | code review, workflow cases, data samples |
| Q-02 | Can source owner build portfolio-wide historical facts without long locks or N+1? | Subledger + DBA | SQL EXPLAIN, actual dataset size, contract |
| Q-03 | What is valid-time + knowledge-time meaning for each source event? | Finance owner + data architect | temporal fixtures and history comparisons |
| Q-04 | What are measured acceptable operational p95/p99 SLO and regression tolerance? | Performance + product owner | P0 baseline and load profile |
| Q-05 | How are entitlements/quota/retention priced and activated? | Product owner | current packaging and commercial research |
| Q-06 | Which user can read results created by another manager, same branch/entity? | Security + product | role/record permission matrix |
| Q-07 | Does user need global executive summary in v1? | Product | use case validation; none added by default |
| Q-08 | Which filters and exact period basis are mandatory for pilot? | Business owner | data definitions and Arabic UX testing |
| Q-09 | How to keep historical source/evidence accessible vs privacy deletion? | Security + legal | retention/compliance decision per country |
| Q-10 | Can dedicated worker be hosted independent of operational Worker with current deploy pipeline? | DevOps + backend | non-prod deploy test, resource limits |
| Q-11 | Which controls allow new run when analysis paused? | Operations + product | kill switch operational runbook |
| Q-12 | Which domain produces action follow-up and approves it? | Owner module | existing workflow and authority checks |

## Prioritized risk register

| ID | Risk | Severity | Mitigation / proof gate |
|---|---|---|---|
| R-01 | Incorrect cash vs credit/writeoff interpretation | **Critical** | Per-method taxonomy approved; accounting reconciliation and temporal fixtures |
| R-02 | Analysis query impacts live writes | **Critical** | Separate worker + DB pool + owner projection + mixed-load regression; pause on regression |
| R-03 | Tenant/entity/branch data leak through saved result or worker | **Critical** | RLS FORCE, scoped contract, worker dispatcher security and current access checks |
| R-04 | Backdated facts leak future knowledge into historical evaluation | High | As-of/known-at rules, controlled replay, independent historic report verification |
| R-05 | Duplicate or partially published result after retries | High | Job lease fencing, idempotency, atomic publish, crash injection |
| R-06 | Shadow official Forecast/Actual in Analysis | High | Owner-canonical mapping, labels and explicit FP&A/Banking ADR |
| R-07 | Reports mislabeled as AI or nonactionable recommendations | High | Real business questions, data quality gate, evidence/impact and pilot customer feedback |
| R-08 | Exploding result/evidence storage and DB I/O | High | Size/retention budget, bounded artifacts, indexed pagination, monitored growth |
| R-09 | User loses access but historical result remains visible | High | Revalidate current effective access, cache invalidation, evidence read guard |
| R-10 | Analyst claims zero runtime overhead without measurement | High | P0 baseline, B01–B12 benchmark matrix, code+env evidence |
| R-11 | Agent builds all modules at once based on catalog | High | Catalog candidates vs committed stages, DoR and per-module ownership gates |
| R-12 | Python/infra premature addition | Medium | measured trigger/ADR and comparable benchmarks only |
| R-13 | Overengineering of generic analysis schema/hypercube | Medium | typed definitions and JSONB only for bounded results, pilot-first contract testing |
| R-14 | Inaccurate Arabic names/filter semantics | Medium | SME UX review, clear business date definitions |
| R-15 | Legacy code drift from pinned SHA | High | Always diff latest origin/dev, re-evaluate conflicts and re-run affected tests |

## Decision authority

Product owner decides business value and workflow changes, with design and engineering challenge. Finance owners decide official calculation semantics, security defines access, performance engineering certifies SLO baselines, architecture owner approves ADR/module graph; no agent can silently override these responsibilities. Unresolved **Critical** risks block implementation of affected slice, not discovery or documentation.


---



---

<!-- source: docs/15_TRACEABILITY_TEST_MATRIX.md -->

# 15 — Requirements Traceability & Exact Acceptance Matrix

**Goal:** Every statement in specs has an executable acceptance path. No task accepted solely for completing code compilation or screenshots. Test case names illustrative; agents implement matching framework conventions.

| Requirement | Proposed evidence | Must pass before |
|---|---|---|
| PR-001 Dedicated analysis module route | Navigation registry + Playwright Arabic role route test | UX pilot acceptance |
| PR-002 No auto-runs on page open | Browser instrumentation 20× navigation/refresh/filter change; backend job table count unchanged | Foundation/UI acceptance |
| PR-003 Manual create + idempotency | Concurrent duplicate HTTP POST + retries; one run/one result | Foundation acceptance |
| PR-004 Filters and date basis | Valid/invalid range/property tests and server-side SQL whitelist | Every analysis type |
| PR-005 Effective access | Roles, tenant/entity/branch records, read-vs-run-vs-review negative paths | Any deployment |
| PR-006 Persistent job recovery | Host crash/lease expire/restart with exact final publication | Worker acceptance |
| PR-007 Historical integrity | Run before/after corrected source; original report checksum unchanged, freshness separate | Any historical release |
| PR-008 Explainable recommendation | Rule/evidence fixture; missing source → no unsupported recommendation | Decision pilot |
| PR-009 No autonomous business writes | Unit/integration test + architectural code review; no business state changed by analysis | Any deployment |
| PR-010 Bounded history | Cursor pagination and SQL EXPLAIN large data; stable sorted scope | History endpoints |
| PR-011 No material operational slowdown | Reproducible B01–B12 mixed load p95/p99, outbox lag, locks and resource limits | Production release |
| PR-012 Insufficient source data | Data quality fixtures, partial/insufficient display | Every analysis type |
| PR-013 Shared frontend patterns | `architecture:frontend`, localization, RTL/English, accessibility, auth route tests | UI acceptance |
| PR-014 Interpretability | Reviewer sees reason/source/caveat/alternative | Recommendation approval |
| PR-015 Export retention | Scope/PII, signed link and export auditing | When export enabled |
| PR-016 Executive view future | Explicit scoped business use case and ADR | Future phase only |

## Code-level acceptance by layer

**Owner module:** read contract correct, per-entity/record scope, run cursor, per-fact temporal fields, adequate indexes, no modification in daily transaction hot path, tests for negative business states.

**Analysis core:** schema FORCE RLS, defined manifest/dependency, correct job transitions/idempotency, immutable published snapshots, validated arithmetic and recommendation rules, audit/access.

**Worker:** separate process and DB resources, safe claim/heartbeat/lease/fence, checkpoint retries, bounded cancellation, monitoring and kill switch.

**API:** authenticated scoped GET and POST with identical error conventions, no auto-run GET, OpenAPI versioned DTOs, active definition registry and limits, pagination.

**Frontend:** natural Arabic page title; user filters; explicit submit; past-run history; recommendation detail; no analysis in existing operational page; role discoverability and session invalidation.

**Quality:** multi-tenant matrix, temporal financial fixtures, data quality gates, load tests, canary and restore, no source regression.

## Realistic acceptance data set matrix

| Fixture | Small | Medium | Large / concurrency |
|---|---|---|---|
| Tenants | 2+ isolation | several mixed module entitlements | noisy tenant and constrained role combinations |
| Legal entities | 2 with different permissions | multi-branch | large entity with concurrent payment/sales posting |
| AR/payments | full/partial/mixed/noncash/reversed | diverse currencies, delayed allocations | historical records sufficient to expose N+1 and index mistakes |
| Results | one run | different version/filter/history | many runs and large evidence lists |
| Jobs | normal complete | duplicate/cancel/retry | worker crash, many concurrent jobs, max allowed queue |
| UX | RTL/LTR | narrow role, stale caches | slow network, context switch, 403/503 and failed worker |

Dataset volumes **must be chosen after P0** to reflect target Egyptian customer ranges; do not invent production capacities or assert simulator results prove market relevance.

## Mandatory report fields delivered per stage

1. Exact branch/commit/source diff.
2. Links to current owner docs/ADRs and task spec IDs.
3. Commands executed and reproducible environment/seed data.
4. Counts and status of tests, failures and investigation; no hidden skips.
5. Measured API/DB/worker resource baseline and changed result where applicable.
6. Security evidence and negative cases, data correctness/reconciliation evidence.
7. Known open risks/limitations classified Critical/High/Medium.
8. Manifest/permission/navigation/entitlement updates and localization parity.
9. Relevant docs updated; review needed for any deferred ADR.
10. Post-merge `origin/dev` SHA and working tree status (agent branch policy; no feature branch unless user changes direction).

## Before declaring a module done

One affected module can be delivered as an implementation slice **only after** its full backend/database/API/frontend integration and associated Acceptance tests are complete. Horizontal skeletons (schema without API, API without secure frontend, UI without real facts) are **not** product complete. No destructive DB migration or production cutover without explicit governance.


---



---

<!-- source: docs/16_IMPLEMENTATION_AGENT_HANDOFF.md -->

# 16 — Engineering Agent Handoff and Governance

**IMPORTANT:** This is a handoff policy and **stage-zero discovery prompt**, not authorization to begin coding the whole platform. Implementation must wait until P0 evidence and P1 ADR/product gates are accepted. The repo is `dev` only according to product-owner branch governance.

## Agent operating contract

- Begin every assigned prompt: **Act as a senior ...** technical role appropriate to stage.
- Read root and scoped `AGENTS.md` files, existing Master Blueprint/Navigation/UI/Module packs and this design pack before touching code.
- Check current `origin/dev` and pin commit; this pack was prepared at `8a06af00dd82ed8e6ee593c992993c25786200e1` and may already be stale.
- Do not substitute speculative services/modules for owner code. No generic table sweeps, ORM cross-module imports, direct source schema reads from Analysis, new entitlements/permissions engine or automatic analysis on GET.
- Prove operational performance/financial correctness and tenant/record isolation before marking any implementation slice complete.
- Avoid repeat user interruptions for minor blockers; if an architectural conflict requires sign-off, document exact evidence, options, impact and recommendation.
- Use `dev` only, follow repo's approved safe git workflow, push via configured SSH and report exact SHA when a code task is explicitly authorized. This package is docs-only; no repo mutations in this preparation task.
- Never invent tests, actual business data, benchmark numbers or approvals.

## Next permitted task: P0-only discovery prompt (English)

> Act as a Principal ERP Software Architect, Senior .NET Engineer, PostgreSQL Data Architect, Security Reviewer, Performance Engineer, and Senior Product-Technical Analyst. You own a **read-only design discovery and operational baseline** for ToDir ERP Business Analysis & Decision Recommendations.
>
> Work against the repository's latest `origin/dev`, preserving the user's single-branch `dev` policy. Read the root and scoped AGENTS.md, current approved Master Blueprint, navigation/effective-access contract, module owner technical packs, existing .NET Contracts, Workers/Messaging, PostgreSQL schema and migrations, frontend route registry, and the provided `TODIR_ENTERPRISE_ANALYSIS_DESIGN_PACK` in order. The documentation is a *design proposal*, not pre-approved code direction.
>
> First, pin and report the exact Git SHA and list all relevant changes since the design pack's pinned SHA (`8a06af00dd82ed8e6ee593c992993c25786200e1`). Verify and classify each critical statement as implemented, documented-only, inferred, unverified, or outdated.
>
> Perform a detailed read-only inventory of existing owner read contracts and source-of-truth semantics for Subledger, Payments, Banking, Sales, Inventory and Manufacturing. Map direct dependencies and forbidden boundaries, current .NET worker deployment shape, current navigation/access/entitlement pattern, existing result/history/audit conventions, and actual database indexes/tenant RLS. Deeply investigate cash-receipt vs application dates, corrections, partial payment, reversals, writeoffs, historical as-of and knowledge-time. **Do not assume collections is the selected commercial pilot.**
>
> Establish a reproducible performance baseline on an isolated non-destructive environment for critical Sales, Invoicing, Payments, Subledger, Inventory, POS and relevant Manufacturing flows, including p50/p95/p99, throughput, PostgreSQL CPU/locks/query plans/pool, RabbitMQ outbox lag, worker resource usage, dataset size, and exact commands. If prerequisites are unavailable, mark benchmark `NOT MEASURED`; do not invent data, bypass controls or modify important DBs.
>
> Deliver only: (1) VERIFIED_CURRENT_STATE.md with file/line evidence and exact SHA, (2) MODULE_SOURCE_FACT_AND_READ_CONTRACT_MATRIX.md, (3) DATA_TRUTH_AND_TEMPORAL_RISK_REPORT.md, (4) OPERATIONAL_PERFORMANCE_BASELINE.md plus real scripts/artifacts, (5) ARCHITECTURE_ADR_REVIEW.md identifying approved/proposed/conflicting choices, (6) precise P1 approvals and test prerequisites. Include failure modes, alternatives and your independent recommendation, not agreement with the supplied proposal.
>
> User product requirements are fixed: dedicated naturally named analysis pages **inside relevant modules**; explicit authorized button to request computation only after typed filters and period selection; **no automatic analysis** from opening/refreshing pages, events or schedules; saved immutable historical runs; evidence-based decision recommendations reviewed by humans; no direct domain mutation by AI; no observable unacceptable regression to everyday ERP operations; retain current effective-access/entitlement/UI conventions.
>
> **Do not implement Analysis backend, API, worker, migrations, new pages, Python service, or alter existing operational functions in this P0 task.** Do not rename the source modules or introduce arbitrary technology. Prefer the shortest trustworthy path to evidence. Document every blocked measurement, and stop for formal ADR approval before implementation.

## Implementation-stage task contract (use ONLY after P1 acceptance)

> Act as a Principal .NET Modular Monolith Engineer and Data/Performance Architect. Implement exactly the approved stage [P2/P3...] on latest `dev`, respecting Master Blueprint and approved ADRs. Deliver backend, database, API, frontend (when task owns a user-facing slice), permission/entitlement/navigation, tests and docs end-to-end. Do not reimplement business facts, add auto-runs, move AI into core transaction paths, or use cross-schema SQL. Establish before/after performance metrics. Run architecture/security/real-Postgres/real-stack/UI and failure recovery tests as scoped; report commands and results honestly. Use safe forward-only migrations and protect seeded DBs. Finalize task-owned docs, push the accepted scope to `origin/dev`, and report commit SHA and residual risks. The current stage is not complete until explicit acceptance gates pass.

## Code review checklist for every PR/commit

- [ ] Does any existing operation controller/service call Analysis directly or await Analysis work? If yes, reject except explicitly approved nonblocking and measured capability.
- [ ] Does any frontend operational page trigger analysis POST or load hidden analysis data? If yes, reject.
- [ ] Any module-to-module implementation import or direct SQL read/write across schemas? If yes, reject.
- [ ] Has source fact semantics been agreed by owner and historical corrections handled?
- [ ] Are source filters/permissions/tenant/legal entity and branch scope enforced by backend and SQL RLS?
- [ ] Does GET change state, create work or cause recomputation? If yes, reject.
- [ ] Are run status/result/decisions versioned, history immutable, and records paginated?
- [ ] Is worker failure/retry/cancel idempotent and operational ERP independent?
- [ ] Are p95/p99 and DB/broker resource regressions compared to real baseline?
- [ ] Are UX names natural Egyptian Arabic and localization parity preserved?
- [ ] Did the agent update affected module docs, current performance evidence and ADR links without invented PASS claims?

## Decision hygiene

Every stage's report must provide **Recommendation / Rationale / Alternatives Rejected / Evidence / Unknowns / Acceptance Criteria**. Never use «معاك حق» or consensus as architectural proof. A new idea changes approved design only with a reasoned change request that identifies affected requirements, data migrations, tests and rollback.


---



---

<!-- source: docs/17_PER_ANALYSIS_SPECIFICATION_TEMPLATE.md -->

# 17 — Mandatory Specification Template Before Implementing Any Analysis

**Purpose:** prevent every domain agent from inventing filters, formulas, output labels, recommendation rules or SQL independently. The master catalog is **not** sufficient to start implementing a new analysis: complete and approve one instance of this template per committed type.

```yaml
spec_id: "AN-[MODULE]-[NUMBER]"
status: "Draft|BusinessApproved|ArchitectureApproved|ReadyForImplementation|Accepted"
version: 1
owner_module: "e.g. subledger"
analysis_code: "stable code, e.g. collections.hygiene"
label_ar: "Arabic business wording"
label_en: "Natural English UI wording"
intended_user_roles: []
primary_decision_question: "specific managerial question"
measurable_business_outcome: "how value will be tested"
supported_legal_entities: "per effective access"
supported_branch_scope: "supported/unsupported + why"
source_owner_contract_versions: []
output_schema_version: 1
engine_version: "release/version"
```

## A. Business scope and definitions

- Problem statement in Egyptian business practice (not general AI marketing).
- Buyer/decision-maker and who can act on a recommendation; data sensitivity.
- Business purpose, what the result **does not claim**, and explicit difference from existing reports/forecasts/approval workflows.
- Success/failure examples and situations where **no recommendation** is correct.

## B. Filters and period semantics

| Field | Type | Required? | Default? | Legal choices and dependencies | Permission scope / error |
|---|---|---|---|---|---|
| Legal Entity | UUID resolved from trusted session | yes | current context only if allowed | permitted list | 403 if cross-scope |
| `fromDate` | date | type-dependent | none, unless approved | in valid fiscal/calendar range | 400 if invalid |
| `toDate` | date | type-dependent | none | >= from; max span | 400 if invalid |
| `asOfDate` | date | when snapshot analysis | none | distinct from activity period | 400 if invalid |
| Branch/record | scoped ID list | type-dependent | NOT silently all | bounded, authorized | 403/422 |
| Currency | ISO code | metric dependent | no silent mixing | explicitly supported source | 422 if unsupported |
| Comparison | enum | optional | none | precise basis defined | 400 if invalid |

Special: date boundaries inclusive/exclusive? fiscal timezone? source valid-time and known-at? Backdated transactions? identity of filters when user switches company? Sort/pagination? Maximum result size? Spell out all; **never guess**.

## C. Data source and ownership matrix

For each metric and field, document: `canonical owner module`, `existing method/contract + exact path`, `additional owner read capability required?`, `source version`, `status/transaction finality condition`, `currency/UOM policy`, `historical correction behavior`, `security scope`, `max cardinality`, `index/read-model plan`, `data readiness tests`. Any field not verified remains `BLOCKED`.

## D. Metric calculation truth table

For every metric:

```text
metric_code / Arabic label / description
population (which rows qualify), excluded states (draft/void/reversed)
period definition / as-of / known-at
formula with exact decimal rounding and currency/unit handling
separate denominators and missing/zero handling
independent reference calculation / source reconciliation
edge-case fixtures with expected values
completeness and sensitivity warnings
```

The formula must be *owner-approved*; no duplicated Accounting/Inventory calculation inside Analysis.

## E. Recommendation eligibility specification

For each `rule_code`:

- Objective condition and severity policy, threshold source/version.
- Minimum evidence and data quality required; which source faults suppress it.
- Human-readable recommended **review action**, not final autonomous command.
- Expected impact metric if estimable, assumptions, downside and alternatives.
- Owner operational action (if supported), permission and current-state preconditions.
- Expiry/staleness handling; output of the rule if data partially missing.
- Rule-specific positive, false-positive, no-action and regression fixtures.

## F. UX states

Include Arabic real user label and helper copy; valid empty state, queued/running/fail/partial/complete, saved history, tables and charts, evidence drilldown, accessible keyboard/mobility, English parity, URL/read cache rules and no auto-run assertion. Product owner/SME must approve screenshots or interactive prototypes **before full frontend build**.

## G. Security, privacy, entitlement

Permission matrix (read, own-read, run, review, action), legal entity and branch, source-record access, role changes after request, sensitive field redaction, retention/export/consent, back-end and DB negative tests. Never let a generic Analysis permission overrule source module rights.

## H. Operations and load

Representative data size, expected fact extraction cost, owner read query plan, max allowed timeframe, streaming/pagination, worker memory/CPU/pool/timeout, cancellation, peak period, replica lag policy. Tests: operational baseline and mixed-load p95/p99, DB lock, outbox metrics; no absolute SLA invented.

## I. Acceptance criteria and traceability

- Given/When/Then business examples including partial/reversal/negative facts.
- Exact fixture input, expected KPI and justification, human review.
- API/OpenAPI contract, job/retry/idempotency/historical immutability, source watermark.
- User clicks «بدء التحليل» and only then POST occurs; no analysis POST on GET.
- Completion requires real runtime/DB evidence and approval, no fake PASS.

## J. Release and maintenance

Owners, dependency version, historical migration/replay behavior, change-log, backward-compatible formula version, feature disable/recovery, monitoring, documentation updates and acceptance signoff. If calculation changes later, **new engine/definition version**; no silent reinterpretation of old runs.


---



---

<!-- source: docs/18_IMPLEMENTATION_STRUCTURE_MODULE_MAP.md -->

# 18 — Proposed Code Structure, Ownership and End-to-End Dependency Map

**Status:** implementation blueprint **conditional on P0/P1**. This is not permission to create every folder immediately. Names must be reconciled with current .NET host/module conventions and accepted ADRs before code.

## A. Server code responsibilities

| Proposed component | Role | Explicitly forbidden |
|---|---|---|
| `AnalysisDefinitionCatalog` | Allowed typed definitions, validators, versions and Arabic label translation keys | Runtime arbitrary SQL or untrusted formulas |
| `AnalysisRequestService` | Authorization/entitlement/scope/filter/idempotency validation; records job | Calling owner write operations or executing heavy calculation in request |
| `AnalysisRunRepository` | Analysis-owned state and history (tenant+entity RLS), cursor queries | Direct read/write of Sales/Subledger tables |
| `AnalysisJobClaimer` | Narrow, fenced job claim; lease, fair scheduling, progress | Cross-tenant unrestricted query through `erp_app` |
| `AnalysisJobProcessor` | Bounded extraction, compute, quality checks, result publication | Running in operational API process for heavy workloads |
| `IAnalysisAlgorithm` | Versioned deterministic calculation over **approved fact DTOs** | Reimplementing official accounting/stock financial truth |
| `AnalysisSourceResolver` | Resolve module owner read capabilities, source watermark and cutoff | Importing module implementation assemblies |
| `AnalysisResultPublisher` | Atomically seal versioned output, evidence and recommendation set | Changing historic published results in place |
| `RecommendationRules` | Typed evidence-based proposal rules over validated metrics | Automatically posting business commands |
| `RecommendationReviewService` | Scope-safe review decisions and audit | Treating review as accounting/stock approval |
| `AnalysisStatus/Result API` | Safe status/results/history GET and explicit run POST | GET side effects or implicit recalculation |
| `OperationalHealth/Telemetry` | Job metrics/kill switch independent of business availability | Making core ERP health require AI worker |

**Implementation placement:** prefer one module implementation project (per `solution-boundaries.md`) and its optional Contracts project, not new Domain/Application/Infra projects per module. Separate `AnalysisWorker` is **host**, not another business domain. Required project graph and manifest semantics must be validated by architecture tests.

## B. Ownership matrix and allowed call direction

```text
[Sales/Inventory/Payments/Subledger/Banking existing owner modules]
  ↑ provide published scoped read capabilities, status/evidence, version
  │
[AnalysisSourceResolver / AnalysisWorker]
  → [AnalysisDefinition/Algorithms/Rules]
  → [Analysis-owned Result/Evidence/Recommendation tables]
  → [Analysis GET API]
  → [Dedicated domain analysis route in React]

[React explicit manager POST] → [Analysis Request API] → [Job Store]

[Recommendation review] → [Analysis-owned review history]
[Future explicit action handoff] → [OWNER business Command/Approval]  (separate guarded step)
```

Never reverse the arrow to require daily Sales/Payments/Inventory calls to wait for Analysis. Do not attach AI to owner `SaveChanges`, stock posting or ledger posting. Existing module events and outbox flow remain semantically unchanged.

## C. DI/hosting boundaries

- Operational API host can register low-cost **request and history service** without starting compute loops or reading large data in startup/health probes.
- Operational `backend/Worker` continues existing event consumers and hosted services; no analysis CPU-heavy job hosted there.
- New Analysis host registers only required owner **read capabilities**, Analysis services, observability and dedicated database pool/limits; do not initialize all commercial command consumers unnecessarily if avoidable.
- If owner read capability registration currently bootstraps heavy module commands, P0 must measure/investigate and propose a safe separate registration surface; do not bypass module boundaries.
- Worker deployment role should have independent restart/scale, small resource pool, kill switch and limited source access.
- At-most-attempt / once-final-publication semantics protected by DB fence and uniqueness, not by in-memory singleton locks.

## D. Database and migration convention

- New schema/module-owned tables use existing DbMigrator path and migration tests; RLS FORCE and unique indexes first.
- Any new owner projection is stored and managed **within that owner's schema or published read contract**, not created directly by Analysis in a source schema.
- Avoid runtime DDL, direct cross-schema writes, unbounded JSONB result blobs or heavy trigger changes on operational tables.
- No materialized view refresh tied to invoice posting or transaction commit unless **separately approved and benchmarked**; explicit user job may compute from a bounded owner read model as appropriate.
- Schema evolution is forward-only, additive and backwards-compatible; no destructive `DROP/DELETE` against normal development/prod data.

## E. Frontend structure (illustrative Sales route)

```text
frontend/modules/sales/
  routes.tsx                 # register authorized independent business route
  pages/sales-analysis-page.tsx
  features/analysis/         # module-specific filters/metric explanations
  services/                 # thin API client calls via shared runtime, scoped keys
  i18n/{ar,en}.ts           # natural translations
  tests/                    # route, permission, forms, no-auto-POST, result history
frontend/components/        # common analysis filter form / report history / evidence UI
frontend/app/router/destination-registry.ts
```

Keep `SalesAnalysisPage` within Sales **presentation**, while `Analysis` owns server-side job results. No business UI naming `IntelligenceAndAnalytics` and no new module sidebars. For pilot Collections, actual route may live under Subledger or business-facing customer receivables section; decide with product and current navigation ownership; never inject a ghost page into Payments merely because it has payment facts.

## F. Contracts and API mapping

`frontend type-specific request` → `AnalysisRunRequest DTO` → `AnalysisDefinitionCatalog` → `TenantScopedJob` → `OwnerFactsReadCapability` → `AlgorithmEngine` → `SavedRunResult` → `Result API/History UI`.

A specific analysis definition **must not contain a raw SQL string**. It must reference an approved source fact contract, algorithm implementation and filter schema version. A new module integration should generally add only: `definition`, `owner data read adapter if missing`, `algorithm`, `recommendation policy`, `module route` and `acceptance tests`. It should **reuse** the existing job/persistence/auth/history engine.

## G. Boundaries to test mechanically

- Compile-time project references comply with `backend/Tests/Architecture` graph.
- Frontend imports to other modules only via their root `public.ts`/`public.tsx`; shared components not private direct imports.
- Route is registered in existing effective-access registry; neither parent overview permission nor free-form CSS access governs it.
- No `Analysis` namespace referenced in transaction-writing owner API/services outside explicitly approved optional read links.
- No Analysis SQL contains `INSERT/UPDATE/DELETE` into another module schema; no raw read joins from Analysis into owner schemas.
- No API `GET` creates jobs or touches compute entry point.

## H. Detailed development iteration sequence for first approved P2 slice

1. Open current commit/source docs, prepare fixture demonstrating module+host dependency constraints.
2. Confirm domain owner interfaces and typed analysis definition; approve filter/permission codes.
3. Add migration + RLS + job store under feature gate; clean and incremental upgrade tests.
4. Implement HTTP run POST/idempotency + GET history/status, no compute yet; auth/negative tests.
5. Add dedicated host and one **fixture-only** algorithm verifying lease/retry/results immutability and process isolation.
6. Add result/evidence/recommendation review contract and reliable publication transaction; concurrency/crash tests.
7. Add one module's analysis frontend with reusable components, Arabic copy and permission-driven routes.
8. Run real-stack & mixed-load tests and reconcile regression to P0; fix anything that violates gates **before calling complete**.
9. Update governance and frontend/backend skills where approved architecture changes affect future agents, without loosening existing rules; document support/runbook.
10. Final review/approval, then `origin/dev` commit/push under repo policy for **task-owned** changes only.

## I. Explicit cut scope for initial foundation

Build only **one** execution engine, one history store, one permissioned analysis route, one approved fact adapter, one sample deterministic analysis, one recommendation review path, tests and observability. Do **not** create empty "AI controllers" under all 24 modules or deploy unused Python workers.

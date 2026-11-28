# ToDir ERP — Business Analysis & Decision Recommendations | P1 Design Pack

**Version:** 1.0 — P1 design and approval package; **not approved for implementation**  
**Prepared:** 2026-11-28  
**Reference branch:** `dev`  
**Current repository commit:** `c02f12e5c33f5f0ebd5e3866d447b79eb3b124d6`  
**Audience:** Product Owner, ERP Architect, Backend/DB, Frontend, Security, Performance, QA, implementation agents.

> **ما هذه الحزمة؟** مجموعة مواصفات مرتبة لتنفيذ تحليلات الأعمال والتوصيات داخل ToDir، مع الحفاظ على أداء ووظائف ERP الحالية. هذه وثائق تصميم لا تعني أن أي جداول أو APIs أو Worker جديد قد تم إنشاؤه. أضيفت أدلة P0-B runtime/benchmark اللاحقة كملحق تحقق محلي مستقل.

> **حالة P1 الحالية:** اكتملت مصالحة الحالة الراهنة والتصميم المعماري وعقود القراءة والـWorker والتخزين والصلاحيات والواجهة والأداء ومواصفة الـPilot. الحزمة **جاهزة لمراجعة Product Owner**، لكن القرارات المعلّمة `NEEDS_OWNER_APPROVAL` ليست موافقة ضمنية ولا تسمح ببدء P2.

> **نتيجة P0-B/P0-D:** `P0_EVIDENCE_COMPLETE`. تم تشغيل مسار migration/seed النظيف دون `-PreMigrationSql`، وإثبات سلامة التشغيل المتوازي ودورة حياة bounded lease لـDbMigrator، والتحقق من API وfunctional smoke وtenant isolation وقياسات diagnostic/load القابلة لإعادة الحساب على بيئة disposable. النتائج baseline محلية وليست شهادة سعة إنتاجية. راجع [فهرس أدلة P0](evidence/p0/README.md).

## نقطة الدخول الحالية

اقرأ الوثائق الجديدة بالترتيب التالي قبل أي قرار تنفيذ:

1. [الحالة الراهنة](CURRENT_STATUS.md) ثم [المصالحة](CURRENT_STATE_RECONCILIATION.md).
2. [ملف الاعتماد](ARCHITECTURE_APPROVAL_BRIEF.md) و[سجل القرارات](DECISION_REGISTER.md).
3. [المنتج والـPilot](PRODUCT_AND_PILOT_DOSSIER.md) و[عقود الملكية والزمن](SOURCE_OWNERSHIP_AND_TEMPORAL_TRUTH.md).
4. [التنفيذ والبيانات والأمان](EXECUTION_AND_DATA_ARCHITECTURE.md) ثم [عقد UX/API/Performance](UX_API_AND_PERFORMANCE_CONTRACT.md).
5. [مواصفة الـPilot](PILOT_SPECIFICATION.md) و[مخطط P2](P2_EXECUTION_BLUEPRINT.md).
6. [مصفوفة القبول](ACCEPTANCE_MATRIX.md) و[التحقق الوثائقي](DOCUMENTATION_VERIFICATION.md) قبل تسليم أي كود.

الوثائق المرقمة القديمة تظل مرجع التصميم الأصلي. عند تعارض حقيقة تاريخية فيها مع [CURRENT_STATE_RECONCILIATION.md](CURRENT_STATE_RECONCILIATION.md)، تكون وثائق الحالة الحالية هي المرجع التشغيلي، مع الاحتفاظ بالسجل التاريخي وعدم إعادة كتابة دليل P0.

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

تقرير التدقيق الحالي هو `AUDIT_REPORT_2026-11-24.md` ويُقرأ بعد `README.md` وقبل الوثائق المرقمة. أدلة P0 الحالية موجودة في `evidence/p0/` ويجب قراءتها قبل أي P1/P2 قرار. التقرير والأدلة سجلات تحقق، وليسا مصدر متطلبات بديلًا. أما `reference/` فيحتوي تقارير تاريخية أو نسخًا مجمعة غير معيارية لأغراض المقارنة فقط.

## وضع الملفات داخل مستودع ToDir لاحقًا

المسار المعتمد للحزمة هو `D:\Web\full-projects\todir-erp\docs\enterprise-analysis\`، وتبقى الوثائق المرقمة داخل `docs/` والمراجع داخل `reference/`. النسخة المجمعة الموجودة في `reference/` تاريخية وغير معيارية؛ لا تُعد مصدرًا ثانيًا للمتطلبات. لم تُعدّل ملفات التشغيل ضمن هذا التدقيق.

## قواعد استخدام الحزمة

- **ORDER:** ابدأ بمرحلة Discovery + baseline؛ ثم Architecture ADR/product sign-off؛ ثم Core foundation؛ ثم Pilot موثق؛ ثم التوسع. لا تقفز من الفكرة إلى تنفيذ كل الموديولات.
- **SOURCES:** نتائج P0-B مبنية على `ab49df8b95a16c92804cce7405c42a9f95b0beb7` مع تغييرات P0-D الحالية الخاصة بـDbMigrator والاختبارات وأداة حفظ عينات benchmark. القياسات تخص مسار الطلبات وتُحدّد بحدود Development Seed والبيئة المحلية. baseline وتدقيق التشغيل المتوازي والأرشيف الدائم موثقة في `evidence/p0/P0-B03_OPERATIONAL_PERFORMANCE_BASELINE.md` و`evidence/p0/P0-B07_INDEPENDENT_FINAL_REVIEW.md` و`evidence/p0/P0-B09_ARTIFACT_VERIFICATION_AND_REPRODUCTION.md` و`evidence/p0/P0_FINAL_ACCEPTANCE_AND_HANDOFF.md`.
- **STATUS:** `USER-LOCKED` = متطلب صريح؛ `EXISTING-VERIFIED` = ظهر من قراءة ملفات repo؛ `PROPOSED-ADR` = قرار هندسي مرشح يحتاج اعتماد؛ `TO-VERIFY` = لا يجوز افتراضه قبل الاختبار؛ `BLOCKED` = متوقف على prerequisite محدد؛ `DEFERRED` = مؤجل عمدًا؛ `FUTURE` = خارج النسخة الأولى.
- **OWNERSHIP:** الـERP يملك العمليات والحقائق؛ صاحب الموديول يملك عقود قراءته؛ Analysis يملك عمليات التحليل والنتائج والتوصيات فقط.
- **NAMING:** أسماء الـUI عربية من واقع العمل؛ أسماء الـAPIs التقنية تصف المورد/الوظيفة، ولا تُفرض تسمية «AI» في كل endpoint.
- **CHANGE CONTROL:** أي تغيير في متطلب `USER-LOCKED`، أو أي تعارض مع Master Blueprint، يحتاج ADR/موافقة صريحة وليس قرار Agent ضمني.

## Definition of Ready لهذه الوثائق

هذه حزمة **P1 design complete / owner approval pending** وليست مواصفة تنفيذ نهائية. P0 مغلق حسب أدلته؛ أما بدء P2 فيتطلب موافقة Product Owner على القرارات الموجودة في [ARCHITECTURE_APPROVAL_BRIEF.md](ARCHITECTURE_APPROVAL_BRIEF.md)، ثم تحقق التنفيذ من العقود المقترحة على الكود الحالي. لا يجوز ملء الفجوات بالتخمين.

## References

- [Audited ToDir repository](https://github.com/ahmedAbdelal96/todir-erp/tree/e26de6f6192fdcf7a28802150c53cf124a5cb771)
- [Master Blueprint](https://github.com/ahmedAbdelal96/todir-erp/blob/e26de6f6192fdcf7a28802150c53cf124a5cb771/docs/architecture/01_ERP_SAAS_MASTER_MODULE_AND_DEPENDENCY_BLUEPRINT.md)
- [Navigation and permissions architecture](https://github.com/ahmedAbdelal96/todir-erp/blob/e26de6f6192fdcf7a28802150c53cf124a5cb771/docs/architecture/TODIR_NAVIGATION_ACCESS_UX_ARCHITECTURE_DECISION.md)
- [Backend solution boundaries](https://github.com/ahmedAbdelal96/todir-erp/blob/e26de6f6192fdcf7a28802150c53cf124a5cb771/docs/architecture/solution-boundaries.md)
- [FP&A design pack](https://github.com/ahmedAbdelal96/todir-erp/blob/e26de6f6192fdcf7a28802150c53cf124a5cb771/docs/modules/28_ERP_SAAS_BUDGETING_FINANCIAL_PLANNING_FPA_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md)
- The independent static review by Claude is included in `reference/CLAUDE_COLLECTIONS_AND_WORKLOAD_REVIEW_2026-11-19.md`; its conclusions are **review evidence**, not proven runtime behavior.

# ToDir ERP — Business Analysis & Decision Recommendations | Design Pack

**Version:** 0.9 + audit correction — Architecture/Product design baseline; **NOT approved for implementation**  
**Prepared:** 2026-11-22  
**Reference branch:** `dev`  
**Audit source commit:** `e9f7626953daf4c7cd426a49199dfd46fde69a8c` (audited locally on 2026-11-24)  
**Audience:** Product Owner, ERP Architect, Backend/DB, Frontend, Security, Performance, QA, implementation agents.

> **ما هذه الحزمة؟** مجموعة مواصفات مرتبة لتنفيذ تحليلات الأعمال والتوصيات داخل ToDir، مع الحفاظ على أداء ووظائف ERP الحالية. هذه وثائق تصميم لا تعني أن أي جداول أو APIs أو Worker جديد قد تم إنشاؤه. لم تُجر اختبارات runtime/benchmark في إعداد هذه الحزمة.

> **نتيجة التدقيق:** تم تدقيق الحزمة مقابل `dev` الحالي وتصحيح الفهرسة والنسخة المكررة وبعض الحدود المعمارية. النتيجة **REQUIRES_CORRECTIONS**: لا يبدأ P2 قبل إغلاق قرارات طبقة الموديول/الـWorker، وإثبات عقود القراءة والبيانات الزمنية، وقياس خط أساس الأداء. راجع [تقرير التدقيق](AUDIT_REPORT_2026-11-24.md).

> **نتيجة P0:** `P0_PARTIALLY_VERIFIED`. اكتملت مراجعة المصدر والعقود والحدود، لكن baseline التشغيلي لم يُقَس لأن Docker API رفض الاتصال في بيئة التنفيذ. راجع [فهرس أدلة P0](evidence/p0/README.md).

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
- **SOURCES:** نتائج P0 مبنية على `e9f7626953daf4c7cd426a49199dfd46fde69a8c` محليًا والمطابق لـ`origin/dev` وقت التحقق. لا تدّعي الوثائق تشغيل benchmark؛ سبب عدم القياس موثق في `evidence/p0/P0-02_OPERATIONAL_BASELINE.md`.
- **STATUS:** `USER-LOCKED` = متطلب صريح؛ `EXISTING-VERIFIED` = ظهر من قراءة ملفات repo؛ `PROPOSED-ADR` = قرار هندسي مرشح يحتاج اعتماد؛ `TO-VERIFY` = لا يجوز افتراضه قبل الاختبار؛ `BLOCKED` = متوقف على prerequisite محدد؛ `DEFERRED` = مؤجل عمدًا؛ `FUTURE` = خارج النسخة الأولى.
- **OWNERSHIP:** الـERP يملك العمليات والحقائق؛ صاحب الموديول يملك عقود قراءته؛ Analysis يملك عمليات التحليل والنتائج والتوصيات فقط.
- **NAMING:** أسماء الـUI عربية من واقع العمل؛ أسماء الـAPIs التقنية تصف المورد/الوظيفة، ولا تُفرض تسمية «AI» في كل endpoint.
- **CHANGE CONTROL:** أي تغيير في متطلب `USER-LOCKED`، أو أي تعارض مع Master Blueprint، يحتاج ADR/موافقة صريحة وليس قرار Agent ضمني.

## Definition of Ready لهذه الوثائق

هذه حزمة **Design v0.9** وليست مواصفة تنفيذ نهائية، إلى أن يُنجز P0 ويُثبت: عقود القراءة الممكنة، صحة معاني البيانات، Baseline الأداء، فعالية الـRLS والاشتراكات، سياسة الصلاحيات الدقيقة، ونتيجة ADRs. الفجوات مذكورة صراحةً، لا يجوز ملؤها بالتخمين.

## References

- [Audited ToDir repository](https://github.com/ahmedAbdelal96/todir-erp/tree/e26de6f6192fdcf7a28802150c53cf124a5cb771)
- [Master Blueprint](https://github.com/ahmedAbdelal96/todir-erp/blob/e26de6f6192fdcf7a28802150c53cf124a5cb771/docs/architecture/01_ERP_SAAS_MASTER_MODULE_AND_DEPENDENCY_BLUEPRINT.md)
- [Navigation and permissions architecture](https://github.com/ahmedAbdelal96/todir-erp/blob/e26de6f6192fdcf7a28802150c53cf124a5cb771/docs/architecture/TODIR_NAVIGATION_ACCESS_UX_ARCHITECTURE_DECISION.md)
- [Backend solution boundaries](https://github.com/ahmedAbdelal96/todir-erp/blob/e26de6f6192fdcf7a28802150c53cf124a5cb771/docs/architecture/solution-boundaries.md)
- [FP&A design pack](https://github.com/ahmedAbdelal96/todir-erp/blob/e26de6f6192fdcf7a28802150c53cf124a5cb771/docs/modules/28_ERP_SAAS_BUDGETING_FINANCIAL_PLANNING_FPA_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md)
- The independent static review by Claude is included in `reference/CLAUDE_COLLECTIONS_AND_WORKLOAD_REVIEW_2026-11-19.md`; its conclusions are **review evidence**, not proven runtime behavior.

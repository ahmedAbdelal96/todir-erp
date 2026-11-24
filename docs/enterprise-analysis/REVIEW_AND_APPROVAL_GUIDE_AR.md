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

التدقيق الحالي قرأ فرع `dev` عند `e26de6f6192fdcf7a28802150c53cf124a5cb771`، مع وجود تغييرات غير ملتزم بها في الـworking tree، وتحقق من وجود Manufacturing وQuality ونظام الصلاحيات والـNavigation والـWorkers/RabbitMQ/Quartz والـSubledger/Payments. **ده فحص static وليس إثبات إن كل الموديولات انتهت أو إن أحمال الأداء مقبولة.** لا نعيد استخدام تقرير Claude الأقدم أو النسخة المجمعة التاريخية كأنها Snapshot أحدث؛ المصدر الحالي هو `README.md` وتقرير التدقيق ثم الوثائق المرقمة.

اقرأ `README.md` وترتيب الملفات قبل إسناد مهام للـAgent. ابدأ P0 فقط. كل نقطة غير محسومة موجودة في `docs/14_ADRS_OPEN_QUESTIONS_AND_RISK_REGISTER.md`، وكل شروط القبول في `docs/15_TRACEABILITY_TEST_MATRIX.md`.

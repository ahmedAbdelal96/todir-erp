# ToDir — Code-Based Review: Collections Data Truth & Intelligence Workload Isolation

Nov 19, 2026 · @Ahmed

## الخلاصة والحكم

**Collections مرشح قوي لكنه غير مؤكد بعد: نموذج بيانات Subledger أقوى مما توقعنا، لكن ثلاث ثغرات تحسم الجدوى قبل أي نموذج إحصائي.**

- **نقطة قوة \[Verified\]:** تاريخ التسويات immutable (triggers على الجداول) وSubledger نفسه يعيد بناء الرصيد كما كان في أي تاريخ (as-of) من الـ applications والـ reversals والـ write-offs.
- **ثغرة 1 \[Verified\]:** تاريخ النقد الحقيقي ليس على صف الـ application. هو على الدفعة (Payment) المرتبطة. أما `ApplicationDate` و`SettlementDate` فيُدخلهما المستخدم، ولا توجد قاعدة تاريخ تربطهما بالفاتورة أو بالدفعة.
- **ثغرة 2 \[Verified\]:** تخصيص الدفعة على الفواتير عملية يدوية منفصلة. الفجوة بين وصول النقد وتخصيصه قد تفسد قياس "أيام السداد" إن اعتمدنا تاريخ الـ application.
- **ثغرة 3 \[Verified\]:** لا يوجد read contract على مستوى المحفظة. الـ Aging لعميل واحد فقط، والأحداث تحمل معرفات بلا تواريخ ولا مبالغ. الـ Intelligence يحتاج read model يملكه Subledger.
- **العزل \[Verified + Inference\]:** الـ Worker الحالي process واحد لكل شيء. كل الأحداث نوع MassTransit واحد وتصل إلى 17 consumer، فإضافة consumer للـ Intelligence تضاعف نسخ الرسائل. الأبسط: جدول jobs + Quartz clustered، بلا queue مستقلة في v1.
- **التعارض المعماري:** لا يوجد تعارض صريح مع Blueprint أو solution-boundaries أو FP&A. لكن مطلوب ثلاثة قرارات رسمية (ADRs) في قسم 7.

**توصيتي:** لا تعتمد Collections كخيار نهائي الآن. ابدأ بـ Data Readiness audit (أسبوعان على بيانات عميل حقيقي)، ثم Pilot 0 حتمي (Collections Hygiene)، ثم Pilot 1 إحصائي بعد اجتياز البوابات (قسم 8).

## المنهج وتصنيف الأدلة وحدود المراجعة

المراجعة قراءة ثابتة (static) للكود على فرع `dev` عند commit `d07dd58` من [المستودع](https://github.com/ahmedAbdelal96/todir-erp/tree/dev). لم يُعدَّل شيء ولم تُشغَّل migrations ولا اختبارات ولا قاعدة بيانات. المسارات أدناه نسبية لجذر المستودع، والأسطر تقريبية لهذا الـ commit.

- **Verified:** قرأتُ الكود أو الـ SQL أو الوثيقة المذكورة بنفسي ورأيتُ السلوك مكتوبًا فيها.
- **Inference:** استنتاج منطقي من الكود المقروء، لكن لم يُثبت بتشغيل أو ببيانات حقيقية.
- **Unverified:** لم أجد دليلًا في الكود (مثل الجهة التي تحدّث `reconciliation_state`) أو يتوقف على بيانات العميل أو بيئة الإنتاج.

**حدود المراجعة:** لا أعرف جودة بيانات أي عميل فعلي، ولا حجم الجداول، ولا إعدادات الإنتاج الفعلية خارج `ops/terraform`. أي رقم أداء هنا افتراض يجب قياسه، وليس نتيجة.

## المحور 1أ: ما معنى كل تاريخ، وأين تاريخ التحصيل الحقيقي؟

**الجواب المختصر:** نعم، يمكن استخراج تاريخ تحصيل لكل فاتورة، لكن بربط ثلاثة جداول (open item ← application ← payment)، وليس من جدول واحد. ودقته تتوقف على من أدخل التواريخ ومتى.

| التاريخ | أين يعيش | ماذا يعني فعليًا | التصنيف |
| --- | --- | --- | --- |
| `document_date` / `posting_date` / `due_date` | `party_open_item` (`subledger-schema.sql:48-50`) | تاريخ الفاتورة، تاريخ الترحيل، الاستحقاق من payment terms | Verified |
| `payment_date` / `posting_date` / `value_date` / `completed_at` | `payment` (`payments-schema.sql:78-92`) | تاريخ الدفعة وترحيلها وتاريخ القيمة (قد يكون NULL) ووقت اكتمال المعالجة | Verified |
| `application_date` | `application_batch` (`subledger-schema.sql:114`) | تاريخ محاسبي يُدخله المستخدم للمطابقة بين الدفعة والفاتورة | Verified |
| `settlement_date` | `application_batch` (`subledger-schema.sql:115`) | يُستخدم في الكود لسياق سعر الصرف فقط (`SubledgerService.cs:154,171`) | Verified |
| `confirmed_at` | `application_batch` (`:120`) | وقت التأكيد الفعلي في النظام (system time) | Verified |
| `booking_date` / `value_date` | `external_statement_line` (`banking-schema.sql:91-93`) | تاريخ الحركة على كشف البنك | Verified |

**الفرق بين الثلاثة التي سألت عنها:**

- **تاريخ تنفيذ الدفع الفعلي \[Verified كمكان، Inference كدلالة\]:** موجود على الـ Payment (`payment_date`، و`value_date` إن وُجد). لكن لا يوجد ما يضمن أنه تاريخ النقد المستلم فعلًا لكل طريقة دفع (شيك، تحويل، كاش).
- **ApplicationDate \[Verified\]:** تاريخ تخصيص محاسبي يملأه المستخدم عند الـ apply (`PaymentsService.cs:448-477` يبني `ApplyOpenItemsCommand` بتواريخ يورّدها المستدعي). هو تاريخ المحاسبة، لا تاريخ النقد.
- **SettlementDate \[Verified\]:** لا يؤثر في أرصدة أو aging. استخدامه الوحيد الذي وجدته هو الـ FX context. لا تعتمد عليه كتاريخ نقد.
- **لا تحقق من ترتيب التواريخ \[Verified\]:** `ValidatePair` (`SubledgerRepository.cs:~1288`) لا يتحقق أن `application_date` ≥ تاريخ الفاتورة أو الدفعة، فيمكن أن تظهر تسوية قبل فاتورتها.

**النتيجة \[Inference\]:** تاريخ التحصيل الموثوق = `payment.value_date` إن وجد، وإلا `payment_date`، مع علم مصدر التاريخ لكل صف. ولا تستخدم `application_date` كمصدر أولى لأنه يتأخر بتأخر التخصيص اليدوي.

**ربط البنك \[Unverified\]:** لم أجد كودًا يحدّث `reconciliation_state` في Payments أو Banking، فلا أستطيع القول إن تاريخ الكشف يُنقل إلى `value_date` الدفعة تلقائيًا. الـ claims (`BankingRepository.cs:244-261`) تربط تطابقًا بـ `system_reference`، لكن نمط التطابق الفعلي يحتاج تجربة.

## المحور 1ب: الحالات الحدودية

| الحالة | كيف يتعامل معها النظام حاليًا | أثرها على تحليل التحصيل | التصنيف |
| --- | --- | --- | --- |
| Partial payments | كل application يخصم جزءًا من open item (`open_item_application`, `subledger-schema.sql:132-145`)، والفاتورة تبقى مفتوحة بالباقي | لكل فاتورة أكثر من تاريخ تحصيل. نحتاج مقاييس مرجّحة بالمبلغ (متوسط مرجح، تاريخ أول دفعة، تاريخ السداد الكامل) | Verified |
| Credit notes | تُحفظ كـ open item باتجاه دائن (credit polarity) ثم تُطبّق لاحقًا على الفاتورة (`InvoicingService.cs:66-85, 344-367`) | تخفض الرصيد وليست نقدًا. إذا حُسبت كتحصيل أفسدت مؤشر السداد السريع، ولا بد من تمييز نوع الـ application (نقد vs credit) | Verified (التمييز نفسه Inference) |
| Write-offs | جدول `write_off` مستقل بـ `effective_date` وmaker-checker (`created_by <> approved_by`)، ويخفض الـ open item عند الاعتماد (`pass2-write-offs.sql:69-83`، `SubledgerRepository.cs:~149-185`) | ليس تحصيلًا. يجب استبعاده من مؤشر التحصيل وتسجيله كحدث سلبي (دين متعثر نهائيًا) | Verified |
| Reversals | `application_reversal` بـ `effective_date`، ويُتحقق من تاريخه مقابل `ApplicationDate` (`SubledgerRepository.cs:503-523`)، والدفعة المطبّقة لا تُعكس قبل فك التطبيق (`PaymentsRepository.cs:605`)، وعكس الدفعة ينشئ open item جديدًا (`:630`) | التحصيل الذي ُعكس يجب ألا يُعد ناجحًا. العكس يُسجّل كحدث لاحق ولا يُعدّل التاريخ الأصلي | Verified |
| Opening balances | `opening_batch` بـ `go_live_date`، والبنود لها `document_date` و`due_date`، والدفعة المنتهية immutable (`pass2-opening.sql:9, 46-47`) | لا تاريخ للتحصيلات قبل الـ go-live. أي تحليل سلوكي يبدأ من go-live فقط | Verified |
| Backdated transactions | تواريخ الترحيل والتطبيق مدخلة من المستخدم، لكن `created_at` و`confirmed_at` يبقيان system time، والجداول INSERT-only (`subledger-schema.sql:220-226, 254-256`) | يمكن معرفة ما كان معروفًا فعلًا في وقت معين (knowledge-time)، ما دام التحليل يقارن `created_at` لا تاريخ المستخدم فقط | Verified (الاستغلال Inference) |

**المخاطر التي تنتج من هذا:**

- **التحيّز الزمني \[Inference\]:** إذا دُرّب نموذج على تواريخ تُعرف اليوم لكن لم تكن معروفة وقتها (تخصيص متأخر بتاريخ رجعي)، يحدث label leakage. العلاج: بناء الـ snapshots بثنائية الزمن (valid-time + `created_at`).
- **التقسيم \[Unverified\]:** لم أجد دليلًا على قواعد تمنع تحصيل واحد من أن ينقسم على أكثر من عميل/فاتورة من جهة المستخدم، وهذا شائع عند العملاء في مصر؛ يحتاج فحصًا على بيانات حقيقية.

## المحور 1ج: Snapshots تاريخية، كفاية البيانات، الحد الأدنى للـ Pilot

**هل نستطيع بناء as-of snapshots صحيحة؟ نعم من حيث المبدأ، بشرطين.**

- **\[Verified\]** `GetPartyBalanceAsync` (`SubledgerRepository.cs:764`) يعيد بناء الرصيد عند تاريخ ما باستخدام `historicalSql` (`:802`) مع فلاتر على `posting_date` و`application_date` و`effective_date` للعكس والـ write-off (`:807-848`). الجداول INSERT-only وimmutable فالتاريخ لا يُكتب فوقه.
- **شرط 1 \[Inference\]:** المستقبل يحتاج snapshot لكل عميل ولكل يوم. الاستعلامات الحالية (`:902-984`) تعمل على مستوى الطرف الواحد، فلا يمكن تشغيلها على المحفظة كاملة بلا read model مستقل.
- **شرط 2 \[Inference\]:** الـ as-of هنا يعكس valid-time. لمعرفة ما كان معروفًا في ذلك اليوم نضيف قيد `created_at <= asOf` لتفادي الـ backdating (انظر المحور 1ب).
- **لم أجرِ أية مطابقة بين ناتج `historicalSql` وقيم محفوظة في وقتها \[Unverified\]:** الكود مقنع لكن اختبار مطابقة على بيانات حقيقية غير موجود في ما قرأت.

**الموجود الكافي للتحليل الوصفي والحتمي \[Verified\]:**

- فواتير بتواريخ استحقاق ومبالغ، ومدفوعات بتواريخ وطريقة دفع، وتطبيقات وعكوس وwrite-offs بتواريخ نافذة.
- من هذا يمكن: Aging صحيح في أي تاريخ، وأيام السداد الفعلية لكل فاتورة مغلقة، ومؤشرات مثل التأخر عن الاستحقاق.

**الناقص للتنبؤ الإحصائي:**

- **\[Verified\]** لا توجد بيانات سلوكية منظّمة: تذكيرات، اتصالات تحصيل، وعود بالدفع، ونزاعات، والأحداث تحمل IDs فقط (`SubledgerEvents.cs:5-20`). هذه المعلومات غير محفوظة في ما قرأته لكن يجب التأكد من وجودها في CRM قبل الحكم نهائيًا \[Unverified\].
- **\[Verified\]** لا توجد `ISubledgerCapabilities` لقراءة المحفظة كاملة (`SubledgerContracts.cs:185`).
- **\[Inference\]** تخصيص يدوي للدفعات: المستخدم قد يخصص بعد أيام من استلام النقد، فيظهر العميل أبطأ مما هو.

**الحد الأدنى لـ Pilot موثوق \[Inference من النتائج أعلاه\]:**

- أكثر من 12 شهرًا من التاريخ المستقر بعد الـ go-live (موسمية)، ولكل عميل عدة فواتير مغلقة على الأقل.
- نسبة من الدفعات لها `value_date` أو مطابقة بنكية كافية (عتبة يحددها الـ audit، لا أقترح رقمًا قبل رؤية البيانات).
- فجوة قليلة بين تاريخ استلام النقد وتاريخ التخصيص (مقاسة من `created_at`).
- معدل write-off وreversal منخفض نسبيًا ومستقر.
- قراءة محفظة كاملة عبر read model يملكه Subledger.

## المحور 2: عزل حمل الـ Intelligence

**الحقائق التي تحكم التصميم \[Verified\]:**

- الـ Worker الحالي process واحد يحمل الـ consumers (`Worker/Program.cs:38-59`) وموديولات (`:60-80`) ومهام Quartz (`:81-103`) وhosted services (`:104-108`).
- `OutboxDispatchWorker` واحد: يعمل كل 5 ثوانٍ (`:24`) ويسحب حتى 100 رسالة (`:41`) عبر `claim_pending_outbox` مع `FOR UPDATE SKIP LOCKED` (`platform-kernel-schema.sql:31-50`)، فيمكن تشغيل أكثر من نسخة دون تكرار.
- كل الأحداث تُنشر كنوع MassTransit واحد `OutboxPublishedMessage` (`Core/Messaging/Messaging.cs:14`)، وتستهلكه 17 consumer يرشحون بحسب `MessageType` داخل الكود (مثال: `BankingPosSettlementConsumer.cs`)، لذا كل consumer جديد يستلم نسخة من **كل** الرسائل.
- إعدادات RabbitMQ: quorum queues (`MessagingHostingExtensions.cs:24`)، prefetch 16 (`:25`)، `ConcurrentMessageLimit` 8 (`Messaging.cs:86`)، retry أسّي (`:99-117`)، والـ inbox لمنع التكرار.
- Quartz clustered على Postgres (`SchedulingHostingExtensions.cs:40-48`)، وحد أقصى للـ connection pool (`TODIR_DB_MAX_POOL_SIZE`، `:14-16`)، وموردا Terraform: pool 20 لكل worker وcommand timeout 15 ثانية بلا حدود CPU/ذاكرة ظاهرة (`ops/terraform/main.tf:150-240`).
- العزل بين المستأجرين: الدور `erp_app` مع RLS على `app.tenant_id` (`docs/architecture/runtime-database.md`).
- لم أجد سياسة purge/retention لجدول `outbox_message` (`:559-575`) فهو ينمو بلا حد.

**الإجابات على أسئلتك:**

| السؤال | الجواب | التصنيف |
| --- | --- | --- |
| هل يمكن Intelligence Worker مستقل دون كسر الـ modular monolith؟ | نعم، كـ process إضافي من نفس الـ codebase، يقرأ فقط عبر contracts/read models التي تملكها الموديولات، ويكتب فقط في schema خاص به. هذا يحترم القاعدة المعمارية لأن Roslyn architecture tests ستمنع أي وصول مباشر لجداول موديول آخر | Inference |
| فصل الموارد | process/حاوية منفصلة بحدود CPU/ذاكرة خاصة، وconnection pool صغير، ووظيفة `statement_timeout` أطول لكن مع دور DB خاص يملك صلاحيات قراءة فقط | Inference |
| تجنب ضغط PostgreSQL | قراءة من read replica إن وجد، أو نافذة زمنية غير ذروة مع حد أقصى للاتصالات وقراءة من read model/projection بدل الجداول التشغيلية (يتوافق مع قاعدة Blueprint للتقارير) | Inference |
| تجنب ضغط RabbitMQ | لا تضف consumer على الـ `OutboxPublishedMessage` المشترك، ولا تمرّر أحداث تشغيل التحليل عبر الـ outbox. استخدم جدول jobs وQuartz للجدولة | Inference |
| Duplicate jobs | unique idempotency key = (tenant, job\_type, input\_snapshot\_id, model\_version)، و`INSERT ... ON CONFLICT DO NOTHING`، وclaim بـ `SKIP LOCKED` مثل الـ outbox | Inference |
| Stale results | كل نتيجة تحمل `as_of` و`source_watermark` و`valid_until`، وتُعالَج كـ superseded عند وصول watermark أحدث (نفس فكرة Actual Snapshot Provenance في FP&A) | Inference |
| Tenant data leakage | كل job يعمل باتصال واحد للتينانت ويضبط `app.tenant_id` قبل أي استعلام (RLS)، ويجب ألا تستخدم اتصالات pool مشتركة بلا إعادة ضبط، ولا نماذج مشتركة بين التينانت، ومدخلات التدريب لكل tenant بمفاتيح تمنع الخلط | Inference |
| هل نحتاج queue مستقلة من البداية؟ | لا في v1. جدول jobs مع Quartz الموجود يكفي من حيث حجم العمل المتوقع (تحليلات ليلية لا تفاعلية)، ويتفادى مبدأ Blueprint #20. تضاف queue مستقلة عند استدعاء تحليلات تفاعلية أو نماذج Python | Inference |

**بوابات الأداء والموثوقية قبل التنفيذ:**

- تحميل تحليل كامل لـ tenant كبير لا يرفع زمن استجابة API التشغيلي ولا تأخر الـ outbox lag عن حد تحدده (قياس حالي غير متوفر \[Unverified\]).
- اختبار محاكاة انهيار الـ worker أثناء job لا يُنتج نتيجتين ولا يترك job عالقًا.
- اختبار عزل: تشغيل job لـ tenant أ لا يرى صفًا واحدًا من tenant ب (حتى مع اتصال مُعاد استخدامه).
- قياس حجم نمو `outbox_message` ووجود تنظيف قبل أي زيادة في الأحداث.

## التعارض مع القرارات المعمارية القائمة

لم أجد تعارضًا صريحًا، لكن هناك ثلاث نقاط تحتاج قرارًا رسميًا (ADR) قبل البناء \[استنتاج من مقارنة النصوص\].

| المصدر | ما تنص عليه | التصميم المقترح | الحكم |
| --- | --- | --- | --- |
| Blueprint مبدأ #18 (`01_ERP_SAAS_MASTER...:40-60`) | AI never reads/writes the database directly | Intelligence يقرأ فقط من read models وعبر contracts ولا يكتب في جداول الموديولات | متوافق إذا التزمنا بهذا الشرط |
| Blueprint #20 | تعقيد البنية يجب أن يبرر نفسه | .NET فقط + jobs table + بلا queue مستقلة في v1 | متوافق |
| Blueprint قاعدة التقارير (`:2438-2455`) | projections أولاً، read replica قبل DWH | قراءة من projection خاص بـ Subledger | متوافق، ويحتاج ADR يحدد من يملك الـ projection |
| Blueprint AI Gateway (`:2528-2545`) | risk classes للمخرجات | نتائج Intelligence تصنف توصيات لا قرارات، ولا تكتب مباشرة | متوافق مع تصنيف المخاطر |
| solution-boundaries | موديولات بـ contracts/events، وكاتب واحد (single writer)، واختبارات Roslyn | موديول Intelligence جديد بـ manifest وlayer مناسب (المعمارية الحالية تضع L6 للـ AI/الطبقات العليا، `:140-141`) | متوافق، لكن الـ layer والاعتماديات يجب أن تُقرّ بـ ADR |
| FP&A spec (`28_...FPA...:124,132-135,248-250,268-270,390`) | AI Suggestion != Approved Forecast، Actual Snapshot Provenance، لا Python/SQL للـ tenant في formula engine، AR forecast حتمي مبني على DSO | تحليل Collections يصنَّف كـ analysis لا كـ Forecast معتمد، ونموذج Python ليس جزئاً من الـ formula engine | تعارض محتمل إذا سُميت النتيجة Forecast، والتسمية تحل ذلك |

**الـ ADRs الثلاثة المطلوبة \[Inference\]:**

- **ADR-1:** موقع موديول Intelligence في الـ layers وقائمة الـ contracts التي يقرأها من الموديولات الأخرى.
- **ADR-2:** ملكية الـ Collections read model (Subledger) وقواعد الـ watermark والـ as-of.
- **ADR-3:** تسمية مخرجات Intelligence (analysis/recommendation) وعلاقتها بـ Forecast المعتمد في FP&A، مع منع دخولها في أي أرقام مالية رسمية بلا اعتماد بشري.

## توصية أول Pilot وما يجب تصميمه من البداية

**التسلسل المقترح \[توصية، ليست حقيقة مُثبتة\]:**

1. **Data Readiness audit (أسبوعان):** استعلامات للقراءة فقط على نسخة من بيانات عميل حقيقي: نسبة الدفعات ذات `value_date`، الفجوة بين `payment_date` و`created_at` للتطبيق، عدد الفواتير المغلقة لكل عميل، تطبيقات قبل تاريخ الفاتورة، معدل العكوس والشطب، ونسبة المطابقة البنكية.
2. **Pilot 0 حتمي — Collections Hygiene:** Aging موثّق، أيام سداد فعلية، تنبيهات بشذوذ البيانات (تطبيق قبل فاتورة، دفعات غير مخصصة منذ مدة). قيمة فورية بلا نموذج، وتختبر الـ read model والـ job والعزل.
3. **Pilot 1 إحصائي:** توقع تاريخ السداد أو احتمال التأخر، بعد اجتياز بوابة البيانات وتفوقه على baselines بسيطة (تاريخ العميل السابق، المتوسط المتحرك) في backtest زمني.

**لماذا ليس الحكم نهائيًا:** ما رأيته أن تصميم البيانات يدعم Collections، لكن جودة البيانات الفعلية مجهولة. إذا فشلت البوابة، البديل المحتمل هو **Inventory** (تحذيرات مخزون حتمية) أو **Sales** التي لم أفحصها في هذه المراجعة \[Unverified\].

**ما يجب تصميمه من اليوم الأول لإعادة الاستخدام (Inventory، Sales، Manufacturing، FP&A) \[Inference\]:**

- **Domain-neutral job envelope:** `tenant_id`، `job_type`، `domain`، `input_snapshot_id`، `model_version`، `status`، `idempotency_key` — بلا أثر محدد لـ Collections في الهيكل.
- **Read model يملكه كل موديول** ويقدمه عبر contract بـ `as_of` و`watermark`، لا استعلام خاص بكل تحليل.
- **Result store عام:** نتيجة + مصدر (provenance) + جودة (backtest metrics) + `valid_until` + حالة اعتماد بشري.
- **Baselines-first backtest harness** يعمل لأي domain بتقسيم زمني لا عشوائي.
- **Data Readiness Gate كقالب:** نفس الفحوصات (اكتمال، تاريخية، تباين تواريخ) تُشغّل على أي domain قبل أي نموذج.
- **Python-ready input/output contracts** (مخطط JSON موثّق بـ versioning) حتى لو بدأ التنفيذ بـ .NET، لتسهيل إضافة Python لاحقًا بلا إعادة تصميم.

## ما لم يُتحقق منه وأرخص تجارب لحسمه

| Unverified | أرخص تجربة للتحقق |
| --- | --- |
| من يحدّث `reconciliation_state` وهل تنتقل تواريخ الكشف إلى الدفعة | بحث أعمق في الكود (consumers وjobs وtriggers في SQL) ثم مطابقة عينة من بيانات عميل |
| جودة بيانات العميل الفعلية (فجوة التخصيص، backdating، تقسيم الدفعات) | Data Readiness audit باستعلامات قراءة فقط على نسخة من بيانات عميل (أسبوعان) |
| تطابق ناتج `historicalSql` مع أرصدة موثّقة في تواريخ سابقة | اختبار مقارنة على تقارير محفوظة من نهاية شهر سابق |
| حجم الضغط الحقيقي على PostgreSQL وRabbitMQ من job تحليل كامل | load test على بيئة staging بحجم تينانت كبير مع قياس زمن الـ API والـ outbox lag |
| موارد التشغيل الفعلية خارج Terraform | مراجعة بيئة الإنتاج مع فريق البنية |
| مواقع بيانات السلوك (تذكيرات، اتصالات) في CRM | مراجعة مخطط CRM وما يجمعه فعليًا |
| جاهزية Inventory وSales كبديل لـ Collections | نفس المراجعة الكودية للموديولات هذه قبل أي اختيار |

**تنبيه على الأرقام والأسطر:** أرقام الأسطر أعلاه مقروءة من commit `d07dd58` وقد تتحرك مع أي commit لاحق، لذا يُنصح بالاتكاء على اسم الدالة أو الجدول لا رقم السطر.

import React, { useState } from "react";
import { WizardPage, type WizardStep } from "../../../components/pages/wizard-page.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { Select } from "../../../components/primitives/select.tsx";
import { useCreateImportJob, useDryRunImport, useExecuteImport, useImportJob } from "../hooks/import-queries.ts";
import type { FieldMapping, TargetFieldDefinition } from "../types.ts";

export interface ImportWizardPageProps {
  readonly onFinish?: (() => void) | undefined;
}

const STEPS: readonly WizardStep[] = [
  { id: "upload", title: "رفع الملف", description: "تحديد ملف البيانات (CSV / Excel)" },
  { id: "mapping", title: "مطابقة الحقول", description: "ربط أعمدة الملف بحقول النظام" },
  { id: "validation", title: "الفحص والتجربة", description: "فحص صحة البيانات وتشغيل تجريبي" },
  { id: "execution", title: "تنفيذ الاستيراد", description: "استيراد السجلات إلى قاعدة البيانات" }
];

const TARGET_FIELDS: readonly TargetFieldDefinition[] = [
  { id: "displayName", label: "الاسم التجاري / اسم المنشأة", isRequired: true, example: "شركة الأفق للتجارة" },
  { id: "legalName", label: "الاسم القانوني", isRequired: false, example: "شركة الأفق للتجارة العامة ذ.م.م" },
  { id: "taxId", label: "الرقم الضريبي (VAT)", isRequired: false, example: "300123456700003" },
  { id: "partyType", label: "نوع الطرف (منشأة / فرد)", isRequired: true, example: "Organization" },
  { id: "primaryEmail", label: "البريد الإلكتروني", isRequired: false, example: "info@example.com" },
  { id: "primaryPhone", label: "رقم الهاتف", isRequired: false, example: "+966 50 123 4567" }
];

const SAMPLE_SOURCE_COLUMNS = ["customer_name", "official_legal_name", "vat_number", "type", "email_address", "phone_no"];

export function ImportWizardPage({ onFinish }: ImportWizardPageProps): React.JSX.Element {
  const [currentStepIndex, setCurrentStepIndex] = useState(0);
  const [selectedFile, setSelectedFile] = useState<string>("parties_batch_2026.csv");
  const [mappings, setMappings] = useState<FieldMapping>({
    customer_name: "displayName",
    official_legal_name: "legalName",
    vat_number: "taxId",
    type: "partyType",
    email_address: "primaryEmail",
    phone_no: "primaryPhone"
  });

  const [jobId, setJobId] = useState<string | null>("job-imp-001");
  const { data: job } = useImportJob(jobId);

  const createJobMutation = useCreateImportJob();
  const dryRunMutation = useDryRunImport();
  const executeMutation = useExecuteImport();

  const handleNext = async () => {
    if (currentStepIndex === 0) {
      if (!jobId) {
        const created = await createJobMutation.mutateAsync({
          fileName: selectedFile,
          entityType: "Parties"
        });
        setJobId(created.id);
      }
      setCurrentStepIndex(1);
    } else if (currentStepIndex === 1) {
      if (jobId) {
        await dryRunMutation.mutateAsync({
          jobId,
          mappings
        });
      }
      setCurrentStepIndex(2);
    } else if (currentStepIndex === 2) {
      if (jobId) {
        await executeMutation.mutateAsync({
          jobId,
          mappings
        });
      }
      setCurrentStepIndex(3);
    }
  };

  const handleBack = () => {
    if (currentStepIndex > 0) {
      setCurrentStepIndex(currentStepIndex - 1);
    }
  };

  const handleMappingChange = (sourceCol: string, targetFieldId: string) => {
    setMappings((prev) => ({
      ...prev,
      [sourceCol]: targetFieldId
    }));
  };

  return (
    <WizardPage
      title="معالج استيراد بيانات الأطراف التجارية"
      steps={STEPS}
      currentStepIndex={currentStepIndex}
      onNext={handleNext}
      onBack={handleBack}
      onComplete={onFinish}
      isSubmitting={dryRunMutation.isPending || executeMutation.isPending}
    >
      {currentStepIndex === 0 && (
        <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
          <div
            style={{
              border: "2px dashed var(--color-border-default)",
              borderRadius: "var(--radius-lg)",
              padding: "2.5rem 1.5rem",
              textAlign: "center",
              backgroundColor: "var(--color-surface-subtle)"
            }}
          >
            <div style={{ fontSize: "2.5rem", marginBottom: "0.5rem" }}>📁</div>
            <h3 style={{ margin: "0 0 0.5rem 0" }}>اختر ملف البيانات المراد استيراده</h3>
            <p style={{ margin: "0 0 1rem 0", color: "var(--color-content-muted)", fontSize: "0.875rem" }}>
              الصيغ المدعومة: CSV، Excel (.xlsx). الحد الأقصى لحجم الملف 25 ميجابايت.
            </p>
            <div style={{ display: "inline-block", padding: "0.5rem 1rem", backgroundColor: "var(--color-surface-card)", borderRadius: "var(--radius-md)", border: "1px solid var(--color-border-default)" }}>
              الملف المحدد: <strong>{selectedFile}</strong> (1,000 سجل)
            </div>
          </div>
        </div>
      )}

      {currentStepIndex === 1 && (
        <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
          <div>
            <h3 style={{ margin: "0 0 0.25rem 0" }}>ربط أعمدة الملف بحقول النظام</h3>
            <p style={{ margin: 0, color: "var(--color-content-muted)", fontSize: "0.875rem" }}>
              قم بمطابقة كل عمود من ملف الاستيراد مع الحقل المقابل له في دليل الأطراف.
            </p>
          </div>

          <div style={{ border: "1px solid var(--color-border-default)", borderRadius: "var(--radius-md)", overflow: "hidden" }}>
            <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "0.875rem" }}>
              <thead>
                <tr style={{ backgroundColor: "var(--color-surface-subtle)", borderBottom: "1px solid var(--color-border-default)" }}>
                  <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>العمود في الملف</th>
                  <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>حقل النظام المقابل</th>
                  <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>حالة الحقل</th>
                </tr>
              </thead>
              <tbody>
                {SAMPLE_SOURCE_COLUMNS.map((col) => {
                  const currentTarget = mappings[col] ?? "";
                  const targetDef = TARGET_FIELDS.find((f) => f.id === currentTarget);
                  return (
                    <tr key={col} style={{ borderBottom: "1px solid var(--color-border-default)" }}>
                      <td style={{ padding: "0.75rem 1rem", fontWeight: 600, fontFamily: "var(--font-mono)" }}>
                        {col}
                      </td>
                      <td style={{ padding: "0.5rem 1rem", width: "300px" }}>
                        <Select
                          aria-label={`مطابقة ${col}`}
                          size="compact"
                          selectedKey={currentTarget}
                          onSelectionChange={(key) => handleMappingChange(col, String(key ?? ""))}
                          options={[
                            { id: "", label: "— تجاهل هذا العمود —" },
                            ...TARGET_FIELDS.map((f) => ({
                              id: f.id,
                              label: `${f.label} ${f.isRequired ? "*" : ""}`
                            }))
                          ]}
                        />
                      </td>
                      <td style={{ padding: "0.75rem 1rem" }}>
                        {targetDef?.isRequired ? (
                          <span style={{ color: "var(--color-brand-primary)", fontWeight: 500, fontSize: "0.75rem" }}>
                            حقل إلزامي *
                          </span>
                        ) : (
                          <span style={{ color: "var(--color-content-muted)", fontSize: "0.75rem" }}>
                            اختياري
                          </span>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {currentStepIndex === 2 && (
        <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
          <div>
            <h3 style={{ margin: "0 0 0.25rem 0" }}>نتائج الفحص والتشغيل التجريبي (Dry Run)</h3>
            <p style={{ margin: 0, color: "var(--color-content-muted)", fontSize: "0.875rem" }}>
              تم فحص بنية البيانات ومطابقتها لقواعد التحقق قبل كتابة أي سجل فعلي في قاعدة البيانات.
            </p>
          </div>

          <div style={{ display: "grid", gridTemplateColumns: "repeat(3, 1fr)", gap: "1rem" }}>
            <div style={{ padding: "1rem", backgroundColor: "var(--color-surface-subtle)", borderRadius: "var(--radius-md)", textAlign: "center" }}>
              <div style={{ fontSize: "0.875rem", color: "var(--color-content-muted)" }}>إجمالي السجلات</div>
              <div style={{ fontSize: "1.5rem", fontWeight: 700 }}>1,000</div>
            </div>
            <div style={{ padding: "1rem", backgroundColor: "var(--color-surface-success-subtle, #ecfdf5)", color: "var(--color-status-success)", borderRadius: "var(--radius-md)", textAlign: "center" }}>
              <div style={{ fontSize: "0.875rem" }}>سجلات صالحة للاستيراد</div>
              <div style={{ fontSize: "1.5rem", fontWeight: 700 }}>980</div>
            </div>
            <div style={{ padding: "1rem", backgroundColor: "var(--color-surface-danger-subtle, #fef2f2)", color: "var(--color-status-danger)", borderRadius: "var(--radius-md)", textAlign: "center" }}>
              <div style={{ fontSize: "0.875rem" }}>سجلات تحتوي على أخطاء</div>
              <div style={{ fontSize: "1.5rem", fontWeight: 700 }}>20</div>
            </div>
          </div>

          <div style={{ border: "1px solid var(--color-border-default)", borderRadius: "var(--radius-md)", padding: "1rem" }}>
            <h4 style={{ margin: "0 0 0.75rem 0", fontSize: "0.875rem" }}>سجل الأخطاء المكتشفة (عينة):</h4>
            <ul style={{ margin: 0, paddingInlineStart: "1.25rem", fontSize: "0.8125rem", color: "var(--color-status-danger)", display: "flex", flexDirection: "column", gap: "0.375rem" }}>
              <li>السطر 42: الحقل <code>taxId</code> غير صالح (يجب أن يتكون من 15 رقماً ويبدأ بـ 3).</li>
              <li>السطر 115: الحقل <code>primaryEmail</code> صيغة بريد إلكتروني غير صحيحة.</li>
              <li>السطر 890: الحقل <code>displayName</code> فارغ وهو حقل إلزامي.</li>
            </ul>
          </div>
        </div>
      )}

      {currentStepIndex === 3 && (
        <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem", textAlign: "center", padding: "1rem 0" }}>
          <div style={{ fontSize: "3rem" }}>🎉</div>
          <div>
            <h2 style={{ margin: "0 0 0.5rem 0" }}>اكتمل تنفيذ عملية الاستيراد الجزئي</h2>
            <p style={{ margin: 0, color: "var(--color-content-muted)", fontSize: "0.9375rem" }}>
              تم ترحيل 980 سجلاً بنجاح إلى دليل الأطراف التجارية. تم استبعاد 20 سجلاً مخالفاً وإصدار تقرير تسوية بالأخطاء.
            </p>
          </div>

          <div style={{ display: "inline-flex", justifyContent: "center", gap: "1rem" }}>
            <Button variant="secondary" onPress={() => {}}>
              تحميل تقرير الأخطاء المستبعدة (CSV)
            </Button>
            {onFinish && (
              <Button variant="primary" onPress={onFinish}>
                العودة إلى دليل الأطراف
              </Button>
            )}
          </div>
        </div>
      )}
    </WizardPage>
  );
}

import { useState } from "react";
import { SettingsPage } from "../../../components/pages/settings-page.tsx";
import { FormSection } from "../../../components/forms/form-section.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { Alert } from "../../../components/primitives/alert.tsx";
import { Spinner } from "../../../components/primitives/spinner.tsx";
import { useLoginDomain, type UseIdentityContext } from "../hooks/identity-queries.ts";
import { LoginDomainChangeDialog } from "../features/login-domain-change-dialog.tsx";

export interface LoginDomainSettingsPageProps {
  readonly context?: UseIdentityContext | undefined;
}

export function LoginDomainSettingsPage({ context }: LoginDomainSettingsPageProps): React.JSX.Element {
  const [isChangeDialogOpen, setIsChangeDialogOpen] = useState(false);
  const { data: domainInfo, isLoading, error } = useLoginDomain(context);

  const currentDomain = domainInfo?.loginDomain || "";

  return (
    <SettingsPage
      title="إعدادات تسجيل الدخول للشركة"
      subtitle="إدارة نطاق تسجيل الدخول المعتمد لحسابات موظفي الشركة"
    >
      {isLoading ? (
        <div style={{ display: "flex", justifyContent: "center", padding: "4rem" }}>
          <Spinner size="large" aria-label="جارٍ تحميل إعدادات النطاق..." />
        </div>
      ) : error ? (
        <div style={{ padding: "2rem" }}>
          <Alert variant="danger">تعذر تحميل نطاق تسجيل الدخول الحالي للشركة.</Alert>
        </div>
      ) : (
        <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
          <FormSection
            title="نطاق تسجيل دخول الموظفين"
            description="النطاق المعتمد لإنشاء وإدارة بريد تسجيل الدخول لكافة مستخدمي الشركة"
          >
            <div
              style={{
                backgroundColor: "var(--color-surface-subtle, #f8fafc)",
                padding: "1.5rem",
                borderRadius: "var(--radius-lg, 0.5rem)",
                border: "1px solid var(--color-border-default, #e2e8f0)",
                display: "flex",
                flexDirection: "column",
                gap: "1.25rem"
              }}
            >
              <div>
                <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted, #64748b)", fontWeight: 600 }}>
                  عنوان نطاق الشركة الحالي:
                </span>
                <div
                  style={{
                    fontSize: "1.375rem",
                    fontWeight: 700,
                    color: "var(--color-action-primary, #0f766e)",
                    marginTop: "0.25rem"
                  }}
                >
                  <bdi dir="ltr">@{currentDomain || "—"}</bdi>
                </div>
              </div>

              <p style={{ margin: 0, fontSize: "0.875rem", color: "var(--color-content-secondary, #334155)", lineHeight: 1.6 }}>
                يحدد هذا النطاق اللاحقة التلقائية لبريد تسجيل الدخول لجميع مستخدمي الشركة (مثال: <bdi dir="ltr">ahmed@{currentDomain || "company.com"}</bdi>).
                يقوم النظام بالتعرف على الشركة تلقائيًا فور إدخال الموظف لبريده الإلكتروني وكلمة المرور دون الحاجة لإدخال أي معرّفات إضافية.
              </p>

              <div>
                <Button variant="primary" onPress={() => setIsChangeDialogOpen(true)}>
                  تغيير نطاق تسجيل الدخول...
                </Button>
              </div>
            </div>
          </FormSection>

          <FormSection
            title="إجراءات الأمان والتحقق"
            description="متطلبات الأمان وإجراءات التغييرات الحساسة"
          >
            <Alert variant="info">
              تغيير نطاق تسجيل الدخول عملية أمنية حساسة تتطلب صلاحيات إدارة الشركة والتحقق بخطوتين (MFA). عند تغيير النطاق، سيتم تحديث بريد تسجيل الدخول لكافة مستخدمي الشركة تلقائيًا.
            </Alert>
          </FormSection>
        </div>
      )}

      <LoginDomainChangeDialog
        isOpen={isChangeDialogOpen}
        onOpenChange={setIsChangeDialogOpen}
        currentDomain={currentDomain}
        context={context}
      />
    </SettingsPage>
  );
}

import { useState } from "react";
import { RecordPage } from "../../../components/pages/record-page.tsx";
import { FormSection } from "../../../components/forms/form-section.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { StatusBadge } from "../../../components/fields/status-badge.tsx";
import { Spinner } from "../../../components/primitives/spinner.tsx";
import { useUserRecord, type UseIdentityContext } from "../hooks/identity-queries.ts";
import { AdminPasswordResetDialog } from "../features/admin-password-reset-dialog.tsx";
import { DeactivateUserDialog } from "../features/deactivate-user-dialog.tsx";
import { ReactivateUserDialog } from "../features/reactivate-user-dialog.tsx";
import type { UserAccountSummary } from "../types.ts";

export interface UserRecordPageProps {
  readonly userId: string;
  readonly onBack: () => void;
  readonly context?: UseIdentityContext | undefined;
}

export function UserRecordPage({
  userId,
  onBack,
  context
}: UserRecordPageProps): React.JSX.Element {
  const [resetTargetUser, setResetTargetUser] = useState<UserAccountSummary | null>(null);
  const [deactivateTargetUser, setDeactivateTargetUser] = useState<UserAccountSummary | null>(null);
  const [reactivateTargetUser, setReactivateTargetUser] = useState<UserAccountSummary | null>(null);

  const { data: user, isLoading, error } = useUserRecord(userId, context);

  if (isLoading) {
    return (
      <div style={{ display: "flex", justifyContent: "center", padding: "4rem" }}>
        <Spinner size="large" aria-label="جارٍ تحميل بيانات المستخدم..." />
      </div>
    );
  }

  if (error || !user) {
    return (
      <div style={{ padding: "2rem", textAlign: "center" }}>
        <p style={{ color: "var(--color-status-danger, #ef4444)", fontWeight: 600 }}>
          تعذر العثور على بيانات المستخدم المطلوب.
        </p>
        <Button variant="secondary" onPress={onBack}>
          العودة لقائمة المستخدمين
        </Button>
      </div>
    );
  }

  const isActive = user.status === "Active";

  return (
    <RecordPage
      title={user.name}
      recordCode={user.loginPrefix}
      status={isActive ? "success" : "neutral"}
      breadcrumbs={[
        { label: "المستخدمون", onClick: onBack },
        { label: user.name }
      ]}
      headerActions={
        <div style={{ display: "flex", gap: "0.75rem" }}>
          <Button variant="secondary" onPress={() => setResetTargetUser(user)}>
            إعادة تعيين كلمة المرور
          </Button>
          {isActive ? (
            <Button variant="danger" onPress={() => setDeactivateTargetUser(user)}>
              تعطيل المستخدم
            </Button>
          ) : (
            <Button variant="secondary" onPress={() => setReactivateTargetUser(user)}>
              إعادة تفعيل المستخدم
            </Button>
          )}
          <Button variant="secondary" onPress={onBack}>
            رجوع
          </Button>
        </div>
      }
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
        <FormSection
          title="معلومات الحساب الأساسية"
          description="بيانات الهوية والبريد المستخدم لتسجيل الدخول إلى النظام"
        >
          <div
            style={{
              display: "grid",
              gridTemplateColumns: "repeat(auto-fit, minmax(240px, 1fr))",
              gap: "1.25rem",
              padding: "1rem 0"
            }}
          >
            <div>
              <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted, #64748b)" }}>الاسم:</span>
              <div style={{ fontWeight: 600, fontSize: "1rem", color: "var(--color-content-primary, #0f172a)", marginTop: "0.25rem" }}>
                {user.name}
              </div>
            </div>

            <div>
              <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted, #64748b)" }}>البريد المستخدم لتسجيل الدخول:</span>
              <div style={{ fontWeight: 600, fontSize: "1rem", color: "var(--color-content-primary, #0f172a)", marginTop: "0.25rem" }}>
                <bdi dir="ltr">{user.email}</bdi>
              </div>
            </div>

            <div>
              <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted, #64748b)" }}>حالة الحساب:</span>
              <div style={{ marginTop: "0.25rem" }}>
                <StatusBadge
                  intent={isActive ? "success" : "neutral"}
                  label={isActive ? "نشط" : "معطّل"}
                  size="small"
                />
              </div>
            </div>

            <div>
              <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted, #64748b)" }}>حالة كلمة المرور:</span>
              <div style={{ marginTop: "0.25rem", fontWeight: 500, fontSize: "0.875rem" }}>
                {user.mustChangePassword ? (
                  <span style={{ color: "var(--color-status-warning, #f59e0b)", fontWeight: 600 }}>
                    مطلوب تغيير كلمة المرور عند تسجيل الدخول القادم
                  </span>
                ) : (
                  <span style={{ color: "var(--color-status-success, #15803d)", fontWeight: 600 }}>
                    كلمة المرور مفعّلة ومحدثة
                  </span>
                )}
              </div>
            </div>

            <div>
              <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted, #64748b)" }}>تاريخ إنشاء الحساب:</span>
              <div style={{ fontWeight: 500, fontSize: "0.875rem", color: "var(--color-content-secondary, #334155)", marginTop: "0.25rem" }}>
                {user.createdAt ? new Date(user.createdAt).toLocaleDateString("ar-EG") : "—"}
              </div>
            </div>
          </div>
        </FormSection>
      </div>

      <AdminPasswordResetDialog
        isOpen={Boolean(resetTargetUser)}
        onOpenChange={(open) => !open && setResetTargetUser(null)}
        user={resetTargetUser}
        context={context}
      />

      <DeactivateUserDialog
        isOpen={Boolean(deactivateTargetUser)}
        onOpenChange={(open) => !open && setDeactivateTargetUser(null)}
        user={deactivateTargetUser}
        context={context}
      />

      <ReactivateUserDialog
        isOpen={Boolean(reactivateTargetUser)}
        onOpenChange={(open) => !open && setReactivateTargetUser(null)}
        user={reactivateTargetUser}
        context={context}
      />
    </RecordPage>
  );
}

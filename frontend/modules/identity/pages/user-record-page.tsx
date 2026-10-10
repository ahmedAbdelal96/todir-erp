import { useState } from "react";
import { RecordPage } from "../../../components/pages/record-page.tsx";
import { Surface } from "../../../components/pages/surface.tsx";
import { PropertyGrid, PropertyItem, StatCard } from "../../../components/pages/property-grid.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { StatusBadge } from "../../../components/fields/status-badge.tsx";
import { Spinner } from "../../../components/primitives/spinner.tsx";
import { UserIcon, ShieldIcon, KeyIcon, MailIcon, CheckCircleIcon } from "../../../components/primitives/icons.tsx";
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
      <div style={{ display: "flex", justifyContent: "center", alignItems: "center", minHeight: "360px" }}>
        <Spinner size="large" aria-label="جارٍ تحميل بيانات المستخدم..." />
      </div>
    );
  }

  if (error || !user) {
    return (
      <div style={{ padding: "3rem", textAlign: "center" }}>
        <p style={{ color: "var(--color-status-danger, #ef4444)", fontWeight: 600, marginBottom: "1rem" }}>
          تعذر العثور على بيانات المستخدم المطلوب.
        </p>
        <Button variant="secondary" onPress={onBack}>
          العودة لقائمة المستخدمين
        </Button>
      </div>
    );
  }

  const isActive = user.status === "Active";

  const kpiCards = (
    <>
      <StatCard
        title="حالة الحساب"
        value={isActive ? "نشط ومفعّل" : "معطّل"}
        intent={isActive ? "success" : "neutral"}
        icon={<CheckCircleIcon size={20} />}
        subtitle={isActive ? "يمتلك صلاحية الدخول للنظام" : "تم إيقاف صلاحية الدخول مؤقتاً"}
      />
      <StatCard
        title="حالة كلمة المرور"
        value={user.mustChangePassword ? "مطلوب التغيير" : "محدّثة وآمنة"}
        intent={user.mustChangePassword ? "warning" : "success"}
        icon={<KeyIcon size={20} />}
        subtitle={user.mustChangePassword ? "يلزم التغيير عند أول تسجيل دخول" : "تم تعيينها والتحقق منها بنجاح"}
      />
      <StatCard
        title="النطاق / بادئة الدخول"
        value={user.loginPrefix ?? "الافتراضي"}
        intent="info"
        icon={<ShieldIcon size={20} />}
        subtitle="نطاق تسجيل الدخول المحدد للمنشأة"
      />
    </>
  );

  return (
    <RecordPage
      title={user.name}
      recordCode={user.loginPrefix ? `@${user.loginPrefix}` : undefined}
      status={isActive ? "success" : "neutral"}
      kpiCards={kpiCards}
      breadcrumbs={[
        { label: "إدارة الهوية والمستخدمين", onClick: onBack },
        { label: user.name }
      ]}
      headerActions={
        <div style={{ display: "flex", gap: "0.75rem", flexWrap: "wrap" }}>
          <Button variant="secondary" onPress={() => setResetTargetUser(user)}>
            إعادة تعيين كلمة المرور
          </Button>
          {isActive ? (
            <Button variant="danger" onPress={() => setDeactivateTargetUser(user)}>
              تعطيل المستخدم
            </Button>
          ) : (
            <Button variant="primary" onPress={() => setReactivateTargetUser(user)}>
              إعادة تفعيل الحساب
            </Button>
          )}
          <Button variant="secondary" onPress={onBack}>
            رجوع
          </Button>
        </div>
      }
      auditInfo={{
        createdAt: user.createdAt
      }}
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
        {/* Basic Identity Information */}
        <Surface
          title="بيانات الحساب والهوية"
          description="المعلومات الأساسية وبيانات الاعتماد المستخدمة لتسجيل الدخول"
        >
          <PropertyGrid columns={3}>
            <PropertyItem
              label="الاسم الكامل"
              value={user.name}
              icon={<UserIcon size={16} />}
            />
            <PropertyItem
              label="البريد الإلكتروني الأساسي"
              value={<bdi dir="ltr">{user.email}</bdi>}
              isCopyable
              rawCopyValue={user.email}
              icon={<MailIcon size={16} />}
            />
            <PropertyItem
              label="بادئة الدخول (Login Prefix)"
              value={user.loginPrefix || "—"}
            />
            <PropertyItem
              label="حالة الحساب"
              value={
                <StatusBadge
                  intent={isActive ? "success" : "neutral"}
                  label={isActive ? "نشط ومفعّل" : "معطّل"}
                  size="small"
                />
              }
            />
            <PropertyItem
              label="إلزامية تغيير كلمة المرور"
              value={
                user.mustChangePassword ? (
                  <span style={{ color: "var(--color-status-warning, #d97706)", fontWeight: 600 }}>
                    مطلوب عند الدخول القادم
                  </span>
                ) : (
                  <span style={{ color: "var(--color-status-success, #16a34a)", fontWeight: 600 }}>
                    غير مطلوب
                  </span>
                )
              }
            />
            <PropertyItem
              label="تاريخ الإنشاء في النظام"
              value={
                user.createdAt
                  ? new Date(user.createdAt).toLocaleDateString("ar-EG", {
                      year: "numeric",
                      month: "long",
                      day: "numeric"
                    })
                  : "—"
              }
            />
          </PropertyGrid>
        </Surface>

        {/* Security & Access Settings */}
        <Surface
          title="الأمان وسياسة الوصول"
          description="إعدادات الحماية وصلاحيات الحساب وسجلات الدخول"
        >
          <PropertyGrid columns={2}>
            <PropertyItem
              label="سياسة التحقق والتوثيق"
              value="المصادقة القياسية بكلمة المرور المشفرة"
            />
            <PropertyItem
              label="مستوى الجلسة والنشاط"
              value={isActive ? "متاح للاتصال عبر بوابة الويب والتطبيقات" : "محظور من الاتصال مؤقتاً"}
            />
          </PropertyGrid>
        </Surface>
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

import { useState } from "react";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { Alert } from "../../../components/primitives/alert.tsx";
import { useCreateUser, useRolesList, type UseIdentityContext } from "../hooks/identity-queries.ts";
import { ApiError } from "../../../core/api/http-client.ts";
import type { CreatedUserAccount } from "../types.ts";

export interface CreateUserDialogProps {
  readonly isOpen: boolean;
  readonly onOpenChange: (open: boolean) => void;
  readonly loginDomain: string;
  readonly context?: UseIdentityContext | undefined;
}

export function CreateUserDialog({
  isOpen,
  onOpenChange,
  loginDomain,
  context
}: CreateUserDialogProps): React.JSX.Element {
  const [name, setName] = useState("");
  const [emailPrefix, setEmailPrefix] = useState("");
  const [selectedRoleIds, setSelectedRoleIds] = useState<string[]>([]);
  const [error, setError] = useState<string | undefined>();
  const [createdResult, setCreatedResult] = useState<CreatedUserAccount | null>(null);
  const [copied, setCopied] = useState(false);

  const { data: roles = [], isLoading: isLoadingRoles } = useRolesList(context);
  const createUserMutation = useCreateUser(context);

  function resetForm() {
    setName("");
    setEmailPrefix("");
    setSelectedRoleIds([]);
    setError(undefined);
    setCreatedResult(null);
    setCopied(false);
  }

  function handleClose(open: boolean) {
    if (!open) {
      resetForm();
    }
    onOpenChange(open);
  }

  function handleRoleToggle(roleId: string) {
    setSelectedRoleIds(prev =>
      prev.includes(roleId) ? prev.filter(id => id !== roleId) : [...prev, roleId]
    );
  }

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(undefined);

    const cleanName = name.trim();
    const cleanPrefix = emailPrefix.trim().toLowerCase();

    if (!cleanName) {
      setError("يرجى إدخال اسم المستخدم");
      return;
    }

    if (!cleanPrefix) {
      setError("يرجى إدخال عنوان البريد الإلكتروني للمستخدم");
      return;
    }

    if (!/^[a-zA-Z0-9._-]+$/.test(cleanPrefix)) {
      setError("يجب أن يحتوي البريد على حروف لاتينية وأرقام ونقاط فقط بدون مسافات أو رموز خاصة");
      return;
    }

    try {
      const result = await createUserMutation.mutateAsync({
        name: cleanName,
        emailPrefix: cleanPrefix,
        roleIds: selectedRoleIds
      });
      setCreatedResult(result);
    } catch (err) {
      if (err instanceof ApiError) {
        const code = err.problemDetails?.code || ((err.problemDetails as Record<string, unknown> | undefined)?.extensions as Record<string, unknown> | undefined)?.code as string | undefined;
        if (code === "identity.email_prefix_already_used") {
          setError(`البريد "${cleanPrefix}@${loginDomain}" مستخدم بالفعل لمستخدم آخر.`);
        } else if (code === "identity.name_invalid") {
          setError("اسم المستخدم غير صالح.");
        } else {
          setError(err.problemDetails?.title || err.message || "تعذر إنشاء المستخدم.");
        }
      } else {
        setError("حدث خطأ غير متوقع أثناء إنشاء المستخدم.");
      }
    }
  }

  async function copyTemporaryPassword() {
    if (createdResult?.temporaryPassword) {
      try {
        await navigator.clipboard.writeText(createdResult.temporaryPassword);
        setCopied(true);
        setTimeout(() => setCopied(false), 2000);
      } catch {
        // clipboard unavailable
      }
    }
  }

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={handleClose}
      title={createdResult ? "تم إنشاء المستخدم بنجاح" : "إضافة مستخدم جديد"}
      description={
        createdResult
          ? "تم إنشاء الحساب وتوليد كلمة مرور مؤقتة للاستخدام لمرة واحدة."
          : "أدخل بيانات الموظف لتسجيل حسابه على نطاق الشركة المعتمد."
      }
    >
      {createdResult ? (
        <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem", marginTop: "1rem" }}>
          <Alert variant="warning">
            يرجى تزويد المستخدم ببيانات الدخول المؤقتة هذه بأمان. سيُطلب من المستخدم تعيين كلمة مرور جديدة عند تسجيل دخوله الأول. لن تظهر كلمة المرور هذه مرة أخرى بعد إغلاق النافذة.
          </Alert>

          <div
            style={{
              backgroundColor: "var(--color-surface-subtle, #f8fafc)",
              padding: "1rem",
              borderRadius: "var(--radius-md, 0.375rem)",
              border: "1px solid var(--color-border-default, #e2e8f0)",
              display: "flex",
              flexDirection: "column",
              gap: "0.75rem"
            }}
          >
            <div>
              <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted, #64748b)" }}>الاسم:</span>
              <div style={{ fontWeight: 600, color: "var(--color-content-primary, #0f172a)" }}>
                {createdResult.user.name}
              </div>
            </div>

            <div>
              <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted, #64748b)" }}>البريد المستخدم لتسجيل الدخول:</span>
              <div style={{ fontWeight: 600, color: "var(--color-content-primary, #0f172a)" }}>
                <bdi dir="ltr">{createdResult.user.email}</bdi>
              </div>
            </div>

            <div>
              <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted, #64748b)" }}>كلمة المرور المؤقتة:</span>
              <div
                style={{
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "space-between",
                  marginTop: "0.25rem",
                  padding: "0.5rem 0.75rem",
                  backgroundColor: "var(--color-surface-card, #ffffff)",
                  borderRadius: "var(--radius-sm, 0.25rem)",
                  border: "1px dashed var(--color-border-strong, #94a3b8)",
                  fontFamily: "monospace",
                  fontSize: "1.0625rem",
                  fontWeight: 700,
                  color: "var(--color-action-primary, #0f766e)"
                }}
              >
                <bdi dir="ltr">{createdResult.temporaryPassword}</bdi>
                <Button variant="secondary" size="compact" onPress={() => void copyTemporaryPassword()}>
                  {copied ? "تم النسخ ✓" : "نسخ كلمة المرور"}
                </Button>
              </div>
            </div>
          </div>

          <div style={{ display: "flex", justifyContent: "flex-end", marginTop: "0.5rem" }}>
            <Button variant="primary" onPress={() => handleClose(false)}>
              إتمام وإغلاق
            </Button>
          </div>
        </div>
      ) : (
        <form onSubmit={(e) => void handleSubmit(e)} style={{ display: "flex", flexDirection: "column", gap: "1.25rem", marginTop: "1rem" }}>
          {error && <Alert variant="danger">{error}</Alert>}

          <div>
            <label
              htmlFor="user-fullname"
              style={{
                display: "block",
                fontSize: "0.875rem",
                fontWeight: 600,
                marginBottom: "0.375rem",
                color: "var(--color-content-primary, #0f172a)"
              }}
            >
              الاسم الكامل
            </label>
            <input
              id="user-fullname"
              type="text"
              required
              autoFocus
              placeholder="مثال: أحمد محمد علي"
              value={name}
              onChange={(e) => setName(e.target.value)}
              style={{
                width: "100%",
                padding: "0.625rem 0.875rem",
                fontSize: "0.9375rem",
                borderRadius: "var(--radius-md, 0.375rem)",
                border: "1px solid var(--color-border-default, #cbd5e1)",
                backgroundColor: "var(--color-surface-subtle, #ffffff)",
                color: "var(--color-content-primary, #0f172a)",
                boxSizing: "border-box"
              }}
            />
          </div>

          <div>
            <label
              htmlFor="user-email-prefix"
              style={{
                display: "block",
                fontSize: "0.875rem",
                fontWeight: 600,
                marginBottom: "0.375rem",
                color: "var(--color-content-primary, #0f172a)"
              }}
            >
              البريد المستخدم لتسجيل الدخول
            </label>
            <div
              style={{
                display: "flex",
                alignItems: "center",
                borderRadius: "var(--radius-md, 0.375rem)",
                border: "1px solid var(--color-border-default, #cbd5e1)",
                backgroundColor: "var(--color-surface-subtle, #ffffff)",
                overflow: "hidden"
              }}
            >
              <input
                id="user-email-prefix"
                type="text"
                required
                placeholder="ahmed.ali"
                value={emailPrefix}
                onChange={(e) => setEmailPrefix(e.target.value)}
                style={{
                  flex: 1,
                  padding: "0.625rem 0.875rem",
                  fontSize: "0.9375rem",
                  border: "none",
                  outline: "none",
                  backgroundColor: "transparent",
                  color: "var(--color-content-primary, #0f172a)",
                  direction: "ltr",
                  textAlign: "right"
                }}
              />
              <span
                style={{
                  padding: "0.625rem 0.875rem",
                  backgroundColor: "var(--color-surface-elevated, #f1f5f9)",
                  color: "var(--color-content-muted, #64748b)",
                  fontSize: "0.875rem",
                  fontWeight: 600,
                  borderInlineStart: "1px solid var(--color-border-default, #cbd5e1)",
                  userSelect: "none",
                  direction: "ltr"
                }}
              >
                @{loginDomain || "company.com"}
              </span>
            </div>
            <p style={{ margin: "0.375rem 0 0 0", fontSize: "0.75rem", color: "var(--color-content-muted, #64748b)" }}>
              يتم إنشاء بريد تسجيل الدخول تلقائيًا على نطاق الشركة المعتمد.
            </p>
          </div>

          {roles.length > 0 && (
            <div>
              <span
                style={{
                  display: "block",
                  fontSize: "0.875rem",
                  fontWeight: 600,
                  marginBottom: "0.5rem",
                  color: "var(--color-content-primary, #0f172a)"
                }}
              >
                الأدوار والصلاحيات (اختياري)
              </span>
              <div
                style={{
                  display: "grid",
                  gridTemplateColumns: "repeat(auto-fill, minmax(180px, 1fr))",
                  gap: "0.5rem",
                  maxHeight: "150px",
                  overflowY: "auto",
                  padding: "0.5rem",
                  borderRadius: "var(--radius-md, 0.375rem)",
                  border: "1px solid var(--color-border-default, #e2e8f0)",
                  backgroundColor: "var(--color-surface-subtle, #f8fafc)"
                }}
              >
                {roles.map((role) => (
                  <label
                    key={role.id}
                    style={{
                      display: "flex",
                      alignItems: "center",
                      gap: "0.5rem",
                      fontSize: "0.875rem",
                      cursor: "pointer",
                      padding: "0.25rem 0.375rem",
                      borderRadius: "var(--radius-sm, 0.25rem)"
                    }}
                  >
                    <input
                      type="checkbox"
                      checked={selectedRoleIds.includes(role.id)}
                      onChange={() => handleRoleToggle(role.id)}
                    />
                    <span>{role.name || role.code}</span>
                  </label>
                ))}
              </div>
            </div>
          )}

          <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "0.5rem" }}>
            <Button variant="secondary" onPress={() => handleClose(false)}>
              إلغاء
            </Button>
            <Button type="submit" variant="primary" isLoading={createUserMutation.isPending}>
              إضافة المستخدم
            </Button>
          </div>
        </form>
      )}
    </Dialog>
  );
}

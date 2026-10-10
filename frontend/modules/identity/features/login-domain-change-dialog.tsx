import { useState } from "react";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { Alert } from "../../../components/primitives/alert.tsx";
import { useUpdateLoginDomain, type UseIdentityContext } from "../hooks/identity-queries.ts";
import { ApiError } from "../../../core/api/http-client.ts";

export interface LoginDomainChangeDialogProps {
  readonly isOpen: boolean;
  readonly onOpenChange: (open: boolean) => void;
  readonly currentDomain: string;
  readonly context?: UseIdentityContext | undefined;
}

export function LoginDomainChangeDialog({
  isOpen,
  onOpenChange,
  currentDomain,
  context
}: LoginDomainChangeDialogProps): React.JSX.Element {
  const [newDomain, setNewDomain] = useState("");
  const [error, setError] = useState<string | undefined>();
  const [success, setSuccess] = useState(false);

  const updateDomainMutation = useUpdateLoginDomain(context);

  function handleClose(open: boolean) {
    if (!open) {
      setNewDomain("");
      setError(undefined);
      setSuccess(false);
    }
    onOpenChange(open);
  }

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(undefined);

    const cleanDomain = newDomain.trim().toLowerCase();

    if (!cleanDomain) {
      setError("يرجى إدخال نطاق تسجيل الدخول الجديد.");
      return;
    }

    if (cleanDomain === currentDomain.toLowerCase()) {
      setError("النطاق الجديد مطابق للنطاق الحالي.");
      return;
    }

    const domainRegex = /^(?:[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?\.)+[a-zA-Z]{2,}$/;
    if (!domainRegex.test(cleanDomain)) {
      setError("يرجى إدخال نطاق إنترنت صالح (مثال: company.com).");
      return;
    }

    try {
      await updateDomainMutation.mutateAsync({ loginDomain: cleanDomain });
      setSuccess(true);
    } catch (err) {
      if (err instanceof ApiError) {
        const code = err.problemDetails?.code || ((err.problemDetails as Record<string, unknown> | undefined)?.extensions as Record<string, unknown> | undefined)?.code as string | undefined;
        if (code === "tenant.login_domain_already_used") {
          setError(`النطاق "${cleanDomain}" مستخدم بالفعل لشركة أخرى.`);
        } else if (code === "tenant.login_domain_invalid") {
          setError("صيغة النطاق غير صالحة.");
        } else if (err.status === 403) {
          setError("تتطلب هذه العملية صلاحيات إدارة النطاق والتحقق بخطوتين (MFA).");
        } else {
          setError(err.problemDetails?.title || err.message || "تعذر تغيير نطاق تسجيل الدخول.");
        }
      } else {
        setError("حدث خطأ غير متوقع أثناء تغيير النطاق.");
      }
    }
  }

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={handleClose}
      title={success ? "تم تغيير نطاق تسجيل الدخول" : "تغيير نطاق تسجيل الدخول للشركة"}
      description="تعديل النطاق المعتمد لبريد تسجيل الدخول لجميع مستخدمي الشركة"
    >
      {success ? (
        <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem", marginTop: "1rem" }}>
          <Alert variant="success">
            تم تحديث نطاق تسجيل الدخول بنجاح إلى <bdi dir="ltr">@{newDomain.trim().toLowerCase()}</bdi>. تم تحديث عناوين البريد لكافة مستخدمي الشركة تلقائيًا.
          </Alert>

          <div style={{ display: "flex", justifyContent: "flex-end" }}>
            <Button variant="primary" onPress={() => handleClose(false)}>
              إتمام وإغلاق
            </Button>
          </div>
        </div>
      ) : (
        <form onSubmit={(e) => void handleSubmit(e)} style={{ display: "flex", flexDirection: "column", gap: "1.25rem", marginTop: "1rem" }}>
          {error && <Alert variant="danger">{error}</Alert>}

          <Alert variant="warning">
            <strong>تنبيه أمني هام:</strong> يؤدي تغيير نطاق تسجيل الدخول إلى تحديث عناوين البريد الإلكتروني لكافة موظفي ومستخدمي الشركة في النظام فورًا. سيتوجب على جميع المستخدمين استخدام العناوين الجديدة التي تنتهي بالنطاق الجديد عند تسجيل الدخول لاحقًا.
          </Alert>

          <div>
            <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted, #64748b)" }}>النطاق الحالي:</span>
            <div style={{ fontWeight: 700, fontSize: "1rem", color: "var(--color-content-primary, #0f172a)", marginTop: "0.25rem" }}>
              <bdi dir="ltr">@{currentDomain || "—"}</bdi>
            </div>
          </div>

          <div>
            <label
              htmlFor="new-login-domain"
              style={{
                display: "block",
                fontSize: "0.875rem",
                fontWeight: 600,
                marginBottom: "0.375rem",
                color: "var(--color-content-primary, #0f172a)"
              }}
            >
              نطاق تسجيل الدخول الجديد
            </label>
            <input
              id="new-login-domain"
              type="text"
              required
              autoFocus
              placeholder="atlasgroup.com"
              value={newDomain}
              onChange={(e) => setNewDomain(e.target.value)}
              style={{
                width: "100%",
                padding: "0.625rem 0.875rem",
                fontSize: "0.9375rem",
                borderRadius: "var(--radius-md, 0.375rem)",
                border: "1px solid var(--color-border-default, #cbd5e1)",
                backgroundColor: "var(--color-surface-subtle, #ffffff)",
                color: "var(--color-content-primary, #0f172a)",
                boxSizing: "border-box",
                direction: "ltr",
                textAlign: "right"
              }}
            />
            <p style={{ margin: "0.375rem 0 0 0", fontSize: "0.75rem", color: "var(--color-content-muted, #64748b)" }}>
              أدخل اسم النطاق فقط بدون @ أو https (مثال: newdomain.com).
            </p>
          </div>

          <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "0.5rem" }}>
            <Button variant="secondary" onPress={() => handleClose(false)}>
              إلغاء
            </Button>
            <Button
              type="submit"
              variant="primary"
              isLoading={updateDomainMutation.isPending}
            >
              تأكيد تغيير النطاق
            </Button>
          </div>
        </form>
      )}
    </Dialog>
  );
}

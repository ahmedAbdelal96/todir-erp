import { useState } from "react";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { Alert } from "../../../components/primitives/alert.tsx";
import { useReactivateUser, type UseIdentityContext } from "../hooks/identity-queries.ts";
import type { UserAccountSummary } from "../types.ts";

export interface ReactivateUserDialogProps {
  readonly isOpen: boolean;
  readonly onOpenChange: (open: boolean) => void;
  readonly user: UserAccountSummary | null;
  readonly context?: UseIdentityContext | undefined;
}

export function ReactivateUserDialog({
  isOpen,
  onOpenChange,
  user,
  context
}: ReactivateUserDialogProps): React.JSX.Element {
  const [error, setError] = useState<string | undefined>();
  const reactivateMutation = useReactivateUser(context);

  function handleClose(open: boolean) {
    if (!open) setError(undefined);
    onOpenChange(open);
  }

  async function handleReactivate() {
    if (!user) return;
    setError(undefined);
    try {
      await reactivateMutation.mutateAsync(user.id);
      handleClose(false);
    } catch (err) {
      setError(err instanceof Error ? err.message : "تعذر إعادة تفعيل المستخدم.");
    }
  }

  if (!user) return <></>;

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={handleClose}
      title="إعادة تفعيل المستخدم"
      description={`استعادة وصول المستخدم ${user.name} إلى النظام`}
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem", marginTop: "1rem" }}>
        {error && <Alert variant="danger">{error}</Alert>}

        <p style={{ margin: 0, fontSize: "0.9375rem", color: "var(--color-content-secondary, #334155)", lineHeight: 1.6 }}>
          هل تريد إعادة تفعيل حساب <strong>{user.name}</strong> (<bdi dir="ltr">{user.email}</bdi>)؟
        </p>

        <Alert variant="info">
          سيتمكن المستخدم مجددًا من تسجيل الدخول إلى حسابه في النظام ومباشرة مهامه بنفس الصلاحيات المخصصة له.
        </Alert>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem" }}>
          <Button variant="secondary" onPress={() => handleClose(false)}>
            إلغاء
          </Button>
          <Button
            variant="primary"
            isLoading={reactivateMutation.isPending}
            onPress={() => void handleReactivate()}
          >
            إعادة تفعيل المستخدم
          </Button>
        </div>
      </div>
    </Dialog>
  );
}

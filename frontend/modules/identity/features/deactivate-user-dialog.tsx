import { useState } from "react";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { Alert } from "../../../components/primitives/alert.tsx";
import { useDeactivateUser, type UseIdentityContext } from "../hooks/identity-queries.ts";
import type { UserAccountSummary } from "../types.ts";

export interface DeactivateUserDialogProps {
  readonly isOpen: boolean;
  readonly onOpenChange: (open: boolean) => void;
  readonly user: UserAccountSummary | null;
  readonly context?: UseIdentityContext | undefined;
}

export function DeactivateUserDialog({
  isOpen,
  onOpenChange,
  user,
  context
}: DeactivateUserDialogProps): React.JSX.Element {
  const [error, setError] = useState<string | undefined>();
  const deactivateMutation = useDeactivateUser(context);

  function handleClose(open: boolean) {
    if (!open) setError(undefined);
    onOpenChange(open);
  }

  async function handleDeactivate() {
    if (!user) return;
    setError(undefined);
    try {
      await deactivateMutation.mutateAsync(user.id);
      handleClose(false);
    } catch (err) {
      setError(err instanceof Error ? err.message : "تعذر تعطيل المستخدم.");
    }
  }

  if (!user) return <></>;

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={handleClose}
      title="تعطيل المستخدم"
      description={`تعطيل وصول المستخدم ${user.name} إلى النظام`}
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem", marginTop: "1rem" }}>
        {error && <Alert variant="danger">{error}</Alert>}

        <p style={{ margin: 0, fontSize: "0.9375rem", color: "var(--color-content-secondary, #334155)", lineHeight: 1.6 }}>
          هل تريد بالتأكيد تعطيل حساب <strong>{user.name}</strong> (<bdi dir="ltr">{user.email}</bdi>)؟
        </p>

        <Alert variant="warning">
          لن يتمكن المستخدم من تسجيل الدخول إلى النظام بعد التعطيل، مع بقاء كافة سجلاته وبياناته السابقة محفوظة بالكامل في النظام دون أي حذف.
        </Alert>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem" }}>
          <Button variant="secondary" onPress={() => handleClose(false)}>
            إلغاء
          </Button>
          <Button
            variant="danger"
            isLoading={deactivateMutation.isPending}
            onPress={() => void handleDeactivate()}
          >
            تعطيل المستخدم
          </Button>
        </div>
      </div>
    </Dialog>
  );
}

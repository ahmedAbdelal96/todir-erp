import { useState } from "react";
import { Trans, useTranslation } from "react-i18next";
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
  const { t } = useTranslation("identity");
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
      setError(t("userActions.deactivateFailed"));
    }
  }

  if (!user) return <></>;

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={handleClose}
      title={t("userActions.deactivateTitle")}
      description={t("userActions.deactivateDescription", { name: user.name })}
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem", marginTop: "1rem" }}>
        {error && <Alert variant="danger">{error}</Alert>}

        <p style={{ margin: 0, fontSize: "0.9375rem", color: "var(--color-content-secondary, #334155)", lineHeight: 1.6 }}>
          <Trans ns="identity" i18nKey="userActions.deactivateConfirm" values={{ name: user.name, email: user.email }} components={{ strong: <strong />, bdi: <bdi dir="ltr" /> }} />
        </p>

        <Alert variant="warning">
          {t("userActions.deactivateWarning")}
        </Alert>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem" }}>
          <Button variant="secondary" onPress={() => handleClose(false)}>
            {t("userActions.cancel")}
          </Button>
          <Button
            variant="danger"
            isLoading={deactivateMutation.isPending}
            onPress={() => void handleDeactivate()}
          >
            {t("userActions.deactivateTitle")}
          </Button>
        </div>
      </div>
    </Dialog>
  );
}

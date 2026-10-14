import { useState } from "react";
import { Trans, useTranslation } from "react-i18next";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { Alert } from "../../../components/primitives/alert.tsx";
import { useResetPassword, type UseIdentityContext } from "../hooks/identity-queries.ts";
import type { UserAccountSummary, ResetPasswordResult } from "../types.ts";

export interface AdminPasswordResetDialogProps {
  readonly isOpen: boolean;
  readonly onOpenChange: (open: boolean) => void;
  readonly user: UserAccountSummary | null;
  readonly context?: UseIdentityContext | undefined;
}

export function AdminPasswordResetDialog({
  isOpen,
  onOpenChange,
  user,
  context
}: AdminPasswordResetDialogProps): React.JSX.Element {
  const { t } = useTranslation("identity");
  const [error, setError] = useState<string | undefined>();
  const [result, setResult] = useState<ResetPasswordResult | null>(null);
  const [copied, setCopied] = useState(false);

  const resetPasswordMutation = useResetPassword(context);

  function handleClose(open: boolean) {
    if (!open) {
      setError(undefined);
      setResult(null);
      setCopied(false);
    }
    onOpenChange(open);
  }

  async function handleReset() {
    if (!user) return;
    setError(undefined);
    try {
      const res = await resetPasswordMutation.mutateAsync(user.id);
      setResult(res);
    } catch (err) {
      setError(t("userActions.resetFailed"));
    }
  }

  async function copyPassword() {
    if (result?.temporaryPassword) {
      try {
        await navigator.clipboard.writeText(result.temporaryPassword);
        setCopied(true);
        setTimeout(() => setCopied(false), 2000);
      } catch {
        // clipboard unavailable
      }
    }
  }

  if (!user) return <></>;

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={handleClose}
      title={result ? t("userActions.resetSuccessTitle") : t("userActions.resetPasswordTitle")}
      description={
        result
          ? t("userActions.resetSuccessDescription", { name: user.name })
          : t("userActions.resetDescription", { name: user.name })
      }
    >
      {result ? (
        <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem", marginTop: "1rem" }}>
          <Alert variant="warning">
            {t("userActions.resetTemporaryPasswordWarning")}
          </Alert>

          <div
            style={{
              backgroundColor: "var(--color-surface-subtle, #f8fafc)",
              padding: "1rem",
              borderRadius: "var(--radius-md, 0.375rem)",
              border: "1px solid var(--color-border-default, #e2e8f0)",
              display: "flex",
              flexDirection: "column",
              gap: "0.5rem"
            }}
          >
            <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted, #64748b)" }}>{t("userActions.newTemporaryPassword")}</span>
            <div
              style={{
                display: "flex",
                alignItems: "center",
                justifyContent: "space-between",
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
              <bdi dir="ltr">{result.temporaryPassword}</bdi>
              <Button variant="secondary" size="compact" onPress={() => void copyPassword()}>
                {copied ? t("userActions.copied") : t("userActions.copyPassword")}
              </Button>
            </div>
          </div>

          <div style={{ display: "flex", justifyContent: "flex-end" }}>
            <Button variant="primary" onPress={() => handleClose(false)}>
              {t("userActions.finishClose")}
            </Button>
          </div>
        </div>
      ) : (
        <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem", marginTop: "1rem" }}>
          {error && <Alert variant="danger">{error}</Alert>}

          <p style={{ margin: 0, fontSize: "0.9375rem", color: "var(--color-content-secondary, #334155)", lineHeight: 1.6 }}>
            <Trans ns="identity" i18nKey="userActions.resetConfirm" values={{ name: user.name, email: user.email }} components={{ strong: <strong />, bdi: <bdi dir="ltr" /> }} />
          </p>

          <Alert variant="info">
            {t("userActions.resetWarning")}
          </Alert>

          <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem" }}>
            <Button variant="secondary" onPress={() => handleClose(false)}>
              {t("userActions.cancel")}
            </Button>
            <Button
              variant="primary"
              isLoading={resetPasswordMutation.isPending}
              onPress={() => void handleReset()}
            >
              {t("userActions.resetPasswordTitle")}
            </Button>
          </div>
        </div>
      )}
    </Dialog>
  );
}

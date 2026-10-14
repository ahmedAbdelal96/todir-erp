import { useState } from "react";
import { Trans, useTranslation } from "react-i18next";
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
  const { t } = useTranslation("identity");
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
      setError(t("userActions.domainRequired"));
      return;
    }

    if (cleanDomain === currentDomain.toLowerCase()) {
      setError(t("userActions.domainUnchanged"));
      return;
    }

    const domainRegex = /^(?:[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?\.)+[a-zA-Z]{2,}$/;
    if (!domainRegex.test(cleanDomain)) {
      setError(t("userActions.domainInvalid"));
      return;
    }

    try {
      await updateDomainMutation.mutateAsync({ loginDomain: cleanDomain });
      setSuccess(true);
    } catch (err) {
      if (err instanceof ApiError) {
        const code = err.problemDetails?.code || ((err.problemDetails as Record<string, unknown> | undefined)?.extensions as Record<string, unknown> | undefined)?.code as string | undefined;
        if (code === "tenant.login_domain_already_used") {
          setError(t("userActions.domainAlreadyUsed"));
        } else if (code === "tenant.login_domain_invalid") {
          setError(t("userActions.domainFormatInvalid"));
        } else if (err.status === 403) {
          setError(t("userActions.domainPermissionRequired"));
        } else {
          setError(t("userActions.domainChangeFailed"));
        }
      } else {
        setError(t("userActions.domainUnexpectedError"));
      }
    }
  }

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={handleClose}
      title={success ? t("userActions.domainChangedTitle") : t("userActions.domainChangeTitle")}
      description={t("userActions.domainChangeDescription")}
    >
      {success ? (
        <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem", marginTop: "1rem" }}>
          <Alert variant="success">
            <Trans ns="identity" i18nKey="userActions.domainChangedDescription" values={{ domain: newDomain.trim().toLowerCase() }} components={{ bdi: <bdi dir="ltr" /> }} />
          </Alert>

          <div style={{ display: "flex", justifyContent: "flex-end" }}>
            <Button variant="primary" onPress={() => handleClose(false)}>
              {t("userActions.finishClose")}
            </Button>
          </div>
        </div>
      ) : (
        <form onSubmit={(e) => void handleSubmit(e)} style={{ display: "flex", flexDirection: "column", gap: "1.25rem", marginTop: "1rem" }}>
          {error && <Alert variant="danger">{error}</Alert>}

          <Alert variant="warning">
            <strong>{t("userActions.securityWarningTitle")}</strong> {t("userActions.domainChangeWarning")}
          </Alert>

          <div>
            <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted, #64748b)" }}>{t("userActions.currentDomain")}</span>
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
              {t("userActions.newDomain")}
            </label>
            <input
              id="new-login-domain"
              type="text"
              required
              autoFocus
              placeholder={t("userActions.domainExample")}
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
              {t("userActions.domainHelp")}
            </p>
          </div>

          <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "0.5rem" }}>
            <Button variant="secondary" onPress={() => handleClose(false)}>
              {t("userActions.cancel")}
            </Button>
            <Button
              type="submit"
              variant="primary"
              isLoading={updateDomainMutation.isPending}
            >
              {t("userActions.confirmDomainChange")}
            </Button>
          </div>
        </form>
      )}
    </Dialog>
  );
}

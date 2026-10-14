import { useState } from "react";
import { Trans, useTranslation } from "react-i18next";
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
  const { t } = useTranslation("identity");
  const [isChangeDialogOpen, setIsChangeDialogOpen] = useState(false);
  const { data: domainInfo, isLoading, error } = useLoginDomain(context);

  const currentDomain = domainInfo?.loginDomain || "";

  return (
    <SettingsPage
      title={t("userActions.domainSettingsTitle")}
      subtitle={t("userActions.domainSettingsSubtitle")}
    >
      {isLoading ? (
        <div style={{ display: "flex", justifyContent: "center", padding: "4rem" }}>
          <Spinner size="large" aria-label={t("userActions.domainSettingsLoading")} />
        </div>
      ) : error ? (
        <div style={{ padding: "2rem" }}>
          <Alert variant="danger">{t("userActions.domainSettingsLoadError")}</Alert>
        </div>
      ) : (
        <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
          <FormSection
            title={t("userActions.employeeLoginDomain")}
            description={t("userActions.employeeLoginDomainDescription")}
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
                  {t("userActions.currentDomainAddress")}
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
                <Trans ns="identity" i18nKey="userActions.domainSettingsExplanation" values={{ domain: currentDomain || "company.com" }} components={{ bdi: <bdi dir="ltr" /> }} />
              </p>

              <div>
                <Button variant="primary" onPress={() => setIsChangeDialogOpen(true)}>
                  {t("userActions.changeLoginDomain")}
                </Button>
              </div>
            </div>
          </FormSection>

          <FormSection
            title={t("userActions.securitySettings")}
            description={t("userActions.securitySettingsDescription")}
          >
            <Alert variant="info">
              {t("userActions.domainSecurityNotice")}
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

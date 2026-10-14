import { useState } from "react";
import { useTranslation } from "react-i18next";
import { LocalDateDisplay } from "../../../components/fields/local-date-display.tsx";
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
  const { t } = useTranslation("identity");
  const [resetTargetUser, setResetTargetUser] = useState<UserAccountSummary | null>(null);
  const [deactivateTargetUser, setDeactivateTargetUser] = useState<UserAccountSummary | null>(null);
  const [reactivateTargetUser, setReactivateTargetUser] = useState<UserAccountSummary | null>(null);

  const { data: user, isLoading, error } = useUserRecord(userId, context);

  if (isLoading) {
    return (
      <div style={{ display: "flex", justifyContent: "center", alignItems: "center", minHeight: "360px" }}>
        <Spinner size="large" aria-label={t("userRecord.loading")} />
      </div>
    );
  }

  if (error || !user) {
    return (
      <div style={{ padding: "3rem", textAlign: "center" }}>
        <p style={{ color: "var(--color-status-danger, #ef4444)", fontWeight: 600, marginBottom: "1rem" }}>
          {t("userRecord.notFound")}
        </p>
        <Button variant="secondary" onPress={onBack}>
          {t("userRecord.backToList")}
        </Button>
      </div>
    );
  }

  const isActive = user.status === "Active";

  const kpiCards = (
    <>
      <StatCard
        title={t("userRecord.accountStatus")}
        value={isActive ? t("userRecord.active") : t("userRecord.disabled")}
        intent={isActive ? "success" : "neutral"}
        icon={<CheckCircleIcon size={20} />}
        subtitle={isActive ? t("userRecord.hasAccess") : t("userRecord.accessStopped")}
      />
      <StatCard
        title={t("userRecord.passwordStatus")}
        value={user.mustChangePassword ? t("userRecord.changeRequired") : t("userRecord.currentAndSecure")}
        intent={user.mustChangePassword ? "warning" : "success"}
        icon={<KeyIcon size={20} />}
        subtitle={user.mustChangePassword ? t("userRecord.changeOnFirstLogin") : t("userRecord.passwordVerified")}
      />
      <StatCard
        title={t("userRecord.loginScope")}
        value={user.loginPrefix ?? t("userRecord.default")}
        intent="info"
        icon={<ShieldIcon size={20} />}
        subtitle={t("userRecord.loginScopeDescription")}
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
        { label: t("userRecord.identityManagement"), onClick: onBack },
        { label: user.name }
      ]}
      headerActions={
        <div style={{ display: "flex", gap: "0.75rem", flexWrap: "wrap" }}>
          <Button variant="secondary" onPress={() => setResetTargetUser(user)}>
            {t("userRecord.resetPassword")}
          </Button>
          {isActive ? (
            <Button variant="danger" onPress={() => setDeactivateTargetUser(user)}>
              {t("userRecord.deactivate")}
            </Button>
          ) : (
            <Button variant="primary" onPress={() => setReactivateTargetUser(user)}>
              {t("userRecord.reactivate")}
            </Button>
          )}
          <Button variant="secondary" onPress={onBack}>
            {t("userRecord.back")}
          </Button>
        </div>
      }
      auditInfo={{
        createdAt: <LocalDateDisplay value={user.createdAt} dateStyle="long" />
      }}
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
        {/* Basic Identity Information */}
        <Surface
          title={t("userRecord.accountIdentity")}
          description={t("userRecord.accountDescription")}
        >
          <PropertyGrid columns={3}>
            <PropertyItem
              label={t("userRecord.fullName")}
              value={user.name}
              icon={<UserIcon size={16} />}
            />
            <PropertyItem
              label={t("userRecord.primaryEmail")}
              value={<bdi dir="ltr">{user.email}</bdi>}
              isCopyable
              rawCopyValue={user.email}
              icon={<MailIcon size={16} />}
            />
            <PropertyItem
              label={t("userRecord.loginPrefix")}
              value={user.loginPrefix || "—"}
            />
            <PropertyItem
              label={t("userRecord.status")}
              value={
                <StatusBadge
                  intent={isActive ? "success" : "neutral"}
                  label={isActive ? t("userRecord.active") : t("userRecord.disabled")}
                  size="small"
                />
              }
            />
            <PropertyItem
              label={t("userRecord.passwordChange")}
              value={
                user.mustChangePassword ? (
                  <span style={{ color: "var(--color-status-warning, #d97706)", fontWeight: 600 }}>
                    {t("userRecord.nextLoginRequired")}
                  </span>
                ) : (
                  <span style={{ color: "var(--color-status-success, #16a34a)", fontWeight: 600 }}>
                    {t("userRecord.notRequired")}
                  </span>
                )
              }
            />
            <PropertyItem
              label={t("userRecord.createdAt")}
              value={
                user.createdAt
                  ? <LocalDateDisplay value={user.createdAt} dateStyle="long" />
                  : "—"
              }
            />
          </PropertyGrid>
        </Surface>

        {/* Security & Access Settings */}
        <Surface
          title={t("userRecord.security")}
          description={t("userRecord.securityDescription")}
        >
          <PropertyGrid columns={2}>
            <PropertyItem
              label={t("userRecord.authenticationPolicy")}
              value={t("userRecord.passwordAuth")}
            />
            <PropertyItem
              label={t("userRecord.sessionActivity")}
              value={isActive ? t("userRecord.webAvailable") : t("userRecord.temporarilyBlocked")}
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

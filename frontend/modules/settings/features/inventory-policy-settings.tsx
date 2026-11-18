import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { useOutletContext } from "react-router-dom";
import { Alert } from "../../../components/primitives/alert.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { LocalDateDisplay } from "../../../components/fields/local-date-display.tsx";
import { Spinner } from "../../../components/primitives/spinner.tsx";
import { useOptionalLegalEntityContext, useOptionalPermissions } from "../../../core/runtime/runtime-provider.tsx";
import type { WorkspaceOutletContext } from "../../../app/router/route-contract.ts";
import { useInventoryPolicy } from "../../inventory/public.ts";
import { InventoryPolicyEditDialog } from "./inventory-policy-edit-dialog.tsx";

export function InventoryPolicySettings(): React.JSX.Element {
  const { t } = useTranslation("settings");
  const outletContext = useOutletContext<WorkspaceOutletContext | undefined>();
  const runtimeEntity = useOptionalLegalEntityContext();
  const runtimePermissions = useOptionalPermissions();
  const legalEntityId = outletContext?.legalEntityId ?? runtimeEntity?.legalEntityId;
  const permissions = outletContext?.permissions ?? runtimePermissions;
  const canRead = permissions ? permissions.hasPermission("inventory.stock.read") : true;
  const canManage = permissions ? permissions.hasPermission("inventory.policy.manage") : true;
  const policyQuery = useInventoryPolicy(legalEntityId ?? "", Boolean(legalEntityId) && canRead);
  const [isEditOpen, setIsEditOpen] = useState(false);

  if (!legalEntityId) {
    return <Alert variant="info" title={t("scope.title")}>{t("scope.unavailable")}</Alert>;
  }

  if (!canRead) {
    return <Alert variant="warning" title={t("inventory.title")}>{t("inventory.unavailable")}</Alert>;
  }

  if (policyQuery.isLoading) {
    return (
      <div style={{ display: "grid", placeItems: "center", gap: "0.75rem", minHeight: "12rem", color: "var(--color-content-muted)" }}>
        <Spinner size="medium" />
        <span>{t("inventory.loading")}</span>
      </div>
    );
  }

  if (policyQuery.isError || !policyQuery.data) {
    return (
      <Alert variant="danger" title={t("inventory.title")}>
        <div style={{ display: "flex", alignItems: "center", gap: "0.75rem", flexWrap: "wrap" }}>
          <span>{t("inventory.loadError")}</span>
          <Button variant="secondary" size="compact" onPress={() => void policyQuery.refetch()}>{t("inventory.retry")}</Button>
        </div>
      </Alert>
    );
  }

  const policy = policyQuery.data;
  const approvalLabel = policy.adjustmentApprovalMode === "ApprovalRequired"
    ? t("inventory.approvalRequired")
    : t("inventory.directConfirm");

  return (
    <section aria-label={t("inventory.title")} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
      <header style={{ display: "flex", alignItems: "flex-start", justifyContent: "space-between", gap: "1rem", flexWrap: "wrap" }}>
        <div>
          <h2 style={{ margin: 0, fontSize: "1.25rem", color: "var(--color-content-primary)" }}>{t("inventory.title")}</h2>
          <p style={{ margin: "0.35rem 0 0", color: "var(--color-content-muted)", fontSize: "0.875rem" }}>{t("inventory.subtitle")}</p>
        </div>
        <Button variant="primary" isDisabled={!canManage} onPress={() => setIsEditOpen(true)}>
          {t("inventory.edit")}
        </Button>
      </header>

      {!canManage && <Alert variant="warning">{t("inventory.readOnly")}</Alert>}

      <div style={{ display: "grid", gap: "1rem", padding: "1.25rem", backgroundColor: "var(--color-surface-card)", border: "1px solid var(--color-border-default)", borderRadius: "var(--radius-lg)", boxShadow: "0 1px 2px rgba(15, 23, 42, 0.06), 0 8px 20px rgba(15, 23, 42, 0.04)" }}>
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(14rem, 1fr))", gap: "1rem" }}>
          <PolicyValue label={t("inventory.approvalMode")} value={approvalLabel} description={t("inventory.approvalModeDescription")} />
          <PolicyValue
            label={t("inventory.separationOfDuties")}
            value={policy.separationOfDuties ? t("inventory.enabled") : t("inventory.disabled")}
            description={t("inventory.separationOfDutiesDescription")}
          />
          <PolicyValue
            label={t("inventory.cutoffDate")}
            value={policy.operationalCutoffDate ? <LocalDateDisplay value={policy.operationalCutoffDate} /> : t("inventory.noCutoffDate")}
            description={t("inventory.cutoffDateDescription")}
          />
        </div>
        <div style={{ display: "flex", justifyContent: "flex-end", paddingTop: "0.75rem", borderTop: "1px solid var(--color-border-subtle, #e2e8f0)", color: "var(--color-content-muted)", fontSize: "0.75rem" }}>
          {t("inventory.savedVersion", { version: policy.version })}
        </div>
      </div>

      <InventoryPolicyEditDialog
        isOpen={isEditOpen}
        onOpenChange={setIsEditOpen}
        legalEntityId={legalEntityId}
        policy={policy}
      />
    </section>
  );
}

interface PolicyValueProps {
  readonly label: string;
  readonly value: React.ReactNode;
  readonly description: string;
}

function PolicyValue({ label, value, description }: PolicyValueProps): React.JSX.Element {
  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "0.4rem", minWidth: 0, padding: "0.875rem", backgroundColor: "var(--color-surface-subtle)", border: "1px solid var(--color-border-subtle, #e2e8f0)", borderRadius: "var(--radius-md)" }}>
      <span style={{ fontSize: "0.8125rem", fontWeight: 600, color: "var(--color-content-secondary)" }}>{label}</span>
      <span style={{ fontSize: "1rem", fontWeight: 700, color: "var(--color-content-primary)" }}>{value}</span>
      <span style={{ fontSize: "0.75rem", lineHeight: 1.5, color: "var(--color-content-muted)" }}>{description}</span>
    </div>
  );
}

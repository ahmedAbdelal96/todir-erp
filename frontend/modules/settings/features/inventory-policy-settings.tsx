import React, { useEffect, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { useOutletContext } from "react-router-dom";
import { Alert } from "../../../components/primitives/alert.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { Checkbox } from "../../../components/primitives/checkbox.tsx";
import { Select } from "../../../components/primitives/select.tsx";
import { LocalDateField } from "../../../components/fields/local-date-field.tsx";
import { Spinner } from "../../../components/primitives/spinner.tsx";
import { useOptionalLegalEntityContext, useOptionalPermissions } from "../../../core/runtime/runtime-provider.tsx";
import type { WorkspaceOutletContext } from "../../../app/router/route-contract.ts";
import {
  useInventoryMutations,
  useInventoryPolicy,
  type AdjustmentApprovalMode
} from "../../inventory/public.ts";

interface InventoryPolicyDraft {
  readonly approvalMode: AdjustmentApprovalMode;
  readonly separationOfDuties: boolean;
  readonly cutoffDate: string | null;
}

const emptyDraft: InventoryPolicyDraft = {
  approvalMode: "DirectConfirm",
  separationOfDuties: false,
  cutoffDate: null
};

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
  const { updatePolicy } = useInventoryMutations(legalEntityId ?? "");
  const [draft, setDraft] = useState<InventoryPolicyDraft>(emptyDraft);
  const [hasSaved, setHasSaved] = useState(false);

  useEffect(() => {
    if (!policyQuery.data) {
      return;
    }

    setDraft({
      approvalMode: policyQuery.data.adjustmentApprovalMode,
      separationOfDuties: policyQuery.data.separationOfDuties,
      cutoffDate: policyQuery.data.operationalCutoffDate?.slice(0, 10) ?? null
    });
    setHasSaved(false);
  }, [policyQuery.data]);

  const savedDraft = useMemo<InventoryPolicyDraft | undefined>(() => {
    if (!policyQuery.data) {
      return undefined;
    }

    return {
      approvalMode: policyQuery.data.adjustmentApprovalMode,
      separationOfDuties: policyQuery.data.separationOfDuties,
      cutoffDate: policyQuery.data.operationalCutoffDate?.slice(0, 10) ?? null
    };
  }, [policyQuery.data]);

  const isDirty = Boolean(savedDraft && JSON.stringify(draft) !== JSON.stringify(savedDraft));
  const isLoading = Boolean(legalEntityId) && canRead && policyQuery.isLoading;

  if (!legalEntityId) {
    return <Alert variant="info" title={t("scope.title")}>{t("scope.unavailable")}</Alert>;
  }

  if (!canRead) {
    return <Alert variant="warning" title={t("inventory.title")}>{t("inventory.unavailable")}</Alert>;
  }

  if (isLoading) {
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

  const handleSave = async (): Promise<void> => {
    if (!canManage || !isDirty || updatePolicy.isPending) {
      return;
    }

    setHasSaved(false);
    try {
      await updatePolicy.mutateAsync({
        adjustmentApprovalMode: draft.approvalMode,
        separationOfDuties: draft.separationOfDuties,
        operationalCutoffDate: draft.cutoffDate
      });
      setHasSaved(true);
    } catch {
      setHasSaved(false);
    }
  };

  const approvalOptions = [
    { id: "DirectConfirm", label: t("inventory.directConfirm"), description: t("inventory.directConfirmDescription") },
    { id: "ApprovalRequired", label: t("inventory.approvalRequired"), description: t("inventory.approvalRequiredDescription") }
  ] as const;

  const entityLabel = outletContext?.legalEntityName
    ? `${outletContext.legalEntityName}${outletContext.legalEntityCode ? ` (${outletContext.legalEntityCode})` : ""}`
    : runtimeEntity?.name
      ? `${runtimeEntity.name} (${runtimeEntity.code})`
      : t("scope.unknown");

  return (
    <section aria-label={t("inventory.title")} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
      <div>
        <h2 style={{ margin: 0, fontSize: "1.25rem", color: "var(--color-content-primary)" }}>{t("inventory.title")}</h2>
        <p style={{ margin: "0.35rem 0 0", color: "var(--color-content-muted)", fontSize: "0.875rem" }}>{t("inventory.subtitle")}</p>
      </div>

      <Alert variant="info" title={t("scope.title")}>
        <div style={{ display: "flex", flexDirection: "column", gap: "0.2rem" }}>
          <span>{t("inventory.currentScope", { entity: entityLabel })}</span>
          <span>{t("scope.description")}</span>
        </div>
      </Alert>

      {hasSaved && <Alert variant="success">{t("inventory.saved")}</Alert>}
      {!canManage && <Alert variant="warning">{t("inventory.readOnly")}</Alert>}

      <div style={{ display: "grid", gap: "1.25rem", padding: "1.25rem", backgroundColor: "var(--color-surface-card)", border: "1px solid var(--color-border-default)", borderRadius: "var(--radius-lg)", boxShadow: "0 1px 2px rgba(15, 23, 42, 0.06), 0 8px 20px rgba(15, 23, 42, 0.04)" }}>
        <Select
          label={t("inventory.approvalMode")}
          description={t("inventory.approvalModeDescription")}
          selectedKey={draft.approvalMode}
          isDisabled={!canManage || updatePolicy.isPending}
          onSelectionChange={(key) => {
            if (key !== null) {
              setDraft((current) => ({ ...current, approvalMode: String(key) as AdjustmentApprovalMode }));
            }
          }}
          options={approvalOptions}
        />

        <Checkbox
          label={t("inventory.separationOfDuties")}
          description={t("inventory.separationOfDutiesDescription")}
          isSelected={draft.separationOfDuties}
          isDisabled={!canManage || updatePolicy.isPending}
          onChange={(isSelected) => setDraft((current) => ({ ...current, separationOfDuties: isSelected }))}
        />

        <LocalDateField
          label={t("inventory.cutoffDate")}
          description={t("inventory.cutoffDateDescription")}
          value={draft.cutoffDate ?? ""}
          isDisabled={!canManage || updatePolicy.isPending}
          onChange={(value) => setDraft((current) => ({ ...current, cutoffDate: value.trim() || null }))}
        />

        <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: "1rem", flexWrap: "wrap", paddingTop: "0.25rem", borderTop: "1px solid var(--color-border-subtle, #e2e8f0)" }}>
          <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>{t("inventory.savedVersion", { version: policyQuery.data.version })}</span>
          <Button
            isDisabled={!canManage || !isDirty}
            isLoading={updatePolicy.isPending}
            onPress={() => void handleSave()}
          >
            {t("inventory.save")}
          </Button>
        </div>

        {updatePolicy.isError && <Alert variant="danger">{t("inventory.saveError")}</Alert>}
      </div>
    </section>
  );
}

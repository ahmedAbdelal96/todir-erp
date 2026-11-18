import React, { useEffect, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Alert } from "../../../components/primitives/alert.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { Checkbox } from "../../../components/primitives/checkbox.tsx";
import { Select } from "../../../components/primitives/select.tsx";
import { LocalDateField } from "../../../components/fields/local-date-field.tsx";
import { useInventoryMutations, type AdjustmentApprovalMode, type InventoryPolicy } from "../../inventory/public.ts";

interface InventoryPolicyDraft {
  readonly approvalMode: AdjustmentApprovalMode;
  readonly separationOfDuties: boolean;
  readonly cutoffDate: string | null;
}

interface InventoryPolicyEditDialogProps {
  readonly isOpen: boolean;
  readonly onOpenChange: (open: boolean) => void;
  readonly legalEntityId: string;
  readonly policy: InventoryPolicy;
}

export function InventoryPolicyEditDialog({
  isOpen,
  onOpenChange,
  legalEntityId,
  policy
}: InventoryPolicyEditDialogProps): React.JSX.Element {
  const { t } = useTranslation("settings");
  const { updatePolicy } = useInventoryMutations(legalEntityId);
  const [draft, setDraft] = useState<InventoryPolicyDraft>(() => toDraft(policy));
  const [hasError, setHasError] = useState(false);

  useEffect(() => {
    if (isOpen) {
      setDraft(toDraft(policy));
      setHasError(false);
    }
  }, [isOpen, policy]);

  const savedDraft = useMemo(() => toDraft(policy), [policy]);
  const isDirty = draft.approvalMode !== savedDraft.approvalMode
    || draft.separationOfDuties !== savedDraft.separationOfDuties
    || draft.cutoffDate !== savedDraft.cutoffDate;

  const approvalOptions = [
    { id: "DirectConfirm", label: t("inventory.directConfirm"), description: t("inventory.directConfirmDescription") },
    { id: "ApprovalRequired", label: t("inventory.approvalRequired"), description: t("inventory.approvalRequiredDescription") }
  ] as const;

  const handleSubmit = async (event: React.FormEvent<HTMLFormElement>): Promise<void> => {
    event.preventDefault();
    if (!isDirty || updatePolicy.isPending) return;

    setHasError(false);
    try {
      await updatePolicy.mutateAsync({
        adjustmentApprovalMode: draft.approvalMode,
        separationOfDuties: draft.separationOfDuties,
        operationalCutoffDate: draft.cutoffDate
      });
      onOpenChange(false);
    } catch {
      setHasError(true);
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={onOpenChange}
      title={t("inventory.editTitle")}
      description={t("inventory.editDescription")}
      size="medium"
    >
      <form onSubmit={(event) => void handleSubmit(event)} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {hasError && <Alert variant="danger">{t("inventory.saveError")}</Alert>}

        <Select
          label={t("inventory.approvalMode")}
          description={t("inventory.approvalModeDescription")}
          selectedKey={draft.approvalMode}
          isDisabled={updatePolicy.isPending}
          onSelectionChange={(key) => {
            if (key !== null) setDraft((current) => ({ ...current, approvalMode: String(key) as AdjustmentApprovalMode }));
          }}
          options={approvalOptions}
        />

        <Checkbox
          label={t("inventory.separationOfDuties")}
          description={t("inventory.separationOfDutiesDescription")}
          isSelected={draft.separationOfDuties}
          isDisabled={updatePolicy.isPending}
          onChange={(isSelected) => setDraft((current) => ({ ...current, separationOfDuties: isSelected }))}
        />

        <LocalDateField
          label={t("inventory.cutoffDate")}
          description={t("inventory.cutoffDateDescription")}
          value={draft.cutoffDate ?? ""}
          isDisabled={updatePolicy.isPending}
          onChange={(value) => setDraft((current) => ({ ...current, cutoffDate: value.trim() || null }))}
        />

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "0.25rem" }}>
          <Button variant="secondary" type="button" onPress={() => onOpenChange(false)}>
            {t("inventory.cancel")}
          </Button>
          <Button type="submit" isDisabled={!isDirty} isLoading={updatePolicy.isPending}>
            {t("inventory.save")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

function toDraft(policy: InventoryPolicy): InventoryPolicyDraft {
  return {
    approvalMode: policy.adjustmentApprovalMode,
    separationOfDuties: policy.separationOfDuties,
    cutoffDate: policy.operationalCutoffDate?.slice(0, 10) ?? null
  };
}

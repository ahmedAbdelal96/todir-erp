import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Select } from "../../../../components/primitives/select.tsx";
import { Checkbox } from "../../../../components/primitives/checkbox.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { generateKey } from "../../../../core/api/http-client.ts";
import { useOpportunityContactRoles, useAddOpportunityContactRoleMutation } from "../../hooks/use-crm-queries.ts";
import { mapCrmError } from "../../services/crm-error-map.ts";
import type { Opportunity } from "../../types.ts";

export interface BuyingCommitteeModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly opportunity: Opportunity | null;
}

export function BuyingCommitteeModal({
  isOpen,
  onClose,
  legalEntityId,
  opportunity
}: BuyingCommitteeModalProps): React.JSX.Element {
  const { t } = useTranslation("crm");
  const { data: contactRoles = [], isLoading } = useOpportunityContactRoles(legalEntityId, opportunity?.id || "");
  const addMutation = useAddOpportunityContactRoleMutation(legalEntityId, opportunity?.id || "");

  const [role, setRole] = useState("DecisionMaker");
  const [isPrimary, setIsPrimary] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const roleOptions = [
    { id: "DecisionMaker", label: t("contactRoles.DecisionMaker") },
    { id: "Influencer", label: t("contactRoles.Influencer") },
    { id: "Champion", label: t("contactRoles.Champion") },
    { id: "Buyer", label: t("contactRoles.Buyer") },
    { id: "Gatekeeper", label: t("contactRoles.Gatekeeper") },
    { id: "TechnicalEvaluator", label: t("contactRoles.TechnicalEvaluator") }
  ];

  const handleAdd = async (e: React.FormEvent) => {
    e.preventDefault();

    try {
      await addMutation.mutateAsync({
        contactPersonId: generateKey(),
        role,
        isPrimary
      });

      setRole("DecisionMaker");
      setIsPrimary(false);
      setErrorMessage(null);
    } catch (err: unknown) {
      const mapped = mapCrmError(err);
      setErrorMessage(t(mapped.messageKey, mapped.defaultMessage));
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open: boolean) => {
        if (!open) onClose();
      }}
      title={t("opportunities.committeeModal.title")}
      size="medium"
      footer={
        <Button variant="secondary" onPress={onClose}>
          {t("actions.close")}
        </Button>
      }
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {errorMessage && (
          <div
            role="alert"
            style={{
              padding: "0.625rem 0.875rem",
              borderRadius: "var(--radius-sm)",
              backgroundColor: "var(--color-status-danger-subtle, #fee2e2)",
              color: "var(--color-status-danger, #b91c1c)",
              fontSize: "0.8125rem"
            }}
          >
            {errorMessage}
          </div>
        )}

        <form onSubmit={handleAdd} style={{ display: "flex", gap: "0.75rem", alignItems: "flex-end", flexWrap: "wrap" }}>
          <div style={{ flex: "1 1 200px" }}>
            <Select
              label={t("opportunities.committeeModal.role")}
              selectedKey={role}
              onSelectionChange={(k) => {
                if (k) setRole(String(k));
              }}
              options={roleOptions}
            />
          </div>
          <div style={{ paddingBottom: "0.5rem" }}>
            <Checkbox isSelected={isPrimary} onChange={setIsPrimary}>
              {t("opportunities.committeeModal.isPrimary")}
            </Checkbox>
          </div>
          <Button type="submit" variant="primary" isDisabled={addMutation.isPending} style={{ marginBottom: "2px" }}>
            {addMutation.isPending ? t("actions.saving") : t("actions.add")}
          </Button>
        </form>

        <div style={{ border: "1px solid var(--color-border-default)", borderRadius: "var(--radius-md)", overflow: "hidden" }}>
          <div style={{ padding: "0.5rem 0.75rem", backgroundColor: "var(--color-surface-subtle)", fontWeight: 600, fontSize: "0.8125rem" }}>
            {t("opportunities.committeeModal.listTitle")} ({contactRoles.length})
          </div>

          <div style={{ maxHeight: "220px", overflowY: "auto" }}>
            {isLoading ? (
              <div style={{ padding: "1.5rem", textAlign: "center", color: "var(--color-content-muted)" }}>
                {t("leads.loading")}
              </div>
            ) : contactRoles.length === 0 ? (
              <div style={{ padding: "1.5rem", textAlign: "center", color: "var(--color-content-muted)", fontSize: "0.8125rem" }}>
                {t("opportunities.committeeModal.empty")}
              </div>
            ) : (
              contactRoles.map((item) => (
                <div
                  key={item.id}
                  style={{
                    padding: "0.625rem 0.75rem",
                    borderBottom: "1px solid var(--color-surface-subtle)",
                    display: "flex",
                    justifyContent: "space-between",
                    alignItems: "center",
                    fontSize: "0.875rem"
                  }}
                >
                  <div style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
                    <span style={{ fontWeight: 600 }}>{t(`contactRoles.${item.role}` as any)}</span>
                    {item.isPrimary && (
                      <span
                        style={{
                          fontSize: "0.6875rem",
                          padding: "0.125rem 0.375rem",
                          borderRadius: "var(--radius-sm)",
                          backgroundColor: "#eff6ff",
                          color: "#1e40af",
                          fontWeight: 700
                        }}
                      >
                        {t("opportunities.committeeModal.isPrimary")}
                      </span>
                    )}
                  </div>
                </div>
              ))
            )}
          </div>
        </div>
      </div>
    </Dialog>
  );
}

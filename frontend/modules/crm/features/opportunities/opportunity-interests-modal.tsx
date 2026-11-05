import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { useOpportunityInterests, useAddOpportunityInterestMutation } from "../../hooks/use-crm-queries.ts";
import { mapCrmError } from "../../services/crm-error-map.ts";
import type { Opportunity } from "../../types.ts";

export interface OpportunityInterestsModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly opportunity: Opportunity | null;
}

export function OpportunityInterestsModal({
  isOpen,
  onClose,
  legalEntityId,
  opportunity
}: OpportunityInterestsModalProps): React.JSX.Element {
  const { t } = useTranslation("crm");
  const { data: interests = [], isLoading } = useOpportunityInterests(legalEntityId, opportunity?.id || "");
  const addMutation = useAddOpportunityInterestMutation(legalEntityId, opportunity?.id || "");

  const [description, setDescription] = useState("");
  const [quantity, setQuantity] = useState("1");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const handleAdd = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!description.trim()) {
      setErrorMessage(t("errors.interestDescriptionRequired"));
      return;
    }

    try {
      await addMutation.mutateAsync({
        productVariantId: null,
        description: description.trim(),
        quantity: Number(quantity) || 1
      });

      setDescription("");
      setQuantity("1");
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
      title={t("opportunities.interestsModal.title")}
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
              background: "var(--color-status-danger-subtle, #fee2e2)",
              color: "var(--color-status-danger, #b91c1c)",
              fontSize: "0.8125rem"
            }}
          >
            {errorMessage}
          </div>
        )}

        <form onSubmit={handleAdd} style={{ display: "flex", gap: "0.5rem", alignItems: "flex-end" }}>
          <div style={{ flex: 2 }}>
            <TextField
              label={t("opportunities.interestsModal.description")}
              value={description}
              onChange={setDescription}
              placeholder={t("opportunities.interestsModal.descriptionPlaceholder")}
              isRequired
            />
          </div>
          <div style={{ flex: 1, maxWidth: "100px" }}>
            <TextField
              label={t("opportunities.interestsModal.quantity")}
              value={quantity}
              onChange={setQuantity}
              type="number"
              isRequired
            />
          </div>
          <Button type="submit" variant="primary" isDisabled={addMutation.isPending} style={{ marginBottom: "2px" }}>
            {addMutation.isPending ? t("actions.saving") : t("actions.add")}
          </Button>
        </form>

        <div style={{ border: "1px solid var(--color-border-default)", borderRadius: "var(--radius-md)", overflow: "hidden" }}>
          <div style={{ padding: "0.5rem 0.75rem", backgroundColor: "var(--color-surface-subtle)", fontWeight: 600, fontSize: "0.8125rem" }}>
            {t("opportunities.interestsModal.listTitle")} ({interests.length})
          </div>

          <div style={{ maxHeight: "220px", overflowY: "auto" }}>
            {isLoading ? (
              <div style={{ padding: "1.5rem", textAlign: "center", color: "var(--color-content-muted)" }}>
                {t("leads.loading")}
              </div>
            ) : interests.length === 0 ? (
              <div style={{ padding: "1.5rem", textAlign: "center", color: "var(--color-content-muted)", fontSize: "0.8125rem" }}>
                {t("opportunities.interestsModal.empty")}
              </div>
            ) : (
              interests.map((item) => (
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
                  <span style={{ fontWeight: 500 }}>{item.description}</span>
                  <span style={{ color: "var(--color-content-muted)", fontSize: "0.8125rem" }}>
                    {t("opportunities.interestsModal.qty", { count: item.quantity })}
                  </span>
                </div>
              ))
            )}
          </div>
        </div>
      </div>
    </Dialog>
  );
}

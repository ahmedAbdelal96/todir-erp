import React, { useState, useEffect } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Select } from "../../../../components/primitives/select.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { useUpdateOpportunityMutation } from "../../hooks/use-crm-queries.ts";
import { mapCrmError } from "../../services/crm-error-map.ts";
import type { Opportunity, ForecastCategory } from "../../types.ts";

export interface EditOpportunityModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly opportunity: Opportunity | null;
  readonly onUpdated?: (() => void) | undefined;
}

export function EditOpportunityModal({
  isOpen,
  onClose,
  legalEntityId,
  opportunity,
  onUpdated
}: EditOpportunityModalProps): React.JSX.Element {
  const { t } = useTranslation("crm");
  const updateMutation = useUpdateOpportunityMutation(legalEntityId, opportunity?.id || "");

  const [name, setName] = useState("");
  const [estimatedAmount, setEstimatedAmount] = useState("");
  const [expectedCloseDate, setExpectedCloseDate] = useState("");
  const [forecastCategory, setForecastCategory] = useState<ForecastCategory>("Pipeline");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  useEffect(() => {
    if (opportunity) {
      setName(opportunity.name);
      setEstimatedAmount(String(opportunity.estimatedAmount));
      setExpectedCloseDate(opportunity.expectedCloseDate || "");
      setForecastCategory(opportunity.forecastCategory);
      setErrorMessage(null);
    }
  }, [opportunity]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!opportunity) return;
    if (!name.trim()) {
      setErrorMessage(t("errors.opportunityNameRequired"));
      return;
    }

    try {
      await updateMutation.mutateAsync({
        name: name.trim(),
        estimatedAmount: Number(estimatedAmount) || 0,
        expectedCloseDate: expectedCloseDate || null,
        forecastCategory,
        version: opportunity.version
      });

      onClose();
      onUpdated?.();
    } catch (err: unknown) {
      const mapped = mapCrmError(err);
      setErrorMessage(t(mapped.messageKey, mapped.defaultMessage));
    }
  };

  const forecastCategoryOptions = [
    { id: "Pipeline", label: t("forecastCategories.Pipeline") },
    { id: "BestCase", label: t("forecastCategories.BestCase") },
    { id: "Commit", label: t("forecastCategories.Commit") },
    { id: "Closed", label: t("forecastCategories.Closed") }
  ];

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open: boolean) => {
        if (!open) onClose();
      }}
      title={t("opportunities.editOpportunityModal.title")}
      size="medium"
      footer={
        <>
          <Button
            variant="secondary"
            onPress={onClose}
            isDisabled={updateMutation.isPending}
          >
            {t("actions.cancel")}
          </Button>
          <Button
            variant="primary"
            type="submit"
            form="edit-opportunity-form"
            isDisabled={updateMutation.isPending}
          >
            {updateMutation.isPending ? t("actions.saving") : t("actions.save")}
          </Button>
        </>
      }
    >
      <form id="edit-opportunity-form" onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "0.875rem" }}>
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

        <TextField
          label={t("opportunities.fields.name")}
          value={name}
          onChange={setName}
          isRequired
        />

        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", gap: "0.75rem" }}>
          <TextField
            label={t("opportunities.fields.estimatedAmount")}
            value={estimatedAmount}
            onChange={setEstimatedAmount}
            type="number"
            isRequired
          />
          <TextField
            label={t("opportunities.fields.expectedCloseDate")}
            value={expectedCloseDate}
            onChange={setExpectedCloseDate}
            type="date"
          />
        </div>

        <Select
          label={t("opportunities.fields.forecastCategory")}
          selectedKey={forecastCategory}
          onSelectionChange={(k) => setForecastCategory(k as ForecastCategory)}
          options={forecastCategoryOptions}
        />
      </form>
    </Dialog>
  );
}

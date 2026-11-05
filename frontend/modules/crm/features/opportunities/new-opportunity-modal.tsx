import React, { useState, useEffect } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Select } from "../../../../components/primitives/select.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { useCreateOpportunityMutation, usePipelines } from "../../hooks/use-crm-queries.ts";
import { mapCrmError } from "../../services/crm-error-map.ts";
import type { ForecastCategory } from "../../types.ts";

export interface NewOpportunityModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly onCreated?: (opportunityId: string) => void;
}

export function NewOpportunityModal({
  isOpen,
  onClose,
  legalEntityId,
  onCreated
}: NewOpportunityModalProps): React.JSX.Element {
  const { t } = useTranslation("crm");
  const createMutation = useCreateOpportunityMutation(legalEntityId);
  const { data: pipelines = [] } = usePipelines(legalEntityId);

  const [name, setName] = useState("");
  const [selectedPipelineId, setSelectedPipelineId] = useState("");
  const [selectedStageId, setSelectedStageId] = useState("");
  const [estimatedAmount, setEstimatedAmount] = useState("50000");
  const [currencyCode, setCurrencyCode] = useState("EGP");
  const [expectedCloseDate, setExpectedCloseDate] = useState("");
  const [forecastCategory, setForecastCategory] = useState<ForecastCategory>("Pipeline");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const reset = () => {
    setName("");
    setEstimatedAmount("50000");
    setCurrencyCode("EGP");
    setExpectedCloseDate(new Date(Date.now() + 30 * 86400000).toISOString().slice(0, 10));
    setForecastCategory("Pipeline");
    setErrorMessage(null);
  };

  useEffect(() => {
    if (pipelines.length > 0 && !selectedPipelineId) {
      const defaultPipe = pipelines.find((p) => p.isDefault) || pipelines[0];
      if (defaultPipe) {
        setSelectedPipelineId(defaultPipe.id);
        const firstStage = defaultPipe.stages[0];
        if (firstStage) {
          setSelectedStageId(firstStage.id);
        }
      }
    }
  }, [pipelines, selectedPipelineId]);

  const activePipeline = pipelines.find((p) => p.id === selectedPipelineId);
  const stages = activePipeline?.stages || [];

  const handlePipelineChange = (pipeId: string) => {
    setSelectedPipelineId(pipeId);
    const pipe = pipelines.find((p) => p.id === pipeId);
    const firstStage = pipe?.stages[0];
    if (firstStage) {
      setSelectedStageId(firstStage.id);
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim()) {
      setErrorMessage(t("errors.opportunityNameRequired"));
      return;
    }
    if (!selectedPipelineId || !selectedStageId) {
      setErrorMessage(t("errors.pipelineAndStageRequired"));
      return;
    }

    try {
      const opp = await createMutation.mutateAsync({
        legalEntityId,
        name: name.trim(),
        pipelineId: selectedPipelineId,
        stageId: selectedStageId,
        estimatedAmount: Number(estimatedAmount) || 0,
        currencyCode,
        expectedCloseDate: expectedCloseDate || null,
        forecastCategory
      });

      reset();
      onClose();
      if (onCreated && opp.id) {
        onCreated(opp.id);
      }
    } catch (err: unknown) {
      const mapped = mapCrmError(err);
      setErrorMessage(t(mapped.messageKey, mapped.defaultMessage));
    }
  };

  const pipelineOptions = pipelines.map((p) => ({ id: p.id, label: p.name }));
  const stageOptions = stages.map((s) => ({ id: s.id, label: s.name }));

  const forecastCategoryOptions = [
    { id: "Pipeline", label: t("forecastCategories.Pipeline") },
    { id: "BestCase", label: t("forecastCategories.BestCase") },
    { id: "Commit", label: t("forecastCategories.Commit") }
  ];

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open: boolean) => {
        if (!open) {
          reset();
          onClose();
        }
      }}
      title={t("opportunities.newOpportunityModal.title")}
      size="medium"
      footer={
        <>
          <Button
            variant="secondary"
            onPress={() => {
              reset();
              onClose();
            }}
            isDisabled={createMutation.isPending}
          >
            {t("actions.cancel")}
          </Button>
          <Button
            variant="primary"
            type="submit"
            form="new-opportunity-form"
            isDisabled={createMutation.isPending}
          >
            {createMutation.isPending ? t("actions.saving") : t("opportunities.newOpportunityModal.submit")}
          </Button>
        </>
      }
    >
      <form id="new-opportunity-form" onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "0.875rem" }}>
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
          placeholder={t("opportunities.fields.namePlaceholder")}
        />

        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", gap: "0.75rem" }}>
          <Select
            label={t("opportunities.fields.pipeline")}
            selectedKey={selectedPipelineId}
            onSelectionChange={(k) => handlePipelineChange(String(k))}
            options={pipelineOptions}
          />
          <Select
            label={t("opportunities.fields.stage")}
            selectedKey={selectedStageId}
            onSelectionChange={(k) => setSelectedStageId(String(k))}
            options={stageOptions}
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", gap: "0.75rem" }}>
          <TextField
            label={t("opportunities.fields.estimatedAmount")}
            value={estimatedAmount}
            onChange={setEstimatedAmount}
            type="number"
            isRequired
          />
          <TextField
            label={t("opportunities.fields.currency")}
            value={currencyCode}
            onChange={setCurrencyCode}
            isRequired
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", gap: "0.75rem" }}>
          <TextField
            label={t("opportunities.fields.expectedCloseDate")}
            value={expectedCloseDate}
            onChange={setExpectedCloseDate}
            type="date"
          />
          <Select
            label={t("opportunities.fields.forecastCategory")}
            selectedKey={forecastCategory}
            onSelectionChange={(k) => setForecastCategory(k as ForecastCategory)}
            options={forecastCategoryOptions}
          />
        </div>
      </form>
    </Dialog>
  );
}

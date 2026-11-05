import React, { useState, useEffect } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Select } from "../../../../components/primitives/select.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { generateKey } from "../../../../core/api/http-client.ts";
import { useConvertLeadMutation, usePipelines } from "../../hooks/use-crm-queries.ts";
import { mapCrmError } from "../../services/crm-error-map.ts";
import type { Lead } from "../../types.ts";

export interface ConvertLeadModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly lead: Lead | null;
  readonly onConverted?: (opportunityId: string) => void;
}

export function ConvertLeadModal({
  isOpen,
  onClose,
  legalEntityId,
  lead,
  onConverted
}: ConvertLeadModalProps): React.JSX.Element {
  const { t } = useTranslation("crm");
  const convertMutation = useConvertLeadMutation(legalEntityId, lead?.id || "");
  const { data: pipelines = [] } = usePipelines(legalEntityId);

  const [partyMode, setPartyMode] = useState<"new" | "existing">("new");
  const [newPartyName, setNewPartyName] = useState("");
  const [newPartyType, setNewPartyType] = useState("Organization");
  const [selectedPipelineId, setSelectedPipelineId] = useState("");
  const [selectedStageId, setSelectedStageId] = useState("");
  const [opportunityName, setOpportunityName] = useState("");
  const [estimatedAmount, setEstimatedAmount] = useState("");
  const [currencyCode, setCurrencyCode] = useState("EGP");
  const [expectedCloseDate, setExpectedCloseDate] = useState("");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  useEffect(() => {
    if (lead) {
      setNewPartyName(lead.companyName || lead.displayName);
      setNewPartyType(lead.companyName ? "Organization" : "Individual");
      setOpportunityName(`فرصة - ${lead.companyName || lead.displayName}`);
      setEstimatedAmount("");
      setCurrencyCode("EGP");
      setExpectedCloseDate(new Date(Date.now() + 30 * 86400000).toISOString().slice(0, 10));
      setErrorMessage(null);
    }
  }, [lead]);

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
    if (!lead) return;
    if (!opportunityName.trim()) {
      setErrorMessage(t("errors.opportunityNameRequired"));
      return;
    }
    if (!selectedPipelineId || !selectedStageId) {
      setErrorMessage(t("errors.pipelineAndStageRequired"));
      return;
    }

    try {
      const opp = await convertMutation.mutateAsync({
        existingPartyId: partyMode === "existing" && lead.partyId ? lead.partyId : null,
        newPartyName: partyMode === "new" ? newPartyName.trim() : null,
        newPartyType: partyMode === "new" ? newPartyType : null,
        pipelineId: selectedPipelineId,
        stageId: selectedStageId,
        opportunityName: opportunityName.trim(),
        estimatedAmount: Number(estimatedAmount) || 0,
        currencyCode,
        expectedCloseDate: expectedCloseDate || null,
        idempotencyKey: generateKey(),
        version: lead.version
      });

      onClose();
      if (onConverted && opp.id) {
        onConverted(opp.id);
      }
    } catch (err: unknown) {
      const mapped = mapCrmError(err);
      setErrorMessage(t(mapped.messageKey, mapped.defaultMessage));
    }
  };

  const partyTypeOptions = [
    { id: "Organization", label: t("leads.convertModal.organization") },
    { id: "Individual", label: t("leads.convertModal.individual") }
  ];

  const pipelineOptions = pipelines.map((p) => ({ id: p.id, label: p.name }));
  const stageOptions = stages.map((s) => ({ id: s.id, label: s.name }));

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open: boolean) => {
        if (!open) onClose();
      }}
      title={t("leads.convertModal.title")}
      size="medium"
      footer={
        <>
          <Button
            variant="secondary"
            onPress={onClose}
            isDisabled={convertMutation.isPending}
          >
            {t("actions.cancel")}
          </Button>
          <Button
            variant="primary"
            type="submit"
            form="convert-lead-form"
            isDisabled={convertMutation.isPending}
          >
            {convertMutation.isPending ? t("actions.saving") : t("leads.convertModal.submit")}
          </Button>
        </>
      }
    >
      <form id="convert-lead-form" onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "0.875rem" }}>
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

        <div style={{ fontSize: "0.875rem", color: "var(--color-content-muted)" }}>
          {t("leads.convertModal.leadName")}: <strong>{lead?.displayName}</strong>
        </div>

        {/* Party Creation / Link */}
        <div style={{ border: "1px solid var(--color-border-default)", borderRadius: "var(--radius-md)", padding: "0.875rem", backgroundColor: "var(--color-surface-subtle)" }}>
          <span style={{ fontWeight: 600, fontSize: "0.875rem", display: "block", marginBottom: "0.5rem" }}>
            {t("leads.convertModal.partySection")}
          </span>
          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", gap: "0.75rem" }}>
            <TextField
              label={t("leads.convertModal.partyName")}
              value={newPartyName}
              onChange={setNewPartyName}
              isRequired
            />
            <Select
              label={t("leads.convertModal.partyType")}
              selectedKey={newPartyType}
              onSelectionChange={(k) => setNewPartyType(String(k))}
              options={partyTypeOptions}
            />
          </div>
        </div>

        {/* Opportunity Configuration */}
        <div style={{ border: "1px solid var(--color-border-default)", borderRadius: "var(--radius-md)", padding: "0.875rem", backgroundColor: "var(--color-surface-subtle)" }}>
          <span style={{ fontWeight: 600, fontSize: "0.875rem", display: "block", marginBottom: "0.5rem" }}>
            {t("leads.convertModal.opportunitySection")}
          </span>
          <div style={{ display: "flex", flexDirection: "column", gap: "0.75rem" }}>
            <TextField
              label={t("leads.convertModal.opportunityName")}
              value={opportunityName}
              onChange={setOpportunityName}
              isRequired
            />

            <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", gap: "0.75rem" }}>
              <Select
                label={t("leads.convertModal.pipeline")}
                selectedKey={selectedPipelineId}
                onSelectionChange={(k) => handlePipelineChange(String(k))}
                options={pipelineOptions}
              />
              <Select
                label={t("leads.convertModal.stage")}
                selectedKey={selectedStageId}
                onSelectionChange={(k) => setSelectedStageId(String(k))}
                options={stageOptions}
              />
            </div>

            <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", gap: "0.75rem" }}>
              <TextField
                label={t("leads.convertModal.estimatedAmount")}
                value={estimatedAmount}
                onChange={setEstimatedAmount}
                type="number"
                isRequired
              />
              <TextField
                label={t("leads.convertModal.expectedCloseDate")}
                value={expectedCloseDate}
                onChange={setExpectedCloseDate}
                type="date"
              />
            </div>
          </div>
        </div>
      </form>
    </Dialog>
  );
}

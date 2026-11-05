import React, { useState, useEffect } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Select } from "../../../../components/primitives/select.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { useAssignOpportunityMutation, useTeams } from "../../hooks/use-crm-queries.ts";
import { mapCrmError } from "../../services/crm-error-map.ts";
import type { Opportunity } from "../../types.ts";

export interface AssignOpportunityModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly opportunity: Opportunity | null;
  readonly onAssigned?: (() => void) | undefined;
}

export function AssignOpportunityModal({
  isOpen,
  onClose,
  legalEntityId,
  opportunity,
  onAssigned
}: AssignOpportunityModalProps): React.JSX.Element {
  const { t } = useTranslation("crm");
  const assignMutation = useAssignOpportunityMutation(legalEntityId, opportunity?.id || "");
  const { data: teams = [] } = useTeams(legalEntityId);

  const [selectedTeamId, setSelectedTeamId] = useState<string>("none");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  useEffect(() => {
    if (opportunity) {
      setSelectedTeamId(opportunity.teamId || "none");
      setErrorMessage(null);
    }
  }, [opportunity]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!opportunity) return;

    try {
      await assignMutation.mutateAsync({
        ownerId: opportunity.ownerId || null,
        teamId: selectedTeamId === "none" ? null : selectedTeamId,
        version: opportunity.version
      });

      onClose();
      onAssigned?.();
    } catch (err: unknown) {
      const mapped = mapCrmError(err);
      setErrorMessage(t(mapped.messageKey, mapped.defaultMessage));
    }
  };

  const teamOptions = [
    { id: "none", label: t("leads.assignment.noTeam") },
    ...teams.map((team) => ({ id: team.id, label: team.name }))
  ];

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open: boolean) => {
        if (!open) onClose();
      }}
      title={t("opportunities.assignment.modalTitle")}
      size="small"
      footer={
        <>
          <Button
            variant="secondary"
            onPress={onClose}
            isDisabled={assignMutation.isPending}
          >
            {t("actions.cancel")}
          </Button>
          <Button
            variant="primary"
            type="submit"
            form="assign-opp-form"
            isDisabled={assignMutation.isPending}
          >
            {assignMutation.isPending ? t("actions.saving") : t("opportunities.assignment.submit")}
          </Button>
        </>
      }
    >
      <form id="assign-opp-form" onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
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
          {t("opportunities.assignment.oppName")}: <strong>{opportunity?.name}</strong>
        </div>

        <Select
          label={t("opportunities.assignment.selectTeam")}
          selectedKey={selectedTeamId}
          onSelectionChange={(key) => setSelectedTeamId(String(key))}
          options={teamOptions}
        />
      </form>
    </Dialog>
  );
}

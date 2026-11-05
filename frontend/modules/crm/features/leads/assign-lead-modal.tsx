import React, { useState, useEffect } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Select } from "../../../../components/primitives/select.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { useAssignLeadMutation, useTeams } from "../../hooks/use-crm-queries.ts";
import { mapCrmError } from "../../services/crm-error-map.ts";
import type { Lead } from "../../types.ts";

export interface AssignLeadModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly lead: Lead | null;
  readonly onAssigned?: () => void;
}

export function AssignLeadModal({
  isOpen,
  onClose,
  legalEntityId,
  lead,
  onAssigned
}: AssignLeadModalProps): React.JSX.Element {
  const { t } = useTranslation("crm");
  const assignMutation = useAssignLeadMutation(legalEntityId, lead?.id || "");
  const { data: teams = [] } = useTeams(legalEntityId);

  const [selectedTeamId, setSelectedTeamId] = useState<string>("none");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  useEffect(() => {
    if (lead) {
      setSelectedTeamId(lead.teamId || "none");
      setErrorMessage(null);
    }
  }, [lead]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!lead) return;

    try {
      await assignMutation.mutateAsync({
        ownerId: lead.ownerId || null,
        teamId: selectedTeamId === "none" ? null : selectedTeamId,
        version: lead.version
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
      title={t("leads.assignment.modalTitle")}
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
            form="assign-lead-form"
            isDisabled={assignMutation.isPending}
          >
            {assignMutation.isPending ? t("actions.saving") : t("leads.assignment.submit")}
          </Button>
        </>
      }
    >
      <form id="assign-lead-form" onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
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
          {t("leads.assignment.leadName")}: <strong>{lead?.displayName}</strong>
        </div>

        <Select
          label={t("leads.assignment.selectTeam")}
          selectedKey={selectedTeamId}
          onSelectionChange={(key) => setSelectedTeamId(String(key))}
          options={teamOptions}
        />
      </form>
    </Dialog>
  );
}

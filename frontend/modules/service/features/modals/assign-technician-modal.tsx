import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { ServiceSelect as Select } from "../../components/service-select.tsx";
import { TextArea } from "../../../../components/primitives/text-area.tsx";
import { useAssignTechnicianMutation } from "../../hooks/use-service-queries.ts";

export interface AssignTechnicianModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly visitId: string;
}

export function AssignTechnicianModal({
  isOpen,
  onClose,
  legalEntityId,
  visitId
}: AssignTechnicianModalProps): React.JSX.Element {
  const { t } = useTranslation("service");
  const assignMutation = useAssignTechnicianMutation(legalEntityId);

  const [technicianId, setTechnicianId] = useState("");
  const [crewId, setCrewId] = useState("");
  const [role, setRole] = useState("lead");
  const [plannedHours, setPlannedHours] = useState("2");
  const [notes, setNotes] = useState("");
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!technicianId.trim() && !crewId.trim()) {
      setErrorMsg(t("validation.technicianOrCrewRequired"));
      return;
    }

    try {
      setErrorMsg(null);
      await assignMutation.mutateAsync({
        legalEntityId,
        visitId,
        technicianId: technicianId.trim() || undefined,
        crewId: crewId.trim() || undefined,
        role: role.trim() || undefined,
        plannedHours: plannedHours ? Number(plannedHours) : undefined,
        notes: notes.trim() || undefined
      });
      onClose();
    } catch (err: unknown) {
      setErrorMsg(err instanceof Error ? err.message : t("common.genericError"));
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("dispatch.assignTechnicianTitle")}
      size="medium"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {errorMsg && (
          <div
            style={{
              padding: "0.75rem 1rem",
              borderRadius: "var(--radius-sm, 0.375rem)",
              backgroundColor: "#fef2f2",
              border: "1px solid #fecaca",
              color: "#b91c1c",
              fontSize: "0.875rem"
            }}
          >
            {errorMsg}
          </div>
        )}

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
              {t("dispatch.technicianId")}
            </label>
            <TextField
              value={technicianId}
              onChange={(val) => setTechnicianId(val)}
              placeholder={t("dispatch.technicianPlaceholder", "TECH-001")}
            />
          </div>

          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
              {t("dispatch.crewId")}
            </label>
            <TextField
              value={crewId}
              onChange={(val) => setCrewId(val)}
              placeholder={t("dispatch.crewPlaceholder", "CREW-ALPHA")}
            />
          </div>
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
              {t("dispatch.role")}
            </label>
            <Select
              value={role}
              onChange={(val) => setRole(val)}
              options={[
                { value: "lead", label: t("dispatch.roles.lead") },
                { value: "assistant", label: t("dispatch.roles.assistant") },
                { value: "inspector", label: t("dispatch.roles.inspector") }
              ]}
            />
          </div>

          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
              {t("dispatch.plannedHours")}
            </label>
            <TextField
              value={plannedHours}
              onChange={(val) => setPlannedHours(val)}
              placeholder="2.0"
            />
          </div>
        </div>

        <div>
          <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
            {t("requests.description")}
          </label>
          <TextArea
            value={notes}
            onChange={(val: any) => setNotes(typeof val === 'string' ? val : val?.target?.value ?? '')}
            placeholder={t("dispatch.assignNotesPlaceholder")}
            rows={2}
          />
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "1rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("common.cancel")}
          </Button>
          <Button variant="primary" type="submit" isLoading={assignMutation.isPending}>
            {t("dispatch.confirmAssignment")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

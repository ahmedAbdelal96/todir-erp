import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Select } from "../../../../components/primitives/select.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { generateKey } from "../../../../core/api/http-client.ts";
import { useCreateActivityMutation } from "../../hooks/use-crm-queries.ts";
import { mapCrmError } from "../../services/crm-error-map.ts";
import type { ActivityType } from "../../types.ts";

export interface NewActivityModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly leadId?: string | null;
  readonly opportunityId?: string | null;
  readonly partyId?: string | null;
  readonly onCreated?: () => void;
}

export function NewActivityModal({
  isOpen,
  onClose,
  legalEntityId,
  leadId,
  opportunityId,
  partyId,
  onCreated
}: NewActivityModalProps): React.JSX.Element {
  const { t } = useTranslation("crm");
  const createMutation = useCreateActivityMutation(legalEntityId);

  const [type, setType] = useState<ActivityType>("Call");
  const [subject, setSubject] = useState("");
  const [dueAt, setDueAt] = useState(new Date(Date.now() + 86400000).toISOString().slice(0, 16));
  const [notes, setNotes] = useState("");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const reset = () => {
    setType("Call");
    setSubject("");
    setDueAt(new Date(Date.now() + 86400000).toISOString().slice(0, 16));
    setNotes("");
    setErrorMessage(null);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!subject.trim()) {
      setErrorMessage(t("errors.activitySubjectRequired"));
      return;
    }

    try {
      await createMutation.mutateAsync({
        legalEntityId,
        leadId: leadId || null,
        opportunityId: opportunityId || null,
        partyId: partyId || null,
        type,
        subject: subject.trim(),
        dueAt: new Date(dueAt).toISOString(),
        notes: notes.trim() || null,
        idempotencyKey: generateKey()
      });

      reset();
      onClose();
      onCreated?.();
    } catch (err: unknown) {
      const mapped = mapCrmError(err);
      setErrorMessage(t(mapped.messageKey, mapped.defaultMessage));
    }
  };

  const typeOptions = [
    { id: "Call", label: t("activityTypes.Call") },
    { id: "Meeting", label: t("activityTypes.Meeting") },
    { id: "FollowUp", label: t("activityTypes.FollowUp") },
    { id: "Task", label: t("activityTypes.Task") },
    { id: "Note", label: t("activityTypes.Note") }
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
      title={t("activities.newModal.title")}
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
            form="new-activity-form"
            isDisabled={createMutation.isPending}
          >
            {createMutation.isPending ? t("actions.saving") : t("activities.newModal.submit")}
          </Button>
        </>
      }
    >
      <form id="new-activity-form" onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "0.875rem" }}>
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

        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", gap: "0.75rem" }}>
          <Select
            label={t("activities.fields.type")}
            selectedKey={type}
            onSelectionChange={(k) => setType(k as ActivityType)}
            options={typeOptions}
          />
          <TextField
            label={t("activities.fields.dueAt")}
            value={dueAt}
            onChange={setDueAt}
            type="datetime-local"
            isRequired
          />
        </div>

        <TextField
          label={t("activities.fields.subject")}
          value={subject}
          onChange={setSubject}
          isRequired
          placeholder={t("activities.fields.subjectPlaceholder")}
        />

        <TextField
          label={t("activities.fields.notes")}
          value={notes}
          onChange={setNotes}
          placeholder={t("activities.fields.notesPlaceholder")}
        />
      </form>
    </Dialog>
  );
}

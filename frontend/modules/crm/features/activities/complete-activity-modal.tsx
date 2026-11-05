import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Checkbox } from "../../../../components/primitives/checkbox.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { useCompleteActivityMutation } from "../../hooks/use-crm-queries.ts";
import { mapCrmError } from "../../services/crm-error-map.ts";
import type { Activity } from "../../types.ts";

export interface CompleteActivityModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly activity: Activity | null;
  readonly onCompleted?: () => void;
}

export function CompleteActivityModal({
  isOpen,
  onClose,
  legalEntityId,
  activity,
  onCompleted
}: CompleteActivityModalProps): React.JSX.Element {
  const { t } = useTranslation("crm");
  const completeMutation = useCompleteActivityMutation(legalEntityId, activity?.id || "");

  const [outcome, setOutcome] = useState("");
  const [notes, setNotes] = useState("");
  const [createNextAction, setCreateNextAction] = useState(false);
  const [nextSubject, setNextSubject] = useState("");
  const [nextDueAt, setNextDueAt] = useState(new Date(Date.now() + 3 * 86400000).toISOString().slice(0, 16));
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const reset = () => {
    setOutcome("");
    setNotes("");
    setCreateNextAction(false);
    setNextSubject("");
    setNextDueAt(new Date(Date.now() + 3 * 86400000).toISOString().slice(0, 16));
    setErrorMessage(null);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!activity) return;

    try {
      await completeMutation.mutateAsync({
        version: activity.version,
        outcome: outcome.trim() || null,
        notes: notes.trim() || null,
        createNextAction,
        nextDueAt: createNextAction && nextDueAt ? new Date(nextDueAt).toISOString() : null,
        nextSubject: createNextAction ? nextSubject.trim() || null : null
      });

      reset();
      onClose();
      onCompleted?.();
    } catch (err: unknown) {
      const mapped = mapCrmError(err);
      setErrorMessage(t(mapped.messageKey, mapped.defaultMessage));
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open: boolean) => {
        if (!open) {
          reset();
          onClose();
        }
      }}
      title={t("activities.completeModal.title")}
      size="medium"
      footer={
        <>
          <Button
            variant="secondary"
            onPress={() => {
              reset();
              onClose();
            }}
            isDisabled={completeMutation.isPending}
          >
            {t("actions.cancel")}
          </Button>
          <Button
            variant="primary"
            type="submit"
            form="complete-activity-form"
            isDisabled={completeMutation.isPending}
            style={{ backgroundColor: "var(--color-status-success)", borderColor: "var(--color-status-success)" }}
          >
            {completeMutation.isPending ? t("actions.saving") : t("activities.completeModal.submit")}
          </Button>
        </>
      }
    >
      <form id="complete-activity-form" onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "0.875rem" }}>
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

        <div style={{ backgroundColor: "var(--color-surface-subtle)", padding: "0.75rem", borderRadius: "var(--radius-sm)" }}>
          <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
            {t("activities.fields.subject")}:
          </span>
          <div style={{ fontWeight: 600, fontSize: "0.9375rem" }}>
            {activity?.subject}
          </div>
        </div>

        <TextField
          label={t("activities.completeModal.outcome")}
          value={outcome}
          onChange={setOutcome}
          placeholder={t("activities.completeModal.outcomePlaceholder")}
        />

        <TextField
          label={t("activities.completeModal.notes")}
          value={notes}
          onChange={setNotes}
          placeholder={t("activities.completeModal.notesPlaceholder")}
        />

        {/* Next Action Checkbox & Fields */}
        <div style={{ border: "1px solid var(--color-border-default)", borderRadius: "var(--radius-md)", padding: "0.875rem", backgroundColor: "var(--color-surface-subtle)" }}>
          <Checkbox
            isSelected={createNextAction}
            onChange={setCreateNextAction}
          >
            <span style={{ fontWeight: 600, fontSize: "0.875rem" }}>
              {t("activities.completeModal.createNextAction")}
            </span>
          </Checkbox>

          {createNextAction && (
            <div style={{ display: "flex", flexDirection: "column", gap: "0.75rem", marginTop: "0.75rem" }}>
              <TextField
                label={t("activities.completeModal.nextSubject")}
                value={nextSubject}
                onChange={setNextSubject}
                placeholder={t("activities.completeModal.nextSubjectPlaceholder")}
                isRequired
              />
              <TextField
                label={t("activities.completeModal.nextDueAt")}
                value={nextDueAt}
                onChange={setNextDueAt}
                type="datetime-local"
                isRequired
              />
            </div>
          )}
        </div>
      </form>
    </Dialog>
  );
}

import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { ServiceSelect as Select } from "../../components/service-select.tsx";
import { TextArea } from "../../../../components/primitives/text-area.tsx";
import { useScheduleVisitMutation } from "../../hooks/use-service-queries.ts";
import type { VisitType } from "../../types.ts";

export interface NewVisitModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly workOrderId: string;
  readonly workOrderNumber?: string;
}

export function NewVisitModal({
  isOpen,
  onClose,
  legalEntityId,
  workOrderId,
  workOrderNumber
}: NewVisitModalProps): React.JSX.Element {
  const { t } = useTranslation("service");
  const scheduleMutation = useScheduleVisitMutation(legalEntityId);

  const now = new Date();
  const defaultStart = new Date(now.getTime() + 60 * 60 * 1000).toISOString().slice(0, 16);
  const defaultEnd = new Date(now.getTime() + 3 * 60 * 60 * 1000).toISOString().slice(0, 16);

  const [visitType, setVisitType] = useState<VisitType>("onsite");
  const [scheduledStart, setScheduledStart] = useState(defaultStart);
  const [scheduledEnd, setScheduledEnd] = useState(defaultEnd);
  const [notes, setNotes] = useState("");
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!scheduledStart || !scheduledEnd) {
      setErrorMsg(t("validation.datesRequired"));
      return;
    }

    try {
      setErrorMsg(null);
      await scheduleMutation.mutateAsync({
        legalEntityId,
        workOrderId,
        visitType,
        scheduledStartUtc: new Date(scheduledStart).toISOString(),
        scheduledEndUtc: new Date(scheduledEnd).toISOString(),
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
      title={t("dispatch.scheduleVisitTitle")}
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

        {workOrderNumber && (
          <div
            style={{
              padding: "0.75rem 1rem",
              borderRadius: "var(--radius-md, 0.5rem)",
              backgroundColor: "var(--color-surface-subtle, #f8fafc)",
              border: "1px solid var(--color-border-subtle, #e2e8f0)",
              fontSize: "0.875rem",
              fontWeight: 600,
              color: "var(--color-content-primary, #0f172a)"
            }}
          >
            {t("workOrders.title")}: {workOrderNumber}
          </div>
        )}

        <div>
          <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
            {t("dispatch.visitType")} *
          </label>
          <Select
            value={visitType}
            onChange={(val) => setVisitType(val as VisitType)}
            options={[
              { value: "onsite", label: t("dispatch.types.onsite") },
              { value: "depot", label: t("dispatch.types.depot") },
              { value: "remote", label: t("dispatch.types.remote") }
            ]}
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
              {t("dispatch.scheduledStart")} *
            </label>
            <input
              type="datetime-local"
              value={scheduledStart}
              onChange={(e) => setScheduledStart(e.target.value)}
              style={{
                width: "100%",
                padding: "0.5rem 0.75rem",
                borderRadius: "var(--radius-md, 0.375rem)",
                border: "1px solid var(--color-border-default, #cbd5e1)",
                backgroundColor: "#ffffff",
                fontSize: "0.875rem",
                boxSizing: "border-box"
              }}
              required
            />
          </div>

          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
              {t("dispatch.scheduledEnd")} *
            </label>
            <input
              type="datetime-local"
              value={scheduledEnd}
              onChange={(e) => setScheduledEnd(e.target.value)}
              style={{
                width: "100%",
                padding: "0.5rem 0.75rem",
                borderRadius: "var(--radius-md, 0.375rem)",
                border: "1px solid var(--color-border-default, #cbd5e1)",
                backgroundColor: "#ffffff",
                fontSize: "0.875rem",
                boxSizing: "border-box"
              }}
              required
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
            placeholder={t("dispatch.visitNotesPlaceholder")}
            rows={3}
          />
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "1rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("common.cancel")}
          </Button>
          <Button variant="primary" type="submit" isLoading={scheduleMutation.isPending}>
            {t("dispatch.scheduleVisit")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

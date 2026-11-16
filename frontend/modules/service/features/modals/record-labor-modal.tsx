import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { ServiceSelect as Select } from "../../components/service-select.tsx";
import { TextArea } from "../../../../components/primitives/text-area.tsx";
import { useRecordLaborMutation } from "../../hooks/use-service-queries.ts";
import type { ActivityType } from "../../types.ts";

export interface RecordLaborModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly workOrderId: string;
  readonly defaultVisitId?: string | undefined;
  readonly defaultTechnicianId?: string | undefined;
}

export function RecordLaborModal({
  isOpen,
  onClose,
  legalEntityId,
  workOrderId,
  defaultVisitId,
  defaultTechnicianId
}: RecordLaborModalProps): React.JSX.Element {
  const { t } = useTranslation("service");
  const laborMutation = useRecordLaborMutation(legalEntityId);

  const [visitId, setVisitId] = useState(defaultVisitId || "");
  const [technicianId, setTechnicianId] = useState(defaultTechnicianId || "TECH-001");
  const [activityType, setActivityType] = useState<ActivityType>("repair");
  const [hours, setHours] = useState("1.5");
  const [hourlyRate, setHourlyRate] = useState("150");
  const [currency, setCurrency] = useState("SAR");
  const [isBillable, setIsBillable] = useState(true);
  const [notes, setNotes] = useState("");
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!technicianId.trim()) {
      setErrorMsg(t("validation.technicianRequired"));
      return;
    }

    const h = Number(hours);
    if (isNaN(h) || h <= 0) {
      setErrorMsg(t("validation.invalidHours"));
      return;
    }

    try {
      setErrorMsg(null);
      await laborMutation.mutateAsync({
        legalEntityId,
        workOrderId,
        visitId: visitId.trim() || undefined,
        technicianId: technicianId.trim(),
        activityType,
        hours: h,
        hourlyRate: Number(hourlyRate) || 0,
        currency: currency.trim() || "SAR",
        isBillable,
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
      title={t("labor.recordLaborTitle")}
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
              {t("dispatch.technicianId")} *
            </label>
            <TextField
              value={technicianId}
              onChange={(val) => setTechnicianId(val)}
              placeholder={t("labor.technicianPlaceholder", "TECH-001")}
              isRequired
            />
          </div>

          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
              {t("labor.activityType")} *
            </label>
            <Select
              value={activityType}
              onChange={(val) => setActivityType(val as ActivityType)}
              options={[
                { value: "travel", label: t("labor.activities.travel") },
                { value: "diagnosis", label: t("labor.activities.diagnosis") },
                { value: "repair", label: t("labor.activities.repair") },
                { value: "testing", label: t("labor.activities.testing") },
                { value: "handover", label: t("labor.activities.handover") }
              ]}
            />
          </div>
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
              {t("labor.hours")} *
            </label>
            <TextField
              value={hours}
              onChange={(val) => setHours(val)}
              placeholder="1.5"
              isRequired
            />
          </div>

          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
              {t("labor.hourlyRate")} ({currency})
            </label>
            <TextField
              value={hourlyRate}
              onChange={(val) => setHourlyRate(val)}
              placeholder="150"
            />
          </div>
        </div>

        <div style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
          <input
            type="checkbox"
            id="billable-check"
            checked={isBillable}
            onChange={(e) => setIsBillable(e.target.checked)}
            style={{ width: "1rem", height: "1rem", accentColor: "#2563eb", cursor: "pointer" }}
          />
          <label htmlFor="billable-check" style={{ fontSize: "0.875rem", cursor: "pointer", fontWeight: 500 }}>
            {t("labor.isBillable")}
          </label>
        </div>

        <div>
          <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
            {t("requests.description")}
          </label>
          <TextArea
            value={notes}
            onChange={(val: any) => setNotes(typeof val === 'string' ? val : val?.target?.value ?? '')}
            placeholder={t("labor.laborNotesPlaceholder")}
            rows={2}
          />
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "1rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("common.cancel")}
          </Button>
          <Button variant="primary" type="submit" isLoading={laborMutation.isPending}>
            {t("labor.recordLabor")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

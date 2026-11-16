import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { ServiceSelect as Select } from "../../components/service-select.tsx";
import { useRecordMeterReadingMutation } from "../../hooks/use-service-queries.ts";

export interface RecordMeterModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly installedBaseItemId: string;
  readonly serialNumber?: string | undefined;
}

export function RecordMeterModal({
  isOpen,
  onClose,
  legalEntityId,
  installedBaseItemId,
  serialNumber
}: RecordMeterModalProps): React.JSX.Element {
  const { t } = useTranslation("service");
  const recordMutation = useRecordMeterReadingMutation(legalEntityId);

  const [meterType, setMeterType] = useState("operating_hours");
  const [readingValue, setReadingValue] = useState("");
  const [readingDate, setReadingDate] = useState(new Date().toISOString().slice(0, 10));
  const [source, setSource] = useState("manual");
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const val = Number(readingValue);
    if (isNaN(val) || val < 0) {
      setErrorMsg(t("validation.invalidReadingValue"));
      return;
    }

    try {
      setErrorMsg(null);
      await recordMutation.mutateAsync({
        legalEntityId,
        installedBaseItemId,
        meterType,
        readingValue: val,
        readingDateUtc: new Date(readingDate).toISOString(),
        source
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
      title={t("installedBase.recordMeterTitle")}
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

        {serialNumber && (
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
            {t("installedBase.serialNumber")}: {serialNumber}
          </div>
        )}

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
              {t("installedBase.meterType")} *
            </label>
            <Select
              value={meterType}
              onChange={(val) => setMeterType(val)}
              options={[
                { value: "operating_hours", label: t("installedBase.meterTypes.operatingHours") },
                { value: "cycles", label: t("installedBase.meterTypes.cycles") },
                { value: "kilometers", label: t("installedBase.meterTypes.kilometers") },
                { value: "production_units", label: t("installedBase.meterTypes.units") }
              ]}
            />
          </div>

          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
              {t("installedBase.readingValue")} *
            </label>
            <TextField
              value={readingValue}
              onChange={(val) => setReadingValue(val)}
              placeholder={t("meters.readingPlaceholder", "1250")}
              isRequired
            />
          </div>
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
              {t("installedBase.readingDate")} *
            </label>
            <input
              type="date"
              value={readingDate}
              onChange={(e) => setReadingDate(e.target.value)}
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
              {t("installedBase.source")}
            </label>
            <Select
              value={source}
              onChange={(val) => setSource(val)}
              options={[
                { value: "manual", label: t("installedBase.sources.manual") },
                { value: "telemetry", label: t("installedBase.sources.telemetry") },
                { value: "technician", label: t("installedBase.sources.technician") }
              ]}
            />
          </div>
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "1rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("common.cancel")}
          </Button>
          <Button variant="primary" type="submit" isLoading={recordMutation.isPending}>
            {t("installedBase.saveReading")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

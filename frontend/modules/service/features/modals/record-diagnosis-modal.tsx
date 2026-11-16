import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { TextArea } from "../../../../components/primitives/text-area.tsx";
import { useRecordDiagnosisMutation } from "../../hooks/use-service-queries.ts";

export interface RecordDiagnosisModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly workOrderId: string;
  readonly visitId?: string | undefined;
}

export function RecordDiagnosisModal({
  isOpen,
  onClose,
  legalEntityId,
  workOrderId,
  visitId
}: RecordDiagnosisModalProps): React.JSX.Element {
  const { t } = useTranslation("service");
  const diagnosisMutation = useRecordDiagnosisMutation(legalEntityId);

  const [symptom, setSymptom] = useState("");
  const [failureCode, setFailureCode] = useState("");
  const [rootCause, setRootCause] = useState("");
  const [resolutionCode, setResolutionCode] = useState("");
  const [notes, setNotes] = useState("");
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!symptom.trim() && !failureCode.trim()) {
      setErrorMsg(t("validation.symptomOrFailureCodeRequired"));
      return;
    }

    try {
      setErrorMsg(null);
      await diagnosisMutation.mutateAsync({
        legalEntityId,
        workOrderId,
        visitId,
        symptom: symptom.trim() || undefined,
        failureCode: failureCode.trim() || undefined,
        rootCause: rootCause.trim() || undefined,
        resolutionCode: resolutionCode.trim() || undefined,
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
      title={t("workOrders.recordDiagnosisTitle")}
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

        <div>
          <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
            {t("workOrders.symptom")} *
          </label>
          <TextField
            value={symptom}
            onChange={(val) => setSymptom(val)}
            placeholder={t("workOrders.symptomPlaceholder")}
            isRequired
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
              {t("workOrders.failureCode")}
            </label>
            <TextField
              value={failureCode}
              onChange={(val) => setFailureCode(val)}
              placeholder={t("diagnosis.faultCodePlaceholder", "ERR-MOTOR-01")}
            />
          </div>

          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
              {t("workOrders.resolutionCode")}
            </label>
            <TextField
              value={resolutionCode}
              onChange={(val) => setResolutionCode(val)}
              placeholder={t("diagnosis.resolutionCodePlaceholder", "RES-REPLACED")}
            />
          </div>
        </div>

        <div>
          <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
            {t("workOrders.rootCause")}
          </label>
          <TextField
            value={rootCause}
            onChange={(val) => setRootCause(val)}
            placeholder={t("workOrders.rootCausePlaceholder")}
          />
        </div>

        <div>
          <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
            {t("requests.description")}
          </label>
          <TextArea
            value={notes}
            onChange={(val: any) => setNotes(typeof val === 'string' ? val : val?.target?.value ?? '')}
            placeholder={t("workOrders.diagnosisNotesPlaceholder")}
            rows={3}
          />
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "1rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("common.cancel")}
          </Button>
          <Button variant="primary" type="submit" isLoading={diagnosisMutation.isPending}>
            {t("workOrders.saveDiagnosis")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

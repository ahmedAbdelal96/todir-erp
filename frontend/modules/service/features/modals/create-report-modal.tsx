import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { TextArea } from "../../../../components/primitives/text-area.tsx";
import { useCreateReportMutation } from "../../hooks/use-service-queries.ts";

export interface CreateReportModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly workOrderId: string;
}

export function CreateReportModal({
  isOpen,
  onClose,
  legalEntityId,
  workOrderId
}: CreateReportModalProps): React.JSX.Element {
  const { t } = useTranslation("service");
  const reportMutation = useCreateReportMutation(legalEntityId);

  const [summary, setSummary] = useState("");
  const [customerRep, setCustomerRep] = useState("");
  const [customerNotes, setCustomerNotes] = useState("");
  const [isSigned, setIsSigned] = useState(true);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!summary.trim()) {
      setErrorMsg(t("validation.summaryRequired"));
      return;
    }

    try {
      setErrorMsg(null);
      await reportMutation.mutateAsync({
        legalEntityId,
        workOrderId,
        summary: summary.trim(),
        customerRepresentative: customerRep.trim() || undefined,
        customerNotes: customerNotes.trim() || undefined,
        signatureCaptured: isSigned
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
      title={t("reports.createReportTitle")}
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
            {t("reports.summary")} *
          </label>
          <TextArea
            value={summary}
            onChange={(val: any) => setSummary(typeof val === 'string' ? val : val?.target?.value ?? '')}
            placeholder={t("reports.summaryPlaceholder")}
            rows={3}
            isRequired
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
              {t("reports.customerRepresentative")}
            </label>
            <TextField
              value={customerRep}
              onChange={(val) => setCustomerRep(val)}
              placeholder={t("reports.representativePlaceholder", "م. خالد منصور")}
            />
          </div>

          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
              {t("reports.customerNotes")}
            </label>
            <TextField
              value={customerNotes}
              onChange={(val) => setCustomerNotes(val)}
              placeholder={t("reports.notesPlaceholder")}
            />
          </div>
        </div>

        <div style={{ display: "flex", alignItems: "center", gap: "0.5rem", marginTop: "0.5rem" }}>
          <input
            type="checkbox"
            id="report-signature-check"
            checked={isSigned}
            onChange={(e) => setIsSigned(e.target.checked)}
            style={{ width: "1rem", height: "1rem", accentColor: "#059669", cursor: "pointer" }}
          />
          <label htmlFor="report-signature-check" style={{ fontSize: "0.875rem", cursor: "pointer", fontWeight: 500 }}>
            {t("reports.signatureCaptured")}
          </label>
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "1rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("common.cancel")}
          </Button>
          <Button variant="primary" type="submit" isLoading={reportMutation.isPending}>
            {t("reports.submitReport")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

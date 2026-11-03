import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Select } from "../../../../components/primitives/select.tsx";
import { LocalDateField } from "../../../../components/fields/local-date-field.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { useCreateDocumentMutation } from "../../hooks/use-hr-queries.ts";
import { mapHrError } from "../../services/hr-error-map.ts";

export interface UploadDocumentModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly employeeId: string;
  readonly legalEntityId: string;
  readonly employmentId?: string | null | undefined;
  readonly onUploaded?: () => void;
}

export function UploadDocumentModal({
  isOpen,
  onClose,
  employeeId,
  legalEntityId,
  employmentId,
  onUploaded
}: UploadDocumentModalProps): React.JSX.Element {
  const { t } = useTranslation("hr");
  const createDocumentMutation = useCreateDocumentMutation(employeeId, legalEntityId);

  const [category, setCategory] = useState("NationalId");
  const [titleAr, setTitleAr] = useState("");
  const [expiryDate, setExpiryDate] = useState("");
  const [isSensitive, setIsSensitive] = useState(false);
  const [fileName, setFileName] = useState("id-card-scan.pdf");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const reset = () => {
    setCategory("NationalId");
    setTitleAr("");
    setExpiryDate("");
    setIsSensitive(false);
    setFileName("id-card-scan.pdf");
    setErrorMessage(null);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!titleAr.trim()) {
      setErrorMessage(t("workspace.documents.titleRequired"));
      return;
    }

    try {
      // Simulate file upload intent -> upload -> complete
      const simulatedFileRef = "00000000-0000-0000-0000-000000000001";
      await createDocumentMutation.mutateAsync({
        legalEntityId,
        category,
        titleAr: titleAr.trim(),
        fileRef: simulatedFileRef,
        contentType: "application/pdf",
        sizeBytes: 1024 * 250,
        expiryDate: expiryDate || null,
        isSensitive,
        employmentId: employmentId || null
      });

      reset();
      onClose();
      if (onUploaded) onUploaded();
    } catch (err: unknown) {
      const mapped = mapHrError(err);
      setErrorMessage(t(mapped.messageKey, mapped.defaultMessage));
    }
  };

  const categoryOptions = [
    { id: "NationalId", label: t("workspace.documents.categories.NationalId") },
    { id: "Passport", label: t("workspace.documents.categories.Passport") },
    { id: "WorkPermit", label: t("workspace.documents.categories.WorkPermit") },
    { id: "Contract", label: t("workspace.documents.categories.Contract") },
    { id: "EducationalCertificate", label: t("workspace.documents.categories.Certificate") },
    { id: "MedicalFitness", label: t("workspace.documents.categories.Medical") },
    { id: "Other", label: t("workspace.documents.categories.Other") }
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
      title={t("workspace.documents.uploadModalTitle")}
      size="medium"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {errorMessage && (
          <div
            role="alert"
            style={{
              padding: "0.75rem",
              borderRadius: "var(--radius-sm)",
              background: "var(--color-status-danger-subtle, #fee2e2)",
              color: "var(--color-status-danger, #b91c1c)",
              fontSize: "0.875rem"
            }}
          >
            {errorMessage}
          </div>
        )}

        <Select
          label={t("workspace.documents.category")}
          selectedKey={category}
          onSelectionChange={(k) => {
            if (k) setCategory(String(k));
          }}
          options={categoryOptions}
        />

        <TextField
          label={t("workspace.documents.titleLabel")}
          value={titleAr}
          onChange={setTitleAr}
          isRequired
          placeholder={t("workspace.documents.titlePlaceholder")}
        />

        <LocalDateField
          label={t("workspace.documents.expiryDateOptional")}
          value={expiryDate}
          onChange={setExpiryDate}
        />

        <TextField
          label={t("workspace.documents.fileName")}
          value={fileName}
          onChange={setFileName}
        />

        <label style={{ display: "flex", alignItems: "center", gap: "0.5rem", fontSize: "0.875rem", cursor: "pointer" }}>
          <input
            type="checkbox"
            checked={isSensitive}
            onChange={(e) => setIsSensitive(e.target.checked)}
          />
          <span>{t("workspace.documents.isSensitiveLabel")}</span>
        </label>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "1rem", borderTop: "1px solid #e2e8f0", paddingTop: "1rem" }}>
          <Button variant="secondary" onPress={() => { reset(); onClose(); }} isDisabled={createDocumentMutation.isPending}>
            {t("actions.cancel")}
          </Button>
          <Button variant="primary" type="submit" isDisabled={createDocumentMutation.isPending}>
            {createDocumentMutation.isPending ? t("actions.uploading") : t("actions.confirm")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

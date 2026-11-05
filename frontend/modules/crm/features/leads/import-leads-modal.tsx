import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { useImportLeadsMutation } from "../../hooks/use-crm-queries.ts";
import { mapCrmError } from "../../services/crm-error-map.ts";
import type { ImportLeadsResult } from "../../types.ts";

export interface ImportLeadsModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly onImported?: () => void;
}

const SAMPLE_CSV = `displayname,companyname,email,phone,source,priority
طارق محمود حامد,مؤسسة النور للتجارة,tarek@alnoor.test,01012345678,Website,High
ياسر عبد الرحمن,شركة البناء الحديث,yasser@benaa.test,01123456789,Exhibition,Normal
سارة إبراهيم,مجموعة الأفق,sara@ofok.test,01234567890,Referral,Normal`;

export function ImportLeadsModal({
  isOpen,
  onClose,
  legalEntityId,
  onImported
}: ImportLeadsModalProps): React.JSX.Element {
  const { t } = useTranslation("crm");
  const importMutation = useImportLeadsMutation(legalEntityId);

  const [csvContent, setCsvContent] = useState("");
  const [result, setResult] = useState<ImportLeadsResult | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const reset = () => {
    setCsvContent("");
    setResult(null);
    setErrorMessage(null);
  };

  const handlePasteSample = () => {
    setCsvContent(SAMPLE_CSV);
    setErrorMessage(null);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!csvContent.trim()) {
      setErrorMessage(t("errors.importEmpty"));
      return;
    }

    try {
      const res = await importMutation.mutateAsync({
        csv: csvContent.trim()
      });

      setResult(res);
      if (res.imported > 0) {
        onImported?.();
      }
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
      title={t("leads.importModal.title")}
      size="medium"
      footer={
        <>
          <Button
            variant="secondary"
            onPress={() => {
              reset();
              onClose();
            }}
            isDisabled={importMutation.isPending}
          >
            {result ? t("actions.close") : t("actions.cancel")}
          </Button>
          {!result && (
            <Button
              variant="primary"
              type="submit"
              form="import-leads-form"
              isDisabled={importMutation.isPending || !csvContent.trim()}
            >
              {importMutation.isPending ? t("actions.importing") : t("leads.importModal.submit")}
            </Button>
          )}
        </>
      }
    >
      {result ? (
        <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
          <div
            style={{
              padding: "1rem",
              borderRadius: "var(--radius-md)",
              backgroundColor: "var(--color-status-success-subtle, #ecfdf5)",
              border: "1px solid #a7f3d0",
              color: "var(--color-status-success, #059669)"
            }}
          >
            <h4 style={{ margin: "0 0 0.5rem", fontSize: "1rem" }}>{t("leads.importModal.successTitle")}</h4>
            <p style={{ margin: 0, fontSize: "0.875rem" }}>
              {t("leads.importModal.importedCount", { count: result.imported })}
            </p>
            {result.duplicateCandidates > 0 && (
              <p style={{ margin: "0.25rem 0 0", fontSize: "0.8125rem", color: "var(--color-status-warning, #d97706)" }}>
                {t("leads.importModal.duplicatesDetected", { count: result.duplicateCandidates })}
              </p>
            )}
          </div>

          {result.errors.length > 0 && (
            <div style={{ maxHeight: "150px", overflowY: "auto", border: "1px solid var(--color-border-default)", borderRadius: "var(--radius-sm)", padding: "0.5rem" }}>
              <span style={{ fontWeight: 600, fontSize: "0.8125rem", color: "var(--color-status-danger)" }}>
                {t("leads.importModal.errorsList")}:
              </span>
              <ul style={{ margin: "0.25rem 0 0", paddingInlineStart: "1.25rem", fontSize: "0.75rem", color: "var(--color-status-danger)" }}>
                {result.errors.map((err, i) => (
                  <li key={i}>{err}</li>
                ))}
              </ul>
            </div>
          )}
        </div>
      ) : (
        <form id="import-leads-form" onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "0.875rem" }}>
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

          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
            <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
              {t("leads.importModal.csvInstruction")}
            </span>
            <Button variant="secondary" size="compact" onPress={handlePasteSample}>
              {t("leads.importModal.pasteSample")}
            </Button>
          </div>

          <textarea
            value={csvContent}
            onChange={(e) => setCsvContent(e.target.value)}
            rows={8}
            placeholder={t("leads.importModal.textareaPlaceholder")}
            style={{
              width: "100%",
              padding: "0.75rem",
              borderRadius: "var(--radius-md)",
              border: "1px solid var(--color-border-default)",
              fontFamily: "monospace",
              fontSize: "0.8125rem",
              direction: "ltr",
              boxSizing: "border-box"
            }}
          />
        </form>
      )}
    </Dialog>
  );
}

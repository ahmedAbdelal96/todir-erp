import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { LocalDateField } from "../../../../components/fields/local-date-field.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { useCreateContractAmendmentMutation } from "../../hooks/use-hr-queries.ts";
import { mapHrError } from "../../services/hr-error-map.ts";

export interface RenewContractModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly employmentId: string;
  readonly contractId: string;
  readonly legalEntityId: string;
  readonly onRenewed?: () => void;
}

export function RenewContractModal({
  isOpen,
  onClose,
  employmentId,
  contractId,
  legalEntityId,
  onRenewed
}: RenewContractModalProps): React.JSX.Element {
  const { t } = useTranslation("hr");
  const createAmendmentMutation = useCreateContractAmendmentMutation(employmentId, contractId, legalEntityId);

  const [effectiveDate, setEffectiveDate] = useState(new Date().toISOString().slice(0, 10));
  const [termsSummary, setTermsSummary] = useState("");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const reset = () => {
    setEffectiveDate(new Date().toISOString().slice(0, 10));
    setTermsSummary("");
    setErrorMessage(null);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!termsSummary.trim()) {
      setErrorMessage(t("workspace.contracts.renewalTermsRequired"));
      return;
    }

    try {
      await createAmendmentMutation.mutateAsync({
        amendmentType: "Renewal",
        effectiveDate,
        termsSummary: termsSummary.trim()
      });
      reset();
      onClose();
      if (onRenewed) onRenewed();
    } catch (err: unknown) {
      const mapped = mapHrError(err);
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
      title={t("workspace.contracts.renewModalTitle")}
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

        <LocalDateField
          label={t("workspace.contracts.renewalEffectiveDate")}
          value={effectiveDate}
          onChange={setEffectiveDate}
          isRequired
        />

        <TextField
          label={t("workspace.contracts.renewalTermsSummary")}
          value={termsSummary}
          onChange={setTermsSummary}
          isRequired
          placeholder={t("workspace.contracts.renewalTermsPlaceholder")}
        />

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "1rem", borderTop: "1px solid #e2e8f0", paddingTop: "1rem" }}>
          <Button variant="secondary" onPress={() => { reset(); onClose(); }} isDisabled={createAmendmentMutation.isPending}>
            {t("actions.cancel")}
          </Button>
          <Button variant="primary" type="submit" isDisabled={createAmendmentMutation.isPending}>
            {createAmendmentMutation.isPending ? t("actions.saving") : t("workspace.contracts.renewConfirm")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

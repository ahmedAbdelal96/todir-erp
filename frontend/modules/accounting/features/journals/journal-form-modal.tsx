import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { useCreateJournal } from "../../hooks/use-ledger-queries.ts";
import { mapLedgerError } from "../../services/ledger-error-map.ts";
import type { CreateJournalRequest, JournalType } from "../../types.ts";

export interface JournalFormModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
}

export function JournalFormModal({
  isOpen,
  onClose,
  legalEntityId
}: JournalFormModalProps): React.JSX.Element | null {
  const { t, i18n } = useTranslation("accounting");
  const createJournalMutation = useCreateJournal();

  const [code, setCode] = useState("");
  const [nameAr, setNameAr] = useState("");
  const [nameEn, setNameEn] = useState("");
  const [type, setType] = useState<JournalType>("General");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const currentLang = i18n.language?.startsWith("ar") ? "ar" : "en";

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMessage(null);

    if (!code.trim() || !nameAr.trim() || !nameEn.trim()) {
      setErrorMessage(t("account.validationRequiredFields"));
      return;
    }

    const request: CreateJournalRequest = {
      code: code.trim().toUpperCase(),
      nameAr: nameAr.trim(),
      nameEn: nameEn.trim(),
      type
    };

    try {
      await createJournalMutation.mutateAsync({ legalEntityId, request });
      onClose();
      setCode("");
      setNameAr("");
      setNameEn("");
    } catch (err) {
      setErrorMessage(mapLedgerError(err, currentLang));
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open) => !open && onClose()}
      title={t("journals.createNew")}
      size="medium"
      footer={
        <div style={{ display: "flex", gap: "0.75rem", justifyContent: "flex-end" }}>
          <Button variant="secondary" onPress={onClose}>
            {t("common.cancel")}
          </Button>
          <Button
            variant="primary"
            isLoading={createJournalMutation.isPending}
            onPress={handleSubmit as unknown as () => void}
          >
            {t("common.save")}
          </Button>
        </div>
      }
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        <TextField
          label={t("journals.code")}
          value={code}
          onChange={setCode}
          isRequired
        />

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <TextField
            label={t("journals.nameAr")}
            value={nameAr}
            onChange={setNameAr}
            isRequired
          />
          <TextField
            label={t("journals.nameEn")}
            value={nameEn}
            onChange={setNameEn}
            isRequired
          />
        </div>

        <div>
          <label style={{ display: "block", marginBottom: "0.25rem", fontSize: "0.875rem", fontWeight: 600 }}>
            {t("journals.type")}
          </label>
          <select
            value={type}
            onChange={(e) => setType(e.target.value as JournalType)}
            style={{
              width: "100%",
              padding: "0.5rem 0.75rem",
              borderRadius: "var(--radius-md, 0.5rem)",
              border: "1px solid var(--color-border-default)",
              backgroundColor: "var(--color-surface-card)"
            }}
          >
            <option value="General">{t("journalTypes.General")}</option>
            <option value="Sales">{t("journalTypes.Sales")}</option>
            <option value="Purchases">{t("journalTypes.Purchases")}</option>
            <option value="Bank">{t("journalTypes.Bank")}</option>
            <option value="Cash">{t("journalTypes.Cash")}</option>
            <option value="Inventory">{t("journalTypes.Inventory")}</option>
            <option value="Payroll">{t("journalTypes.Payroll")}</option>
            <option value="Opening">{t("journalTypes.Opening")}</option>
            <option value="Fx">{t("journalTypes.Fx")}</option>
            <option value="Tax">{t("journalTypes.Tax")}</option>
          </select>
        </div>

        {errorMessage && (
          <div
            style={{
              padding: "0.75rem",
              borderRadius: "var(--radius-md, 0.5rem)",
              backgroundColor: "var(--color-status-danger-subtle, #fee2e2)",
              color: "var(--color-status-danger, #b91c1c)",
              fontSize: "0.875rem"
            }}
          >
            {errorMessage}
          </div>
        )}
      </form>
    </Dialog>
  );
}
